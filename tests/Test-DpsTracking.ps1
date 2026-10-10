param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
public static class DpsRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly a;
    static object client, tracker, player;
    static int checks;
    static Type T(string n) { return a.GetType(n, true); }
    static object Get(object o, string n) { var t = o.GetType(); var f = t.GetField(n,F); return f != null ? f.GetValue(o) : t.GetProperty(n,F).GetValue(o,null); }
    static void Set(object o, string n, object v) { var t=o.GetType(); var f=t.GetField(n,F); if(f!=null) f.SetValue(o,v); else t.GetProperty(n,F).GetSetMethod(true).Invoke(o,new object[]{v}); }
    static object New(string n) { return Activator.CreateInstance(T(n),true); }
    static object Call(object o,string n, params object[] args) { return o.GetType().GetMethod(n,F).Invoke(o,args); }
    static void Check(bool ok,string n) { if(!ok) throw new Exception(n); checks++; }
    static object Packet(string n) { return New("ExaltHelper.Proxy.Networking.Packets."+n); }
    static object Entity(int id)
    {
        object e=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));
        Set(e,"ObjectId",id); Set(e,"Inventory",new int[]{17842,21228,-1,-1});
        return e;
    }
    static long Total() { return (long)Get(Get(tracker,"combinedDungeonCombat"),"TotalDamage"); }
    static void Reset()
    {
        tracker=Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"),new object[]{client});
        ((IDictionary)Get(client,"Enemies")).Clear();
    }
    static void Shoot(int owner,int summoner,short bullet,byte count,short damage)
    {
        object p=Packet("PlayerShootServerPacket");
        Set(p,"OwnerId",owner); Set(p,"SummonerId",summoner); Set(p,"BulletId",bullet);
        Set(p,"BulletCount",count); Set(p,"Damage",damage); Set(p,"ContainerType",17842);
        Call(tracker,"OnPlayerShootServer",p);
    }
    static void Hit(int owner,ushort bullet,int target)
    {
        object p=Packet("EnemyHitPacket");
        if (p.GetType().GetField("TargetId", F) != null) Set(p,"TargetId",owner); else Set(p,"huhfbhmqff",owner);
        Set(p,"BulletId",bullet); Set(p,"OwnerId",target);
        Call(tracker,"OnEnemyHit",p);
    }
    static void ServerHit(int owner,ushort bullet,int target,ushort damage)
    {
        object p=Packet("EnemyShootPacket"); Set(p,"ObjectId",owner); Set(p,"_bulletId",bullet);
        Set(p,"OwnerId",target);
        if (p.GetType().GetField("_damageAmount", F) != null) Set(p,"_damageAmount",damage); else Set(p,"huhfbhmpzn",damage);
        Call(tracker,"OnEnemyShootDamage",p);
    }
    public static void Run(string path)
    {
        a=Assembly.LoadFrom(Path.GetFullPath(path));
        Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var items=T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null,Activator.CreateInstance(items.FieldType,new object[]{Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType)}));
        client=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string n in new string[]{"Enemies","Entities","Portals"})
            Set(client,n,Activator.CreateInstance(client.GetType().GetField(n).FieldType));
        Set(client,"ClientId",100); player=Entity(100); Set(player,"PlayerName","Tester");
        Set(player,"Attack",50); Set(player,"Vitality",92); Set(player,"Wisdom",80);
        Set(client,"Player",player);
        // Load the exact XML shipped with this build, not a developer's external equip.xml.
        Type scaling=T("ExaltHelper.Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml",F).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"ExaltHelper_Data","Objects.xml")});
        scaling.GetField("isInitialized",F).SetValue(null,true);
        Reset();
        foreach(int slot in new int[]{1,2,3,8,17,24,28})
            Check((bool)tracker.GetType().GetMethod("IsMainWeaponSlot",F).Invoke(null,new object[]{slot}),"Weapon classification");
        foreach(int slot in new int[]{0,4,13,18,21})
            Check(!(bool)tracker.GetType().GetMethod("IsMainWeaponSlot",F).Invoke(null,new object[]{slot}),"Ability classification");

        // Known six-type shiny Fractal Blades: two 75-105 and four 55-75 projectiles.
        for(int i=0;i<6;i++)
        {
            object p=Packet("PlayerShootPacket"); Set(p,"BulletId",(ushort)i);
            Set(p,"WeaponId",(short)17842); Set(p,"ProjectileTypeId",(sbyte)i);
            Call(tracker,"OnPlayerShoot",p); Hit(100,(ushort)i,900);
        }
        Check(Total()==552,"Six Fractal types must use their own ranges, once each");
        object unknown=Packet("PlayerShootPacket"); Set(unknown,"BulletId",(ushort)99);
        Set(unknown,"WeaponId",(short)17842); Set(unknown,"ProjectileTypeId",(sbyte)99);
        Call(tracker,"OnPlayerShoot",unknown); Hit(100,99,900);
        Check(Total()==552,"Unknown projectile type borrowed bullet zero's damage");
        for(int i=0;i<6;i++) Hit(100,(ushort)i,900);
        Check(Total()==552,"Duplicate client hits inflated damage");
        Hit(100,0,901); Check(Total()==664,"Piercing projectile must count against a second target");
        ServerHit(100,0,900,112); Check(Total()==664,"Server confirmation counted client hit twice");

        Reset(); Shoot(100,0,256,3,100);
        Hit(100,256,900); Hit(100,257,900); Hit(100,258,900);
        Check(Total()==300,"Multi-shot pellets were collapsed or multiplied");
        Reset(); Shoot(200,100,256,1,100); Shoot(201,100,256,1,150);
        Hit(200,256,900); Hit(201,256,900);
        Check(Total()==250,"Two summons with identical bullet IDs collided");
        Reset(); Shoot(200,300,256,1,999); Hit(100,256,900);
        Check(Total()==0,"Another player's projectile was credited locally");
        Reset(); Shoot(200,100,256,1,100);
        ((IDictionary)Get(client,"Enemies"))[200]=Entity(200);
        ServerHit(200,256,900,90); Hit(200,256,900);
        Check(Total()==90,"Summon in enemy list was discarded or counted twice");
        Shoot(200,100,256,1,100); Hit(200,256,900);
        Check(Total()==190,"Reused bullet ID did not start a new projectile");

        // The documented Lethal Strike bonus is added to the existing projectile base.
        foreach(int defense in new int[]{0,20,100})
        {
            int damage=(int)scaling.GetMethod("CalculateProjectileDamage",F).Invoke(null,new object[]{5,17966,92,player,defense});
            Check(damage==5+(int)(264+1.13*defense),"Darkened Sun tooltip formula at defense "+defense);
        }
        Reset();
        object proc=Packet("PlayerShootPacket"); Set(proc,"BulletId",(ushort)50);
        Set(proc,"WeaponId",(short)17966); Set(proc,"ProjectileTypeId",(sbyte)0);
        Call(tracker,"OnPlayerShoot",proc); Set(player,"Vitality",50); Hit(100,50,900);
        Check(Total()==269,"Cloak proc received ATT, lost its stat snapshot, or scaled twice");
        // Shipped test shuriken: 1500 base + (80 ATT - 50)*20 = 2100; no weapon ATT multiplier.
        Reset(); Set(player,"Attack",80);
        object ability=Packet("PlayerShootPacket"); Set(ability,"BulletId",(ushort)60);
        Set(ability,"WeaponId",unchecked((short)0x99aa)); Set(ability,"ProjectileTypeId",(sbyte)0);
        Call(tracker,"OnPlayerShoot",ability); Set(player,"Attack",100); Hit(100,60,900);
        Check(Total()==2100,"Unsigned ability ID or once-only stat scaling failed");
        Reset(); Shoot(100,0,256,1,100); Hit(100,256,900);
        object dancer=Entity(900); Set(dancer,"ObjectType",(ushort)20450); Set(dancer,"Hp",1); Set(dancer,"MaxHp",180000);
        ((IDictionary)Get(client,"Enemies"))[900]=dancer;
        Call(tracker,"OnNewTick",Packet("NewTickPacket"));
        object fight=((IDictionary)Get(tracker,"allEnemies"))[900];
        int ended=(int)Get(fight,"EndTick"); Check(ended!=0,"Completed MV dancer timer kept running at 1 HP");
        Call(tracker,"OnNewTick",Packet("NewTickPacket"));
        Check((int)Get(fight,"EndTick")==ended,"Completed timer moved on a later tick");
        Shoot(100,0,257,1,100); Hit(100,257,900);
        Check((int)Get(fight,"EndTick")==0,"Later damaging phase did not resume timing");
        Reset(); Set(player,"Attack",50); Set(player,"CrucibleId","cruc"); Set(player,"BloodRitualId","rit");
        Call(tracker,"GetRealmSharkPlayerStatsMultiplier");
        Check(!(bool)Get(tracker,"EventDamageModifiersResolved"),"Unobserved event configuration was treated as known");
        object events=Packet("CrucibleResponsePacket");
        string definitions="{\"array\":[{\"id\":\"cruc\",\"crucible\":{\"bonuses\":[{\"type\":1,\"amount\":9}]}},{\"id\":\"rit\",\"crucible\":{\"bonuses\":[{\"type\":5,\"amount\":1.24}]}}]}";
        Set(events,"Definitions",new string[]{definitions}); Call(tracker,"OnCrucibleResponse",events);
        Check(Math.Abs((float)Call(tracker,"GetRealmSharkPlayerStatsMultiplier")-1.86f)<0.0001,
            "Blood Ritual's 24% bonus or non-damage Crucible bonus handling failed");
        Check((bool)Get(tracker,"EventDamageModifiersResolved"),"Captured event IDs did not resolve");
        Set(events,"Definitions",new string[]{"{\"array\":[{\"id\":\"cruc\",\"crucible\":{\"bonuses\":[{\"type\":5,\"amount\":1.1}]}}]}"});
        Call(tracker,"OnCrucibleResponse",events);
        Check(Math.Abs((float)Call(tracker,"GetRealmSharkPlayerStatsMultiplier")-2.046f)<0.0001,"Two event damage multipliers did not combine");
        Set(player,"BloodRitualId","");
        Check(Math.Abs((float)Call(tracker,"GetRealmSharkPlayerStatsMultiplier")-1.65f)<0.0001,"Disabled Blood Ritual still boosted damage");
        Set(events,"Definitions",new string[]{"invalid JSON"}); Call(tracker,"OnCrucibleResponse",events);
        Check(Math.Abs((float)Call(tracker,"GetRealmSharkPlayerStatsMultiplier")-1.65f)<0.0001,"Invalid definitions corrupted existing bonuses");
        Set(player,"CrucibleId","");
        Check(Math.Abs((float)Call(tracker,"GetRealmSharkPlayerStatsMultiplier")-1.5f)<0.0001,"Event bonuses leaked after disabling challenges");
        Set(events,"CrucibleIds",new int[]{1,2}); Set(events,"Definitions",new string[]{definitions});
        var buffer=new MemoryStream();
        object writer=Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketWriter"),new object[]{buffer});
        Call(events,"Write",writer);
        object reader=Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketReader"),new object[]{new MemoryStream(buffer.ToArray())});
        object roundTrip=Packet("CrucibleResponsePacket"); Call(roundTrip,"Read",reader);
        Check((byte)Get(roundTrip,"Id")==183 && ((string[])Get(roundTrip,"Definitions"))[0]==definitions &&
            ((int[])Get(roundTrip,"CrucibleIds"))[1]==2,"Event packet forwarding round trip failed");
        Console.WriteLine("PASS: "+checks+" DPS assertions.");
    }
}
'@
try { [DpsRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
