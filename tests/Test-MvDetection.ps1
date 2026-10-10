param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$SniffLog = "$PSScriptRoot/fixtures/moonlight-dialogue.txt"
)
$ErrorActionPreference = 'Stop'
# Run with Windows PowerShell (the application targets .NET Framework 4.8).
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

public static class MvDetectionRegression
{
    static Assembly assembly;
    static Type trackerType;
    static object tracker;
    static IDictionary enemies;
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object New(string name) { return Activator.CreateInstance(assembly.GetType(name), true); }
    static void Field(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
    static object Call(string name, params object[] args) { return trackerType.GetMethod(name, Flags).Invoke(tracker, args); }
    static object Property(string name) { return trackerType.GetProperty(name, Flags).GetValue(tracker, null); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static bool Umi { get { return (bool)Property("IsUmiMode"); } }
    static void Dialogue(string speaker, string text) { Call("ParseBattleDialogue", speaker, text); }
    static void Reset()
    {
        Call("ResetCounters", true);
        enemies.Clear();
        trackerType.GetProperty("IsInMoonlightVillage").GetSetMethod(true).Invoke(tracker, new object[] { true });
    }
    static void Enemy(int type)
    {
        object enemy = FormatterServices.GetUninitializedObject(assembly.GetType("ExaltHelper.Proxy.DataStructures.MapObject"));
        Field(enemy, "ObjectType", (ushort)type);
        Field(enemy, "ObjectId", type);
        enemies[type] = enemy; // Zero condition bits reproduces a spawn before its first status tick.
    }
    static void Update() { Call("OnUpdate", New("ExaltHelper.Proxy.Networking.Packets.UpdatePacket")); }
    public static void Run(string path, string log)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        trackerType = assembly.GetType("ExaltHelper.Proxy.Mods.MoonlightVillageMod");
        trackerType.GetProperty("EnableChatNotifications").SetValue(null, false, null);
        FieldInfo items = assembly.GetType("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        object emptyItems = Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { emptyItems }));
        Type clientType = assembly.GetType("ExaltHelper.Proxy.Client");
        object client = FormatterServices.GetUninitializedObject(clientType);
        Field(client, "Enemies", Activator.CreateInstance(clientType.GetField("Enemies").FieldType));
        enemies = (IDictionary)clientType.GetField("Enemies").GetValue(client);
        tracker = Activator.CreateInstance(trackerType, new object[] { client });

        foreach (int type in new int[] { 20493, 20601, 49339 })
        {
            Reset(); Enemy(type); Update();
            Check(!Umi, "Early object must not activate Kitsune: " + type);
        }
        Reset();
        Dialogue("#Village Girl Umi", "I saw how you did out there!");
        Dialogue("#Kitsune Umi", "So let's begin!");
        Dialogue("Player", "This concludes the Moonlight Dance.");
        Check(!Umi && (int)Property("DancersCompleted") == 0, "Early dialogue/player chat changed state");
        Field(tracker, "currentPhaseIndex", 10);
        Dialogue("#Kitsune Umi", "So let's begin!");
        Enemy(20493); Update();
        Check(!Umi, "Finale start must not count as completion");

        Reset();
        Dialogue("#Sage Genji", "This concludes the Moonlight Dance.");
        Dialogue("#Village Girl Umi", "I saw how you did out there!");
        Check(!Umi, "Village Girl conversation must not activate Kitsune");
        Enemy(49339); Update();
        Check(!Umi, "Completion prop must never activate Kitsune");
        Dialogue("Player", "So let's begin!");
        Dialogue("#Drummer Kaguya", "Goddess of Revelry");
        Check(!Umi, "Generic phrases must not activate Kitsune");
        Dialogue("#Kitsune Umi", "So let's begin!");
        Check(Umi && (int)Property("CurrentPhaseNumber") == 1, "Genuine Kitsune start was missed");
        Field(tracker, "currentPhaseIndex", 3);
        Dialogue("#Kitsune Umi", "It's a lovely festival night, won't you play with me?");
        Dialogue("#Sage Genji", "This concludes the Moonlight Dance.");
        Enemy(20450); Update();
        Check(Umi && (int)Property("CurrentPhaseNumber") == 4 && (string)Property("CurrentDancer") == "Umi",
            "Repeated Kitsune/late dancer events corrupted active encounter");

        foreach (int type in new int[] { 20493, 20601 })
        {
            Reset(); Dialogue("#Sage Genji", "This concludes the Moonlight Dance.");
            Enemy(type); Update(); Check(Umi, "Post-completion boss detection failed");
        }
        Reset();
        for (int phase = 0; phase < 10; phase++) Call("CommitCurrentPhase", 6);
        Enemy(20493); Update(); Check(!Umi, "Rewards activated Kitsune before the finale ended");
        Call("CommitCurrentPhase", 6);
        Check((int)Property("TotalFlames") == 66, "Village score did not accumulate");
        Update();
        Check(Umi && (int)Property("TotalFlames") == 0 && (int)Property("Boss3Flames") == 30,
            "Kitsune activation must preserve village rewards and begin a separate score");
        Reset(); Call("ActivateUmiMode"); Check(Umi, "Manual override no longer works");
        Call("ResetCounters", true); Check(!Umi, "Full reset retained Umi mode");

        Reset();
        Dialogue("#Dancer Miko", "Our guests seem weary of the troubles behind them.");
        Call("CommitCurrentPhase", 6); Call("CommitCurrentPhase", 8);
        Dialogue("#Sage Genji", "Tonight's festival is filled with fervour!");
        for (int phase = 0; phase < 4; phase++) Call("CommitCurrentPhase", 7);
        Dialogue("#Drummer Kaguya", "Watching the others dance before me has revitalized my soul.");
        Enemy(20452);
        for (int update = 0; update < 10000; update++) Update();
        var announcements = (System.Collections.Generic.HashSet<string>)trackerType.GetField("announcedDancerEncounters", Flags).GetValue(tracker);
        Check(announcements.Count == 3 && announcements.Contains("3:Kaguya"), "Repeated boss updates created extra announcements");
        Check((int)Property("TotalFlames") == 42 && (int)Property("CurrentPhaseNumber") == 7,
            "Repeated boss updates altered score or phase");
        // Even if the display label is cleared, re-observation must not announce again.
        trackerType.GetProperty("CurrentDancer").GetSetMethod(true).Invoke(tracker, new object[] { "Waiting" });
        Update(); Check(announcements.Count == 3, "Re-observation announced the same encounter twice");
        string safeChat = (string)trackerType.GetMethod("ToGameChatText", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "[MV Tracker] \u2694 Boss 3: Kaguya Active! \ud83c\udf38" });
        Check(safeChat.Contains("Boss 3: Kaguya Active!") && Regex.IsMatch(safeChat, "^[\\x00-\\x7F]*$"),
            "Game chat still contains unsupported glyphs");
        Reset(); Check(announcements.Count == 0, "New run did not reset announcement deduplication");

        int sessions = 0, lines = 0;
        if (File.Exists(log))
        {
            bool completed = false, started = false;
            foreach (string line in File.ReadLines(log))
            {
                if (line.Contains("[MV SNIFFER SESSION STARTED]"))
                {
                    Reset(); completed = false; started = false; sessions++;
                    Enemy(20493); Enemy(49339); Update();
                    Check(!Umi, "Replay introduction incorrectly activated Kitsune");
                    enemies.Clear();
                }
                Match m = Regex.Match(line, "\\[DIALOGUE\\] From: '([^']*)' \\| Text: \"(.*?)\" \\| Clean:");
                if (!m.Success) continue;
                string speaker = m.Groups[1].Value, text = m.Groups[2].Value;
                if (speaker == "#Sage Genji" && text.Contains("This concludes the Moonlight Dance")) completed = true;
                if (speaker == "#Kitsune Umi" && completed) started = true;
                Dialogue(speaker, text); lines++;
                Check(Umi == started, "Dialogue replay diverged: " + line);
            }
        }
        Console.WriteLine("PASS: " + checks + " assertions; " + lines + " recorded dialogue events across " + sessions + " sessions.");
    }
}
'@
try { [MvDetectionRegression]::Run($AssemblyPath, $SniffLog) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
