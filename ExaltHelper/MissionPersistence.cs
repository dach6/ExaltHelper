using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExaltHelper;

internal static partial class MissionWorkspace
{
    private sealed class MissionAccountState
    {
        public MissionPreferences Preferences = new MissionPreferences();
        public MissionPreferences LastPreferences = new MissionPreferences();
        public MissionCache Cache = new MissionCache();
        public Task Loading;
        public bool Loaded;
        public long PreferencesVersion, QuestsVersion, SavedPreferencesVersion, SavedQuestsVersion;
        public readonly HashSet<string> ChangedFields = new HashSet<string>();
        public readonly Dictionary<string, Dictionary<string, bool>> ListEdits = new Dictionary<string, Dictionary<string, bool>>();
        public readonly Dictionary<string, bool?> TrackingEdits = new Dictionary<string, bool?>();
        public readonly HashSet<string> TrackingResets = new HashSet<string>();
    }
    private sealed class MissionSave
    {
        public string Account;
        public MissionAccountState State;
        public MissionPreferences Preferences;
        public MissionCache Cache;
        public long PreferencesVersion, QuestsVersion;
    }
    private static readonly Dictionary<string, MissionAccountState> accountStates = new Dictionary<string, MissionAccountState>();
    private static readonly object PersistenceGate = new object();
    private static readonly System.Threading.Timer persistenceTimer = new System.Threading.Timer(_ => Flush(), null, 2000, 2000);
    private static string persistenceError = "";

    // Caller holds Sync. First-load I/O is also deferred, so a new account's Hello
    // packet never waits for the filesystem. Accounts retain their latest in-memory
    // state while the background writer is catching up.
    private static void LoadAccountState(string key)
    {
        persistenceError = "";
        if (key.Length == 0) { preferences = new MissionPreferences(); cache = new MissionCache(); return; }
        if (!accountStates.TryGetValue(key, out var state))
        {
            state = new MissionAccountState(); accountStates[key] = state;
            var captured = state;
            state.Loading = Task.Run(() => LoadAccountFromDisk(key, captured));
        }
        preferences = state.Preferences; cache = state.Cache;
    }

    private static void LoadAccountFromDisk(string key, MissionAccountState state)
    {
        var loadedPreferences = HudStorage.Load("missions-" + key + "-preferences.json", new MissionPreferences()) ?? new MissionPreferences();
        var loadedCache = HudStorage.Load("missions-" + key + "-quests.json", new MissionCache()) ?? new MissionCache();
        loadedPreferences = ClonePreferences(loadedPreferences); loadedCache = CloneCache(loadedCache);
        lock (Sync)
        {
            // A user may edit preferences while disk reads are pending. Replay
            // only the changed fields, preserving the other saved preferences.
            MergePreferences(loadedPreferences, state.Preferences, state.ChangedFields, state.ListEdits, state.TrackingEdits, state.TrackingResets);
            state.Preferences = loadedPreferences; state.LastPreferences = ClonePreferences(loadedPreferences);
            state.ChangedFields.Clear(); state.ListEdits.Clear(); state.TrackingEdits.Clear(); state.TrackingResets.Clear();
            if (state.QuestsVersion == 0) state.Cache = loadedCache;
            state.Loaded = true;
            if (accountKey == key) { preferences = state.Preferences; cache = state.Cache; revision++; }
        }
    }

    private static void QueuePreferencesSave()
    {
        if (accountKey.Length == 0) return;
        if (!accountStates.TryGetValue(accountKey, out var state)) return;
        state.Preferences = preferences;
        if (!state.Loaded)
        {
            foreach (string field in DifferentFields(state.LastPreferences, preferences)) state.ChangedFields.Add(field);
            RecordListEdits(state, "Pinned", state.LastPreferences.Pinned, preferences.Pinned);
            RecordListEdits(state, "Hidden", state.LastPreferences.Hidden, preferences.Hidden);
            RecordListEdits(state, "HiddenCategories", state.LastPreferences.HiddenCategories, preferences.HiddenCategories);
            RecordListEdits(state, "MissionsPage.Collapsed", state.LastPreferences.MissionsPage.Collapsed, preferences.MissionsPage.Collapsed);
            RecordListEdits(state, "QuestsPage.Collapsed", state.LastPreferences.QuestsPage.Collapsed, preferences.QuestsPage.Collapsed);
            foreach (string id in preferences.Pinned.Except(state.LastPreferences.Pinned)) RecordListEdit(state, "Hidden", id, false);
            foreach (string id in preferences.Hidden.Except(state.LastPreferences.Hidden)) RecordListEdit(state, "Pinned", id, false);
            foreach (string id in state.LastPreferences.TrackingOverrides.Keys.Union(preferences.TrackingOverrides.Keys))
            {
                bool beforeExists = state.LastPreferences.TrackingOverrides.TryGetValue(id, out bool before);
                bool afterExists = preferences.TrackingOverrides.TryGetValue(id, out bool after);
                if (beforeExists != afterExists || before != after) state.TrackingEdits[id] = afterExists ? (bool?)after : null;
            }
            state.LastPreferences = ClonePreferences(preferences);
        }
        state.PreferencesVersion++;
    }
    private static void QueueQuestSave()
    {
        if (accountKey.Length == 0) return;
        if (!accountStates.TryGetValue(accountKey, out var state)) return;
        state.Cache = cache; state.QuestsVersion++;
    }

