param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/combat-policy"
)
$ErrorActionPreference = 'Stop'
if ([IO.DriveInfo]::new('C:\').AvailableFreeSpace -le 5GB -or (Test-Path 'C:/Users/Administrator/Desktop/code/.exalthelper-tools/disk-guard/STOP')) { throw 'Disk guard stopped work.' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = $testRoot
Add-Type -ReferencedAssemblies System.Core -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

public static class CombatPolicyRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app;
    static int checks;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object value, string name)
    {
        Type type = value as Type ?? value.GetType(); object target = value is Type ? null : value;
        var field = type.GetField(name, F); return field == null ? type.GetProperty(name, F).GetValue(target, null) : field.GetValue(target);
    }
    static void Set(object value, string name, object data)
    {
        Type type = value as Type ?? value.GetType(); object target = value is Type ? null : value;
        var field = type.GetField(name, F); if (field == null) type.GetProperty(name, F).SetValue(target, data, null); else field.SetValue(target, data);
    }
    static object Call(object value, string name, params object[] args)
    {
        Type type = value as Type ?? value.GetType(); return type.GetMethod(name, F).Invoke(value is Type ? null : value, args);
    }
    static object Store(string name, string file, object value) { return T("HudStorage").GetMethod(name, F).MakeGenericMethod(value.GetType()).Invoke(null, new[] { file, value }); }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static string Group(string dungeon, int type, string name) { return (string)Call(T("HudCombatPolicy"), "Classify", dungeon, type, name); }
    static object Enemy(int id, int type, string name, bool boss = true)
    {
        object enemy = New("Proxy.DataStructures.EnemyCombatSnapshot"); Set(enemy, "EntityId", id); Set(enemy, "ObjectType", type);
        Set(enemy, "Name", name); Set(enemy, "IsBoss", boss); Set(enemy, "TotalDamage", 100L); Set(enemy, "FightDurationSeconds", 2.0); return enemy;
    }
    static object Snapshot(string dungeon, object enemy)
    {
        object snapshot = New("Proxy.DataStructures.DpsSnapshot"); Set(snapshot, "DungeonName", dungeon); Set(snapshot, "ActiveEnemy", enemy);
        Set(Get(snapshot, "LocalPlayer"), "Name", "FixtureHero"); return snapshot;
    }
    static void Player(object enemy, string name, bool local)
    {
        object player = New("Proxy.DataStructures.PlayerDamageEntry"); Set(player, "Name", name); Set(player, "IsLocalPlayer", local);
        Set(player, "Damage", 100L); ((IList)Get(enemy, "Damagers")).Add(player);
    }
    static bool Allowed(object preferences, object snapshot, object enemy) { return (bool)Call(T("HudCombatPolicy"), "ShouldRecord", preferences, snapshot, enemy); }
    static IList Fights() { return (IList)Get(Call(T("HudJournal"), "Snapshot"), "Fights"); }
    static void Observe(object snapshot) { Call(T("HudJournal"), "ObserveSnapshot", snapshot); }
    static void Account(string name) { Call(T("HudJournal"), "UseAccount", name); Call(T("HudJournal"), "WaitForLoads"); }
    static void Classifications()
    {
        object preferences = New("HudCombatPreferences"); var toggles = (IDictionary)Get(preferences, "Groups");
        Check(toggles.Count == 12 && (bool)Get(preferences, "TrackSolo"), "Combat defaults did not preserve all twelve groups and solo fights.");
        object[][] samples = {
            new object[] { "exaltation", "The Nest", 4243, "Killer Bee Queen" },
            new object[] { "expert", "Deadwater Docks", 0, "Bilgewater" },
            new object[] { "adept", "Snake Pit", 0, "Stheno" },
            new object[] { "beginner", "Pirate Cave", 0, "Dreadstump" },
            new object[] { "beacon_guardian", "Realm", 53002, "Legion Fey Guardian" },
            new object[] { "rookie_hero", "Realm", 44158, "Beached Buccaneer" },
            new object[] { "adept_hero", "Realm", 22034, "Beach Bum" },
            new object[] { "veteran_hero", "Realm", 1033, "Sea Dragon" },
            new object[] { "adept_encounter", "Realm", 22003, "Skull Shrine" },
            new object[] { "veteran_encounter", "Realm", 22146, "Lost Sentry" },
            new object[] { "seasonal_encounter", "Realm", 49755, "The Wanderer" },
            new object[] { "treasure_crate", "Realm", 620, "Bilgewater's Booty" }
        };
        foreach (var sample in samples)
        {
            string group = (string)sample[0], dungeon = (string)sample[1], name = (string)sample[3]; int type = (int)sample[2];
            Check(Group(dungeon, type, name) == group, "Wrong classification: " + group);
            object enemy = Enemy(1, type, name), snapshot = Snapshot(dungeon, enemy);
            Check(Allowed(preferences, snapshot, enemy), "Default capture lost " + group);
            toggles[group] = false; Check(!Allowed(preferences, snapshot, enemy), "Disabled capture group leaked: " + group); toggles[group] = true;
        }
        Check(Group("Realm", 56341, "Alien Reactor") == "adept_hero" && Group("Realm", 56342, "Alien Reactor") == "veteran_hero", "Identical Alien Reactor names overrode their distinct object types.");
        Check(Group("Realm", 56342, "Lich") == "veteran_hero", "Object type did not take priority over conflicting hero name.");
        Check(Group("Realm", 65000, "lich") == "rookie_hero", "Hero variant name fallback was lost.");
        Check(Group("Realm", 0, "Alien Reactor") == "adept_hero", "Ambiguous unknown-type hero fallback changed from its first curated tier.");
        Check(Group("The Nest", 620, "Lich") == "treasure_crate", "Crate classification lost priority over dungeon and hero groups.");
        Check(Group("The Nest", 47414, "Dimitus") == "seasonal_encounter", "Special boss difficulty variants lost seasonal priority.");
        Check(Group("Queen Bunny Chamber", 0, "Biff the Buffed Bunny") == "seasonal_encounter", "Seasonal name lost priority over rated dungeon.");
        Check(Group("Hidden Interregnum", 14474, "The Wanderer") == "exaltation", "Dungeon Wanderer was confused with the seasonal object type.");
        Check(Group("The Nest", 2331, "Lich") == "exaltation", "Rated dungeon lost priority over hero fallback.");
        Check(Group("Realm", 22034, "Beach Bum") == "adept_hero", "Hero label lost priority over its encounter label.");
        Check(Group("Realm", 22042, "Ghost Ship") == "adept_encounter", "Unlabeled Ghost Ship override was omitted.");
        Check(Group("Realm", 4324, "Yellow Beehemoth") == "veteran_encounter", "Unlabeled Beehemoth override was omitted.");
        Check(Group("Realm", 8200, "Janus") == "adept" && Group("Realm", 3449, "Stone Guardian") == "adept", "Realm castle miniboss overrides were omitted.");
        Check(Group("Realm", 3428, "Tentacle") == "adept_encounter" && Group("Realm", 22026, "Tentacle") == "adept_encounter", "Hermit tentacle variants bypassed their encounter filter.");
        Check(Group("Realm", 22018, "Pentaract Tower") == "adept_encounter" && Group("Realm", 1337, "Pentaract Tower") == "adept_encounter", "Unlabeled Pentaract members lost their anchor group.");
        Check(Group("Realm", 34486, "Giant Arm") == "veteran_hero", "Assembled Giant member lost its hero anchor group.");
        Check(Group("Realm", 24020, "Criminal Monkey") == "adept_hero", "Animal Merchant member lost its hero anchor group.");
        Check(Group("Realm", 53018, "Chaos Orb") == "beacon_guardian", "Legion Missionary member lost its beacon anchor group.");
        Check(Group("Realm", 56432, "Chest") == "treasure_crate", "Newest curated treasure crate was omitted.");
        Check(Group(null, 0, null) == "unknown", "Unknown classification was not explicit.");

        foreach (string key in toggles.Keys.Cast<string>().ToArray()) toggles[key] = false;
        object unknown = Enemy(2, 65000, "Unclassified"), knownMap = Snapshot("The Nest", unknown);
        Check(Allowed(preferences, Snapshot("Unknown dungeon", unknown), unknown), "Unknown boss was hidden by curated group switches.");
        Set(unknown, "IsBoss", false); Check(Allowed(preferences, knownMap, unknown), "Ordinary dungeon enemy was hidden by boss switches.");
        Set(unknown, "IsBoss", true); Check(!Allowed(preferences, knownMap, unknown), "Boss flag was ignored for rated dungeon capture.");
        Set(unknown, "IsBoss", false); Set(unknown, "ObjectType", 620); Check(!Allowed(preferences, knownMap, unknown), "Explicit crate type escaped filtering without the boss flag.");
        Set(unknown, "ObjectType", 56342); Set(unknown, "Name", "Alien Reactor"); Check(!Allowed(preferences, knownMap, unknown), "Explicit hero type escaped filtering without the boss flag.");

        preferences = New("HudCombatPreferences"); Set(preferences, "TrackSolo", false);
        object solo = Enemy(3, 65000, "Unknown"), soloSnapshot = Snapshot("Realm", solo);
        Check(!Allowed(preferences, soloSnapshot, solo), "An encounter without named participants bypassed solo filtering.");
        Player(solo, "FixtureHero", true); Check(!Allowed(preferences, soloSnapshot, solo), "Solo encounter bypassed solo filtering.");
        Player(solo, "  fixturehero  ", false); Check(!Allowed(preferences, soloSnapshot, solo), "Unresolved local-player flag created a false group fight.");
        Player(solo, "  ", false); Check(!Allowed(preferences, soloSnapshot, solo), "Blank unknown participant created a false group fight.");
        Player(solo, "Friend", false); Check(Allowed(preferences, soloSnapshot, solo), "Late named participant did not qualify the group fight.");
        Check(Allowed(null, soloSnapshot, solo), "Absent legacy preferences changed default capture.");

        ((IDictionary)Get(preferences, "Groups"))["expert"] = false;
        Check((bool)Store("Save", "combat-preferences.json", preferences), "Combat preferences could not be persisted.");
        var loaded = Store("Load", "combat-preferences.json", New("HudCombatPreferences")); Call(loaded, "Normalize");
        Check(!(bool)Get(loaded, "TrackSolo") && !(bool)Call(loaded, "Tracks", "expert") && (bool)Call(loaded, "Tracks", "adept"), "Combat settings round-trip lost defaults or disabled values.");
        Set(loaded, "Groups", new Dictionary<string, bool> { { "EXPERT", false }, { "unknown", false }, { "junk", false } }); Call(loaded, "Normalize");
        Check(((IDictionary)Get(loaded, "Groups")).Count == 12 && !(bool)Call(loaded, "Tracks", "expert") && (bool)Call(loaded, "Tracks", "unknown"), "Settings normalization lost known values or gated unknown fights.");
        Set(loaded, "Groups", null); Call(loaded, "Normalize"); Check((bool)Call(loaded, "Tracks", "expert"), "Null saved groups did not regain default capture.");
    }
    static void Difficulties()
    {
        object[][] cases = {
            new object[] { "The Hive", "Rookie" }, new object[] { "Snake Pit Portal", "Adept" },
            new object[] { "{s.snake_pit_portal}", "Adept" }, new object[] { "{s.oryxs_castle}", "Adept" },
            new object[] { "ice cave", "Expert" }, new object[] { " bella's ", "Expert" },
            new object[] { "Ivory Wyvern Portal", "Adept" }, new object[] { "Legacy Bilgewater's Grotto", "Expert" },
            new object[] { "nest", "Exaltation" }, new object[] { "Stromwell's Rift I", "Expert" },
            new object[] { "Stromwell's Rift II", "Exaltation" }, new object[] { "Future dungeon", "Unknown" },
            new object[] { "Realm", "Unknown" }, new object[] { "", "Unknown" }
        };
        foreach (var row in cases) Check((string)Call(T("HudCombatPolicy"), "DungeonTier", row[0]) == (string)row[1], "Incorrect key-pop tier for " + row[0]);
        double[] boundaries = { 0, 2, 2.1, 4.5, 4.6, 6.5, 6.6, 10, -1, double.NaN, double.PositiveInfinity };
        string[] tiers = { "Rookie", "Rookie", "Adept", "Adept", "Expert", "Expert", "Exaltation", "Exaltation", "Unknown", "Unknown", "Unknown" };
        for (int i = 0; i < boundaries.Length; i++) Check((string)Call(T("HudCombatPolicy"), "DifficultyTier", boundaries[i]) == tiers[i], "Incorrect continuous tier boundary: " + boundaries[i]);
        Check(Group("The Hive", 0, "Boss") == "beginner" && Group("Snake Pit", 0, "Boss") == "adept", "Rookie/adept combat threshold changed.");
        Check(Group("Oryx's Castle", 0, "Boss") == "adept" && Group("Davy Jones' Locker", 0, "Boss") == "expert", "Adept/expert combat threshold changed.");
        Check(Group("High Tech Terror", 0, "Boss") == "expert" && Group("The Nest", 0, "Boss") == "exaltation", "Expert/exaltation combat threshold changed.");
        Check((double)Call(T("HudCombatPolicy"), "DungeonDifficulty", "Abyss of Demons") == 4 && (double)Call(T("HudCombatPolicy"), "DungeonDifficulty", "Infernal Abyss of Demons") == 6, "Normal and Infernal Abyss ratings collapsed.");
        Check((double)Call(T("HudCombatPolicy"), "DungeonDifficulty", "Kogbold Steamworks") == 8 && (double)Call(T("HudCombatPolicy"), "DungeonDifficulty", "Advanced Kogbold Steamworks") == 9, "Normal and advanced Steamworks ratings collapsed.");
    }
    static void Journal()
    {
        object preferences = Get(T("HudPreferences"), "Current"), combat = New("HudCombatPreferences");
        Set(preferences, "Combat", combat); Set(preferences, "RecordCombat", true); Set(preferences, "RecordLoot", false);
        Account("CombatPolicyTransitions");
        var groups = (IDictionary)Get(combat, "Groups"); groups["expert"] = false;
        object enemy = Enemy(401, 65000, "Fixture boss"), snapshot = Snapshot("Deadwater Docks", enemy);
        Player(enemy, "FixtureHero", true); Observe(snapshot); Check(Fights().Count == 0, "Disabled group entered the real journal capture branch.");
        groups["expert"] = true; Set(combat, "TrackSolo", false); Observe(snapshot); Check(Fights().Count == 0, "Excluded solo fight entered the real journal.");
        Player(enemy, "Friend", false); Observe(snapshot); Check(Fights().Count == 1, "Excluded solo fight could not qualify after another player joined.");
        groups["expert"] = false; Set(enemy, "TotalDamage", 0L); ((IList)Get(enemy, "Damagers")).Clear(); Observe(snapshot);
        Check(Fights().Count == 1 && (long)Get(Fights()[0], "Damage") == 0, "A filter change blocked zero-damage corrections of an existing fight.");
        Set(enemy, "TotalDamage", 50L); Set(enemy, "IsDead", true); Observe(snapshot); Observe(snapshot);
        Check(Fights().Count == 1 && (long)Get(Fights()[0], "Damage") == 50 && (bool)Get(Fights()[0], "Defeated"), "Late correction duplicated or failed to finish an existing fight.");
        var runs = (IDictionary)Get(Call(T("HudJournal"), "Snapshot"), "DungeonRuns"); Check((long)runs["Deadwater Docks"] == 1, "Repeated corrected snapshots duplicated the dungeon run.");
        object excluded = Enemy(402, 65000, "Other boss"); Player(excluded, "Friend", false); Set(snapshot, "ActiveEnemy", excluded); Observe(snapshot);
        Check(Fights().Count == 1, "New fight bypassed the disabled group after an earlier fight qualified.");
        Set(excluded, "IsBoss", false); Observe(snapshot); Check(Fights().Count == 2, "Unknown ordinary enemy was incorrectly gated by boss difficulty.");
        object archived = Snapshot("Realm", Enemy(403, 65000, "Archived")); Set(archived, "HistoryIndex", 0); Observe(archived);
        Check(Fights().Count == 2, "Browsing history created fresh combat records.");
        Account("CombatPolicyUnknown"); Set(combat, "TrackSolo", true); foreach (string key in groups.Keys.Cast<string>().ToArray()) groups[key] = false;
        object unknown = Snapshot("Unknown map", Enemy(404, 65000, "Unknown boss")); Observe(unknown); Check(Fights().Count == 1, "Unknown encounter was hidden when every curated group was disabled.");
        Set(preferences, "RecordCombat", false); Observe(Snapshot("Unknown map", Enemy(405, 65000, "Next boss")));
        Check(Fights().Count == 1, "Master combat switch stopped gating capture."); Set(preferences, "RecordCombat", true); Set(preferences, "Combat", New("HudCombatPreferences"));
    }
    static object MapEnemy(object client, string collection, int entity, ushort type, string name)
    {
        object enemy = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { entity });
        Set(enemy, "ObjectType", type); Set(enemy, "StructureName", name); Set(enemy, "MaxHp", 40000); Set(enemy, "Hp", 35000);
        ((IDictionary)Get(client, collection))[entity] = enemy; return enemy;
    }
    static void TrackerTypes()
    {
        var items = T("Proxy.DataStructures.GameData").GetField("Items", F);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { Activator.CreateInstance(items.FieldType.GetProperty("Map", F).PropertyType) }));
        object client = FormatterServices.GetUninitializedObject(T("Proxy.Client"));
        foreach (string field in new[] { "Enemies", "Entities", "Portals" }) Set(client, field, Activator.CreateInstance(client.GetType().GetField(field, F).FieldType));
        object player = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { 100 });
        Set(player, "PlayerName", "FixtureHero"); Set(player, "IsPlayer", true); Set(client, "Player", player); Set(client, "ClientId", 100);
        ((IDictionary)Get(client, "Entities"))[100] = player;
        object tracker = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod"), new object[] { client }); Set(client, "dpsTracker", tracker);
        MapEnemy(client, "Enemies", 501, 56342, "Alien Reactor"); Call(tracker, "RecordCombatDamage", 501, 100, true, "FixtureHero", "Knight");
        var enemies = (IDictionary)Get(tracker, "allEnemies"); object first = enemies[501];
        Check(first != null && (int)Get(Call(first, "ToSnapshot"), "ObjectType") == 56342, "Damage-created tracker lost the veteran reactor object type.");
        ((IDictionary)Get(client, "Enemies")).Remove(501); MapEnemy(client, "Enemies", 502, 56341, "Alien Reactor");
        Call(tracker, "RecordCombatDamage", 502, 100, true, "FixtureHero", "Knight"); object merged = Call(enemies[502], "ToSnapshot");
        Check(ReferenceEquals(first, enemies[502]) && (int)Get(merged, "ObjectType") == 56341 && (int)Get(merged, "EncounterId") == 501, "Phase replacement lost object type or stable encounter identity.");
        MapEnemy(client, "Portals", 503, 620, "Bilgewater's Booty"); Call(tracker, "RecordCombatDamage", 503, 100, true, "FixtureHero", "Knight");
        Check((int)Get(Call(enemies[503], "ToSnapshot"), "ObjectType") == 620, "Portal-backed damage target lost its object type.");
        MapEnemy(client, "Enemies", 504, 53002, "Legion Fey Guardian"); object tick = New("Proxy.Networking.Packets.NewTickPacket"); Call(tracker, "OnNewTick", tick);
        Check(enemies.Contains(504) && (int)Get(Call(enemies[504], "ToSnapshot"), "ObjectType") == 53002, "Tick-created tracker lost its object type.");
        ((IDictionary)Get(client, "Enemies")).Remove(502); MapEnemy(client, "Enemies", 505, 56342, "Alien Reactor"); Call(tracker, "OnNewTick", tick);
        Check(enemies.Contains(505) && ReferenceEquals(first, enemies[505]) && (int)Get(Call(enemies[505], "ToSnapshot"), "ObjectType") == 56342, "Tick phase merge failed to update the object type.");
    }
    public static void Run(string path)
    {
        path = Path.GetFullPath(path); app = Assembly.LoadFrom(path); Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        Classifications(); Difficulties(); Journal(); TrackerTypes();
        Console.WriteLine("PASS: " + checks + " combat capture, classification, difficulty, persistence, journal, and tracker checks.");
    }
}
'@
[CombatPolicyRegression]::Run($AssemblyPath)
