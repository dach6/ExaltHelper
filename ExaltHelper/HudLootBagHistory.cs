using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExaltHelper;

// A display projection only: journal retention and lifetime discoveries continue
// to count individual recorded item instances.
internal sealed class HudLootBagHistory
{
    public string Id { get; }
    public HudLootRecord[] Items { get; }
    public DateTime Time => Items[0].Time;
    public string Bag => Items[0].Bag;
    public string Enemy => Items[0].Enemy;
    public string Dungeon => Items[0].Dungeon;
    public string Character => Items[0].Character;
    public bool Shiny => Items.Any(item => item.Shiny);

    private HudLootBagHistory(string id, IEnumerable<HudLootRecord> records)
    {
        Id = id;
        var items = new List<HudLootRecord>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            // An updated copy of one recorded slot must not produce two icons;
            // identical item types in different slots remain distinct instances.
            if (!string.IsNullOrEmpty(record.Id) && positions.TryGetValue(record.Id, out int position)) items[position] = record;
            else
            {
                if (!string.IsNullOrEmpty(record.Id)) positions[record.Id] = items.Count;
                items.Add(record);
            }
        }
        Items = items.OrderBy(Slot).ToArray();
    }

    public static IEnumerable<HudLootBagHistory> Group(IEnumerable<HudLootRecord> records)
    {
        var bags = new Dictionary<string, List<HudLootRecord>>(StringComparer.Ordinal);
        var result = new List<HudLootBagHistory>();
        int index = 0;
        foreach (var record in records ?? Enumerable.Empty<HudLootRecord>())
        {
            if (record == null) continue;
            string key = GroupKey(record);
            if (key == null)
            {
                // Do not infer bag membership from a shared timestamp, bag type,
                // entity ID, item name, or an unrecognized historic ID prefix.
                result.Add(new HudLootBagHistory(record.Id ?? "record:" + index.ToString(CultureInfo.InvariantCulture), new[] { record }));
            }
            else
            {
                if (!bags.TryGetValue(key, out var contents)) bags[key] = contents = new List<HudLootRecord>();
                contents.Add(record);
            }
            index++;
        }
        result.AddRange(bags.Select(pair => new HudLootBagHistory(pair.Key, pair.Value)));
        return result.OrderByDescending(bag => bag.Time.ToUniversalTime()).ThenBy(bag => bag.Id, StringComparer.Ordinal).ToArray();
    }

    private static string GroupKey(HudLootRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.BagId)) return "bag:" + record.BagId;
        if (!LegacySlot(record.Id, out _)) return null;
        // Older journals recorded a GUID per bag followed by its slot (0..7).
        // Recover only that exact format, and require matching original context.
        // The caller supplies one account's history; a GUID never relies on a
        // reusable game entity ID or a bag-type/time heuristic for identity.
        return "legacy-bag:" + record.Id.Substring(0, 32) + ":" + record.Time.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)
            + Part(record.Bag) + Part(record.Enemy) + Part(record.Dungeon) + Part(record.Character);
    }

    private static string Part(string value) => ":" + (value?.Length ?? -1).ToString(CultureInfo.InvariantCulture) + ":" + value;
    private static int Slot(HudLootRecord record) => record.BagSlot >= 0 ? record.BagSlot : LegacySlot(record.Id, out int slot) ? slot : int.MaxValue;
    private static bool LegacySlot(string id, out int slot)
    {
        slot = -1;
        if (id == null || id.Length != 34 || id[32] != ':' || id[33] < '0' || id[33] > '7'
            || !Guid.TryParseExact(id.Substring(0, 32), "N", out _)) return false;
        slot = id[33] - '0';
        return true;
    }
}
