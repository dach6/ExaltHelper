using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper;

internal static partial class MissionWorkspace
{
    private static readonly object Sync = new object();
    private static MissionCatalog catalog = new MissionCatalog();
    private static MissionPreferences preferences = new MissionPreferences();
    private static MissionCache cache = new MissionCache();
    private static readonly Dictionary<byte, string> pendingClaims = new Dictionary<byte, string>();
    private static string pendingQuest;
    private static string accountKey = "";
    private static bool baselineLoaded;
    private static bool refreshing;
    private static long generation;
    private static long revision;
    private static string status = "Connect to the game, then refresh missions.";
    private static DateTime syncedUtc;
    private static bool? characterSeasonal;
    private static bool? characterCrucible;
    private static int[] equippedSlots = Array.Empty<int>();
    private static Client activeClient;

    public static long Revision { get { lock (Sync) { EnsureAccount(); return revision; } } }
    public static string Status { get { lock (Sync) { EnsureAccount(); return status + (string.IsNullOrEmpty(persistenceError) ? "" : " · " + persistenceError); } } }
    public static bool IsRefreshing { get { lock (Sync) { EnsureAccount(); return refreshing; } } }
    public static string QuestStorage { get { lock (Sync) { EnsureAccount(); return preferences.QuestStorage; } } }
    public static string ActiveAccountKey { get { lock (Sync) { EnsureAccount(); return accountKey; } } }
    public static bool ShowAllChoices { get { lock (Sync) { EnsureAccount(); return preferences.ShowAllChoices; } } }
    public static bool PreferSeasonalMarks { get { lock (Sync) { EnsureAccount(); return preferences.PreferSeasonalMarks; } } }
    public static bool GetAutoTracking(bool quests) { lock (Sync) { EnsureAccount(); return quests ? preferences.AutoTrackQuests : preferences.AutoTrackMissions; } }
    public static Control CreateMissionsPage() => new MissionPage(false);
    public static Control CreateQuestsPage() => new MissionPage(true);

    private static void EnsureAccount()
    {
        string active = AccountWorkspace.AccountKey ?? "";
        if (active == accountKey) return;
        accountKey = active;
        generation++;
        catalog = new MissionCatalog();
        LoadAccountState(active);
        preferences.Pinned = preferences.Pinned ?? new List<string>();
        preferences.Hidden = preferences.Hidden ?? new List<string>();
        preferences.Order = preferences.Order ?? new List<string>();
        preferences.HiddenCategories = preferences.HiddenCategories ?? new List<string>();
        preferences.MissionsPage = preferences.MissionsPage ?? new MissionPagePreferences();
        preferences.QuestsPage = preferences.QuestsPage ?? new MissionPagePreferences();
        cache.Quests = cache.Quests ?? new List<MissionQuest>();
        if (!new[] { "Both", "Seasonal", "Regular" }.Contains(preferences.QuestStorage)) preferences.QuestStorage = "Both";
        baselineLoaded = false;
        refreshing = false;
        pendingClaims.Clear();
        pendingQuest = null;
        syncedUtc = DateTime.MinValue;
        characterSeasonal = characterCrucible = null;
        equippedSlots = Array.Empty<int>();
        status = active.Length > 0 ? "Refresh to load current missions and progress." : "Connect to the game, then refresh missions.";
        revision++;
    }