    // Only the timer, tests and application shutdown should call Flush. Packet/UI
    // observers enqueue versions and never acquire PersistenceGate or perform I/O.
    public static void Flush()
    {
        Task[] loads;
        lock (Sync) loads = accountStates.Values.Select(x => x.Loading).Where(x => x != null && !x.IsCompleted).ToArray();
        if (loads.Length > 0) Task.WaitAll(loads);
        lock (PersistenceGate)
        {
            List<MissionSave> saves;
            lock (Sync)
                saves = accountStates.Where(p => p.Value.Loaded && (p.Value.PreferencesVersion > p.Value.SavedPreferencesVersion || p.Value.QuestsVersion > p.Value.SavedQuestsVersion))
                    .Select(p => new MissionSave { Account = p.Key, State = p.Value,
                        PreferencesVersion = p.Value.PreferencesVersion, QuestsVersion = p.Value.QuestsVersion,
                        Preferences = p.Value.PreferencesVersion > p.Value.SavedPreferencesVersion ? ClonePreferences(p.Value.Preferences) : null,
                        Cache = p.Value.QuestsVersion > p.Value.SavedQuestsVersion ? CloneCache(p.Value.Cache) : null }).ToList();
            foreach (var save in saves)
            {
                if (save.Preferences != null)
                {
                    bool success = HudStorage.Save("missions-" + save.Account + "-preferences.json", save.Preferences);
                    string error = success ? null : HudStorage.LastError ?? "Mission preferences could not be saved; retrying.";
                    lock (Sync)
                    {
                        if (success) save.State.SavedPreferencesVersion = save.PreferencesVersion;
                        SetPersistenceStatus(save, error);
                    }
                }
                if (save.Cache != null)
                {
                    bool success = HudStorage.Save("missions-" + save.Account + "-quests.json", save.Cache);
                    string error = success ? null : HudStorage.LastError ?? "Quest cache could not be saved; retrying.";
                    lock (Sync)
                    {
                        if (success) save.State.SavedQuestsVersion = save.QuestsVersion;
                        SetPersistenceStatus(save, error);
                    }
                }
            }
        }
    }
    public static Task FlushAsync() => Task.Run((Action)Flush);
    private static void SetPersistenceStatus(MissionSave save, string error)
    {
        if (accountKey != save.Account) return;
        if (error != null) persistenceError = error;
        else if (save.State.PreferencesVersion == save.State.SavedPreferencesVersion && save.State.QuestsVersion == save.State.SavedQuestsVersion) persistenceError = "";
    }

    private static MissionPreferences ClonePreferences(MissionPreferences p) => new MissionPreferences {
        Pinned = new List<string>(p.Pinned ?? new List<string>()), Hidden = new List<string>(p.Hidden ?? new List<string>()),
        Order = new List<string>(p.Order ?? new List<string>()), HiddenCategories = new List<string>(p.HiddenCategories ?? new List<string>()),
        QuestStorage = new[] { "Both", "Seasonal", "Regular" }.Contains(p.QuestStorage) ? p.QuestStorage : "Both",
        MissionsPage = (p.MissionsPage ?? new MissionPagePreferences()).Clone(), QuestsPage = (p.QuestsPage ?? new MissionPagePreferences()).Clone(),
        AutoTrackMissions = p.AutoTrackMissions, AutoTrackQuests = p.AutoTrackQuests,
        TrackingOverrides = new Dictionary<string, bool>(p.TrackingOverrides ?? new Dictionary<string, bool>()),
        ShowAllChoices = p.ShowAllChoices, PreferSeasonalMarks = p.PreferSeasonalMarks };
    private static MissionCache CloneCache(MissionCache c) => new MissionCache { QuestsReceivedUtc = c.QuestsReceivedUtc, DailySetUtc = c.DailySetUtc,
        Quests = (c.Quests ?? new List<MissionQuest>()).Where(q => q != null).Select(q => new MissionQuest { Id = q.Id, Name = q.Name,
            Description = q.Description, Expiration = q.Expiration, Category = q.Category, Requirements = (int[])(q.Requirements ?? Array.Empty<int>()).Clone(),
            Rewards = (int[])(q.Rewards ?? Array.Empty<int>()).Clone(), Completed = q.Completed, Repeatable = q.Repeatable, Choice = q.Choice }).ToList() };

