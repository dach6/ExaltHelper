using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace ExaltHelper;

// Mission protocol and grouping follow RealmHound (MIT, Lovens & Pillar, 2026).
// See THIRD-PARTY-NOTICES.md. These models contain no authentication material.
internal sealed class MissionObjective
{
    public int Type { get; set; }
    public string Target { get; set; } = "";
    public int Need { get; set; }
    public int TimeLimit { get; set; }
    public string Label
    {
        get
        {
            string target = Target.Replace('_', ' ');
            switch (Type)
            {
                case 1: return "Defeat " + target;
                case 2: return "Defeat enemies";
                case 3: return "Complete " + target;
                case 4: return "Complete difficulty " + target;
                case 7: return "Close realms";
                case 12: return "Complete difficulty " + target;
                case 14: return "Complete " + target + (TimeLimit > 0 ? " in under " + TimeLimit + "s" : "");
                case 15: return "Gain EXP";
                default: return string.IsNullOrWhiteSpace(target) ? "Objective type " + Type : target;
            }
        }
    }
}

internal sealed class MissionReward
{
    public int Type { get; set; }
    public string Target { get; set; } = "";
    public long Amount { get; set; }
}

internal sealed class MissionDefinition
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = "";
    public long SeasonEnd { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Spread { get; set; } = "";
    public string RewardSpread { get; set; } = "";
    public int[] Parents { get; set; } = Array.Empty<int>();
    public int Participants { get; set; }
    public double IntervalDays { get; set; }
    public string[] Worn { get; set; } = Array.Empty<string>();
    public List<MissionObjective> Objectives { get; set; } = new List<MissionObjective>();
    public List<MissionReward> Rewards { get; set; } = new List<MissionReward>();
    public string Key => "mission:" + SeasonId + ":" + Id;
    public List<int[]> Groups()
    {
        if (string.IsNullOrEmpty(Spread)) return Enumerable.Range(0, Objectives.Count).Select(i => new[] { i }).ToList();
        return Enumerable.Range(0, Objectives.Count).GroupBy(i => i < Spread.Length ? Spread[i].ToString() : "extra:" + i).Select(g => g.ToArray()).ToList();
    }
    public double Fraction(int[] progress)
    {
        var groups = Groups();
        if (groups.Count == 0) return 0;
        return groups.Average(g => g.Max(i => Math.Min(1.0, Math.Max(0, i < progress.Length ? progress[i] : 0) / (double)Math.Max(1, Objectives[i].Need))));
    }
    public bool Complete(int[] progress) => Objectives.Count > 0 && Groups().All(g => g.Any(i => i < progress.Length && progress[i] >= Math.Max(1, Objectives[i].Need)));
}

internal sealed class MissionProgress
{
    public int[] Values { get; set; } = Array.Empty<int>();
    public int RawState { get; set; }
    public long ClaimedAt { get; set; }
    public int ClaimCount { get; set; }
    public bool EverSatisfied { get; set; }
}

internal sealed class MissionQuest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Expiration { get; set; } = "";
    public int Category { get; set; }
    public int[] Requirements { get; set; } = Array.Empty<int>();
    public int[] Rewards { get; set; } = Array.Empty<int>();
    public bool Completed { get; set; }
    public bool Repeatable { get; set; }
    public bool Choice { get; set; }
    public string Key => "quest:" + Id;
}

internal sealed class MissionTaskView
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Detail { get; set; }
    public double Progress { get; set; }
    public string Status { get; set; }
    public bool Eligible { get; set; }
    public bool IsQuest { get; set; }
    public bool Pinned { get; set; }
    public bool ExplicitlyTracked { get; set; }
    public bool Hidden { get; set; }
    public string Category { get; set; }
    public string Rewards { get; set; }
    public string[] Targets { get; set; } = Array.Empty<string>();
    public Dictionary<string, int> RemainingByTarget { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public List<MissionTaskTargetGroup> TaskGroups { get; set; } = new List<MissionTaskTargetGroup>();
    public string[] SourceIds { get; set; } = Array.Empty<string>();
}

