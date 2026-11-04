using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ExaltHelper;

// RealmEye drop-location data distributed by RealmHound (MIT); see assets/dashboard/RealmHound-LICENSE.txt.
internal static class HudCatalog
{
    private static readonly Lazy<Dictionary<string, List<int>>> catalog = new Lazy<Dictionary<string, List<int>>>(Load);
    public static IReadOnlyDictionary<string, List<int>> Dungeons => catalog.Value;
    public static string Locations(int itemId) => string.Join(", ", Dungeons.Where(x => x.Value.Contains(itemId)).Select(x => x.Key));
    private static Dictionary<string, List<int>> Load()
    {
        var result = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "dashboard", "realmeye_drops.json");
        if (!File.Exists(path)) return result;
        try
        {
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            foreach (string category in new[] { "dungeons", "biomes" })
            {
                if (!root.TryGetValue(category, out var section)) continue;
                foreach (IList location in (IEnumerable)section)
                {
                    var ids = new HashSet<int>();
                    foreach (IList source in (IEnumerable)location[2]) foreach (object item in (IEnumerable)source[1]) ids.Add(Convert.ToInt32(item));
                    result[(string)location[0]] = ids.OrderBy(x => x).ToList();
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidCastException) { }
        return result;
    }
}
