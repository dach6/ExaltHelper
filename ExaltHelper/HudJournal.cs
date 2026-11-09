using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper;

internal sealed class HudLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Time { get; set; } = DateTime.Now;
    public string Category { get; set; }
    public string Sender { get; set; }
    public string Recipient { get; set; }
    public string Text { get; set; }
    public string Location { get; set; }
    public string Server { get; set; }
    public string Realm { get; set; }
    public string LocationId { get; set; }
    public string CalloutKind { get; set; }
    public string CalloutName { get; set; }
    public bool IsDungeon { get; set; }
    public string DungeonModifiers { get; set; }
    public int? OriginRealmPercent { get; set; }
    public bool? OpenedInNexus { get; set; }
    public string[] LearnedEventModifiers { get; set; } = Array.Empty<string>();
    public DateTime? AvailableUntilUtc { get; set; }
    public bool CalloutEstimated { get; set; }
    public HudFeedLoot FeedLoot { get; set; }
    public string KeyPopTier { get; set; }
}

internal sealed class HudLootRecord
{
    public string Id { get; set; }
    public string BagId { get; set; }
    public int BagSlot { get; set; } = -1;
    public DateTime Time { get; set; }
    public int ItemId { get; set; }
    public string Item { get; set; }
    public string Rarity { get; set; }
    public bool Shiny { get; set; }
    public string Bag { get; set; }
    public string Enemy { get; set; }
    public string Dungeon { get; set; }
    public string Character { get; set; }
    public string Enchantments { get; set; }
}

internal sealed class HudDamageSource
{
    public int ItemId { get; set; }
    public string Kind { get; set; }
    public long Damage { get; set; }
    public long ConfirmedDamage { get; set; }
    public int Attack { get; set; }
    public int Dexterity { get; set; }
    public int Vitality { get; set; }
    public int Wisdom { get; set; }
    public int Effects { get; set; }
    public int Effects2 { get; set; }
    public double EventMultiplier { get; set; }
}

internal sealed class HudTrophyRecord
{
    public int ItemId { get; set; }
    public long Count { get; set; }
    public DateTime First { get; set; }
    public DateTime Last { get; set; }
    public string FirstDungeon { get; set; }
    public string LastDungeon { get; set; }
}

internal sealed class HudFightRecord
{
    public string Id { get; set; }
    public DateTime Time { get; set; }
    public string Dungeon { get; set; }
    public string Enemy { get; set; }
    public bool Defeated { get; set; }
    public int ObjectType { get; set; }
    public int? MaxHp { get; set; }
    public int? CurrentHp { get; set; }
    public int[] EncounterEntityIds { get; set; } = Array.Empty<int>();
    public bool IsBoss { get; set; }
    public double Seconds { get; set; }
    public double DungeonSeconds { get; set; }
    public long Damage { get; set; }
    public string Character { get; set; }
    public int[] Equipment { get; set; }
    public List<PlayerDamageEntry> Players { get; set; } = new List<PlayerDamageEntry>();
    public Dictionary<string, long> DamageTaken { get; set; } = new Dictionary<string, long>();
    public List<HudDamageSource> Sources { get; set; } = new List<HudDamageSource>();
    public string EstimateNote { get; set; }
}

internal sealed class HudJournalData
{
    public List<HudLogEntry> Chat { get; set; } = new List<HudLogEntry>();
    public List<HudLogEntry> Feed { get; set; } = new List<HudLogEntry>();
    public List<HudLootRecord> Loot { get; set; } = new List<HudLootRecord>();
    public List<HudFightRecord> Fights { get; set; } = new List<HudFightRecord>();
    public List<HudTrophyRecord> Trophies { get; set; } = new List<HudTrophyRecord>();
    public Dictionary<string, long> DungeonRuns { get; set; } = new Dictionary<string, long>();
    public List<string> RecentLootIds { get; set; } = new List<string>();
    public List<string> RecentRunIds { get; set; } = new List<string>();
    public List<string> RecentFightIds { get; set; } = new List<string>();
    public int CollectionVersion { get; set; }
}

