using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExaltHelper;

internal static partial class HudCallouts
{
    private static readonly string[] EventPresets = { "STEAMWORKSMAINTENANCE", "FOOUNDTREASURE", "LOOTING", "CRABRAVE", "REWARDING", "CHEF", "SOUVENIR" };
    private static readonly string[] NonCallable = { "Crystal Cavern", "Cultist Hideout", "The Void" };
    public static string Canonical(string text) => new string((text ?? "").Where(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')).Select(char.ToUpperInvariant).ToArray());
    public static string[] ParseModifierTokens(string raw) => (raw ?? "").Split(';').Select(x => x.Split('|')[0].Trim()).Where(x => x.Length > 0).ToArray();
    public static string ModifierGrade(string raw) => (raw ?? "").Split(';').Where(x => x.Contains("|")).Select(x => x.Substring(x.IndexOf('|') + 1).Trim()).LastOrDefault(x => x.Length > 0) ?? "";
    public static string ShortServer(string name) => HudCalloutNames.Servers.TryGetValue(name ?? "", out string value) ? value : name ?? "";
    private static string MapKey(string name) { string value = (name ?? "").Trim(); if (value.StartsWith("{s.", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal)) value = value.Substring(3, value.Length - 4); return Canonical(value); }
    public static bool IsRealm(string name) { string value = MapKey(name); return value == "REALM" || value == "REALMOFTHEMADGOD" || value == "ROTMG" || value == "ORYXSREALM" || value == "LEGACYREALM"; }
    public static bool IsHub(string name)
    {
        string value = MapKey(name);
        return new[] { "NEXUS", "VAULT", "GUILDHALL", "GUILD", "BAZAAR", "CLOTHBAZAAR", "GRANDBAZAAR", "MARKETPLACE", "DAILYQUESTROOM", "PETYARD", "TUTORIAL", "TESTING", "LOADING", "WAITINGFORGAME" }.Any(x => value == x || (x == "PETYARD" && value.StartsWith(x, StringComparison.Ordinal)));
    }
    public static bool IsDungeon(string name) => !string.IsNullOrWhiteSpace(name) && !IsRealm(name) && !IsHub(name) && !new[] { "ORYXSCASTLE", "ORYXCASTLE", "ORYXSCHAMBER", "ORYXCHAMBER", "ORYXSCOURT", "ORYXCOURT", "COURTOFORYX", "ORYXSSANCTUARY", "ORYXSANCTUARY", "WINECELLAR" }.Contains(MapKey(name));
    private static string Lookup(Dictionary<string, string> values, string name) => values.FirstOrDefault(p => MapKey(p.Key) == MapKey(name)).Value;
    private static string ResolveName(string name, string style, Dictionary<string, string> overrides, Dictionary<string, string> builtIn, bool lower)
    {
        name = (name ?? "").Trim();
        if (name.StartsWith("{s.", StringComparison.Ordinal) && name.EndsWith("}", StringComparison.Ordinal))
            name = builtIn.Keys.FirstOrDefault(k => MapKey(k) == MapKey(name)) ?? name.Substring(3, name.Length - 4).Replace('_', ' ');
        if (style == "None") return "";
        if (style == "Short")
        {
            string custom = overrides != null && overrides.TryGetValue(name, out string exact) ? exact : null;
            if (!string.IsNullOrWhiteSpace(custom)) return CleanText(custom);
            string shortName = Lookup(builtIn, name); if (shortName != null) return shortName;
        }
        return lower ? name.ToLowerInvariant() : name;
    }
    public static HudCalloutResult Build(HudCalloutContext context, HudCalloutSettings settings, bool partyPrefix, bool includeServer, bool includeRealm)
    {
        _ = HudCalloutModifiers.LoadDefaultAsync();
        return BuildWithCatalog(context, settings, partyPrefix, includeServer, includeRealm, HudCalloutModifiers.Current);
    }
    public static HudCalloutResult BuildWithCatalog(HudCalloutContext context, HudCalloutSettings settings, bool partyPrefix, bool includeServer, bool includeRealm, HudModifierCatalog catalog)
    {
        context ??= new HudCalloutContext(); settings ??= new HudCalloutSettings(); catalog ??= new HudModifierCatalog();
        var result = new HudCalloutResult(); string join = "None";
        bool callable = context.Available;
        if (context.Kind == "Dungeon")
        {
            join = settings.DungeonJoin;
            callable &= IsDungeon(context.Name) && !NonCallable.Any(x => MapKey(x) == MapKey(context.Name));
            var tokens = ParseModifierTokens(context.DungeonModifiers);
            var definitions = tokens.Select(catalog.Get).ToArray();
            result.UnknownModifiers = tokens.Where((x, i) => definitions[i] == null).Distinct().ToArray();
            int loot = 0, dust = 0, xp = 0;
            foreach (var definition in definitions.Where(x => x != null))
            { loot = HudCalloutModifiers.Add(loot, definition.Loot); dust = HudCalloutModifiers.Add(dust, definition.Dust); xp = HudCalloutModifiers.Add(xp, definition.Xp); }
            bool known = context.DungeonModifiers != null && result.UnknownModifiers.Length == 0;
            result.LootBonus = known ? (int?)loot : null; result.DustBonus = known ? (int?)dust : null; result.XpBonus = known ? (int?)xp : null;
            var parts = new List<string> { ResolveName(context.Name, settings.DungeonNameStyle, settings.DungeonNameOverrides, HudCalloutNames.Dungeons, true) };
            // A partial sum must not be presented as the full dungeon bonus.
            if (known)
            {
                AddBonus(parts, loot, settings.LootThreshold, settings.LootLabel, settings.Percent);
                AddBonus(parts, dust, settings.DustThreshold, settings.DustLabel, settings.Percent);
                AddBonus(parts, xp, settings.XpThreshold, settings.XpLabel, settings.Percent);
            }
            var tags = settings.RewardTags ?? HudCalloutNames.DefaultTags();
            foreach (var tag in tags.Where(t => t != null && Canonical(t.Id) != "DIMITUS"))
            {
                if (!settings.CallEventModifiers && (EventPresets.Any(p => MatchesBase(tag.Id, p)) ||
                    (settings.LearnEventModifiers && (context.LearnedEventModifiers ?? Array.Empty<string>()).Any(p => MatchesBase(p, Canonical(tag.Id)))))) continue;
                AddTag(parts, tokens, tag);
            }
            int? thessal = tokens.Select(t => Canonical(t)).Select(t => t == "ALEXANDERSLEGACY" ? (int?)100 : t == "ALEXANDERSLEGACY1" ? 25 : t == "ALEXANDERSLEGACY2" ? 50 : t == "ALEXANDERSLEGACY3" ? 75 : (int?)null).FirstOrDefault(t => t.HasValue);
            if (thessal.HasValue) parts.Add("dying thessal " + thessal + (settings.Percent ? "%" : ""));
            var dimitus = tags.FirstOrDefault(t => t != null && Canonical(t.Id) == "DIMITUS");
            if (tokens.Any(t => Canonical(t) == "DIMITUS"))
            {
                callable &= dimitus?.Enabled == true;
                if (dimitus != null) AddTag(parts, tokens, dimitus);
            }
            string origin = Origin(context, settings); if (origin.Length > 0) parts.Add(origin);
            result.Body = string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
            var details = definitions.Where(x => x != null).Select(x => x.Name + (string.IsNullOrEmpty(x.Description) ? "" : ": " + x.Description)).ToList();
            if (!known) details.Add(context.DungeonModifiers == null ? "Dungeon modifiers were not observed; reward bonuses are unknown." : "Reward bonuses are unknown for: " + string.Join(", ", result.UnknownModifiers) + ".");
            if (!callable) details.Add(context.Available ? "This dungeon cannot be called for joining." : "The observed join window has ended.");
            result.Details = string.Join(Environment.NewLine, details);
        }
        else if (context.Kind == "Event")
        {
            join = settings.EventJoin;
            var wave = Regex.Match(context.Name ?? "", @"^Alien Invasion (Adept|Veteran)\s*-?\s*Wave\s+(\d+)$", RegexOptions.IgnoreCase);
            if (wave.Success)
            {
                bool veteran = wave.Groups[1].Value.Equals("Veteran", StringComparison.OrdinalIgnoreCase);
                string prefix = veteran ? settings.AlienVeteranPrefix : settings.AlienAdeptPrefix;
                result.Body = (string.IsNullOrWhiteSpace(prefix) ? "wave" : CleanText(prefix)) + " " + wave.Groups[2].Value;
                if (settings.AlienWave4Boss && wave.Groups[2].Value == "4") result.Body += veteran ? " calbrik soon" : " UFO soon";
            }
            else
            {
                result.Body = ResolveName(context.Name, settings.EventNameStyle, settings.EventNameOverrides, HudCalloutNames.Events, false);
                string hint = settings.EventAddUpcoming ? Lookup(HudCalloutNames.Upcoming, context.Name) : null;
                if (hint != null) result.Body += (result.Body.Length > 0 ? ", " : "") + hint;
            }
        }
        else if (context.Kind == "Thanks" || context.Kind == "Roster")
        {
            result.Body = CleanText(context.Name); result.Text = result.Body;
            result.CanCopy = callable && result.Text.Length > 0; return result;
        }
        else
        {
            result.Body = string.Join(" · ", new[] { context.Location, context.Name }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(CleanText));
            result.Text = (partyPrefix ? "/p " : "") + string.Join(" · ", new[] { includeServer ? context.Server : "", includeRealm ? context.Realm : "", result.Body }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(CleanText));
            result.CanCopy = callable && result.Text.Trim().Length > 0; return result;
        }
        var text = new List<string>();
        if (partyPrefix) text.Add("/p"); if (join == "Beginning") text.Add("j");
        if (includeServer) text.Add(ShortServer(context.Server)); if (includeRealm) text.Add(context.Realm);
        text.Add(result.Body); if (join == "End") text.Add("j");
        result.Text = string.Join(" ", text.Where(x => !string.IsNullOrWhiteSpace(x)).Select(CleanText));
        result.CanCopy = callable && result.Text.Length > 0; return result;
    }
    private static void AddBonus(List<string> parts, int value, int threshold, string label, bool percent)
    { if (value >= threshold && !string.IsNullOrWhiteSpace(label) && label != "None") parts.Add(value.ToString(CultureInfo.InvariantCulture) + (percent ? "%" : "") + " " + label); }
    private static bool MatchesBase(string token, string baseId)
    { string key = Canonical(token); baseId = Canonical(baseId); return baseId.Length > 0 && key.StartsWith(baseId, StringComparison.Ordinal) && key.Substring(baseId.Length).All(c => c >= '0' && c <= '9'); }
    private static void AddTag(List<string> parts, string[] tokens, HudCalloutTag tag)
    {
        if (!tag.Enabled || string.IsNullOrWhiteSpace(tag.Short)) return;
        string key = Canonical(tag.Id);
        bool carried = key == "SYNDICATE" ? tokens.Any(t => new[] { "MERCA", "MERCB", "MERCC", "MERCD" }.Contains(Canonical(t))) : tokens.Any(t => MatchesBase(t, key));
        if (carried) parts.Add(CleanText(tag.Short));
    }
    private static string Origin(HudCalloutContext context, HudCalloutSettings settings)
    {
        if (context.OpenedInNexus == true) return settings.NexusMarker == "Nex" ? "nex" : settings.NexusMarker == "InNexus" ? "in nexus" : "";
        if (!context.OriginRealmPercent.HasValue) return "";
        int score = Math.Max(0, Math.Min(100, context.OriginRealmPercent.Value));
        return settings.RealmStatus == "Closing" && score > 90 ? "in a closing realm" : settings.RealmStatus == "Score" && score >= settings.RealmThreshold ? "in " + score + "% realm" : "";
    }
    private static string CleanText(string text) => Regex.Replace(text ?? "", @"[\r\n\t]+", " ").Trim();
}
