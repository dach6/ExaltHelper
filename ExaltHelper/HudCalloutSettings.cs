using System;
using System.Collections.Generic;
using System.Linq;

namespace ExaltHelper;

internal sealed class HudCalloutSettings
{
    public string DungeonNameStyle { get; set; } = "Short";
    public string EventNameStyle { get; set; } = "Short";
    public string DungeonJoin { get; set; } = "End";
    public string EventJoin { get; set; } = "None";
    public string LootLabel { get; set; } = "lb";
    public string DustLabel { get; set; } = "db";
    public string XpLabel { get; set; } = "None";
    public int LootThreshold { get; set; } = 5;
    public int DustThreshold { get; set; } = 10;
    public int XpThreshold { get; set; } = 10;
    public bool Percent { get; set; }
    public bool EventAddUpcoming { get; set; } = true;
    public bool CallEventModifiers { get; set; } = true;
    public bool LearnEventModifiers { get; set; } = true;
    public int LearnEventRuns { get; set; } = 3;
    public string RealmStatus { get; set; } = "None";
    public int RealmThreshold { get; set; } = 33;
    public string NexusMarker { get; set; } = "None";
    public bool AutoCopyDungeons { get; set; }
    public int ExpirySeconds { get; set; } = 25;
    public string AlienAdeptPrefix { get; set; } = "adept wave";
    public string AlienVeteranPrefix { get; set; } = "veteran wave";
    public bool AlienWave4Boss { get; set; } = true;
    public Dictionary<string, string> DungeonNameOverrides { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> EventNameOverrides { get; set; } = new Dictionary<string, string>();
    public List<HudCalloutTag> RewardTags { get; set; } = HudCalloutNames.DefaultTags();
    public List<HudQuickText> QuickTexts { get; set; } = new List<HudQuickText>();
    public string KeyPopThanks { get; set; } = "Thanks {player} for the key!";
    public string AreaUnlockThanks { get; set; } = "Thanks {player} for the {item}!";

    public void Normalize()
    {
        DungeonNameStyle = Choice(DungeonNameStyle, "Short", "Full", "None");
        EventNameStyle = Choice(EventNameStyle, "Short", "Full", "None");
        DungeonJoin = Choice(DungeonJoin, "End", "Beginning", "None");
        EventJoin = Choice(EventJoin, "None", "Beginning", "End");
        LootLabel = Choice(LootLabel, "lb", "loot", "None");
        DustLabel = Choice(DustLabel, "db", "dust", "None");
        XpLabel = Choice(XpLabel, "None", "xp");
        RealmStatus = Choice(RealmStatus, "None", "Closing", "Score");
        NexusMarker = Choice(NexusMarker, "None", "Nex", "InNexus");
        LootThreshold = Math.Max(0, Math.Min(10000, LootThreshold));
        DustThreshold = Math.Max(0, Math.Min(10000, DustThreshold));
        XpThreshold = Math.Max(0, Math.Min(10000, XpThreshold));
        RealmThreshold = Math.Max(0, Math.Min(100, RealmThreshold));
        LearnEventRuns = Math.Max(2, Math.Min(10, LearnEventRuns));
        ExpirySeconds = Math.Max(1, Math.Min(30, ExpirySeconds));
        DungeonNameOverrides ??= new Dictionary<string, string>(); EventNameOverrides ??= new Dictionary<string, string>();
        RewardTags ??= HudCalloutNames.DefaultTags(); RewardTags.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.Id));
        foreach (var tag in RewardTags) { tag.Id = HudCallouts.Canonical(tag.Id); tag.Short ??= ""; tag.Name ??= tag.Id; }
        QuickTexts ??= new List<HudQuickText>(); QuickTexts = QuickTexts.Where(x => x != null).Take(8).ToList();
        foreach (var quick in QuickTexts) { quick.Name ??= ""; quick.Text ??= ""; }
        AlienAdeptPrefix ??= "adept wave"; AlienVeteranPrefix ??= "veteran wave";
        KeyPopThanks ??= "Thanks {player} for the key!"; AreaUnlockThanks ??= "Thanks {player} for the {item}!";
    }
    private static string Choice(string value, params string[] values) => values.Contains(value) ? value : values[0];
}

internal sealed class HudCalloutTag
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Short { get; set; }
    public bool Enabled { get; set; }
}
internal sealed class HudQuickText
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}
internal sealed class HudCalloutContext
{
    public string Kind { get; set; } = "Message";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Server { get; set; } = "";
    public string Realm { get; set; } = "";
    public string DungeonModifiers { get; set; }
    public int? OriginRealmPercent { get; set; }
    public bool? OpenedInNexus { get; set; }
    public string[] LearnedEventModifiers { get; set; } = Array.Empty<string>();
    public bool Available { get; set; } = true;
}
internal sealed class HudCalloutResult
{
    public string Text { get; set; } = "";
    public string Body { get; set; } = "";
    public bool CanCopy { get; set; }
    public int? LootBonus { get; set; }
    public int? DustBonus { get; set; }
    public int? XpBonus { get; set; }
    public string[] UnknownModifiers { get; set; } = Array.Empty<string>();
    public string Details { get; set; } = "";
}