internal sealed class MissionTaskTargetGroup
{
    public string Label { get; set; } = "";
    public bool IsChoice { get; set; }
    public Dictionary<string, int> Options { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class MissionPreferences
{
    public List<string> Pinned { get; set; } = new List<string>();
    public List<string> Hidden { get; set; } = new List<string>();
    public List<string> Order { get; set; } = new List<string>();
    public List<string> HiddenCategories { get; set; } = new List<string>();
    public bool AutoTrackMissions { get; set; } = true;
    public bool AutoTrackQuests { get; set; } = true;
    public Dictionary<string, bool> TrackingOverrides { get; set; } = new Dictionary<string, bool>();
    public string QuestStorage { get; set; } = "Both";
    public MissionPagePreferences MissionsPage { get; set; } = new MissionPagePreferences();
    public MissionPagePreferences QuestsPage { get; set; } = new MissionPagePreferences();
    public bool ShowAllChoices { get; set; } = true;
    public bool PreferSeasonalMarks { get; set; } = true;
}

internal sealed class MissionPagePreferences
{
    public string Status { get; set; } = "All statuses";
    public string Category { get; set; } = "All categories";
    public bool ShowHidden { get; set; }
    public List<string> Collapsed { get; set; } = new List<string>();
    public MissionPagePreferences Clone() => new MissionPagePreferences { Status = Status, Category = Category, ShowHidden = ShowHidden, Collapsed = new List<string>(Collapsed ?? new List<string>()) };
}

internal sealed class MissionCache
{
    public List<MissionQuest> Quests { get; set; } = new List<MissionQuest>();
    public DateTime QuestsReceivedUtc { get; set; }
    public DateTime DailySetUtc { get; set; }
}

internal sealed class MissionCatalog
{
    public List<MissionDefinition> Definitions { get; set; } = new List<MissionDefinition>();
    public Dictionary<string, MissionProgress> Progress { get; set; } = new Dictionary<string, MissionProgress>();
    public HashSet<string> Locked { get; set; } = new HashSet<string>();
    public HashSet<string> Unlocked { get; set; } = new HashSet<string>();

    public static MissionCatalog Parse(string definitions, string progress)
    {
        var result = new MissionCatalog();
        var serializer = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024, RecursionLimit = 80 };
        var document = serializer.DeserializeObject(definitions) as Dictionary<string, object>;
        if (document == null || !document.ContainsKey("seasons")) throw new FormatException("Mission definitions were not recognized.");
        foreach (var season in Objects(Value(document, "seasons")))
        {
            int seasonId = Int(season, "id");
            if (seasonId <= 0) continue;
            long end = Long(season, "endDate");
            // The API may include multiple concurrent mission trees.
            if (end > 0 && end < DateTimeOffset.UtcNow.ToUnixTimeSeconds() && Int(season, "current") != 1 && Int(season, "available") != 1) continue;
            foreach (var source in Objects(Value(season, "missions")))
            {
                var item = new MissionDefinition
                {
                    SeasonId = seasonId, SeasonName = Text(season, "name"), SeasonEnd = end,
                    Id = Int(source, "id"), Name = Text(source, "name"), Description = Text(source, "desc"),
                    Spread = Text(source, "condSpread"), RewardSpread = Text(source, "rewardsSpread"),
                    Parents = Regex.Matches(Text(source, "parentsStr"), @"\d+").Cast<Match>().Select(m => int.TryParse(m.Value, out int p) ? p : 0).Where(p => p > 0).ToArray(),
                    Participants = Int(source, "participants"), IntervalDays = Math.Max(0, Number(source, "interval")),
                    Worn = Objects(Value(source, "wornRestriction")).Select(w => Text(w, "type")).Where(w => w.Length > 0).ToArray()
                };
                foreach (var c in Objects(Value(source, "conds"))) item.Objectives.Add(new MissionObjective { Type = Int(c, "type"), Target = Text(c, "target"), Need = Math.Max(0, Int(c, "amount")), TimeLimit = Int(c, "time") });
                foreach (var reward in Objects(Value(source, "rewards"))) item.Rewards.Add(new MissionReward { Type = Int(reward, "type"), Target = Text(reward, "target"), Amount = Math.Max(0, Long(reward, "amount")) });
                result.Definitions.Add(item);
            }
        }
        using var reader = XmlReader.Create(new System.IO.StringReader(progress), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 });
        var xml = XDocument.Load(reader);
        if (xml.Root?.Name.LocalName != "Missions") throw new FormatException("Mission progress was not recognized.");
        foreach (var season in xml.Descendants("Season"))
        {
            if (!int.TryParse((string)season.Attribute("id"), out int seasonId)) continue;
            foreach (var row in season.Value.Split('|'))
            {
                string[] values = row.Split(',');
                if (values.Length < 3 || !int.TryParse(values[0], out int id) || !int.TryParse(values[2], out int state)) continue;
                if (!TryNumbers(values[1], out int[] counts)) continue;
                long claimed = values.Length > 5 && long.TryParse(values[5], out long ts) ? Math.Max(0, ts) : 0;
                int claims = values.Length > 4 && int.TryParse(values[4], out int c) ? Math.Max(0, c) : 0;
                string key = "mission:" + seasonId + ":" + id;
                var def = result.Definitions.FirstOrDefault(m => m.Key == key);
                result.Progress[key] = new MissionProgress { Values = counts, RawState = state, ClaimCount = claims, ClaimedAt = claimed, EverSatisfied = state == 2 || state == 3 || claims > 0 || claimed > 0 || (def?.Complete(counts) ?? false) };
            }
        }
        result.Locked = ParsePairs(xml.Root.Element("LockedMissions")?.Value);
        result.Unlocked = ParsePairs(xml.Root.Element("UnlockedMissions")?.Value);
        return result;
    }

