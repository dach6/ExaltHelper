param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/dashboard-combat"
)
$ErrorActionPreference = 'Stop'
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

public static class DashboardCombatIntegration
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static object client, player, tracker;
    static int checks, scenario;
    static readonly List<string> failures = new List<string>();
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
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
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, F).Invoke(o, args); }
    static object Static(string type, string name, params object[] args) { object result = T(type).GetMethod(name, F).Invoke(null, args); if (type == "HudJournal" && name == "UseAccount") T(type).GetMethod("WaitForLoads", F).Invoke(null, null); return result; }
    static object Packet(string name) { return Activator.CreateInstance(T("Proxy.Networking.Packets." + name), true); }
    static void Check(bool good, string message) { checks++; if (!good) failures.Add(message); }
    static IList Fights() { return (IList)Get(Static("HudJournal", "Snapshot"), "Fights"); }
    static IList Sources() { return Fights().Count == 0 ? new ArrayList() : (IList)Get(Fights()[Fights().Count - 1], "Sources"); }
    static long SourceTotal() { return Sources().Cast<object>().Sum(x => (long)Get(x, "Damage")); }
    static long LocalDamage()
    {
        object fight = Get(tracker, "combinedDungeonCombat"); var local = ((IDictionary)Get(fight, "Damagers"))["__LOCAL__"];
        return local == null ? 0 : (long)Get(local, "Damage");
    }
    static void Reset()
    {
        Static("HudJournal", "UseAccount", "CombatHookFixture" + (++scenario));
        tracker = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod"), new object[] { client });
        T("Proxy.Mods.DpsTrackerMod").GetField("currentHistoryIndex", F).SetValue(null, -1);
        Set(client, "dpsTracker", tracker);
        ((IDictionary)Get(client, "Enemies")).Clear(); ((IDictionary)Get(client, "Entities")).Clear();
        ((IDictionary)Get(client, "Entities"))[100] = player;
        Set(player, "Inventory", new[] { 17842, 21228, -1, -1 });
        Set(player, "Attack", 80); Set(player, "Dexterity", 65); Set(player, "Vitality", 92); Set(player, "Wisdom", 80);
        Set(player, "Effects", 0); Set(player, "Effects2", 0); Set(player, "CrucibleId", ""); Set(player, "BloodRitualId", "");
    }
    static void Shoot(short bullet, short damage = 100, int owner = 100, int summoner = 0, byte count = 1, int item = 17842)
    {
        object packet = Packet("PlayerShootServerPacket"); Set(packet, "OwnerId", owner); Set(packet, "SummonerId", summoner);
        Set(packet, "BulletId", bullet); Set(packet, "Damage", damage); Set(packet, "ContainerType", item); Set(packet, "BulletCount", count);
        Call(tracker, "OnPlayerShootServer", packet);
    }
    static void Hit(ushort bullet, int target = 900, int owner = 100, bool send = true)
    {
        object packet = Packet("EnemyHitPacket"); Set(packet, "TargetId", owner); Set(packet, "OwnerId", target); Set(packet, "BulletId", bullet); Set(packet, "Send", send);
        Call(tracker, "OnEnemyHit", packet);
    }
    static void Confirm(ushort bullet, ushort damage, int target = 900, int owner = 100)
    {
        object packet = Packet("EnemyShootPacket"); Set(packet, "ObjectId", owner); Set(packet, "OwnerId", target);
        Set(packet, "_bulletId", bullet); Set(packet, "_damageAmount", damage); Call(tracker, "OnEnemyShootDamage", packet);
    }
    static void Push() { Call(tracker, "PushSnapshotToOverlay"); }
    static object Boss(int id)
    {
        var boss = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { id });
        Set(boss, "ObjectType", (ushort)65021); Set(boss, "StructureName", "Fixture Phase Boss"); Set(boss, "MaxHp", 40000); Set(boss, "Hp", 30000); Set(boss, "IsEnemy", true);
        ((IDictionary)Get(client, "Enemies"))[id] = boss; return boss;
    }
    public static void Run(string path)
    {
        path = Path.GetFullPath(path); assembly = Assembly.LoadFrom(path); Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        var items = T("Proxy.DataStructures.GameData").GetField("Items", F);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { Activator.CreateInstance(items.FieldType.GetProperty("Map", F).PropertyType) }));
        client = FormatterServices.GetUninitializedObject(T("Proxy.Client"));
        foreach (string field in new[] { "Enemies", "Entities", "Portals" }) Set(client, field, Activator.CreateInstance(client.GetType().GetField(field, F).FieldType));
        player = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { 100 });
        Set(player, "PlayerName", "FixtureHero"); Set(player, "IsPlayer", true); Set(client, "ClientId", 100); Set(client, "Player", player);
        var scaling = T("Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml", F).Invoke(null, new object[] { Path.Combine(Directory.GetCurrentDirectory(), "ExaltHelper_Data", "Objects.xml") });
        scaling.GetField("isInitialized", F).SetValue(null, true);

        Reset(); Shoot(256); Hit(256); Push();
        Check(Fights().Count == 1 && SourceTotal() == 100 && LocalDamage() == 100, "Real projectile handler did not publish initial source damage");
        Set(player, "Attack", 20); Set(player, "Dexterity", 30); Set(player, "Vitality", 10); Set(player, "Wisdom", 20); Set(player, "Inventory", new[] { 2666, 2650, -1, -1 });
        Confirm(256, 60); Confirm(256, 60); Hit(256); Push();
        Check(SourceTotal() == LocalDamage() && SourceTotal() == 60, "Server correction/duplicates made source totals disagree with local combat damage");
        Check(Sources().Count == 1 && (int)Get(Sources()[0], "Attack") == 80 && (int)Get(Sources()[0], "Vitality") == 92,
            "Gear/buff changes split a delayed correction into a negative or wrong-context source");
        Check(Sources().Count == 1 && (long)Get(Sources()[0], "ConfirmedDamage") == 60, "Confirmed contribution was duplicated or lost");

        Reset(); Shoot(256); Confirm(256, 60); Hit(256); Push();
        Check(SourceTotal() == 60 && SourceTotal() == LocalDamage(), "Server-first projectile history differs from client-first history");

        Reset(); Shoot(256); Hit(256); Push(); Confirm(256, 0); Push();
        Check(LocalDamage() == 0, "Rejected hit fixture did not reach production correction path");
        Check(Fights().Cast<object>().Sum(x => (long)Get(x, "Damage")) == 0 && Fights().Cast<object>().Sum(x => ((IList)Get(x, "Sources")).Cast<object>().Sum(s => (long)Get(s, "Damage"))) == 0,
            "Zero confirmation left stale positive combat/source history");

        Reset(); Shoot(256, count: 3);
        for (ushort bullet = 256; bullet < 259; bullet++) { Hit(bullet); Confirm(bullet, 60); }
        Push(); Check(SourceTotal() == 180 && SourceTotal() == LocalDamage(), "Pellet source history does not match corrected combat");

        Reset(); Shoot(256, owner: 200, summoner: 100); Shoot(256, owner: 201, summoner: 100);
        Hit(256, owner: 200); Hit(256, owner: 201); Confirm(256, 40, owner: 200); Confirm(256, 70, owner: 201); Push();
        Check(SourceTotal() == 110 && SourceTotal() == LocalDamage(), "Summons sharing bullet ids collided in source history");
        Check(Sources().Cast<object>().All(x => (string)Get(x, "Kind") == "Summon"), "Summon damage was mislabeled as a weapon source");

        Reset(); Shoot(256); Hit(256, send: false); Push();
        Check(Fights().Count == 0 && LocalDamage() == 0, "Blocked hit packet entered persistent combat history");

        Reset(); Boss(900); Shoot(256); Hit(256); Push();
        object taken = Packet("EnemyShootPacket"); Set(taken, "ObjectId", 900); Set(taken, "OwnerId", 100); Set(taken, "_damageAmount", (ushort)25);
        Static("HudJournal", "ObservePacket", client, taken, false); Push();
        Check(Fights().Count == 1 && ((IDictionary)Get(Fights()[0], "DamageTaken")).Contains("FixtureHero") &&
            (long)((IDictionary)Get(Fights()[0], "DamageTaken"))["FixtureHero"] == 25, "Observed player damage taken is not attached to the enemy fight");
        ((IDictionary)Get(client, "Enemies")).Remove(900); Boss(901); Shoot(257); Hit(257, 901); Push();
        Check(Fights().Count == 1 && (long)Get(Fights()[0], "Damage") == 200, "Boss phase entity replacement duplicated a cumulative encounter");
        Check(Fights().Count == 1 && SourceTotal() == LocalDamage(), "Boss phase replacement split or lost source history");
        Set(taken, "ObjectId", 901); Set(taken, "_damageAmount", (ushort)15);
        Static("HudJournal", "ObservePacket", client, taken, false); Push();
        Check((long)((IDictionary)Get(Fights()[0], "DamageTaken"))["FixtureHero"] == 40,
            "Boss phase replacement lost player damage taken from either phase");

        Reset(); Boss(900); Shoot(256); Hit(256);
        object map = Packet("MapInfoPacket"); Set(map, "MapName", "Nexus"); Set(map, "DisplayName", "Nexus");
        Call(tracker, "OnMapChange", map); Static("HudJournal", "ObservePacket", client, map, false);
        Check(Fights().Count == 1 && (long)Get(Fights()[0], "Damage") == 100 && SourceTotal() == 100,
            "Leaving before the next timer snapshot lost the final fight or damage sources");

        Reset(); Boss(900); Shoot(256); Hit(256); Push(); Confirm(256, 0);
        Call(tracker, "OnMapChange", map); Static("HudJournal", "ObservePacket", client, map, false);
        Check(Fights().Count == 1 && (long)Get(Fights()[0], "Damage") == 0 && SourceTotal() == 0,
            "Leaving immediately after zero correction left stale positive history");

        Reset(); Set(player, "ExaltationBonusDamage", 1050); Set(player, "Effects", 0xC0024); Set(player, "CrucibleId", "snapshot-fixture");
        object definitions = Packet("CrucibleResponsePacket");
        Set(definitions, "Definitions", new[] { "{\"id\":\"snapshot-fixture\",\"bonuses\":[{\"type\":5,\"amount\":1.25}]}" });
        Call(tracker, "OnCrucibleResponse", definitions);
        object live = Get(Call(tracker, "CompileCurrentSnapshot"), "LocalPlayer");
        Check((int)Get(live, "ExaltationBonusDamage") == 1050 && (bool)Get(live, "Damaging") && (bool)Get(live, "Berserk") && (bool)Get(live, "Weak") && (bool)Get(live, "Dazed"),
            "Live calculator snapshot omitted packet-derived buffs or exaltation multiplier");
        Check((bool)Get(live, "HasChallengeDamageBonus") && Math.Abs((double)Get(live, "ChallengeDamageMultiplier") - 1.25) < .00001,
            "Live calculator snapshot omitted resolved challenge damage bonus");
        Set(player, "CrucibleId", "unknown-fixture"); live = Get(Call(tracker, "CompileCurrentSnapshot"), "LocalPlayer");
        Check(!(bool)Get(live, "HasChallengeDamageBonus") && (string)Get(live, "CrucibleId") == "unknown-fixture" && !(bool)Get(live, "HasMasteryDamageBonus"),
            "Unresolved challenge or mastery bonus was reported as known");

        foreach (string failure in failures) Console.WriteLine("FAIL: " + failure);
        if (failures.Count > 0) throw new Exception(failures.Count + " of " + checks + " dashboard combat integration checks failed.");
        Console.WriteLine("PASS: " + checks + " dashboard combat integration assertions.");
    }
}
'@
try { [DashboardCombatIntegration]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