    public static async Task RefreshAsync()
    {
        long requestGeneration;
        lock (Sync)
        {
            EnsureAccount();
            if (refreshing) return;
            if (!AccountWorkspace.IsAuthenticated || accountKey.Length == 0)
            {
                status = "Connect a character and refresh account data first.";
                revision++;
                return;
            }
            refreshing = true;
            status = "Syncing mission definitions and progress…";
            requestGeneration = generation;
            revision++;
        }
        try
        {
            // The absolute baseline wins over any socket deltas received while fetching.
            string definitions = await AccountWorkspace.FetchApiAsync("missions/getClientSeasons").ConfigureAwait(false);
            string progress = await AccountWorkspace.FetchApiAsync("missions/getPlayerMissions").ConfigureAwait(false);
            var parsed = MissionCatalog.Parse(definitions, progress);
            lock (Sync)
            {
                EnsureAccount();
                if (requestGeneration != generation) return;
                catalog = parsed;
                baselineLoaded = true;
                syncedUtc = DateTime.UtcNow;
                status = "Synced " + syncedUtc.ToLocalTime().ToString("HH:mm") + " · progress updates as you play";
                revision++;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is System.Net.WebException || ex is FormatException || ex is System.Xml.XmlException || ex is ArgumentException || ex is IOException || ex is TaskCanceledException)
        {
            lock (Sync)
            {
                EnsureAccount();
                if (requestGeneration == generation)
                {
                    // Never display a transport exception: its URL may contain credentials.
                    status = "Mission sync failed. Reconnect the game, then refresh. Last results retained.";
                    revision++;
                }
            }
        }
        finally { lock (Sync) { EnsureAccount(); if (requestGeneration == generation) { refreshing = false; revision++; } } }
    }

    public static void ObservePacket(Client client, Packet packet, bool fromClient)
    {
        if (packet == null) return;
        bool featurePacket = packet.Id == 6 || packet.Id == 58 || packet.Id == 96 || packet.Id == 163 || packet.Id == 164 || packet.Id == 165;
        if (!featurePacket && !(packet is HelloPacket) && !(packet is UpdatePacket) && !(packet is NewTickPacket)) return;
        lock (Sync)
        {
            EnsureAccount();
            if (fromClient && packet is HelloPacket)
            {
                activeClient = client;
                characterSeasonal = characterCrucible = null;
                equippedSlots = Array.Empty<int>();
                pendingClaims.Clear(); pendingQuest = null;
                return;
            }
            if (activeClient != null && !ReferenceEquals(activeClient, client)) return;
            if (accountKey.Length == 0) return;
            if (!fromClient && client != null)
            {
                IEnumerable<ObjectStatsData> stats = packet is NewTickPacket tick ? tick.Statuses : packet is UpdatePacket update ? update.NewObjects?.Select(o => o.Stats) : null;
                if (stats != null)
                    foreach (var objectStats in stats.Where(s => s.ObjectId == client.PlayerId))
                        foreach (var stat in objectStats.StatList ?? new List<StatData>())
                        {
                            if (stat.Type == StatType.Seasonal) characterSeasonal = stat.Value != 0;
                            if (stat.Type == StatType.CrucibleId) characterCrucible = !string.IsNullOrEmpty(stat.StringValue) && stat.StringValue != "0";
                        }
                if (client.Player != null)
                {
                    equippedSlots = client.Player.Inventory.Take(4).Select(id => id >= 0 && id <= ushort.MaxValue && GameData.Items != null && GameData.Items.Map.TryGetValue((ushort)id, out var item) ? (int)item.SlotType : 0).ToArray();
                }
            }
            if (!featurePacket) return;
            try { ApplyPayload(packet.Id, packet.GetPayloadBytes(), fromClient); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidDataException || ex is OverflowException)
            {
                // Observation must never alter or interrupt the game's packet forwarding.
                status = "An unfamiliar mission or quest update was skipped. Refresh to resync.";
                revision++;
            }
        }
    }

    internal static void ApplyPayload(byte packetId, byte[] bytes, bool fromClient)
    {
        lock (Sync) { EnsureAccount(); ApplyPayloadCore(packetId, bytes, fromClient); }
    }

    private static void ApplyPayloadCore(byte packetId, byte[] bytes, bool fromClient)
    {
        using var memory = new MemoryStream(bytes, false);
        using var reader = new PacketReader(memory);
        if (!fromClient && packetId == 6)
        {
            int count = ReadCount(reader, 1000);
            var quests = new List<MissionQuest>();
            for (int i = 0; i < count; i++)
            {
                var quest = new MissionQuest
                {
                    Id = ReadText(reader), Name = ReadText(reader), Description = ReadText(reader), Expiration = ReadText(reader), Category = reader.ReadInt32()
                };
                reader.ReadInt32();
                quest.Requirements = ReadItems(reader);
                quest.Rewards = ReadItems(reader);
                quest.Completed = reader.ReadBoolean();
                quest.Choice = reader.ReadBoolean();
                quest.Repeatable = reader.ReadBoolean();
                quests.Add(quest);
            }
            reader.ReadInt16(); // Next refresh price. Refresh is performed in the game.
            bool newSet = cache.Quests.Count == 0 || quests.Any(q => cache.Quests.All(old => old.Id != q.Id));
            DateTime setTime = cache.DailySetUtc == DateTime.MinValue ? cache.QuestsReceivedUtc : cache.DailySetUtc;
            cache = new MissionCache { Quests = quests, QuestsReceivedUtc = DateTime.UtcNow, DailySetUtc = newSet || setTime == DateTime.MinValue ? DateTime.UtcNow : setTime };
            QueueQuestSave();
            revision++;
        }
        else if (!fromClient && packetId == 165 && baselineLoaded && !refreshing)
        {
            catalog.ApplyDelta(ReadText(reader));
            revision++;
        }
        else if (fromClient && packetId == 163)
        {
            int season = reader.ReadInt32();
            int mission = reader.ReadInt32();
            byte request = reader.ReadByte();
            reader.ReadInt16();
            pendingClaims[request] = "mission:" + season + ":" + mission;
        }
        else if (!fromClient && packetId == 164)
        {
            byte request = reader.ReadByte(); bool accepted = reader.ReadByte() != 0; reader.ReadInt16();
            if (pendingClaims.TryGetValue(request, out string key))
            {
                pendingClaims.Remove(request);
                if (accepted)
                {
                    if (!catalog.Progress.TryGetValue(key, out var p)) catalog.Progress[key] = p = new MissionProgress();
                    p.RawState = 2; p.ClaimCount++; p.ClaimedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); p.EverSatisfied = true;
                    revision++;
                }
            }
        }
        else if (fromClient && packetId == 58) pendingQuest = ReadText(reader);
        else if (!fromClient && packetId == 96)
        {
            bool accepted = reader.ReadBoolean(); ReadText(reader);
            if (accepted && pendingQuest != null)
            {
                var quest = cache.Quests.FirstOrDefault(q => q.Id == pendingQuest);
                if (quest != null && !quest.Repeatable) { quest.Completed = true; QueueQuestSave(); }
                revision++;
            }
            pendingQuest = null;
        }
    }

