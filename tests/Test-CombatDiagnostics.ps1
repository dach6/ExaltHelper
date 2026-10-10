param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

public static class CombatDiagnosticsRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly assembly;
    static bool standalone;
    static object client, player, tracker, diagnostics;
    static int checks;
    static Type T(string name) {
        if (standalone) name = name.Replace("ExaltHelper.Proxy.DataStructures.", "ExaltHUD.Models.")
            .Replace("ExaltHelper.Proxy.Networking.", "ExaltHUD.Protocol.")
            .Replace("ExaltHelper.Proxy.Helpers.", "ExaltHUD.Data.")
            .Replace("ExaltHelper.Proxy.Mods.DpsTrackerMod", "ExaltHUD.Tracking.DpsTracker")
            .Replace("ExaltHelper.Proxy.Client", "ExaltHUD.Tracking.TrackingSession");
        return assembly.GetType(name, true);
    }
    static object Get(object obj, string name) { var t=obj.GetType(); var f=t.GetField(name,F); return f!=null?f.GetValue(obj):t.GetProperty(name,F).GetValue(obj,null); }
    static void Set(object obj, string name, object value) { var t=obj.GetType(); var f=t.GetField(name,F); if(f!=null)f.SetValue(obj,value);else t.GetProperty(name,F).GetSetMethod(true).Invoke(obj,new[]{value}); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name,F).Invoke(obj,args); }
    static object New(string name) { return Activator.CreateInstance(T(name),true); }
    static object Packet(string name) { return New("ExaltHelper.Proxy.Networking.Packets."+name); }
    static void Check(bool ok, string name) { if(!ok)throw new Exception(name); checks++; }
    static long Total() { return (long)Get(Get(tracker,"combinedDungeonCombat"),"TotalDamage"); }
    static int RowCount() { return ((ICollection)Get(diagnostics,"rows")).Count; }
    static string[] Csv(string line) {
        var fields=new List<string>(); var field=new StringBuilder(); bool quoted=false;
        for(int i=0;i<line.Length;i++) {
            char c=line[i];
            if(c=='"') { if(quoted && i+1<line.Length && line[i+1]=='"') { field.Append('"'); i++; } else quoted=!quoted; }
            else if(c==',' && !quoted) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString()); return fields.ToArray();
    }
    static byte[] BoostFixture(int length) {
        var bytes=new byte[5+length];
        Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length)),bytes,4);
        bytes[4]=148;
        for(int i=0;i<length;i++) bytes[5+i]=(byte)(i*37);
        return bytes;
    }
    static byte[] Serialize(object packet) {
        using(var stream=new MemoryStream()) {
            var writer=(BinaryWriter)Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.PacketWriter"),new object[]{stream});
            writer.Write(0); writer.Write((byte)Get(packet,"Id"));
            Call(packet,"Write",writer); writer.Write((byte[])Get(packet,"ExtraBytes"));
            var bytes=stream.ToArray(); Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length)),bytes,4); return bytes;
        }
    }
    static void Hit(ushort bullet) {
        var hit=Packet("EnemyHitPacket"); Set(hit,"TargetId",100); Set(hit,"OwnerId",900); Set(hit,"BulletId",bullet);
        Call(tracker,"OnEnemyHit",hit);
    }
    static void Save(string directory) { Call(diagnostics,"Save",directory); }
    public static void Run(string path) {
        path=Path.GetFullPath(path); assembly=Assembly.LoadFrom(path); standalone=assembly.GetName().Name=="ExaltHUD";
        Directory.SetCurrentDirectory(Path.GetDirectoryName(path));
        var items=T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null,Activator.CreateInstance(items.FieldType,new object[]{Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType)}));
        client=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string name in new[]{"Enemies","Entities","Portals"}) Set(client,name,Activator.CreateInstance(client.GetType().GetField(name,F).FieldType));
        player=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));
        Set(player,"ObjectId",100); Set(player,"ObjectType",(ushort)768); Set(player,"PlayerName","DiagnosticTester");
        Set(player,"Inventory",new[]{17842,21228,14471,3813}); Set(player,"Vitality",92); Set(player,"Attack",50);
        Set(player,"Effects2",unchecked((int)0x80000000));
        Set(client,"ClientId",100); Set(client,"Player",player);
        tracker=Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"),new object[]{client});
        Set(client,standalone?"Dps":"dpsTracker",tracker);
        if(standalone) Set(client,"hasMap",true);
        diagnostics=Get(tracker,"damageDiagnostics");
        var enemy=FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));
        Set(enemy,"ObjectId",900); Set(enemy,"Defense",25); ((IDictionary)Get(client,"Enemies"))[900]=enemy;
        var scaling=T("ExaltHelper.Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml",F).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),standalone?"ExaltHUD_Data":"ExaltHelper_Data","Objects.xml")});
        scaling.GetField("isInitialized",F).SetValue(null,true);

        foreach(int length in new[]{0,1,7,8,9,16}) {
            byte[] bytes=BoostFixture(length);
            var packet=T("ExaltHelper.Proxy.Networking.Packet").GetMethod("Create",F).Invoke(null,new object[]{bytes});
            Check(packet.GetType()==T("ExaltHelper.Proxy.Networking.Packets.DamageBoostPacket"),"Boost message must be recognized");
            Check(Serialize(packet).SequenceEqual(bytes),"Boost message must be forwarded byte-for-byte at length "+length);
            int before=RowCount();
            Call(client,standalone?"Process":"RoutePacket",packet);
            Check(RowCount()==before+(length==8?1:0),"Only the known eight-byte boost payload may enter diagnostics");
            Check(Total()==0 && (standalone || (bool)Get(packet,"Send")),"Observing boost messages must not change totals or suppress the packet");
        }
        if(standalone) {
            var isTracked=T("ExaltHelper.Proxy.Networking.Packet").GetMethod("IsTracked",F);
            Check((bool)isTracked.Invoke(null,new object[]{(byte)148,true}) && !(bool)isTracked.Invoke(null,new object[]{(byte)148,false}),"Passive capture must track boost messages in the incoming direction only");
        }

        var shot=Packet("PlayerShootServerPacket"); Set(shot,"OwnerId",100); Set(shot,"BulletId",(short)300);
        Set(shot,"ContainerType",17966); Set(shot,"Damage",(short)6); Set(shot,"BulletCount",(byte)2);
        Call(tracker,"OnPlayerShootServer",shot);
        Check(RowCount()==3 && Total()==0,"Both local cloak pellets must be recorded at creation without adding damage");
        Set(player,"Vitality",102); Set(player,"Effects2",0); Set(player,"Inventory",new[]{17842,2650,14471,3813});
        Hit(300); Check(Total()==273,"Diagnostics must preserve the cloak base, scaling snapshot and target defense handling");
        Set(enemy,"Effects",0x1000000); Hit(301); Check(Total()==273,"Zero-damage observations must not add damage");
        Hit(99); Check(Total()==273,"Untracked hits must be visible without manufacturing damage");
        int localRows=RowCount(); Set(shot,"OwnerId",200); Call(tracker,"OnPlayerShootServer",shot);
        Check(RowCount()==localRows,"Other players' raw projectile shots must not fill the local combat log");

        string directory=Path.Combine(Path.GetTempPath(),"ExaltDiagnosticsTest-"+Guid.NewGuid().ToString("N"));
        string latest=Path.Combine(directory,"dps-latest.csv"), history=Path.Combine(directory,"History");
        try {
            Save(directory);
            var lines=File.ReadAllLines(latest); var header=Csv(lines[2]);
            var row=lines.Skip(3).Select(Csv).Single(r=>r[1]=="local_estimate");
            Check(lines.Skip(3).Select(Csv).All(r=>r.Length==header.Length),"Every diagnostic event must match the CSV schema");
            Check(row[Array.IndexOf(header,"origin_vitality")]=="92" && row[16]=="102","Shot-time VIT and hit-time VIT must remain distinguishable");
            Check(row[Array.IndexOf(header,"origin_scaling_stat")]=="92" && row[Array.IndexOf(header,"origin_ability_item")]=="21228","The originating cloak and scaling snapshot must survive a gear swap");
            Check(row[Array.IndexOf(header,"origin_effects2")]=="-2147483648" && row[21]=="0","All condition bits must survive after the effect expires");
            var boost=lines.Skip(3).Select(Csv).Single(r=>r[1]=="combat_boost");
            Check(boost[Array.IndexOf(header,"combat_boost_payload")]=="00-25-4A-6F-94-B9-DE-03","Boost payload must be recorded without reinterpretation");
            Check(lines.Any(l=>l.Contains("local_zero")) && lines.Any(l=>l.Contains("untracked_hit")),"Zero and missing projectile hits must remain distinguishable");
            Check(!File.ReadAllText(latest).Contains("DiagnosticTester"),"Diagnostics must omit character names");
            string first=Directory.GetFiles(history,"*.csv").Single(); string firstContents=File.ReadAllText(first);
            Save(directory); Check(Directory.GetFiles(history,"*.csv").Length==1,"Periodic saves must update the existing dungeon log");
            Call(diagnostics,"Reset"); Save(directory);
            Check(File.ReadAllText(latest)==firstContents && Directory.GetFiles(history,"*.csv").Length==1,"An empty map must retain the completed log");
            Call(diagnostics,"Record","second-fight"); Save(directory);
            Check(File.ReadAllText(first)==firstContents && Directory.GetFiles(history,"*.csv").Length==2,"A subsequent fight must not overwrite earlier evidence");
            string unrelated=Path.Combine(history,"dps-user-saved.csv"); File.WriteAllText(unrelated,"preserve me");
            for(int i=0;i<31;i++) { Call(diagnostics,"Reset"); Call(diagnostics,"Record","retention-fixture-"+i); Save(directory); }
            Check(Directory.GetFiles(history,"*.csv").Length==31 && File.ReadAllText(unrelated)=="preserve me","Retention must keep 30 generated logs and preserve unrelated files");
            Check(File.ReadAllText(latest).Contains("retention-fixture-30"),"Retention must leave the newest trace available");
            Save(latest); Check(Total()==273,"An unwritable diagnostic path must not change totals");
            Call(diagnostics,"Reset"); Call(diagnostics,"Record","background-fixture");
            Call(diagnostics,"SaveInBackground",true,directory);
            for(int i=0;i<300 && (bool)Get(diagnostics,"savePending");i++)Thread.Sleep(10);
            Check(!(bool)Get(diagnostics,"savePending") && File.ReadAllText(latest).Contains("background-fixture"),"Background saves must also preserve the current trace");
        } finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("PASS: "+checks+" combat diagnostic, packet forwarding and history assertions.");
    }
}
'@
try { [CombatDiagnosticsRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
