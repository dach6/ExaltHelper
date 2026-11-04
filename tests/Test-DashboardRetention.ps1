param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/dashboard-retention"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = $testRoot
Add-Type -ReferencedAssemblies System.Core, System.Drawing -TypeDefinition @'
using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

public static class DashboardRetentionRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object o, string name) { var f = o.GetType().GetField(name, F); return f != null ? f.GetValue(o) : o.GetType().GetProperty(name, F).GetValue(o, null); }
    static void Set(object o, string name, object value) { var f = o.GetType().GetField(name, F); if (f != null) f.SetValue(o, value); else o.GetType().GetProperty(name, F).SetValue(o, value, null); }
    static object Static(string type, string method, params object[] args) { return T(type).GetMethod(method, F).Invoke(null, args); }
    static object Call(object o, string method, params object[] args) { return o.GetType().GetMethod(method, F).Invoke(o, args); }
    static object Journal(string name, params object[] args) { object result = Static("HudJournal", name, args); if (name == "UseAccount") Static("HudJournal", "WaitForLoads"); return result; }
    static IList Rows(string name) { return (IList)Get(Journal("Snapshot"), name); }
    static long Trophy(int id) { object row = Rows("Trophies").Cast<object>().FirstOrDefault(x => (int)Get(x, "ItemId") == id); return row == null ? 0 : (long)Get(row, "Count"); }
    static long Discoveries() { return Rows("Trophies").Cast<object>().Sum(x => (long)Get(x, "Count")); }
    static long Runs(string dungeon) { var runs = (IDictionary)Get(Journal("Snapshot"), "DungeonRuns"); return runs.Contains(dungeon) ? (long)runs[dungeon] : 0; }
    static string[] Ids(string name) { return Rows(name).Cast<object>().Select(x => (string)Get(x, "Id")).ToArray(); }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static object Snapshot(string dungeon, DateTime start, string character)
    {
        object snapshot = New("Proxy.DataStructures.DpsSnapshot"); Set(snapshot, "DungeonName", dungeon); Set(snapshot, "DungeonStartTime", start);
        Set(Get(snapshot, "LocalPlayer"), "Name", character); Set(Get(snapshot, "LocalPlayer"), "ClassName", "Wizard"); return snapshot;
    }
    static object Bag(int entityId, params int[] items)
    {
        object bag = New("Proxy.DataStructures.LootBagDrop"); Set(bag, "BagEntityId", entityId);
        foreach (int item in items) ((IList)Get(bag, "ItemIds")).Add(item);
        return bag;
    }
    static void MapsAndAccounts()
    {
        Journal("UseAccount", "LootAccountA"); DateTime start = DateTime.Now.AddMinutes(-20);
        object first = Snapshot("The Nest", start, "FirstHero"); object bag = Bag(77, 17842, 17842);
        ((IList)Get(first, "LootBags")).Add(bag); Journal("ObserveSnapshot", first);
        Check(Rows("Loot").Count == 2 && Trophy(17842) == 2, "Identical item slots were collapsed in loot or collections");
        object second = Snapshot("Nexus", start.AddMinutes(10), "SecondHero"); ((IList)Get(second, "LootBags")).Add(bag);
        for (int i = 0; i < 3; i++) Journal("ObserveSnapshot", second);
        Check(Rows("Loot").Count == 2 && Trophy(17842) == 2, "Retained session bag was recorded again after a map change");
        Check(Rows("Loot").Cast<object>().All(x => (string)Get(x, "Dungeon") == "The Nest" && ((string)Get(x, "Character")).StartsWith("FirstHero")),
            "Retained bag was attributed to the new dungeon or character");
        Check((DateTime)Get(bag, "DungeonStartTime") == start, "Capture provenance changed after entering another map");
        ((IList)Get(second, "LootBags")).Add(Bag(77, 17842)); Journal("ObserveSnapshot", second);
        Check(Rows("Loot").Count == 3 && Trophy(17842) == 3, "Reused entity id suppressed a distinct bag in a later map");
        Journal("UseAccount", "LootAccountB"); Journal("ObserveSnapshot", second);
        Check(Rows("Loot").Count == 0 && Discoveries() == 0, "Previous-account session bags entered another account's collections");
        ((IList)Get(second, "LootBags")).Add(Bag(77, 2650)); Journal("ObserveSnapshot", second); Journal("Flush");
        Check(Rows("Loot").Count == 1 && Trophy(2650) == 1, "New account could not record its own reused bag id");
        Journal("UseAccount", "LootAccountA"); Check(Trophy(17842) == 3 && Trophy(2650) == 0, "Durable collection aggregates crossed account boundaries");
    }
    static void Retention()
    {
        Journal("UseAccount", "RetentionAccount"); object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        Set(preferences, "HistoryLimit", 100);
        object snapshot = Snapshot("The Nest", DateTime.Now.AddHours(-2), "RetentionHero");
        var bags = (IList)Get(snapshot, "LootBags"); bags.Add(Bag(1000, 17842));
        for (int i = 1; i <= 100; i++) bags.Add(Bag(1000 + i, 2650));
        var enemies = (IList)Get(snapshot, "DefeatedEnemies");
        for (int i = 0; i < 101; i++)
        {
            object enemy = New("Proxy.DataStructures.EnemyCombatSnapshot"); Set(enemy, "EntityId", 2000 + i); Set(enemy, "Name", "Fixture boss " + i);
            Set(enemy, "TotalDamage", 10L); Set(enemy, "IsDead", true); enemies.Add(enemy);
        }
        Journal("ObserveSnapshot", snapshot); string[] loot = Ids("Loot"), fights = Ids("Fights");
        Check(loot.Length == 100 && fights.Length == 100, "History retention did not cap loot and encounters");
        Check(!Rows("Loot").Cast<object>().Any(x => (int)Get(x, "ItemId") == 17842) && Trophy(17842) == 1 && Discoveries() == 101,
            "Trimming visible loot erased lifetime collection progress");
        for (int i = 0; i < 8; i++) Journal("ObserveSnapshot", snapshot);
        Check(Ids("Loot").SequenceEqual(loot) && Discoveries() == 101, "Repeated snapshots replayed trimmed loot or inflated collections");
        Check(Ids("Fights").SequenceEqual(fights) && Runs("The Nest") == 1, "Repeated snapshots replayed trimmed encounters or inflated run counts");
        Journal("Flush"); Journal("UseAccount", "TemporaryAccount"); Journal("UseAccount", "RetentionAccount");
        Check(Trophy(17842) == 1 && Discoveries() == 101 && Runs("The Nest") == 1, "History retention aggregates did not survive account reload");
        string scope = (string)T("HudJournal").GetProperty("ScopeId", F).GetValue(null, null);
        foreach (object bag in bags) Set(bag, "JournalScopeId", scope);
        Journal("ObserveSnapshot", snapshot);
        Check(Ids("Loot").SequenceEqual(loot) && Ids("Fights").SequenceEqual(fights) && Discoveries() == 101,
            "Persisted deduplication ledger lost trimmed records on reload");
        Journal("Flush"); Journal("BeginConnection");
        object newestBag = Bag(9000, 4242);
        object storageGate = T("HudStorage").GetField("Gate", F).GetValue(null); Task replay = null;
        Monitor.Enter(storageGate);
        try
        {
            replay = Task.Run(() => {
                Static("HudJournal", "UseAccount", "RetentionAccount");
                string pendingScope = (string)T("HudJournal").GetProperty("ScopeId", F).GetValue(null, null);
                foreach (object bag in bags) Set(bag, "JournalScopeId", pendingScope);
                for (int i = 0; i < 3; i++) Journal("ObserveSnapshot", snapshot);
                bags.Add(newestBag); Journal("ObserveSnapshot", snapshot);
            });
            Check(replay.Wait(1000), "Cold-load retention replay blocked packet observation on disk I/O");
        }
        finally { Monitor.Exit(storageGate); if (replay != null) replay.Wait(5000); }
        Static("HudJournal", "WaitForLoads");
        Check(Rows("Loot").Count == 100 && Ids("Fights").OrderBy(x => x).SequenceEqual(fights.OrderBy(x => x)) && Discoveries() == 102 && Runs("The Nest") == 1,
            "Cold-load replay revived trimmed rows or recounted lifetime collections/runs");
        Check(Rows("Loot").Cast<object>().Any(x => (int)Get(x, "ItemId") == 4242) && Trophy(17842) == 1,
            "UTC cached timestamps sorted a genuinely new local-time drop out of retained history");
        object nextRun = Snapshot("The Nest", DateTime.Now, "RetentionHero");
        object nextEnemy = New("Proxy.DataStructures.EnemyCombatSnapshot"); Set(nextEnemy, "EntityId", 2000); Set(nextEnemy, "Name", "New run boss");
        Set(nextEnemy, "TotalDamage", 1L); Set(nextEnemy, "IsDead", true); ((IList)Get(nextRun, "DefeatedEnemies")).Add(nextEnemy);
        Journal("ObserveSnapshot", nextRun); Check(Runs("The Nest") == 2, "Entity reuse in a new dungeon suppressed a legitimate run");
    }
    static void BagTypes()
    {
        int[] ids = { 1280, 1283, 1286, 1287, 1288, 1289, 1291, 1292, 1294, 1295, 1708, 1296, 1709, 1710, 1722, 1723, 1724, 1725, 1726, 1727, 1728, 4358 };
        string[] names = { "Brown Bag", "Soulbound Bag", "Pink Bag", "Purple Bag", "Egg Basket", "Cyan Bag", "Blue Bag", "White Bag", "Gold Bag", "Orange Bag", "Red Bag", "White Bag", "Brown Bag", "Pink Bag", "Purple Bag", "Egg Basket", "Gold Bag", "Cyan Bag", "Blue Bag", "Orange Bag", "Red Bag", "Cyan Bag" };
        for (int i = 0; i < ids.Length; i++)
        {
            object[] args = { (ushort)ids[i], "", Color.Empty };
            Check((bool)Static("Proxy.Mods.DpsTrackerMod", "IsQualifyingLootBag", args) && (string)args[1] == names[i], "Wrong or missing loot bag mapping: " + ids[i]);
        }
        object[] unknown = { (ushort)9999, "", Color.Empty };
        Check(!(bool)Static("Proxy.Mods.DpsTrackerMod", "IsQualifyingLootBag", unknown), "Non-bag object was classified as loot");
    }
    static object Stat(int id, int value, string text = null)
    {
        object stat = New("Proxy.Networking.Packets.DataObjects.StatData");
        Type type = T("Proxy.Networking.Packets.DataObjects.StatType");
        object kind = type.GetMethods(F).Single(m => m.Name == "op_Implicit" && m.ReturnType == type && m.GetParameters()[0].ParameterType == typeof(int)).Invoke(null, new object[] { id });
        Set(stat, "Type", kind); Set(stat, "Value", value); if (text != null) Set(stat, "StringValue", text); return stat;
    }
    static object ObjectBag(int id, ushort type, string enchant = null, bool mixed = false)
    {
        object obj = New("Proxy.Networking.Packets.DataObjects.ObjectData"); Set(obj, "ObjectType", type);
        object status = Get(obj, "Stats"); Set(status, "ObjectId", id);
        var stats = (IList)Activator.CreateInstance(status.GetType().GetField("StatList", F).FieldType);
        stats.Add(Stat(8, 2580)); if (mixed) stats.Add(Stat(9, 2580)); if (enchant != null) stats.Add(Stat(80, 0, enchant)); Set(status, "StatList", stats); return obj;
    }
    static void TrackerCapture()
    {
        Journal("UseAccount", "CaptureAccount");
        var items = T("Proxy.DataStructures.GameData").GetField("Items", F);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType) }));
        object client = FormatterServices.GetUninitializedObject(T("Proxy.Client"));
        Set(client, "_remoteClientLock", new object()); Set(client, "_localClientLock", new object());
        foreach (string field in new[] { "Enemies", "Entities", "Portals" }) Set(client, field, Activator.CreateInstance(client.GetType().GetField(field, F).FieldType));
        object player = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { 100 });
        Set(player, "PlayerName", "CaptureHero"); Set(player, "ObjectType", (ushort)768); Set(player, "Inventory", new[] { -1, -1, -1, -1 });
        Set(client, "Player", player); Set(client, "ClientId", 100);
        object tracker = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod"), new object[] { client }); Set(client, "dpsTracker", tracker);
        Set(tracker, "currentDungeonName", "The Nest");
        Check(!(bool)Get(Static("ItemSpriteManager", "GetItemMetadata", 2580), "IsShiny"), "Conditional-bag fixture must use a non-shiny item");
        Call(tracker, "ProcessLootBag", ObjectBag(77, 1726));
        Check(Rows("Loot").Count == 1 && (string)Get(Rows("Loot")[0], "Dungeon") == "The Nest", "Real boosted-blue bag capture did not record original provenance");
        object map = New("Proxy.Networking.Packets.MapInfoPacket"); Set(map, "MapName", "Nexus"); Set(map, "DisplayName", "Nexus");
        Set(player, "PlayerName", "LaterHero"); Call(tracker, "OnMapChange", map);
        Check(Rows("Loot").Count == 1 && ((string)Get(Rows("Loot")[0], "Character")).StartsWith("CaptureHero"), "Real map transition duplicated or relabeled captured loot");
        Call(tracker, "ProcessLootBag", ObjectBag(77, 1725));
        Check(Rows("Loot").Count == 2 && Trophy(2580) == 2, "Boosted-teal bag or reused entity id in a new map was lost");
        Call(tracker, "ProcessLootBag", ObjectBag(78, 1280));
        Check(Rows("Loot").Count == 2, "Ordinary common contents unexpectedly entered conditional public-bag history");
        object[] feedBags = Rows("Feed").Cast<object>().Where(x => Get(x, "FeedLoot") != null).ToArray();
        Check(feedBags.Length == 3 && ((int[])Get(Get(feedBags.Last(), "FeedLoot"), "ItemIds")).SequenceEqual(new[] { 2580 }),
            "Real tracker discarded a public bag from the independent feed before history filtering");
        string enchant = Convert.ToBase64String(new byte[] { 0, 2, 4, 0x52, 1, 253, 0 }).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Call(tracker, "ProcessLootBag", ObjectBag(79, 1280, enchant, true));
        Check(Rows("Loot").Count == 4 && Trophy(2580) == 2 && Rows("Loot").Cast<object>().Skip(2).All(x => (string)Get(x, "Enemy") == "Public / player bag"),
            "Qualifying mixed public bag lost an ordinary slot or counted as an earned boss trophy");
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        bool recording = (bool)Get(preferences, "RecordLoot"); Set(preferences, "RecordLoot", false);
        try
        {
            Call(tracker, "ProcessLootBag", ObjectBag(80, 1726));
            Check(Rows("Feed").Cast<object>().Count(x => Get(x, "FeedLoot") != null) == 5 && Rows("Loot").Count == 4,
                "Real tracker coupled feed capture to the history recording switch");
            Call(tracker, "ProcessLootBag", ObjectBag(80, 1726));
            Check(Rows("Feed").Cast<object>().Count(x => Get(x, "FeedLoot") != null) == 5,
                "Repeated object observation duplicated a feed bag while history recording was disabled");
        }
        finally { Set(preferences, "RecordLoot", recording); }
    }
    static object PolicyBag(int item, int enchants = 0, params string[] names)
    {
        object bag = Bag(1, item); Set(bag, "BagType", "Brown Bag");
        ((IList)Get(bag, "ItemEnchantCounts")).Add(enchants);
        ((IList)Get(bag, "ItemEnchants")).Add(new System.Collections.Generic.List<string>(names)); return bag;
    }
    static bool Track(object bag) { return (bool)Static("HudLootPolicy", "ShouldTrack", bag); }
    static void LootPolicy()
    {
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null), original = Get(preferences, "Loot"), policy = New("HudLootPreferences");
        foreach (string name in new[] { "Shiny", "AnyEnchantment", "LootEnchantments", "UniqueEnchantments", "AwakenedEnchantments", "LegendaryPlus", "Untiered", "SetTier", "Potions", "Marks", "Tiered" }) Set(policy, name, false);
        ((IList)Get(policy, "AlwaysTrackBags")).Clear(); Set(preferences, "Loot", policy);
        try
        {
            object ordinary = PolicyBag(2580);
            Check(!Track(ordinary), "Disabled bag/content filters recorded an ordinary T0 dagger");
            ((IList)Get(policy, "AlwaysTrackBags")).Add("Brown Bag"); Check(Track(ordinary), "Selected bag type failed to admit ordinary contents"); ((IList)Get(policy, "AlwaysTrackBags")).Clear();
            foreach (var test in new[] { Tuple.Create("Shiny", 17842), Tuple.Create("Untiered", 2650), Tuple.Create("Potions", 2591), Tuple.Create("Marks", 6139) })
            {
                Set(policy, test.Item1, true); Check(Track(PolicyBag(test.Item2)) && !Track(ordinary), test.Item1 + " content filter did not distinguish its real item fixture from common gear"); Set(policy, test.Item1, false);
            }
            Set(policy, "Tiered", true); Set(policy, "MinimumTier", 1);
            Check(Track(PolicyBag(2581)) && !Track(ordinary), "Tier threshold did not distinguish a T1 Dirk from a T0 Steel Dagger");
            Set(policy, "MinimumTier", 2); Check(!Track(PolicyBag(2581)), "Tier threshold included equipment below the selected tier"); Set(policy, "Tiered", false);
            Set(policy, "AnyEnchantment", true); Check(Track(PolicyBag(2580, 1, "Attack Bonus I")) && !Track(ordinary), "Any-enchantment option ignored an ordinary stat enchant"); Set(policy, "AnyEnchantment", false);
            Set(policy, "LegendaryPlus", true); Check(!Track(PolicyBag(2580, 2)) && Track(PolicyBag(2580, 3)), "Legendary-or-higher option did not enforce its enchant-slot threshold"); Set(policy, "LegendaryPlus", false);
            Set(policy, "LootEnchantments", true);
            Check(Track(PolicyBag(2580, 1, "Loot Bonus III")) && !Track(PolicyBag(2580, 1, "Loot Bonus II")) && !Track(PolicyBag(2580, 1, "XP Loot Bonus III")), "High-loot enchant selection included lower loot or XP bonuses"); Set(policy, "LootEnchantments", false);
            Set(policy, "UniqueEnchantments", true); Check(Track(PolicyBag(2580, 1, "Stheno's Swiftness")) && !Track(PolicyBag(2580, 1, "Attack Bonus I")), "Unique-enchantment metadata did not recognize its internal uppercase ID"); Set(policy, "UniqueEnchantments", false);
            Set(policy, "AwakenedEnchantments", true); Check(Track(PolicyBag(2580, 1, "Awakened Strike")) && !Track(PolicyBag(2580, 1, "Attack Bonus I")), "Awakened-enchantment filter included a normal enchant");
            ((IList)Get(policy, "AlwaysTrackBags")).Add("Purple Bag"); Set(policy, "MinimumTier", 12); Set(policy, "Tiered", true); Call(preferences, "Save");
            object loaded = T("HudStorage").GetMethod("Load", F).MakeGenericMethod(preferences.GetType()).Invoke(null, new object[] { "layout.json", New("HudPreferences") }); object saved = Get(loaded, "Loot");
            Check(((IList)Get(saved, "AlwaysTrackBags")).Contains("Purple Bag") && (int)Get(saved, "MinimumTier") == 12 && (bool)Get(saved, "AwakenedEnchantments") && !(bool)Get(saved, "AnyEnchantment"), "Bag/content preferences did not survive disk serialization");
        }
        finally { Set(preferences, "Loot", original); Call(preferences, "Save"); }
    }
    static void DungeonGroups()
    {
        var fights = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(T("HudFightRecord")));
        for (int i = 0; i < 3; i++)
        {
            object fight = New("HudFightRecord"); Set(fight, "Id", (i < 2 ? "runA:" : "runB:") + i); Set(fight, "Dungeon", "The Nest");
            Set(fight, "Time", DateTime.Today.AddSeconds(i * 10)); Set(fight, "Seconds", 5.0); Set(fight, "DungeonSeconds", i == 0 ? 20.0 : 30.0);
            Set(fight, "Damage", i == 0 ? 100L : i == 1 ? 200L : 50L); Set(fight, "Equipment", new[] { 1, 2, 3, 4 });
            object hero = New("Proxy.DataStructures.PlayerDamageEntry"); Set(hero, "Name", "Hero"); Set(hero, "ClassName", "Wizard");
            Set(hero, "Damage", i == 0 ? 100L : i == 1 ? 150L : 50L); Set(hero, "GuardHits", i == 0 ? 2 : 3); Set(hero, "GuardDamage", i == 0 ? 10L : 15L);
            Set(hero, "IsLocalPlayer", i == 0); ((IList)Get(fight, "Players")).Add(hero);
            ((IDictionary)Get(fight, "DamageTaken"))["Hero"] = i == 0 ? 7L : 13L;
            if (i == 1) { object ally = New("Proxy.DataStructures.PlayerDamageEntry"); Set(ally, "Name", "Ally"); Set(ally, "Damage", 50L); ((IList)Get(fight, "Players")).Add(ally); }
            fights.Add(fight);
        }
        object[] groups = ((IEnumerable)Static("ExaltDashboardForm", "DungeonHistory", fights)).Cast<object>().ToArray();
        Check(groups.Length == 2, "Dungeon history merged separate runs of the same dungeon");
        object run = groups.Single(x => (string)Get(x, "Id") == "run:runA");
        Check((long)Get(run, "Damage") == 300 && (double)Get(run, "Seconds") == 30 && (long)Get(run, "Damage") / (double)Get(run, "Seconds") == 10,
            "Dungeon DPS used summed fight durations instead of observed run duration");
        object heroTotal = ((IList)Get(run, "Players")).Cast<object>().Single(x => (string)Get(x, "Name") == "Hero");
        Check((long)Get(heroTotal, "Damage") == 250 && Math.Abs((double)Get(heroTotal, "Dps") - 250.0 / 30) < .00001 && (bool)Get(heroTotal, "IsLocalPlayer"),
            "Dungeon player totals, DPS or local identity did not combine overlapping players");
        Check((int)Get(heroTotal, "GuardHits") == 5 && (long)Get(heroTotal, "GuardDamage") == 25 && (long)((IDictionary)Get(run, "DamageTaken"))["Hero"] == 20,
            "Dungeon guard or damage-taken totals did not sum encounters");
        ((int[])Get(run, "Equipment"))[0] = 99;
        Check(((int[])Get(fights[0], "Equipment"))[0] == 1, "Dungeon detail view could mutate the original encounter equipment");
    }
    public static void Run(string path)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        MapsAndAccounts(); Retention(); BagTypes(); TrackerCapture(); LootPolicy(); DungeonGroups(); Journal("Flush");
        Console.WriteLine("PASS: " + checks + " loot provenance, retention, collection, run and bag-type assertions.");
    }
}
'@
try { [DashboardRetentionRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