    private static IEnumerable<string> DifferentFields(MissionPreferences a, MissionPreferences b)
    {
        if (!a.Pinned.SequenceEqual(b.Pinned)) yield return "Pinned";
        if (!a.Hidden.SequenceEqual(b.Hidden)) yield return "Hidden";
        if (!a.Order.SequenceEqual(b.Order)) yield return "Order";
        if (!a.HiddenCategories.SequenceEqual(b.HiddenCategories)) yield return "HiddenCategories";
        if (a.QuestStorage != b.QuestStorage) yield return "QuestStorage";
        if (a.ShowAllChoices != b.ShowAllChoices) yield return "ShowAllChoices";
        if (a.PreferSeasonalMarks != b.PreferSeasonalMarks) yield return "PreferSeasonalMarks";
        if (a.AutoTrackMissions != b.AutoTrackMissions) yield return "AutoTrackMissions";
        if (a.AutoTrackQuests != b.AutoTrackQuests) yield return "AutoTrackQuests";
        foreach (string field in DifferentPageFields("MissionsPage", a.MissionsPage, b.MissionsPage)) yield return field;
        foreach (string field in DifferentPageFields("QuestsPage", a.QuestsPage, b.QuestsPage)) yield return field;
    }
    private static IEnumerable<string> DifferentPageFields(string prefix, MissionPagePreferences a, MissionPagePreferences b)
    {
        if (a.Status != b.Status) yield return prefix + ".Status";
        if (a.Category != b.Category) yield return prefix + ".Category";
        if (a.ShowHidden != b.ShowHidden) yield return prefix + ".ShowHidden";
    }
    private static void RecordListEdits(MissionAccountState state, string field, List<string> before, List<string> after)
    {
        var additions = after.Except(before).ToArray(); var removals = before.Except(after).ToArray();
        if (additions.Length == 0 && removals.Length == 0) return;
        foreach (string value in additions) RecordListEdit(state, field, value, true);
        foreach (string value in removals) RecordListEdit(state, field, value, false);
    }
    private static void RecordListEdit(MissionAccountState state, string field, string value, bool present)
    {
        if (!state.ListEdits.TryGetValue(field, out var edits)) state.ListEdits[field] = edits = new Dictionary<string, bool>();
        edits[value] = present;
    }
    private static void MergeList(List<string> into, string field, Dictionary<string, Dictionary<string, bool>> edits)
    {
        if (!edits.TryGetValue(field, out var changes)) return;
        foreach (var change in changes)
            if (change.Value) { if (!into.Contains(change.Key)) into.Add(change.Key); }
            else into.RemoveAll(x => x == change.Key);
    }
    private static void RecordTrackingReset(string prefix)
    {
        if (!accountStates.TryGetValue(accountKey, out var state) || state.Loaded) return;
        state.TrackingResets.Add(prefix);
        state.ChangedFields.Add(prefix == "quest:" ? "AutoTrackQuests" : "AutoTrackMissions");
        foreach (string id in state.TrackingEdits.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) state.TrackingEdits.Remove(id);
        if (state.ListEdits.TryGetValue("Pinned", out var pins))
            foreach (string id in pins.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) pins.Remove(id);
    }
    private static void MergePreferences(MissionPreferences into, MissionPreferences from, HashSet<string> fields, Dictionary<string, Dictionary<string, bool>> listEdits, Dictionary<string, bool?> trackingEdits, HashSet<string> trackingResets)
    {
        foreach (string prefix in trackingResets)
        {
            foreach (string id in into.TrackingOverrides.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) into.TrackingOverrides.Remove(id);
            into.Pinned.RemoveAll(k => k.StartsWith(prefix, StringComparison.Ordinal));
        }
        MergeList(into.Pinned, "Pinned", listEdits); MergeList(into.Hidden, "Hidden", listEdits); MergeList(into.HiddenCategories, "HiddenCategories", listEdits);
        foreach (var edit in trackingEdits)
            if (edit.Value.HasValue) into.TrackingOverrides[edit.Key] = edit.Value.Value;
            else into.TrackingOverrides.Remove(edit.Key);
        if (fields.Contains("Order")) into.Order = from.Order.Concat(into.Order.Except(from.Order)).ToList();
        if (fields.Contains("QuestStorage")) into.QuestStorage = from.QuestStorage;
        if (fields.Contains("ShowAllChoices")) into.ShowAllChoices = from.ShowAllChoices;
        if (fields.Contains("PreferSeasonalMarks")) into.PreferSeasonalMarks = from.PreferSeasonalMarks;
        if (fields.Contains("AutoTrackMissions")) into.AutoTrackMissions = from.AutoTrackMissions;
        if (fields.Contains("AutoTrackQuests")) into.AutoTrackQuests = from.AutoTrackQuests;
        MergePage(into.MissionsPage, from.MissionsPage, "MissionsPage", fields, listEdits);
        MergePage(into.QuestsPage, from.QuestsPage, "QuestsPage", fields, listEdits);
    }
    private static void MergePage(MissionPagePreferences into, MissionPagePreferences from, string prefix, HashSet<string> fields, Dictionary<string, Dictionary<string, bool>> listEdits)
    {
        if (fields.Contains(prefix + ".Status")) into.Status = from.Status;
        if (fields.Contains(prefix + ".Category")) into.Category = from.Category;
        if (fields.Contains(prefix + ".ShowHidden")) into.ShowHidden = from.ShowHidden;
        MergeList(into.Collapsed, prefix + ".Collapsed", listEdits);
    }
}
