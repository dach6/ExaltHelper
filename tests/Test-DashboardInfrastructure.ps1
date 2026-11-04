param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/dashboard-infrastructure"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = $testRoot
Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms, System.Core -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class DashboardInfrastructureRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
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
    static object Call(object o, string method, params object[] args) { return o.GetType().GetMethod(method, F).Invoke(o, args); }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static void Int(BinaryWriter w, int value) { w.Write(IPAddress.HostToNetworkOrder(value)); }
    static void Short(BinaryWriter w, short value) { w.Write(IPAddress.HostToNetworkOrder(value)); }
    static void WireString(BinaryWriter w, string text) { byte[] utf8 = Encoding.UTF8.GetBytes(text); Short(w, (short)utf8.Length); w.Write(utf8); }
    static byte[] Packet(byte id, byte[] payload)
    {
        using (var s = new MemoryStream()) using (var w = new BinaryWriter(s))
        { Int(w, payload.Length + 5); w.Write(id); w.Write(payload); return s.ToArray(); }
    }
    static byte[] TextPayload()
    {
        using (var s = new MemoryStream()) using (var w = new BinaryWriter(s))
        {
            WireString(w, "FixturePlayer"); Int(w, 123); Short(w, 42); w.Write((byte)5);
            WireString(w, "FixtureRecipient"); WireString(w, "Fixture message"); WireString(w, "Fixture message");
            w.Write(false); Int(w, 0); w.Write(new byte[] { 0x91, 0xFF, 0x33 });
            return s.ToArray();
        }
    }
    static object Parse(byte[] bytes) { return T("Proxy.Networking.Packet").GetMethod("Create", F).Invoke(null, new object[] { bytes }); }
    static byte[] SerializePayload(object packet)
    {
        using (var s = new MemoryStream())
        using (var w = (BinaryWriter)Activator.CreateInstance(T("Proxy.Networking.PacketWriter"), new object[] { s }))
        {
            Call(packet, "Write", w); w.Write((byte[])Get(packet, "ExtraBytes")); return s.ToArray();
        }
    }
    static void PacketObservation()
    {
        foreach (byte[] bytes in new[] { Packet(44, TextPayload()), Packet(251, new byte[] { 0, 1, 0xFF, 0xEE }), Packet(44, new byte[] { 0, 15, 1 }) })
        {
            byte[] original = (byte[])bytes.Clone();
            object packet = Parse(bytes);
            byte[] observed = (byte[])Call(packet, "GetPayloadBytes");
            Check(observed.SequenceEqual(original.Skip(5)), "HUD observer did not receive original complete payload");
            Check(SerializePayload(packet).SequenceEqual(original.Skip(5)), "Reading HUD payload changed packet forwarding");
            Check(bytes.SequenceEqual(original), "Packet observation mutated the receive buffer");
            if (observed.Length > 0) observed[0] ^= 0xFF;
            Check(((byte[])Call(packet, "GetPayloadBytes")).SequenceEqual(original.Skip(5)), "Observer can mutate another observer's payload");
            Check(SerializePayload(packet).SequenceEqual(original.Skip(5)), "Observer payload mutation corrupted forwarding");
        }
        // A typed packet is editable by existing mods; observers must not mutate it.
        object text = Parse(Packet(44, TextPayload()));
        Set(text, "Text", "Changed by existing mod");
        byte[] before = SerializePayload(text);
        Call(text, "GetPayloadBytes");
        Check(SerializePayload(text).SequenceEqual(before), "Observation reset an existing mod's packet changes");
    }
    static byte[] MapPayload(byte[] suffix)
    {
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        {
            Int(writer, 100); Int(writer, 80); WireString(writer, "Realm of the Mad God"); WireString(writer, "Realm"); WireString(writer, "Cyclops");
            Int(writer, 12345); Int(writer, 0); Int(writer, 0x3FC00000); // Independent big-endian IEEE754 1.5f.
            writer.Write(true); writer.Write(false); writer.Write(true); Short(writer, 85); Int(writer, 1234); WireString(writer, "FixtureVersion");
            Short(writer, 7); Short(writer, 18); writer.Write(false); Int(writer, 0x123456); WireString(writer, "CHEF;GENEROUS|S"); Short(writer, -1);
            writer.Write(suffix); return stream.ToArray();
        }
    }
    static byte[] Scores(int maximum, int current)
    {
        // RealmHound map_info.rs and the independent pyrelay packet reader put
        // MAXIMUM before CURRENT. This fixture does not use PacketWriter.
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        { Int(writer, maximum); Int(writer, current); return stream.ToArray(); }
    }
    static void MapScores()
    {
        foreach (int[] pair in new[] { new[] { 1000, 250 }, new[] { 0, 0 }, new[] { -1, -1 } })
        {
            byte[] payload = MapPayload(Scores(pair[0], pair[1]).Concat(new byte[] { 0xFF, 0x21, 0xA4 }).ToArray());
            object map = Parse(Packet(92, payload));
            Check(map.GetType().Name == "MapInfoPacket", "Valid map fixture fell back to an untyped packet");
            Check((int)Get(map, "MaxRealmScore") == pair[0] && (int)Get(map, "CurrentRealmScore") == pair[1], "MapInfo reversed the maximum/current realm-score wire order");
            Check(SerializePayload(map).SequenceEqual(payload), "MapInfo changed score/sentinel presence or trailing bytes while forwarding");
        }
        for (int length = 0; length < 8; length++)
        {
            byte[] tail = Enumerable.Range(1, length).Select(i => (byte)i).ToArray(), payload = MapPayload(tail);
            object map = Parse(Packet(92, payload));
            Check((int)Get(map, "MaxRealmScore") == -1 && (int)Get(map, "CurrentRealmScore") == -1, "Incomplete optional score pair was presented as observed realm data");
            Check(SerializePayload(map).SequenceEqual(payload), "Incomplete optional score tail was lost or rewritten");
        }
        object edited = Parse(Packet(92, MapPayload(new byte[0]))); Set(edited, "MaxRealmScore", 1000);
        Check(SerializePayload(edited).SequenceEqual(MapPayload(Scores(1000, -1))), "Explicit score edits emitted a malformed half pair");
        object observed = Parse(Packet(92, MapPayload(Scores(1000, 250))));
        T("HudJournal").GetMethod("ObservePacket", F).Invoke(null, new object[] { null, observed, false });
        Check((int)T("HudJournal").GetProperty("RealmScore", F).GetValue(null, null) == 250 && (int)T("HudJournal").GetProperty("MaxRealmScore", F).GetValue(null, null) == 1000,
            "Wire map score did not reach the HUD as a 25-percent realm");
    }
    static object Storage(string method, Type type, params object[] args)
    {
        return T("HudStorage").GetMethod(method, F).MakeGenericMethod(type).Invoke(null, args);
    }
    static void Persistence(string root)
    {
        object fallback = Activator.CreateInstance(T("HudPreferences"), true);
        Check(ReferenceEquals(Storage("Load", fallback.GetType(), "missing.json", fallback), fallback), "Missing settings did not use defaults");
        Set(fallback, "DesktopX", -1550); Set(fallback, "DesktopY", -100);
        Set(fallback, "DesktopWidth", 1150); Set(fallback, "DesktopHeight", 780);
        Set(fallback, "OverlayX", 600); Set(fallback, "OverlayY", 30); Set(fallback, "OverlayLocked", true);
        Set(fallback, "Page", "missions");
        Storage("Save", fallback.GetType(), "fixture-layout.json", fallback);
        object loaded = Storage("Load", fallback.GetType(), "fixture-layout.json", Activator.CreateInstance(fallback.GetType(), true));
        Check((int)Get(loaded, "DesktopX") == -1550 && (int)Get(loaded, "DesktopY") == -100, "Negative monitor position was not persisted");
        Check((int)Get(loaded, "DesktopWidth") == 1150 && (int)Get(loaded, "DesktopHeight") == 780, "Desktop size did not round-trip");
        Check((int)Get(loaded, "OverlayX") == 600 && (int)Get(loaded, "OverlayY") == 30 && (bool)Get(loaded, "OverlayLocked"), "Compact settings were mixed with desktop settings");
        Check((string)Get(loaded, "Page") == "missions", "Selected page did not round-trip");
        Set(fallback, "Page", "quests"); Storage("Save", fallback.GetType(), "fixture-layout.json", fallback);
        Check(File.Exists(Path.Combine(root, "fixture-layout.json.bak")), "Atomic settings update did not retain previous file");
        Check(!File.Exists(Path.Combine(root, "fixture-layout.json.tmp")), "Successful settings save left an unfinished temporary file");
        File.WriteAllText(Path.Combine(root, "broken.json"), "{ partial");
        Check(ReferenceEquals(Storage("Load", fallback.GetType(), "broken.json", fallback), fallback), "Malformed settings crash or lose defaults");
        Check(File.ReadAllText(Path.Combine(root, "broken.json")) == "{ partial", "Loading corrupt settings destroyed the original file");
        string error = (string)T("HudStorage").GetProperty("LastError", F).GetValue(null, null);
        Check(!string.IsNullOrEmpty(error), "Corrupt settings did not expose a recoverable error");
        Parallel.For(0, 16, i => Storage("Save", typeof(Dictionary<string, int>), "concurrent.json", new Dictionary<string, int> { { "index", i }, { "check", i * 7 } }));
        var concurrent = (Dictionary<string, int>)Storage("Load", typeof(Dictionary<string, int>), "concurrent.json", new Dictionary<string, int>());
        Check(concurrent.Count == 2 && concurrent["check"] == concurrent["index"] * 7, "Concurrent persistence wrote torn JSON");
    }
    static void Table()
    {
        using (var host = new Form { Size = new Size(1000, 600), StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
        using (var page = (Control)Activator.CreateInstance(T("HudTablePage"), new object[] { "Fixture data", new string[] { "Name", "Dungeon", "Damage" } }))
        {
            host.Controls.Add(page); IntPtr handle = host.Handle; host.Show(); host.PerformLayout(); Application.DoEvents();
            var grid = (DataGridView)Get(page, "Grid"); var search = (TextBox)Get(page, "SearchBox");
            var category = (ComboBox)Get(page, "Category"); var period = (ComboBox)Get(page, "Period");
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("HudTableRow")));
            for (int i = 0; i < 90; i++)
            {
                object row = Activator.CreateInstance(T("HudTableRow"), new object[] { i, new object[] { "Player " + i, i % 2 == 0 ? "The Nest" : "Lost Halls", i * 100L } });
                Set(row, "Category", i % 2 == 0 ? "The Nest" : "Lost Halls");
                Set(row, "Time", DateTime.Now.AddDays(i < 30 ? 0 : i < 60 ? -3 : -40)); list.Add(row);
            }
            Call(page, "SetRows", list); Check(grid.RowCount == 90, "Table omitted input rows");
            search.Text = "  pLaYeR 8  "; Check(grid.RowCount == 11, "Search is not trimmed and case insensitive");
            category.SelectedItem = "The Nest"; Check(grid.RowCount == 6, "Category and text search do not compose");
            period.SelectedIndex = 1; Check(grid.RowCount == 1, "Today filter does not compose with text and category");
            search.Clear(); category.SelectedIndex = 0; Check(grid.RowCount == 30, "Today filter included older records");
            period.SelectedIndex = 2; Check(grid.RowCount == 60, "Last-week filter omitted recent or included old records");
            period.SelectedIndex = 0; grid.CurrentCell = grid.Rows[55].Cells[0]; grid.FirstDisplayedScrollingRowIndex = 40;
            Call(page, "SetRows", list);
            Check((int)grid.CurrentRow.Tag == 55, "Snapshot refresh lost selection");
            Check(grid.FirstDisplayedScrollingRowIndex == 40, "Snapshot refresh reset scroll position");
            search.Text = "missing fixture"; Check(grid.RowCount == 0, "Search miss retained stale rows");
            search.Clear(); Call(page, "BindPreferences", "fixture-table"); category.SelectedItem = "Lost Halls"; period.SelectedIndex = 2;
            using (var restored = (Control)Activator.CreateInstance(T("HudTablePage"), new object[] { "Restored fixture", new[] { "Name", "Dungeon", "Damage" } }))
            {
                Call(restored, "BindPreferences", "fixture-table"); Call(restored, "SetRows", list);
                Check((string)((ComboBox)Get(restored, "Category")).SelectedItem == "Lost Halls" && ((ComboBox)Get(restored, "Period")).SelectedIndex == 2
                    && ((DataGridView)Get(restored, "Grid")).RowCount == 30, "Recreated page did not restore durable category/date filters");
            }
            period.SelectedIndex = 0;
            search.Clear(); category.SelectedItem = "Lost Halls";
            for (int i = list.Count - 1; i >= 0; i--) if (i % 2 == 1) list.RemoveAt(i);
            Call(page, "SetRows", list);
            Check((string)category.SelectedItem == "Lost Halls" && grid.RowCount == 0, "A temporarily missing category discarded the user's persistent filter");
            list.Clear(); Call(page, "SetRows", list);
            Check(grid.RowCount == 0 && (string)category.SelectedItem == "Lost Halls", "Empty snapshot lost a persistent category or retained old rows");
            Check(search.AccessibleName.Length > 0 && category.AccessibleName.Length > 0, "Filtering controls lack accessible names");
        }
    }
    public static void Run(string assemblyPath, string root)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        PacketObservation(); MapScores(); Persistence(root); Table();
        Console.WriteLine("PASS: " + checks + " dashboard packet, persistence and filtering assertions.");
    }
}
'@
try { [DashboardInfrastructureRegression]::Run($AssemblyPath, $testRoot) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
