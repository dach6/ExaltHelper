using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper;

internal sealed class HudFeedLoot
{
    public string BagType { get; set; }
    public int[] ItemIds { get; set; } = Array.Empty<int>();
    public string[] Rarities { get; set; } = Array.Empty<string>();
    public bool[] Shiny { get; set; } = Array.Empty<bool>();
    public int[] EnchantCounts { get; set; } = Array.Empty<int>();
    public string[][] Enchantments { get; set; } = Array.Empty<string[]>();
    public static HudFeedLoot FromBag(LootBagDrop bag) => new HudFeedLoot { BagType = bag.BagType, ItemIds = bag.ItemIds.ToArray(), Rarities = (bag.ItemRarities ?? new List<string>()).ToArray(),
        Shiny = bag.ItemIsShiny.ToArray(), EnchantCounts = bag.ItemEnchantCounts.ToArray(), Enchantments = bag.ItemEnchants.Select(x => (x ?? new List<string>()).ToArray()).ToArray() };
    public LootBagDrop ToBag() => new LootBagDrop { BagType = BagType, ItemIds = (ItemIds ?? Array.Empty<int>()).ToList(), ItemRarities = (Rarities ?? Array.Empty<string>()).ToList(),
        ItemIsShiny = (Shiny ?? Array.Empty<bool>()).ToList(), ItemEnchantCounts = (EnchantCounts ?? Array.Empty<int>()).ToList(),
        ItemEnchants = (Enchantments ?? Array.Empty<string[]>()).Select(x => (x ?? Array.Empty<string>()).ToList()).ToList() };
    public HudFeedLoot Clone() => new HudFeedLoot { BagType = BagType, ItemIds = (int[])(ItemIds ?? Array.Empty<int>()).Clone(),
        Rarities = (string[])(Rarities ?? Array.Empty<string>()).Clone(), Shiny = (bool[])(Shiny ?? Array.Empty<bool>()).Clone(),
        EnchantCounts = (int[])(EnchantCounts ?? Array.Empty<int>()).Clone(),
        Enchantments = (Enchantments ?? Array.Empty<string[]>()).Select(x => (string[])(x ?? Array.Empty<string>()).Clone()).ToArray() };
}

internal static partial class HudJournal
{
    private static HudLogEntry currentLocation;
    private static string locationSignature;
    private static readonly Dictionary<int, DateTime> portalSpawns = new Dictionary<int, DateTime>();
    private static DateTime? pendingPortalSpawn;
    private static readonly Dictionary<string, List<string[]>> dungeonModifierRuns = new Dictionary<string, List<string[]>>();
    private static readonly int?[,] dustAmounts = new int?[2, 3];
    private static readonly int?[,] dustCaps = new int?[2, 3];
    private static readonly bool[,] dustFull = new bool[2, 3];
    private static readonly bool[,] dustObserved = new bool[2, 3];
    private static bool dustSeasonal;
    public static DateTime? RealmCastleUtc { get; private set; }

    public static void RecordFeedLoot(LootBagDrop bag)
    {
        if (bag == null || string.IsNullOrEmpty(bag.DropId) || bag.ItemIds.Count == 0) return;
        lock (Gate)
        {
            string id = "bag:" + bag.DropId;
            if (bag.JournalScopeId != scopeId || data.Feed.Any(x => x.Id == id)) return;
            string items = string.Join(", ", bag.ItemIds.Select((item, index) => ItemSpriteManager.GetItemMetadata(item).Name
                + (bag.ItemIsShiny.ElementAtOrDefault(index) ? " (shiny)" : "")
                + (bag.ItemEnchants.ElementAtOrDefault(index)?.Count > 0 ? " [" + string.Join(", ", bag.ItemEnchants[index]) + "]" : "")));
            data.Feed.Add(new HudLogEntry { Id = id, Time = bag.DropTime, Category = "Loot", Sender = bag.SourceMonster, Text = bag.BagType + " · " + items,
                Location = bag.Dungeon, Server = Server, Realm = Realm, LocationId = CurrentLocationId, CalloutKind = "Message", FeedLoot = HudFeedLoot.FromBag(bag) });
            HudSounds.PlayLoot(bag);
            Trim(data.Feed, 500); MarkDirty(JournalChanges.Feed);
        }
    }

