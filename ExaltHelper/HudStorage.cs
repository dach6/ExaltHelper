using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ExaltHelper;

internal static class HudStorage
{
    private static readonly object Gate = new object();
    public static string Root => Environment.GetEnvironmentVariable("EXALTHELPER_HUD_DATA") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExaltHelper", "HUD");
    public static string LastError { get; private set; }

    public static T Load<T>(string name, T fallback)
    {
        lock (Gate)
        {
            try
            {
                string path = Path.Combine(Root, name);
                if (!File.Exists(path)) return fallback;
                return new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 128 }.Deserialize<T>(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                LastError = "Could not load " + Path.GetFileName(name) + ". Your saved file was kept.";
                return fallback;
            }
        }
    }

    public static bool Save<T>(string name, T value)
    {
        lock (Gate)
        {
            try
            {
                string path = Path.Combine(Root, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 128 }.Serialize(value);
                File.WriteAllText(path + ".tmp", json);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak", true);
                else File.Move(path + ".tmp", path);
                LastError = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                LastError = "Could not save " + Path.GetFileName(name) + ". Check that the HUD data folder is writable.";
                return false;
            }
        }
    }
}

internal sealed class HudPreferences
{
    public string Theme { get; set; } = "Midnight";
    public string AccentColor { get; set; } = "#00e5bf";
    public bool CompactControls { get; set; }
    public bool TabTooltips { get; set; } = true;
    public bool Expanded { get; set; }
    public int DesktopX { get; set; } = int.MinValue;
    public int DesktopY { get; set; }
    public int DesktopWidth { get; set; } = 1240;
    public int DesktopHeight { get; set; } = 820;
    public bool Maximized { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool WidgetBar { get; set; } = true;
    public int WidgetRows { get; set; } = 1;
    public string WidgetStorage { get; set; } = "Current";
    public bool CalloutServer { get; set; } = true;
    public bool CalloutRealm { get; set; } = true;
    public bool PartyCallouts { get; set; }
    public HudCalloutSettings Callouts { get; set; } = new HudCalloutSettings();
    public bool SeasonWarnings { get; set; } = true;
    public bool BattlePassWarnings { get; set; } = true;
    public bool CalendarWarnings { get; set; } = true;
    public Dictionary<string, string> TableCategories { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> TablePeriods { get; set; } = new Dictionary<string, string>();
    public List<string> HiddenFeedCategories { get; set; } = new List<string>();
    public List<string> HiddenKeyPopTiers { get; set; } = new List<string>();
    public bool TaskBar { get; set; } = true;
    public bool RecordChat { get; set; } = true;
    public bool RecordLoot { get; set; } = true;
    public HudLootPreferences Loot { get; set; } = new HudLootPreferences();
    public HudLootPreferences FeedLoot { get; set; } = new HudLootPreferences();
    public bool RecordCombat { get; set; } = true;
    public HudCombatPreferences Combat { get; set; } = new HudCombatPreferences();
    public bool CombatByDungeon { get; set; }
    public bool SoundAlerts { get; set; }
    public HudSoundSettings Sounds { get; set; } = new HudSoundSettings();
    public int HistoryLimit { get; set; } = 10000;
    public string Page { get; set; } = "live";
    public List<string> HiddenWidgets { get; set; } = new List<string>();
    public List<string> WidgetOrder { get; set; } = new List<string>();
    public int OverlayX { get; set; } = int.MinValue;
    public int OverlayY { get; set; }
    public int OverlayHeight { get; set; } = 440;
    public bool OverlayLocked { get; set; }
    public static HudPreferences Current { get; } = Load();
    private static HudPreferences Load()
    {
        var value = HudStorage.Load("layout.json", new HudPreferences()) ?? new HudPreferences();
        if (string.Equals(value.AccentColor, "#58dec1", StringComparison.OrdinalIgnoreCase)) value.AccentColor = "#00e5bf";
        value.HiddenWidgets ??= new List<string>(); value.WidgetOrder ??= new List<string>(); value.HiddenFeedCategories ??= new List<string>();
        value.TableCategories ??= new Dictionary<string, string>(); value.TablePeriods ??= new Dictionary<string, string>();
        value.Callouts ??= new HudCalloutSettings(); value.Callouts.Normalize();
        value.HiddenKeyPopTiers = (value.HiddenKeyPopTiers ?? new List<string>()).Where(x => x == "Rookie" || x == "Adept" || x == "Expert" || x == "Exaltation").Distinct().ToList();
        value.Combat ??= new HudCombatPreferences(); value.Combat.Normalize();
        value.Sounds ??= new HudSoundSettings(); value.Sounds.Normalize();
        value.Loot ??= new HudLootPreferences(); value.Loot.AlwaysTrackBags ??= new List<string>();
        value.Loot.MinimumTier = Math.Max(0, Math.Min(15, value.Loot.MinimumTier));
        value.FeedLoot ??= new HudLootPreferences(); value.FeedLoot.AlwaysTrackBags ??= new List<string>();
        value.FeedLoot.MinimumTier = Math.Max(0, Math.Min(15, value.FeedLoot.MinimumTier));
        value.WidgetRows = value.WidgetRows > 1 ? 2 : 1; value.HistoryLimit = Math.Max(100, Math.Min(50000, value.HistoryLimit));
        return value;
    }
    public void Save() => HudStorage.Save("layout.json", this);
}
