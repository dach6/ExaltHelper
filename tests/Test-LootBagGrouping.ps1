param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts/loot-bag-grouping",
    [switch]$LegacyReproductionOnly
)
$ErrorActionPreference = 'Stop'
$taskDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputDirectory)))
if ($taskDrive.AvailableFreeSpace -le 5GB -or (Test-Path -LiteralPath 'C:/Users/Administrator/Desktop/code/.exalthelper-tools/disk-guard/STOP')) { throw 'Disk guard stopped work.' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$priorData = $env:EXALTHELPER_HUD_DATA
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'data'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$harness = Join-Path $testRoot 'LootBagGroupingTest.dll'
Add-Type -OutputAssembly $harness -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms,System.Web.Extensions -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public sealed class LootBagGroupingRegression : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app;
    static int checks;
    static readonly List<string> results = new List<string>();
    static readonly DateTime when = DateTime.Today.AddHours(12);
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object source, string name)
    {
        Type type = source as Type ?? source.GetType(); object target = source is Type ? null : source;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target); }
        return type.GetProperty(name, F).GetValue(target, null);
    }
    static void Set(object source, string name, object value)
    {
        Type type = source as Type ?? source.GetType(); object target = source is Type ? null : source;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) { field.SetValue(target, value); return; } }
        type.GetProperty(name, F).SetValue(target, value, null);
    }
    static object Call(object source, string name, params object[] args)
    { Type type = source as Type ?? source.GetType(); return type.GetMethod(name, F).Invoke(source is Type ? null : source, args); }
    static object Journal(string method, params object[] args) { return Call(T("HudJournal"), method, args); }
    static IList List(object source, string name) { return (IList)Get(source, name); }
    static object[] Values(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
    static Array Typed(string type, IEnumerable<object> values)
    { object[] entries = values.ToArray(); Array result = Array.CreateInstance(T(type), entries.Length); for (int i = 0; i < entries.Length; i++) result.SetValue(entries[i], i); return result; }
    static object[] Groups(params object[] records) { return Values(Call(T("HudLootBagHistory"), "Group", Typed("HudLootRecord", records))); }
    static object[] Items(object group) { return Values(Get(group, "Items")); }
    static void Check(bool pass, string text)
    {
        checks++; string line = (pass ? "PASS " : "FAIL ") + text; results.Add(line); Console.WriteLine(line);
        if (!pass) throw new InvalidOperationException(text);
    }
    static object Record(string id, string bagId = null, int slot = -1, int itemId = 17842, string enchant = "ATT +2", bool shiny = false)
    {
        object record = New("HudLootRecord"); Set(record, "Id", id); Set(record, "BagId", bagId); Set(record, "BagSlot", slot); Set(record, "ItemId", itemId);
        Set(record, "Item", Get(Call(T("ItemSpriteManager"), "GetItemMetadata", itemId), "Name")); Set(record, "Rarity", slot == 1 ? "Divine" : "Legendary");
        Set(record, "Enchantments", enchant); Set(record, "Shiny", shiny); Set(record, "Time", when); Set(record, "Bag", "White Bag");
        Set(record, "Enemy", "Stone Guardian"); Set(record, "Dungeon", "Lost Halls"); Set(record, "Character", "Fixture Knight"); return record;
    }
    static void IdentityAndInstances()
    {
        object first = Record("drop-a:0", "scope:map:drop-a", 0), second = Record("drop-a:1", "scope:map:drop-a", 1, enchant: "VIT +5", shiny: true);
        object[] grouped = Groups(second, first); object[] items = Items(grouped.Single());
        Check(grouped.Length == 1 && items.Length == 2 && ReferenceEquals(items[0], first) && ReferenceEquals(items[1], second), "One explicit bag keeps separate identical item instances in slot order");
        object other = Record("drop-b:0", "scope:map:drop-b", 0);
        Check(Groups(first, second, other).Length == 2, "Different bag identities remain separate despite identical type, timestamp and context");
        object otherMap = Record("drop-a:0", "scope:other-map:drop-a", 0), otherSession = Record("drop-a:0", "other-scope:map:drop-a", 0);
        Check(Groups(first, otherMap, otherSession).Length == 3, "Map and connection scopes keep reused drop IDs separate");
        object update = Record("drop-a:1", "scope:map:drop-a", 1, enchant: "WIS +7", shiny: true);
        items = Items(Groups(first, second, update).Single());
        Check(items.Length == 2 && ReferenceEquals(items[1], update), "A duplicate update replaces one slot's view without adding an icon");
        string key = (string)Get(Groups(second, first).Single(), "Id");
        Check(key == (string)Get(Groups(first, second, update).Single(), "Id"), "The bag row key remains stable as items arrive or details change");
        object[] views = Values(Call(T("ExaltDashboardForm"), "LootBagItems", Groups(first, second).Single()));
        Check(views.Length == 2 && (int)Get(views[0], "ItemId") == (int)Get(views[1], "ItemId"), "The production item selector supplies both instances to the shared icon renderer");
        string a = (string)Get(views[0], "Tooltip"), b = (string)Get(views[1], "Tooltip");
        Check(a.Contains("ATT +2") && !a.Contains("VIT +5") && b.Contains("VIT +5") && !b.Contains("ATT +2"), "Per-item hover details keep adjacent identical items' enchantments separate");
        Check((string)Get(views[0], "Rarity") == "Legendary" && (string)Get(views[1], "Rarity") == "Divine" && b.Contains("Shiny"), "Per-item rarity and shiny status survive bag grouping");
    }
    static void LegacyHistory()
    {
        string guid = "0123456789abcdef0123456789abcdef";
        object first = Record(guid + ":0"), second = Record(guid + ":1", enchant: "VIT +5");
        Check(Groups(second, first).Length == 1 && ReferenceEquals(Items(Groups(second, first).Single())[0], first), "Existing GUIDN:slot history recovers a bag and its original slot order");
        Check((int)Get(first, "BagSlot") == -1 && Get(first, "BagId") == null, "Legacy grouping does not rewrite saved records or invent persisted identities");
        object other = Record("fedcba9876543210fedcba9876543210:0");
        Check(Groups(first, second, other).Length == 2, "Different historical bag GUIDs never merge solely because their timestamps match");
        foreach (string field in new[] { "Enemy", "Dungeon", "Character", "Bag" })
        {
            object conflict = Record(guid + ":2"); Set(conflict, field, "Different " + field);
            Check(Groups(first, conflict).Length == 2, "Historical GUID recovery requires consistent " + field + " context");
        }
        object later = Record(guid + ":2"); Set(later, "Time", when.AddTicks(1));
        Check(Groups(first, later).Length == 2, "Historical records with conflicting drop instants remain separate");
        object utc = Record(guid + ":1"); Set(utc, "Time", when.ToUniversalTime());
        Check(Groups(first, utc).Length == 1, "Equivalent UTC and local instants identify the same historical bag");
        foreach (var pair in new[] { new[] { "old-bag:0", "old-bag:1" }, new[] { "not-a-guid00000000000000000000000:0", "not-a-guid00000000000000000000000:1" }, new[] { guid + ":8", guid + ":9" }, new[] { guid + ":00", guid + ":01" } })
            Check(Groups(Record(pair[0]), Record(pair[1])).Length == 2, "Unrecognized historical identity remains separate: " + pair[0]);
        Check(Groups(Record(null), Record(null)).Length == 2, "Missing historical IDs never combine unrelated records");
        var serializer = new JavaScriptSerializer(); string json = serializer.Serialize(Typed("HudLootRecord", new[] { first, second, other }));
        object[] reloaded = Values(serializer.Deserialize(json, T("HudLootRecord").MakeArrayType()));
        Check(Groups(reloaded).Length == 2 && Groups(reloaded).Sum(group => Items(group).Length) == 3, "Legacy records remain correctly grouped after JSON reload");
    }
    static object Bag(string id, DateTime mapTime, params int[] itemIds)
    {
        object bag = New("Proxy.DataStructures.LootBagDrop"); Set(bag, "DropId", id); Set(bag, "JournalScopeId", Get(T("HudJournal"), "ScopeId"));
        Set(bag, "DungeonStartTime", mapTime); Set(bag, "Dungeon", "Lost Halls"); Set(bag, "Character", "Fixture Knight"); Set(bag, "SourceMonster", "Stone Guardian");
        Set(bag, "BagType", "White Bag"); Set(bag, "DropTime", when);
        foreach (int idValue in itemIds) { List(bag, "ItemIds").Add(idValue); List(bag, "ItemRarities").Add("Legendary"); List(bag, "ItemEnchants").Add(new List<string> { "ATT +2" }); }
        return bag;
    }
    static long Discoveries(object data) { return List(data, "Trophies").Cast<object>().Sum(value => (long)Get(value, "Count")); }
    static void RecordingAndReload()
    {
        Journal("BeginConnection"); Journal("UseAccount", "LootGroupingFixture"); Journal("WaitForLoads");
        DateTime mapTime = when.AddMinutes(-10); string guid = "1234567890abcdef1234567890abcdef";
        object bag = Bag(guid, mapTime, 17842, 17842); Journal("RecordLoot", bag);
        object snapshot = Journal("Snapshot"); object[] records = List(snapshot, "Loot").Cast<object>().ToArray();
        string identity = (string)Get(records[0], "BagId"), scope = (string)Get(T("HudJournal"), "ScopeId");
        Check(records.Length == 2 && Discoveries(snapshot) == 2 && Groups(records).Length == 1, "Grouping preserves independent journal records and lifetime discovery counts");
        Check(identity == scope + ":" + mapTime.ToUniversalTime().Ticks + ":" + guid && records.All(record => (string)Get(record, "BagId") == identity), "New records save a connection/map/drop identity rather than a bag entity ID");
        Check(records.Select(record => (int)Get(record, "BagSlot")).SequenceEqual(new[] { 0, 1 }) && records.Select(record => (string)Get(record, "Id")).SequenceEqual(new[] { guid + ":0", guid + ":1" }), "New bag metadata preserves existing item-event IDs and original slots");
        int revision = (int)Journal("PageRevision", "loot"); Journal("RecordLoot", bag); Journal("RecordLoot", bag);
        snapshot = Journal("Snapshot"); Check(List(snapshot, "Loot").Count == 2 && Discoveries(snapshot) == 2 && (int)Journal("PageRevision", "loot") == revision, "Duplicate bag observations do not inflate records, trophies, icons or refresh revisions");
        List(bag, "ItemIds").Add(21228); List(bag, "ItemRarities").Add("Rare"); Journal("RecordLoot", bag);
        snapshot = Journal("Snapshot"); records = List(snapshot, "Loot").Cast<object>().ToArray();
        Check(records.Length == 3 && Discoveries(snapshot) == 3 && Items(Groups(records).Single()).Length == 3, "A later newly observed slot joins its original bag without recounting earlier slots");
        object secondMap = Bag("abcdef1234567890abcdef1234567890", mapTime.AddMinutes(5), 14471); Set(secondMap, "BagEntityId", Get(bag, "BagEntityId")); Journal("RecordLoot", secondMap);
        snapshot = Journal("Snapshot"); Check(List(snapshot, "Loot").Count == 4 && Groups(List(snapshot, "Loot").Cast<object>().ToArray()).Length == 2, "A reused entity ID in another map produces a separate recorded bag");
        object stale = Bag("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", mapTime, 3813); Set(stale, "JournalScopeId", "previous-account"); Journal("RecordLoot", stale);
        Check(List(Journal("Snapshot"), "Loot").Count == 4, "Bag grouping preserves rejection of stale-account observations");
        object copy = Journal("SnapshotForPage", "loot"); Set(List(copy, "Loot")[0], "BagId", "changed copy"); Set(List(copy, "Loot")[0], "BagSlot", 7);
        Check((string)Get(List(Journal("Snapshot"), "Loot")[0], "BagId") == identity && (int)Get(List(Journal("Snapshot"), "Loot")[0], "BagSlot") == 0, "Page snapshots clone bag identity and slot without sharing mutable records");
        string[] beforeIds = Groups(List(Journal("Snapshot"), "Loot").Cast<object>().ToArray()).Select(group => (string)Get(group, "Id")).ToArray();
        Journal("Flush"); Journal("BeginConnection"); Journal("UseAccount", "LootGroupingFixture"); Journal("WaitForLoads");
        snapshot = Journal("Snapshot"); records = List(snapshot, "Loot").Cast<object>().ToArray();
        Check(records.Length == 4 && Discoveries(snapshot) == 4 && Groups(records).Select(group => (string)Get(group, "Id")).SequenceEqual(beforeIds), "Disk reload preserves grouped rows, stable keys and lifetime totals");
        Set(bag, "JournalScopeId", Get(T("HudJournal"), "ScopeId")); Journal("RecordLoot", bag);
        Check(List(Journal("Snapshot"), "Loot").Count == 4 && Discoveries(Journal("Snapshot")) == 4, "The persisted per-item ledger still suppresses replay after reconnect");
        bool recording = (bool)Get(Get(T("HudPreferences"), "Current"), "RecordLoot"); Set(Get(T("HudPreferences"), "Current"), "RecordLoot", false);
        Journal("RecordLoot", Bag("dddddddddddddddddddddddddddddddd", mapTime, 3813));
        Check(List(Journal("Snapshot"), "Loot").Count == 4, "Grouped display remains independent of the recording toggle");
        Set(Get(T("HudPreferences"), "Current"), "RecordLoot", recording);
    }
    static void StopTimers(Control control)
    {
        for (Type current = control.GetType(); current != null; current = current.BaseType)
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (field.FieldType == typeof(System.Windows.Forms.Timer)) { var timer = field.GetValue(control) as System.Windows.Forms.Timer; if (timer != null) timer.Stop(); }
        foreach (Control child in control.Controls) StopTimers(child);
    }
    static void Capture(Form form, string path)
    { Application.DoEvents(); using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path, ImageFormat.Png); } }
    static void LegacyDashboardReproduction()
    {
        Journal("BeginConnection"); object data = New("HudJournalData");
        for (int slot = 0; slot < 2; slot++)
        {
            // This fixture deliberately uses only fields from the old journal,
            // so it reproduces the visible defect on the previous executable.
            object record = New("HudLootRecord"); Set(record, "Id", "1234567890abcdef1234567890abcdef:" + slot); Set(record, "ItemId", 17842);
            Set(record, "Item", "Legacy same-bag fixture"); Set(record, "Rarity", "Legendary"); Set(record, "Time", when); Set(record, "Bag", "White Bag");
            Set(record, "Enemy", "Stone Guardian"); Set(record, "Dungeon", "Lost Halls"); Set(record, "Character", "Fixture Knight"); List(data, "Loot").Add(record);
        }
        Set(T("HudJournal"), "data", data); object preferences = Get(T("HudPreferences"), "Current"); Set(preferences, "Page", "loot");
        ((IDictionary)Get(preferences, "TableCategories"))["loot"] = "All categories"; ((IDictionary)Get(preferences, "TablePeriods"))["loot"] = "All dates";
        using (var form = (Form)New("ExaltDashboardForm"))
        {
            try
            {
                StopTimers(form); Call(form, "SelectPage", "loot"); StopTimers(form);
                var grid = (DataGridView)Get(((IDictionary)Get(form, "tables"))["loot"], "Grid");
                Check(grid.RowCount == 1, "Two persisted GUIDN:slot records from one bag render as one Loot history row [actual=" + grid.RowCount + "]");
                Check(List(Journal("SnapshotForPage", "loot"), "Loot").Count == 2, "The grouped row retains both original persisted item records");
            }
            finally { Set(T("HudJournal"), "dirty", false); Call(form, "Shutdown"); }
        }
    }
    static void DashboardAndDetails(string root)
    {
        Journal("BeginConnection"); object data = New("HudJournalData"); int[] ids = { 17842, 17842, 21228, 14471, 3813, 6657, 6661, 7397 };
        for (int slot = 0; slot < ids.Length; slot++) List(data, "Loot").Add(Record("large-bag:" + slot, "ui:map:large-bag", slot, ids[slot], slot == 1 ? "VIT +5" : "ATT +2", slot == 1));
        List(data, "Loot").Add(Record("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:0", itemId: 2650)); List(data, "Loot").Add(Record("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:1", itemId: 2592));
        List(data, "Loot").Add(Record("unknown-old:0", itemId: 3813)); List(data, "Loot").Add(Record("unknown-old:1", itemId: 14471));
        Set(T("HudJournal"), "data", data); object preferences = Get(T("HudPreferences"), "Current"); Set(preferences, "Page", "loot");
        ((IDictionary)Get(preferences, "TableCategories"))["loot"] = "Shiny"; ((IDictionary)Get(preferences, "TablePeriods"))["loot"] = "All dates";
        using (var form = (Form)New("ExaltDashboardForm"))
        {
            StopTimers(form); form.TopMost = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-1800, -1100); form.ShowInTaskbar = false; form.Show(); StopTimers(form); Application.DoEvents();
            Call(form, "SelectPage", "loot"); StopTimers(form); object table = ((IDictionary)Get(form, "tables"))["loot"];
            var grid = (DataGridView)Get(table, "Grid"); var category = (ComboBox)Get(table, "Category"); var search = (TextBox)Get(table, "SearchBox"); var period = (ComboBox)Get(table, "Period");
            Check(grid.RowCount == 1 && (string)category.SelectedItem == "Shiny" && Items(grid.Rows[0].Tag).Length == 8, "A saved Shiny filter retains a mixed bag with every recorded item");
            category.SelectedItem = "White Bag"; Check(grid.RowCount == 4, "The bag-type filter includes mixed shiny bags and safely separated legacy records");
            search.Text = "VIT +5"; Check(grid.RowCount == 1 && Items(grid.Rows[0].Tag).Length == 8, "Searching one item's enchantment finds the whole bag without discarding its other items");
            search.Text = (string)Get(List(data, "Loot")[6], "Item"); Check(grid.RowCount == 1, "An item beyond the first icon remains searchable by name");
            search.Text = "Divine"; Check(grid.RowCount == 1, "A later item's recorded rarity remains searchable");
            search.Clear(); category.SelectedItem = "Shiny"; period.SelectedItem = "Today";
            Check(grid.RowCount == 1, "Bag grouping composes date and category filters");
            var original = grid.Rows[0]; string rowId = (string)original.HeaderCell.Tag; Call(form, "RefreshPageData");
            Check(grid.RowCount == 1 && ReferenceEquals(original, grid.Rows[0]) && (string)grid.Rows[0].HeaderCell.Tag == rowId, "Unchanged grouped history reuses native rows and stable hover targets");
            object group = grid.Rows[0].Tag; object[] views = Values(Call(T("ExaltDashboardForm"), "LootBagItems", group));
            Check(views.Length == 8 && views.All(view => Get(view, "Icon") != null), "A full eight-slot bag supplies every actual icon to the production grid");
            using (var detail = (Control)Call(T("ExaltDashboardForm"), "CreateLootBagDetails", group))
            {
                var detailGrid = (DataGridView)Get(detail, "Grid"); Check(detailGrid.RowCount == 8, "Opening a grouped bag exposes every item as a separate details entry");
                Check((string)Get(detailGrid.Rows[0].Tag, "Enchantments") == "ATT +2" && (string)Get(detailGrid.Rows[1].Tag, "Enchantments") == "VIT +5", "Bag details preserve each duplicate item's enchantments and slot order");
                using (var detailHost = new Form { ClientSize = new Size(920, 500), StartPosition = FormStartPosition.Manual, Location = new Point(-1800, -1100), ShowInTaskbar = false })
                { detailHost.Controls.Add(detail); detailHost.Show(); Capture(detailHost, Path.Combine(root, "grouped-loot-details.png")); detailHost.Close(); }
            }
            period.SelectedIndex = 0; category.SelectedIndex = 0; search.Clear(); form.Size = new Size(1280, 820); Application.DoEvents();
            Check(grid.RowCount == 4 && grid.Rows.Cast<DataGridViewRow>().Sum(row => Items(row.Tag).Length) == 12, "Grouping changes only visual row count, retaining all twelve item instances");
            Capture(form, Path.Combine(root, "grouped-loot-normal.png")); form.Size = form.MinimumSize; Application.DoEvents();
            Capture(form, Path.Combine(root, "grouped-loot-minimum.png"));
            Check(grid.RowCount == 4 && grid.Columns["Item"].Width >= 240, "Minimum window width retains a usable multi-item column and every bag row");
            Set(T("HudJournal"), "dirty", false); Call(form, "Shutdown");
        }
    }
    public void Run(string path, string root, bool legacyOnly)
    {
        app = Assembly.LoadFrom(path); Environment.CurrentDirectory = Path.GetDirectoryName(path);
        object preferences = Get(T("HudPreferences"), "Current"); Set(preferences, "SoundAlerts", false); Set(preferences, "RecordLoot", true); Set(preferences, "RecordCombat", false);
        Set(Get(preferences, "Callouts"), "AutoCopyDungeons", false); Set(T("AccountWorkspace"), "accessToken", ""); Set(T("AccountWorkspace"), "activeClient", null);
        var gate = (SemaphoreSlim)Get(T("AccountWorkspace"), "RefreshGate"); if (!gate.Wait(0)) throw new InvalidOperationException("Could not isolate account refresh");
        ((System.Threading.Timer)Get(T("AccountWorkspace"), "saveTimer")).Change(Timeout.Infinite, Timeout.Infinite); ((System.Threading.Timer)Get(T("HudJournal"), "saveTimer")).Change(Timeout.Infinite, Timeout.Infinite);
        try
        {
            Call(T("Proxy.DataStructures.GameData"), "LoadGameData"); ((Task)Call(T("HudItemPresentation"), "WarmUpAsync")).GetAwaiter().GetResult();
            LegacyDashboardReproduction();
            if (!legacyOnly) { IdentityAndInstances(); LegacyHistory(); RecordingAndReload(); DashboardAndDetails(root); }
        }
        finally
        {
            Set(T("HudJournal"), "dirty", false); Set(T("AccountWorkspace"), "dirty", false); gate.Release();
            File.WriteAllLines(Path.Combine(root, "review.txt"), results);
        }
        Console.WriteLine("Loot bag grouping: " + checks + " checks passed.");
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($harness))
$setup = New-Object AppDomainSetup
$setup.ApplicationBase = Split-Path $assembly -Parent
$domain = [AppDomain]::CreateDomain('Loot bag grouping regression', $null, $setup)
try {
    $runner = $domain.CreateInstanceFromAndUnwrap($harness, 'LootBagGroupingRegression')
    $runner.Run($assembly, $testRoot, [bool]$LegacyReproductionOnly)
} finally {
    [AppDomain]::Unload($domain)
    $env:EXALTHELPER_HUD_DATA = $priorData
    @{ Assembly=$assembly; SHA256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash; CreatedUtc=[DateTime]::UtcNow.ToString('o'); Results=$testRoot } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'manifest.json') -Encoding UTF8
    Write-Output "Loot grouping results: $testRoot"
}