// Account-scoped history. Packet observers never issue game commands or save raw packets.
internal static partial class HudJournal
{
    [Flags]
    private enum JournalChanges { None = 0, Feed = 1, Chat = 2, Loot = 4, Combat = 8, Collections = 16, All = 31 }
    private static int feedRevision, chatRevision, lootRevision, combatRevision, collectionRevision;
    private static readonly object Gate = new object();
    private static readonly object WriteGate = new object();
    private static readonly Dictionary<string, int> writtenRevisions = new Dictionary<string, int>();
    private sealed class PendingSave { public string Profile; public int Revision; public HudJournalData Data; }
    private static readonly Dictionary<string, PendingSave> pendingSaves = new Dictionary<string, PendingSave>();
    private sealed class EventDelta
    {
        public readonly Dictionary<string, HudLootRecord> Loot = new Dictionary<string, HudLootRecord>();
        public readonly Dictionary<string, string> Runs = new Dictionary<string, string>();
        public void Include(EventDelta other)
        {
            foreach (var drop in other.Loot) Loot[drop.Key] = drop.Value;
            foreach (var run in other.Runs) Runs[run.Key] = run.Value;
        }
    }
    private sealed class PendingLoad
    {
        public string Profile;
        public HudJournalData Data;
        public EventDelta Events;
        public Task Completion;
    }
    private static readonly Dictionary<string, PendingLoad> pendingLoads = new Dictionary<string, PendingLoad>();
    private static EventDelta unboundEvents = new EventDelta();
    private static HudJournalData data = new HudJournalData();
    private static string profile;
    private static string currentAccountKey;
    private static string scopeId = Guid.NewGuid().ToString("N");
    private static readonly HashSet<string> seenLoot = new HashSet<string>();
    private static readonly HashSet<string> seenRuns = new HashSet<string>();
    private static readonly HashSet<string> seenFights = new HashSet<string>();
    private static bool dirty;
    private static int revision;
    private static readonly Dictionary<string, List<HudDamageSource>> sources = new Dictionary<string, List<HudDamageSource>>();
    private static readonly Dictionary<int, Dictionary<string, long>> damageTaken = new Dictionary<int, Dictionary<string, long>>();
    private static readonly Timer saveTimer = new Timer(_ => Flush(), null, 10000, 10000);
    public static int Revision { get { lock (Gate) return revision; } }
    public static string ScopeId { get { lock (Gate) return scopeId; } }
    public static string Map { get; private set; } = "Waiting for game";
    public static string Realm { get; private set; } = "";
    public static string Server { get; private set; } = "";
    public static int RealmScore { get; private set; } = -1;
    public static int MaxRealmScore { get; private set; } = -1;
    public static DateTime LastPacketUtc { get; private set; }
    public static int Latency { get; private set; }
    public static DpsSnapshot LiveSnapshot { get; private set; } = new DpsSnapshot();

    public static void BeginConnection()
    {
        lock (Gate)
        {
            QueueCurrent(); profile = currentAccountKey = null; data = new HudJournalData();
            unboundEvents = new EventDelta();
            scopeId = Guid.NewGuid().ToString("N"); seenLoot.Clear(); seenRuns.Clear(); seenFights.Clear();
            sources.Clear(); damageTaken.Clear(); LiveSnapshot = new DpsSnapshot(); dirty = false; revision++; TouchPages(JournalChanges.All);
            Map = "Waiting for game"; Realm = Server = ""; RealmScore = MaxRealmScore = -1; LastPacketUtc = default; Latency = 0;
            ResetCalloutState();
        }
    }

