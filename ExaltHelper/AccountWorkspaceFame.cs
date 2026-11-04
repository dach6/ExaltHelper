using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExaltHelper;

// PCStats ordering and fame tables follow RealmHound a4c6f74 (MIT).
internal static class AccountWorkspaceFame
{
    internal sealed class Line
    {
        public string Name;
        public long Flat;
        public double Percent;
        public string Progress;
        public long Bonus(long fame) => Flat + (long)Math.Ceiling(Math.Max(0, fame) * Percent / 100d);
    }

    public static Dictionary<string, int> Decode(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return new Dictionary<string, int>();
        try
        {
            if (encoded.Length > 32768) return null;
            byte[] bytes = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/'));
            if (bytes.Length < 4) return null;
            if (BitConverter.ToInt32(bytes, 0) == 0) return new Dictionary<string, int>();
            Dictionary<string, int> best = null; long bestScore = long.MaxValue;
            foreach (int size in new[] { 32, 16 })
            {
                if (bytes.Length < size + 4) continue;
                try
                {
                    int position = size + 4, unknown = 0;
                    var result = new Dictionary<string, int>();
                    foreach (int bit in AccountWorkspaceReferenceData.ReadOrder)
                    {
                        if (bit >= size * 8 || (bytes[4 + bit / 8] & (1 << bit % 8)) == 0) continue;
                        result[AccountWorkspaceReferenceData.Stats[bit]] = ReadInteger(bytes, ref position);
                    }
                    for (int bit = 0; bit < size * 8; bit++)
                        if ((bytes[4 + bit / 8] & (1 << bit % 8)) != 0 && !AccountWorkspaceReferenceData.Stats.ContainsKey(bit)) unknown++;
                    long score = (bytes.Length - position) * 1024L + unknown;
                    if (score < bestScore) { bestScore = score; best = result; }
                }
                catch (InvalidDataException) { }
            }
            return best;
        }
        catch (FormatException) { return null; }
    }

    private static int ReadInteger(byte[] bytes, ref int position)
    {
        if (position >= bytes.Length) throw new InvalidDataException();
        byte first = bytes[position++], current = first;
        long value = first & 63; int shift = 6, count = 1;
        while ((current & 128) != 0)
        {
            if (count++ >= 5 || position >= bytes.Length) throw new InvalidDataException();
            current = bytes[position++]; value |= (long)(current & 127) << shift; shift += 7;
        }
        if (value > int.MaxValue) throw new InvalidDataException();
        return (first & 64) != 0 ? -(int)value : (int)value;
    }

    internal static long Tiered(int value, int[] thresholds, int[] bonuses)
    {
        long result = 0;
        for (int i = 0; i < 4; i++) if (value >= thresholds[i]) result += bonuses[i];
        if (thresholds[4] > 0) result += Math.Min(20, Math.Max(0, value) / thresholds[4]) * (long)bonuses[4];
        return result;
    }

