param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/dashboard-feed"
)
$ErrorActionPreference = 'Stop'
if ((New-Object IO.DriveInfo 'C:\').AvailableFreeSpace -le 5GB) { throw 'Disk-space stop threshold reached.' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$env:EXALTHELPER_HUD_DATA = $testRoot
Add-Type -ReferencedAssemblies System.Core,System.Xml,System.Xml.Linq -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;

public static class DashboardFeedRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static object client;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object obj, string name) { var f = obj.GetType().GetField(name, F); return f != null ? f.GetValue(obj) : obj.GetType().GetProperty(name, F).GetValue(obj, null); }
    static void Set(object obj, string name, object value) { var f = obj.GetType().GetField(name, F); if (f != null) f.SetValue(obj, value); else obj.GetType().GetProperty(name, F).SetValue(obj, value, null); }
    static object Static(string type, string name, params object[] args) { return T(type).GetMethod(name, F).Invoke(null, args); }
    static object Journal(string name, params object[] args) { object result = Static("HudJournal", name, args); if (name == "UseAccount") Static("HudJournal", "WaitForLoads"); return result; }
    static IList Rows(string kind) { return (IList)Get(Journal("Snapshot"), kind); }
    static object Preferences { get { return T("HudPreferences").GetProperty("Current", F).GetValue(null, null); } }
    static string Scope { get { return (string)T("HudJournal").GetProperty("ScopeId", F).GetValue(null, null); } }
    static object Current { get { return Journal("CurrentLocation"); } }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static object[] Feed(string category) { return Rows("Feed").Cast<object>().Where(x => (string)Get(x, "Category") == category).ToArray(); }
    static void Observe(object packet, bool outgoing = false) { Journal("ObservePacket", client, packet, outgoing); }
    static object Map(string name, uint seed, string realm = "", int current = -1, int maximum = -1, string modifiers = "")
    {
        object map = New("Proxy.Networking.Packets.MapInfoPacket");
        Set(map, "DisplayName", name); Set(map, "MapName", name); Set(map, "RealmName", realm); Set(map, "Fp", seed);
        Set(map, "CurrentRealmScore", current); Set(map, "MaxRealmScore", maximum); Set(map, "DungeonModifiers", modifiers); return map;
    }
    static object Stat(int id, string text, int value = 0)
    {
        object stat = New("Proxy.Networking.Packets.DataObjects.StatData");
        Type statType = stat.GetType().GetProperty("Type", F).PropertyType;
        object type = statType.GetMethods(F).First(x => x.Name == "op_Implicit" && x.ReturnType == statType && x.GetParameters()[0].ParameterType == typeof(byte)).Invoke(null, new object[] { (byte)id });
        Set(stat, "Type", type); Set(stat, "StringValue", text); Set(stat, "Value", value); return stat;
    }
    static object Status(int id, params object[] values)
    {
        object status = New("Proxy.Networking.Packets.DataObjects.ObjectStatsData"); Set(status, "ObjectId", id);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Proxy.Networking.Packets.DataObjects.StatData")));
        foreach (object stat in values) list.Add(stat); Set(status, "StatList", list); return status;
    }
    static void Tick(int player, bool seasonal, string amount, string cap)
    {
        var stats = new List<object> { Stat(24, null, seasonal ? 1 : 0) };
        if (amount != null) stats.Add(Stat(127, amount)); if (cap != null) stats.Add(Stat(147, cap));
        object tick = New("Proxy.Networking.Packets.NewTickPacket");
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Proxy.Networking.Packets.DataObjects.ObjectStatsData")));
        list.Add(Status(player, stats.ToArray())); Set(tick, "Statuses", list); Observe(tick);
    }
    static void Portal()
    {
        Type structure = T("Proxy.DataStructures.ObjectStructure");
        object portal = Activator.CreateInstance(structure, F, null, new object[] { XElement.Parse("<Object type='0x1234' id='Snake Pit Portal'><Class>Portal</Class></Object>") }, null);
        var entries = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(ushort), structure)); entries.Add((ushort)0x1234, portal);
        object lookup = Activator.CreateInstance(T("Proxy.DataStructures.LookupTable`2").MakeGenericType(typeof(ushort), structure), new object[] { entries });
        T("Proxy.DataStructures.GameData").GetField("Objects", F).SetValue(null, lookup);
        object obj = New("Proxy.Networking.Packets.DataObjects.ObjectData"); Set(obj, "ObjectType", (ushort)0x1234); Set(obj, "Stats", Status(99));
        Array objects = Array.CreateInstance(obj.GetType(), 1); objects.SetValue(obj, 0);
        object update = New("Proxy.Networking.Packets.UpdatePacket"); Set(update, "NewObjects", objects); Observe(update);
        var spawns = (IDictionary)T("HudJournal").GetField("portalSpawns", F).GetValue(null);
        Check(spawns.Contains(99), "Actual Update portal was not observed");
        DateTime first = DateTime.UtcNow.AddSeconds(-5); spawns[99] = first; Observe(update);
        Check((DateTime)spawns[99] == first, "Reobserving the same portal restarted its countdown");
        object use = New("Proxy.Networking.Packet"); Set(use, "Id", (byte)47); Set(use, "_rawPayload", new byte[] { 0, 0, 0, 99 });
        byte[] before = (byte[])T("Proxy.Networking.Packet").GetMethod("GetPayloadBytes", F).Invoke(use, null); Observe(use, true);
        Check(before.SequenceEqual((byte[])T("Proxy.Networking.Packet").GetMethod("GetPayloadBytes", F).Invoke(use, null)), "Portal observer changed outgoing bytes");
    }
    static void Locations()
    {
        Journal("UseAccount", "FeedLocations"); Observe(Map("Realm", 1, "Cyclops", 250, 1000)); Portal();
        object dungeon = Map("Snake Pit", 2, "Cyclops", -1, -1, "GENEROUS|S"); Observe(dungeon);
        object first = Current;
        Check((bool)Get(first, "IsDungeon") && !(bool)Get(first, "CalloutEstimated"), "Observed portal entry was not identified as anchored");
        Check((int)Get(first, "OriginRealmPercent") == 25 && !(bool)Get(first, "OpenedInNexus"), "Portal entry lost its origin realm score");
        double remaining = ((DateTime)Get(first, "AvailableUntilUtc") - DateTime.UtcNow).TotalSeconds;
        Check(remaining > 22 && remaining <= 25, "Portal deadline was not anchored30seconds from first sight");
        int entries = Feed("Travel").Length; Observe(dungeon);
        Check(Feed("Travel").Length == entries && (string)Get(Current, "LocationId") == (string)Get(first, "LocationId"), "Repeated MapInfo created another call or run");
        Observe(Map("Nexus", 3));
        object old = Feed("Travel").First(x => (string)Get(x, "LocationId") == (string)Get(first, "LocationId"));
        Check((DateTime)Get(old, "AvailableUntilUtc") <= DateTime.UtcNow, "Leaving a dungeon kept its join call available");
        Check((string)Get(old, "Location") == "Snake Pit" && (string)Get(old, "DungeonModifiers") == "GENEROUS|S" && (int)Get(old, "OriginRealmPercent") == 25, "Historical context was replaced with the latest map");
        Observe(Map("Snake Pit", 4, "Nexus", -1, -1, "GENEROUS"));
        Check((bool)Get(Current, "OpenedInNexus") && Get(Current, "OriginRealmPercent") == null && (bool)Get(Current, "CalloutEstimated"), "Hub entry inherited a realm percentage");
        Check(((string[])Get(Current, "LearnedEventModifiers")).Length == 0, "A duplicate MapInfo taught a false event modifier");
        Observe(Map("Snake Pit", 5, "Nexus", -1, -1, "GENEROUS"));
        Check(((string[])Get(Current, "LearnedEventModifiers")).SequenceEqual(new[] { "GENEROUS" }), "Three distinct repeated solo modifiers were not learned");
        ((string[])Get(Current, "LearnedEventModifiers"))[0] = "MUTATED";
        Check(((string[])Get(Current, "LearnedEventModifiers"))[0] == "GENEROUS", "Returned callout metadata shared mutable arrays");
        Journal("UseAccount", "FeedOtherAccount");
        Check(Get(Current, "LocationId") == null && Get(Current, "OriginRealmPercent") == null, "Account switch retained a callout origin");
    }
    static void Dust()
    {
        Journal("UseAccount", "FeedDust");
        Tick(42, false, "1:100", "1:100"); Check(Feed("Dust").Length == 0, "First full observation spammed a dust warning");
        Tick(42, false, "1:90", null); Tick(42, false, "1:100", null);
        Check(Feed("Dust").Length == 1 && ((string)Get(Feed("Dust")[0], "Text")).StartsWith("Regular Green"), "A real cap crossing was not reported");
        Tick(42, false, "1:100", null); Check(Feed("Dust").Length == 1, "Repeated cap value repeated a warning");
        Tick(77, false, "1:0", "1:100"); Tick(77, false, "1:100", null); Check(Feed("Dust").Length == 1, "Another player's stats changed dust notices");
        Tick(42, true, "1:10", "1:20"); Tick(42, true, "1:20", null);
        Check(Feed("Dust").Length == 2 && ((string)Get(Feed("Dust")[1], "Text")).StartsWith("Seasonal Green"), "Seasonal caps were mixed with regular caps");
        Tick(42, false, "1:100", null); Check(Feed("Dust").Length == 2, "Returning to regular mode retriggered its full warning");
        Tick(42, true, "4:9,5:3", "4:10,5:4"); Tick(42, true, "4:10,5:4", null);
        Check(Feed("Dust").Length == 4, "Simultaneous purple and red cap crossings were collapsed");
        Check(((string)Get(Feed("Dust")[2], "Text")).StartsWith("Seasonal Purple") && ((string)Get(Feed("Dust")[3], "Text")).StartsWith("Seasonal Red"), "Dust protocol IDs4and5 had incorrect color names");
        Journal("UseAccount", "FeedDustB"); Tick(42, true, "1:20", "1:20"); Check(Feed("Dust").Length == 0, "New account inherited dust transition state");
    }
    static void RealmClose()
    {
        Journal("UseAccount", "FeedRealmClose"); Observe(Map("Snake Pit", 7, "Cyclops"));
        Set(Preferences, "RecordChat", false);
        object announcement = New("Proxy.Networking.Packets.TextPacket"); Set(announcement, "Name", "Commander Calbrik"); Set(announcement, "Text", "Atmosphere breach imminent"); Observe(announcement);
        Check(Rows("Chat").Count == 0 && Feed("Event").Any(x => (string)Get(x, "CalloutKind") == "Event" && (string)Get(x, "CalloutName") == "Alien Invasion Adept - Wave 1"), "Recognized plain NPC event depended on chat recording or lost its event display category");
        int eventCount = Rows("Feed").Count; Set(announcement, "Name", "FixturePlayer"); Observe(announcement);
        Check(Rows("Feed").Count == eventCount, "A player's copy of an announcement created a realm event"); Set(Preferences, "RecordChat", true);
        object text = New("Proxy.Networking.Packets.TextPacket"); Set(text, "Text", "{\"key\":\"s.oryx_closed_realm\"}"); Set(text, "Name", "FixturePlayer");
        Observe(text);
        Check(T("HudJournal").GetProperty("RealmCastleUtc", F).GetValue(null, null) == null, "Player text spoofed the realm-close signal");
        Set(text, "Name", "#Oryx the Mad God"); Observe(text);
        DateTime deadline = (DateTime)T("HudJournal").GetProperty("RealmCastleUtc", F).GetValue(null, null);
        Check((deadline - DateTime.UtcNow).TotalSeconds > 118 && (deadline - DateTime.UtcNow).TotalSeconds <= 120, "Trusted realm-close message did not anchor the castle countdown");
        Check((DateTime)Get(Current, "AvailableUntilUtc") <= DateTime.UtcNow, "Realm-close message left a dungeon join call active");
        Observe(text); Check((DateTime)T("HudJournal").GetProperty("RealmCastleUtc", F).GetValue(null, null) == deadline, "Repeated realm-close signal restarted the timer");
        Observe(Map("Nexus", 8)); Check(T("HudJournal").GetProperty("RealmCastleUtc", F).GetValue(null, null) == null, "Castle countdown survived leaving the map");
    }
    static void LootAndKeys()
    {
        Journal("UseAccount", "FeedLoot"); Set(Preferences, "RecordLoot", false);
        object bag = New("Proxy.DataStructures.LootBagDrop"); Set(bag, "JournalScopeId", Scope); Set(bag, "BagType", "White Bag"); Set(bag, "Dungeon", "Snake Pit");
        ((IList)Get(bag, "ItemIds")).Add(2650); ((IList)Get(bag, "ItemIds")).Add(2592);
        ((IList)Get(bag, "ItemIsShiny")).Add(true); ((IList)Get(bag, "ItemIsShiny")).Add(false);
        Journal("RecordFeedLoot", bag); Journal("RecordFeedLoot", bag);
        Check(Feed("Loot").Length == 1 && Rows("Loot").Count == 0, "Feed loot depended on history recording or duplicated a bag");
        object row = Feed("Loot")[0]; Check(((int[])Get(Get(row, "FeedLoot"), "ItemIds")).SequenceEqual(new[] { 2650, 2592 }), "Feed did not retain the entire mixed bag");
        ((IList)Get(bag, "ItemIds")).Clear(); Check(((int[])Get(Get(Feed("Loot")[0], "FeedLoot"), "ItemIds")).Length == 2, "Feed bag shared tracker item lists");
        object filter = Get(Preferences, "FeedLoot"); ((IList)Get(filter, "AlwaysTrackBags")).Clear();
        foreach (PropertyInfo property in filter.GetType().GetProperties()) if (property.PropertyType == typeof(bool)) property.SetValue(filter, false, null);
        Check(!(bool)Static("HudLootPolicy", "ShowInFeed", row), "Disabled display filters still admitted a bag");
        Set(filter, "Shiny", true); Check((bool)Static("HudLootPolicy", "ShowInFeed", row), "Independent shiny feed filter did not admit the complete bag");
        Journal("Flush"); Journal("UseAccount", "FeedTemp"); Journal("UseAccount", "FeedLoot");
        Check(((int[])Get(Get(Feed("Loot")[0], "FeedLoot"), "ItemIds")).Length == 2, "Saved feed bag failed to roundtrip");
        Set(bag, "JournalScopeId", "wrong-account"); ((IList)Get(bag, "ItemIds")).Add(2592); Set(bag, "DropId", "alien"); Journal("RecordFeedLoot", bag);
        Check(Feed("Loot").Length == 1, "Foreign account bag entered feed");
        object notice = New("Proxy.Networking.Packets.NotificationPacket"); Set(notice, "NotificationType", (byte)8); Set(notice, "ObjectId", 0x1234);
        Set(notice, "Message", "{\"key\":\"s.portal_opened\",\"tokens\":{\"player\":\"FixtureOpener,unused\"}}"); Observe(notice);
        Check(Feed("Key pop").Length == 1 && (string)Get(Feed("Key pop")[0], "CalloutName") == "FixtureOpener", "Key-opening notification lost its opener");
        Check((string)Get(Feed("Key pop")[0], "KeyPopTier") == "Adept", "Snake Pit's reference2.5-grave difficulty was not retained in the key-opening notification");
        Set(notice, "Message", "{\"player\":\"/danger\"}"); Observe(notice); Check(Feed("Key pop").Length == 1, "Invalid opener became a command target");
        Set(notice, "Message", "{\"key\":\"s.portal_opened\"}"); Observe(notice); Check(Feed("Key pop").Length == 1, "Unowned portal was labeled as a key pop");
        Set(Preferences, "RecordLoot", true); Journal("Flush");
    }
    public static void Run(string path)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        var items = T("Proxy.DataStructures.GameData").GetField("Items", F);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { Activator.CreateInstance(items.FieldType.GetProperty("Map", F).PropertyType) }));
        client = FormatterServices.GetUninitializedObject(T("Proxy.Client")); Set(client, "ClientId", 42); Set(client, "_currentServerName", "FixtureServer");
        Locations(); Dust(); RealmClose(); LootAndKeys();
        Console.WriteLine("PASS: " + checks + " live feed, portal provenance, dust, loot and key-opening assertions.");
    }
}
'@
try { [DashboardFeedRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