    public static List<MissionTaskView> GetMissions()
    {
        lock (Sync)
        {
            EnsureAccount();
            var character = AccountWorkspace.CurrentCharacter;
            if (character != null)
            {
                characterSeasonal = character.Seasonal; characterCrucible = character.Crucible;
                equippedSlots = character.Inventory.Take(4).Where(id => id > 0).Select(id => ItemSpriteManager.GetItemMetadata(id).SlotType).ToArray();
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Sort(catalog.Definitions.Select(def =>
            {
                string state = catalog.Status(def, now);
                catalog.Progress.TryGetValue(def.Key, out var p);
                int[] values = p?.Values ?? Array.Empty<int>();
                var detail = new StringBuilder();
                if (def.Objectives.Count == 0) detail.AppendLine(def.Description);
                foreach (int[] group in def.Groups())
                {
                    if (group.Length > 1) detail.AppendLine("Complete one of:");
                    foreach (int i in group)
                    {
                        var objective = def.Objectives[i];
                        int have = i < values.Length ? values[i] : 0;
                        detail.AppendLine(objective.Label + "  " + have.ToString("N0") + "/" + objective.Need.ToString("N0") + " · " + Math.Max(0, objective.Need - have).ToString("N0") + " remaining");
                        var dungeons = MissionTargets.Dungeons(objective);
                        if (objective.Type == 4 || objective.Type == 12) detail.AppendLine("Counts: " + string.Join(", ", dungeons));
                    }
                }
                bool eligible = Qualifies(def);
                string qualifier = def.Participants == 2 ? "Seasonal character" : def.Participants == 4 ? "Crucible character" : "Any character";
                if (def.Worn.Length > 0) qualifier += " · Equip " + string.Join(" or ", def.Worn);
                detail.AppendLine(qualifier + (eligible ? " · eligible" : " · current character does not qualify"));
                if (def.Parents.Length > 0) detail.AppendLine("Requires: " + string.Join(", ", def.Parents.Select(id => catalog.Definitions.FirstOrDefault(d => d.SeasonId == def.SeasonId && d.Id == id)?.Name ?? "Mission " + id)));
                if (state == "Ready to claim") detail.AppendLine("Claim this reward in the game's Missions panel.");
                detail.AppendLine("Season: " + def.SeasonName);
                int[] planValues = state.StartsWith("Cooldown", StringComparison.Ordinal) ? new int[def.Objectives.Count] : values;
                var remaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var taskGroups = new List<MissionTaskTargetGroup>();
                foreach (var group in def.Groups().Where(g => !g.Any(j => j < planValues.Length && planValues[j] >= Math.Max(1, def.Objectives[j].Need))))
                {
                    var taskGroup = new MissionTaskTargetGroup { Label = string.Join(" or ", group.Select(i => def.Objectives[i].Label)), IsChoice = group.Length > 1 };
                    double leading = group.Max(i => (i < planValues.Length ? planValues[i] : 0) / (double)Math.Max(1, def.Objectives[i].Need));
                    var relevant = group.Where(i => preferences.ShowAllChoices || group.Length == 1 || leading <= 0 || Math.Abs((i < planValues.Length ? planValues[i] : 0) / (double)Math.Max(1, def.Objectives[i].Need) - leading) < 0.000001);
                    foreach (int i in relevant)
                    {
                        string[] candidates = MissionTargets.Dungeons(def.Objectives[i]);
                        if (candidates.Length == 0) candidates = new[] { "objective:" + def.Objectives[i].Type + ":" + def.Objectives[i].Target };
                        foreach (string target in candidates)
                        {
                            int count = Math.Max(0, def.Objectives[i].Need - (i < planValues.Length ? planValues[i] : 0));
                            taskGroup.Options[target] = taskGroup.Options.TryGetValue(target, out int old) ? Math.Min(old, count) : count;
                            remaining[target] = Math.Max(remaining.TryGetValue(target, out old) ? old : 0, count);
                        }
                    }
                    taskGroups.Add(taskGroup);
                }
                string rewards = RewardText(def);
                if (rewards.Length > 0) detail.AppendLine("Rewards: " + rewards);
                foreach (var objective in def.Objectives)
                {
                    string hint = MissionTargetHints.Objective(objective);
                    if (hint.Length > 0) detail.AppendLine(hint);
                }
                string category = def.Participants == 255 ? "Regular" : def.Participants == 2 ? "Seasonal" : def.Participants == 4 ? "Crucible" : "Event";
                return Decorate(new MissionTaskView { Id = def.Key, Title = def.Name, Category = category, Detail = detail.ToString().Trim(), Status = state, Progress = def.Fraction(values), Eligible = eligible, Rewards = rewards, Targets = remaining.Keys.ToArray(), RemainingByTarget = remaining, TaskGroups = taskGroups });
            })).ToList();
        }
    }

    public static List<MissionTaskView> GetQuests()
        => GetQuestsCore(false);

    private static List<MissionTaskView> GetQuestsCore(bool forTaskbar)
    {
        lock (Sync)
        {
            EnsureAccount();
            return Sort(cache.Quests.Select(q =>
            {
                string category = QuestCategory(q);
                DateTime dailySet = cache.DailySetUtc == DateTime.MinValue ? cache.QuestsReceivedUtc : cache.DailySetUtc;
                bool stale = category.StartsWith("Daily", StringComparison.Ordinal) && dailySet.Date < DateTime.UtcNow.Date;
                DateTimeOffset? end = QuestExpiration(q.Expiration);
                bool expired = end.HasValue && end.Value <= DateTimeOffset.UtcNow;
                var groups = q.Requirements.GroupBy(id => id).ToList();
                int needed = groups.Sum(g => g.Count());
                int seasonal = groups.Sum(g => Math.Min(g.Count(), AccountWorkspace.GetItemCount(g.Key, true)));
                int regular = groups.Sum(g => Math.Min(g.Count(), AccountWorkspace.GetItemCount(g.Key, false)));
                int owned = preferences.QuestStorage == "Seasonal" ? seasonal : preferences.QuestStorage == "Regular" ? regular : Math.Max(seasonal, regular);
                bool selectedSeasonal = preferences.QuestStorage == "Seasonal" || (preferences.QuestStorage == "Both" && seasonal >= regular);
                if (forTaskbar && preferences.QuestStorage == "Both") { selectedSeasonal = preferences.PreferSeasonalMarks; owned = selectedSeasonal ? seasonal : regular; }
                var detail = new StringBuilder(q.Description + Environment.NewLine);
                var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var taskGroups = new List<MissionTaskTargetGroup>();
                foreach (var requirement in groups)
                {
                    string item = ItemName(requirement.Key);
                    int ss = AccountWorkspace.GetItemCount(requirement.Key, true), ns = AccountWorkspace.GetItemCount(requirement.Key, false);
                    detail.AppendLine(item + " × " + requirement.Count() + " · Seasonal " + ss + " · Regular " + ns);
                    string dungeon = MissionTargets.MarkDungeon(item);
                    int have = selectedSeasonal ? ss : ns;
                    if (have < requirement.Count())
                    {
                        string target = dungeon ?? "item:" + requirement.Key;
                        targets[target] = (targets.TryGetValue(target, out int old) ? old : 0) + requirement.Count() - have;
                        taskGroups.Add(new MissionTaskTargetGroup { Label = item, Options = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { target, requirement.Count() - have } } });
                    }
                    if (dungeon != null)
                    {
                        detail.AppendLine("Find in " + dungeon + ". " + MissionTargetHints.Dungeon(dungeon));
                    }
                }
                string state = expired ? "Expired" : stale ? "Refresh at Tinkerer" : q.Completed && !q.Repeatable ? "Claimed" : needed > 0 && owned >= needed ? "Ready to claim" : "In progress";
                if (preferences.QuestStorage == "Both") detail.AppendLine("Storage sides are separate; marks from different sides are not combined.");
                if (forTaskbar) detail.AppendLine("Taskbar tracks " + (selectedSeasonal ? "Seasonal" : "Regular") + " marks.");
                if (state == "Ready to claim") detail.AppendLine("Redeem at the Tinkerer using " + (selectedSeasonal ? "Seasonal" : "Regular") + " storage.");
                if (stale) detail.AppendLine("Daily quest data is from " + cache.QuestsReceivedUtc.ToLocalTime().ToString("g") + ". Visit the Tinkerer to refresh.");
                if (end.HasValue) detail.AppendLine("Expires: " + end.Value.ToLocalTime().ToString("g"));
                else if (category.StartsWith("Daily", StringComparison.Ordinal)) detail.AppendLine("Daily reset: " + dailySet.Date.AddDays(1).ToString("yyyy-MM-dd HH:mm") + " UTC");
                string rewards = (q.Choice ? "Choose one: " : "") + string.Join(", ", q.Rewards.GroupBy(id => id).Select(g => ItemName(g.Key) + " × " + g.Count()));
                detail.AppendLine("Rewards: " + rewards);
                if (q.Repeatable) detail.AppendLine("Repeatable quest");
                return Decorate(new MissionTaskView { Id = q.Key, Title = q.Name, Detail = detail.ToString().Trim(), Status = state, Progress = needed > 0 ? owned / (double)needed : 0, Eligible = !stale && !expired, IsQuest = true, Category = category, Rewards = rewards, Targets = targets.Keys.ToArray(), RemainingByTarget = targets, TaskGroups = taskGroups });
            })).ToList();
        }
    }

