param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/combat-tracker"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'isolated-data'
Add-Type -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms,System.Web.Extensions -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

public static class CombatTrackerViewRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly app; static int checks; static string output;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object target, string name)
    {
        Type type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        for (Type current = type; current != null; current = current.BaseType) { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(instance); }
        return type.GetProperty(name, F).GetValue(instance, null);
    }
    static void Set(object target, string name, object value)
    {
        Type type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        var field = type.GetField(name, F); if (field != null) field.SetValue(instance, value); else type.GetProperty(name, F).SetValue(instance, value, null);
    }
    static object Call(object target, string name, params object[] values)
    {
        Type type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        return type.GetMethods(F).Single(m => m.Name == name && m.GetParameters().Length == values.Length).Invoke(instance, values);
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static IList List(object value, string name) { return (IList)Get(value, name); }
    static IEnumerable<Control> Children(Control control) { yield return control; foreach (Control child in control.Controls) foreach (var nested in Children(child)) yield return nested; }
    static void Pump() { for (int i = 0; i < 5; i++) Application.DoEvents(); }
    static void StopTimers(Control control)
    {
        for (Type type = control.GetType(); type != null; type = type.BaseType)
            foreach (var field in type.GetFields(F | BindingFlags.DeclaredOnly).Where(f => !f.IsStatic && f.FieldType == typeof(Timer))) { var timer = field.GetValue(control) as Timer; if (timer != null) timer.Stop(); }
        foreach (Control child in control.Controls) StopTimers(child);
    }
    static void Render(Control control, string name)
    {
        Pump(); using (var bitmap = new Bitmap(control.Width, control.Height)) { control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png); }
    }
    static object Player(string name, string cls, long damage, int hits, bool local = false)
    {
        object player = New("Proxy.DataStructures.PlayerDamageEntry"); Set(player, "Name", name); Set(player, "ClassName", cls); Set(player, "Damage", damage);
        Set(player, "Hits", (int?)hits); Set(player, "IsLocalPlayer", local); Set(player, "EquipmentIds", new[] { 2827, 2650, 2756, 2752 }); return player;
    }
    static object Enemy(int id, string name, int type, long damage, double seconds, params object[] players)
    {
        var enemy = New("Proxy.DataStructures.EnemyCombatSnapshot"); Set(enemy, "EntityId", id); Set(enemy, "EncounterId", id); Set(enemy, "ObjectType", type);
        Set(enemy, "Name", name); Set(enemy, "MaxHp", 9843750); Set(enemy, "CurrentHp", 3640000); Set(enemy, "TotalDamage", damage); Set(enemy, "FightDurationSeconds", seconds);
        Set(enemy, "EncounterEntityIds", new[] { id }); foreach (object player in players) List(enemy, "Damagers").Add(player); return enemy;
    }
    static object Snapshot(params object[] enemies)
    {
        var snapshot = New("Proxy.DataStructures.DpsSnapshot"); Set(snapshot, "DungeonName", "Oryx's Sanctuary"); Set(snapshot, "DungeonStartTime", new DateTime(2026, 11, 9, 18, 0, 0));
        Set(snapshot, "ElapsedDungeonSeconds", 880.0); if (enemies.Length > 0) Set(snapshot, "ActiveEnemy", enemies[0]);
        foreach (object enemy in enemies) List(snapshot, "Encounters").Add(enemy); return snapshot;
    }
    static string Id(object snapshot, int enemy) { return ((DateTime)Get(snapshot, "DungeonStartTime")).Ticks + ":" + enemy; }
    static object EmptyFights() { return Activator.CreateInstance(typeof(List<>).MakeGenericType(T("HudFightRecord"))); }
    static DataGridViewRow Find(DataGridView grid, string fight, string player = null, bool total = false)
    {
        return grid.Rows.Cast<DataGridViewRow>().Single(r => (string)Get(Get(r.Tag, "Fight"), "Id") == fight && (bool)Get(r.Tag, "Total") == total
            && (player == null ? (bool)Get(r.Tag, "Header") != total : Get(r.Tag, "Player") != null && (string)Get(Get(r.Tag, "Player"), "Name") == player));
    }
    static void Sprites()
    {
        Call(T("HudGameSprites"), "Initialize");
        object wizard = Call(T("HudGameSprites"), "Class", "Wizard"), priest = Call(T("HudGameSprites"), "Class", "Priest");
        object monster = Call(T("HudGameSprites"), "Object", 45613), portal = Call(T("HudGameSprites"), "Portal", "Oryx's Sanctuary");
        Check(wizard != null && priest != null && (int)Get(wizard, "AtlasId") == 2, "Class portraits must resolve animated idle frames in the characters atlas.");
        Check(monster != null && portal != null && (int)Get(portal, "AtlasId") == 4, "Monster and dungeon portal portraits must resolve real game artwork.");
        Check(!((Rectangle)Get(wizard, "Bounds")).Equals((Rectangle)Get(priest, "Bounds")), "Different classes must have different portraits.");
        Check(Call(T("HudGameSprites"), "Object", int.MaxValue) == null && Call(T("HudGameSprites"), "Portal", "Never Observed Dungeon") == null, "Unknown object names must not borrow unrelated artwork.");
        foreach (object sprite in new[] { wizard, priest, monster, portal })
        {
            Rectangle rect = (Rectangle)Get(sprite, "Bounds"); int atlasId = (int)Get(sprite, "AtlasId");
            string path = Path.Combine(Path.GetDirectoryName(app.Location), "assets", "sprites", atlasId == 2 ? "characters.png" : "mapObjects.png");
            using (var atlas = new Bitmap(path)) using (var drawn = new Bitmap(rect.Width, rect.Height))
            {
                Check(new Rectangle(Point.Empty, atlas.Size).Contains(rect), "Portrait crop is inside its declared atlas.");
                using (var graphics = Graphics.FromImage(drawn)) Call(T("HudGameSprites"), "Draw", graphics, sprite, new Rectangle(Point.Empty, drawn.Size), "combat");
                int visible = 0; bool equal = true;
                for (int y = 0; y < rect.Height; y++) for (int x = 0; x < rect.Width; x++) { Color actual = drawn.GetPixel(x, y), expected = atlas.GetPixel(rect.X + x, rect.Y + y); if (actual.A > 0) visible++; if (expected.A == 255 && actual.ToArgb() != expected.ToArgb()) equal = false; }
                Check(visible > 5 && equal, "Portrait pixels must come from the correct atlas, including coordinates valid in both atlases.");
            }
        }
        bool rejected = false;
        try { Call(T("HudGameSprites"), "InspectFrames", new byte[] { 255, 255, 255, 127, 0, 0, 0, 0 }); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected, "Truncated or out-of-bounds sprite data must be rejected.");
    }
    static void Tracking()
    {
        Call(T("Proxy.DataStructures.GameData"), "LoadGameData");
        object client = FormatterServices.GetUninitializedObject(T("Proxy.Client"));
        foreach (string name in new[] { "Enemies", "Entities", "Portals" }) Set(client, name, Activator.CreateInstance(client.GetType().GetField(name, F).FieldType));
        object local = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { 100 });
        Set(local, "PlayerName", "FixtureHero"); Set(local, "IsPlayer", true); Set(local, "Inventory", Enumerable.Repeat(-1, 20).ToArray()); Set(client, "ClientId", 100); Set(client, "Player", local);
        object tracker = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod"), new object[] { client });
        foreach (int id in new[] { 8101, 8102 })
        {
            object enemy = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { id }); Set(enemy, "ObjectType", (ushort)65000); Set(enemy, "StructureName", "Identical monster"); Set(enemy, "Hp", 1000); Set(enemy, "MaxHp", 1000);
            ((IDictionary)Get(client, "Enemies"))[id] = enemy;
        }
        Call(tracker, "RecordCombatDamage", 8101, 100, false, "First", "Wizard"); Call(tracker, "RecordCombatDamage", 8102, 250, false, "Second", "Priest");
        object snapshot = Call(tracker, "CompileCurrentSnapshot"); var encounters = List(snapshot, "Encounters").Cast<object>().ToArray();
        Check(encounters.Length == 2 && encounters.Select(e => (int)Get(e, "EncounterId")).Distinct().Count() == 2, "The real tracker must expose both simultaneous same-name monsters.");
        Check(encounters.Sum(e => (long)Get(e, "TotalDamage")) == 350 && encounters.All(e => List(e, "Damagers").Count == 1), "Each target retains only its own contributors and damage.");
        Check(encounters.All(e => (int)Get(List(e, "Damagers")[0], "Hits") == 1), "Actual tracked hit counts must reach UI snapshots.");
        Call(T("HudJournal"), "UseAccount", "CombatTrackerJournalFixture"); Call(T("HudJournal"), "WaitForLoads"); Call(T("HudJournal"), "ObserveSnapshot", snapshot);
        var records = (IList)Call(T("HudJournal"), "SnapshotForRun", Get(snapshot, "DungeonStartTime"));
        Check(records.Count == 2, "The journal must retain non-focused living encounters too.");
        ((IDisposable)tracker).Dispose();
    }
    static void View()
    {
        object first = Enemy(11, "Twin Guardian", 2354, 1000, 10, Player("Runner", "Rogue", 250, 4), Player("Hero", "Wizard", 750, 7, true));
        object second = Enemy(12, "Twin Guardian", 45613, 2000, 20, Player("Runner", "Rogue", 1600, 20), Player("Hero", "Wizard", 400, 6, true));
        object snapshot = Snapshot(first, second); string firstId = Id(snapshot, 11), secondId = Id(snapshot, 12);
        using (var form = new Form { Width = 1180, Height = 760, StartPosition = FormStartPosition.Manual, Location = new Point(-2200, -2200) })
        using (var page = (Control)New("HudCombatTrackerPage"))
        {
            form.Controls.Add(page); form.Show(); Call(page, "SetLive", snapshot, "account-a", EmptyFights()); Pump();
            var grid = (DataGridView)Get(page, "Grid"); var expanded = (IEnumerable)Get(page, "ExpandedIds");
            Check(grid.Rows.Cast<DataGridViewRow>().Count(r => (bool)Get(r.Tag, "Header")) == 2, "Same-named monsters must have separate expandable sections.");
            Check(expanded.Cast<string>().SequenceEqual(new[] { firstId }), "The current focus expands initially.");
            Check(Convert.ToString(Find(grid, firstId, "Hero").Cells["Damage"].Value) == "750" && Convert.ToString(Find(grid, firstId, "Hero").Cells["Share"].Value) == "75.0%", "The first monster's rank/share must use only its own damage.");
            Call(page, "ToggleEncounter", secondId);
            Check(Convert.ToString(Find(grid, secondId, "Runner").Cells["Rank"].Value) == "1" && Convert.ToString(Find(grid, secondId, "Runner").Cells["DPS"].Value) == "80", "Different monsters must have independent rankings and fight-duration DPS.");
            var savedRow = Find(grid, firstId, "Hero"); grid.CurrentCell = savedRow.Cells["Player"];
            Set(List(first, "Damagers")[1], "Damage", 700L); Set(first, "TotalDamage", 950L); Call(page, "SetLive", snapshot, "account-a", EmptyFights());
            Check(ReferenceEquals(savedRow, Find(grid, firstId, "Hero")) && grid.CurrentRow == savedRow && ((IEnumerable)Get(page, "ExpandedIds")).Cast<string>().Count() == 2, "Live correction must preserve rows, selection, and expansion.");
            Set(List(first, "Damagers")[1], "Damage", 0L); Set(List(first, "Damagers")[1], "Hits", (int?)0); Set(first, "TotalDamage", 250L); Call(page, "SetLive", snapshot, "account-a", EmptyFights());
            Check(Convert.ToString(Find(grid, firstId, "Hero").Cells["Damage"].Value) == "0" && Convert.ToString(Find(grid, firstId, "Hero").Cells["Hits"].Value) == "0", "Corrections to zero must remove stale damage and hits.");
            var search = (TextBox)Get(page, "Search"); search.Text = "Hero"; Check(grid.Rows.Cast<DataGridViewRow>().Count(r => Get(r.Tag, "Player") != null) == 4, "Searching for a contributor must preserve the full monster leaderboard.");
            search.Text = "absent"; Check(grid.RowCount == 0, "Encounter search must filter unmatched monsters."); search.Clear();
            grid.CurrentCell = Find(grid, secondId).Cells["Player"]; var key = new KeyEventArgs(Keys.Left); Call(grid, "OnKeyDown", key);
            Check(!((IEnumerable)Get(page, "ExpandedIds")).Cast<string>().Contains(secondId) && key.Handled, "Keyboard left must collapse the selected encounter.");
            Call(grid, "OnKeyDown", new KeyEventArgs(Keys.Right)); Check(((IEnumerable)Get(page, "ExpandedIds")).Cast<string>().Contains(secondId), "Keyboard right must expand the selected encounter.");
            Set(second, "IsDead", true); Call(page, "SetLive", snapshot, "account-a", EmptyFights()); ((ComboBox)Get(page, "StateFilter")).SelectedIndex = 2;
            Check(grid.Rows.Cast<DataGridViewRow>().Where(r => (bool)Get(r.Tag, "Header")).All(r => (string)Get(Get(r.Tag, "Fight"), "Id") == secondId), "The defeated filter must use each encounter's state.");
            ((ComboBox)Get(page, "StateFilter")).SelectedIndex = 0;
            var preferences = Get(T("HudPreferences"), "Current"); var previous = (Color[])Get(T("HudTheme"), "Palette"); Set(preferences, "Theme", "Light"); Call(T("HudTheme"), "Apply", page, previous);
            Check(Find(grid, firstId).Height == 70 && grid.DefaultCellStyle.ForeColor.GetBrightness() < .5, "Theme switches must preserve expanded header heights and readable text.");
            form.Width = 660; Pump(); Check(grid.Width > 500 && grid.Height > 200 && grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) > grid.ClientSize.Width, "Narrow layouts must retain a usable table with horizontal scrolling.");
            Render(form, "combat-narrow-light");
            previous = (Color[])Get(T("HudTheme"), "Palette"); Set(preferences, "Theme", "Midnight"); Call(T("HudTheme"), "Apply", page, previous);
            Call(page, "SetLive", Snapshot(), "account-b", EmptyFights());
            Check(grid.RowCount == 0 && !((IEnumerable)Get(page, "ExpandedIds")).Cast<string>().Any(), "Account/session changes must clear the old encounter view.");
            var legacy = New("HudFightRecord"); Set(legacy, "Id", "old:1"); Set(legacy, "Enemy", "Legacy boss"); Set(legacy, "Damage", 120L); Set(legacy, "Seconds", 4.0);
            var oldPlayer = Player("Old player", "Priest", 120, 0); Set(oldPlayer, "Hits", null); List(legacy, "Players").Add(oldPlayer);
            var legacyList = (IList)EmptyFights(); legacyList.Add(legacy); Call(page, "SetHistory", legacyList, "Recorded dungeon");
            Check(Convert.ToString(Find(grid, "old:1", "Old player").Cells["Hits"].Value) == "\u2014" && Convert.ToString(Find(grid, "old:1", "Old player").Cells["Taken"].Value) == "\u2014", "Legacy encounters must distinguish unavailable observations from zero.");
        }
    }
    static void Preview()
    {
        string[] names = { "Kotekmax", "Lollifant", "Lolshpe", "Akdr", "ZorbakDu", "RubyLee", "Hero", "Biggadikk" };
        string[] classes = { "Knight", "Wizard", "Priest", "Rogue", "Warrior", "Sorcerer", "Archer", "Paladin" };
        var players = names.Select((name, i) => Player(name, classes[i], 899075 - i * 76000, 2176 - i * 180, i == 6)).ToArray();
        long total = players.Sum(p => (long)Get(p, "Damage"));
        object boss = Enemy(101, "Oryx the Mad God", 2354, total, 489, players); Set(boss, "EncounterEntityIds", new[] { 101, 102, 103 });
        object beisa = Enemy(201, "Chief Beisa", 45613, 2169000, 187, Player("Hero", "Archer", 2169000, 1100, true)); Set(beisa, "IsDead", true); Set(beisa, "CurrentHp", 0);
        object other = Enemy(202, "Oryx the Mad God", 5952, 418800, 93, Player("Kotekmax", "Knight", 418800, 303)); Set(other, "IsDead", true); Set(other, "CurrentHp", 0);
        object snapshot = Snapshot(boss, beisa, other);
        Call(T("HudJournal"), "UseAccount", "CombatTrackerPreview"); Call(T("HudJournal"), "WaitForLoads"); Call(T("HudJournal"), "ObserveSnapshot", snapshot);
        using (var form = (Form)New("ExaltDashboardForm"))
        {
            StopTimers(form); form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-2200, -2200); form.Size = new Size(1450, 1000); form.Show();
            Call(form, "UpdateSnapshot", snapshot); Call(form, "SelectPage", "performance"); Call(form, "RefreshDashboard"); StopTimers(form); Pump();
            Check(Children(form).OfType<Button>().Any(b => b.Name == "nav_performance" && b.Text == "Combat tracker"), "The dedicated tracker must be discoverable in the sidebar.");
            Render(form, "expanded-combat-tracker");
            var page = (Control)Get(form, "combatTracker"); var grid = (DataGridView)Get(page, "Grid");
            Check(grid.RowCount == 12, "The dashboard must show all current and completed monster groups without duplicates.");
            Call(form, "Shutdown");
        }
    }
    public static void Run(string path, string directory)
    {
        output = directory; app = Assembly.LoadFrom(Path.GetFullPath(path)); Directory.SetCurrentDirectory(Path.GetDirectoryName(app.Location));
        Application.EnableVisualStyles(); Sprites(); Tracking(); View(); Preview();
        Console.WriteLine("PASS: " + checks + " combat tracker, attribution, persistence, sprite, interaction and layout assertions.");
    }
}
'@
try { [CombatTrackerViewRegression]::Run((Resolve-Path -LiteralPath $AssemblyPath).Path, $testRoot) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
Write-Output ('Combat tracker results: ' + $testRoot)
