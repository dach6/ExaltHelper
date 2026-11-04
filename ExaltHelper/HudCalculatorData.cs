using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ExaltHelper.Proxy.Helpers;

namespace ExaltHelper;

// Formula provenance: RealmHound's assets/object_list.rs and stats/derived.rs.
// Keep the source attributes: each effect has its own scaling and rounding rules.
internal sealed class HudCalculatorItem
{
    public int Id, Slot, NumProjectiles = 1;
    public string Name = "", ObjectName = "";
    public bool Equipment;
    public float RateOfFire = 1;
    public double XpBonus;
    public readonly List<XElement> EquipEffects = new List<XElement>();
    public readonly List<XElement> Procs = new List<XElement>();
    public readonly Dictionary<int, XElement> Projectiles = new Dictionary<int, XElement>();
    public readonly List<HudCalculatorMode> Modes = new List<HudCalculatorMode>();
    public readonly List<XElement> ConditionalEffects = new List<XElement>();
    public bool IsWeapon => new[] { 1, 2, 3, 8, 17, 24 }.Contains(Slot);
    public bool IsArmor => new[] { 6, 7, 14 }.Contains(Slot);
    public bool IsAbility => Slot > 0 && Slot <= 31 && !IsWeapon && !IsArmor && Slot != 9 && Slot != 10 && Slot != 26;
    public override string ToString() => Name;
}

internal sealed class HudCalculatorMode
{
    public string Name = "Primary";
    public double MpCost, Cooldown;
    public readonly List<XElement> Effects = new List<XElement>();
    public readonly List<XElement> HexEffects = new List<XElement>();
    public readonly List<XElement> ConditionalEffects = new List<XElement>();
    public override string ToString() => Name;
}

