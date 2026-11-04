using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace ExaltHelper;

internal sealed class HudModifierDefinition
{
    public string Id, Name, Description, Labels;
    public int Xp, Loot, Dust;
}
internal sealed class HudModifierCatalog
{
    internal readonly Dictionary<string, HudModifierDefinition> Definitions = new Dictionary<string, HudModifierDefinition>();
    public int Count => Definitions.Count;
    public HudModifierDefinition Get(string token) => Definitions.TryGetValue(HudCallouts.Canonical(token), out var definition) ? definition : null;
}

// The game asset is the numeric authority. Packet tokens alone never imply a
// reward percentage; an unknown modifier is reported explicitly to the caller.
internal static class HudCalloutModifiers
{
    private static readonly object LoadGate = new object();
    private static HudModifierCatalog current = new HudModifierCatalog();
    private static Task loading;
    public static string Error { get; private set; } = "";
    public static HudModifierCatalog Current => Volatile.Read(ref current);
    public static Task LoadDefaultAsync()
    {
        lock (LoadGate) return loading ??= Task.Run(() => {
            string directory = Path.GetDirectoryName(typeof(HudCalloutModifiers).Assembly.Location);
            string path = Path.Combine(directory, "assets", "xml", "mods.xml");
            try { Volatile.Write(ref current, ParseXml(File.ReadAllText(path))); Error = ""; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException || ex is InvalidDataException)
            { Error = "Dungeon reward bonuses are unavailable: modifier data could not be loaded."; }
        });
    }
    public static HudModifierCatalog ParseXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) throw new InvalidDataException("Modifier data is empty.");
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        var document = XDocument.Load(reader);
        var result = new HudModifierCatalog();
        foreach (var element in document.Descendants("DungeonModifier"))
        {
            string id = (string)element.Attribute("id");
            if (HudCallouts.Canonical(id).Length == 0) continue;
            var definition = new HudModifierDefinition { Id = id, Name = (string)element.Element("DisplayId") ?? id,
                Description = (string)element.Element("Description") ?? "", Labels = (string)element.Element("Labels") ?? "" };
            foreach (var mutator in element.Element("Mutators")?.Descendants() ?? Enumerable.Empty<XElement>())
            {
                // Item-specific drops must not inflate the headline reward.
                if (mutator.Attribute("itemId") != null || mutator.Attribute("excludeLabels") != null) continue;
                if (!double.TryParse((string)mutator.Attribute("amount"), NumberStyles.Float, CultureInfo.InvariantCulture, out double amount) || double.IsNaN(amount) || double.IsInfinity(amount)) continue;
                double percent = Math.Round((amount - 1) * 100, MidpointRounding.AwayFromZero);
                if (percent < int.MinValue || percent > int.MaxValue) continue;
                int bonus = (int)percent;
                switch (mutator.Name.LocalName)
                {
                    case "EntityStatMultiplier":
                        if (string.Equals((string)mutator.Attribute("stat"), "XP", StringComparison.OrdinalIgnoreCase)) definition.Xp = Add(definition.Xp, bonus);
                        break;
                    case "EntityDropMultiplier": definition.Loot = Add(definition.Loot, bonus); break;
                    case "EntityDustMultiplier": definition.Dust = Add(definition.Dust, bonus); break;
                }
            }
            result.Definitions[HudCallouts.Canonical(id)] = definition;
        }
        if (result.Count == 0) throw new InvalidDataException("No dungeon modifiers were found.");
        return result;
    }
    internal static int Add(int a, int b) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)a + b));
}
