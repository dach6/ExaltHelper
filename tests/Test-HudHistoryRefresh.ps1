param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts/hud-history-refresh"
)
$ErrorActionPreference = 'Stop'
$taskDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputDirectory)))
if ($taskDrive.AvailableFreeSpace -le 5GB) { throw 'Disk guard stopped work: at most 5 GiB free.' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$priorData = $env:EXALTHELPER_HUD_DATA
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'data'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$harness = Join-Path $testRoot 'HudHistoryRefreshTest.dll'
Add-Type -OutputAssembly $harness -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed class HudHistoryRefreshRegression : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app;
    static readonly List<string> results = new List<string>();
    static int checks, failures;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object instance, string name)
    {
        Type type = instance as Type ?? instance.GetType(); object target = instance is Type ? null : instance;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target); }
        return type.GetProperty(name, F).GetValue(target, null);
    }
    static void Set(object instance, string name, object value)
    {
        Type type = instance as Type ?? instance.GetType(); object target = instance is Type ? null : instance;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) { field.SetValue(target, value); return; } }
        type.GetProperty(name, F).SetValue(target, value, null);
    }
    static object Call(object instance, string name, params object[] args)
    { Type type = instance as Type ?? instance.GetType(); return type.GetMethod(name, F).Invoke(instance is Type ? null : instance, args); }
    static object Journal(string name, params object[] args) { return Call(T("HudJournal"), name, args); }
    static IList List(object source, string name) { return (IList)Get(source, name); }
    static void Check(bool pass, string message)
    { checks++; if (!pass) failures++; string line = (pass ? "PASS " : "FAIL ") + message; results.Add(line); Console.WriteLine(line); }
    static readonly string[] pageKeys = { "live", "chat", "loot", "combat", "trophies" };
    static int[] Revisions() { return pageKeys.Select(key => (int)Journal("PageRevision", key)).ToArray(); }
    static void Changes(int[] prior, string description, params string[] changed)
    {
        int[] next = Revisions();
        for (int i = 0; i < pageKeys.Length; i++) Check(changed.Contains(pageKeys[i]) ? next[i] > prior[i] : next[i] == prior[i], description + " updates only required history: " + pageKeys[i]);
    }
    static object CombatSnapshot()
    {
        object snapshot = New("Proxy.DataStructures.DpsSnapshot"), enemy = New("Proxy.DataStructures.EnemyCombatSnapshot"), player = New("Proxy.DataStructures.PlayerDamageEntry");
        Set(snapshot, "DungeonName", "The Nest"); Set(snapshot, "DungeonStartTime", new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        Set(enemy, "EntityId", 17); Set(enemy, "Name", "History refresh fixture"); Set(enemy, "IsBoss", true); Set(enemy, "TotalDamage", 100L); Set(enemy, "FightDurationSeconds", 5.0);
        Set(player, "Name", "Fixture Knight"); Set(player, "Damage", 100L); List(enemy, "Damagers").Add(player); Set(snapshot, "ActiveEnemy", enemy); return snapshot;
    }
    static void PageRevisions()
    {
        Journal("BeginConnection"); int[] before = Revisions();
        Journal("ObserveSnapshot", New("Proxy.DataStructures.DpsSnapshot")); Changes(before, "Stats without history");
        before = Revisions(); var combat = CombatSnapshot(); Journal("ObserveSnapshot", combat); Changes(before, "Active combat", "combat");
        before = Revisions(); Set(Get(combat, "ActiveEnemy"), "TotalDamage", 150L); Journal("ObserveSnapshot", combat); Changes(before, "Damage correction", "combat");
        before = Revisions(); Set(Get(combat, "ActiveEnemy"), "IsDead", true); Journal("ObserveSnapshot", combat); Changes(before, "First dungeon completion", "combat", "trophies");
        before = Revisions(); Journal("ObserveSnapshot", combat); Changes(before, "Repeated completion", "combat");
        before = Revisions(); Journal("AppendFeed", "Combat", "Independent feed event"); Changes(before, "Feed event", "live");
        object packet = New("Proxy.Networking.Packets.TextPacket"); Set(packet, "Name", "FixturePlayer"); Set(packet, "Recipient", ""); Set(packet, "Text", "Ordinary realm chat fixture");
        before = Revisions(); Journal("ObservePacket", null, packet, false); Changes(before, "Realm chat", "chat");
        object bag = New("Proxy.DataStructures.LootBagDrop"); Set(bag, "DropId", "refresh-bag"); Set(bag, "JournalScopeId", Get(T("HudJournal"), "ScopeId"));
        Set(bag, "BagType", "White Bag"); Set(bag, "Dungeon", "The Nest"); List(bag, "ItemIds").Add(17842); List(bag, "ItemRarities").Add("Legendary");
        before = Revisions(); Journal("RecordLoot", bag); Changes(before, "New loot", "loot", "trophies");
        before = Revisions(); Journal("RecordLoot", bag); Changes(before, "Duplicate loot");
        before = Revisions(); Journal("RecordFeedLoot", bag); Changes(before, "Loot feed entry", "live");
        for (int i = 0; i < 501; i++) Journal("AppendFeed", "Game", "Cap fixture " + i);
        Check(List(Journal("SnapshotForPage", "live"), "Feed").Count == 500, "Feed keeps its production 500-record cap");
        before = Revisions(); Journal("UseAccount", "HistoryRefreshOtherAccount"); Journal("WaitForLoads"); Changes(before, "Account change", pageKeys);
        before = Revisions(); Journal("BeginConnection"); Changes(before, "New connection", pageKeys);
        Check(List(Journal("SnapshotForPage", "loot"), "Loot").Count == 0 && List(Journal("SnapshotForPage", "live"), "Feed").Count == 0, "New connection clears visible account history");
    }
    static object Fixture()
    {
        var data = New("HudJournalData");
        for (int i = 0; i < 40; i++)
        {
            var log = New("HudLogEntry"); Set(log, "Id", "log-" + i); Set(log, "Time", DateTime.UtcNow.AddSeconds(-i)); Set(log, "Category", "Combat");
            Set(log, "Text", "Fixture event " + i); Set(log, "LearnedEventModifiers", new[] { "Known modifier" });
            var contents = New("HudFeedLoot"); Set(contents, "BagType", "White Bag"); Set(contents, "ItemIds", new[] { 17842 }); Set(contents, "Enchantments", new[] { new[] { "ATT +2" } }); Set(log, "FeedLoot", contents);
            List(data, "Feed").Add(log); List(data, "Chat").Add(log);
            var loot = New("HudLootRecord"); Set(loot, "Id", "loot-" + i); Set(loot, "ItemId", 17842); Set(loot, "Item", "Fixture item " + i); Set(loot, "Rarity", "Legendary");
            Set(loot, "Bag", "White Bag"); Set(loot, "Time", DateTime.UtcNow.AddSeconds(-i)); Set(loot, "Enchantments", "ATT +2"); List(data, "Loot").Add(loot);
            var fight = New("HudFightRecord"); Set(fight, "Id", "fight-" + i); Set(fight, "Equipment", new[] { 17842, 21228, 14471, 3813 }); Set(fight, "Enemy", "Fixture enemy"); Set(fight, "Seconds", 5.0);
            var damage = New("Proxy.DataStructures.PlayerDamageEntry"); Set(damage, "Name", "Fixture Knight"); Set(damage, "Damage", 100L); List(fight, "Players").Add(damage);
            var source = New("HudDamageSource"); Set(source, "ItemId", 17842); Set(source, "Damage", 100L); List(fight, "Sources").Add(source); ((IDictionary)Get(fight, "DamageTaken"))["Fixture Knight"] = 25L;
            List(data, "Fights").Add(fight);
        }
        var trophy = New("HudTrophyRecord"); Set(trophy, "ItemId", 17842); Set(trophy, "Count", 3L); List(data, "Trophies").Add(trophy);
        ((IDictionary)Get(data, "DungeonRuns"))["The Nest"] = 2L; List(data, "RecentLootIds").Add("ledger-loot"); List(data, "RecentFightIds").Add("ledger-fight"); List(data, "RecentRunIds").Add("ledger-run");
        Set(T("HudJournal"), "data", data); return data;
    }
    static long Allocated(Func<object> read)
    {
        read(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        for (int i = 0; i < 8; i++) read();
        return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;
    }
    static void PageSnapshots()
    {
        var data = Fixture(); string[] names = { "Feed", "Chat", "Loot", "Fights", "Trophies" };
        for (int page = 0; page < pageKeys.Length; page++)
        {
            object copy = Journal("SnapshotForPage", pageKeys[page]);
            for (int collection = 0; collection < names.Length; collection++) Check(List(copy, names[collection]).Count == (page == collection ? List(data, names[collection]).Count : 0), pageKeys[page] + " contains only its requested " + names[collection] + " history");
            Check(List(copy, "RecentLootIds").Count + List(copy, "RecentRunIds").Count + List(copy, "RecentFightIds").Count == 0, pageKeys[page] + " omits persistence ledgers");
            Check(!ReferenceEquals(List(copy, names[page])[0], List(data, names[page])[0]), pageKeys[page] + " returns detached records");
        }
        object live = Journal("SnapshotForPage", "live"), chat = Journal("SnapshotForPage", "chat");
        Set(List(live, "Feed")[0], "Text", "Changed copy"); ((string[])Get(List(chat, "Chat")[0], "LearnedEventModifiers"))[0] = "Changed copy";
        object copiedContents = Get(List(live, "Feed")[0], "FeedLoot"); ((int[])Get(copiedContents, "ItemIds"))[0] = 1; ((string[][])Get(copiedContents, "Enchantments"))[0][0] = "Changed copy";
        Check((string)Get(List(data, "Feed")[0], "Text") == "Fixture event 0" && ((string[])Get(List(data, "Chat")[0], "LearnedEventModifiers"))[0] == "Known modifier", "Feed/chat projection edits cannot mutate journal text or modifier arrays");
        object originalContents = Get(List(data, "Feed")[0], "FeedLoot"); Check(((int[])Get(originalContents, "ItemIds"))[0] == 17842 && ((string[][])Get(originalContents, "Enchantments"))[0][0] == "ATT +2", "Feed projection detaches item IDs and nested enchantments");
        object fight = List(Journal("SnapshotForPage", "combat"), "Fights")[0]; ((int[])Get(fight, "Equipment"))[0] = 1; Set(List(fight, "Players")[0], "Damage", 1L); Set(List(fight, "Sources")[0], "Damage", 2L); ((IDictionary)Get(fight, "DamageTaken"))["Fixture Knight"] = 3L;
        object original = List(data, "Fights")[0]; Check(((int[])Get(original, "Equipment"))[0] == 17842 && (long)Get(List(original, "Players")[0], "Damage") == 100 && (long)Get(List(original, "Sources")[0], "Damage") == 100 && (long)((IDictionary)Get(original, "DamageTaken"))["Fixture Knight"] == 25, "Combat projection detaches equipment, players, sources, and damage totals");
        object lootCopy = List(Journal("SnapshotForPage", "loot"), "Loot")[0]; Set(lootCopy, "Enchantments", "Changed copy"); Check((string)Get(List(data, "Loot")[0], "Enchantments") == "ATT +2", "Loot projection edits cannot mutate recorded enchantments");
        object trophies = Journal("SnapshotForPage", "trophies"); Set(List(trophies, "Trophies")[0], "Count", 1L); ((IDictionary)Get(trophies, "DungeonRuns"))["The Nest"] = 1L;
        Check((long)Get(List(data, "Trophies")[0], "Count") == 3 && (long)((IDictionary)Get(data, "DungeonRuns"))["The Nest"] == 2, "Trophy projection detaches discovery and dungeon-run totals");
        object empty = Journal("SnapshotForPage", "vault"); Check(names.All(name => List(empty, name).Count == 0) && ((IDictionary)Get(empty, "DungeonRuns")).Count == 0, "Vault requests no journal history");
        for (int i = 0; i < 500; i++)
        {
            var many = New("HudFightRecord"); Set(many, "Id", "large-" + i); Set(many, "Equipment", new int[4]);
            for (int p = 0; p < 20; p++) List(many, "Players").Add(New("Proxy.DataStructures.PlayerDamageEntry")); List(data, "Fights").Add(many);
        }
        long fullBytes = Allocated(() => Journal("Snapshot")), lootBytes = Allocated(() => Journal("SnapshotForPage", "loot"));
        Check(lootBytes * 4 < fullBytes, "Loot projection allocation does not scale with unrelated fights [loot=" + lootBytes + ", full=" + fullBytes + "]");
    }
    static object Row(int id, int hp, string extraTip = null)
    {
        var key = New("HudLogEntry"); Set(key, "Id", "p" + id); Set(key, "Text", "HP " + hp);
        var row = Activator.CreateInstance(T("HudTableRow"), new object[] { key, new object[] { "Player" + id.ToString("D2"), hp, id % 2 == 0 ? "Rare" : "Legendary" } });
        Set(row, "StableKey", "p" + id); Set(row, "Category", id % 2 == 0 ? "Alpha" : "Beta"); Set(row, "Time", DateTime.Today);
        Set(row, "Tooltip", extraTip ?? "Player " + id + " HP " + hp); return row;
    }
    static object[] FeedItems(object row) { return ((IEnumerable)Call(T("ExaltDashboardForm"), "FeedItems", row)).Cast<object>().ToArray(); }
    static void FeedRarities()
    {
        ((Task)Call(T("HudItemPresentation"), "WarmUpAsync")).GetAwaiter().GetResult();
        object bag = New("Proxy.DataStructures.LootBagDrop"); Set(bag, "BagType", "White Bag");
        foreach (int id in new[] { 17842, 17842 }) List(bag, "ItemIds").Add(id);
        List(bag, "ItemRarities").Add("Uncommon"); List(bag, "ItemRarities").Add("Divine");
        List(bag, "ItemEnchantCounts").Add(2); List(bag, "ItemEnchantCounts").Add(1);
        List(bag, "ItemEnchants").Add(new List<string> { "ATT +2" }); List(bag, "ItemEnchants").Add(new List<string> { "VIT +5" });
        object stored = Call(T("HudFeedLoot"), "FromBag", bag); var grades = (string[])Get(stored, "Rarities");
        Check(grades.SequenceEqual(new[] { "Uncommon", "Divine" }), "Feed records observed per-slot rarity from the loot bag");
        List(bag, "ItemRarities")[0] = "Rare"; Check(grades[0] == "Uncommon", "Recorded rarity array is detached from the tracker bag");
        object cloned = Call(stored, "Clone"); grades[1] = "Rare";
        Check(!ReferenceEquals(grades, Get(cloned, "Rarities")) && ((string[])Get(cloned, "Rarities"))[1] == "Divine", "Feed clone detaches per-slot rarity arrays");
        ((int[])Get(stored, "ItemIds"))[0] = 1; ((string[][])Get(stored, "Enchantments"))[0][0] = "Changed source";
        Check(((int[])Get(cloned, "ItemIds"))[0] == 17842 && ((string[][])Get(cloned, "Enchantments"))[0][0] == "ATT +2", "Feed clone independently retains item IDs and nested enchantments");
        object roundtrip = Call(cloned, "ToBag"); Check(List(roundtrip, "ItemRarities").Cast<string>().SequenceEqual(new[] { "Uncommon", "Divine" }), "Feed conversion back to a bag preserves observed rarities");
        List(roundtrip, "ItemRarities")[0] = "Common"; Check(((string[])Get(cloned, "Rarities"))[0] == "Uncommon", "Converted bag cannot mutate stored feed rarities");
        object row = New("HudLogEntry"); Set(row, "FeedLoot", cloned); Set(row, "Location", "The Nest"); var items = FeedItems(row);
        Check(items.Length == 2 && (int)Get(items[0], "ItemId") == (int)Get(items[1], "ItemId"), "Feed keeps adjacent instances of the same item");
        Check((string)Get(items[0], "Rarity") == "Uncommon" && (string)Get(items[1], "Rarity") == "Divine", "Observed feed grades override catalog and conflicting enchant counts");
        Check(((string)Get(items[0], "Tooltip")).Contains("ATT +2") && !((string)Get(items[0], "Tooltip")).Contains("VIT +5") && ((string)Get(items[1], "Tooltip")).Contains("VIT +5") && !((string)Get(items[1], "Tooltip")).Contains("ATT +2"), "Same-item feed tooltips retain their own enchantments");
        Check(ReferenceEquals(Get(items[0], "Icon"), Get(items[1], "Icon")), "Instance-specific feed details share the cached item sprite");
        Set(cloned, "Rarities", new string[0]); Set(cloned, "EnchantCounts", new[] { 1, 4 }); items = FeedItems(row);
        Check((string)Get(items[0], "Rarity") == "Uncommon" && (string)Get(items[1], "Rarity") == "Divine", "Legacy feed derives one-slot and four-slot rarity correctly");
        Set(cloned, "EnchantCounts", new[] { 2, 3 }); items = FeedItems(row);
        Check((string)Get(items[0], "Rarity") == "Rare" && (string)Get(items[1], "Rarity") == "Legendary", "Legacy feed derives two-slot and three-slot rarity correctly");
        Set(cloned, "Rarities", null); Set(cloned, "EnchantCounts", new[] { 0, 0 }); items = FeedItems(row);
        string catalogGrade = (string)Get(Call(T("ItemSpriteManager"), "GetItemMetadata", 17842), "Rarity");
        Check(items.All(item => (string)Get(item, "Rarity") == catalogGrade), "Legacy feed without observed grade or enchant slots falls back to the item catalog");
        Check(((string[])Get(Call(cloned, "Clone"), "Rarities")).Length == 0, "Older feed records with null rarity arrays remain clonable");
    }
    static Array Rows(int changed = -1, int hp = 0, string tip = null, bool reverse = false)
    {
        var values = Array.CreateInstance(T("HudTableRow"), 60);
        for (int i = 0; i < 60; i++) { int id = reverse ? 59 - i : i; values.SetValue(Row(id, id == changed ? hp : 100 + id, id == changed ? tip : null), i); } return values;
    }
    static Dictionary<string, DataGridViewRow> NativeRows(DataGridView grid) { return grid.Rows.Cast<DataGridViewRow>().ToDictionary(row => (string)row.HeaderCell.Tag); }
    static bool SameRows(DataGridView grid, Dictionary<string, DataGridViewRow> before) { return grid.RowCount == before.Count && grid.Rows.Cast<DataGridViewRow>().All(row => ReferenceEquals(row, before[(string)row.HeaderCell.Tag])); }
    static void TableRefresh()
    {
        using (var form = new Form { ClientSize = new Size(940, 480), StartPosition = FormStartPosition.Manual, Location = new Point(-1600, -1000), ShowInTaskbar = false })
        using (var page = (Control)Activator.CreateInstance(T("HudTablePage"), new object[] { "Refresh fixture", new[] { "Name", "HP", "Rarity" } }))
        {
            form.Controls.Add(page); form.Show(); Application.DoEvents(); var grid = (DataGridView)Get(page, "Grid");
            Call(page, "SetRows", Rows()); var before = NativeRows(grid); grid.CurrentCell = before["p12"].Cells[1]; grid.FirstDisplayedScrollingRowIndex = 8;
            int added = 0, removed = 0; grid.RowsAdded += (_, e) => added += e.RowCount; grid.RowsRemoved += (_, e) => removed += e.RowCount;
            object oldTag = before["p12"].Tag; int scroll = grid.FirstDisplayedScrollingRowIndex;
            Call(page, "SetRows", Rows()); Check(SameRows(grid, before) && added == 0 && removed == 0, "Unchanged values reuse native rows without remove/add events");
            Check(!ReferenceEquals(oldTag, before["p12"].Tag) && (string)Get(before["p12"].Tag, "Text") == "HP 112", "Unchanged values refresh backing row models");
            Check(ReferenceEquals(grid.CurrentRow, before["p12"]) && grid.FirstDisplayedScrollingRowIndex == scroll, "Unchanged values preserve selection and scroll");
            Call(page, "SetRows", Rows(12, 999)); Check(SameRows(grid, before) && added == 0 && removed == 0 && (int)before["p12"].Cells[1].Value == 999, "Changed HP updates its cell while reusing all native rows");
            Check(before["p12"].Cells.Cast<DataGridViewCell>().All(cell => cell.ToolTipText == "Player 12 HP 999") && (string)Get(before["p12"].Tag, "Text") == "HP 999", "Changed HP refreshes tooltip and model together");
            Check(ReferenceEquals(grid.CurrentRow, before["p12"]) && grid.FirstDisplayedScrollingRowIndex == scroll, "Changed HP preserves selection and scroll");
            grid.Sort(grid.Columns[1], ListSortDirection.Descending); before = NativeRows(grid); grid.CurrentCell = before["p12"].Cells[1];
            Call(page, "SetRows", Rows(59, 1500)); Check(SameRows(grid, before) && (string)grid.Rows[0].HeaderCell.Tag == "p59", "Changed sort value reorders existing native rows");
            Check(grid.SortedColumn == grid.Columns[1] && grid.SortOrder == SortOrder.Descending && ReferenceEquals(grid.CurrentRow, before["p12"]), "Live values preserve active sorting and selected record");
            var category = (ComboBox)Get(page, "Category"); category.SelectedItem = "Alpha"; before = NativeRows(grid);
            Check(grid.RowCount == 30 && (string)grid.CurrentRow.HeaderCell.Tag == "p12", "Category filter preserves a selected matching record");
            Call(page, "SetRows", Rows(12, 600)); Check(SameRows(grid, before) && grid.RowCount == 30 && (string)category.SelectedItem == "Alpha", "Live updates preserve category filters and reuse visible rows");
            var search = (TextBox)Get(page, "SearchBox"); search.Text = "Player12"; var single = grid.Rows[0];
            Call(page, "SetRows", Rows(12, 500, "New per-item enchantment details")); Check(grid.RowCount == 1 && ReferenceEquals(single, grid.Rows[0]) && (int)single.Cells[1].Value == 500, "Search remains applied as matching values change");
            Check(single.Cells.Cast<DataGridViewCell>().All(cell => cell.ToolTipText == "New per-item enchantment details"), "Tooltip changes reach every cell after filtering");
            search.Clear(); category.SelectedIndex = 0; before = NativeRows(grid); grid.CurrentCell = before["p12"].Cells[1]; grid.FirstDisplayedScrollingRowIndex = 5; scroll = grid.FirstDisplayedScrollingRowIndex;
            Call(page, "SetRows", Rows(12, 500, "Rebound item details", true)); Check(SameRows(grid, before) && grid.FirstDisplayedScrollingRowIndex == scroll && (string)grid.CurrentRow.HeaderCell.Tag == "p12", "Reordered input preserves sorted native rows, selection, and scroll");
            Check(before["p12"].Cells[0].ToolTipText == "Rebound item details", "Sorted model replacement uses current item details");
            var period = (ComboBox)Get(page, "Period"); period.SelectedIndex = 1; Call(page, "SetRows", Rows()); before = NativeRows(grid); added = removed = 0;
            Call(page, "RefreshDateFilter"); Check(SameRows(grid, before) && added == 0 && removed == 0, "Today filter keeps native rows intact within the same local day");
            var retained = (IList)Get(page, "rows"); string expired = (string)Get(retained[0], "StableKey"); Set(retained[0], "Time", DateTime.Today.AddTicks(-1));
            Set(page, "filterDate", DateTime.Today.AddDays(-1)); Call(page, "RefreshDateFilter");
            Check(grid.RowCount == 59 && grid.Rows.Cast<DataGridViewRow>().All(row => !Equals(row.HeaderCell.Tag, expired)), "Local midnight expires yesterday's record without a data refresh");
            Check((DateTime)Get(page, "filterDate") == DateTime.Today && period.SelectedIndex == 1, "Midnight refresh advances the cached date and preserves Today selection");
            before = NativeRows(grid); added = removed = 0; Call(page, "RefreshDateFilter"); Check(SameRows(grid, before) && added == 0 && removed == 0, "Repeated date checks do not rebuild already-current filtered rows");
            period.SelectedIndex = 0; before = NativeRows(grid); Set(page, "filterDate", DateTime.Today.AddDays(-1)); Call(page, "RefreshDateFilter");
            Check(grid.RowCount == 60 && SameRows(grid, before), "All dates keeps older records across midnight without rebuilding");
            form.Close();
        }
    }
    static void StopTimers(Control control)
    {
        for (Type t = control.GetType(); t != null; t = t.BaseType) foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            if (field.FieldType == typeof(System.Windows.Forms.Timer)) { var timer = field.GetValue(control) as System.Windows.Forms.Timer; if (timer != null) timer.Stop(); }
        foreach (Control child in control.Controls) StopTimers(child);
    }
    static void DashboardRefresh()
    {
        Journal("BeginConnection"); Fixture(); Set(Get(T("HudPreferences"), "Current"), "Page", "loot");
        using (var form = (Form)New("ExaltDashboardForm"))
        {
            StopTimers(form); form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-1700, -1100); form.ShowInTaskbar = false; form.Show(); StopTimers(form); Application.DoEvents();
            var tables = (IDictionary)Get(form, "tables");
            foreach (string page in new[] { "loot", "live", "chat" })
            {
                Call(form, "SelectPage", page); StopTimers(form); var grid = (DataGridView)Get(tables[page], "Grid");
                var native = NativeRows(grid); var tags = grid.Rows.Cast<DataGridViewRow>().ToDictionary(row => (string)row.HeaderCell.Tag, row => row.Tag);
                object snapshot = CombatSnapshot(); Set(snapshot, "TotalGroupDps", 12345.0); Journal("ObserveSnapshot", snapshot); Call(form, "UpdateSnapshot", snapshot); Call(form, "RefreshDashboard");
                Check(SameRows(grid, native) && grid.Rows.Cast<DataGridViewRow>().All(row => ReferenceEquals(row.Tag, tags[(string)row.HeaderCell.Tag])), page + " ignores unrelated combat without rebuilding history models");
                Call(form, "RefreshPageData"); Check(SameRows(grid, native), page + " explicit refresh uses stable IDs for copied history records");
                if (page == "live") Check(((IList)Get(form, "liveMetrics")).Cast<Label>().Any(label => label.Text.Contains(12345.ToString("N0"))), "Live combat metrics update without a Feed history refresh");
            }
            Call(form, "SelectPage", "loot"); StopTimers(form); var lootGrid = (DataGridView)Get(tables["loot"], "Grid"); object lootTag = lootGrid.Rows[0].Tag;
            Journal("AppendFeed", "Game", "Unrelated notification"); Call(form, "RefreshDashboard"); Check(ReferenceEquals(lootTag, lootGrid.Rows[0].Tag), "Feed notifications leave Loot models untouched");
            Journal("BeginConnection"); Call(form, "RefreshDashboard"); Check(lootGrid.RowCount == 0, "Account boundary clears active Loot history immediately");
            Set(T("HudJournal"), "dirty", false); Call(form, "Shutdown");
        }
    }
    public void Run(string path, string root)
    {
        AppDomain.MonitoringIsEnabled = true; app = Assembly.LoadFrom(path); Environment.CurrentDirectory = Path.GetDirectoryName(path);
        var preferences = Get(T("HudPreferences"), "Current"); Set(preferences, "SoundAlerts", false); Set(preferences, "RecordCombat", true); Set(preferences, "RecordLoot", true); Set(preferences, "RecordChat", true);
        Set(Get(preferences, "Callouts"), "AutoCopyDungeons", false);
        Set(T("AccountWorkspace"), "accessToken", ""); Set(T("AccountWorkspace"), "activeClient", null); var gate = (SemaphoreSlim)Get(T("AccountWorkspace"), "RefreshGate");
        if (!gate.Wait(0)) throw new InvalidOperationException("Could not isolate account refresh");
        ((System.Threading.Timer)Get(T("AccountWorkspace"), "saveTimer")).Change(Timeout.Infinite, Timeout.Infinite); ((System.Threading.Timer)Get(T("HudJournal"), "saveTimer")).Change(Timeout.Infinite, Timeout.Infinite);
        try { Call(T("Proxy.DataStructures.GameData"), "LoadGameData"); PageRevisions(); PageSnapshots(); FeedRarities(); TableRefresh(); DashboardRefresh(); }
        finally { Set(T("HudJournal"), "dirty", false); Set(T("AccountWorkspace"), "dirty", false); gate.Release(); File.WriteAllLines(Path.Combine(root, "review.txt"), results); }
        Console.WriteLine("HUD history refresh: " + checks + " checks, " + failures + " failures.");
        if (failures > 0) throw new InvalidOperationException(failures + " history refresh assertions failed.");
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($harness))
$setup = New-Object AppDomainSetup
$setup.ApplicationBase = Split-Path $assembly -Parent
$domain = [AppDomain]::CreateDomain('HUD history refresh regression', $null, $setup)
try {
    $runner = $domain.CreateInstanceFromAndUnwrap($harness, 'HudHistoryRefreshRegression')
    $runner.Run($assembly, $testRoot)
} finally {
    [AppDomain]::Unload($domain)
    $env:EXALTHELPER_HUD_DATA = $priorData
    @{ Assembly=$assembly; SHA256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash; CreatedUtc=[DateTime]::UtcNow.ToString('o'); Results=$testRoot } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'manifest.json') -Encoding UTF8
    Write-Output "History refresh results: $testRoot"
}