    public void ApplyDelta(string message)
    {
        if (message.StartsWith("pool/#", StringComparison.Ordinal)) message = message.Substring(6);
        if (!message.StartsWith("prog/", StringComparison.Ordinal)) return;
        foreach (string part in message.Substring(5).Split('|'))
        {
            string[] v = part.Split(',');
            if (v.Length != 3 || !int.TryParse(v[0], out int season) || !int.TryParse(v[1], out int id) || !TryNumbers(v[2], out int[] delta)) continue;
            string key = "mission:" + season + ":" + id;
            var def = Definitions.FirstOrDefault(d => d.Key == key);
            if (def == null) continue;
            if (!Progress.TryGetValue(key, out var runtime)) Progress[key] = runtime = new MissionProgress();
            int[] values = runtime.Values;
            if (values.Length < delta.Length) Array.Resize(ref values, delta.Length);
            for (int i = 0; i < delta.Length; i++) values[i] = (int)Math.Max(0, Math.Min(int.MaxValue, (long)values[i] + delta[i]));
            runtime.Values = values;
            if (def.Complete(values)) runtime.EverSatisfied = true;
        }
    }

    public string Status(MissionDefinition definition, long now)
    {
        Progress.TryGetValue(definition.Key, out var runtime);
        if (runtime != null && (runtime.RawState == 2 || runtime.RawState == 3))
        {
            if (definition.IntervalDays <= 0) return "Claimed";
            if (runtime.ClaimedAt <= 0) return "Cooldown";
            long reset = runtime.ClaimedAt + (long)(definition.IntervalDays * 86400);
            if (reset > now) return "Cooldown · " + TimeSpan.FromSeconds(reset - now).ToString(@"d\d\ hh\h\ mm\m", CultureInfo.InvariantCulture);
            runtime.Values = new int[definition.Objectives.Count];
            runtime.RawState = 0;
            runtime.ClaimedAt = 0;
        }
        bool engaged = runtime != null && (runtime.EverSatisfied || runtime.RawState == 1 || runtime.Values.Any(v => v > 0));
        bool parentsDone = definition.Parents.All(id =>
        {
            string key = "mission:" + definition.SeasonId + ":" + id;
            if (!Progress.TryGetValue(key, out var parent)) return false;
            return parent.EverSatisfied || parent.RawState == 1 || parent.RawState == 2 || parent.RawState == 3;
        });
        if (!engaged && (Locked.Contains(definition.Key) || (!parentsDone && !Unlocked.Contains(definition.Key)))) return "Locked";
        if (runtime?.RawState == 1) return "Ready to claim";
        if (runtime != null && runtime.RawState > 3) return "Unknown server state " + runtime.RawState;
        if (definition.Complete(runtime?.Values ?? Array.Empty<int>())) return "Ready to claim";
        return "In progress";
    }

    private static HashSet<string> ParsePairs(string value)
    {
        var result = new HashSet<string>();
        foreach (string part in (value ?? "").Split(';'))
        {
            var p = part.Split(',');
            if (p.Length == 2 && int.TryParse(p[0], out int season) && int.TryParse(p[1], out int id)) result.Add("mission:" + season + ":" + id);
        }
        return result;
    }
    private static bool TryNumbers(string text, out int[] values)
    {
        var parts = text.Split(':'); values = new int[parts.Length];
        if (parts.Length > 128) return false;
        for (int i = 0; i < parts.Length; i++) if (!int.TryParse(parts[i], out values[i])) return false;
        return values.Length > 0;
    }
    private static object Value(Dictionary<string, object> d, string key) => d.TryGetValue(key, out object v) ? v : null;
    private static IEnumerable<Dictionary<string, object>> Objects(object value) => (value as object[] ?? Array.Empty<object>()).OfType<Dictionary<string, object>>();
    private static string Text(Dictionary<string, object> d, string key) => Convert.ToString(Value(d, key), CultureInfo.InvariantCulture) ?? "";
    private static int Int(Dictionary<string, object> d, string key) => int.TryParse(Text(d, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    private static long Long(Dictionary<string, object> d, string key) => long.TryParse(Text(d, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
    private static double Number(Dictionary<string, object> d, string key) => double.TryParse(Text(d, key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsInfinity(v) && !double.IsNaN(v) ? v : 0;
}
