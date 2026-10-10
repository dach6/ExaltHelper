param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

public static class RosterRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly a;
    static object client, tracker;

    static Type T(string n) { return a.GetType(n, true); }
    static object Get(object o, string n) { var t = o.GetType(); var f = t.GetField(n,F); return f != null ? f.GetValue(o) : t.GetProperty(n,F).GetValue(o,null); }
    static void Set(object o, string n, object v) { var t=o.GetType(); var f=t.GetField(n,F); if(f!=null) f.SetValue(o,v); else t.GetProperty(n,F).GetSetMethod(true).Invoke(o,new object[]{v}); }
    static object Call(object o, string n, params object[] args)
    {
        var m = o.GetType().GetMethod(n, F);
        if (m == null) throw new Exception("Method not found: " + n + " on " + o.GetType().Name);
        return m.Invoke(o, args);
    }
    static void Check(bool ok, string n) { if(!ok) throw new Exception(n); }
    static object Packet(string n) { return Activator.CreateInstance(T("ExaltHelper.Proxy.Networking.Packets."+n), true); }

    static object MakePlayer(int id, string name)
    {
        object e = FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.DataStructures.MapObject"));
        Set(e, "ObjectId", id);
        Set(e, "PlayerName", name);
        Set(e, "ObjectType", (ushort)782); // Huntress
        Set(e, "Hp", 700);
        Set(e, "MaxHp", 700);
        Set(e, "Inventory", new int[] { -1, -1, -1, -1 });
        return e;
    }

    public static void Run(string path)
    {
        a = Assembly.LoadFrom(Path.GetFullPath(path));
        Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

        var items = T("ExaltHelper.Proxy.DataStructures.GameData").GetField("Items");
        items.SetValue(null, Activator.CreateInstance(items.FieldType, new object[]{ Activator.CreateInstance(items.FieldType.GetProperty("Map").PropertyType) }));

        client = FormatterServices.GetUninitializedObject(T("ExaltHelper.Proxy.Client"));
        foreach(string n in new string[]{"Enemies","Entities","Portals"})
            Set(client, n, Activator.CreateInstance(client.GetType().GetField(n, F).FieldType));

        Set(client, "ClientId", 999);
        object localPlayer = MakePlayer(999, "LocalTester");
        Set(client, "Player", localPlayer);

        Type scaling = T("ExaltHelper.Proxy.Helpers.AbilityScalingManager");
        scaling.GetMethod("ParseEquipXml", F).Invoke(null, new object[]{ Path.Combine(Directory.GetCurrentDirectory(), "ExaltHelper_Data", "Objects.xml") });
        scaling.GetField("isInitialized", F).SetValue(null, true);

        tracker = Activator.CreateInstance(T("ExaltHelper.Proxy.Mods.DpsTrackerMod"), new object[]{ client });

        var roster = (IDictionary)Get(tracker, "playerRoster");
        Check(roster.Count == 0, "Initial roster should be empty");

        // 1. Add player BGJOE with id 100
        object p1 = MakePlayer(100, "BGJOE");
        Call(tracker, "UpdatePlayerRosterEntry", p1);
        Check(roster.Count == 1, "Roster should have 1 player after BGJOE added");

        // 2. BGJOE leaves instance: packet.Drops contains id 100
        object dropPacket = Packet("UpdatePacket");
        Set(dropPacket, "Drops", new int[] { 100 });
        Call(tracker, "OnUpdate", dropPacket);
        Check(roster.Count == 0, "Roster should have 0 players after BGJOE dropped");

        // 3. BGJOE rejoins with new ObjectId 200
        object p2 = MakePlayer(200, "BGJOE");
        Call(tracker, "UpdatePlayerRosterEntry", p2);
        Check(roster.Count == 1, "Roster should have 1 player after BGJOE rejoined with new id");

        // 4. Verify snapshot only has 1 player
        object snap = Call(tracker, "CompileCurrentSnapshot");
        var playersList = (IList)Get(snap, "Players");
        Check(playersList.Count == 1, "Snapshot should only contain 1 player");
        Check((string)Get(playersList[0], "Name") == "BGJOE", "Player name should be BGJOE");

        // 5. Test direct name collision without drop packet (simulate edge case)
        object p3 = MakePlayer(300, "BGJOE");
        Call(tracker, "UpdatePlayerRosterEntry", p3);
        Check(roster.Count == 1, "Roster should still have 1 player when updated with new id for same name");

        // 6. Add a second player CheesyMichael (id 400)
        object p4 = MakePlayer(400, "CheesyMichael");
        Call(tracker, "UpdatePlayerRosterEntry", p4);
        Check(roster.Count == 2, "Roster should have 2 distinct players");

        // 7. Drop BGJOE (id 300), CheesyMichael remains
        object dropPacket2 = Packet("UpdatePacket");
        Set(dropPacket2, "Drops", new int[] { 300 });
        Call(tracker, "OnUpdate", dropPacket2);
        Check(roster.Count == 1, "Roster should have 1 player after BGJOE dropped");
        Check(roster.Contains(400), "CheesyMichael should remain in roster");

        // 8. Test Shiny Item Rarity: verify shiny items retain their normal rarity
        Type spriteMgr = T("ExaltHelper.ItemSpriteManager");
        spriteMgr.GetMethod("InitializeAssets", F).Invoke(null, null);

        var getMeta = spriteMgr.GetMethod("GetItemMetadata", F);
        // Colossus Sword Shiny (1230) -> Legendary
        object colossusMeta = getMeta.Invoke(null, new object[]{ 1230 });
        Check((bool)Get(colossusMeta, "IsShiny"), "Colossus Sword Shiny should be marked as shiny");
        Check((string)Get(colossusMeta, "Rarity") == "Legendary", "Colossus Sword Shiny should have Legendary rarity, not Shiny/Divine");

        // Wand of the Fallen Shiny (1274) -> Rare
        object fallenMeta = getMeta.Invoke(null, new object[]{ 1274 });
        Check((bool)Get(fallenMeta, "IsShiny"), "Wand of the Fallen Shiny should be marked as shiny");
        Check((string)Get(fallenMeta, "Rarity") == "Rare", "Wand of the Fallen Shiny should have Rare rarity, not Shiny/Divine");

        // Candy-Coated Armor Shiny (1227) -> Uncommon/Rare
        object ccMeta = getMeta.Invoke(null, new object[]{ 1227 });
        Check((bool)Get(ccMeta, "IsShiny"), "Candy-Coated Armor Shiny should be marked as shiny");
        Check((string)Get(ccMeta, "Rarity") != "Shiny", "Candy-Coated Armor Shiny should not have Shiny rarity");

        Console.WriteLine("PASS: All Player Roster and Shiny Item regression checks passed!");
    }
}
'@

try {
    [RosterRegression]::Run((Resolve-Path $AssemblyPath).Path)
} catch {
    Write-Error $_.Exception.ToString()
    if ($_.Exception.InnerException) {
        Write-Error $_.Exception.InnerException.ToString()
    }
}
