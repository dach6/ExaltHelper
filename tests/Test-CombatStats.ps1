param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

public static class CombatStatRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly assembly;
    static bool standalone;
    static object client, player, tracker;
    static int checks;
    static readonly List<string> failures = new List<string>();
    static Type T(string name) {
        if (standalone) name = name.Replace("ExaltHelper.Proxy.DataStructures.", "ExaltHUD.Models.")
            .Replace("ExaltHelper.Proxy.Networking.Packets.DataObjects.", "ExaltHUD.Protocol.DataObjects.")
            .Replace("ExaltHelper.Proxy.Networking.", "ExaltHUD.Protocol.")
            .Replace("ExaltHelper.Proxy.Helpers.", "ExaltHUD.Data.")
            .Replace("ExaltHelper.Proxy.Mods.DpsTrackerMod", "ExaltHUD.Tracking.DpsTracker")
            .Replace("ExaltHelper.Proxy.Client", "ExaltHUD.Tracking.TrackingSession");
        return assembly.GetType(name, true);
    }
    static object Get(object o, string n) { var t=o.GetType(); var f=t.GetField(n,F); return f!=null?f.GetValue(o):t.GetProperty(n,F).GetValue(o,null); }
    static void Set(object o,string n,object v) { var t=o.GetType(); var f=t.GetField(n,F); if(f!=null)f.SetValue(o,v);else t.GetProperty(n,F).GetSetMethod(true).Invoke(o,new object[]{v}); }
    static object Call(object o,string n,params object[] args) { return o.GetType().GetMethod(n,F).Invoke(o,args); }
    static object New(string name) { return Activator.CreateInstance(T(name),true); }
    static object Packet(string name) { return New("ExaltHelper.Proxy.Networking.Packets."+name); }
    static void Check(bool ok,string name) { checks++; if(!ok)failures.Add(name); }
    static long Total() { return (long)Get(Get(tracker,"combinedDungeonCombat"),"TotalDamage"); }
    static void Reset() { tracker=Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"),new object[]{client}); }
    static void Write(object writer,Type type,object value) { writer.GetType().GetMethod("Write",new[]{type}).Invoke(writer,new[]{value}); }

    // Use numeric protocol IDs and serialized status data, not assignments to the
    // model's named properties. Distinct values expose a shifted/mislabeled stat.
    static void Receive(params int[] idValuePairs) {
        var bytes=new MemoryStream();
        var writer=Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketWriter"),new object[]{bytes});
        Call(writer,"WriteCompressedInt",100);
        Write(writer,typeof(float),0f); Write(writer,typeof(float),0f);
        Call(writer,"WriteCompressedInt",idValuePairs.Length/2);
        for(int i=0;i<idValuePairs.Length;i+=2) {
            Write(writer,typeof(byte),(byte)idValuePairs[i]);
            Call(writer,"WriteCompressedInt",idValuePairs[i+1]);
            Write(writer,typeof(byte),(byte)0);
        }
        var reader=Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketReader"),new object[]{new MemoryStream(bytes.ToArray())});
        var status=New("ExaltHelper.Proxy.Networking.Packets.DataObjects.ObjectStatsData");
        Call(status,"Read",reader);
        Call(player,"UpdateFromObjectStatsData",status,0,0,0,0L,false);
    }
    static void ShootCloak(ushort bullet) {
        var shot=Packet("PlayerShootPacket"); Set(shot,"BulletId",bullet); Set(shot,"WeaponId",(short)17966);
        Set(shot,"ProjectileTypeId",(sbyte)0); Call(tracker,"OnPlayerShoot",shot);
    }
    static void Hit(ushort bullet) {
        var hit=Packet("EnemyHitPacket"); Set(hit,"TargetId",100); Set(hit,"OwnerId",900); Set(hit,"BulletId",bullet);
        Call(tracker,"OnEnemyHit",hit);
    }
    public static void Run(string path) {
        path=Path.GetFullPath(path); assembly=Assembly.LoadFrom(path); standalone=assembly.GetName().Name=="ExaltHUD";
        Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        var items=T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null,Activator.CreateInstance(items.FieldType,new object[]{Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType)}));
        client=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string name in new[]{"Enemies","Entities","Portals"}) Set(client,name,Activator.CreateInstance(client.GetType().GetField(name,F).FieldType));
        player=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));
        Set(player,"ObjectId",100); Set(player,"ObjectType",(ushort)768); Set(player,"Inventory",new int[30]);
        Set(player,"PlayerName","StatFixture"); Set(player,"Exaltations",new int[8]);
        Set(client,"ClientId",100); Set(client,"Player",player);
        var scaling=T("ExaltHelper.Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml",F).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),standalone?"ExaltHUD_Data":"ExaltHelper_Data","Objects.xml")});
        scaling.GetField("isInitialized",F).SetValue(null,true);

        Receive(0,1000,3,252,20,50,21,25,22,50,25,4500,26,92,27,61,28,77,51,17,52,11,53,2,8,17842,9,21228,10,14471,11,3813,113,1000);
        Check((int)Get(player,"Vitality")==92,"Stat 26 must populate Vitality, not the skin ID from stat 25");
        Check((int)Get(player,"Wisdom")==61,"Stat 27 must populate Wisdom");
        Check((int)Get(player,"Dexterity")==77,"Stat 28 must populate Dexterity");
        var skin=player.GetType().GetProperty("Skin",F);
        Check(skin!=null && (int)skin.GetValue(player,null)==4500,"Stat 25 must remain a separate Skin property");
        var statEnum=scaling.GetNestedType("StatType",F);
        foreach(var pair in new[]{Tuple.Create("VITALITY_STAT",92),Tuple.Create("WISDOM_STAT",61),Tuple.Create("DEXTERITY_STAT",77)}) {
            int actual=(int)scaling.GetMethod("GetPlayerStatValue",F).Invoke(null,new[]{player,Enum.Parse(statEnum,pair.Item1)});
            Check(actual==pair.Item2,"Ability scaling must read the received "+pair.Item1);
        }

        Reset(); ShootCloak(1); Hit(1);
        Check(Total()==269,"Locally created Darkened Sun proc at received VIT 92 and DEF 0 must deal 269");
        Receive(25,64000); Reset(); ShootCloak(2); Hit(2);
        Check(Total()==269,"Changing only the skin must not change cloak damage");
        Receive(26,102); Reset(); ShootCloak(3);
        var rows=(IEnumerable)Get(Get(tracker,"damageDiagnostics"),"rows");
        foreach(string row in rows) { Check(row.Split(',')[16]=="102","Combat diagnostics must report actual vitality, including a temporary boost"); break; }
        Receive(26,92); Hit(3);
        Check(Total()==299,"A proc fired during the VIT boost must retain its 102-VIT shot snapshot after expiry");
        Reset(); ShootCloak(4); Hit(4);
        Check(Total()==269,"The next proc must use VIT 92 after the temporary boost expires");
        Receive(27,83,28,95); Reset(); ShootCloak(5); Hit(5);
        Check(Total()==269,"WIS and DEX changes must not alter VIT-scaled cloak damage");
        Receive(27,61,28,77);

        // Update callers that previously compensated for the wrong property names.
        var classStats=New("ExaltHelper.Proxy.DataStructures.ClassStats");
        foreach(var pair in new[]{Tuple.Create("LifeMax",1000),Tuple.Create("ManaMax",252),Tuple.Create("AttackMax",50),Tuple.Create("DefenseMax",25),Tuple.Create("SpeedMax",50),Tuple.Create("VitalityMax",75),Tuple.Create("WisdomMax",50),Tuple.Create("DexterityMax",75)}) Set(classStats,pair.Item1,pair.Item2);
        ((IDictionary)T("ExaltHelper.Proxy.DataStructures.ClassStats").GetField("Map").GetValue(null))[(ushort)768]=classStats;
        var local=Get(Call(tracker,"CompileCurrentSnapshot"),"LocalPlayer");
        Check((int)Get(local,"Vitality")==92 && (int)Get(local,"Wisdom")==61 && (int)Get(local,"Dexterity")==77,"My Info must display the correct received stats");
        Check((int)Get(local,"BaseVitality")==75 && (int)Get(local,"BaseWisdom")==50 && (int)Get(local,"BaseDexterity")==75,"My Info base stats must subtract the corresponding equipment bonuses");
        var maxed=tracker.GetType().GetMethod("GetMaxedStats",F).Invoke(null,new[]{player});
        Check((int)Get(maxed,"Item2")==8,"Parse maxed-stat counts must retain the correct stat-to-bonus mapping");

        Reset(); var server=Packet("PlayerShootServerPacket");
        Set(server,"OwnerId",100);Set(server,"BulletId",(short)256);Set(server,"ContainerType",589);Set(server,"Damage",(short)350);Set(server,"BulletCount",(byte)1);
        Call(tracker,"OnPlayerShootServer",server);Hit(256);
        Check(Total()==350,"Ordinary server-created abilities must not receive a second stat bonus");
        var confirmation=Packet("EnemyShootPacket");Set(confirmation,"ObjectId",100);Set(confirmation,"OwnerId",900);Set(confirmation,"_bulletId",(ushort)256);Set(confirmation,"_damageAmount",(ushort)345);
        Call(tracker,"OnEnemyShootDamage",confirmation);
        Check(Total()==345,"A matching server report must still replace the estimate");

        if(!standalone) {
            var health=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Mods.PlayerTrackerMod"));
            Set(health,"_client",client); Set(health,"_clientHp",100); Call(health,"ApplyHealthRegeneration",1000);
            Check((int)Get(health,"_clientHp")==124,"Health regeneration must continue to use VIT 92, not WIS 61");
        }
        foreach(string failure in failures) Console.WriteLine("FAIL: "+failure);
        if(failures.Count>0) throw new Exception(failures.Count+" of "+checks+" combat-stat assertions failed.");
        Console.WriteLine("PASS: "+checks+" combat-stat packet, scaling, display and reconciliation assertions.");
    }
}
'@
try { [CombatStatRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
