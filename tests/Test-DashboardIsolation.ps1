param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/dashboard-isolation"
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
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

public static class DashboardIsolationRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object Get(object o, string name) { var f = o.GetType().GetField(name, F); return f != null ? f.GetValue(o) : o.GetType().GetProperty(name, F).GetValue(o, null); }
    static void Set(object o, string name, object value) { var f = o.GetType().GetField(name, F); if (f != null) f.SetValue(o, value); else o.GetType().GetProperty(name, F).SetValue(o, value, null); }
    static object Static(string name, string method, params object[] args) { object result = T(name).GetMethod(method, F).Invoke(null, args); if (name == "HudJournal" && method == "UseAccount") T(name).GetMethod("WaitForLoads", F).Invoke(null, null); return result; }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, F).Invoke(o, args); }
    static object Packet(string name) { return Activator.CreateInstance(T("Proxy.Networking.Packets." + name), true); }
    static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
    static IList Chat() { return (IList)Get(Static("HudJournal", "Snapshot"), "Chat"); }
    static object Shared(string name) { return T("Proxy.Mods.DpsTrackerMod").GetField(name, F).GetValue(null); }
    static int SharedCount(string name) { return ((ICollection)Shared(name)).Count; }
    static object Tracker(object client)
    {
        object tracker = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod"), new object[] { client });
        Set(client, "dpsTracker", tracker); return tracker;
    }
    static string AccountKey() { return (string)T("AccountWorkspace").GetProperty("AccountKey", F).GetValue(null, null); }
    static object Client()
    {
        object client = FormatterServices.GetUninitializedObject(T("Proxy.Client"));
        foreach (string field in new[] { "Enemies", "Entities", "Portals" }) Set(client, field, Activator.CreateInstance(client.GetType().GetField(field, F).FieldType));
        return client;
    }
    static void Hello(object client, string token)
    {
        object hello = Packet("HelloPacket"); Set(hello, "AccessToken", token);
        Static("HudObserver", "Observe", client, hello, true);
    }
    static void Message(object client, string sender)
    {
        object packet = Packet("TextPacket"); Set(packet, "Name", sender); Set(packet, "Recipient", "");
        Set(packet, "Text", "Offline fixture"); Set(packet, "CleanText", "Offline fixture");
        byte[] before = (byte[])Call(packet, "GetPayloadBytes");
        Static("HudObserver", "Observe", client, packet, false);
        Check((bool)Get(packet, "Send") && before.SequenceEqual((byte[])Call(packet, "GetPayloadBytes")), "Account observation changed forwarded chat bytes");
    }
    static void Identify(object client, int id, string account)
    {
        object player = Activator.CreateInstance(T("Proxy.DataStructures.MapObject"), new object[] { id });
        Set(player, "AccountId", account); Set(player, "PlayerName", "IsolationFixture"); Set(client, "Player", player); Set(client, "ClientId", id);
        object map = Packet("MapInfoPacket"); Set(map, "MapName", "Nexus"); Set(map, "DisplayName", "Nexus");
        Static("HudObserver", "Observe", client, map, false);
        Static("HudJournal", "WaitForLoads");
    }
    public static void Run(string path, string root)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        var items = T("Proxy.DataStructures.GameData").GetField("Items", F);
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[] { Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType) }));
        // Occupy the production refresh gate so HELLO cannot start an HTTP request.
        // RefreshAsync uses a non-blocking WaitAsync(0), so no request is queued.
        var refreshGate = (SemaphoreSlim)T("AccountWorkspace").GetField("RefreshGate", F).GetValue(null);
        Check(refreshGate.Wait(0), "Offline fixture could not suppress API refresh");
        try
        {
            object first = Client(), second = Client();
            Hello(first, "ISOLATION_FAKE_TOKEN_A");
            Check((bool)Static("HudObserver", "Accepts", first) && !(bool)Static("HudObserver", "Accepts", second), "HELLO did not establish one active client");
            Message(first, "PendingA");
            Check(AccountKey() == "" && Chat().Count == 1, "Unidentified connection lost buffered observations");
            Identify(first, 100, "OfflineAccountA"); string firstKey = AccountKey();
            Check(firstKey.Length == 64 && Chat().Count == 1, "Account resolution lost buffered observations");
            Message(first, "ResolvedA"); Static("HudJournal", "Flush");
            object firstTracker = Tracker(first);
            Call(firstTracker, "LogAbilityUsage", "OldA", "Wizard", "FixtureAbility", "Fixture item", "Fixture details", true);
            object party = Packet("PartyMemberAddedPacket"); Set(party, "Name", "OldFriendA");
            Call(firstTracker, "OnPartyMemberAdded", party);
            ((IList)Shared("sessionDungeonHistory")).Add(Activator.CreateInstance(T("Proxy.DataStructures.DpsSnapshot"), true));
            ((IList)Shared("sessionLootBags")).Add(Activator.CreateInstance(T("Proxy.DataStructures.LootBagDrop"), true));
            ((IList)Shared("sessionLootDrops")).Add(Activator.CreateInstance(T("Proxy.DataStructures.LootDropEntry"), true));
            Check(SharedCount("sessionAbilityLog") == 1 && SharedCount("sessionPartyMembers") == 1, "Session fixture did not populate abilities and party");
            object reconnect = Client(); object reconnectTracker = Tracker(reconnect); Hello(reconnect, "ISOLATION_FAKE_TOKEN_A_RENEWED"); Identify(reconnect, 101, "OfflineAccountA");
            Check(SharedCount("sessionAbilityLog") == 1 && SharedCount("sessionPartyMembers") == 1 && SharedCount("sessionDungeonHistory") == 1,
                "Same-account reconnect cleared session history, abilities or party");
            object reconnectedSnapshot = Call(reconnectTracker, "CompileCurrentSnapshot");
            Check(((IList)Get(reconnectedSnapshot, "LootBags")).Count == 1 && ((IList)Get(reconnectedSnapshot, "LootDrops")).Count == 1,
                "Renewed login for the same account hid retained loot under its old journal scope");

            object secondTracker = Tracker(second);
            Hello(second, "ISOLATION_FAKE_TOKEN_B");
            Check(AccountKey() == "" && Chat().Count == 0, "New unresolved account displayed the prior account's history");
            object pendingSnapshot = Call(secondTracker, "CompileCurrentSnapshot");
            Check(((IList)Get(pendingSnapshot, "AbilityLog")).Count == 0 && ((IList)Get(pendingSnapshot, "PartyMembers")).Count == 0
                && ((IList)Static("Proxy.Mods.DpsTrackerMod", "GetDungeonHistoryList")).Count == 0,
                "Unresolved connection exposed previous-account session lists");
            Message(first, "StaleA");
            Check(Chat().Count == 0 && AccountKey() == "", "Previous client wrote into unresolved new-account history");
            object live = T("HudJournal").GetProperty("LiveSnapshot", F).GetValue(null, null);
            object staleTracker = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod"), new object[] { first });
            Call(staleTracker, "PushSnapshotToOverlay");
            Check(ReferenceEquals(live, T("HudJournal").GetProperty("LiveSnapshot", F).GetValue(null, null)), "Previous client replaced the new connection's live snapshot");
            object bullet = Activator.CreateInstance(T("Proxy.Mods.DpsTrackerMod+RealmSharkBullet"), true);
            Set(bullet, "HudContext", Activator.CreateInstance(T("HudDamageSource"), true));
            Call(staleTracker, "LogProjectileDamage", bullet, 900, "local_estimate", 100, 0, 100, null);
            Check(((IDictionary)T("HudJournal").GetField("sources", F).GetValue(null)).Count == 0, "Previous client's damage source entered the new account");
            Message(second, "PendingB"); Identify(second, 200, "OfflineAccountB"); string secondKey = AccountKey();
            Check(secondKey.Length == 64 && secondKey != firstKey && Chat().Count == 1 && (string)Get(Chat()[0], "Sender") == "PendingB", "New-account resolution merged previous-account history");
            string[] sharedLists = { "sessionAbilityLog", "sessionPartyMembers", "sessionDungeonHistory", "sessionLootBags", "sessionLootDrops" };
            Check(sharedLists.All(name => SharedCount(name) == 0), "Resolved account handoff retained another account's session collections");
            Call(firstTracker, "OnPartyMemberAdded", party);
            object stasis = Packet("StasisPacket"); Set(stasis, "StasisDuration", 5f); Call(firstTracker, "OnStasis", stasis);
            Call(firstTracker, "LogAbilityUsage", "LateA", "Wizard", "OtherFixtureAbility", "Fixture item", "Fixture details", true);
            Call(firstTracker, "ArchiveCurrentDungeon");
            Check(sharedLists.All(name => SharedCount(name) == 0), "Old client repopulated session collections after account handoff");
            Static("HudJournal", "Flush");
            Static("HudJournal", "UseAccount", firstKey);
            Check(Chat().Count == 2 && Chat().Cast<object>().All(x => ((string)Get(x, "Sender")).EndsWith("A")), "Prior account did not retain exactly its own messages");
            Static("HudJournal", "UseAccount", secondKey);
            Check(Chat().Count == 1 && (string)Get(Chat()[0], "Sender") == "PendingB", "New-account buffered message was not durable");
            Check(Directory.GetFiles(root, "journal-*.json").Length == 2, "Connections did not create separate journals");
            Check(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).All(f => !File.ReadAllText(f).Contains("ISOLATION_FAKE_TOKEN")), "Session credential was persisted");
        }
        finally { T("AccountWorkspace").GetField("accessToken", F).SetValue(null, ""); refreshGate.Release(); }
        Console.WriteLine("PASS: " + checks + " offline connection, account-isolation and byte-preservation assertions.");
    }
}
'@
try { [DashboardIsolationRegression]::Run($AssemblyPath, $testRoot) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
