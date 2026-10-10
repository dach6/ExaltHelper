param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$FixturePath = "$PSScriptRoot/fixtures/darkened-sun-ghost-bride.csv"
)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;

public static class LethalStrikeReplay
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly assembly;
    static bool standalone;
    static object client, player, enemy, tracker;
    static int checks;
    static readonly List<string> failures = new List<string>();
    static Type T(string name) {
        if(standalone) name=name.Replace("ExaltHelper.Proxy.DataStructures.","ExaltHUD.Models.")
            .Replace("ExaltHelper.Proxy.Networking.","ExaltHUD.Protocol.")
            .Replace("ExaltHelper.Proxy.Helpers.","ExaltHUD.Data.")
            .Replace("ExaltHelper.Proxy.Mods.DpsTrackerMod","ExaltHUD.Tracking.DpsTracker")
            .Replace("ExaltHelper.Proxy.Client","ExaltHUD.Tracking.TrackingSession");
        return assembly.GetType(name,true);
    }
    static object Get(object o,string n) { var t=o.GetType();var f=t.GetField(n,F);return f!=null?f.GetValue(o):t.GetProperty(n,F).GetValue(o,null); }
    static void Set(object o,string n,object v) { var t=o.GetType();var f=t.GetField(n,F);if(f!=null)f.SetValue(o,v);else t.GetProperty(n,F).GetSetMethod(true).Invoke(o,new[]{v}); }
    static object Call(object o,string n,params object[] args) { return o.GetType().GetMethod(n,F).Invoke(o,args); }
    static object Packet(string n) { return Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.Packets."+n),true); }
    static object Entity(int id) { var o=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));Set(o,"ObjectId",id);return o; }
    static void Check(bool ok,string message) { checks++;if(!ok)failures.Add(message); }
    static long Total() { return (long)Get(Get(tracker,"combinedDungeonCombat"),"TotalDamage"); }
    static int Hits() { var local=((IDictionary)Get(Get(tracker,"combinedDungeonCombat"),"Damagers"))["__LOCAL__"];return local==null?0:(int)Get(local,"Hits"); }
    static void Reset() {
        tracker=Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"),new object[]{client});
        Set(player,"Vitality",94);Set(player,"Attack",58);Set(player,"Effects",0);Set(player,"Effects2",0);
        Set(player,"Inventory",new[]{17842,21228,14471,3813});Set(player,"ExaltationBonusDamage",1000);
        Set(enemy,"Defense",10);Set(enemy,"Effects",0);Set(enemy,"Effects2",0);
    }
    static void Int(BinaryWriter w,int value) { w.Write(IPAddress.HostToNetworkOrder(value)); }
    static void Short(BinaryWriter w,short value) { w.Write(IPAddress.HostToNetworkOrder(value)); }
    // Independent incoming wire fixture with a captured cloak base damage of 6.
    // Count and owner cases exercise the same packet decoding route.
    static void ServerShot(ushort bullet,short damage=6,int container=17966,int owner=100,int summoner=0,byte count=1) {
        using(var bytes=new MemoryStream()) {
            using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true)) {
                Int(writer,39);writer.Write((byte)12);Short(writer,(short)bullet);Int(writer,owner);Int(writer,container);
                Int(writer,0);Int(writer,0);Int(writer,0);Short(writer,damage);Int(writer,summoner);
                writer.Write((byte)0);writer.Write(count);Int(writer,0);
            }
            var packet=T("ExaltHelper.Proxy.Networking.Packet").GetMethod("Create",F).Invoke(null,new object[]{bytes.ToArray()});
            Call(tracker,"OnPlayerShootServer",packet);
            Check((short)Get(packet,"Damage")==damage,"Tracking must not modify the forwarded projectile damage");
        }
    }
    static void WeaponShot(ushort bullet,int damage) {
        // A shot CSV contains the actual RNG result, not the connection seed.
        // Seed the weapon record from that observed value; cloak shots above go
        // through packet decoding and the production creation handler.
        var shot=Activator.CreateInstance(tracker.GetType().GetNestedType("RealmSharkBullet",F));
        Set(shot,"TotalDmg",damage);Set(shot,"ContainerType",17842);Set(shot,"ScalingApplied",true);
        Set(shot,"ShooterId",100);Set(shot,"BulletId",(int)bullet);
        ((IDictionary)Get(tracker,"playerProjectiles"))[((long)100<<32)|bullet]=shot;
    }
    static void Hit(ushort bullet,int owner=100,int target=900,bool send=true) {
        var hit=Packet("EnemyHitPacket");Set(hit,"TargetId",owner);Set(hit,"OwnerId",target);Set(hit,"BulletId",bullet);
        if(!standalone) Set(hit,"Send",send);
        Call(tracker,"OnEnemyHit",hit);
    }
    static void Confirm(ushort bullet,ushort damage,int owner=100,int target=900) {
        var hit=Packet("EnemyShootPacket");Set(hit,"ObjectId",owner);Set(hit,"OwnerId",target);
        Set(hit,"_bulletId",bullet);Set(hit,"_damageAmount",damage);Call(tracker,"OnEnemyShootDamage",hit);
    }
    public static void Run(string path,string fixture) {
        path=Path.GetFullPath(path);fixture=Path.GetFullPath(fixture);assembly=Assembly.LoadFrom(path);
        standalone=assembly.GetName().Name=="ExaltHUD";Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        var items=T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null,Activator.CreateInstance(items.FieldType,new object[]{Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType)}));
        client=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string name in new[]{"Enemies","Entities","Portals"})Set(client,name,Activator.CreateInstance(client.GetType().GetField(name,F).FieldType));
        player=Entity(100);Set(player,"PlayerName","ReplayFixture");Set(player,"ObjectType",(ushort)768);Set(client,"ClientId",100);Set(client,"Player",player);
        enemy=Entity(900);Set(enemy,"ObjectType",(ushort)7089);((IDictionary)Get(client,"Enemies"))[900]=enemy;
        var scaling=T("ExaltHelper.Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml",F).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),standalone?"ExaltHUD_Data":"ExaltHelper_Data","Objects.xml")});
        scaling.GetField("isInitialized",F).SetValue(null,true);
        Reset();Set(player,"CrucibleId","replay-crucible");Set(player,"BloodRitualId","replay-ritual");
        var definitions=Packet("CrucibleResponsePacket");
        Set(definitions,"Definitions",new[]{"{\"array\":[{\"id\":\"replay-crucible\",\"crucible\":{\"bonuses\":[]}},{\"id\":\"replay-ritual\",\"crucible\":{\"bonuses\":[{\"type\":5,\"amount\":1.24}]}}]}"});
        Call(tracker,"OnCrucibleResponse",definitions);
        long weapon=0,cloak=0,capturedReferenceTotal=0,tomatoWeapon=0;int cloakCount=0,weaponCount=0;
        foreach(string line in File.ReadAllLines(fixture).Skip(1)) {
            var cols=line.Split(',').Select(s=>s.Trim('"')).ToArray();
            int container=int.Parse(cols[0]),raw=int.Parse(cols[2]);ushort bullet=ushort.Parse(cols[1]);
            Set(player,"Vitality",int.Parse(cols[3]));Set(player,"Attack",int.Parse(cols[9]));
            Set(enemy,"Defense",int.Parse(cols[5]));Set(enemy,"Effects",int.Parse(cols[6]));Set(enemy,"Effects2",int.Parse(cols[7]));
            capturedReferenceTotal+=int.Parse(cols[11]);long before=Total();
            if(container==17966) { ServerShot(bullet,(short)raw);cloakCount++; }
            else {
                int roll=int.Parse(cols[8]);float multiplier=(float)Call(tracker,"GetRealmSharkPlayerStatsMultiplier");
                Check((int)(roll*multiplier)==raw,"Captured weapon roll/event bonus changed at bullet "+bullet);
                WeaponShot(bullet,raw);weaponCount++;
                tomatoWeapon+=(int)(roll*((int.Parse(cols[9])+25)*0.02f))-int.Parse(cols[5]);
            }
            Set(player,"Vitality",int.Parse(cols[4]));Hit(bullet);long amount=Total()-before;
            if(container==17966) cloak+=amount;else weapon+=amount;
            Check(amount==(container==17966?277:int.Parse(cols[11])),"Captured Ghost Bride hit differs at bullet "+bullet+": "+amount);
        }
        Check(weaponCount==92 && cloakCount==26 && capturedReferenceTotal==13634,"Fixture must contain 92 weapon hits, 26 cloak hits and a 13,634 captured reference total");
        Check(tomatoWeapon==10814 && tomatoWeapon+26*276==17990,"The independent Tomato formula must reproduce its 17,990 reference total");
        Check(weapon==13634 && cloak==7202 && Total()==20836 && Hits()==118,"All recorded cloak hits must contribute once, preserving weapon damage");
        Console.WriteLine("Replay: weapon="+weapon+", cloak="+cloak+", total="+Total()+"; captured reference="+capturedReferenceTotal+"; Tomato formula=17990.");
        Set(player,"CrucibleId","");Set(player,"BloodRitualId","");

        Reset();ServerShot(256);Hit(256);Hit(256);
        Check(Total()==277 && Hits()==1,"Duplicate cloak hits must count once");
        Confirm(256,250);Confirm(256,250);Hit(256);
        Check(Total()==250 && Hits()==1,"Server confirmation must replace the cloak estimate, not add another hit");
        Reset();ServerShot(256);Confirm(256,0);Hit(256);
        Check(Total()==0 && Hits()==0,"A server-rejected cloak hit must never be resurrected");
        Reset();Set(player,"Vitality",102);ServerShot(256);Set(player,"Vitality",94);Set(player,"Inventory",new[]{17842,2650,14471,3813});Hit(256);
        Check(Total()==302,"An in-flight cloak proc must retain its VIT snapshot after the boost and gear change");
        Reset();Set(enemy,"Defense",0);ServerShot(256);Hit(256);
        Check(Total()==276,"Zero-defense damage must preserve the supplied base and flat bonus");
        Reset();Set(enemy,"Defense",100);ServerShot(256);Hit(256);
        Check(Total()==291,"The cloak bonus must use the actual target defense, not Tomato's fixed 100 shortcut");
        Reset();Set(player,"Vitality",20);ServerShot(256);Hit(256);
        Check(Total()==60,"Stats below the cloak threshold must not create a negative scaling bonus");
        Reset();Set(enemy,"Effects",0x1000000);ServerShot(256);Hit(256);
        Check(Total()==0,"Lethal Strike must not bypass invulnerability");
        Reset();ServerShot(511,count:2);Hit(511);Hit(256);
        Check(Total()==554 && Hits()==2,"Both cloak pellets must survive a server bullet-ID wrap");
        Reset();ServerShot(256,350,589);Hit(256);
        Check(Total()==350,"Void Quiver server damage must remain supplied damage without extra DEF scaling");
        Reset();ServerShot(256,350,17842);Hit(256);
        Check(Total()==340,"Ordinary server weapon shots must not acquire cloak scaling");
        Reset();ServerShot(256,350,17966,200,100);Hit(256,200);
        Check(Total()==340,"Summon damage must retain the separate supplied-damage path");
        if(!standalone) { Reset();ServerShot(256);Hit(256,send:false);Check(Total()==0,"Blocked hit packets must stay excluded"); }
        foreach(string failure in failures)Console.WriteLine("FAIL: "+failure);
        if(failures.Count!=0)throw new Exception(failures.Count+" of "+checks+" Lethal Strike replay checks failed.");
        Console.WriteLine("PASS: "+checks+" Lethal Strike live-fixture and accounting assertions.");
    }
}
'@
try { [LethalStrikeReplay]::Run($AssemblyPath,$FixturePath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
