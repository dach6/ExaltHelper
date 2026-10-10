param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
public static class AbilityPacketRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly assembly;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper.Proxy.Networking." + name, true); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Int(BinaryWriter writer, int value) { writer.Write(IPAddress.HostToNetworkOrder(value)); }
    static void Short(BinaryWriter writer, short value) { writer.Write(IPAddress.HostToNetworkOrder(value)); }
    static void Float(BinaryWriter writer, float value) { byte[] bytes = BitConverter.GetBytes(value); Array.Reverse(bytes); writer.Write(bytes); }
    // Independent wire fixture, including the original optional-field boundaries.
    static byte[] Fixture(int item, int optionalBytes, byte count, bool extension)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            Int(writer, 33 + optionalBytes + (extension ? 3 : 0)); writer.Write((byte)12);
            Short(writer, 300); Int(writer, 100); Int(writer, item);
            Float(writer, 1.25f); Float(writer, -3.5f); Float(writer, 0.75f);
            Short(writer, 700); Int(writer, 0);
            if (optionalBytes >= 1) writer.Write((byte)2);
            if (optionalBytes >= 2) writer.Write(count);
            if (optionalBytes >= 6) Float(writer, 0.25f);
            if (extension) writer.Write(new byte[] { 0x91, 0x00, 0xE7 });
            return stream.ToArray();
        }
    }
    static object Parse(byte[] bytes) { return T("Packet").GetMethod("Create", F).Invoke(null, new object[] { bytes }); }
    static byte[] Serialize(object packet)
    {
        using (var stream = new MemoryStream())
        using (var writer = (BinaryWriter)Activator.CreateInstance(T("PacketWriter"), new object[] { stream }))
        {
            writer.Write(0); writer.Write((byte)T("Packet").GetField("Id", F).GetValue(packet));
            packet.GetType().GetMethod("Write", F).Invoke(packet, new object[] { writer });
            writer.Write((byte[])T("Packet").GetField("ExtraBytes", F).GetValue(packet));
            byte[] bytes = stream.ToArray();
            byte[] length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
            Array.Copy(length, bytes, 4);
            return bytes;
        }
    }
    public static void Run(string path)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        // Swarmlord, its side shots/shiny, and Primal Arcana normal/proc/shiny.
        foreach (int item in new int[] { 0x7b7c, 0x79e8, 0x1d27, 0x3a90, 0x5625, 0x7f9 })
        foreach (int optionalBytes in new int[] { 0, 1, 2, 6 })
        {
            byte[] input = Fixture(item, optionalBytes, 8, false);
            object packet = Parse(input);
            Check(packet.GetType() == T("Packets.PlayerShootServerPacket"), "Ability packet fell back to raw forwarding");
            byte[] output = Serialize(packet);
            Check(input.SequenceEqual(output), "Projectile packet changed for item " + item.ToString("X") +
                " with " + optionalBytes + " optional bytes: " + input.Length + " -> " + output.Length + " bytes");
            byte count = (byte)packet.GetType().GetProperty("BulletCount").GetValue(packet, null);
            Check(count == (optionalBytes < 2 ? 1 : 8), "Missing projectile count must mean one shot");
        }
        foreach (byte count in new byte[] { 0, 1, 16, 255 })
        {
            byte[] input = Fixture(0x3a90, 6, count, true);
            Check(input.SequenceEqual(Serialize(Parse(input))), "Explicit count or unknown trailing bytes changed");
        }
        // Reusing an instance must not retain optional values from the previous packet.
        object reused = Parse(Fixture(0x3a90, 6, 16, false));
        byte[] shortPacket = Fixture(0x7b7c, 0, 1, false);
        using (var stream = new MemoryStream(shortPacket))
        using (var reader = (BinaryReader)Activator.CreateInstance(T("PacketReader"), new object[] { stream }))
        {
            stream.Position = 5;
            reused.GetType().GetMethod("Read").Invoke(reused, new object[] { reader });
        }
        Check(shortPacket.SequenceEqual(Serialize(reused)), "Optional fields leaked between reads");
        Console.WriteLine("PASS: " + checks + " ability packet assertions.");
    }
}
'@
try { [AbilityPacketRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
