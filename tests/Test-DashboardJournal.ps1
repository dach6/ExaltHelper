param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/dashboard-journal"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = $testRoot
Add-Type -ReferencedAssemblies System.Core, System.Drawing -TypeDefinition @'
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public static class DashboardJournalRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
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
    static object JournalRaw(string name, params object[] args) { return T("HudJournal").GetMethod(name, F).Invoke(null, args); }
    static void WaitForLoads() { if (!Task.Run(() => JournalRaw("WaitForLoads")).Wait(10000)) throw new Exception("Journal load did not finish after contention was released"); }
    static object Journal(string name, params object[] args) { object result = JournalRaw(name, args); if (name == "UseAccount") WaitForLoads(); return result; }
    static IList Rows(string name) { return (IList)Get(Journal("Snapshot"), name); }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static object Chat(string sender, string recipient, string message)
    {
        object text = New("Proxy.Networking.Packets.TextPacket");
        Set(text, "Name", sender); Set(text, "Recipient", recipient); Set(text, "Text", message);
        Set(text, "CleanText", message); Set(text, "ObjectId", 7);
        return text;
    }
    static void Conversations()
    {
        Journal("UseAccount", "FixtureAccountA");
        object map = New("Proxy.Networking.Packets.MapInfoPacket");
        Set(map, "MapName", "Realm of the Mad God"); Set(map, "DisplayName", "Realm"); Set(map, "RealmName", "Cyclops");
        Set(map, "CurrentRealmScore", 500); Set(map, "MaxRealmScore", 1000);
        Journal("ObservePacket", null, map, false);
        Check((string)T("HudJournal").GetProperty("Realm", F).GetValue(null, null) == "Cyclops", "Live feed lost realm name");
        Check((int)T("HudJournal").GetProperty("RealmScore", F).GetValue(null, null) == 500, "Live feed lost realm score");
        Check(Rows("Feed").Count == 1 && (string)Get(Rows("Feed")[0], "Category") == "Travel", "Map entry did not become a travel event");
        string[] senders = { "FixturePublic", "FixtureGuild", "#g:GuildSender", "FixtureParty", "#p:PartySender", "FixtureDM", "#Oryx", "FixtureDM", "FixtureDM", "Guild", "Party", "#G:GuildSender", "#P:PartySender" };
        string[] recipients = { "", "*Guild*", "", "*Party*", "", "FixtureLocal", "", "Guildie", "PartyAnimal", "", "", "", "" };
        string[] categories = { "Realm", "Guild", "Guild", "Party", "Party", "Whisper", "Game", "Whisper", "Whisper", "Guild", "Party", "Guild", "Party" };
        for (int i = 0; i < senders.Length; i++)
        {
            Journal("ObservePacket", null, Chat(senders[i], recipients[i], "Fixture " + i), false);
            object entry = Rows("Chat")[i];
            Check((string)Get(entry, "Category") == categories[i], "Chat channel misclassified: " + senders[i] + " -> " + recipients[i]);
            Check((string)Get(entry, "Text") == "Fixture " + i, "Chat text was altered by journal");
        }
        int count = Rows("Chat").Count;
        Journal("ObservePacket", null, Chat("OutgoingFixture", "", "Outgoing test"), true);
        Check(Rows("Chat").Count == count, "Outgoing packet doubled the server-echo chat log");
        object preferences = T("HudPreferences").GetProperty("Current", F).GetValue(null, null);
        Set(preferences, "RecordChat", false);
        Journal("ObservePacket", null, Chat("FixturePublic", "", "Disabled record"), false);
        Check(Rows("Chat").Count == count, "Chat recording preference was ignored");
        Set(preferences, "RecordChat", true);
    }
    static object CombatSnapshot(DateTime start, int enemyId)
    {
        object snapshot = New("Proxy.DataStructures.DpsSnapshot");
        Set(snapshot, "DungeonName", "The Nest"); Set(snapshot, "DungeonStartTime", start);
        object local = Get(snapshot, "LocalPlayer"); Set(local, "Name", "FixtureHero"); Set(local, "ClassName", "Wizard");
        object enemy = New("Proxy.DataStructures.EnemyCombatSnapshot");
        Set(enemy, "EntityId", enemyId); Set(enemy, "Name", "Unknown boss"); Set(enemy, "TotalDamage", 1000L);
        Set(enemy, "FightDurationSeconds", 10.0); Set(enemy, "IsBoss", true);
        object player = New("Proxy.DataStructures.PlayerDamageEntry");
        Set(player, "Name", "FixtureHero"); Set(player, "Damage", 1000L); Set(player, "IsLocalPlayer", true);
        ((IList)Get(enemy, "Damagers")).Add(player); Set(snapshot, "ActiveEnemy", enemy);
        object bag = New("Proxy.DataStructures.LootBagDrop"); Set(bag, "BagEntityId", 20); Set(bag, "SourceMonster", "Fixture Boss");
        ((IList)Get(bag, "ItemIds")).Add(0xA00); ((IList)Get(bag, "ItemIds")).Add(0xA00);
        ((IList)Get(snapshot, "LootBags")).Add(bag);
        return snapshot;
    }
    static void Histories()
    {
        DateTime start = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Local);
        object snapshot = CombatSnapshot(start, 10);
        Journal("ObserveSnapshot", snapshot); Journal("ObserveSnapshot", snapshot); Journal("ObserveSnapshot", snapshot);
        Check(Rows("Fights").Count == 1, "Repeated live snapshots duplicated encounter history");
        Check(Rows("Loot").Count == 2, "Identical loot slots were collapsed or repeated snapshots duplicated them");
        object enemy = Get(snapshot, "ActiveEnemy");
        Set(enemy, "TotalDamage", 850L); Set(enemy, "Name", "Killer Bee Queen"); Set(enemy, "IsDead", true);
        Set(((IList)Get(enemy, "Damagers"))[0], "Damage", 850L);
        Journal("ObserveSnapshot", snapshot);
        object fight = Rows("Fights")[0];
        Check((long)Get(fight, "Damage") == 850L && Rows("Fights").Count == 1, "Server correction did not update the existing encounter");
        Check((string)Get(fight, "Enemy") == "Killer Bee Queen", "Late monster metadata left history with unknown name");
        Check((bool)Get(fight, "Defeated"), "Defeat status was not updated in history");
        object old = CombatSnapshot(start.AddMinutes(-30), 99); Set(old, "HistoryIndex", 0);
        Journal("ObserveSnapshot", old);
        Check(Rows("Fights").Count == 1 && Rows("Loot").Count == 2, "Browsing archived snapshots created new history");
        Check(ReferenceEquals(T("HudJournal").GetProperty("LiveSnapshot", F).GetValue(null, null), snapshot), "Browsing history replaced live widget data");
        object player = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { 7 });
        Set(player, "Attack", 70); Set(player, "Dexterity", 60); Set(player, "Vitality", 40); Set(player, "Wisdom", 50);
        Journal("RecordSource", start, 10, 100, "Weapon", 100, 0, player, 1.0);
        Journal("RecordSource", start, 10, 100, "Weapon", -40, 60, player, 1.0);
        Set(player, "Vitality", 100);
        Journal("RecordSource", start, 10, 100, "Weapon", 30, 0, player, 1.0);
        Journal("ObserveSnapshot", snapshot);
        IList sources = (IList)Get(Rows("Fights")[0], "Sources");
        Check(sources.Count == 2, "Different vitality calculation contexts were collapsed");
        Check(sources.Cast<object>().Sum(x => (long)Get(x, "Damage")) == 90L, "Damage-source corrections were double counted");
        Check(sources.Cast<object>().Sum(x => (long)Get(x, "ConfirmedDamage")) == 60L, "Source confirmation totals were lost");
        ((IList)Get(Journal("Snapshot"), "Fights")).Clear();
        Check(Rows("Fights").Count == 1, "UI snapshot collection can clear stored encounter history");
        Journal("UseAccount", "FixtureAccountB");
        Check(Rows("Fights").Count == 0 && Rows("Chat").Count == 0 && Rows("Loot").Count == 0, "Account switch leaked previous-account history");
        Journal("ObservePacket", null, Chat("SecondAccount", "", "Second account fixture"), false); Journal("Flush");
        Journal("UseAccount", "FixtureAccountA");
        Check(Rows("Fights").Count == 1 && Rows("Loot").Count == 2, "Previous account's persisted history was lost");
        Check(!Rows("Chat").Cast<object>().Any(x => (string)Get(x, "Sender") == "SecondAccount"), "Persisted chat history crossed account boundaries");
    }
    static void ConcurrentSave()
    {
        Journal("ObservePacket", null, Chat("BeforeSave", "", "Save contention fixture"), false);
        object storageGate = T("HudStorage").GetField("Gate", F).GetValue(null);
        object writeGate = T("HudJournal").GetField("WriteGate", F).GetValue(null);
        Task flush = null, observation = null;
        Monitor.Enter(storageGate);
        try
        {
            flush = Task.Run(() => Journal("Flush"));
            bool writing = false;
            for (int i = 0; i < 100; i++)
            {
                if (!Monitor.TryEnter(writeGate)) { writing = true; break; }
                Monitor.Exit(writeGate); Thread.Sleep(10);
            }
            Check(writing, "Save contention fixture did not reach the storage writer");
            observation = Task.Run(() => Journal("ObservePacket", null, Chat("DuringSave", "", "Nonblocking observation fixture"), false));
            Check(observation.Wait(1000), "Slow persistence blocked incoming journal observations");
        }
        finally { Monitor.Exit(storageGate); if (flush != null) flush.Wait(5000); if (observation != null) observation.Wait(5000); }
        Journal("Flush"); Journal("BeginConnection"); Journal("UseAccount", "FixtureAccountA");
        Check(Rows("Chat").Cast<object>().Any(x => (string)Get(x, "Sender") == "DuringSave"), "A concurrent save cleared dirty state for a newer observation");
    }
    static bool HasSender(string sender) { return Rows("Chat").Cast<object>().Any(x => (string)Get(x, "Sender") == sender); }
    static void DetachedSnapshots()
    {
        object copy = Journal("Snapshot");
        object chat = ((IList)Get(copy, "Chat"))[0], feed = ((IList)Get(copy, "Feed"))[0], loot = ((IList)Get(copy, "Loot"))[0], fight = ((IList)Get(copy, "Fights"))[0];
        string chatText = (string)Get(chat, "Text"), feedText = (string)Get(feed, "Text"), lootItem = (string)Get(loot, "Item");
        long damage = (long)Get(((IList)Get(fight, "Players"))[0], "Damage"); int equipment = ((int[])Get(fight, "Equipment"))[0];
        Set(chat, "Text", "Mutated snapshot chat"); Set(feed, "Text", "Mutated snapshot feed"); Set(loot, "Item", "Mutated snapshot loot");
        Set(((IList)Get(fight, "Players"))[0], "Damage", -99L); ((int[])Get(fight, "Equipment"))[0] = -99;
        Check((string)Get(Rows("Chat")[0], "Text") == chatText && (string)Get(Rows("Feed")[0], "Text") == feedText,
            "UI snapshot edits mutated stored chat or feed rows");
        Check((string)Get(Rows("Loot")[0], "Item") == lootItem, "UI snapshot edits mutated a stored loot row");
        Check((long)Get(((IList)Get(Rows("Fights")[0], "Players"))[0], "Damage") == damage && ((int[])Get(Rows("Fights")[0], "Equipment"))[0] == equipment,
            "UI snapshot edits mutated stored player totals or equipment");
    }
    static void ColdLoadMerge()
    {
        Journal("UseAccount", "ColdMergeAccountA");
        object snapshot = CombatSnapshot(DateTime.Now.AddHours(-1), 451);
        Set(Get(snapshot, "ActiveEnemy"), "IsDead", true);
        Journal("ObserveSnapshot", snapshot); Journal("ObservePacket", null, Chat("ColdSavedA", "", "Saved A baseline"), false); Journal("Flush"); Journal("BeginConnection");
        object storageGate = T("HudStorage").GetField("Gate", F).GetValue(null); Task switching = null, observation = null;
        Monitor.Enter(storageGate);
        try
        {
            switching = Task.Run(() => JournalRaw("UseAccount", "ColdMergeAccountA"));
            Check(switching.Wait(1000), "Cold profile handoff blocked on storage I/O");
            observation = Task.Run(() => {
                string scope = (string)T("HudJournal").GetProperty("ScopeId", F).GetValue(null, null);
                foreach (object bag in (IList)Get(snapshot, "LootBags")) Set(bag, "JournalScopeId", scope);
                object extraBag = New("Proxy.DataStructures.LootBagDrop"); Set(extraBag, "BagEntityId", 21); ((IList)Get(extraBag, "ItemIds")).Add(0xA00);
                ((IList)Get(snapshot, "LootBags")).Add(extraBag);
                object enemy = Get(snapshot, "ActiveEnemy"); Set(enemy, "TotalDamage", 600L); Set(((IList)Get(enemy, "Damagers"))[0], "Damage", 600L);
                for (int i = 0; i < 5; i++) Journal("ObserveSnapshot", snapshot);
                Journal("ObservePacket", null, Chat("ColdFreshA", "", "Observed during disk contention"), false);
            });
            Check(observation.Wait(1000), "Cold profile loading blocked packet observations");
        }
        finally { Monitor.Exit(storageGate); if (switching != null) switching.Wait(5000); if (observation != null) observation.Wait(5000); }
        WaitForLoads();
        Check(Rows("Chat").Count == 2 && HasSender("ColdSavedA") && HasSender("ColdFreshA"), "Late cache adoption lost saved or new chat");
        Check(Rows("Fights").Count == 1 && (long)Get(Rows("Fights")[0], "Damage") == 600, "Late cache adoption overwrote the newer fight correction");
        Check(Rows("Loot").Count == 3 && Rows("Trophies").Cast<object>().Sum(x => (long)Get(x, "Count")) == 3,
            "Replayed loot during cold loading duplicated discoveries or lost a new drop");
        Check((long)((IDictionary)Get(Journal("Snapshot"), "DungeonRuns"))["The Nest"] == 1, "Replayed fight during cold loading doubled the run aggregate");
        Journal("Flush"); Journal("UseAccount", "ColdMergeAccountB"); Journal("ObservePacket", null, Chat("ColdSavedB", "", "Saved B baseline"), false); Journal("Flush"); Journal("BeginConnection");
        switching = null;
        Monitor.Enter(storageGate);
        try
        {
            switching = Task.Run(() => {
                JournalRaw("UseAccount", "ColdMergeAccountA"); Journal("ObservePacket", null, Chat("ColdPendingA", "", "Account A in flight"), false);
                JournalRaw("UseAccount", "ColdMergeAccountB"); Journal("ObservePacket", null, Chat("ColdPendingB", "", "Account B in flight"), false);
                JournalRaw("UseAccount", "ColdMergeAccountA"); JournalRaw("UseAccount", "ColdMergeAccountB");
            });
            Check(switching.Wait(1000), "Switching/reentering pending cold profiles waited for disk I/O");
        }
        finally { Monitor.Exit(storageGate); if (switching != null) switching.Wait(5000); }
        WaitForLoads();
        Check(Rows("Chat").Count == 2 && HasSender("ColdSavedB") && HasSender("ColdPendingB") && !HasSender("ColdPendingA"),
            "A late load adopted the wrong account or lost the active account's pending events");
        Journal("UseAccount", "ColdMergeAccountA");
        Check(Rows("Chat").Count == 3 && HasSender("ColdSavedA") && HasSender("ColdFreshA") && HasSender("ColdPendingA"),
            "Inactive cold-load completion lost or duplicated account A's observations");
        Journal("Flush"); Journal("BeginConnection"); Journal("UseAccount", "ColdMergeAccountA");
        Check(Rows("Loot").Count == 3 && Rows("Trophies").Cast<object>().Sum(x => (long)Get(x, "Count")) == 3 && Rows("Chat").Count == 3,
            "Cold-load merged records and aggregates did not survive disk reload");
    }
    static void FailedSaveHandoff(string root)
    {
        Journal("Flush"); Journal("UseAccount", "RetryAccountA");
        Journal("ObservePacket", null, Chat("FailedA", "", "Pending save for account A"), false);
        Journal("ObserveSnapshot", CombatSnapshot(DateTime.Now, 987));
        string originalRoot = Environment.GetEnvironmentVariable("EXALTHELPER_HUD_DATA");
        string blocker = Path.Combine(root, "unwritable-root-fixture"); File.WriteAllText(blocker, "This file intentionally prevents creating a storage directory.");
        try
        {
            Environment.SetEnvironmentVariable("EXALTHELPER_HUD_DATA", blocker); Journal("Flush");
            Check(!string.IsNullOrEmpty((string)T("HudStorage").GetProperty("LastError", F).GetValue(null, null)), "Failed-save fixture did not reach the real persistence failure");
            Check(((IDictionary)T("HudJournal").GetField("pendingSaves", F).GetValue(null)).Count > 0, "Failed save discarded the queued journal snapshot");
            Journal("BeginConnection");
            Check(Rows("Chat").Count == 0 && Rows("Fights").Count == 0, "Unresolved reconnect exposed previous account data");
            Journal("ObservePacket", null, Chat("PendingRetryA", "", "Reconnect before identity"), false);
            Journal("UseAccount", "RetryAccountA");
            Check(HasSender("FailedA") && HasSender("PendingRetryA") && Rows("Fights").Count == 1 && Rows("Loot").Count == 2,
                "Reconnect after failed save lost queued history or buffered observations");
            Journal("ObservePacket", null, Chat("NewestRetryA", "", "Newer revision after reentry"), false); Journal("Flush");
            Journal("UseAccount", "RetryAccountB");
            Check(Rows("Chat").Count == 0 && Rows("Fights").Count == 0 && Rows("Loot").Count == 0, "Failed account save leaked history into a new account");
            Journal("ObservePacket", null, Chat("FailedB", "", "Pending save for account B"), false);
            Journal("UseAccount", "RetryAccountA");
            Check(Rows("Chat").Count == 3 && HasSender("NewestRetryA") && !HasSender("FailedB"), "Returning to a queued account lost a newer revision or mixed profiles");
            Journal("UseAccount", "RetryAccountB"); Journal("ObservePacket", null, Chat("NewestRetryB", "", "Second pending B revision"), false); Journal("Flush");
            Check(((IDictionary)T("HudJournal").GetField("pendingSaves", F).GetValue(null)).Count == 2, "Repeated write failures lost one of two pending account profiles");
        }
        finally { Environment.SetEnvironmentVariable("EXALTHELPER_HUD_DATA", originalRoot); }
        Journal("Flush");
        Check(((IDictionary)T("HudJournal").GetField("pendingSaves", F).GetValue(null)).Count == 0, "Successful retry did not complete every pending account save");
        Journal("BeginConnection"); Journal("UseAccount", "RetryAccountA");
        Check(Rows("Chat").Count == 3 && HasSender("NewestRetryA") && Rows("Fights").Count == 1 && Rows("Loot").Count == 2,
            "Retried account A data did not survive a disk reload");
        Journal("BeginConnection"); Journal("UseAccount", "RetryAccountB");
        Check(Rows("Chat").Count == 2 && HasSender("FailedB") && HasSender("NewestRetryB") && !HasSender("FailedA"),
            "Retried account B data did not survive a separate disk reload");
        Journal("Flush");
    }
    public static void Run(string path, string root)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        ((Timer)T("HudJournal").GetField("saveTimer", F).GetValue(null)).Change(Timeout.Infinite, Timeout.Infinite);
        Conversations(); Histories(); DetachedSnapshots(); Journal("Flush");
        Journal("BeginConnection");
        Journal("ObservePacket", null, Chat("BeforeIdentity", "", "Connection backlog fixture"), false);
        Journal("UseAccount", "FixtureAccountA");
        Check(Rows("Chat").Cast<object>().Any(x => (string)Get(x, "Sender") == "BeforeIdentity"), "Connecting account did not adopt buffered observations");
        Journal("Flush"); Journal("BeginConnection"); Journal("UseAccount", "FixtureAccountA");
        Check(Rows("Chat").Cast<object>().Any(x => (string)Get(x, "Sender") == "BeforeIdentity"), "Buffered observations adopted by an account were not marked for persistence");
        ConcurrentSave();
        string[] files = Directory.GetFiles(root, "journal-*.json");
        Check(files.Length == 2, "Account journals did not persist independently");
        Check(files.All(f => !f.Contains("FixtureAccount")), "Account identity was exposed in a file name");
        Check(files.All(f => !File.ReadAllText(f).Contains("FixtureAccount")), "Journal persisted account identity instead of feature data");
        FailedSaveHandoff(root);
        ColdLoadMerge();
        Console.WriteLine("PASS: " + checks + " dashboard channel, history, correction and account-isolation assertions.");
    }
}
'@
try { [DashboardJournalRegression]::Run($AssemblyPath, $testRoot) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