    public static List<MissionTaskView> GetTrackedTasks()
    {
        var tasks = GetMissions().Concat(GetQuestsCore(true)).Where(t => t.Pinned && !t.Hidden && t.Status != "Claimed" && t.Status != "Expired"
            && (t.ExplicitlyTracked || (t.IsQuest ? t.Category == "Daily quest chests" && t.TaskGroups.Count > 0 : t.Status == "In progress" || t.Status.StartsWith("Cooldown", StringComparison.Ordinal)))).ToList();
        var projections = tasks.SelectMany(t => (t.Status != "In progress" && t.Status != "Refresh at Tinkerer" && t.Status != "Locked" && !t.Status.StartsWith("Cooldown", StringComparison.Ordinal)) || t.TaskGroups.Count == 0
            ? new[] { new { Row = t, Group = new MissionTaskTargetGroup { Label = t.Title, Options = new Dictionary<string, int> { { "task:" + t.Id, 0 } } } } }
            : t.TaskGroups.Select(group => new { Row = t, Group = group })).ToList();
        bool allChoices = ShowAllChoices;
        var assigned = projections.SelectMany(p =>
        {
            var ranked = p.Group.Options.Keys.OrderByDescending(target => projections.Where(other => other.Row.Eligible && other.Row.Status == "In progress" && other.Group.Options.ContainsKey(target)).Select(other => other.Row.Id).Distinct().Count())
                .ThenBy(MissionTargets.Rank).ThenBy(target => p.Group.Options[target]).ThenBy(target => target, StringComparer.OrdinalIgnoreCase);
            return (allChoices && p.Group.IsChoice ? ranked : ranked.Take(1)).Select(target => new { p.Row, p.Group, Target = target });
        });
        return assigned.GroupBy(p => p.Target, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Any(p => p.Row.ExplicitlyTracked || !p.Row.Status.StartsWith("Cooldown", StringComparison.Ordinal)))
            .Select(g =>
        {
            var members = g.Select(p => p.Row).GroupBy(t => t.Id).Select(group => group.First()).OrderByDescending(t => t.Status == "In progress" && t.Eligible).ToList();
            if (g.Key.StartsWith("task:", StringComparison.Ordinal)) return members[0];
            bool location = !g.Key.StartsWith("objective:", StringComparison.Ordinal) && !g.Key.StartsWith("item:", StringComparison.Ordinal);
            string label = location ? g.Key : g.First().Group.Label;
            int later = members.Count(t => t.Status == "Locked" || t.Status.StartsWith("Cooldown", StringComparison.Ordinal));
            var activeGroups = g.Where(p => p.Row.Status == "In progress" && p.Row.Eligible).ToList();
            int left = (activeGroups.Count > 0 ? activeGroups : g.ToList()).Max(p => p.Group.Options[g.Key]);
            var result = new MissionTaskView
            {
                Id = members[0].Id, SourceIds = members.Select(t => t.Id).ToArray(), Title = label + " × " + left + (members.Count > 1 ? " · " + members.Count + " tasks" : "") + (later > 0 ? " · " + later + " later" : ""),
                Detail = (location ? "Suggested farming option: " + g.Key + Environment.NewLine + MissionTargetHints.Dungeon(g.Key) : label) + (allChoices && g.Any(p => p.Group.IsChoice) ? Environment.NewLine + "Choice alternative: completing one option satisfies its mission group." : "") + (later > 0 ? Environment.NewLine + "Locked and cooldown missions can progress after they become available; their counts are not included in the active target." : "") + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, members.Select(m => m.Title + " — " + m.Status + Environment.NewLine + m.Detail)),
                Eligible = members.Any(t => t.Eligible && t.Status == "In progress"), Progress = members.Where(t => t.Status == "In progress").DefaultIfEmpty(members[0]).Average(t => t.Progress), Status = members.Count > 1 ? (later > 0 ? "Linked objectives" : "Progress together") : members[0].Status, IsQuest = members.All(t => t.IsQuest), Pinned = true, Targets = new[] { g.Key }
            };
            return result;
        }).OrderByDescending(t => t.Status == "Ready to claim").ThenByDescending(t => t.Eligible).ThenByDescending(t => t.Progress).ToList();
    }

    internal static DateTime NextTaskRefreshUtc(DateTime utcNow)
    {
        lock (Sync)
        {
            EnsureAccount();
            utcNow = utcNow.ToUniversalTime();
            DateTime next = utcNow.Date.AddDays(1);
            long now = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
            foreach (var definition in catalog.Definitions)
            {
                if (!catalog.Progress.TryGetValue(definition.Key, out var progress) || (progress.RawState != 2 && progress.RawState != 3)
                    || definition.IntervalDays <= 0 || progress.ClaimedAt <= 0) continue;
                double resetValue = progress.ClaimedAt + Math.Truncate(definition.IntervalDays * 86400);
                if (resetValue > 253402300799 || resetValue <= 0) continue;
                long reset = (long)resetValue;
                if (reset <= now) return utcNow;
                // Cooldowns display whole remaining minutes. Their next displayed
                // change is relative to the claim time, not the wall-clock minute.
                long change = Math.Min(reset, now + (reset - now) % 60 + 1);
                DateTime when = DateTimeOffset.FromUnixTimeSeconds(change).UtcDateTime;
                if (when < next) next = when;
            }
            foreach (var quest in cache.Quests)
            {
                DateTimeOffset? expires = QuestExpiration(quest.Expiration);
                if (expires.HasValue && expires.Value.UtcDateTime > utcNow && expires.Value.UtcDateTime < next)
                    next = expires.Value.UtcDateTime;
            }
            return next;
        }
    }

    public static void SetAutoTracking(bool quests, bool value)
    {
        lock (Sync)
        {
            EnsureAccount();
            if ((quests ? preferences.AutoTrackQuests : preferences.AutoTrackMissions) == value) return;
            if (quests) preferences.AutoTrackQuests = value; else preferences.AutoTrackMissions = value;
            string prefix = quests ? "quest:" : "mission:";
            foreach (string key in preferences.TrackingOverrides.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) preferences.TrackingOverrides.Remove(key);
            preferences.Pinned.RemoveAll(k => k.StartsWith(prefix, StringComparison.Ordinal));
            RecordTrackingReset(prefix);
            SavePreferences();
        }
    }
    public static void TogglePin(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        lock (Sync)
        {
            EnsureAccount();
            bool value = preferences.Hidden.Contains(id) || !TrackingEnabled(id);
            preferences.TrackingOverrides[id] = value;
            preferences.Pinned.Remove(id);
            if (value) { preferences.Pinned.Add(id); preferences.Hidden.Remove(id); }
            SavePreferences();
        }
    }
    public static void ToggleHidden(string id)
    {
        lock (Sync)
        {
            EnsureAccount(); Toggle(preferences.Hidden, id);
            if (preferences.Hidden.Contains(id)) { preferences.Pinned.Remove(id); preferences.TrackingOverrides[id] = false; }
            SavePreferences();
        }
    }
    public static void ToggleCategory(string category) { lock (Sync) { EnsureAccount(); Toggle(preferences.HiddenCategories, category); SavePreferences(); } }
    public static void SetQuestStorage(string storage) { lock (Sync) { EnsureAccount(); if (!new[] { "Both", "Seasonal", "Regular" }.Contains(storage)) return; preferences.QuestStorage = storage; SavePreferences(); } }
    public static void SetShowAllChoices(bool value) { lock (Sync) { EnsureAccount(); preferences.ShowAllChoices = value; SavePreferences(); } }
    public static void SetPreferSeasonalMarks(bool value) { lock (Sync) { EnsureAccount(); preferences.PreferSeasonalMarks = value; SavePreferences(); } }
    public static MissionPagePreferences GetPagePreferences(bool quests) { lock (Sync) { EnsureAccount(); return (quests ? preferences.QuestsPage : preferences.MissionsPage).Clone(); } }
    public static void SetPagePreferences(bool quests, MissionPagePreferences value) { lock (Sync) { EnsureAccount(); if (quests) preferences.QuestsPage = value.Clone(); else preferences.MissionsPage = value.Clone(); SavePreferences(); } }
    public static void MoveBefore(string id, string before)
    {
        lock (Sync)
        {
            EnsureAccount(); if (id == before) return;
            var all = GetMissions().Concat(GetQuests()).Select(v => v.Id).ToList();
            all.Remove(id); int index = all.IndexOf(before); all.Insert(index < 0 ? all.Count : index, id);
            preferences.Order = all; SavePreferences();
        }
    }
    public static string QuestSummary { get { lock (Sync) { EnsureAccount(); return cache.Quests.Count == 0 ? "Visit the Tinkerer to load daily and event quests." : "Received " + cache.QuestsReceivedUtc.ToLocalTime().ToString("g") + " · visit the Tinkerer after the daily reset"; } } }
    private static IEnumerable<MissionTaskView> Sort(IEnumerable<MissionTaskView> rows) => rows.OrderBy(row => { int order = preferences.Order.IndexOf(row.Id); return order < 0 ? int.MaxValue : order; }).ThenBy(row => row.Category).ThenBy(row => row.Title);
    private static bool TrackingEnabled(string id)
    {
        if (preferences.TrackingOverrides.TryGetValue(id, out bool tracked)) return tracked;
        if (preferences.Pinned.Contains(id)) return true; // Legacy explicit selections.
        if (id.StartsWith("mission:", StringComparison.Ordinal)) return preferences.AutoTrackMissions;
        var quest = cache.Quests.FirstOrDefault(q => q.Key == id);
        return quest != null && preferences.AutoTrackQuests && QuestCategory(quest) == "Daily quest chests";
    }
    private static MissionTaskView Decorate(MissionTaskView row)
    {
        row.Hidden = preferences.Hidden.Contains(row.Id) || preferences.HiddenCategories.Contains(row.Category);
        row.Pinned = !row.Hidden && TrackingEnabled(row.Id);
        row.ExplicitlyTracked = preferences.TrackingOverrides.TryGetValue(row.Id, out bool tracked) ? tracked : preferences.Pinned.Contains(row.Id);
        row.SourceIds = new[] { row.Id }; return row;
    }
    private static void Toggle(List<string> values, string value) { if (!values.Remove(value)) values.Add(value); }
    private static void SavePreferences() { QueuePreferencesSave(); revision++; }
    private static int ReadCount(PacketReader r, int maximum) { int count = r.ReadInt16(); if (count < 0 || count > maximum) throw new InvalidDataException(); return count; }
    private static string ReadText(PacketReader r) { int count = r.ReadUInt16(); if (count > r.BaseStream.Length - r.BaseStream.Position) throw new EndOfStreamException(); return Encoding.UTF8.GetString(r.ReadBytes(count)); }
    private static int[] ReadItems(PacketReader r) { int count = ReadCount(r, 1000); if (count * 4 > r.BaseStream.Length - r.BaseStream.Position) throw new EndOfStreamException(); var result = new int[count]; for (int i = 0; i < count; i++) result[i] = r.ReadInt32(); return result; }
    internal static string ItemName(int id) => ItemSpriteManager.GetItemMetadata(id).Name ?? "Item " + id;
    private static string QuestCategory(MissionQuest quest)
    {
        if (quest.Rewards.Any(id => ItemName(id).IndexOf("quest chest", StringComparison.OrdinalIgnoreCase) >= 0)) return "Daily quest chests";
        string name = quest.Name.ToLowerInvariant(); int category = quest.Category;
        if (category == 0) return new[] { "eggs", "scout", "potion fusion", "laundry" }.Any(name.Contains) ? "Daily misc" : "Daily quest chests";
        if ((category >= 999 && category <= 1001) || (category >= 6999 && category <= 7000) || (category >= 7162 && category <= 7163))
            return name.Contains("craftsmanship") ? "Weekly schematic exchange" : name.Contains("ore refin") ? "Exchange" : "Permanent account rewards";
        if ((category >= 7010 && category <= 7012) || category == 11000) return "Exchange";
        if (category == 9080 || (category >= 9095 && category <= 9097)) return "Skin exchange";
        return "Other";
    }
    private static DateTimeOffset? QuestExpiration(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            return seconds > 0 && seconds <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds((long)seconds) : (DateTimeOffset?)null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) ? timestamp : (DateTimeOffset?)null;
    }
    private static string RewardText(MissionDefinition d)
    {
        var groups = Enumerable.Range(0, d.Rewards.Count).GroupBy(i => i < d.RewardSpread.Length ? d.RewardSpread[i].ToString() : "reward:" + i);
        return string.Join(" + ", groups.Select(g => (g.Count() > 1 ? "Choose: " : "") + string.Join(" or ", g.Select(i => { var r = d.Rewards[i]; return r.Type == 1 ? r.Amount.ToString("N0") + " battle XP" : int.TryParse(r.Target, out int id) ? ItemName(id) + " × " + r.Amount : r.Amount + " " + r.Target; }))));
    }
    private static bool Qualifies(MissionDefinition definition)
    {
        if (definition.Participants == 2 && characterSeasonal != true) return false;
        if (definition.Participants == 4 && characterCrucible != true) return false;
        if (definition.Participants != 0 && definition.Participants != 2 && definition.Participants != 4 && definition.Participants != 255) return false;
        return definition.Worn.Length == 0 || definition.Worn.Any(w => SlotTypes.TryGetValue(w, out int slot) && equippedSlots.Contains(slot));
    }
    private static readonly Dictionary<string, int> SlotTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "SWORD", 1 }, { "DAGGER", 2 }, { "BOW", 3 }, { "TOME", 4 }, { "SHIELD", 5 }, { "LIGHT_ARMOR", 6 }, { "LEATHER", 6 }, { "LEATHER_ARMOR", 6 }, { "HEAVY_ARMOR", 7 }, { "HEAVY", 7 }, { "WAND", 8 }, { "RING", 9 }, { "SPELL", 11 }, { "SEAL", 12 }, { "CLOAK", 13 }, { "ROBE", 14 }, { "QUIVER", 15 }, { "HELM", 16 }, { "HELMET", 16 }, { "STAFF", 17 }, { "POISON", 18 }, { "SKULL", 19 }, { "TRAP", 20 }, { "ORB", 21 }, { "PRISM", 22 }, { "SCEPTER", 23 }, { "KATANA", 24 }, { "STAR", 25 }, { "WAKIZASHI", 27 }, { "WAKI", 27 }, { "LUTE", 28 }, { "MACE", 29 }, { "SHEATH", 30 }, { "SIGIL", 31 }
    };
}
