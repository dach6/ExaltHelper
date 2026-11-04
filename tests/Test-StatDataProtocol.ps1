param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/stat-protocol"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = $testRoot
Add-Type -ReferencedAssemblies System.Core -TypeDefinition @'
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

public static class StatDataProtocolRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object o, string name) { var f = o.GetType().GetField(name, F); return f != null ? f.GetValue(o) : o.GetType().GetProperty(name, F).GetValue(o, null); }
    static void Set(object o, string name, object value) { var f = o.GetType().GetField(name, F); if (f != null) f.SetValue(o, value); else o.GetType().GetProperty(name, F).SetValue(o, value, null); }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, F).Invoke(o, args); }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static byte[] Bytes(string hex) { return hex.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray(); }
    static byte[] Serialize(object value)
    {
        using (var stream = new MemoryStream())
        using (var writer = (BinaryWriter)Activator.CreateInstance(T("Proxy.Networking.PacketWriter"), new object[] { stream }))
        { Call(value, "Write", writer); return stream.ToArray(); }
    }
    static object ReadStat(byte[] bytes, out long position)
    {
        using (var stream = new MemoryStream(bytes))
        using (var reader = (BinaryReader)Activator.CreateInstance(T("Proxy.Networking.PacketReader"), new object[] { stream }))
        { var stat = New("Proxy.Networking.Packets.DataObjects.StatData"); Call(stat, "Read", reader); position = stream.Position; return stat; }
    }
    static void GoldenValues()
    {
        // Independent protocol vectors: first byte has six data bits, sign bit 0x40,
        // continuation bit 0x80; subsequent bytes have seven data bits.
        int[] values = { 0, 1, 63, 64, 65, 127, 128, 200, 255, 256, 8191, 8192, 65535, int.MaxValue, -1, -63, -64, int.MinValue };
        string[] encoded = { "00", "01", "3F", "80 01", "81 01", "BF 01", "80 02", "88 03", "BF 03", "80 04", "BF 7F", "80 80 01", "BF FF 07", "BF FF FF FF 0F", "41", "7F", "C0 01", "C0 80 80 80 10" };
        for (int i = 0; i < values.Length; i++)
        {
            byte[] numeric = Bytes("74 2A " + encoded[i]); long position;
            object stat = ReadStat(numeric.Concat(new byte[] { 0xE7 }).ToArray(), out position);
            Check(Convert.ToInt32(Get(stat, "SecondaryValue")) == values[i], "Numeric secondary value decoded incorrectly: " + values[i]);
            Check(position == numeric.Length, "Secondary value consumed bytes from the next stat: " + values[i]);
            Check(Serialize(stat).SequenceEqual(numeric), "Numeric stat forwarding changed wire bytes: " + values[i]);
            Check(Convert.ToInt32(Get(Call(stat, "Clone"), "SecondaryValue")) == values[i], "Clone truncated secondary value: " + values[i]);
            byte[] text = Bytes("1F 00 02 4F 4B " + encoded[i]);
            stat = ReadStat(text.Concat(new byte[] { 0xE7 }).ToArray(), out position);
            Check((string)Get(stat, "StringValue") == "OK" && Convert.ToInt32(Get(stat, "SecondaryValue")) == values[i] && position == text.Length,
                "String stat secondary value or alignment is wrong: " + values[i]);
            Check(Serialize(stat).SequenceEqual(text), "String stat forwarding changed wire bytes: " + values[i]);
        }
        bool truncated = false;
        try { long ignored; ReadStat(Bytes("74 2A 80"), out ignored); }
        catch (TargetInvocationException ex) { truncated = ex.InnerException is EndOfStreamException; }
        Check(truncated, "Truncated compressed secondary value was silently accepted");
        long end;
        byte[] ritualBytes = Bytes("9B 00 10").Concat(Encoding.UTF8.GetBytes("5027802235043840")).Concat(Bytes("41")).ToArray();
        object ritual = ReadStat(ritualBytes, out end);
        Check((string)Get(ritual, "StringValue") == "5027802235043840" && end == ritualBytes.Length,
            "Blood Ritual stat155 was not decoded as a string identifier");
        Check(Serialize(ritual).SequenceEqual(ritualBytes), "Blood Ritual identifier forwarding changed wire bytes");
    }
    static byte[] Status()
    {
        return Bytes("A4 01 00 00 00 00 00 00 00 00 03 74 2A 88 03 1A 9C 01 41 1F 00 07 46 69 78 74 75 72 65 80 04");
    }
    static byte[] Frame(byte id, byte[] payload)
    {
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        { writer.Write(IPAddress.HostToNetworkOrder(payload.Length + 5)); writer.Write(id); writer.Write(payload); return stream.ToArray(); }
    }
    static void FullPackets()
    {
        byte[] tick = Bytes("00 00 00 01 00 00 00 64 00 00 00 02 00 2A 00 01").Concat(Status()).Concat(Bytes("DE AD")).ToArray();
        byte[] update = Bytes("00 00 00 00 00 00 00 00 00 00 01 03 00").Concat(Status()).Concat(Bytes("01 A4 01 DE AD")).ToArray();
        foreach (var fixture in new[] { Tuple.Create((byte)10, tick, "NewTickPacket"), Tuple.Create((byte)42, update, "UpdatePacket") })
        {
            object packet = T("Proxy.Networking.Packet").GetMethod("Create", F).Invoke(null, new object[] { Frame(fixture.Item1, fixture.Item2) });
            Check(packet.GetType().Name == fixture.Item3, "Large secondary value forced a valid " + fixture.Item3 + " into raw fallback");
            object status = fixture.Item1 == 10 ? ((IList)Get(packet, "Statuses"))[0] : Get(((Array)Get(packet, "NewObjects")).GetValue(0), "Stats");
            var stats = (IList)Get(status, "StatList");
            Check(stats.Count == 3 && (int)Get(stats[1], "Value") == 92 && (string)Get(stats[2], "StringValue") == "Fixture", "Compressed secondary shifted following numeric/string stats");
            Check(Convert.ToInt32(Get(stats[0], "SecondaryValue")) == 200 && Convert.ToInt32(Get(stats[2], "SecondaryValue")) == 256, "Packet lost large stack counts");
            Check(((byte[])Call(packet, "GetPayloadBytes")).SequenceEqual(fixture.Item2), "Parsed packet forwarding changed payload or trailing bytes");
            if (fixture.Item1 == 42) Check(((int[])Get(packet, "Drops")).SequenceEqual(new[] { 100 }), "Secondary stat shifted following object-drop data");
        }
    }
    static void GeneratedSentinels()
    {
        var items = T("Proxy.DataStructures.GameData").GetField("Items", F);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType) }));
        object client = FormatterServices.GetUninitializedObject(T("Proxy.Client"));
        object player = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { 100 }); Set(client, "Player", player);
        var antiDebuff = Activator.CreateInstance(T("Proxy.Mods.AntiDebuffMod"), new object[] { client });
        Check(Serialize(Call(antiDebuff, "CreatePrimaryEffectsStat")).SequenceEqual(Bytes("1D 00 41")), "Primary effects generated sentinel changed its prior wire41");
        Check(Serialize(Call(antiDebuff, "CreateSecondaryEffectsStat")).SequenceEqual(Bytes("60 00 41")), "Secondary effects generated sentinel changed its prior wire41");
        var objects = T("Proxy.DataStructures.GameData").GetField("Objects", F);
        objects.SetValue(null, Activator.CreateInstance(objects.FieldType, new object[] { Activator.CreateInstance(objects.FieldType.GetProperty("Map").PropertyType) }));
        Set(client, "Portals", Activator.CreateInstance(client.GetType().GetField("Portals", F).FieldType));
        ((IDictionary)Get(client, "Portals"))[100] = player;
        var antiLag = Activator.CreateInstance(T("Proxy.Mods.AntiLagMod"), new object[] { client });
        ((IDictionary)Get(antiLag, "_originalObjectSizes"))[100] = 100;
        Set(antiLag, "_allyPlayerSize", -1);
        object settings = T("Settings").GetProperty("Default", F).GetValue(null, null);
        int originalSize = (int)Get(settings, "AntiLagAllyPlayerSize"); Set(settings, "AntiLagAllyPlayerSize", 100);
        try
        {
            object tick = New("Proxy.Networking.Packets.NewTickPacket");
            Set(tick, "Statuses", Activator.CreateInstance(tick.GetType().GetField("Statuses", F).FieldType));
            Call(antiLag, "OnNewTick", tick);
            var statuses = (IList)Get(tick, "Statuses");
            Check(statuses.Count == 1, "Display-size fixture did not generate a status");
            Check(Serialize(((IList)Get(statuses[0], "StatList"))[0]).SequenceEqual(Bytes("02 A4 01 41")), "Generated display-size sentinel changed its prior wire41");
        }
        finally { Set(settings, "AntiLagAllyPlayerSize", originalSize); }
        var potion = Activator.CreateInstance(T("Proxy.DataStructures.PotionInfo"), new object[] { 42, 256 });
        Check(Convert.ToInt32(Get(potion, "Quantity")) == 256, "Potion stack model truncated the decoded count");
    }
    public static void Run(string path, string root)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path)); Directory.SetCurrentDirectory(root);
        GoldenValues(); FullPackets(); GeneratedSentinels();
        Console.WriteLine("PASS: " + checks + " compressed-secondary stat, packet forwarding and generated-sentinel assertions.");
    }
}
'@
try { [StatDataProtocolRegression]::Run($AssemblyPath, $testRoot) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
