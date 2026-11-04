param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ([IO.DriveInfo]::new('C:\').AvailableFreeSpace -le 5GB) { throw 'STOP: C: free space is at or below 5 GB.' }
Add-Type -ReferencedAssemblies System.Drawing,System.Windows.Forms,System.Core -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class MissionRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app;
    static int checks;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object value, string name)
    {
        Type t = value as Type ?? value.GetType(); object o = value is Type ? null : value;
        var f = t.GetField(name, F); return f != null ? f.GetValue(o) : t.GetProperty(name, F).GetValue(o, null);
    }
    static void Set(object value, string name, object data)
    {
        Type t = value as Type ?? value.GetType(); object o = value is Type ? null : value;
        var f = t.GetField(name, F); if (f != null) f.SetValue(o, data); else t.GetProperty(name, F).SetValue(o, data, null);
    }
    static object Call(object value, string name, params object[] args)
    {
        Type t = value as Type ?? value.GetType();
        return t.GetMethod(name, F).Invoke(value is Type ? null : value, args);
    }
    static void Check(bool passed, string message) { if (!passed) throw new Exception(message); checks++; }
    static List<object> List(object value) { return ((IEnumerable)value).Cast<object>().ToList(); }
    static object Def(object catalog, int id) { return List(Get(catalog, "Definitions")).First(d => (int)Get(d, "Id") == id); }
    static object Row(List<object> rows, string id) { return rows.First(r => (string)Get(r, "Id") == id); }
    static HashSet<string> TaskIds() { return new HashSet<string>(List(Call(T("MissionWorkspace"), "GetTrackedTasks")).SelectMany(t => (string[])Get(t, "SourceIds"))); }
    static string Status(object catalog, int id) { return (string)Call(catalog, "Status", Def(catalog, id), DateTimeOffset.UtcNow.ToUnixTimeSeconds()); }
    static object Runtime(object catalog, int id) { return ((IDictionary)Get(catalog, "Progress"))["mission:900:" + id]; }
    static int[] Values(object catalog, int id) { return (int[])Get(Runtime(catalog, id), "Values"); }
    static byte[] Payload(Action<BinaryWriter> write) { using (var m = new MemoryStream()) { using (var w = new BinaryWriter(m, Encoding.UTF8, true)) write(w); return m.ToArray(); } }
    static void I16(BinaryWriter w, int n) { w.Write((byte)(n >> 8)); w.Write((byte)n); }
    static void I32(BinaryWriter w, int n) { w.Write((byte)(n >> 24)); w.Write((byte)(n >> 16)); w.Write((byte)(n >> 8)); w.Write((byte)n); }
    static void Text(BinaryWriter w, string s) { byte[] bytes = Encoding.UTF8.GetBytes(s); I16(w, bytes.Length); w.Write(bytes); }
    static void Packet(byte id, bool outgoing, Action<BinaryWriter> write) { Call(T("MissionWorkspace"), "ApplyPayload", id, Payload(write), outgoing); }
    static void Quest(BinaryWriter w, string id, int[] requirements)
    {
        Text(w, id); Text(w, id == "daily" ? "Ocean Trench marks" : "Mixed marks"); Text(w, "Bring marks to the Tinkerer."); Text(w, ""); I32(w, 0); I32(w, 0);
        I16(w, requirements.Length); foreach (int item in requirements) I32(w, item); I16(w, 1); I32(w, 2726); w.Write(false); w.Write(false); w.Write(false);
    }
    static void Store(bool seasonal, params int[] items)
    {
        object storage = New("AccountStorage"); Set(storage, "Seasonal", seasonal); Set(storage, "Items", items);
        object account = Get(T("AccountWorkspace"), "state");
        ((IList)Get(account, "Storage")).Add(storage);
        Set(account, "Revision", (long)Get(account, "Revision") + 1);
    }
    const string Definitions = @"{""seasons"":[{""id"":900,""name"":""Test season"",""current"":1,""missions"":[
        {""id"":1,""name"":""Repeating gate"",""interval"":0.75,""participants"":255,""conds"":[{""type"":3,""target"":""Ocean Trench"",""amount"":3}]},
        {""id"":2,""name"":""Child mission"",""parentsStr"":""1"",""participants"":255,""conds"":[{""type"":3,""target"":""Ocean Trench"",""amount"":2}]},
        {""id"":3,""name"":""Choices plus an AND step"",""participants"":255,""condSpread"":""1112"",""conds"":[{""type"":3,""target"":""Ocean Trench"",""amount"":2},{""type"":3,""target"":""The Nest"",""amount"":2},{""type"":3,""target"":""Lost Halls"",""amount"":2},{""type"":2,""target"":"""",""amount"":5}]},
        {""id"":4,""name"":""Mystic mission"",""participants"":4,""wornRestriction"":[{""type"":""ORB""}],""conds"":[{""type"":2,""amount"":10}]},
        {""id"":5,""name"":""Locked child"",""parentsStr"":""4"",""participants"":2,""conds"":[{""type"":3,""target"":""The Nest"",""amount"":1}]}
    ]}]}";
    const string Baseline = "<Missions><LockedMissions>;900,5</LockedMissions><Season id=\"900\">1,0,0,0,1,1|2,0,0|3,0:2:0:4,0|4,0,0|5,0,0</Season></Missions>";

    public static void Run(string assemblyPath, string fixtures, string screenshots)
    {
        string isolated = Path.Combine(Path.GetTempPath(), "ExaltHelper-Missions-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("EXALTHELPER_HUD_DATA", isolated);
        app = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        object real = Call(T("MissionCatalog"), "Parse", File.ReadAllText(Path.Combine(fixtures, "missions-season52.json")), File.ReadAllText(Path.Combine(fixtures, "missions-season52.xml")));
        Check(List(Get(real, "Definitions")).Count == 36, "Real season fixture lost missions.");
        foreach (object definition in List(Get(real, "Definitions"))) Check((string)Call(real, "Status", definition, DateTimeOffset.UtcNow.ToUnixTimeSeconds()) != "Locked", "Reset repeatable gate locked a real season mission.");
        Check(Status(real, 12) == "In progress", "Gathering Storm was locked behind a reset parent.");

        object model = Call(T("MissionCatalog"), "Parse", Definitions, Baseline);
        Check(Status(model, 2) == "In progress", "Durable claim history did not unlock child.");
        Check(Status(model, 5) == "Locked", "An unstarted locked mission became available.");
        Check(!(bool)Call(Def(model, 3), "Complete", new int[] { 0, 2, 0, 4 }), "OR group ignored an independent AND step.");
        Check((bool)Call(Def(model, 3), "Complete", new int[] { 0, 2, 0, 5 }), "Mixed OR/AND completion was not recognized.");
        Call(model, "ApplyDelta", "pool/#prog/900,3,0:0:0:1|900,2,1|999,1,99|900,2,wrong|900,2,1,extra");
        Check(Values(model, 3)[3] == 5 && Values(model, 2)[0] == 1, "Valid additive deltas or malformed-entry rejection failed.");
        Check(Status(model, 3) == "Ready to claim", "Completed live mission did not show claim reminder.");
        Call(model, "ApplyDelta", "prog/900,2,0"); Check(Values(model, 2)[0] == 1, "Login zero delta erased progress.");
        Call(model, "ApplyDelta", "prog/900,2,2147483647"); Check(Values(model, 2)[0] == int.MaxValue, "Large delta overflowed progress.");
        Call(model, "ApplyDelta", "prog/900,2,-2147483647"); Check(Values(model, 2)[0] == 0, "Negative correction did not clamp safely.");
        Set(Runtime(model, 1), "RawState", 3); Set(Runtime(model, 1), "ClaimedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Check(Status(model, 1).StartsWith("Cooldown"), "Repeatable claim did not start cooldown.");
        Set(Runtime(model, 1), "ClaimedAt", 1L); Check(Status(model, 1) == "In progress" && Status(model, 2) == "In progress", "Cooldown reset re-locked child.");

        object state = Get(T("AccountWorkspace"), "state"); Set(state, "AccountKey", "mission-test-account-a");
        Call(T("MissionWorkspace"), "GetMissions"); Set(T("MissionWorkspace"), "catalog", model); Set(T("MissionWorkspace"), "baselineLoaded", true);
        Set(T("MissionWorkspace"), "characterSeasonal", (bool?)true); Set(T("MissionWorkspace"), "characterCrucible", (bool?)true); Set(T("MissionWorkspace"), "equippedSlots", new int[] { 17, 21, 14, 9 });
        var missions = List(Call(T("MissionWorkspace"), "GetMissions"));
        Check((bool)Get(Row(missions, "mission:900:4"), "Eligible"), "Orb restriction rejected an eligible Mystic.");
        Set(T("MissionWorkspace"), "equippedSlots", new int[] { 17, 11, 14, 9 });
        Check(!(bool)Get(Row(List(Call(T("MissionWorkspace"), "GetMissions")), "mission:900:4"), "Eligible"), "Orb restriction accepted a Spell.");
        Packet(163, true, w => { I32(w, 900); I32(w, 2); w.Write((byte)7); I16(w, -1); });
        Packet(164, false, w => { w.Write((byte)7); w.Write((byte)0); I16(w, 0); });
        Check((int)Get(Runtime(model, 2), "RawState") == 0, "Failed claim acknowledgement marked claimed.");
        Packet(163, true, w => { I32(w, 900); I32(w, 2); w.Write((byte)8); I16(w, -1); });
        Packet(164, false, w => { w.Write((byte)8); w.Write((byte)1); I16(w, 0); });
        Check((int)Get(Runtime(model, 2), "RawState") == 2, "Successful claim acknowledgement was lost.");
        Packet(6, false, w => { I16(w, 2); Quest(w, "daily", new int[] { 7728, 7728, 7728 }); Quest(w, "mixed", new int[] { 7728, 7737 }); I16(w, 0); });
        Store(true, 7728); Store(false, 7728, 7728, 7737);
        var quests = List(Call(T("MissionWorkspace"), "GetQuests"));
        Check((double)Get(Row(quests, "quest:daily"), "Progress") < 1, "Seasonal and regular marks were incorrectly combined.");
        Call(T("MissionWorkspace"), "SetQuestStorage", "Regular");
        Check((string)Get(Row(List(Call(T("MissionWorkspace"), "GetQuests")), "quest:mixed"), "Status") == "Ready to claim", "Complete regular mark set did not show claim reminder.");
        Call(T("MissionWorkspace"), "SetQuestStorage", "Seasonal");
        Check((string)Get(Row(List(Call(T("MissionWorkspace"), "GetQuests")), "quest:mixed"), "Status") == "In progress", "Seasonal filter used regular marks.");
        Packet(58, true, w => Text(w, "mixed")); Packet(96, false, w => { w.Write(true); Text(w, "Accepted"); });
        Check((string)Get(Row(List(Call(T("MissionWorkspace"), "GetQuests")), "quest:mixed"), "Status") == "Claimed", "Successful quest claim stayed active.");
        bool rejected = false; try { Packet(6, false, w => I16(w, -1)); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected && List(Call(T("MissionWorkspace"), "GetQuests")).Count == 2, "Malformed quest list corrupted previous results.");

        Check((bool)Call(T("MissionWorkspace"), "GetAutoTracking", false) && (bool)Call(T("MissionWorkspace"), "GetAutoTracking", true) && (bool)Get(T("MissionWorkspace"), "ShowAllChoices"), "New accounts did not enable automatic tracking and all choices.");
        Check(TaskIds().Contains("mission:900:1") && TaskIds().Contains("quest:daily"), "In-progress missions and daily chests were not tracked automatically.");
        Check(!TaskIds().Contains("mission:900:3") && !TaskIds().Contains("mission:900:5") && !TaskIds().Contains("quest:mixed"), "Automatic tracking included ready, locked, or claimed tasks.");
        object exchange = New("MissionQuest"); Set(exchange, "Id", "exchange"); Set(exchange, "Name", "Potion exchange"); Set(exchange, "Category", 7010); Set(exchange, "Requirements", new int[] { 7737 });
        ((IList)Get(Get(T("MissionWorkspace"), "cache"), "Quests")).Add(exchange);
        Check(!TaskIds().Contains("quest:exchange"), "Automatic quest tracking included a non-daily exchange.");
        Call(T("MissionWorkspace"), "TogglePin", "quest:exchange");
        Check(TaskIds().Contains("quest:exchange"), "Manual quest tracking did not allow an exchange.");
        ((IList)Get(Get(T("MissionWorkspace"), "cache"), "Quests")).Remove(exchange);
        Call(T("MissionWorkspace"), "TogglePin", "mission:900:1");
        Check(!TaskIds().Contains("mission:900:1") && TaskIds().Contains("quest:daily"), "A per-card opt-out disabled the wrong source.");
        Call(T("MissionWorkspace"), "TogglePin", "quest:daily");
        Call(T("MissionWorkspace"), "SetAutoTracking", false, false);
        Call(T("MissionWorkspace"), "TogglePin", "mission:900:5");
        Check(TaskIds().Contains("mission:900:5"), "Explicit tracking under a disabled default did not retain a locked reminder.");
        Call(T("MissionWorkspace"), "SetAutoTracking", false, true);
        Check(TaskIds().Contains("mission:900:1") && !TaskIds().Contains("mission:900:5") && !TaskIds().Contains("quest:daily"), "Mission master switch did not reset only mission overrides.");
        Call(T("MissionWorkspace"), "SetAutoTracking", true, false);
        Set(Runtime(model, 1), "RawState", 3); Set(Runtime(model, 1), "ClaimedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Check(!TaskIds().Contains("mission:900:1"), "An automatic cooldown mission appeared without an active linked target.");
        Call(T("MissionWorkspace"), "SetAutoTracking", true, true);
        Check(TaskIds().Contains("mission:900:1") && List(Call(T("MissionWorkspace"), "GetTrackedTasks")).Any(t => ((string)Get(t, "Title")).Contains("later")), "An automatic cooldown mission did not link to a shared daily quest.");
        Set(Runtime(model, 1), "ClaimedAt", 1L);
        Call(T("MissionWorkspace"), "ToggleHidden", "mission:900:1");
        Call(T("MissionWorkspace"), "SetAutoTracking", false, false); Call(T("MissionWorkspace"), "SetAutoTracking", false, true);
        Check(!TaskIds().Contains("mission:900:1"), "Changing automatic tracking exposed a hidden mission.");
        Call(T("MissionWorkspace"), "ToggleHidden", "mission:900:1");
        Call(T("MissionWorkspace"), "ToggleCategory", "Regular");
        Check(!TaskIds().Contains("mission:900:1") && TaskIds().Contains("quest:daily"), "Automatic tracking ignored hidden categories.");
        Call(T("MissionWorkspace"), "ToggleCategory", "Regular");
        object incoming = Call(T("MissionCatalog"), "Parse", Definitions.Replace("\"id\":1,\"name\":\"Repeating gate\"", "\"id\":6,\"name\":\"New mission\""), Baseline);
        object newMission = Def(incoming, 6); ((IList)Get(model, "Definitions")).Add(newMission);
        object newQuest = New("MissionQuest"); Set(newQuest, "Id", "new-daily"); Set(newQuest, "Name", "New daily"); Set(newQuest, "Requirements", new int[] { 7737 });
        ((IList)Get(Get(T("MissionWorkspace"), "cache"), "Quests")).Add(newQuest);
        Check(TaskIds().Contains("mission:900:6") && TaskIds().Contains("quest:new-daily"), "Newly arriving tasks did not inherit automatic tracking.");
        Set(newQuest, "Requirements", new int[] { 7728 });
        Check(!TaskIds().Contains("quest:new-daily"), "Automatic tracking kept a daily quest with no marks remaining.");
        DateTime originalDailySet = (DateTime)Get(Get(T("MissionWorkspace"), "cache"), "DailySetUtc");
        Set(Get(T("MissionWorkspace"), "cache"), "DailySetUtc", DateTime.UtcNow.AddDays(-1));
        Check(TaskIds().Contains("quest:daily") && List(Call(T("MissionWorkspace"), "GetTrackedTasks")).Any(t => ((string)Get(t, "Detail")).Contains("Refresh at Tinkerer")), "Automatic tracking lost the stale daily quest indicator.");
        Set(Get(T("MissionWorkspace"), "cache"), "DailySetUtc", originalDailySet);
        ((IList)Get(model, "Definitions")).Remove(newMission); ((IList)Get(Get(T("MissionWorkspace"), "cache"), "Quests")).Remove(newQuest);
        Call(T("MissionWorkspace"), "SetAutoTracking", false, false); Call(T("MissionWorkspace"), "SetAutoTracking", true, false);
        Call(T("MissionWorkspace"), "SetShowAllChoices", false);
        Call(T("MissionWorkspace"), "TogglePin", "mission:900:1"); Call(T("MissionWorkspace"), "TogglePin", "quest:daily");
        var tasks = List(Call(T("MissionWorkspace"), "GetTrackedTasks"));
        Check(tasks.Count == 1 && ((string[])Get(tasks[0], "SourceIds")).Length == 2, "Matching mission and quest did not merge in taskbar.");
        Check(((string)Get(tasks[0], "Detail")).Contains("Hermit God"), "Taskbar did not include a real farming location.");
        Check(((string)Get(tasks[0], "Title")).Contains("\u00d7"), "Taskbar omitted remaining objective count.");
        Set(Runtime(model, 3), "Values", new int[] { 0, 0, 0, 2 });
        Call(T("MissionWorkspace"), "TogglePin", "mission:900:3");
        tasks = List(Call(T("MissionWorkspace"), "GetTrackedTasks"));
        Check(tasks.Count == 2 && tasks.Any(t => ((string)Get(t, "Title")).Contains("Defeat enemies")), "Taskbar dropped an independent AND objective.");
        Check(tasks.Any(t => ((string[])Get(t, "SourceIds")).Length == 3 && ((string)Get(t, "Title")).StartsWith("Ocean Trench")), "Unchosen alternatives did not prefer a shared mission/quest target.");
        Set(Runtime(model, 3), "Values", new int[] { 0, 1, 0, 2 });
        tasks = List(Call(T("MissionWorkspace"), "GetTrackedTasks"));
        Check(tasks.Any(t => ((string)Get(t, "Title")).StartsWith("The Nest")), "Taskbar did not follow the most progressed choice.");
        Call(T("MissionWorkspace"), "SetShowAllChoices", true);
        tasks = List(Call(T("MissionWorkspace"), "GetTrackedTasks"));
        Check(tasks.Any(t => ((string[])Get(t, "SourceIds")).Length == 3), "Show-all-choices setting did not restore shared alternative.");
        Call(T("MissionWorkspace"), "SetShowAllChoices", false);
        Set(Runtime(model, 1), "RawState", 3); Set(Runtime(model, 1), "ClaimedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        tasks = List(Call(T("MissionWorkspace"), "GetTrackedTasks"));
        Check(tasks.Any(t => ((string)Get(t, "Title")).Contains("later") && ((string[])Get(t, "SourceIds")).Length == 2), "Cooldown mission did not link to the shared quest with blocked status.");
        Set(Runtime(model, 1), "ClaimedAt", 1L);
        Call(T("MissionWorkspace"), "TogglePin", "mission:900:3");
        Set(Runtime(model, 3), "Values", new int[] { 0, 2, 0, 5 });
        Call(T("MissionWorkspace"), "ToggleHidden", "quest:daily"); Check(List(Call(T("MissionWorkspace"), "GetTrackedTasks")).Count == 1, "Hiding quest removed unrelated mission.");
        Check(!(bool)Get(Row(List(Call(T("MissionWorkspace"), "GetQuests")), "quest:daily"), "Pinned"), "Hidden quest stayed pinned.");
        Call(T("MissionWorkspace"), "ToggleHidden", "quest:daily");
        Call(T("MissionWorkspace"), "MoveBefore", "mission:900:4", "mission:900:1");
        var ordered = List(Call(T("MissionWorkspace"), "GetMissions")).Select(r => (string)Get(r, "Id")).ToList();
        Check(ordered.IndexOf("mission:900:4") + 1 == ordered.IndexOf("mission:900:1"), "Custom mission order was not retained.");
        Set(Get(T("MissionWorkspace"), "cache"), "QuestsReceivedUtc", DateTime.UtcNow.AddDays(-1));
        Set(Get(T("MissionWorkspace"), "cache"), "DailySetUtc", DateTime.UtcNow.AddDays(-1));
        Check((string)Get(Row(List(Call(T("MissionWorkspace"), "GetQuests")), "quest:daily"), "Status") == "Refresh at Tinkerer", "Daily reset did not stale cached quests.");
        Packet(6, false, w => { I16(w, 2); Quest(w, "daily", new int[] { 7728, 7728, 7728 }); Quest(w, "mixed", new int[] { 7728, 7737 }); I16(w, 0); });
        Check((string)Get(Row(List(Call(T("MissionWorkspace"), "GetQuests")), "quest:daily"), "Status") == "Refresh at Tinkerer", "Repeated stale quest fetch falsely renewed daily reset.");
        Check(Call(T("MissionWorkspace"), "QuestExpiration", "1787644800.0") != null, "Unix quest expiration was not decoded.");
        Check((string)Call(T("MissionTargets"), "MarkDungeon", "Advanced Mark of the Killer Bee Queen") == "Plagued Nest", "Advanced Nest mark was merged into normal Nest.");

        object difficultyObjective = New("MissionObjective"); Set(difficultyObjective, "Type", 4);
        Set(difficultyObjective, "Target", "7-8");
        var ratedTargets = (string[])Call(T("MissionTargets"), "Dungeons", difficultyObjective);
        Check(ratedTargets.Contains("The Nest") && ratedTargets.Contains("Kogbold Steamworks"), "Normal Nest/Steamworks were omitted from 7-8 difficulty recommendations.");
        Check(!ratedTargets.Contains("Plagued Nest") && !ratedTargets.Contains("Advanced Kogbold Steamworks"), "Advanced variants leaked into normal-difficulty recommendations.");
        Set(difficultyObjective, "Target", "8.5-9");
        ratedTargets = (string[])Call(T("MissionTargets"), "Dungeons", difficultyObjective);
        Check(ratedTargets.Contains("Plagued Nest") && ratedTargets.Contains("Advanced Kogbold Steamworks") && !ratedTargets.Contains("The Nest") && !ratedTargets.Contains("Kogbold Steamworks"), "Advanced difficulty recommendations merged with normal dungeon variants.");
        Set(difficultyObjective, "Type", 12); Set(difficultyObjective, "Target", "3.5-4.5");
        ratedTargets = (string[])Call(T("MissionTargets"), "Dungeons", difficultyObjective);
        Check(ratedTargets.Contains("Abyss of Demons") && !ratedTargets.Contains("Infernal Abyss of Demons"), "Adept mission recommendations confused Abyss with Infernal Abyss.");
        Set(difficultyObjective, "Target", "6");
        ratedTargets = (string[])Call(T("MissionTargets"), "Dungeons", difficultyObjective);
        Check(ratedTargets.Contains("Infernal Abyss of Demons") && !ratedTargets.Contains("Abyss of Demons"), "Exact difficulty recommendations lost Infernal Abyss.");
        Set(difficultyObjective, "Target", "7.5");
        ratedTargets = (string[])Call(T("MissionTargets"), "Dungeons", difficultyObjective);
        Check(ratedTargets.Contains("Stromwell's Rift II") && ratedTargets.Contains("White Snake Invasion II") && !ratedTargets.Contains("Stromwell's Rift I") && !ratedTargets.Contains("Stromwell's Rift III"), "Staged dungeon difficulties collapsed into their hardest stage.");
        Check((double)Call(T("MissionTargets"), "Rank", "The Nest") == 7 && (double)Call(T("MissionTargets"), "Rank", "Plagued Nest") == 8.5, "Recommendation sorting does not preserve normal and advanced Nest ratings.");
        Check((double)Call(T("MissionTargets"), "Rank", "Unknown future dungeon") == 100, "Unknown dungeon rank changed from its safe fallback.");

        if (!string.IsNullOrEmpty(screenshots)) Directory.CreateDirectory(screenshots);
        using (var form = new Form { ClientSize = new Size(1150, 720), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
        {
            var page = (Control)Call(T("MissionWorkspace"), "CreateMissionsPage"); form.Controls.Add(page); form.Show(); Application.DoEvents();
            Check(page.Controls.Count > 0, "Mission page failed to create controls.");
            var automatic = (CheckBox)Get(page, "autoTrack"); automatic.Checked = true;
            Check((bool)Call(T("MissionWorkspace"), "GetAutoTracking", false) && (bool)Get(Row(List(Call(T("MissionWorkspace"), "GetMissions")), "mission:900:4"), "Pinned"), "Automatic tracking checkbox did not update mission cards.");
            automatic.Checked = false;
            Check(!TaskIds().Any(id => id.StartsWith("mission:")), "Automatic tracking checkbox did not clear manual mission overrides.");
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); if (!string.IsNullOrEmpty(screenshots)) bitmap.Save(Path.Combine(screenshots, "missions.png"), ImageFormat.Png); }
            form.ClientSize = new Size(620, 560); Application.DoEvents();
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); if (!string.IsNullOrEmpty(screenshots)) bitmap.Save(Path.Combine(screenshots, "missions-narrow.png"), ImageFormat.Png); }
            var panel = (Panel)Get(page, "cards"); panel.AutoScrollPosition = new Point(0, 150); int scroll = -panel.AutoScrollPosition.Y;
            Call(page, "RefreshRows", true); Application.DoEvents();
            Check(-panel.AutoScrollPosition.Y == scroll && panel.Controls.Cast<Control>().Min(c => c.Top) < 0, "Mission refresh jumped or lost the scrolled viewport.");
            var search = (TextBox)Get(page, "search"); search.Text = "Mystic mission"; Application.DoEvents();
            var card = panel.Controls.Cast<Control>().Single(c => c.GetType().Name == "MissionCard" && c.AccessibleName.StartsWith("Mystic mission."));
            var actions = card.Controls.Cast<Control>().Single();
            actions.Controls.Cast<Button>().Single(b => b.Text == "Track").PerformClick();
            Check((bool)Get(Row(List(Call(T("MissionWorkspace"), "GetMissions")), "mission:900:4"), "Pinned"), "Track button did not update the Taskbar preference.");
            search.Clear(); Application.DoEvents();
            var heading = panel.Controls.Cast<Control>().OfType<Button>().First(b => b.Text.Contains("IN PROGRESS")); heading.PerformClick();
            Check(((IList)Get(Call(T("MissionWorkspace"), "GetPagePreferences", false), "Collapsed")).Contains("In progress"), "Section collapse was not persisted.");
            ((ComboBox)Get(page, "filter")).SelectedItem = "Locked";
            Check((string)Get(Call(T("MissionWorkspace"), "GetPagePreferences", false), "Status") == "Locked", "Status filter was not persisted.");
            Call(page, "SelectTask", "mission:900:4"); Application.DoEvents();
            Check(panel.Controls.Cast<Control>().Any(c => c.GetType().Name == "MissionCard"), "Taskbar navigation did not expand a collapsed section.");
        }
        Set(state, "AccountKey", "mission-test-account-b");
        Check(List(Call(T("MissionWorkspace"), "GetMissions")).Count == 0 && List(Call(T("MissionWorkspace"), "GetQuests")).Count == 0, "Account switch leaked mission/quest state.");
        Check(List(Call(T("MissionWorkspace"), "GetTrackedTasks")).Count == 0, "Account switch leaked pinned tasks.");
        Set(state, "AccountKey", "mission-test-account-a");
        Check(List(Call(T("MissionWorkspace"), "GetQuests")).Count == 2, "Returning account did not load its quest cache.");
        Check(!(bool)Call(T("MissionWorkspace"), "GetAutoTracking", false) && !(bool)Call(T("MissionWorkspace"), "GetAutoTracking", true), "Account switch lost automatic tracking preferences.");
        Call(T("MissionWorkspace"), "Flush");
        string[] saved = Directory.GetFiles(isolated, "*.json", SearchOption.AllDirectories);
        Check(saved.Length >= 2, "Mission preferences or quest cache were not persisted.");
        Check(saved.All(p => !File.ReadAllText(p).Contains("accessToken")), "Mission data persisted authentication material.");
        object diskGate = Get(T("HudStorage"), "Gate");
        object writeGate = Get(T("MissionWorkspace"), "PersistenceGate");
        Task writer = null;
        Monitor.Enter(diskGate);
        try
        {
            Task edit = Task.Run(() => Call(T("MissionWorkspace"), "TogglePin", "mission:nonblocking:first"));
            Check(edit.Wait(1000), "Preference editing waited for disk I/O.");
            Task packet = Task.Run(() => Packet(6, false, w => { I16(w, 1); Quest(w, "nonblocking-quest", new int[] { 100 }); I16(w, 0); }));
            Check(packet.Wait(1000), "Quest packet forwarding waited for disk I/O.");
            writer = Task.Run(() => Call(T("MissionWorkspace"), "Flush"));
            bool held = false; DateTime until = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < until)
            {
                if (!Monitor.TryEnter(writeGate)) { held = true; break; }
                Monitor.Exit(writeGate); Thread.Sleep(10);
            }
            Check(held, "Background persistence did not reach the disk write.");
            Task newer = Task.Run(() => Call(T("MissionWorkspace"), "TogglePin", "mission:nonblocking:newest"));
            Check(newer.Wait(1000), "A background write held the mission state lock.");
        }
        finally { Monitor.Exit(diskGate); }
        Check(writer.Wait(5000), "Background mission save did not finish.");
        Call(T("MissionWorkspace"), "Flush");
        string firstFile = Path.Combine(isolated, "missions-mission-test-account-a-preferences.json");
        Check(File.ReadAllText(firstFile).Contains("mission:nonblocking:newest"), "An older persistence snapshot overwrote a newer edit.");
        string blockedRoot = Path.Combine(isolated, "blocked-root"); File.WriteAllText(blockedRoot, "test");
        Environment.SetEnvironmentVariable("EXALTHELPER_HUD_DATA", blockedRoot);
        try
        {
            Call(T("MissionWorkspace"), "TogglePin", "mission:retry-after-failure"); Call(T("MissionWorkspace"), "Flush");
            object pending = ((IDictionary)Get(T("MissionWorkspace"), "accountStates"))["mission-test-account-a"];
            Check((long)Get(pending, "PreferencesVersion") > (long)Get(pending, "SavedPreferencesVersion"), "Failed persistence incorrectly cleared pending changes.");
        }
        finally { Environment.SetEnvironmentVariable("EXALTHELPER_HUD_DATA", isolated); }
        Call(T("MissionWorkspace"), "Flush");
        Check(File.ReadAllText(firstFile).Contains("mission:retry-after-failure"), "Failed mission save did not retry.");

        File.WriteAllText(Path.Combine(isolated, "missions-mission-test-cold-preferences.json"), "{\"AutoTrackMissions\":false,\"AutoTrackQuests\":false,\"TrackingOverrides\":{\"mission:saved\":true,\"quest:saved\":false},\"Pinned\":[\"saved-pin\"],\"Hidden\":[\"new-pin\"],\"MissionsPage\":{\"Category\":\"Saved category\",\"Collapsed\":[\"Ready to claim\"]}}");
        File.WriteAllText(Path.Combine(isolated, "missions-mission-test-cold-quests.json"), "{\"Quests\":[{\"Id\":\"stale-disk\"}]}");
        Control coldPage = null;
        Monitor.Enter(diskGate);
        try
        {
            Set(state, "AccountKey", "mission-test-cold");
            Task cold = Task.Run(() =>
            {
                Call(T("MissionWorkspace"), "GetPagePreferences", false);
                Call(T("MissionWorkspace"), "TogglePin", "new-pin");
                object page = Call(T("MissionWorkspace"), "GetPagePreferences", false); Set(page, "Status", "Locked");
                ((IList)Get(page, "Collapsed")).Add("In progress"); Call(T("MissionWorkspace"), "SetPagePreferences", false, page);
                Packet(6, false, w => { I16(w, 1); Quest(w, "fresh-server", new int[] { 100 }); I16(w, 0); });
            });
            Check(cold.Wait(1000), "Cold account initialization performed blocking file I/O on the caller.");
            coldPage = (Control)Call(T("MissionWorkspace"), "CreateMissionsPage");
            ((TextBox)Get(coldPage, "search")).Text = "preserve search";
        }
        finally { Monitor.Exit(diskGate); }
        ((Task)Call(T("MissionWorkspace"), "FlushAsync")).GetAwaiter().GetResult();
        object prefs = Get(T("MissionWorkspace"), "preferences");
        Check(((IList)Get(prefs, "Pinned")).Contains("saved-pin") && ((IList)Get(prefs, "Pinned")).Contains("new-pin"), "Startup edits discarded saved pin memberships.");
        Check(!((IList)Get(prefs, "Hidden")).Contains("new-pin"), "Pinning during startup did not clear its saved hidden state.");
        object pagePrefs = Call(T("MissionWorkspace"), "GetPagePreferences", false);
        Check((string)Get(pagePrefs, "Status") == "Locked" && (string)Get(pagePrefs, "Category") == "Saved category", "Startup page edit discarded untouched saved fields.");
        Check(((IList)Get(pagePrefs, "Collapsed")).Contains("Ready to claim") && ((IList)Get(pagePrefs, "Collapsed")).Contains("In progress"), "Startup collapse changes discarded saved sections.");
        Check(List(Get(Get(T("MissionWorkspace"), "cache"), "Quests")).Single(q => (string)Get(q, "Id") == "fresh-server") != null, "Late disk load overwrote the authoritative quest packet.");
        Check(File.ReadAllText(Path.Combine(isolated, "missions-mission-test-cold-quests.json")).Contains("fresh-server"), "Shutdown flush did not persist queued quest changes.");
        Check(!(bool)Call(T("MissionWorkspace"), "GetAutoTracking", false) && !(bool)Call(T("MissionWorkspace"), "GetAutoTracking", true), "Late saved tracking defaults were overwritten by an unrelated early edit.");
        var overrides = (IDictionary)Get(prefs, "TrackingOverrides");
        Check((bool)overrides["mission:saved"] && !(bool)overrides["quest:saved"] && (bool)overrides["new-pin"], "Late preference load lost true or false tracking overrides.");
        Call(coldPage, "RefreshRows", false);
        Check(!((CheckBox)Get(coldPage, "autoTrack")).Checked && ((TextBox)Get(coldPage, "search")).Text == "preserve search", "Late preferences did not refresh the toolbar or erased the current search.");
        coldPage.Dispose();

        File.WriteAllText(Path.Combine(isolated, "missions-mission-test-cold-reset-preferences.json"), "{\"AutoTrackMissions\":false,\"AutoTrackQuests\":false,\"TrackingOverrides\":{\"mission:old\":true,\"mission:old-off\":false,\"quest:keep\":true},\"Pinned\":[\"mission:legacy\",\"quest:legacy\"]}");
        Monitor.Enter(diskGate);
        try
        {
            Set(state, "AccountKey", "mission-test-cold-reset");
            Call(T("MissionWorkspace"), "SetAutoTracking", false, false);
            Call(T("MissionWorkspace"), "TogglePin", "mission:before-reset");
            Call(T("MissionWorkspace"), "SetAutoTracking", false, true);
            Call(T("MissionWorkspace"), "SetAutoTracking", false, false);
            Call(T("MissionWorkspace"), "TogglePin", "mission:after-reset");
            Call(T("MissionWorkspace"), "TogglePin", "quest:new-override");
        }
        finally { Monitor.Exit(diskGate); }
        Call(T("MissionWorkspace"), "Flush");
        prefs = Get(T("MissionWorkspace"), "preferences"); overrides = (IDictionary)Get(prefs, "TrackingOverrides");
        Check(!(bool)Call(T("MissionWorkspace"), "GetAutoTracking", false) && !overrides.Contains("mission:old") && !overrides.Contains("mission:old-off") && !overrides.Contains("mission:before-reset"), "A cold master reset replayed old mission overrides.");
        Check((bool)overrides["mission:after-reset"] && (bool)overrides["quest:new-override"] && (bool)overrides["quest:keep"], "Cold master reset lost later edits or unrelated quest overrides.");
        Check(!((IList)Get(prefs, "Pinned")).Contains("mission:legacy") && ((IList)Get(prefs, "Pinned")).Contains("quest:legacy"), "Cold master reset did not clear only its legacy pins.");
        Set(state, "AccountKey", "mission-test-after-reset"); Call(T("MissionWorkspace"), "GetMissions");
        ((IDictionary)Get(T("MissionWorkspace"), "accountStates")).Remove("mission-test-cold");
        Set(state, "AccountKey", "mission-test-cold"); Call(T("MissionWorkspace"), "GetMissions"); Call(T("MissionWorkspace"), "Flush");
        prefs = Get(T("MissionWorkspace"), "preferences"); overrides = (IDictionary)Get(prefs, "TrackingOverrides");
        Check(!(bool)Call(T("MissionWorkspace"), "GetAutoTracking", true) && (bool)overrides["mission:saved"] && !(bool)overrides["quest:saved"], "Disk reload lost automatic tracking defaults or per-card overrides.");
        Set(T("MissionWorkspace"), "refreshing", true); Set(state, "AccountKey", "mission-test-after-refresh-switch");
        Check(!(bool)Get(T("MissionWorkspace"), "IsRefreshing"), "Account switch left the old refresh flag set.");
        Console.WriteLine("PASS: " + checks + " mission, quest, taskbar, persistence, and UI checks. Isolated data: " + isolated);
    }
}
'@
[MissionRegression]::Run($AssemblyPath, "$PSScriptRoot/fixtures", $ScreenshotDirectory)
