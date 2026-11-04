param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
if ([IO.DriveInfo]::new('C:\').AvailableFreeSpace -le 5GB -or (Test-Path 'C:/Users/Administrator/Desktop/code/.exalthelper-tools/disk-guard/STOP')) { throw 'STOP: disk guard' }
Add-Type -ReferencedAssemblies System.Core,System.Web.Extensions -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public static class CalloutRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly app; static int checks; static object settings, context, catalog;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object o, string name) { Type t = o as Type ?? o.GetType(); var field = t.GetField(name, F); return field != null ? field.GetValue(o is Type ? null : o) : t.GetProperty(name, F).GetValue(o is Type ? null : o, null); }
    static void Set(object o, string name, object value) { Type t = o.GetType(); var field = t.GetField(name, F); if (field != null) field.SetValue(o, value); else t.GetProperty(name, F).SetValue(o, value, null); }
    static object Call(object o, string name, params object[] args) { Type t = o as Type ?? o.GetType(); return t.GetMethod(name, F).Invoke(o is Type ? null : o, args); }
    static object Engine(string name, params object[] args) { return Call(T("HudCallouts"), name, args); }
    static void Check(bool passed, string message) { if (!passed) throw new Exception(message); checks++; }
    static object Build(bool party = false, bool server = false, bool realm = false) { return Engine("BuildWithCatalog", context, settings, party, server, realm, catalog); }
    static string Body() { return (string)Get(Build(), "Body"); }
    static object Tag(string id) { return ((IEnumerable)Get(settings, "RewardTags")).Cast<object>().Single(x => (string)Get(x, "Id") == id); }
    static void Reset() { settings = New("HudCalloutSettings"); context = New("HudCalloutContext"); Set(context, "Kind", "Dungeon"); Set(context, "Name", "Lost Halls"); Set(context, "DungeonModifiers", ""); }
    const string Xml = @"<DungeonModifiers>
      <DungeonModifier id='LOOTING'><DisplayId>Looting</DisplayId><Mutators><EntityDropMultiplier amount='1.50'/></Mutators></DungeonModifier>
      <DungeonModifier id='DUSTSTORM'><DisplayId>Dust Storm</DisplayId><Mutators><EntityDustMultiplier amount='1.50'/></Mutators></DungeonModifier>
      <DungeonModifier id='EXPERIENCED'><DisplayId>Experienced</DisplayId><Mutators><EntityStatMultiplier stat='XP' amount='2.00'/><EntityStatMultiplier stat='Defense' amount='2.00'/></Mutators></DungeonModifier>
      <DungeonModifier id='GENEROUS'><DisplayId>Generous</DisplayId><Mutators/></DungeonModifier>
      <DungeonModifier id='MERCA'><DisplayId>Syndicate Takeover</DisplayId><Mutators/></DungeonModifier>
      <DungeonModifier id='QUALIFIED'><DisplayId>Qualified</DisplayId><Mutators><EntityDropMultiplier itemId='1' amount='100'/><EntityDustMultiplier excludeLabels='POTION' amount='100'/><EntityStatMultiplier stat='XP' itemId='1' amount='100'/><EntityDropMultiplier reqLabels='BOSS' amount='1.45'/><EntityDustMultiplier amount='1.18'/><EntityStatMultiplier stat='XP' amount='1.05'/></Mutators></DungeonModifier>
      <DungeonModifier id='NEGATIVE'><DisplayId>Negative</DisplayId><Mutators><EntityDropMultiplier amount='0.50'/><EntityDustMultiplier amount='NaN'/></Mutators></DungeonModifier>
      <DungeonModifier id='STEAMWORKS_MAINTENANCE'><DisplayId>Steamworks Maintenance</DisplayId><Mutators><EntityDropMultiplier amount='1.20'/></Mutators></DungeonModifier>
      <DungeonModifier id='SOUVENIR_1'><DisplayId>Souvenir</DisplayId><Mutators/></DungeonModifier>
    </DungeonModifiers>";
    public static void Run(string path)
    {
        app = Assembly.LoadFrom(Path.GetFullPath(path));
        catalog = Call(T("HudCalloutModifiers"), "ParseXml", Xml); Reset();
        Check((int)Get(catalog, "Count") == 9, "Modifier XML lost definitions.");
        object qualified = Call(catalog, "Get", "qualified");
        Check((int)Get(qualified, "Loot") == 45 && (int)Get(qualified, "Dust") == 18 && (int)Get(qualified, "Xp") == 5, "Item-qualified rewards inflated the general bonus or enemy-class rewards were omitted.");
        Check((int)Get(Call(catalog, "Get", "EXPERIENCED"), "Xp") == 100, "Enemy defense multiplier was confused with XP.");
        Check((int)Get(Call(catalog, "Get", "NEGATIVE"), "Loot") == -50 && (int)Get(Call(catalog, "Get", "NEGATIVE"), "Dust") == 0, "Negative or nonfinite modifier amounts were mishandled.");
        bool rejected = false; try { Call(T("HudCalloutModifiers"), "ParseXml", "<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///never-read'>]><DungeonModifiers>&ext;</DungeonModifiers>"); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected, "Modifier XML allowed external entity declarations.");
        rejected = false; try { Call(T("HudCalloutModifiers"), "ParseXml", "<DungeonModifiers/>"); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected, "Empty modifier data was accepted as authoritative.");
        Check(((string[])Engine("ParseModifierTokens", " CHEF ;GENEROUS|S; ;|A")).SequenceEqual(new[] { "CHEF", "GENEROUS" }) && (string)Engine("ModifierGrade", "CHEF;GENEROUS|S;|A") == "A", "Wire grade leaked into the modifier identifiers.");
        Check((string)Engine("Canonical", "steamworks_maintenance") == "STEAMWORKSMAINTENANCE", "Modifier canonicalization failed.");

        Set(context, "DungeonModifiers", "LOOTING;DUSTSTORM;EXPERIENCED;MERCA;GENEROUS|S");
        Check(Body() == "halls 50 lb 50 db synd generous", "Default reward or modifier ordering changed.");
        Set(context, "Server", "USMidWest2"); Set(context, "Realm", "Medusa");
        Check((string)Get(Build(true, true, true), "Text") == "/p USMW2 Medusa halls 50 lb 50 db synd generous j", "Full callout assembly or server abbreviation failed.");
        Set(settings, "DungeonJoin", "Beginning");
        Check(((string)Get(Build(true, true, true), "Text")).StartsWith("/p j USMW2 Medusa halls"), "Join marker was not immediately after the party prefix.");
        Set(settings, "DungeonJoin", "None");
        Check(!((string)Get(Build(), "Text")).EndsWith(" j"), "Disabled join marker remained.");
        Set(settings, "Percent", true); Set(settings, "XpLabel", "xp"); Set(settings, "LootLabel", "loot"); Set(settings, "DustLabel", "dust");
        Check(Body() == "halls 50% loot 50% dust 100% xp synd generous", "Label or percent preferences were ignored.");
        Set(settings, "LootThreshold", 51); Set(settings, "DustThreshold", 51); Set(settings, "XpThreshold", 101);
        Check(Body() == "halls synd generous", "Independent reward thresholds were ignored.");
        Set(settings, "LootThreshold", 50);
        Check(Body().Contains("50% loot"), "Threshold equality did not include the reward.");
        Set(settings, "LootLabel", "None"); Check(!Body().Contains("loot"), "Hidden loot label remained in the callout.");
        Set(Tag("GENEROUS"), "Short", "gen"); Check(Body().EndsWith("gen"), "Editable modifier nickname was ignored.");
        Set(Tag("GENEROUS"), "Enabled", false); Check(!Body().Contains("gen"), "Disabled modifier tag remained.");

        Reset(); Set(context, "DungeonModifiers", "LOOTING;UNOBSERVED_MOD");
        object unknown = Build();
        Check(Get(unknown, "LootBonus") == null && !((string)Get(unknown, "Body")).Contains("50 lb") && ((string)Get(unknown, "Details")).Contains("UNOBSERVED_MOD"), "A partial modifier sum was presented as the full known bonus.");
        Set(context, "DungeonModifiers", null);
        Check(Get(Build(), "LootBonus") == null && ((string)Get(Build(), "Details")).Contains("not observed"), "Unobserved modifiers were reported as zero.");
        Set(context, "DungeonModifiers", ""); Check((int)Get(Build(), "LootBonus") == 0, "Observed empty modifier list was not known zero.");
        Set(context, "DungeonModifiers", "STEAMWORKS_MAINTENANCE");
        Check(Body() == "halls 20 lb turrets off", "Canonical event preset did not produce its tag.");
        Set(settings, "CallEventModifiers", false);
        Check(Body() == "halls 20 lb", "Suppressing event tags also suppressed their numeric reward.");
        Set(context, "DungeonModifiers", "GENEROUS"); Set(context, "LearnedEventModifiers", new[] { "GENEROUS" });
        Check(Body() == "halls", "Learned event tags were not suppressed.");
        Set(settings, "LearnEventModifiers", false); Check(Body() == "halls generous", "Disabling learned event detection did not restore its tag.");
        Reset(); Set(context, "DungeonModifiers", "SOUVENIR_1"); Check(Body() == "halls souv", "Numbered modifier did not match its base tag.");
        Set(context, "DungeonModifiers", "SOUVENIRPARTY"); Check(Body() == "halls", "An unrelated prefix matched a modifier tag.");
        Set(context, "DungeonModifiers", "ALEXANDERSLEGACY_1"); Check(Body() == "halls dying thessal 25", "Alexander's Legacy tier formatting failed.");
        Set(settings, "Percent", true); Set(context, "DungeonModifiers", "ALEXANDERSLEGACY"); Check(Body() == "halls dying thessal 100%", "Current Alexander's Legacy chance or percent switch failed.");
        Set(context, "DungeonModifiers", "DIMITUS"); Check(!(bool)Get(Build(), "CanCopy"), "Disabled Dimitus did not suppress the entire callout.");
        Set(Tag("DIMITUS"), "Enabled", true); Check((bool)Get(Build(), "CanCopy") && Body().EndsWith("dimitus"), "Enabled Dimitus did not restore the callout.");

        Reset(); ((IDictionary)Get(settings, "DungeonNameOverrides"))["Lost Halls"] = "lh";
        Check(Body() == "lh", "Dungeon custom name did not override its nickname.");
        Set(settings, "DungeonNameStyle", "Full"); Check(Body() == "lost halls", "Full dungeon name did not ignore the short override.");
        Set(settings, "DungeonNameStyle", "None"); Check((string)Get(Build(true), "Text") == "/p j" && (bool)Get(Build(true), "CanCopy"), "Name-less join call incorrectly became unavailable.");
        Set(settings, "DungeonNameStyle", "Short"); Set(context, "Name", "Some Unknown Dungeon"); Check(Body() == "some unknown dungeon", "Unknown dungeon did not retain a full-name fallback.");
        foreach (string name in new[] { "Crystal Cavern", "Cultist Hideout", "The Void", "Nexus", "{s.vault}", "Oryx's Castle" }) { Set(context, "Name", name); Check(!(bool)Get(Build(), "CanCopy"), "Unjoinable area generated a join call: " + name); }
        Set(context, "Name", "The Ivory Wyvern"); Check(Body() == "ivory", "Legacy/branch dungeon nickname was not independent.");
        Set(context, "Name", "{s.snake_pit}"); Check(Body() == "snake", "Localized dungeon did not resolve its short nickname.");
        Set(settings, "DungeonNameStyle", "Full"); Check(Body() == "snake pit", "Localized full dungeon name was copied as a raw string key."); Set(settings, "DungeonNameStyle", "Short");
        foreach (string name in new[] { "{s.the_void}", "{s.oryx_s_sanctuary}", "Wine Cellar", "Court of Oryx" }) { Set(context, "Name", name); Check(!(bool)Get(Build(), "CanCopy"), "Localized/special unjoinable area produced a join call: " + name); }
        Set(context, "Name", "Lost Halls"); Set(context, "OriginRealmPercent", 90); Set(settings, "RealmStatus", "Closing");
        Check(!Body().Contains("closing"), "90% realm was prematurely marked closed to joins.");
        Set(context, "OriginRealmPercent", 91); Check(Body().EndsWith("in a closing realm"), "Closing realm suffix was omitted.");
        Set(settings, "RealmStatus", "Score"); Set(settings, "RealmThreshold", 33); Set(context, "OriginRealmPercent", 33); Check(Body().EndsWith("in 33% realm"), "Realm threshold was not inclusive.");
        Set(context, "OriginRealmPercent", 32); Check(!Body().Contains("realm"), "Realm score below its threshold was emitted.");
        Set(context, "OpenedInNexus", true); Set(settings, "NexusMarker", "InNexus"); Check(Body().EndsWith("in nexus") && !Body().Contains("% realm"), "Nexus origin was confused with realm provenance.");
        Set(context, "Available", false); Check(!(bool)Get(Build(), "CanCopy"), "Expired join call remained copyable.");

        Reset(); Set(context, "Kind", "Event"); Set(context, "Name", "Ravenous Rot");
        Check(Body() == "rav rot, halls soon", "Event upcoming-dungeon hint is missing.");
        ((IDictionary)Get(settings, "EventNameOverrides"))["Ravenous Rot"] = "rot"; Check(Body() == "rot, halls soon", "Event custom name was ignored.");
        Set(settings, "EventNameStyle", "Full"); Check(Body() == "Ravenous Rot, halls soon", "Full event name casing or override behavior is wrong.");
        Set(settings, "EventNameStyle", "None"); Check(Body() == "halls soon", "Name-less event did not keep its upcoming hint.");
        Set(settings, "EventAddUpcoming", false); Check(Body() == "", "Disabled event name and upcoming hint left unwanted text.");
        Set(context, "Name", "Alien Invasion Adept - Wave 4"); Check(Body() == "adept wave 4 UFO soon", "Adept wave-4 boss hint failed.");
        Set(context, "Name", "Alien Invasion Veteran - Wave 4"); Set(settings, "AlienVeteranPrefix", "vets"); Check(Body() == "vets 4 calbrik soon", "Veteran custom prefix or wave-4 hint failed.");
        Set(settings, "AlienWave4Boss", false); Check(Body() == "vets 4", "Disabled alien boss hint remained.");
        Set(settings, "EventJoin", "Beginning"); Check((string)Get(Build(true), "Text") == "/p j vets 4", "Independent event join preference was ignored.");

        Check((string)Engine("DetectEvent", "#Oryx the Mad God", "{\"k\":\"stringlist.Ravenous_Rot.new.0\"}") == "Ravenous Rot", "Localized Oryx spawn did not resolve.");
        Check((string)Engine("DetectEvent", "#Oryx the Mad God", "{\"k\":\"stringlist.Flying_Behemoth_Egg.new.1\"}") == "Flying Behemoth", "Curated encounter override was missed.");
        Check((string)Engine("DetectEvent", "#Oryx the Mad God", "{\"k\":\"stringlist.WorldAPOSs_Oyster.new.0\"}") == "World's Oyster", "Localized apostrophe decoding failed.");
        Check(Engine("DetectEvent", "#Oryx the Mad God", "{\"k\":\"stringlist.AI_Alien_Taunt.new.0\"}") == null, "Invisible alien pre-event was announced.");
        Check(Engine("DetectEvent", "SomePlayer", "{\"k\":\"stringlist.Ravenous_Rot.new.0\"}") == null, "Player chat was promoted to a realm event.");
        Check(Engine("DetectEvent", "SomeOryx the Mad God", "{\"k\":\"stringlist.Ravenous_Rot.new.0\"}") == null, "Partial NPC sender name was treated as an authoritative announcement.");
        Check((string)Engine("DetectEvent", "#Oryx", "{\"k\":\"s.notice\",\"t\":{\"name\":{\"k\": \"stringlist.Ravenous_Rot.new.0\"}}}") == "Ravenous Rot", "Nested localized spawn tokens leaked their JSON prefix into the name.");
        Check((string)Engine("DetectEvent", "#Commander Calbrik", "Deploy punisher units for full elimination of life!") == "Alien Invasion Adept - Wave 4", "Alien dialogue did not resolve its wave.");
        Check((string)Engine("DetectEvent", "#Commander Calbrik", "Fine, I'll do it myself...") == "UFO", "Real UFO announcement was missed.");
        Check((string)Engine("DetectEvent", "#Mysterious Crystal", "My riches await!") == "Mysterious Crystal", "Crystal encounter was missed.");
        Check((string)Engine("DetectEvent", "#Herman the Rodent Hunter", "A colossal presence approaches!") == "Mammoth Rat", "Mammoth Rat announcement was missed.");
        Check(((string[])Engine("ParseRoster", "Players online (4): Zed, alice, Bob, ALICE")).SequenceEqual(new[] { "alice", "Bob", "Zed" }), "Who roster did not sort and deduplicate names.");
        Check(((string[])Engine("ParseRoster", "Players online (0): ")).Length == 0 && Engine("ParseRoster", "Ordinary conversation") == null, "Who roster empty/non-roster handling failed.");
        Check((string)Engine("KeyThanks", "Alice,a19d", "Me", settings) == "Thanks Alice for the key!" && (string)Engine("KeyThanks", "ME,a19d", "Me", settings) == "", "Key thanks misidentified the opener or thanked the local player.");
        object inc = Engine("ParseAreaUnlock", "Wine Cellar unlocked by Alice");
        object rune = Engine("ParseAreaUnlock", "{\"k\":\"s.something_by_player\",\"t\":{\"name\":\"The Shield Monument has been activated\",\"player\":\"Bob,1cf5\",}}");
        Check((string)Get(inc, "Item") == "inc" && (int)Get(rune, "MonumentIndex") == 1 && (string)Get(rune, "Player") == "Bob", "Area unlock text/lenient JSON did not resolve.");
        Array unlocks = Array.CreateInstance(T("HudAreaUnlock"), 1); unlocks.SetValue(inc, 0);
        Check((string)Engine("AreaThanks", unlocks, "Me", settings) == "Thanks Alice for the inc!", "Single area thanks failed.");
        unlocks = Array.CreateInstance(T("HudAreaUnlock"), 2); unlocks.SetValue(rune, 0); unlocks.SetValue(Engine("ParseAreaUnlock", "The Sword Monument has been activated by Alice"), 1);
        Check((string)Engine("AreaThanks", unlocks, "Me", settings) == "Thanks Bob, Alice for the runes!", "Combined rune thanks failed.");
        Check((string)Engine("AreaThanks", unlocks, "Bob", settings) == "Thanks Alice for the sword rune!", "Local rune contributor was not excluded.");
        Set(settings, "KeyPopThanks", "/p ty {player} for {item}"); Check((string)Engine("KeyThanks", "Alice", "Me", settings) == "/p ty Alice for key", "Custom thanks template was ignored.");
        Check(Engine("ParseAreaUnlock", "Something unrelated unlocked by Alice") == null, "Unknown area unlock was given invented semantics.");

        Set(context, "Kind", "Message"); Set(context, "Name", "An old event"); Set(context, "Location", "The Nest"); Set(context, "Server", "USMidWest2"); Set(context, "Realm", "Medusa");
        Check((string)Get(Build(true, true, true), "Text") == "/p USMidWest2 \u00b7 Medusa \u00b7 The Nest \u00b7 An old event", "Legacy message callout context changed.");
        Set(context, "Kind", "Thanks"); Set(context, "Name", "Thanks Alice for the key!"); Check((string)Get(Build(true, true, true), "Text") == "Thanks Alice for the key!", "Thanks callout inherited an unrelated location or join prefix.");

        Set(settings, "LearnEventRuns", 100); Set(settings, "ExpirySeconds", 180); Set(settings, "DungeonNameStyle", "Invalid"); Set(settings, "RewardTags", null); Call(settings, "Normalize");
        Check((int)Get(settings, "LearnEventRuns") == 10 && (int)Get(settings, "ExpirySeconds") == 30 && (string)Get(settings, "DungeonNameStyle") == "Short", "Saved callout settings were not normalized.");
        string json = new JavaScriptSerializer().Serialize(settings); object restored = new JavaScriptSerializer().Deserialize(json, T("HudCalloutSettings")); Call(restored, "Normalize");
        Check((string)Get(restored, "KeyPopThanks") == "/p ty {player} for {item}" && ((IDictionary)Get(restored, "EventNameOverrides")).Contains("Ravenous Rot"), "Callout templates/name overrides did not survive serialization.");
        ((Task)Call(T("HudCalloutModifiers"), "LoadDefaultAsync")).GetAwaiter().GetResult();
        object production = Get(T("HudCalloutModifiers"), "Current"); Check((int)Get(production, "Count") > 100, "Packaged current-game modifier definitions did not load.");
        Reset(); Set(context, "DungeonModifiers", "LOOTING");
        object packaged = Engine("Build", context, settings, false, false, false);
        Check(Get(packaged, "LootBonus") != null && ((int)Get(packaged, "LootBonus")) > 0, "Production modifier table did not produce a known reward bonus.");
        Console.WriteLine("PASS: " + checks + " callout naming, modifiers, provenance, message helpers, and persistence checks.");
    }
}
'@
[CalloutRegression]::Run($AssemblyPath)
