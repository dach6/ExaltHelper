param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
public static class DpsAccountingRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly assembly;
    static object client, tracker, player;
    static int checks;
    static readonly List<string> failures = new List<string>();
    static Type T(string name) { return assembly.GetType(name, true); }
    static object Get(object obj, string name) { var t=obj.GetType(); var f=t.GetField(name,F); return f!=null?f.GetValue(obj):t.GetProperty(name,F).GetValue(obj,null); }
    static void Set(object obj,string name,object value) { var t=obj.GetType(); var f=t.GetField(name,F); if(f!=null)f.SetValue(obj,value);else t.GetProperty(name,F).GetSetMethod(true).Invoke(obj,new object[]{value}); }
    static object Call(object obj,string name,params object[] args) { return obj.GetType().GetMethod(name,F).Invoke(obj,args); }
    static void Check(bool result,string name) { checks++; if(!result)failures.Add(name); }
    static object Packet(string name) { return Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.Packets."+name),true); }
    static object Entity(int id) { var e=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject")); Set(e,"ObjectId",id); return e; }
    static object Dungeon { get { return Get(tracker,"combinedDungeonCombat"); } }
    static object Fight(int target) { return ((IDictionary)Get(tracker,"allEnemies"))[target]; }
    static long Total() { return (long)Get(Dungeon,"TotalDamage"); }
    static object Local(object fight) { return fight==null?null:((IDictionary)Get(fight,"Damagers"))["__LOCAL__"]; }
    static long Damage(object fight) { var p=Local(fight); return p==null?0:(long)Get(p,"Damage"); }
    static int Hits(object fight) { var p=Local(fight); return p==null?0:(int)Get(p,"Hits"); }
    static void Reset() { tracker=Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"),new object[]{client}); ((IDictionary)Get(client,"Enemies")).Clear(); }
    static void Shoot(int owner,int summoner,short bullet,short damage,int item=17842,byte count=1)
    {
        var p=Packet("PlayerShootServerPacket"); Set(p,"OwnerId",owner); Set(p,"SummonerId",summoner);
        Set(p,"BulletId",bullet); Set(p,"Damage",damage); Set(p,"ContainerType",item); Set(p,"BulletCount",count);
        Call(tracker,"OnPlayerShootServer",p);
    }
    static void Hit(int owner,ushort bullet,int target,bool send=true)
    {
        var p=Packet("EnemyHitPacket"); Set(p,"TargetId",owner); Set(p,"OwnerId",target); Set(p,"BulletId",bullet);
        if(p.GetType().GetField("Send",F)!=null)Set(p,"Send",send);
        Call(tracker,"OnEnemyHit",p);
    }
    static void Confirm(int owner,ushort bullet,int target,ushort damage)
    {
        var p=Packet("EnemyShootPacket"); Set(p,"ObjectId",owner); Set(p,"OwnerId",target); Set(p,"_bulletId",bullet); Set(p,"_damageAmount",damage);
        Call(tracker,"OnEnemyShootDamage",p);
    }
    public static void Run(string path)
    {
        path=Path.GetFullPath(path); assembly=Assembly.LoadFrom(path); Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        var items=T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null,Activator.CreateInstance(items.FieldType,new object[]{Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType)}));
        client=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string name in new[]{"Enemies","Entities","Portals"})Set(client,name,Activator.CreateInstance(client.GetType().GetField(name).FieldType));
        Set(client,"ClientId",100); player=Entity(100); Set(client,"Player",player); Set(player,"PlayerName","Tester");
        Set(player,"Inventory",new[]{17842,21228,-1,-1}); Set(player,"Attack",80); Set(player,"Vitality",92); Set(player,"Wisdom",80);
        var scaling=T("ExaltHelper.Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml",F).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"ExaltHelper_Data","Objects.xml")});
        scaling.GetField("isInitialized",F).SetValue(null,true);

        Reset(); Shoot(100,0,256,100); Hit(100,256,900); Confirm(100,256,900,60);
        Check(Total()==60,"A 100-damage estimate must become the server's 60 damage");
        Check((long)Get(Fight(900),"TotalDamage")==60 && Damage(Fight(900))==60,"Boss and player totals must both be corrected");
        Check(Damage(Dungeon)==60 && Hits(Dungeon)==1 && Hits(Fight(900))==1,"Correction must keep one hit and update dungeon attribution");
        Confirm(100,256,900,60); Hit(100,256,900);
        Check(Total()==60 && Hits(Dungeon)==1,"Repeated reports must not restore the estimate or add a hit");
        Hit(100,256,901); Confirm(100,256,901,80);
        Check(Total()==140 && Damage(Fight(901))==80,"Piercing targets need independent corrections");
        Shoot(100,0,256,100); Hit(100,256,900); Confirm(100,256,900,70);
        Check(Total()==210 && Hits(Dungeon)==3,"A newly fired projectile may reuse an earlier bullet ID");

        Reset(); Shoot(100,0,256,100); Hit(100,256,900); Confirm(100,256,900,150);
        Check(Total()==150 && Hits(Dungeon)==1,"Server damage must also correct an underestimate");
        Reset(); Shoot(100,0,256,100); Confirm(100,256,900,60); Hit(100,256,900);
        Check(Total()==60 && Hits(Dungeon)==1,"Server-first and client-first reports must produce the same result");
        Reset(); Shoot(100,0,256,100); Hit(100,256,900); Confirm(100,256,900,0);
        Check(Total()==0 && Damage(Dungeon)==0 && Hits(Dungeon)==0,"Zero confirmation must remove an estimated damaging hit");
        Hit(100,256,900); Check(Total()==0,"A rejected hit must not reappear on a repeated local report");
        Reset(); Shoot(100,0,256,100); Confirm(100,256,900,0); Hit(100,256,900);
        Check(Total()==0,"Zero damage received first must suppress a later estimate");

        Reset(); Shoot(200,100,256,100); Shoot(201,100,256,100);
        Hit(200,256,900); Hit(201,256,900); Confirm(200,256,900,40); Confirm(201,256,900,70);
        Check(Total()==110 && Hits(Dungeon)==2,"Summons sharing bullet IDs need separate corrections");
        Reset(); Shoot(100,0,256,100,17842,3);
        for(ushort bullet=256;bullet<259;bullet++){Hit(100,bullet,900);Confirm(100,bullet,900,60);}
        Check(Total()==180 && Hits(Dungeon)==3,"Each pellet must retain its own server damage");

        Reset(); Shoot(100,0,256,2100,0x99aa); Hit(100,256,900);
        Check(Total()==2100,"Server-created shots must use supplied damage without adding ability scaling again");
        foreach(short supplied in new short[]{5,20,100,350})
        {
            Reset(); Shoot(100,0,256,supplied,17966); Hit(100,256,900);
            Check(Total()==supplied+264,"A server cloak proc must retain its base and add its VIT bonus once: "+supplied);
        }
        Reset(); Shoot(100,0,256,2100,0x99aa);
        var enemy=Entity(900); Set(enemy,"Defense",40); ((IDictionary)Get(client,"Enemies"))[900]=enemy;
        Hit(100,256,900); Check(Total()==2060,"Server projectile damage still needs target defense applied");

        if(Packet("EnemyHitPacket").GetType().GetField("Send",F)!=null)
        {
            Reset(); Shoot(100,0,256,100); Hit(100,256,900,false);
            Check(Total()==0,"A hit blocked by the proxy must not enter local DPS");
            Hit(100,256,900); Check(Total()==100,"An allowed hit must still count after a blocked attempt");
        }

        Reset(); Shoot(100,0,256,100);
        Call(Get(tracker,"activeGuardingEntities"),"Add",900); Hit(100,256,900);
        var fight=Fight(900); Set(fight,"EndTick",12345); Set(fight,"IsGuarding",false); Call(Get(tracker,"activeGuardingEntities"),"Clear");
        Confirm(100,256,900,60);
        Check((long)Get(fight,"TotalGuardDamage")==60 && (long)Get(Dungeon,"TotalGuardDamage")==60,"Correction must preserve guard classification from the original hit");
        Check((long)Get(Local(fight),"GuardDamage")==60 && (int)Get(Local(fight),"GuardHits")==1,"Player guard totals must agree with corrected damage");
        Check((int)Get(fight,"EndTick")==12345,"A confirmation must not restart a completed fight timer");
        Confirm(100,256,900,0); Check(Total()==60,"A repeated conflicting confirmation must not count as another hit");
        Reset(); Shoot(100,0,256,100); Call(Get(tracker,"activeGuardingEntities"),"Add",900); Hit(100,256,900); Confirm(100,256,900,0);
        Check((int)Get(Fight(900),"TotalGuardHits")==0 && (long)Get(Fight(900),"TotalGuardDamage")==0 && (int)Get(Dungeon,"TotalGuardHits")==0,"Rejected guard damage must remove guard hit counters");

        // Network-order seed fixture, including a value with the unsigned high bit set.
        foreach(uint seed in new[]{0x01020304u,0xf1234567u})
        {
            var bytes=new[]{(byte)(seed>>24),(byte)(seed>>16),(byte)(seed>>8),(byte)seed};
            var input=new MemoryStream(bytes);
            var reader=Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketReader"),new object[]{input});
            Check((uint)Call(reader,"ReadUInt32")==seed,"Map RNG seed must be read in network order: "+seed);
            var output=new MemoryStream(); var writer=Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketWriter"),new object[]{output});
            writer.GetType().GetMethod("Write",new[]{typeof(uint)}).Invoke(writer,new object[]{seed});
            Check(BitConverter.ToString(output.ToArray())==BitConverter.ToString(bytes),"Map RNG seed must be forwarded unchanged: "+seed);
        }

        Reset(); Shoot(100,0,256,100); Hit(100,256,900); Confirm(100,256,900,60);
        var diagnostics=Get(tracker,"damageDiagnostics"); var rows=(ICollection)Get(diagnostics,"rows");
        Check(rows.Count==3,"Trace must contain the raw shot, estimate and correction");
        string traceDirectory=Path.Combine(Path.GetTempPath(),"ExaltDpsTest-"+Guid.NewGuid().ToString("N"));
        string tracePath=Path.Combine(traceDirectory,"dps-latest.csv");
        try
        {
            Call(diagnostics,"Save",traceDirectory);
            string trace=File.ReadAllText(tracePath);
            Check(trace.Contains("local_estimate") && trace.Contains("server_correction") && trace.Contains(",60,100,-40,"),"Saved trace must show the exact overestimate correction");
            Check(!trace.Contains("Tester"),"Combat trace must omit player names");
            Call(diagnostics,"Reset"); Call(diagnostics,"Save",traceDirectory);
            Check(rows.Count==0 && File.ReadAllText(tracePath)==trace,"An empty map must not erase the previous combat trace");
            for(int i=0;i<20001;i++)Call(diagnostics,"Record",i.ToString());
            Check(rows.Count==20000 && (int)Get(diagnostics,"omittedRows")==1,"Diagnostic memory must stay bounded");
            Call(diagnostics,"Save",traceDirectory);
            string[] saved=File.ReadAllLines(tracePath);
            Check(saved.Length==20003 && saved[1]=="# omitted_older_rows=1" && saved[3]=="1","Saved trace must identify omitted rows and keep the newest combat data");
            // A regular file cannot be used as the output directory. Logging must fail harmlessly.
            Call(diagnostics,"Save",tracePath); Check(Total()==60,"Diagnostic output failures must not change combat totals");
        }
        finally { if(File.Exists(tracePath))File.Delete(tracePath); if(Directory.Exists(traceDirectory))Directory.Delete(traceDirectory,true); }
        foreach(string failure in failures)Console.WriteLine("FAIL: "+failure);
        if(failures.Count>0)throw new Exception(failures.Count+" of "+checks+" DPS accounting checks failed.");
        Console.WriteLine("PASS: "+checks+" DPS accounting assertions.");
    }
}
'@
try { [DpsAccountingRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