    public static void UseAccount(string accountKey)
    {
        if (string.IsNullOrWhiteSpace(accountKey)) return;
        lock (Gate) if (currentAccountKey == accountKey) return;
        using var hash = SHA256.Create();
        string next = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(accountKey))).Replace("-", "").Substring(0, 24);
        lock (Gate)
        {
            if (profile == next) return;
            QueueCurrent();
            var pending = profile == null ? data : null;
            var pendingEvents = profile == null ? unboundEvents : null;
            profile = next;
            currentAccountKey = accountKey;
            bool queued = pendingSaves.TryGetValue(profile, out var saved);
            PendingLoad loading = null;
            bool cold = !queued && !pendingLoads.TryGetValue(profile, out loading);
            data = queued ? CloneData(saved.Data) : cold ? new HudJournalData() : loading.Data;
            if (pending != null) data = MergeBuffered(data, pending, pendingEvents);
            if (cold)
            {
                loading = new PendingLoad { Profile = profile, Data = data, Events = pendingEvents ?? new EventDelta() };
                pendingLoads[profile] = loading;
                loading.Completion = Task.Run(() => LoadProfile(loading));
            }
            else if (loading != null)
            {
                loading.Data = data;
                if (pendingEvents != null) loading.Events.Include(pendingEvents);
            }
            unboundEvents = new EventDelta();
            NormalizeCollections();
            if (pending == null) { scopeId = Guid.NewGuid().ToString("N"); sources.Clear(); damageTaken.Clear(); LiveSnapshot = new DpsSnapshot(); ResetCalloutState(); }
            dirty = true; revision++; TouchPages(JournalChanges.All);
        }
    }

    private static void LoadProfile(PendingLoad loading)
    {
        // Storage may be busy for arbitrarily long; no journal lock is held here.
        var stored = HudStorage.Load("journal-" + loading.Profile + ".json", new HudJournalData()) ?? new HudJournalData();
        NormalizeData(stored);
        lock (Gate)
        {
            var merged = MergeBuffered(stored, loading.Data, loading.Events);
            pendingLoads.Remove(loading.Profile);
            revision++;
            if (profile == loading.Profile)
            {
                data = merged; NormalizeCollections(); dirty = true; TouchPages(JournalChanges.All);
            }
            pendingSaves[loading.Profile] = new PendingSave { Profile = loading.Profile, Revision = revision, Data = CloneData(merged) };
        }
    }

    private static void WaitForLoads()
    {
        while (true)
        {
            Task[] loads; lock (Gate) loads = pendingLoads.Values.Select(x => x.Completion).ToArray();
            if (loads.Length == 0) return;
            Task.WaitAll(loads);
        }
    }

    private static EventDelta CurrentEvents => profile == null ? unboundEvents : pendingLoads.TryGetValue(profile, out var loading) ? loading.Events : null;

    public static void ObservePacket(Client client, Packet packet, bool fromClient)
    {
        lock (Gate)
        {
            LastPacketUtc = DateTime.UtcNow;
            if (fromClient) { ObservePortalUse(packet); return; }
            ObserveCalloutStats(client, packet);
            if (packet is MapInfoPacket map)
            {
                ObserveLocation(client, map);
            }
            else if (packet is TextPacket chat)
            {
                string category = ChatCategory(chat.Name, chat.Recipient);
                var entry = new HudLogEntry { Category = category, Sender = chat.Name, Recipient = chat.Recipient, Text = chat.Text, Location = Map, Server = Server, Realm = Realm };
                StampMessageContext(entry);
                HudSounds.PlayChat(chat.Name, chat.Recipient, chat.Text, client?.Player?.PlayerName, category);
                if (entry.CalloutKind == "Event") HudSounds.PlayEvent(entry.CalloutName);
                ObserveRealmMessage(chat.Name, chat.Text);
                if (HudPreferences.Current.RecordChat) { data.Chat.Add(entry); Trim(data.Chat); MarkDirty(JournalChanges.Chat); }
                if (category == "Whisper" || category == "Game" || entry.CalloutKind == "Event" || (chat.Name ?? "").IndexOf("Oryx", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var feedEntry = entry.CalloutKind == "Event" ? CloneLog(entry) : entry;
                    if (entry.CalloutKind == "Event") feedEntry.Category = "Event";
                    data.Feed.Add(feedEntry); Trim(data.Feed, 500); MarkDirty(JournalChanges.Feed);
                }
            }
            else if (packet is NotificationPacket notice && !string.IsNullOrEmpty(notice.Message))
            {
                if (notice.NotificationType != 8 || !RecordKeyPop(notice)) AppendFeed("Notice", notice.Message);
            }
            else if (packet is NotificationPacket notification)
            {
                byte kind = notification.NotificationType;
                if (kind <= 4 || kind == 9 || kind == 10 || kind == 12)
                {
                    using var reader = new PacketReader(new MemoryStream(packet.GetPayloadBytes()));
                    reader.ReadByte(); reader.ReadByte();
                    string message = reader.ReadString();
                    if (!string.IsNullOrEmpty(message)) AppendFeed(kind == 2 || kind == 9 ? "Alert" : "Notice", message);
                }
            }
            else if (packet.Id == 169)
            {
                byte[] payload = packet.GetPayloadBytes();
                if (payload.Length == 4) { using var reader = new PacketReader(new MemoryStream(payload)); RealmScore = reader.ReadInt32(); revision++; }
            }
            else if (packet is NewTickPacket tick) Latency = tick.ServerLastRTTMS;
            else if (packet is EnemyShootPacket hit && client?.Entities != null && client.Entities.TryGetValue(hit.TargetId, out var target) && target.IsPlayer)
            {
                string player = string.IsNullOrEmpty(target.PlayerName) ? target.Name : target.PlayerName;
                if (string.IsNullOrEmpty(player)) return;
                if (!damageTaken.TryGetValue(hit.AttackerId, out var totals)) damageTaken[hit.AttackerId] = totals = new Dictionary<string, long>();
                totals.TryGetValue(player, out long total); totals[player] = total + hit.DamageAmount;
            }
        }
    }

    public static string ChatCategory(string sender, string recipient)
    {
        sender ??= ""; recipient ??= "";
        if (sender.StartsWith("#g:", StringComparison.OrdinalIgnoreCase) || sender.Equals("Guild", StringComparison.OrdinalIgnoreCase)
            || recipient.Equals("Guild", StringComparison.OrdinalIgnoreCase) || recipient.Equals("*Guild*", StringComparison.OrdinalIgnoreCase)) return "Guild";
        if (sender.StartsWith("#p:", StringComparison.OrdinalIgnoreCase) || sender.Equals("Party", StringComparison.OrdinalIgnoreCase)
            || recipient.Equals("Party", StringComparison.OrdinalIgnoreCase) || recipient.Equals("*Party*", StringComparison.OrdinalIgnoreCase)) return "Party";
        if (recipient.Length > 0) return "Whisper";
        return sender.Length == 0 || sender.StartsWith("#") ? "Game" : "Realm";
    }

    public static void AppendFeed(string category, string text)
    {
        lock (Gate)
        {
            data.Feed.Add(new HudLogEntry { Category = category, Text = text, Location = Map, Server = Server, Realm = Realm });
            if (category == "Dust") HudSounds.Play("Dust");
            Trim(data.Feed, 500); MarkDirty(JournalChanges.Feed);
        }
    }

    public static void RecordSource(DateTime start, int targetId, int itemId, string kind, int delta, int confirmedDelta, MapObject player, double eventMultiplier)
        => RecordSourceContext(start, targetId, new HudDamageSource { ItemId = itemId, Kind = kind, Attack = player?.Attack ?? 0, Dexterity = player?.Dexterity ?? 0,
            Vitality = player?.Vitality ?? 0, Wisdom = player?.Wisdom ?? 0, Effects = player?.Effects ?? 0, Effects2 = player?.Effects2 ?? 0, EventMultiplier = eventMultiplier }, delta, confirmedDelta);

    public static void RecordSourceContext(DateTime start, int targetId, HudDamageSource context, int delta, int confirmedDelta)
    {
        if (context == null || !HudPreferences.Current.RecordCombat || (delta == 0 && confirmedDelta == 0)) return;
        lock (Gate)
        {
            string key = FightKey(start, targetId);
            if (!sources.TryGetValue(key, out var entries)) sources[key] = entries = new List<HudDamageSource>();
            var row = entries.FirstOrDefault(x => x.ItemId == context.ItemId && x.Kind == context.Kind && x.Attack == context.Attack && x.Effects == context.Effects
                && x.Effects2 == context.Effects2 && x.EventMultiplier == context.EventMultiplier && x.Dexterity == context.Dexterity
                && x.Vitality == context.Vitality && x.Wisdom == context.Wisdom);
            if (row == null)
            {
                row = CloneSource(context); row.Damage = row.ConfirmedDamage = 0;
                entries.Add(row);
            }
            row.Damage += delta; row.ConfirmedDamage += confirmedDelta;
        }
    }

    public static string FightKey(DateTime start, int entityId) => start.Ticks + ":" + entityId;

    public static void ObserveSnapshot(DpsSnapshot snapshot)
    {
        if (snapshot == null || snapshot.HistoryIndex >= 0) return;
        lock (Gate)
        {
            var changes = JournalChanges.None;
            if (LiveSnapshot.DungeonStartTime != snapshot.DungeonStartTime)
                foreach (string stale in sources.Keys.Where(x => !x.StartsWith(snapshot.DungeonStartTime.Ticks + ":", StringComparison.Ordinal)).ToArray()) sources.Remove(stale);
            LiveSnapshot = snapshot;
            if (HudPreferences.Current.RecordCombat)
            {
                var enemies = (snapshot.Encounters ?? new List<EnemyCombatSnapshot>()).Concat(snapshot.DefeatedEnemies);
                if (snapshot.ActiveEnemy != null) enemies = enemies.Concat(new[] { snapshot.ActiveEnemy });
                foreach (var enemy in enemies.Where(x => x.TotalDamage > 0 || data.Fights.Any(f => f.Id == FightKey(snapshot.DungeonStartTime, x.EncounterId >= 0 ? x.EncounterId : x.EntityId))).GroupBy(x => x.EncounterId >= 0 ? x.EncounterId : x.EntityId).Select(x => x.First()))
                {
                    string key = FightKey(snapshot.DungeonStartTime, enemy.EncounterId >= 0 ? enemy.EncounterId : enemy.EntityId);
                    var fight = data.Fights.FirstOrDefault(x => x.Id == key);
                    if (fight == null)
                    {
                        // Existing records keep accepting late damage corrections. A filtered
                        // live fight can qualify later when another player joins the encounter.
                        if (!HudCombatPolicy.ShouldRecord(HudPreferences.Current.Combat, snapshot, enemy)) continue;
                        if (!seenFights.Add(key)) continue;
                        data.RecentFightIds.Add(key);
                        fight = new HudFightRecord { Id = key, Time = snapshot.Timestamp, Dungeon = snapshot.DungeonName, Enemy = enemy.Name,
                            Character = snapshot.LocalPlayer.Name + " · " + snapshot.LocalPlayer.ClassName, Equipment = (int[])snapshot.LocalPlayer.EquipmentIds.Clone() };
                        data.Fights.Add(fight);
                    }
                    changes |= JournalChanges.Combat;
                    fight.Defeated = enemy.IsDead; fight.IsBoss = enemy.IsBoss; fight.Seconds = enemy.FightDurationSeconds;
                    fight.DungeonSeconds = snapshot.ElapsedDungeonSeconds;
                    fight.Enemy = enemy.Name;
                    fight.ObjectType = enemy.ObjectType;
                    fight.MaxHp = enemy.MaxHp > 0 ? (int?)enemy.MaxHp : null;
                    fight.CurrentHp = enemy.MaxHp > 0 ? (int?)enemy.CurrentHp : null;
                    fight.EncounterEntityIds = (int[])(enemy.EncounterEntityIds ?? Array.Empty<int>()).Clone();
                    var priorPlayers = fight.Players;
                    fight.Damage = enemy.TotalDamage; fight.Players = enemy.Damagers.Select(ClonePlayer).ToList(); fight.EstimateNote = snapshot.DamageEstimateNote;
                    foreach (var player in fight.Players.Where(p => p.EquipmentIds.Length == 0))
                    {
                        var previous = priorPlayers.FirstOrDefault(p => string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase));
                        if (previous == null) continue;
                        player.ClassType = previous.ClassType; player.SkinType = previous.SkinType;
                        player.EquipmentIds = (int[])(previous.EquipmentIds ?? Array.Empty<int>()).Clone();
                    }
                    var taken = new Dictionary<string, long>();
                    foreach (int entityId in enemy.EncounterEntityIds.Length > 0 ? enemy.EncounterEntityIds : new[] { enemy.EntityId })
                        if (damageTaken.TryGetValue(entityId, out var phase)) foreach (var player in phase) { taken.TryGetValue(player.Key, out long total); taken[player.Key] = total + player.Value; }
                    fight.DamageTaken = taken;
                    if (sources.TryGetValue(key, out var split)) fight.Sources = split.Select(CloneSource).ToList();
                    string runId = snapshot.DungeonStartTime.Ticks.ToString();
                    if (enemy.IsDead && seenRuns.Add(runId))
                    {
                        changes |= JournalChanges.Collections;
                        data.RecentRunIds.Add(runId);
                        string dungeon = snapshot.DungeonName ?? "Unknown";
                        data.DungeonRuns.TryGetValue(dungeon, out long count); data.DungeonRuns[dungeon] = count + 1;
                        var events = CurrentEvents; if (events != null) events.Runs[runId] = dungeon;
                    }
                }
                int fightCount = data.Fights.Count;
                Trim(data.Fights);
                if (fightCount != data.Fights.Count) changes |= JournalChanges.Combat;
            }
            if (HudPreferences.Current.RecordLoot)
            {
                foreach (var bag in snapshot.LootBags)
                {
                    if (bag.JournalScopeId == null)
                    {
                        bag.JournalScopeId = scopeId; bag.Dungeon = snapshot.DungeonName; bag.DungeonStartTime = snapshot.DungeonStartTime;
                        bag.Character = snapshot.LocalPlayer.Name + " · " + snapshot.LocalPlayer.ClassName;
                    }
                    if (RecordLootCore(bag)) changes |= JournalChanges.Loot | JournalChanges.Collections;
                }
                int lootCount = data.Loot.Count;
                Trim(data.Loot);
                if (lootCount != data.Loot.Count) changes |= JournalChanges.Loot;
            }
            TrimLedgers();
            MarkDirty(changes);
        }
    }
    public static void RecordLoot(LootBagDrop bag)
    {
        if (bag == null || !HudPreferences.Current.RecordLoot) return;
        lock (Gate) { if (RecordLootCore(bag)) { Trim(data.Loot); TrimLedgers(); MarkDirty(JournalChanges.Loot | JournalChanges.Collections); } }
    }
    private static bool RecordLootCore(LootBagDrop bag)
    {
        if (bag.JournalScopeId != scopeId || string.IsNullOrEmpty(bag.DropId)) return false;
        // Keep the existing per-item event ID for discovery/deduplication. The
        // separate bag identity is scoped to this connection and map instance.
        string bagId = bag.JournalScopeId + ":" + (bag.DungeonStartTime == default ? 0 : bag.DungeonStartTime.ToUniversalTime().Ticks)
            .ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + bag.DropId;
        bool added = false;
        for (int slot = 0; slot < bag.ItemIds.Count; slot++)
        {
            int id = bag.ItemIds[slot]; if (id <= 0) continue;
            string key = bag.DropId + ":" + slot;
            if (!seenLoot.Add(key)) continue;
            data.RecentLootIds.Add(key); added = true;
            var meta = ItemSpriteManager.GetItemMetadata(id);
            var record = new HudLootRecord { Id = key, BagId = bagId, BagSlot = slot, Time = bag.DropTime, ItemId = id, Item = meta.Name,
                Rarity = bag.ItemRarities.ElementAtOrDefault(slot) ?? meta.Rarity, Shiny = bag.ItemIsShiny.ElementAtOrDefault(slot),
                Bag = bag.BagType, Enemy = bag.SourceMonster, Dungeon = bag.Dungeon, Character = bag.Character,
                Enchantments = string.Join(", ", bag.ItemEnchants.ElementAtOrDefault(slot) ?? new List<string>()) };
            data.Loot.Add(record); AddDiscovery(record);
            var events = CurrentEvents; if (events != null) events.Loot[key] = record;
        }
        return added;
    }
    private static void AddDiscovery(HudLootRecord drop) => AddDiscovery(data, drop);
    private static void AddDiscovery(HudJournalData destination, HudLootRecord drop)
    {
        if (drop.Bag == "Brown Bag") return;
        var trophy = destination.Trophies.FirstOrDefault(x => x.ItemId == drop.ItemId);
        if (trophy == null) { trophy = new HudTrophyRecord { ItemId = drop.ItemId, First = drop.Time, Last = drop.Time, FirstDungeon = drop.Dungeon, LastDungeon = drop.Dungeon }; destination.Trophies.Add(trophy); }
        trophy.Count++;
        if (drop.Time.ToUniversalTime() < trophy.First.ToUniversalTime()) { trophy.First = drop.Time; trophy.FirstDungeon = drop.Dungeon; }
        if (drop.Time.ToUniversalTime() >= trophy.Last.ToUniversalTime()) { trophy.Last = drop.Time; trophy.LastDungeon = drop.Dungeon; }
    }
    private static void NormalizeCollections()
    {
        NormalizeData(data);
        seenLoot.Clear(); seenRuns.Clear(); seenFights.Clear();
        seenLoot.UnionWith(data.RecentLootIds); seenRuns.UnionWith(data.RecentRunIds); seenFights.UnionWith(data.RecentFightIds);
    }
    private static void NormalizeData(HudJournalData value)
    {
        value.Chat ??= new List<HudLogEntry>(); value.Feed ??= new List<HudLogEntry>();
        value.Loot ??= new List<HudLootRecord>(); value.Fights ??= new List<HudFightRecord>();
        value.Loot.RemoveAll(x => x == null || string.IsNullOrEmpty(x.Id) || x.ItemId <= 0);
        value.Fights.RemoveAll(x => x == null || string.IsNullOrEmpty(x.Id));
        value.Chat.RemoveAll(x => x == null); value.Feed.RemoveAll(x => x == null);
        foreach (var entry in value.Chat.Concat(value.Feed)) if (string.IsNullOrEmpty(entry.Id)) entry.Id = Guid.NewGuid().ToString("N");
        foreach (var fight in value.Fights)
        {
            fight.Players ??= new List<PlayerDamageEntry>(); fight.Players.RemoveAll(x => x == null);
            fight.Sources ??= new List<HudDamageSource>(); fight.Sources.RemoveAll(x => x == null);
            fight.DamageTaken ??= new Dictionary<string, long>(); fight.Equipment ??= Array.Empty<int>();
        }
        value.Trophies ??= new List<HudTrophyRecord>(); value.DungeonRuns ??= new Dictionary<string, long>();
        value.Trophies.RemoveAll(x => x == null || x.ItemId <= 0);
        value.RecentLootIds ??= new List<string>(); value.RecentRunIds ??= new List<string>(); value.RecentFightIds ??= new List<string>();
        if (value.CollectionVersion < 1)
        {
            if (value.Trophies.Count == 0) foreach (var drop in value.Loot) AddDiscovery(value, drop);
            if (value.DungeonRuns.Count == 0) foreach (var run in value.Fights.Where(x => x.Defeated).GroupBy(x => x.Id.Split(':')[0]))
            {
                string dungeon = run.First().Dungeon ?? "Unknown";
                value.DungeonRuns.TryGetValue(dungeon, out long count); value.DungeonRuns[dungeon] = count + 1;
                value.RecentRunIds.Add(run.Key);
            }
            value.CollectionVersion = 1;
        }
        value.RecentLootIds = DistinctLedger(value.RecentLootIds.Concat(value.Loot.Select(x => x.Id)), 16000);
        value.RecentRunIds = DistinctLedger(value.RecentRunIds, 5000);
        value.RecentFightIds = DistinctLedger(value.RecentFightIds.Concat(value.Fights.Select(x => x.Id)), 50000);
    }
    private static List<string> DistinctLedger(IEnumerable<string> ids, int limit)
    {
        var values = ids.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        if (values.Count > limit) values.RemoveRange(0, values.Count - limit);
        return values;
    }

    private static HudJournalData MergeBuffered(HudJournalData stored, HudJournalData buffered, EventDelta events)
    {
        NormalizeData(stored); NormalizeData(buffered);
        var lootIds = new HashSet<string>(stored.RecentLootIds);
        var runIds = new HashSet<string>(stored.RecentRunIds);
        var fightIds = new HashSet<string>(stored.RecentFightIds);
        stored.Chat = stored.Chat.Concat(buffered.Chat).GroupBy(x => x.Id).Select(g => g.Last()).ToList();
        stored.Feed = stored.Feed.Concat(buffered.Feed).GroupBy(x => x.Id).Select(g => g.Last()).ToList();
        var fights = stored.Fights.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Last());
        foreach (var fight in buffered.Fights)
            if (fights.ContainsKey(fight.Id) || !fightIds.Contains(fight.Id)) fights[fight.Id] = fight;
        stored.Fights = fights.Values.OrderBy(x => x.Time.ToUniversalTime()).ToList();
        foreach (var drop in buffered.Loot) if (!lootIds.Contains(drop.Id)) stored.Loot.Add(drop);
        // Collection totals may outlive detailed rows. Keep every event observed
        // during loading separately until its identity can be checked against disk.
        foreach (var drop in events.Loot.Values) if (lootIds.Add(drop.Id)) AddDiscovery(stored, drop);
        foreach (var run in events.Runs) if (runIds.Add(run.Key))
        {
            stored.DungeonRuns.TryGetValue(run.Value, out long count); stored.DungeonRuns[run.Value] = count + 1;
        }
        stored.RecentLootIds = DistinctLedger(stored.RecentLootIds.Concat(buffered.RecentLootIds).Concat(events.Loot.Keys), 16000);
        stored.RecentRunIds = DistinctLedger(stored.RecentRunIds.Concat(buffered.RecentRunIds).Concat(events.Runs.Keys), 5000);
        stored.RecentFightIds = DistinctLedger(stored.RecentFightIds.Concat(buffered.RecentFightIds), 50000);
        // JavaScriptSerializer restores UTC values while live observations use
        // local time; compare instants so retention keeps the newest events.
        stored.Chat = stored.Chat.OrderBy(x => x.Time.ToUniversalTime()).ToList(); stored.Feed = stored.Feed.OrderBy(x => x.Time.ToUniversalTime()).ToList();
        stored.Loot = stored.Loot.OrderBy(x => x.Time.ToUniversalTime()).ToList();
        Trim(stored.Chat); Trim(stored.Feed, 500); Trim(stored.Fights); Trim(stored.Loot);
        return stored;
    }
    private static void TrimLedgers()
    {
        // The tracker retains at most 100 eight-slot bags and ignores archived snapshots.
        // Keep a larger independent deduplication window so retention never replays those drops.
        while (data.RecentLootIds.Count > 16000) { seenLoot.Remove(data.RecentLootIds[0]); data.RecentLootIds.RemoveAt(0); }
        while (data.RecentRunIds.Count > 5000) { seenRuns.Remove(data.RecentRunIds[0]); data.RecentRunIds.RemoveAt(0); }
        while (data.RecentFightIds.Count > 50000) { seenFights.Remove(data.RecentFightIds[0]); data.RecentFightIds.RemoveAt(0); }
    }
    private static HudTrophyRecord CloneTrophy(HudTrophyRecord x) => new HudTrophyRecord { ItemId = x.ItemId, Count = x.Count, First = x.First, Last = x.Last, FirstDungeon = x.FirstDungeon, LastDungeon = x.LastDungeon };
    private static HudLogEntry CloneLog(HudLogEntry x) => new HudLogEntry { Id = x.Id, Time = x.Time, Category = x.Category, Sender = x.Sender,
        Recipient = x.Recipient, Text = x.Text, Location = x.Location, Server = x.Server, Realm = x.Realm,
        LocationId = x.LocationId, CalloutKind = x.CalloutKind, CalloutName = x.CalloutName, IsDungeon = x.IsDungeon, DungeonModifiers = x.DungeonModifiers,
        OriginRealmPercent = x.OriginRealmPercent, OpenedInNexus = x.OpenedInNexus, LearnedEventModifiers = (string[])(x.LearnedEventModifiers ?? Array.Empty<string>()).Clone(),
        AvailableUntilUtc = x.AvailableUntilUtc, CalloutEstimated = x.CalloutEstimated, FeedLoot = x.FeedLoot?.Clone(), KeyPopTier = x.KeyPopTier };
    private static HudLootRecord CloneLoot(HudLootRecord x) => new HudLootRecord { Id = x.Id, BagId = x.BagId, BagSlot = x.BagSlot, Time = x.Time, ItemId = x.ItemId, Item = x.Item,
        Rarity = x.Rarity, Shiny = x.Shiny, Bag = x.Bag, Enemy = x.Enemy, Dungeon = x.Dungeon, Character = x.Character, Enchantments = x.Enchantments };
    private static PlayerDamageEntry ClonePlayer(PlayerDamageEntry x) => new PlayerDamageEntry { Rank = x.Rank, Name = x.Name, ClassName = x.ClassName,
        Hits = x.Hits, ClassType = x.ClassType, SkinType = x.SkinType, EquipmentIds = (int[])(x.EquipmentIds ?? Array.Empty<int>()).Clone(),
        Damage = x.Damage, Dps = x.Dps, Percentage = x.Percentage, IsLocalPlayer = x.IsLocalPlayer, GuardHits = x.GuardHits,
        GuardDamage = x.GuardDamage, Status = x.Status, LastHpPercent = x.LastHpPercent };
    private static HudDamageSource CloneSource(HudDamageSource x) => new HudDamageSource { ItemId = x.ItemId, Kind = x.Kind, Damage = x.Damage,
        ConfirmedDamage = x.ConfirmedDamage, Attack = x.Attack, Dexterity = x.Dexterity, Vitality = x.Vitality, Wisdom = x.Wisdom,
        Effects = x.Effects, Effects2 = x.Effects2, EventMultiplier = x.EventMultiplier };

    public static HudJournalData Snapshot()
    {
        lock (Gate) return CloneData(data);
    }
    public static int PageRevision(string page)
    {
        lock (Gate)
            return page == "live" ? feedRevision : page == "chat" ? chatRevision : page == "loot" ? lootRevision
                : page == "combat" || page == "performance" ? combatRevision : page == "trophies" ? collectionRevision : 0;
    }
    // UI tables need one history at a time. Copying the full journal here also
    // copied every fight's players/sources and all persistence ledgers while
    // holding the same lock used by packet observers.
    public static HudJournalData SnapshotForPage(string page)
    {
        lock (Gate)
        {
            var result = new HudJournalData();
            switch (page)
            {
                case "live": result.Feed = data.Feed.Select(CloneLog).ToList(); break;
                case "chat": result.Chat = data.Chat.Select(CloneLog).ToList(); break;
                case "loot": result.Loot = data.Loot.Select(CloneLoot).ToList(); break;
                case "combat": result.Fights = data.Fights.Select(CloneFight).ToList(); break;
                case "trophies":
                    result.Trophies = data.Trophies.Select(CloneTrophy).ToList();
                    result.DungeonRuns = new Dictionary<string, long>(data.DungeonRuns);
                    result.CollectionVersion = data.CollectionVersion;
                    break;
            }
            return result;
        }
    }
    private static HudFightRecord CloneFight(HudFightRecord x)
        => new HudFightRecord { Id = x.Id, Time = x.Time, Dungeon = x.Dungeon, Enemy = x.Enemy, Defeated = x.Defeated,
            ObjectType = x.ObjectType, MaxHp = x.MaxHp, CurrentHp = x.CurrentHp, EncounterEntityIds = (int[])(x.EncounterEntityIds ?? Array.Empty<int>()).Clone(),
            IsBoss = x.IsBoss, Seconds = x.Seconds, DungeonSeconds = x.DungeonSeconds, Damage = x.Damage, Character = x.Character, Equipment = (int[])(x.Equipment ?? Array.Empty<int>()).Clone(),
            Players = x.Players.Select(ClonePlayer).ToList(), DamageTaken = new Dictionary<string, long>(x.DamageTaken), Sources = x.Sources.Select(CloneSource).ToList(), EstimateNote = x.EstimateNote };
    public static List<HudFightRecord> SnapshotForRun(DateTime start)
    {
        string prefix = start.Ticks + ":";
        lock (Gate) return data.Fights.Where(x => x.Id != null && x.Id.StartsWith(prefix, StringComparison.Ordinal)).Select(CloneFight).ToList();
    }
    private static HudJournalData CloneData(HudJournalData source)
        => new HudJournalData { Chat = source.Chat.Select(CloneLog).ToList(), Feed = source.Feed.Select(CloneLog).ToList(), Loot = source.Loot.Select(CloneLoot).ToList(),
                CollectionVersion = 1, Trophies = source.Trophies.Select(CloneTrophy).ToList(), DungeonRuns = new Dictionary<string, long>(source.DungeonRuns),
                RecentLootIds = source.RecentLootIds.ToList(), RecentRunIds = source.RecentRunIds.ToList(), RecentFightIds = source.RecentFightIds.ToList(),
                Fights = source.Fights.Select(CloneFight).ToList() };
    private static void TouchPages(JournalChanges changes)
    {
        if ((changes & JournalChanges.Feed) != 0) feedRevision++;
        if ((changes & JournalChanges.Chat) != 0) chatRevision++;
        if ((changes & JournalChanges.Loot) != 0) lootRevision++;
        if ((changes & JournalChanges.Combat) != 0) combatRevision++;
        if ((changes & JournalChanges.Collections) != 0) collectionRevision++;
    }
    private static void MarkDirty(JournalChanges changes = JournalChanges.All) { dirty = true; revision++; TouchPages(changes); }
    private static void Trim<T>(List<T> list, int limit = 0)
    {
        if (limit == 0) limit = Math.Max(100, Math.Min(50000, HudPreferences.Current.HistoryLimit));
        if (list.Count > limit) list.RemoveRange(0, list.Count - limit);
    }
    public static void Flush()
    {
        WaitForLoads();
        PendingSave[] saves;
        lock (Gate)
        {
            QueueCurrent(); saves = pendingSaves.Values.ToArray();
        }
        foreach (var saved in saves)
        {
            bool success;
            lock (WriteGate)
            {
                success = writtenRevisions.TryGetValue(saved.Profile, out int written) && written >= saved.Revision;
                if (!success) success = HudStorage.Save("journal-" + saved.Profile + ".json", saved.Data);
                if (success) writtenRevisions[saved.Profile] = Math.Max(written, saved.Revision);
            }
            lock (Gate) if (success)
            {
                if (pendingSaves.TryGetValue(saved.Profile, out var pending) && pending.Revision == saved.Revision) pendingSaves.Remove(saved.Profile);
                if (profile == saved.Profile && revision == saved.Revision) dirty = false;
            }
        }
    }
    // Caller holds Gate. Retain a detached snapshot across account handoffs and
    // failed writes; only a successful write of that revision can remove it.
    private static void QueueCurrent()
    {
        if (!dirty || profile == null || pendingLoads.ContainsKey(profile)) return;
        if (!pendingSaves.TryGetValue(profile, out var saved) || saved.Revision < revision)
            pendingSaves[profile] = new PendingSave { Profile = profile, Revision = revision, Data = CloneData(data) };
    }
}
