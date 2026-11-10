param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/combat-run-navigation"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'isolated-data'
Add-Type -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms,System.Web.Extensions -TypeDefinition @'
using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

public static class CombatRunNavigationRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app; static int checks;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object target, string name)
    {
        Type type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        for (Type current = type; current != null; current = current.BaseType) { var f = current.GetField(name, F | BindingFlags.DeclaredOnly); if (f != null) return f.GetValue(instance); }
        return type.GetProperty(name, F).GetValue(instance, null);
    }
    static void Set(object target, string name, object value)
    {
        Type type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        var field = type.GetField(name, F); if (field != null) field.SetValue(instance, value); else type.GetProperty(name, F).SetValue(instance, value, null);
    }
    static object Call(object target, string name, params object[] args)
    {
        Type type = target as Type ?? target.GetType(); return type.GetMethods(F).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target is Type ? null : target, args);
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void UseAccount(string key)
    {
        var account = New("AccountSnapshot"); Set(account, "AccountKey", key); Set(T("AccountWorkspace"), "state", account);
        Call(T("HudJournal"), "UseAccount", key); Call(T("HudJournal"), "WaitForLoads");
    }
    static long Ticks(object run) { return ((DateTime)Get(run, "DungeonStartTime")).Ticks; }
    static object Snapshot(int hour, string dungeon, long damage, bool boss)
    {
        var snapshot = New("Proxy.DataStructures.DpsSnapshot"); var start = new DateTime(2026, 11, 9, hour, 0, 0, DateTimeKind.Local);
        Set(snapshot, "DungeonStartTime", start); Set(snapshot, "DungeonName", dungeon); Set(snapshot, "Timestamp", start.AddMinutes(3));
        Set(snapshot, "ElapsedDungeonSeconds", 180.0); Set(snapshot, "TotalDungeonDamage", damage);
        var player = New("Proxy.DataStructures.PlayerDamageEntry"); Set(player, "Name", "Runner"); Set(player, "ClassName", "Wizard");
        Set(player, "Damage", damage); Set(player, "EquipmentIds", new[] { 2827, 2650, 2756, 2752 });
        var enemy = New("Proxy.DataStructures.EnemyCombatSnapshot"); Set(enemy, "EncounterId", 11); Set(enemy, "EntityId", 11);
        Set(enemy, "Name", "Repeated monster"); Set(enemy, "TotalDamage", damage); Set(enemy, "IsDead", true); Set(enemy, "IsBoss", boss); Set(enemy, "FightDurationSeconds", 10.0);
        ((IList)Get(enemy, "Damagers")).Add(player); ((IList)Get(snapshot, "Encounters")).Add(enemy);
        return snapshot;
    }
    static void Observe(object run) { Call(T("HudJournal"), "ObserveSnapshot", run); }
    static void Refresh(Form form) { Call(form, "RefreshDashboard"); Application.DoEvents(); }
    static void Update(Form form, object run) { Call(form, "UpdateSnapshot", run); Refresh(form); }
    static void StopTimers(Form form) { ((Timer)Get(form, "refreshTimer")).Stop(); ((Timer)Get(form, "saveTimer")).Stop(); }
    static ComboBox Choices(object nav) { return (ComboBox)Get(nav, "Runs"); }
    static long Selected(object nav) { return (long)Get(nav, "SelectedStartTicks"); }
    static void Click(object nav, string button) { ((Button)Get(nav, button)).PerformClick(); }
    static DataGridView Grid(object form, string page) { return (DataGridView)Get(Get(form, page), "Grid"); }
    static DataGridViewRow[] Headers(DataGridView grid) { return grid.Rows.Cast<DataGridViewRow>().Where(row => (bool)Get(row.Tag, "Header")).ToArray(); }
    static void OnlyRun(DataGridView grid, object run, long damage)
    {
        var headers = Headers(grid);
        Check(headers.Length == 1 && (string)Get(Get(headers[0].Tag, "Fight"), "Id") == Ticks(run) + ":11", "Selected visit must contain only its own monster encounters.");
        Check((long)Get(Get(headers[0].Tag, "Fight"), "Damage") == damage, "Repeated monsters in separate dungeon visits must not share damage totals.");
    }
    public static void Run(string path, string output)
    {
        app = Assembly.LoadFrom(Path.GetFullPath(path)); Directory.SetCurrentDirectory(Path.GetDirectoryName(app.Location)); Application.EnableVisualStyles();
        UseAccount("CombatRunsA"); Set(T("Proxy.Mods.DpsTrackerMod"), "sessionAccountKey", "CombatRunsA");
        var preferences = Get(T("HudPreferences"), "Current"); Set(preferences, "WidgetBar", false); Set(preferences, "TaskBar", false); Set(preferences, "Page", "performance");
        object saved = Snapshot(9, "Abyss of Demons", 90, false), first = Snapshot(10, "The Shatters", 100, false), second = Snapshot(11, "The Shatters", 200, true), live = Snapshot(12, "Lost Halls", 300, false);
        foreach (var run in new[] { saved, first, second, live }) Observe(run);
        var session = (IList)Get(T("Proxy.Mods.DpsTrackerMod"), "sessionDungeonHistory"); session.Add(first); session.Add(second);
        Set(T("Proxy.Mods.DpsTrackerMod"), "lastLiveSnapshot", live);
        using (var form = (Form)New("ExaltDashboardForm"))
        {
            StopTimers(form); form.Location = new Point(-2400, -2400); form.Show(); Update(form, live); StopTimers(form);
            var nav = Get(form, "combatTrackerRuns"); var grid = Grid(form, "combatTracker");
            Check(Choices(nav).Items.Count == 4 && Selected(nav) == 0, "Tracker must offer Live and all three previous visits, including durable history outside the compact cache.");
            var repeats = Choices(nav).Items.Cast<object>().Where(run => (string)Get(run, "Dungeon") == "The Shatters").ToArray();
            Check(repeats.Length == 2 && repeats[0].ToString() != repeats[1].ToString(), "Repeated visits need distinct date/time labels.");
            Click(nav, "Previous"); OnlyRun(grid, second, 200); Check((bool)Get(Get(form, "combatTracker"), "history"), "Archived snapshot must be labeled as recorded, not current.");
            Check((int)Get(T("Proxy.Mods.DpsTrackerMod"), "currentHistoryIndex") == 1, "Expanded navigation must select the corresponding compact visit.");
            Click(nav, "Previous"); OnlyRun(grid, first, 100); Click(nav, "Previous"); OnlyRun(grid, saved, 90);
            Check(!((Button)Get(nav, "Previous")).Enabled, "Previous must stop at the oldest retained visit.");
            Click(nav, "Next"); OnlyRun(grid, first, 100); Click(nav, "Next"); OnlyRun(grid, second, 200); Click(nav, "Next"); OnlyRun(grid, live, 300);
            Check(Selected(nav) == 0 && !((Button)Get(nav, "Next")).Enabled, "Next must return to Live and stop there.");
            Call(T("Proxy.Mods.DpsTrackerMod"), "SelectDungeonByIndex", 0); Update(form, first); OnlyRun(grid, first, 100);
            Check(Selected(nav) == Ticks(first), "Compact history selection must be reflected in the expanded tracker.");
            Call(nav, "SelectRun", Ticks(saved), true);
            object newer = Snapshot(13, "Lost Halls", 400, false); Observe(newer); Set(T("Proxy.Mods.DpsTrackerMod"), "currentHistoryIndex", -1);
            Set(T("Proxy.Mods.DpsTrackerMod"), "lastLiveSnapshot", newer); Update(form, newer); OnlyRun(grid, saved, 90);
            Check(Selected(nav) == Ticks(saved), "A new map and incoming Live data must preserve a selected historical visit.");
            Call(T("Proxy.Mods.DpsTrackerMod"), "NavigateLiveDungeon"); Update(form, newer); OnlyRun(grid, newer, 400);
            Call(form, "SelectPage", "combat"); Refresh(form); StopTimers(form);
            var historyNav = Get(form, "combatHistoryRuns"); var historyGrid = Grid(form, "combatHistoryTracker");
            Check(Selected(historyNav) == -1 && Headers(historyGrid).Length == 5, "Combat history must default to all retained visits, not just the latest dungeon.");
            Check((bool)Get(Get(Headers(historyGrid)[0].Tag, "Fight"), "IsBoss"), "Bosses must remain first in the history overview.");
            Click(historyNav, "Previous"); OnlyRun(historyGrid, live, 300); Click(historyNav, "Next"); OnlyRun(historyGrid, newer, 400);
            Call(historyNav, "SelectRun", Ticks(second), true); OnlyRun(historyGrid, second, 200);
            Call(Get(form, "combatHistoryTracker"), "ToggleEncounter", Ticks(second) + ":11");
            var playerRow = historyGrid.Rows.Cast<DataGridViewRow>().Single(row => Get(row.Tag, "Player") != null); historyGrid.CurrentCell = playerRow.Cells["Player"];
            Check(Convert.ToString(playerRow.Cells["Damage"].Value) == "200", "Inline player rankings must use only the selected visit's damage.");
            Observe(newer); Refresh(form);
            Check(Selected(historyNav) == Ticks(second) && ReferenceEquals(historyGrid.CurrentRow, playerRow), "Live journal refresh must retain historical selection, native rows, and expansion.");
            session.RemoveAt(0); Set(T("Proxy.Mods.DpsTrackerMod"), "currentHistoryIndex", 0); Update(form, newer);
            Check(Selected(historyNav) == Ticks(second) && ReferenceEquals(historyGrid.CurrentRow, playerRow), "Archive index shifts must preserve the selected dungeon visit and its expanded player row.");
            form.Size = new Size(900, 700); Refresh(form);
            var navControl = (Control)historyNav; var dropdown = Choices(historyNav);
            Check(dropdown.Width > 200 && dropdown.Right <= navControl.ClientSize.Width && ((Button)Get(historyNav, "Live")).Right <= navControl.ClientSize.Width, "Run navigation must fit the minimum expanded window without horizontal scrolling.");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "expanded-run-history.png"), ImageFormat.Png); }
            var uncaptured = Snapshot(8, "Secluded Thicket", 500, false); Set(preferences, "RecordCombat", false); session.Add(uncaptured); Update(form, newer);
            Call(historyNav, "SelectRun", Ticks(uncaptured), true); OnlyRun(historyGrid, uncaptured, 500); Set(preferences, "RecordCombat", true);
            Call(T("HudJournal"), "Flush"); session.Clear(); UseAccount("CombatRunsB"); Refresh(form);
            Check(Choices(historyNav).Items.Count == 2 && Selected(historyNav) == -1 && historyGrid.RowCount == 0, "Account switches must clear both previous-run choices and encounter rows.");
            Call(form, "SelectPage", "performance"); Refresh(form);
            Check(Choices(nav).Items.Count == 1 && grid.RowCount == 0, "A stale display snapshot must not expose previous-account Live encounters.");
            UseAccount("CombatRunsA"); Call(form, "SelectPage", "combat"); Refresh(form);
            Check(Choices(historyNav).Items.Count == 7 && Headers(historyGrid).Length == 5, "All five recorded visits must reload from disk after the compact session cache is cleared.");
            Call(historyNav, "SelectRun", Ticks(saved), true); OnlyRun(historyGrid, saved, 90);
            Call(form, "Shutdown");
        }
        Console.WriteLine("PASS: " + checks + " dungeon visit navigation, repeat-run attribution, persistence, compact synchronization, account isolation and layout assertions.");
    }
}
'@
try { [CombatRunNavigationRegression]::Run((Resolve-Path -LiteralPath $AssemblyPath).Path, $testRoot) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
Write-Output ('Combat run navigation results: ' + $testRoot)