    private static bool RecordKeyPop(NotificationPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.Message) || packet.Message.Length > 65536) return false;
        try
        {
            var payload = new JavaScriptSerializer().DeserializeObject(packet.Message) as Dictionary<string, object>;
            if (payload == null) return false;
            object raw;
            if (!payload.TryGetValue("player", out raw) && payload.TryGetValue("tokens", out var nested) && nested is Dictionary<string, object> tokens)
                tokens.TryGetValue("player", out raw);
            string player = (raw as string)?.Split(',')[0].Trim();
            if (string.IsNullOrEmpty(player) || player.Length > 32 || player.Any(x => !char.IsLetterOrDigit(x) && x != '_')) return false;
            string portal = GameData.Objects?.GetById((ushort)packet.ObjectId)?.Name;
            if (string.IsNullOrEmpty(portal)) portal = ItemSpriteManager.GetItemMetadata(packet.ObjectId).Name;
            string dungeon = (portal ?? "").EndsWith(" Portal", StringComparison.OrdinalIgnoreCase) ? portal.Substring(0, portal.Length - 7) : portal;
            data.Feed.Add(new HudLogEntry { Category = "Key pop", Sender = player, Text = player + " opened " + (portal ?? "a dungeon"),
                Location = Map, Server = Server, Realm = Realm, LocationId = CurrentLocationId, CalloutKind = "KeyPop", CalloutName = player,
                KeyPopTier = HudCombatPolicy.DungeonTier(dungeon) });
            HudSounds.PlayKeyPop(dungeon, player);
            Trim(data.Feed, 500); MarkDirty(JournalChanges.Feed); return true;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public static string CurrentLocationId { get { lock (Gate) return currentLocation?.LocationId; } }
    public static HudLogEntry CurrentLocation()
    {
        lock (Gate) return currentLocation == null
            ? new HudLogEntry { Category = "Travel", Location = Map, Server = Server, Realm = Realm, CalloutKind = "Message" }
            : CloneLog(currentLocation);
    }

    private static void ResetCalloutState()
    {
        currentLocation = null; locationSignature = null; pendingPortalSpawn = null;
        RealmCastleUtc = null;
        portalSpawns.Clear(); dungeonModifierRuns.Clear();
        Array.Clear(dustAmounts, 0, dustAmounts.Length); Array.Clear(dustCaps, 0, dustCaps.Length);
        Array.Clear(dustFull, 0, dustFull.Length); Array.Clear(dustObserved, 0, dustObserved.Length); dustSeasonal = false;
        Map = "Waiting for game"; Realm = Server = ""; RealmScore = MaxRealmScore = -1;
    }

    private static void ObservePortalUse(Packet packet)
    {
        if (packet.Id != 47) return;
        // Read a detached payload; an unsupported optional packet must remain byte-for-byte intact.
        byte[] payload = packet.GetPayloadBytes();
        pendingPortalSpawn = null;
        if (payload.Length < 4) return;
        using var reader = new PacketReader(new MemoryStream(payload));
        if (portalSpawns.TryGetValue(reader.ReadInt32(), out var spawn)) pendingPortalSpawn = spawn;
    }

    private static void ObserveLocation(Client client, MapInfoPacket map)
    {
        DateTime now = DateTime.UtcNow;
        bool fromRealm = MaxRealmScore > 0 || IsRealmMap(Map);
        bool fromHub = IsHubMap(Map);
        int? originPercent = fromRealm && MaxRealmScore > 0 && RealmScore >= 0
            ? (int?)Math.Min(100, Math.Max(0, (long)RealmScore * 100 / MaxRealmScore)) : null;
        string nextMap = string.IsNullOrWhiteSpace(map.DisplayName) ? map.MapName ?? "" : map.DisplayName;
        string nextServer = client?._currentServerName ?? "";
        string nextRealm = map.RealmName ?? "";
        string modifiers = map.DungeonModifiers ?? "";
        string signature = map.Fp + "\u001f" + nextMap + "\u001f" + nextServer + "\u001f" + nextRealm + "\u001f" + modifiers;
        Map = nextMap; Server = nextServer; Realm = nextRealm;
        RealmScore = map.CurrentRealmScore; MaxRealmScore = map.MaxRealmScore;
        if (locationSignature == signature) return;
        RealmCastleUtc = null;

        foreach (var entry in data.Feed.Where(x => x.IsDungeon && x.AvailableUntilUtc.HasValue && x.AvailableUntilUtc.Value > now)) entry.AvailableUntilUtc = now;
        sources.Clear(); damageTaken.Clear();
        bool dungeon = IsDungeonMap(Map, MaxRealmScore);
        bool anchored = pendingPortalSpawn.HasValue && pendingPortalSpawn.Value <= now && now - pendingPortalSpawn.Value < TimeSpan.FromSeconds(30);
        bool? inNexus = null;
        if (dungeon)
        {
            if (fromHub || IsHubMap(Realm) || (fromRealm && string.IsNullOrWhiteSpace(Realm))) inNexus = true;
            else if (!string.IsNullOrWhiteSpace(Realm)) inNexus = false;
        }
        var row = new HudLogEntry { Time = now.ToLocalTime(), Category = "Travel", Text = "Entered " + Map + (Realm.Length > 0 ? " · " + Realm : ""),
            Location = Map, Server = Server, Realm = Realm, LocationId = Guid.NewGuid().ToString("N"), IsDungeon = dungeon,
            CalloutKind = dungeon ? "Dungeon" : "Message", CalloutName = Map, DungeonModifiers = modifiers,
            OriginRealmPercent = dungeon && anchored && inNexus != true ? originPercent : null, OpenedInNexus = inNexus,
            CalloutEstimated = !anchored,
            AvailableUntilUtc = dungeon ? (DateTime?)(anchored ? pendingPortalSpawn.Value.AddSeconds(30) : now.AddSeconds(25)) : null };
        if (row.OriginRealmPercent >= 100) row.AvailableUntilUtc = now;
        if (dungeon)
        {
            string key = MapKey(Map);
            if (!dungeonModifierRuns.TryGetValue(key, out var runs)) dungeonModifierRuns[key] = runs = new List<string[]>();
            runs.Add(ModifierTokens(modifiers));
            if (runs.Count > 10) runs.RemoveAt(0);
            row.LearnedEventModifiers = LearnedModifiers(runs);
        }
        currentLocation = row; locationSignature = signature;
        if (dungeon) HudSounds.PlayDungeon(modifiers);
        else if (MaxRealmScore > 0 && RealmScore == 0) HudSounds.Play("RealmOpened");
        pendingPortalSpawn = null; portalSpawns.Clear();
        data.Feed.Add(row); Trim(data.Feed, 500); MarkDirty(JournalChanges.Feed);
    }

    private static string[] ModifierTokens(string raw) => (raw ?? "").Split(';').Select(x => x.Split('|')[0].Trim())
        .Where(x => x.Length > 0).GroupBy(LocationKey).Select(x => x.First()).ToArray();
    private static string[] LearnedModifiers(List<string[]> runs)
    {
        var settings = HudPreferences.Current.Callouts;
        if (!settings.LearnEventModifiers) return Array.Empty<string>();
        int count = Math.Max(2, Math.Min(10, settings.LearnEventRuns));
        if (runs.Count < count) return Array.Empty<string>();
        var recent = runs.Skip(runs.Count - count).ToArray();
        return recent[0].Where(token => recent.All(set => set.Any(x => LocationKey(x) == LocationKey(token)))
            && recent.Any(set => set.Length == 1 && LocationKey(set[0]) == LocationKey(token))).ToArray();
    }

    private static string LocationKey(string value) => new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string MapKey(string value)
    {
        string text = (value ?? "").Trim();
        if (text.StartsWith("{s.", StringComparison.OrdinalIgnoreCase) && text.EndsWith("}")) text = text.Substring(3, text.Length - 4);
        return LocationKey(text);
    }
    private static bool IsHubMap(string name)
    {
        string key = MapKey(name);
        return key == "nexus" || key == "nexusportal" || key == "vault" || key == "guildhall" || key == "guild" || key == "bazaar" || key == "grandbazaar" || key == "clothbazaar";
    }
    private static bool IsRealmMap(string name) => HudCallouts.IsRealm(name);
    private static bool IsDungeonMap(string name, int maxRealmScore) => maxRealmScore <= 0 && HudCallouts.IsDungeon(name);

    private static void StampMessageContext(HudLogEntry row)
    {
        row.LocationId = currentLocation?.LocationId;
        // Only server/NPC announcements may become event calls; ordinary player text cannot impersonate them.
        string eventName = HudCallouts.DetectEvent(row.Sender, row.Text);
        row.CalloutKind = eventName == null ? "Message" : "Event";
        row.CalloutName = eventName;
    }

    private static void ObserveRealmMessage(string sender, string text)
    {
        string speaker = (sender ?? "").TrimStart('#').Trim();
        if (!speaker.Equals("Oryx", StringComparison.OrdinalIgnoreCase) && !speaker.Equals("Oryx the Mad God", StringComparison.OrdinalIgnoreCase)) return;
        text ??= "";
        if (text.IndexOf("s.oryx_closed_realm", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("realm is now closed", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("realm has closed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (RealmCastleUtc.HasValue) return;
            DateTime now = DateTime.UtcNow; RealmCastleUtc = now.AddSeconds(120);
            foreach (var entry in data.Feed.Where(x => x.LocationId == currentLocation?.LocationId && (x.IsDungeon || x.CalloutKind == "Event"))) entry.AvailableUntilUtc = now;
            if (currentLocation?.IsDungeon == true) currentLocation.AvailableUntilUtc = now;
            HudSounds.Play("RealmClosing"); MarkDirty(JournalChanges.Feed);
        }
        else if (text.IndexOf("s.oryx_minions_failed", StringComparison.OrdinalIgnoreCase) >= 0) HudSounds.Play("OryxWarning");
    }

    private static void ObserveCalloutStats(Client client, Packet packet)
    {
        IEnumerable<ObjectStatsData> statuses = Array.Empty<ObjectStatsData>();
        if (packet is UpdatePacket update)
        {
            foreach (var obj in update.NewObjects ?? Array.Empty<ObjectData>())
                if (obj?.Stats != null && GameData.Objects?.GetById(obj.ObjectType)?.ObjectClass == "Portal" && !portalSpawns.ContainsKey(obj.Stats.ObjectId))
                    portalSpawns[obj.Stats.ObjectId] = DateTime.UtcNow;
            if (portalSpawns.Count > 4096)
                foreach (int id in portalSpawns.Where(x => DateTime.UtcNow - x.Value > TimeSpan.FromSeconds(30)).Select(x => x.Key).ToArray()) portalSpawns.Remove(id);
            statuses = (update.NewObjects ?? Array.Empty<ObjectData>()).Where(x => x?.Stats != null).Select(x => x.Stats);
        }
        else if (packet is NewTickPacket tick) statuses = tick.Statuses ?? new List<ObjectStatsData>();
        if (client == null) return;
        foreach (var status in statuses.Where(x => x?.ObjectId == client.PlayerId))
        {
            var stats = status.StatList;
            if (stats == null) continue;
            var seasonal = stats.FirstOrDefault(x => x.Type == 24);
            if (seasonal != null) dustSeasonal = seasonal.Value != 0;
            int mode = dustSeasonal ? 1 : 0;
            bool changed = false;
            foreach (var stat in stats.Where(x => x.Type == 127 || x.Type == 147))
            {
                foreach (string part in (stat.StringValue ?? "").Split(','))
                {
                    string[] pair = part.Split(':');
                    if (pair.Length != 2 || !int.TryParse(pair[0], out int id) || !int.TryParse(pair[1], out int value) || value < 0) continue;
                    int index = id == 1 ? 0 : id == 4 ? 1 : id == 5 ? 2 : -1;
                    if (index < 0) continue;
                    if (stat.Type == 127) dustAmounts[mode, index] = value; else dustCaps[mode, index] = value;
                    changed = true;
                }
            }
            if (!changed) continue;
            for (int i = 0; i < 3; i++)
            {
                if (!dustAmounts[mode, i].HasValue || !dustCaps[mode, i].HasValue || dustCaps[mode, i] <= 0) continue;
                bool full = dustAmounts[mode, i] >= dustCaps[mode, i];
                if (dustObserved[mode, i] && full && !dustFull[mode, i]) AppendFeed("Dust", (dustSeasonal ? "Seasonal " : "Regular ") + new[] { "Green", "Purple", "Red" }[i]
                    + " dust is full (" + dustAmounts[mode, i] + "/" + dustCaps[mode, i] + ").");
                dustFull[mode, i] = full; dustObserved[mode, i] = true;
            }
        }
    }
}
