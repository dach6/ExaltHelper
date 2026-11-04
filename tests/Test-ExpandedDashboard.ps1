param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotDirectory = "$PSScriptRoot/../artifacts/expanded-dashboard"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ScreenshotDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'data'
$assembly = (Resolve-Path $AssemblyPath).Path
$harness = Join-Path $testRoot 'DashboardTest.dll'
Add-Type -OutputAssembly $harness -ReferencedAssemblies System.Core, System.Drawing, System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed class ExpandedDashboardRegression : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window, int index);
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        return f == null ? o.GetType().GetProperty(name, F).GetValue(o, null) : f.GetValue(o);
    }
    static void Set(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) f.SetValue(o, value); else o.GetType().GetProperty(name, F).SetValue(o, value, null);
    }
    static object Call(object o, string method, params object[] args) { return o.GetType().GetMethod(method, F).Invoke(o, args); }
    static object Static(string type, string method, params object[] args) { object result = T(type).GetMethod(method, F).Invoke(null, args); if (type == "HudJournal" && method == "UseAccount") T(type).GetMethod("WaitForLoads", F).Invoke(null, null); return result; }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static double Channel(byte value) { double n = value / 255.0; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
    static double Luminance(Color value) { return .2126 * Channel(value.R) + .7152 * Channel(value.G) + .0722 * Channel(value.B); }
    static double Contrast(Color a, Color b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
    static void TableTheme(Control page, bool light)
    {
        var grid = Children(page).OfType<DataGridView>().First();
        var styles = grid.Rows.Cast<DataGridViewRow>().Take(2).Select(r => r.Cells[0].InheritedStyle).ToArray();
        Check(styles.Length == 2, "Theme contrast fixture needs populated even and odd rows");
        Check(styles.All(s => Contrast(s.ForeColor, s.BackColor) >= 4.5 && Contrast(s.SelectionForeColor, s.SelectionBackColor) >= 4.5),
            "Theme switch made normal or selected table text unreadable");
        Check(styles.All(s => light ? s.BackColor.GetBrightness() > .75f : s.BackColor.GetBrightness() < .4f),
            "Existing even or odd rows retained the preceding theme surface");
    }
    static IEnumerable<Control> Children(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
    static void Layout(Control control) { control.PerformLayout(); foreach (var child in Children(control)) child.PerformLayout(); Application.DoEvents(); }
    static Rectangle Restore(Rectangle saved, params Rectangle[] screens)
    { return (Rectangle)Static("ExaltDashboardForm", "RestoreBoundsToScreens", saved, screens); }
    static void Monitors()
    {
        var primary = new Rectangle(0, 0, 1920, 1040);
        var left = new Rectangle(-1600, -200, 1600, 1000);
        var saved = new Rectangle(-1400, -100, 1200, 800);
        Check(Restore(saved, primary, left) == saved, "Valid negative-coordinate monitor placement changed");
        Check(primary.Contains(Restore(saved, primary)), "Removed monitor left the dashboard offscreen");
        Check(left.Contains(Restore(new Rectangle(-1550, -150, 4000, 2500), left)), "Oversized saved bounds were not clamped to a monitor");
        Check(primary.Contains(Restore(new Rectangle(1800, 950, 1100, 720), primary)), "Partly offscreen window left inaccessible edges");
        Check(primary.Contains(Restore(new Rectangle(100, 100, -20, -10), primary)), "Invalid saved size was not repaired");
        Check(left.Contains(Restore(new Rectangle(-1300, -150, 1000, 800), primary, left)), "Window moved from the monitor it overlaps most");
        var small = new Rectangle(0, 0, 800, 560);
        Check(small.Contains(Restore(new Rectangle(0, 0, 1200, 800), small)), "Small monitor recovery still exceeds the work area");
        var fallback = Restore(Rectangle.Empty);
        Check(fallback.Width > 0 && fallback.Height > 0, "No-monitor fallback produced unusable bounds");
    }
    static void CompactBounds()
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        int savedX = workArea.Right - 396, savedY = workArea.Top + 16;
        Set(preferences, "OverlayX", savedX); Set(preferences, "OverlayY", savedY); Set(preferences, "OverlayHeight", 440);
        using (var overlay = (Form)New("DpsOverlayForm"))
        {
            ((System.Windows.Forms.Timer)Get(overlay, "updateTimer")).Stop();
            Check(overlay.Left == savedX && overlay.Top == savedY && overlay.Width == 380,
                "Restoring the compact overlay applied desktop minimum width and shifted its saved position");
        }
    }
    static object Snapshot()
    {
        object snapshot = New("Proxy.DataStructures.DpsSnapshot");
        Set(snapshot, "DungeonName", "The Nest"); Set(snapshot, "DungeonStartTime", DateTime.Now.AddMinutes(-4));
        Set(snapshot, "ElapsedDungeonSeconds", 240.0); Set(snapshot, "TotalDungeonDamage", 654321L); Set(snapshot, "KillsCount", 7);
        object local = Get(snapshot, "LocalPlayer"); Set(local, "Name", "FixtureHero"); Set(local, "ClassName", "Wizard");
        Set(local, "CurrentHp", 650); Set(local, "MaxHp", 770); Set(local, "CurrentMp", 180); Set(local, "MaxMp", 385);
        Set(local, "Attack", 75); Set(local, "Dexterity", 60); Set(local, "Vitality", 40); Set(local, "Wisdom", 50);
        object enemy = New("Proxy.DataStructures.EnemyCombatSnapshot"); Set(enemy, "EntityId", 500); Set(enemy, "Name", "Killer Bee Queen");
        Set(enemy, "TotalDamage", 654321L); Set(enemy, "FightDurationSeconds", 83.0); Set(enemy, "IsBoss", true);
        for (int i = 0; i < 25; i++)
        {
            object damage = New("Proxy.DataStructures.PlayerDamageEntry"); Set(damage, "Name", i == 0 ? "FixtureHero" : "Fixture " + i);
            Set(damage, "ClassName", "Wizard"); Set(damage, "Damage", 80000L - i * 1000); Set(damage, "Dps", 1000.0 - i * 10);
            Set(damage, "IsLocalPlayer", i == 0); ((IList)Get(enemy, "Damagers")).Add(damage);
            object player = New("Proxy.DataStructures.PlayerParseEntry"); Set(player, "Name", "Fixture " + i); Set(player, "ClassName", "Wizard");
            ((IList)Get(snapshot, "Players")).Add(player);
            object party = New("Proxy.DataStructures.PartyMemberEntry"); Set(party, "Name", "Fixture " + i); Set(party, "IsInDungeon", i % 2 == 0);
            ((IList)Get(snapshot, "PartyMembers")).Add(party);
        }
        Set(snapshot, "ActiveEnemy", enemy); return snapshot;
    }
    static void Render(Form form, string path)
    {
        Layout(form);
        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, ImageFormat.Png); }
    }
    static void Warnings()
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        object account = New("AccountSnapshot"); DateTime now = new DateTime(2026, 11, 30, 12, 0, 0, DateTimeKind.Utc);
        Set(account, "SeasonEndUtc", now.AddDays(2)); Set(account, "BattlePassEndUtc", now.AddHours(1));
        Set(preferences, "SeasonWarnings", true); Set(preferences, "BattlePassWarnings", true); Set(preferences, "CalendarWarnings", true);
        Func<int> count = () => ((IEnumerable)Static("ExaltDashboardForm", "CycleWarnings", now, account)).Cast<object>().Count();
        Check(count() == 3, "Approaching season/battlepass and last-day calendar reminders were omitted");
        Set(preferences, "SeasonWarnings", false); Check(count() == 2, "Season reminder toggle changed other reminder categories");
        Set(preferences, "BattlePassWarnings", false); Check(count() == 1, "Battlepass reminder toggle changed calendar reminders");
        Set(preferences, "CalendarWarnings", false); Check(count() == 0, "Disabled reminder categories remained visible");
        Set(preferences, "SeasonWarnings", true); Set(preferences, "BattlePassWarnings", true); Set(preferences, "CalendarWarnings", true);
        Set(account, "SeasonEndUtc", now.AddDays(4)); Set(account, "BattlePassEndUtc", now.AddHours(-1));
        Check(((IEnumerable)Static("ExaltDashboardForm", "CycleWarnings", now.AddDays(-1), account)).Cast<object>().Count() == 1,
            "Calendar or expiration warnings ignored UTC dates and countdown bounds");
    }
    static void Callouts(Form form)
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        IDataObject previousClipboard = Clipboard.GetDataObject();
        var previous = new Dictionary<string, object>();
        foreach (string name in new[] { "Map", "Realm", "Server" }) previous[name] = T("HudJournal").GetProperty(name, F).GetValue(null, null);
        try
        {
            T("HudJournal").GetProperty("Map", F).SetValue(null, "The Nest", null);
            T("HudJournal").GetProperty("Realm", F).SetValue(null, "Cyclops", null);
            T("HudJournal").GetProperty("Server", F).SetValue(null, "USWest", null);
            Static("HudJournal", "AppendFeed", "Game", "Fixture realm event");
            Call(form, "SelectPage", "live"); Call(form, "RefreshPageData"); Layout(form);
            var page = (Control)((IDictionary)Get(form, "pages"))["live"];
            var grid = Children(page).OfType<DataGridView>().Single(); grid.CurrentCell = grid.Rows[0].Cells[0];
            var copy = Children(page).OfType<Button>().Single(b => b.Text == "Copy callout");
            Set(preferences, "PartyCallouts", true); Set(preferences, "CalloutServer", true); Set(preferences, "CalloutRealm", true);
            copy.PerformClick();
            Check(Clipboard.GetText() == "/p USWest \u00b7 Cyclops \u00b7 The Nest \u00b7 Fixture realm event", "Event callout omitted selected event, location, or party prefix");
            T("HudJournal").GetProperty("Map", F).SetValue(null, "Nexus", null);
            T("HudJournal").GetProperty("Realm", F).SetValue(null, "Gorgon", null);
            T("HudJournal").GetProperty("Server", F).SetValue(null, "USEast", null);
            copy.PerformClick();
            Check(Clipboard.GetText() == "/p USWest \u00b7 Cyclops \u00b7 The Nest \u00b7 Fixture realm event", "A historical event callout used the new server, realm, or dungeon");
            Set(preferences, "PartyCallouts", false); Set(preferences, "CalloutServer", false); Set(preferences, "CalloutRealm", false);
            copy.PerformClick();
            Check(Clipboard.GetText() == "The Nest \u00b7 Fixture realm event", "Disabled callout options still entered the clipboard");
            grid.CurrentCell = null; copy.PerformClick();
            Check(Clipboard.GetText() == "Nexus", "Dungeon callout without a selected event copied unrelated text");
            object legacy = New("HudLogEntry"); Set(legacy, "Location", "Legacy Castle"); Set(legacy, "Text", "Legacy event");
            grid.CurrentCell = grid.Rows[0].Cells[0]; grid.CurrentRow.Tag = legacy;
            Set(preferences, "CalloutServer", true); Set(preferences, "CalloutRealm", true); copy.PerformClick();
            Check(Clipboard.GetText() == "Legacy Castle \u00b7 Legacy event", "Old feed records invented current server/realm metadata");
        }
        finally
        {
            foreach (var entry in previous) T("HudJournal").GetProperty(entry.Key, F).SetValue(null, entry.Value, null);
            Set(preferences, "PartyCallouts", false); Set(preferences, "CalloutServer", true); Set(preferences, "CalloutRealm", true);
            if (previousClipboard != null) Clipboard.SetDataObject(previousClipboard, true); else Clipboard.Clear();
        }
    }
    static TControl Named<TControl>(Control parent, string name) where TControl : Control
    { return Children(parent).OfType<TControl>().Single(x => x.Name == name); }
    static object ReloadPreferences(object preferences)
    { return T("HudStorage").GetMethod("Load", F).MakeGenericMethod(preferences.GetType()).Invoke(null, new object[] { "layout.json", New("HudPreferences") }); }
    static void RichCallouts(Form form, string images)
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        Call(preferences, "Save"); object original = Get(ReloadPreferences(preferences), "Callouts");
        IDataObject priorClipboard = Clipboard.GetDataObject();
        var timer = (System.Windows.Forms.Timer)Get(form, "refreshTimer"); bool ticking = timer.Enabled; timer.Stop();
        int priorWidgetRows = (int)Get(preferences, "WidgetRows");
        var warning = (Label)Get(form, "cycleWarnings"); string priorWarning = warning.Text; bool priorWarningVisible = warning.Visible; int priorWarningHeight = warning.Height;
        var options = new Dictionary<string, object>();
        foreach (string name in new[] { "PartyCallouts", "CalloutServer", "CalloutRealm" }) options[name] = Get(preferences, name);
        try
        {
            ((Task)Static("HudCalloutModifiers", "LoadDefaultAsync")).GetAwaiter().GetResult();
            Call(form, "SelectPage", "settings");
            var settingsPage = (FlowLayoutPanel)((IDictionary)Get(form, "pages"))["settings"];
            var style = Named<ComboBox>(settingsPage, "callout_dungeon_name"); style.SelectedItem = "Full";
            Named<ComboBox>(settingsPage, "callout_dungeon_join").SelectedItem = "Beginning";
            Named<ComboBox>(settingsPage, "callout_realm_status").SelectedItem = "Score";
            Named<CheckBox>(settingsPage, "callout_percent").Checked = true;
            var full = Named<ComboBox>(settingsPage, "callout_dungeon_override_name"); full.SelectedItem = "The Nest";
            Named<TextBox>(settingsPage, "callout_dungeon_replacement").Text = "bee house";
            Children(full.Parent).OfType<Button>().Single(b => b.Text == "Save").PerformClick();
            var tags = Named<DataGridView>(settingsPage, "callout_reward_tags");
            var generous = tags.Rows.Cast<DataGridViewRow>().Single(r => (string)Get(r.Tag, "Id") == "GENEROUS");
            generous.Cells["Tag"].Value = "chest";
            var quick = Named<DataGridView>(settingsPage, "callout_quick_texts");
            foreach (DataGridViewRow row in quick.Rows) { row.Cells[0].Value = ""; row.Cells[1].Value = ""; }
            quick.Rows[0].Cells[0].Value = "Rally"; quick.Rows[0].Cells[1].Value = "/p regroup";
            Children(settingsPage).OfType<Button>().Single(b => b.Text == "Save quick text buttons").PerformClick();
            object saved = Get(ReloadPreferences(preferences), "Callouts");
            Check((string)Get(saved, "DungeonNameStyle") == "Full" && (string)Get(saved, "DungeonJoin") == "Beginning"
                && (string)Get(saved, "RealmStatus") == "Score" && (bool)Get(saved, "Percent"), "Callout format controls did not persist their selected values");
            Check((string)((IDictionary)Get(saved, "DungeonNameOverrides"))["The Nest"] == "bee house"
                && ((IList)Get(saved, "RewardTags")).Cast<object>().Any(x => (string)Get(x, "Id") == "GENEROUS" && (string)Get(x, "Short") == "chest"),
                "Custom dungeon name or edited modifier tag did not persist");
            Check(((IList)Get(saved, "QuickTexts")).Count == 1 && (string)Get(((IList)Get(saved, "QuickTexts"))[0], "Text") == "/p regroup",
                "Configured quick-text button did not survive serialization");
            settingsPage.ScrollControlIntoView(style); Render(form, Path.Combine(images, "callout-settings.png"));
            Call(form, "SelectPage", "live"); Call(form, "RefreshPageData");
            var live = (Control)((IDictionary)Get(form, "pages"))["live"];
            var grid = Children(live).OfType<DataGridView>().Single(); grid.CurrentCell = grid.Rows[0].Cells[0];
            object entry = New("HudLogEntry");
            Set(entry, "CalloutKind", "Dungeon"); Set(entry, "CalloutName", "The Nest"); Set(entry, "Location", "The Nest");
            Set(entry, "Server", "USWest"); Set(entry, "Realm", "Cyclops"); Set(entry, "DungeonModifiers", "LOOTING;GENEROUS|S");
            Set(entry, "OriginRealmPercent", 50); Set(entry, "OpenedInNexus", false); Set(entry, "AvailableUntilUtc", DateTime.UtcNow.AddMinutes(5));
            grid.CurrentRow.Tag = entry;
            foreach (string name in options.Keys) Set(preferences, name, true);
            var copy = Children(live).OfType<Button>().Single(b => b.Text == "Copy callout"); copy.PerformClick();
            string expected = "/p j USW Cyclops the nest 50% lb chest in 50% realm";
            Check(Clipboard.GetText() == expected, "Dungeon Copy callout did not apply the saved format and real modifier catalog: " + Clipboard.GetText());
            Check(((Label)Get(form, "calloutPreview")).Text == "Callout: " + expected, "Callout preview disagreed with copied dungeon text");
            var rally = Named<Button>(live, "callout_quick_0"); Check(rally.Text == "Rally", "Saved quick text did not become a labeled live button");
            rally.PerformClick(); Check(Clipboard.GetText() == "/p regroup", "Live quick-text button changed its configured text");
            Layout(form);
            foreach (var toolbar in Children(live).OfType<FlowLayoutPanel>().Where(p => p.Dock == DockStyle.Top))
                Check(toolbar.Controls.Cast<Control>().Where(c => c.Visible).All(c => c.Top >= 0 && c.Bottom <= toolbar.ClientRectangle.Bottom),
                    "Live toolbar clipped callout or configured quick-text buttons at minimum size");
            Render(form, Path.Combine(images, "callout-live-buttons.png"));
            Check(grid.Rows.Cast<DataGridViewRow>().Any(r => grid.GetRowDisplayRectangle(r.Index, true).Height >= r.Height),
                "Minimum-size live feed has no fully visible data row beneath its callout controls");
            Call(form, "SelectPage", "settings"); style.SelectedItem = "Short"; Call(form, "SelectPage", "live");
            grid.CurrentCell = grid.Rows[0].Cells[0]; grid.CurrentRow.Tag = entry; copy.PerformClick();
            Check(Clipboard.GetText() == "/p j USW Cyclops bee house 50% lb chest in 50% realm", "Short-name mode ignored the saved dungeon override");
            Set(entry, "DungeonModifiers", "LOOTING;UNRECOGNIZED_FIXTURE"); Call(form, "RefreshCalloutPreview"); copy.PerformClick();
            Check(((Label)Get(form, "calloutPreview")).Text.Contains("bonuses unknown") && !Clipboard.GetText().Contains("50% lb"),
                "Unknown modifier preview or copy fabricated a complete bonus from partial definitions");
            Clipboard.SetText("keep expired selection"); Set(entry, "AvailableUntilUtc", DateTime.UtcNow.AddSeconds(-1)); copy.PerformClick();
            Check(Clipboard.GetText() == "keep expired selection" && ((Label)Get(form, "calloutPreview")).Text.StartsWith("Callout unavailable:"),
                "Expired dungeon selection replaced the clipboard or remained callable");
            Call(form, "SelectPage", "settings");
            for (int i = 0; i < 8; i++) { quick.Rows[i].Cells[0].Value = "Long quick text button number " + i; quick.Rows[i].Cells[1].Value = "/p regroup " + i; }
            Children(settingsPage).OfType<Button>().Single(b => b.Text == "Save quick text buttons").PerformClick();
            Set(preferences, "WidgetRows", 2); Call(form, "SelectPage", "live"); Call(form, "RefreshDashboard");
            warning.Text = "Season ends soon\nBattlepass ends soon\nCalendar reward reminder"; warning.Height = 79; warning.Visible = true;
            Render(form, Path.Combine(images, "callout-live-maximum-controls.png"));
            var widgetBar = (FlowLayoutPanel)Get(form, "widgetBar");
            Check(((IEnumerable)Get(form, "quickTextButtons")).Cast<Button>().Count() == 8 && warning.Visible
                && widgetBar.Controls.Cast<Control>().Where(c => c.Visible).Select(c => c.Top).Distinct().Count() == 2
                && grid.Rows.Cast<DataGridViewRow>().Any(r => grid.GetRowDisplayRectangle(r.Index, true).Height >= r.Height),
                "Minimum-size live feed lost readable rows with all eight quick texts, two widget rows and cycle warnings");
        }
        finally
        {
            Call(form, "ClearAutomaticCallout"); Set(preferences, "Callouts", original);
            Set(preferences, "WidgetRows", priorWidgetRows); warning.Text = priorWarning; warning.Height = priorWarningHeight; warning.Visible = priorWarningVisible;
            foreach (var option in options) Set(preferences, option.Key, option.Value);
            Call(preferences, "Save"); Call(form, "SelectPage", "party");
            if (ticking) timer.Start();
            if (priorClipboard != null) Clipboard.SetDataObject(priorClipboard, true); else Clipboard.Clear();
        }
    }
    static void ObserveCalloutMap(string name, uint seed)
    {
        object map = New("Proxy.Networking.Packets.MapInfoPacket"); Set(map, "MapName", name); Set(map, "DisplayName", name);
        Set(map, "Fp", seed); Set(map, "DungeonModifiers", ""); Static("HudJournal", "ObservePacket", null, map, false);
    }
    static void AutomaticCallouts(Form form)
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null), original = Get(preferences, "Callouts");
        object settings = New("HudCalloutSettings"); Set(settings, "DungeonNameStyle", "Full"); Set(settings, "DungeonJoin", "None"); Set(preferences, "Callouts", settings);
        var options = new Dictionary<string, object>();
        foreach (string name in new[] { "PartyCallouts", "CalloutServer", "CalloutRealm" }) { options[name] = Get(preferences, name); Set(preferences, name, false); }
        var state = T("AccountWorkspace").GetField("state", F); object oldAccount = state.GetValue(null), account = New("AccountSnapshot");
        string priorJournal = (string)T("HudJournal").GetField("currentAccountKey", F).GetValue(null);
        IDataObject clipboard = Clipboard.GetDataObject();
        var timer = (System.Windows.Forms.Timer)Get(form, "refreshTimer"); bool ticking = timer.Enabled; timer.Stop();
        try
        {
            Call(form, "ClearAutomaticCallout"); Set(form, "autoCalloutEnabled", false);
            Set(account, "AccountKey", "AutomaticCalloutA"); state.SetValue(null, account); Static("HudJournal", "UseAccount", "AutomaticCalloutA");
            ObserveCalloutMap("The Nest", 100); Clipboard.SetText("keep existing observation"); Thread.Sleep(20);
            Set(settings, "AutoCopyDungeons", true); Call(form, "RefreshAutomaticCallout"); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "keep existing observation", "Enabling automatic callouts replayed a previously observed dungeon");
            ObserveCalloutMap("The Nest", 101); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "the nest", "New current dungeon did not automatically copy its formatted callout");
            Check((DateTime)Get(form, "autoCalloutExpiresUtc") <= (DateTime)Get(Static("HudJournal", "CurrentLocation"), "AvailableUntilUtc"),
                "Automatic clipboard lifetime extended the known join deadline");
            Clipboard.SetText("user replacement"); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "user replacement", "Repeated refresh overwrote user text with an already copied callout");
            ObserveCalloutMap("Snake Pit", 102); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "snake pit", "A distinct dungeon did not receive its own automatic callout");
            Set(form, "autoCalloutExpiresUtc", DateTime.UtcNow.AddSeconds(-1)); Call(form, "RefreshAutomaticCallout");
            Check(!Clipboard.ContainsText(), "Expired automatic callout was not cleared while still owned");
            ObserveCalloutMap("The Nest", 103); Call(form, "RefreshAutomaticCallout"); Clipboard.SetText("user text after expiry");
            Set(form, "autoCalloutExpiresUtc", DateTime.UtcNow.AddSeconds(-1)); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "user text after expiry", "Callout expiration cleared text subsequently copied by the user");
            ObserveCalloutMap("Snake Pit", 104); Call(form, "RefreshAutomaticCallout"); ObserveCalloutMap("Nexus", 105); Call(form, "RefreshAutomaticCallout");
            Check(!Clipboard.ContainsText(), "Leaving the dungeon retained its owned automatic callout");
            ObserveCalloutMap("The Nest", 106); Call(form, "RefreshAutomaticCallout"); Thread.Sleep(20);
            Set(account, "AccountKey", "AutomaticCalloutB"); Call(form, "RefreshAutomaticCallout"); Call(form, "RefreshAutomaticCallout");
            Check(!Clipboard.ContainsText(), "Workspace account transition recopied an old journal dungeon before journal handoff");
            Static("HudJournal", "UseAccount", "AutomaticCalloutB"); ObserveCalloutMap("Snake Pit", 107); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "snake pit", "New account could not automatically copy its own newly observed dungeon");
            Set(settings, "AutoCopyDungeons", false); Call(form, "RefreshAutomaticCallout");
            Check(!Clipboard.ContainsText(), "Disabling automatic callouts retained an owned clipboard entry");
            Set(settings, "AutoCopyDungeons", true); Call(form, "RefreshAutomaticCallout"); ObserveCalloutMap("The Nest", 108); Call(form, "RefreshAutomaticCallout");
            Call(form, "CopyText", "the nest"); Set(form, "autoCalloutExpiresUtc", DateTime.UtcNow.AddSeconds(-1)); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "the nest", "Manual copy of identical text remained owned by automatic clipboard cleanup");
            ObserveCalloutMap("Snake Pit", 109); Call(form, "RefreshAutomaticCallout"); Clipboard.SetText("user text while disabling");
            Set(settings, "AutoCopyDungeons", false); Call(form, "RefreshAutomaticCallout");
            Check(Clipboard.GetText() == "user text while disabling", "Disabling automatic callouts cleared unrelated clipboard text");
        }
        finally
        {
            Call(form, "ClearAutomaticCallout"); Set(form, "autoCalloutEnabled", false); state.SetValue(null, oldAccount);
            Static("HudJournal", "UseAccount", priorJournal); Set(preferences, "Callouts", original);
            foreach (var option in options) Set(preferences, option.Key, option.Value);
            if (ticking) timer.Start();
            if (clipboard != null) Clipboard.SetDataObject(clipboard, true); else Clipboard.Clear();
        }
    }
    static void HistoryTimeZones(Form form)
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        bool grouped = (bool)Get(preferences, "CombatByDungeon"); Set(preferences, "CombatByDungeon", false);
        string priorJournal = (string)T("HudJournal").GetField("currentAccountKey", F).GetValue(null);
        var timer = (System.Windows.Forms.Timer)Get(form, "refreshTimer"); bool ticking = timer.Enabled; timer.Stop();
        try
        {
            Static("HudJournal", "UseAccount", "HistoryTimeZoneFixture");
            DateTime priorDay = DateTime.Today.AddMinutes(-30), today = DateTime.Today.AddMinutes(30);
            DateTime[] times = { priorDay, priorDay.ToUniversalTime(), today, today.ToUniversalTime() };
            object data = New("HudJournalData");
            for (int i = 0; i < times.Length; i++)
            {
                object log = New("HudLogEntry"); Set(log, "Time", times[i]); Set(log, "Category", "Game"); Set(log, "Text", "Timezone fixture " + i);
                Set(log, "Location", "The Nest"); ((IList)Get(data, "Feed")).Add(log); ((IList)Get(data, "Chat")).Add(log);
                object loot = New("HudLootRecord"); Set(loot, "Id", "timezone-loot-" + i); Set(loot, "Time", times[i]);
                Set(loot, "Item", "Timezone fixture"); Set(loot, "Bag", "White Bag"); ((IList)Get(data, "Loot")).Add(loot);
                object fight = New("HudFightRecord"); Set(fight, "Id", "timezone-fight-" + i); Set(fight, "Time", times[i]);
                Set(fight, "Enemy", "Timezone fixture"); Set(fight, "Dungeon", "The Nest"); Set(fight, "Seconds", 1.0);
                Set(fight, "Equipment", new int[4]); ((IList)Get(data, "Fights")).Add(fight);
            }
            object gate = T("HudJournal").GetField("Gate", F).GetValue(null);
            lock (gate) T("HudJournal").GetField("data", F).SetValue(null, data);
            foreach (string key in new[] { "live", "chat", "loot", "combat" })
            {
                Call(form, "SelectPage", key); object table = ((IDictionary)Get(form, "tables"))[key];
                var grid = (DataGridView)Get(table, "Grid"); var period = (ComboBox)Get(table, "Period");
                var category = (ComboBox)Get(table, "Category"); var search = (TextBox)Get(table, "SearchBox");
                period.SelectedIndex = 0; category.SelectedIndex = 0; search.Clear(); Call(form, "RefreshPageData");
                string formatted = today.ToString(key == "live" ? "HH:mm:ss" : "g");
                Check(grid.RowCount == 4 && grid.Rows.Cast<DataGridViewRow>().Take(2).All(r => ((DateTime)Get(r.Tag, "Time")).ToUniversalTime() == today.ToUniversalTime()
                    && Convert.ToString(r.Cells[0].Value) == formatted), key + " displayed or ordered equivalent UTC/local history timestamps differently");
                period.SelectedIndex = 1;
                Check(grid.RowCount == 2 && grid.Rows.Cast<DataGridViewRow>().All(r => ((DateTime)Get(r.Tag, "Time")).ToLocalTime() == today),
                    key + " Today filter classified a cached UTC timestamp by its UTC calendar date");
                period.SelectedIndex = 0;
                if (key == "loot" || key == "combat")
                {
                    search.Text = priorDay.ToString("yyyy-MM-dd");
                    Check(grid.RowCount == 2 && grid.Rows.Cast<DataGridViewRow>().All(r => ((DateTime)Get(r.Tag, "Time")).ToLocalTime() == priorDay),
                        key + " searchable dates differ from the visible local dates"); search.Clear();
                }
            }
        }
        finally
        {
            Static("HudJournal", "UseAccount", priorJournal); Set(preferences, "CombatByDungeon", grouped); Call(form, "SelectPage", "party");
            if (ticking) timer.Start();
        }
    }
    static void WidgetPreferences(Form form)
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        var bar = (FlowLayoutPanel)Get(form, "widgetBar");
        Control first = bar.Controls[0];
        Call(form, "MoveWidget", "widget:HP", new Point(first.Left + 2, first.Top + 2));
        Check((string)bar.Controls[0].Tag == "HP", "Dropping a widget did not move it to the target position");
        ((IList)Get(preferences, "HiddenWidgets")).Add("Gold"); Call(preferences, "Save"); Call(form, "RefreshWidgets");
        Check(!((Control)((IDictionary)Get(form, "widgets"))["Gold"]).Visible, "Disabled widget remained visible");
        object persisted = T("HudStorage").GetMethod("Load", F).MakeGenericMethod(preferences.GetType()).Invoke(null, new object[] { "layout.json", New("HudPreferences") });
        Check((string)((IList)Get(persisted, "WidgetOrder"))[0] == "HP" && ((IList)Get(persisted, "HiddenWidgets")).Contains("Gold"),
            "Widget order and visibility did not survive disk serialization");
        using (var restored = (Form)New("ExaltDashboardForm"))
        {
            restored.Location = new Point(-2500, -2500); restored.Show(); Layout(restored);
            var restoredBar = (FlowLayoutPanel)Get(restored, "widgetBar");
            Check((string)restoredBar.Controls[0].Tag == "HP" && !((Control)((IDictionary)Get(restored, "widgets"))["Gold"]).Visible,
                "A recreated dashboard lost widget order or visibility");
            Call(restored, "Shutdown");
        }
        ((IList)Get(preferences, "HiddenWidgets")).Remove("Gold"); Call(preferences, "Save"); Call(form, "RefreshWidgets");
    }
    static void WidgetValues(Form form)
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        var stateField = T("AccountWorkspace").GetField("state", F); object priorAccount = stateField.GetValue(null);
        object priorStorage = Get(preferences, "WidgetStorage");
        try
        {
            object account = New("AccountSnapshot"); Set(account, "UpdatedUtc", DateTime.UtcNow);
            Set(account, "Gold", 0); Set(account, "Stars", 68); Set(account, "CharacterSlots", 8); Set(account, "OwnedSkins", new[] { 1, 2 });
            Set(account, "RegularForgeFire", 77); Set(account, "SeasonalForgeFire", 88);
            Set(account, "RegularMaterials", new int?[] { 1, null, 3, 4 }); Set(account, "SeasonalMaterials", new int?[] { 5, 6, 7, 8 });
            object dead = New("AccountCharacter"); Set(dead, "HasPcStats", true); ((IDictionary)Get(dead, "PcStats"))["minutes_active"] = 90;
            ((IList)Get(account, "DeadCharacters")).Add(dead); stateField.SetValue(null, account);
            Set(preferences, "WidgetStorage", "Regular"); Call(form, "RefreshWidgets");
            var widgets = (IDictionary)Get(form, "widgets"); var tips = (ToolTip)Get(form, "tooltips");
            Check(((Control)widgets["Character"]).Text.Contains("FixtureHero") && tips.GetToolTip((Control)widgets["Character"]).Contains("ATT 75"),
                "Character widget omitted observed name or attribute details");
            Check(((Control)widgets["Playtime"]).Text.EndsWith("1h 30m"), "Cumulative playtime ignored known deceased-character statistics");
            Check(((Control)widgets["Gold"]).Text.EndsWith("   0") && ((Control)widgets["Fame"]).Text.Contains("\u2014") && ((Control)widgets["Skins"]).Text.EndsWith("   2"),
                "Currency/collection widgets confused a known zero with an unknown value");
            string dust = tips.GetToolTip((Control)widgets["Dust"]);
            Check(((Control)widgets["Dust"]).Text.Contains("\u2014") && !dust.Contains("0 / 0 / 0") && (dust.Contains("\u2014") || dust.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0 || dust.Contains("Not observed")),
                "Unknown dust tooltip fabricated zero balances");
            Check(((Control)widgets["Forge"]).Text.EndsWith("77") && ((Control)widgets["Materials"]).Text.EndsWith("1 / \u2014 / 3 / 4"),
                "Regular storage widgets omitted known values or replaced an unknown material tier");
            Set(account, "SeasonalDust", 60); Set(account, "SeasonalDustAmounts", new[] { 10, 20, 30 });
            Set(account, "Revision", (long)Get(account, "Revision") + 1);
            Set(preferences, "WidgetStorage", "Seasonal"); Call(form, "RefreshWidgets");
            Check(((Control)widgets["Forge"]).Text.EndsWith("88") && ((Control)widgets["Materials"]).Text.EndsWith("5 / 6 / 7 / 8") && ((Control)widgets["Dust"]).Text.EndsWith("60"),
                "Seasonal widget selection reused regular storage balances");
        }
        finally { stateField.SetValue(null, priorAccount); Set(preferences, "WidgetStorage", priorStorage); Call(form, "RefreshWidgets"); }
    }
    static void PartyLists(Form form)
    {
        Call(form, "SelectPage", "party");
        var page = (Control)((IDictionary)Get(form, "pages"))["party"];
        var grid = Children(page).OfType<DataGridView>().Single(); grid.CurrentCell = grid.Rows[0].Cells[0];
        string name = (string)grid.CurrentRow.Tag;
        var watch = Children(page).OfType<Button>().Single(b => b.Text == "Watch / unwatch");
        var ban = Children(page).OfType<Button>().Single(b => b.Text == "Ban / unban");
        watch.PerformClick(); ban.PerformClick();
        Check(((IEnumerable)Get(form, "watchlist")).Cast<string>().Contains(name) && ((IEnumerable)Get(form, "banned")).Cast<string>().Contains(name)
            && grid.Rows.Cast<DataGridViewRow>().Any(r => (string)r.Tag == name && Convert.ToString(r.Cells[5].Value) == "Watched / Banned"),
            "Party watch and banned flags replaced each other or were not shown together");
        string root = (string)T("HudStorage").GetProperty("Root", F).GetValue(null, null);
        Check(File.ReadAllText(Path.Combine(root, "party-watchlist.json")).Contains(name) && File.ReadAllText(Path.Combine(root, "party-banned.json")).Contains(name),
            "Separate party lists did not persist");
        Set(form, "lastNoticeCheck", DateTime.MinValue); Call(form, "CheckNotifications");
        Check(((IList)Get(Static("HudJournal", "Snapshot"), "Feed")).Cast<object>().Any(x => ((string)Get(x, "Text") ?? "").Contains("Banned-list player " + name)),
            "Banned-list arrival did not produce a local notification");
        using (var restored = (Form)New("ExaltDashboardForm"))
        {
            Check(((IEnumerable)Get(restored, "watchlist")).Cast<string>().Contains(name) && ((IEnumerable)Get(restored, "banned")).Cast<string>().Contains(name),
                "Recreated dashboard lost persisted watch or banned members");
            Call(restored, "Shutdown");
        }
        ban.PerformClick();
        Check(!((IEnumerable)Get(form, "banned")).Cast<string>().Contains(name) && ((IEnumerable)Get(form, "watchlist")).Cast<string>().Contains(name),
            "Unbanning a party member removed their independent watch preference");
        watch.PerformClick();
    }
    static void Pages(string images)
    {
        Static("HudJournal", "UseAccount", "ExpandedDashboardFixture");
        object snapshot = Snapshot(); Static("HudJournal", "ObserveSnapshot", snapshot);
        for (int i = 1; i <= 2; i++)
        {
            object previous = Snapshot(); Set(previous, "DungeonStartTime", DateTime.Now.AddMinutes(-10 - i));
            Set(Get(previous, "ActiveEnemy"), "EntityId", 500 + i); Static("HudJournal", "ObserveSnapshot", previous);
        }
        Static("HudJournal", "ObserveSnapshot", snapshot);
        using (var form = (Form)New("ExaltDashboardForm"))
        {
            form.Location = new Point(-2500, -2500); form.Show(); Call(form, "UpdateSnapshot", snapshot); Call(form, "RefreshDashboard"); Layout(form);
            Check(form.FormBorderStyle == FormBorderStyle.Sizable && form.MaximizeBox, "Expanded HUD is not a resizable/maximizable desktop window");
            Check(form.ShowInTaskbar, "Expanded HUD is absent from the Windows taskbar");
            Check((GetWindowLong(form.Handle, -20) & 0x08000020) == 0, "Expanded desktop accidentally uses click-through/no-activate overlay styles");
            var pages = (IDictionary)Get(form, "pages");
            string[] keys = { "live", "combat", "performance", "loot", "trophies", "missions", "quests", "characters", "treasury", "vault", "exaltations", "pets", "party", "players", "abilities", "chat", "moonlight", "calculator", "settings" };
            foreach (string key in keys)
            {
                var nav = Children(form).OfType<Button>().Single(b => b.Name == "nav_" + key);
                nav.PerformClick(); Layout(form);
                Check((string)Get(form, "SelectedPage") == key, "Navigation did not select " + key);
                var page = (Control)pages[key];
                Check(page.Visible && page.Width > 300 && page.Height > 150, "Page is hidden or has unusable dimensions: " + key);
                Check(((Control)Get(form, "widgetBar")).Visible && ((Control)Get(form, "taskBar")).Visible, "Widget/task bars disappeared on " + key);
                Check(pages.Values.Cast<Control>().Count(p => p.Visible) == 1, "Multiple pages overlap after navigation to " + key);
                var bar = (Control)Get(form, "widgetBar");
                Check(bar.Controls.Cast<Control>().Where(c => c.Visible).All(c => c.Top >= 0 && c.Bottom <= bar.ClientRectangle.Bottom),
                    "Widget bar clips labels when the horizontal scrollbar is visible");
                if (key == "trophies") Check(Children(page).OfType<DataGridView>().First().RowCount > 75, "Packaged dungeon and biome catalog is empty or truncated");
                if (key == "live" || key == "combat" || key == "missions" || key == "characters" || key == "settings" || key == "trophies") Render(form, Path.Combine(images, key + "-wide.png"));
            }
            Call(form, "SelectPage", "settings");
            var theme = Children((Control)pages["settings"]).OfType<ComboBox>().Single(c => c.Items.Contains("Light"));
            theme.SelectedItem = "Light"; Layout(form); Call(form, "SelectPage", "live");
            Check(form.BackColor.GetBrightness() > .75f, "Light theme selection did not apply to the existing desktop");
            TableTheme((Control)pages["combat"], true);
            Render(form, Path.Combine(images, "live-light.png"));
            Call(form, "SelectPage", "settings"); theme.SelectedItem = "Slate"; Layout(form);
            Check(form.BackColor.GetBrightness() < .4f, "Slate theme did not replace the light palette");
            TableTheme((Control)pages["combat"], false);
            theme.SelectedItem = "Midnight";
            TableTheme((Control)pages["combat"], false);
            Callouts(form);
            Call(form, "SelectPage", "combat");
            var combatGrid = Children((Control)pages["combat"]).OfType<DataGridView>().First();
            Check(combatGrid.RowCount == 3, "Combat fixtures did not populate independent encounters");
            combatGrid.CurrentCell = combatGrid.Rows[2].Cells[0];
            object selectedFight = combatGrid.CurrentRow.Tag;
            string selectedId = (string)Get(selectedFight, "Id");
            Static("HudJournal", "ObserveSnapshot", snapshot); Call(form, "RefreshPageData");
            Check((string)Get(combatGrid.CurrentRow.Tag, "Id") == selectedId && !ReferenceEquals(combatGrid.CurrentRow.Tag, selectedFight),
                "Refreshing cloned combat records lost the selected encounter");
            Call(form, "SelectPage", "party");
            Control partyPage = (Control)pages["party"];
            var grid = Children(partyPage).OfType<DataGridView>().First();
            Check(grid.RowCount == 25, "Party page does not display populated snapshot members");
            form.Size = form.MinimumSize; Layout(form);
            Check(partyPage.Width > 300 && partyPage.Height > 150, "Minimum dashboard size clips the active page");
            foreach (var toolbar in Children(partyPage).OfType<FlowLayoutPanel>().Where(p => p.Dock == DockStyle.Top))
                Check(toolbar.Controls.Cast<Control>().Where(c => c.Visible).All(c => c.Top >= 0 && c.Bottom <= toolbar.ClientRectangle.Bottom),
                    "Minimum-size toolbar clips interactive controls vertically");
            Check(grid.RowCount == 25, "Resizing lost party rows"); Render(form, Path.Combine(images, "party-minimum.png"));
            Call(form, "SelectPage", "does-not-exist"); Check((string)Get(form, "SelectedPage") == "party", "Unknown navigation key destroyed the active page");
            var widgets = (IDictionary)Get(form, "widgets");
            Check(widgets.Contains("HP") && ((Control)widgets["HP"]).Text.Contains("650"), "Live widgets are disconnected from snapshots");
            object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
            Set(preferences, "WidgetRows", 2); Call(form, "RefreshDashboard"); Layout(form);
            Check(((FlowLayoutPanel)Get(form, "widgetBar")).WrapContents && ((Control)Get(form, "widgetBar")).Height > 90,
                "Two-row widget preference was ignored");
            Check(((Control)Get(form, "widgetBar")).Controls.Cast<Control>().Where(c => c.Visible).Select(c => c.Top).Distinct().Count() == 2,
                "Two-row widget setting produced a different number of rows at minimum width");
            Render(form, Path.Combine(images, "widgets-two-rows-minimum.png"));
            Set(preferences, "WidgetRows", 1); Call(form, "RefreshDashboard"); Layout(form);
            WidgetValues(form);
            WidgetPreferences(form);
            PartyLists(form);
            RichCallouts(form, images);
            AutomaticCallouts(form);
            HistoryTimeZones(form);
            Set(preferences, "WidgetBar", false); Set(preferences, "TaskBar", false); Call(form, "RefreshDashboard");
            Check(!((Control)Get(form, "widgetBar")).Visible && !((Control)Get(form, "taskBar")).Visible, "Bar visibility preferences are ignored");
            Set(preferences, "WidgetBar", true); Set(preferences, "TaskBar", true); Call(form, "RefreshDashboard");
            form.WindowState = FormWindowState.Normal; form.Bounds = new Rectangle(60, 70, 1050, 710); form.TopMost = true; Call(form, "SaveLayout");
            Check((int)Get(preferences, "DesktopX") == 60 && (int)Get(preferences, "DesktopY") == 70, "Desktop position is not saved");
            Check((int)Get(preferences, "DesktopWidth") == 1050 && (int)Get(preferences, "DesktopHeight") == 710, "Desktop width and height are not saved");
            Check((bool)Get(preferences, "AlwaysOnTop") && (string)Get(preferences, "Page") == "party", "Pin/page preferences are not saved");
            form.WindowState = FormWindowState.Maximized; Layout(form); Call(form, "SaveLayout");
            Check((bool)Get(preferences, "Maximized") && (int)Get(preferences, "DesktopWidth") == 1050 && (int)Get(preferences, "DesktopHeight") == 710,
                "Maximizing lost the normal restore bounds or persisted a normal window state");
            form.WindowState = FormWindowState.Normal; Layout(form);
            // Closing the desktop returns to compact mode; it must not kill the tracker thread.
            int compactRequests = 0; EventHandler compact = (_, __) => compactRequests++;
            form.GetType().GetEvent("CompactRequested", F).AddEventHandler(form, compact);
            form.Close();
            Check(compactRequests == 1 && !form.IsDisposed && !form.Visible, "Desktop close did not request compact mode without disposing tracking UI");
            Call(form, "Shutdown"); Check(form.IsDisposed, "Shutdown leaked the dashboard window");
        }
    }
    static Form ManagedForm(string typeName)
    {
        var field = T("DpsOverlayManager").GetFields(F).SingleOrDefault(f => f.FieldType.FullName == "ExaltHelper." + typeName);
        return field == null ? null : field.GetValue(null) as Form;
    }
    static void OnUi(Form form, Action action) { if (form.InvokeRequired) form.Invoke(action); else action(); }
    static void Modes()
    {
        Static("DpsOverlayManager", "Initialize");
        try
        {
            Form overlay = null;
            for (int i = 0; i < 100; i++) { overlay = ManagedForm("DpsOverlayForm"); if (overlay != null && overlay.IsHandleCreated) break; Thread.Sleep(20); }
            Check(overlay != null && overlay.IsHandleCreated, "Overlay manager did not initialize its UI thread");
            Static("DpsOverlayManager", "SetExpanded", true);
            Form desktop = null;
            for (int i = 0; i < 100; i++) { desktop = ManagedForm("ExaltDashboardForm"); if (desktop != null && desktop.IsHandleCreated) break; Thread.Sleep(20); }
            Check(desktop != null && desktop.IsHandleCreated, "Expand command did not create desktop mode");
            OnUi(desktop, () => Check(desktop.Visible && !overlay.Visible, "Expanded mode left both views visible or hid the desktop"));
            OnUi(desktop, () => {
                using (var game = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) })
                {
                    game.Show(); game.WindowState = FormWindowState.Minimized;
                    Set(overlay, "cachedGameHwnd", game.Handle);
                    Call(overlay, "OnTimerTick", null, EventArgs.Empty);
                    Check(desktop.Visible && !overlay.Visible, "Minimizing the game hid the desktop or resurrected the compact overlay");
                    game.Close();
                    Call(overlay, "OnTimerTick", null, EventArgs.Empty);
                    Check(desktop.Visible, "Closing the game hid the independent desktop");
                }
            });
            Static("DpsOverlayManager", "ToggleClickThrough");
            OnUi(desktop, () => Check((GetWindowLong(desktop.Handle, -20) & 0x08000020) == 0 && desktop.Enabled, "F9 command locked the expanded dashboard"));
            Static("DpsOverlayManager", "ToggleVisibility");
            OnUi(desktop, () => Check(!desktop.Visible && !overlay.Visible, "F8 hide did not target the expanded view"));
            Static("DpsOverlayManager", "ToggleVisibility");
            OnUi(desktop, () => Check(desktop.Visible, "F8 show did not restore expanded view"));
            object current = Snapshot(); Static("DpsOverlayManager", "UpdateSnapshot", current);
            OnUi(desktop, () => { Call(desktop, "SelectPage", "party"); Call(desktop, "RefreshDashboard"); Check((string)Get(desktop, "SelectedPage") == "party", "Expanded controls stopped responding while overlay locked"); });
            OnUi(desktop, () => Check(ReferenceEquals(Get(desktop, "snapshot"), current), "Background snapshot delivery did not reach the UI thread"));
            Static("DpsOverlayManager", "SetExpanded", false);
            OnUi(overlay, () => Check(overlay.Visible && !desktop.Visible, "Compact command did not restore the compact view"));
            Static("DpsOverlayManager", "SetExpanded", true);
            OnUi(desktop, () => Check((string)Get(desktop, "SelectedPage") == "party", "Mode switching lost desktop navigation state"));
            OnUi(desktop, () => Check(ReferenceEquals(Get(desktop, "snapshot"), current), "Mode switching replaced the populated tracking snapshot"));
        }
        finally { Static("DpsOverlayManager", "Shutdown"); }
        var thread = (Thread)T("DpsOverlayManager").GetField("overlayThread", F).GetValue(null);
        Check(thread.Join(2000) && ManagedForm("ExaltDashboardForm").IsDisposed && ManagedForm("DpsOverlayForm").IsDisposed,
            "Shutdown left a dashboard, overlay, or UI thread running");
        Static("DpsOverlayManager", "UpdateSnapshot", Snapshot());
        Check(ManagedForm("ExaltDashboardForm").IsDisposed, "A late background snapshot recreated disposed UI");
    }
    public void Run(string path, string images)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        Warnings(); Monitors(); CompactBounds(); Pages(images); Modes();
        Console.WriteLine("PASS: " + checks + " expanded-dashboard monitor, page, persistence and mode assertions.");
        Console.WriteLine("Rendered review images: " + images);
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($harness))
$setup = New-Object AppDomainSetup
$setup.ApplicationBase = Split-Path $assembly -Parent
$domain = [AppDomain]::CreateDomain('Exalt expanded dashboard', $null, $setup)
try {
    $runner = $domain.CreateInstanceFromAndUnwrap($harness, 'ExpandedDashboardRegression')
    $runner.Run($assembly, $testRoot)
} catch { Write-Host $_.Exception.ToString(); exit 1 }
finally { [AppDomain]::Unload($domain); Remove-Item -LiteralPath $harness -Force }