    public static List<Line> Breakdown(AccountCharacter character)
    {
        var result = new List<Line>();
        int[] max = AccountWorkspace.ClassMaximums(character.ClassType);
        string[] statNames = { "HP", "MP", "ATT", "DEF", "SPD", "DEX", "VIT", "WIS" };
        for (int i = 0; i < 8; i++) if (max[i] > 0 && character.Stats[i] >= max[i])
            result.Add(new Line { Name = "Maxed " + statNames[i], Flat = i < 2 ? 200 : 100, Percent = i < 2 ? 5 : 2.5, Progress = "Maxed" });
        void Add(string key, int[] thresholds, int[] bonuses)
        {
            int value = character.PcStats.TryGetValue(key, out int count) ? count : 0;
            long bonus = Tiered(value, thresholds, bonuses);
            int next = thresholds.FirstOrDefault(t => t > value);
            if (next == 0 && value / thresholds[4] < 20) next = (value / thresholds[4] + 1) * thresholds[4];
            result.Add(new Line { Name = DisplayName(key), Flat = bonus, Progress = next == 0 ? "All milestones earned" : value.ToString("N0") + " / " + next.ToString("N0") + " to next tier" });
        }
        Add("tiles_discovered", new[] { 200000, 1000000, 4000000, 10000000, 25000000 }, new[] { 20, 100, 400, 1000, 2500 });
        Add("quests_completed", new[] { 25, 100, 400, 1000, 2000 }, new[] { 20, 200, 800, 2000, 5000 });
        Add("hits", new[] { 8000, 40000, 200000, 800000, 2000000 }, new[] { 20, 100, 400, 1000, 2500 });
        Add("party_level_ups", new[] { 25, 50, 100, 400, 1000 }, new[] { 20, 50, 200, 400, 1000 });
        Add("potions_drunk", new[] { 25, 100, 400, 1000, 2000 }, new[] { 20, 50, 200, 400, 1000 });
        Add("ability_used", new[] { 100, 500, 2500, 10000, 25000 }, new[] { 20, 50, 200, 400, 1000 });
        Add("teleports", new[] { 50, 200, 500, 1000, 2500 }, new[] { 20, 50, 200, 400, 1000 });
        Add("kills", new[] { 2000, 5000, 10000, 50000, 100000 }, new[] { 20, 50, 200, 400, 1000 });
        Add("god_kills", new[] { 200, 1000, 5000, 10000, 50000 }, new[] { 20, 100, 400, 1000, 2500 });
        Add("lesser_gods_kills", new[] { 100, 500, 2000, 5000, 25000 }, new[] { 20, 100, 400, 1000, 2500 });
        Add("oryx_kills", new[] { 1, 20, 100, 250, 500 }, new[] { 200, 500, 1000, 3000, 8000 });
        Add("encounter_kills", new[] { 5, 50, 200, 500, 1250 }, new[] { 20, 200, 800, 2000, 5000 });
        Add("hero_kills", new[] { 5, 100, 500, 1250, 2500 }, new[] { 20, 100, 400, 1000, 2500 });
        foreach (var key in new[] { "cube", "critter", "beast", "humanoid", "undead", "nature", "construct", "grotesque", "structure" })
            Add(key + "_kills", new[] { 200, 1000, 5000, 10000, 50000 }, new[] { 20, 100, 400, 1000, 2500 });
        foreach (var key in AccountWorkspaceReferenceData.Stats.Values.Where(s => s.StartsWith("biomes.")))
            // Matches the currently observed game payout (tiers 2 and 4 awarded early).
            Add(key, new[] { 200, 200, 5000, 5000, 50000 }, new[] { 20, 100, 400, 1000, 2500 });
        foreach (var entry in AccountWorkspaceReferenceData.DungeonNames)
            if (AccountWorkspaceReferenceData.DungeonBonuses.TryGetValue(entry.Value, out int[] bonuses))
                Add("dungeons." + entry.Key, new[] { 1, 10, 20, 40, 100 }, bonuses);
        foreach (var collection in AccountWorkspaceReferenceData.Collections)
        {
            var missing = collection.Dungeons.Where(key => !character.PcStats.TryGetValue("dungeons." + key, out int count) || count < 1).ToArray();
            result.Add(new Line { Name = collection.Name, Flat = missing.Length == 0 ? collection.Flat : 0,
                Percent = missing.Length == 0 ? collection.Percent : 0, Progress = missing.Length == 0 ? "Collection complete"
                    : "Missing: " + string.Join(", ", missing.Select(k => AccountWorkspaceReferenceData.DungeonNames.TryGetValue(k, out string name) ? name : k)) });
        }
        return result;
    }

    public static string DisplayName(string key)
    {
        if (key.StartsWith("dungeons.") && AccountWorkspaceReferenceData.DungeonNames.TryGetValue(key.Substring(9), out string name)) return name;
        return CultureName(key.Replace("biomes.", "").Replace('_', ' '));
    }
    private static string CultureName(string text) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
}
