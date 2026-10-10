param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
public static class DpsModifierRegression
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Assembly assembly;
    static object client,player,tracker;
    static int checks;
    static Type T(string n) { return assembly.GetType(n,true); }
    static object Get(object obj,string n) { var t=obj.GetType();var f=t.GetField(n,F);return f!=null?f.GetValue(obj):t.GetProperty(n,F).GetValue(obj,null); }
    static void Set(object obj,string n,object v) { var t=obj.GetType();var f=t.GetField(n,F);if(f!=null)f.SetValue(obj,v);else t.GetProperty(n,F).SetValue(obj,v,null); }
    static object Call(object obj,string n,params object[] args) { return obj.GetType().GetMethod(n,F).Invoke(obj,args); }
    static object Packet(string n) { return Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.Packets."+n),true); }
    static object Entity(int id) { var e=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));Set(e,"ObjectId",id);return e; }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message);checks++; }
    static long Total() { return (long)Get(Get(tracker,"combinedDungeonCombat"),"TotalDamage"); }
    static object Fight() { return ((IDictionary)Get(tracker,"allEnemies"))[900]; }
    static string Enchant(short id) { return Convert.ToBase64String(new byte[]{0,2,4,(byte)id,(byte)(id>>8),253,0}).TrimEnd('=').Replace('+','-').Replace('/','_'); }
    static void Reset(short enchant=0) {
        tracker=Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"),new object[]{client});
        ((IDictionary)Get(client,"Enemies")).Clear();
        Set(player,"Inventory",new[]{17842,21228,14471,3813});Set(player,"ItemDataString",enchant==0?"":Enchant(enchant)+",,,");
        Set(player,"Attack",50);Set(player,"Vitality",92);Set(player,"Effects",0);Set(player,"Effects2",0);
        Set(player,"CrucibleId","");Set(player,"BloodRitualId","");Set(player,"ExaltationBonusDamage",1000);
        Set(tracker,"realmSharkRng",Activator.CreateInstance(T("ExaltHelper.Proxy.Helpers.RealmSharkRng"),new object[]{1L}));
    }
    static void Shoot(sbyte projectile=0) {
        var p=Packet("PlayerShootPacket");Set(p,"WeaponId",(short)17842);Set(p,"BulletId",(ushort)1);Set(p,"ProjectileTypeId",projectile);Call(tracker,"OnPlayerShoot",p);
    }
    static void Hit(ushort bullet=1) { var p=Packet("EnemyHitPacket");Set(p,"TargetId",100);Set(p,"OwnerId",900);Set(p,"BulletId",bullet);Call(tracker,"OnEnemyHit",p); }
    public static void Run(string path) {
        path=Path.GetFullPath(path);assembly=Assembly.LoadFrom(path);Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        var items=T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null,Activator.CreateInstance(items.FieldType,new object[]{Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType)}));
        client=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string n in new[]{"Enemies","Entities","Portals"})Set(client,n,Activator.CreateInstance(client.GetType().GetField(n).FieldType));
        player=Entity(100);Set(client,"Player",player);Set(client,"ClientId",100);Set(player,"PlayerName","Tester");

        Reset();Shoot();Hit();Check(Total()==123,"Unenchanted Fractal shot: seed 1 rolls 82; ATT 50 gives 123");
        Reset(0x152);Shoot();Hit();Check(Total()==127,"Damage Bonus IV must mutate 75-105 to 78-110 before the roll (85 * 1.5)");
        for(sbyte projectile=1;projectile<6;projectile++) {
            Reset(0x152);Shoot(projectile);Hit();
            Check(Total()==(projectile==1?127:96),"Fractal subprojectile enchantment: "+projectile);
        }
        Reset(0x166);Shoot();Hit();Check(Total()==142,"Damage Tradeoff IV's 12.5% modifier is missing");
        Reset(0x5be);Shoot();Hit();Check(Total()==117,"Fire rate tradeoff must reduce per-hit damage; observed shots already include its fire rate");
        Reset(0x152);Shoot();Set(player,"ItemDataString","");Set(player,"Attack",100);Hit();
        Check(Total()==127,"A gear/stat change after firing must not modify a projectile already in flight");
        Reset(0x152);
        var server=Packet("PlayerShootServerPacket");Set(server,"OwnerId",100);Set(server,"BulletId",(short)256);
        Set(server,"ContainerType",17842);Set(server,"Damage",(short)120);Set(server,"BulletCount",(byte)1);Call(tracker,"OnPlayerShootServer",server);Hit(256);
        Check(Total()==120,"Server-created projectiles must not receive an enchantment multiplier again");

        Reset(0x152);Set(player,"Effects",0x40000);Set(player,"CrucibleId","modifier-test-cruc");Set(player,"BloodRitualId","modifier-test-rit");
        var events=Packet("CrucibleResponsePacket");
        Set(events,"Definitions",new[]{"{\"array\":[{\"id\":\"modifier-test-cruc\",\"crucible\":{\"bonuses\":[{\"type\":5,\"amount\":1.1}]}},{\"id\":\"modifier-test-rit\",\"crucible\":{\"bonuses\":[{\"type\":5,\"amount\":1.24}]}}]}"});
        Call(tracker,"OnCrucibleResponse",events);Shoot();Hit();
        Check(Total()==217,"Enchantment, Damaging, Crucible and Blood Ritual must combine exactly once");
        Check((bool)Get(tracker,"EventDamageModifiersResolved"),"Captured bonuses should be marked resolved");
        Reset();Set(player,"BloodRitualId","unseen-ritual");Shoot();Hit();
        Check(Total()==123 && !(bool)Get(tracker,"EventDamageModifiersResolved"),"An unseen ritual must stay explicitly unknown instead of using a guessed bonus");
        Set(player,"BloodRitualId","");

        // A hit can arrive before an enemy's first status update.
        Reset();Shoot();Hit();Check((string)Get(Fight(),"Name")=="Monster","Missing entities use a temporary name");
        var enemy=Entity(900);Set(enemy,"ObjectType",(ushort)7099);Set(enemy,"MaxHp",40000);Set(enemy,"Hp",10000);
        ((IDictionary)Get(client,"Enemies"))[900]=enemy;Call(tracker,"OnNewTick",Packet("NewTickPacket"));
        Check((string)Get(Fight(),"Name")=="Ghost of Skuld","Late enemy metadata must repair the existing fight name");
        Check((bool)Get(Fight(),"IsBoss") && (int)Get(Fight(),"MaxHp")==40000,"Late boss metadata must repair the existing encounter");
        Set(enemy,"ObjectType",(ushort)65021);Set(enemy,"StructureName","Future Boss");Call(tracker,"OnNewTick",Packet("NewTickPacket"));
        Check((string)Get(Fight(),"Name")=="Future Boss","Unknown object metadata must not overwrite a real name with Item #...");
        int now=Environment.TickCount;Set(Fight(),"StartTick",now-10000);Set(Fight(),"LastDamageTick",now-5000);
        Call(tracker,"MarkEnemyDefeated",Fight());
        Check(Math.Abs((double)Call(Fight(),"GetFightDurationSec")-5.0)<0.001,"Five seconds of death animation must not dilute completed DPS");
        Check(unchecked((int)Get(Fight(),"DefeatedTick")-now)<1000,"Loot attribution must still use disappearance time");
        Shoot();Hit();
        int finalHitTick=(int)Get(Fight(),"EndTick");
        Check(finalHitTick!=0 && finalHitTick==(int)Get(Fight(),"LastDamageTick"),"A late damaging hit must not restart a defeated enemy's clock");
        var confirmation=Packet("EnemyShootPacket");Set(confirmation,"ObjectId",100);Set(confirmation,"OwnerId",900);
        Set(confirmation,"_bulletId",(ushort)1);Set(confirmation,"_damageAmount",(ushort)150);Call(tracker,"OnEnemyShootDamage",confirmation);
        Check((int)Get(Fight(),"EndTick")==finalHitTick,"A correction must not extend a completed fight");

        string directory=Path.Combine(Path.GetTempPath(),"ExaltModifierTest-"+Guid.NewGuid().ToString("N"));
        string file=Path.Combine(directory,"dps-latest.csv");
        try {
            var diagnostics=Get(tracker,"damageDiagnostics");
            // Finish a pending periodic write before requesting this explicit test destination.
            for(int i=0;i<300 && (bool)Get(diagnostics,"savePending");i++)Thread.Sleep(10);
            Call(diagnostics,"SaveInBackground",true,directory);
            for(int i=0;i<300 && (bool)Get(diagnostics,"savePending");i++)Thread.Sleep(10);
            Check(File.Exists(file),"Combat trace should be available without disconnecting or changing map");
            string trace=File.ReadAllText(file);
            Check(trace.Contains("event_definitions_known") && trace.Contains("shot_enchants") && trace.Contains("local_shot"),"Trace must identify bonuses and shot inputs");
            Check(!trace.Contains("Tester"),"Trace must omit player names");
        } finally { if(File.Exists(file))File.Delete(file);if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("PASS: "+checks+" DPS modifier, name, timing and trace assertions.");
    }
}
'@
try { [DpsModifierRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