internal static class HudCalculatorData
{
    private static readonly object Gate = new object();
    private static Dictionary<int, HudCalculatorItem> items;
    private static Dictionary<int, XElement> enchants;
    public static string Status { get; private set; } = "";
    public static IReadOnlyDictionary<int, HudCalculatorItem> Items { get { EnsureLoaded(); return items; } }
    public static HudCalculatorItem Item(int id) => Items.TryGetValue(id, out var item) ? item : null;
    public static HudCalculatorItem Named(string name) => Items.Values.FirstOrDefault(x => x.ObjectName == name || x.Name == name);

    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (items != null) return;
            var result = new Dictionary<int, HudCalculatorItem>();
            var enchantResult = new Dictionary<int, XElement>();
            try
            {
                string directory = Path.GetDirectoryName(typeof(HudCalculatorData).Assembly.Location);
                string objectPath = new[] { Path.Combine(directory, "ExaltHelper_Data", "Objects.xml"), ResourceHelper.ObjectsXmlPath }.FirstOrDefault(File.Exists);
                if (objectPath == null) throw new FileNotFoundException("Objects.xml is unavailable.");
                using (var reader = XmlReader.Create(objectPath, ReaderSettings()))
                    while (reader.Read())
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Object")
                            using (var subtree = reader.ReadSubtree())
                            {
                                var value = ParseItem(XElement.Load(subtree));
                                if (value != null) result[value.Id] = value;
                            }
                string enchantPath = new[] { Path.Combine(directory, "assets", "xml", "enchantments.xml"), Path.Combine("assets", "xml", "enchantments.xml") }.FirstOrDefault(File.Exists);
                if (enchantPath != null)
                    using (var reader = XmlReader.Create(enchantPath, ReaderSettings()))
                        foreach (var e in XDocument.Load(reader).Descendants("Enchantment"))
                            enchantResult[Id((string)e.Attribute("type"))] = e;
                Status = enchantPath == null ? "Enchantment definitions unavailable; imported enchant bonuses may be incomplete." : "";
            }
            catch (Exception ex) when (ex is IOException || ex is XmlException || ex is UnauthorizedAccessException)
            { Status = "Calculator data unavailable: " + ex.Message; }
            items = result; enchants = enchantResult;
        }
    }

    private static XmlReaderSettings ReaderSettings() => new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 100000000 };

    public static HudCalculatorItem ParseItem(XElement e)
    {
        int id = Id((string)e.Attribute("type")); if (id <= 0) return null;
        var item = new HudCalculatorItem { Id = id, Name = (string)e.Element("DisplayId") ?? (string)e.Attribute("id") ?? id.ToString(),
            ObjectName = (string)e.Attribute("id") ?? "", Slot = (int)Value(e.Element("SlotType")), Equipment = e.Element("Item") != null,
            NumProjectiles = Math.Max(1, (int)Value(e.Element("NumProjectiles"), 1)), RateOfFire = (float)Value(e.Element("RateOfFire"), 1), XpBonus = Value(e.Element("XPBonus")) };
        foreach (var p in e.Elements("Projectile")) item.Projectiles[(int)Attr(p, "id")] = p;
        item.EquipEffects.AddRange(e.Elements("ActivateOnEquip"));
        item.Procs.AddRange(e.Elements().Where(x => x.Name.LocalName == "OnPlayerShootActivate" || x.Name.LocalName == "OnEnemyHitActivate"));
        item.ConditionalEffects.AddRange(e.Elements().Where(x => x.Name.LocalName.StartsWith("On", StringComparison.Ordinal) && !item.Procs.Contains(x) && x.Name.LocalName != "OnDetonateHexActivate"));
        var modes = e.Elements("Ability").ToList();
        if (e.Elements("Activate").Any() || modes.Count == 0) item.Modes.Add(ParseMode(e, "Primary", e));
        for (int i = 0; i < modes.Count; i++) item.Modes.Add(ParseMode(modes[i], (string)modes[i].Attribute("name") ?? "Mode " + (i + 1), e));
        return item;
    }

    private static HudCalculatorMode ParseMode(XElement e, string name, XElement item)
    {
        var mode = new HudCalculatorMode { Name = name, MpCost = Value(e.Element("MpCost"), Value(item.Element("MpCost"))),
            Cooldown = Value(e.Element("Cooldown"), Value(item.Element("Cooldown"))) };
        mode.Effects.AddRange(e.Elements("Activate"));
        mode.HexEffects.AddRange(e.Elements("OnDetonateHexActivate"));
        mode.ConditionalEffects.AddRange(e.Elements().Where(x => x.Name.LocalName.StartsWith("On", StringComparison.Ordinal) && x.Name.LocalName != "OnDetonateHexActivate"));
        // Start/end-use effects belong to one selected mode; they describe a single completed use.
        mode.Effects.AddRange(e.Elements("StartUse").Elements("Activate"));
        mode.Effects.AddRange(e.Elements("EndUse").Elements("Activate"));
        return mode;
    }

    public static IEnumerable<XElement> Enchants(string code)
    {
        EnsureLoaded();
        foreach (int id in EnchantIds(code)) if (enchants.TryGetValue(id, out var e)) yield return e;
    }
    public static IEnumerable<int> EnchantIds(string code)
    {
        if (string.IsNullOrEmpty(code)) yield break;
        byte[] bytes = EnchantmentParser.SixBitStringToBytes(code);
        if (bytes.Length < 3 || (bytes[1] | (bytes[2] << 8)) != 1026) yield break;
        for (int i = 3; i + 1 < bytes.Length && i < 11; i += 2)
        {
            int id = (short)(bytes[i] | (bytes[i + 1] << 8));
            if (id == 253 || id == -3) yield break;
            if (id > 0) yield return id;
        }
    }
    public static bool HasUnknownEnchants(string code) { EnsureLoaded(); return EnchantIds(code).Any(id => !enchants.ContainsKey(id)); }
    public static double Attr(XElement e, string name, double fallback = 0) => Number((string)e?.Attribute(name), fallback);
    public static double Value(XElement e, double fallback = 0) => Number(e?.Value, fallback);
    public static double Number(string value, double fallback = 0) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && !double.IsNaN(n) && !double.IsInfinity(n) ? n : fallback;
    public static int Id(string value) => value != null && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? int.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex) ? hex : 0
        : int.TryParse(value, out int dec) ? dec : 0;
    public static int StatIndex(string name)
    {
        switch ((name ?? "").ToUpperInvariant())
        {
            case "HP": case "MAXHP": case "0": return 0;
            case "MP": case "MAXMP": case "3": return 1;
            case "ATT": case "ATK": case "20": return 2;
            case "DEF": case "21": return 3;
            case "SPD": case "22": return 4;
            case "DEX": case "28": return 5;
            case "VIT": case "26": return 6;
            case "WIS": case "27": return 7;
            default: return -1;
        }
    }
}
