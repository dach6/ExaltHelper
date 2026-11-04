using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ExaltHelper;

internal sealed class HudAreaUnlock
{
    public string Title { get; set; }
    public string Item { get; set; }
    public string Player { get; set; }
    public int MonumentIndex { get; set; } = -1;
}

internal static partial class HudCallouts
{
    // Sender gating prevents ordinary player conversations from becoming events.
    public static string DetectEvent(string sender, string text)
    {
        sender ??= ""; text ??= "";
        string source = PlayerName(sender);
        if (source.Equals("Mysterious Crystal", StringComparison.OrdinalIgnoreCase) || source.Equals("New Mysterious Crystal", StringComparison.OrdinalIgnoreCase)) return "Mysterious Crystal";
        if (source.Equals("Herman the Rodent Hunter", StringComparison.OrdinalIgnoreCase))
            return text.IndexOf("colossal pres", StringComparison.OrdinalIgnoreCase) >= 0 ? "Mammoth Rat" : null;
        if (source.Equals("Commander Calbrik", StringComparison.OrdinalIgnoreCase))
        {
            if (text.IndexOf("Fine, I'll do it myself", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Fine, I’ll do it myself", StringComparison.OrdinalIgnoreCase) >= 0) return "UFO";
            string[] phrases = { "Atmosphere breach imminent", "extermination units", "be faced with extreme prejudice", "Deploy punisher units", "The members of the Calbrik Guard will grant you", "Release the Mech Squad", "even those creepy pods", "How can eliminating such simple humanoid creatures" };
            for (int i = 0; i < phrases.Length; i++) if (text.IndexOf(phrases[i], StringComparison.OrdinalIgnoreCase) >= 0) return "Alien Invasion " + (i < 4 ? "Adept" : "Veteran") + " - Wave " + (i % 4 + 1);
            return null;
        }
        if (!source.Equals("Oryx the Mad God", StringComparison.OrdinalIgnoreCase) && !(sender.StartsWith("#", StringComparison.Ordinal) && source.Equals("Oryx", StringComparison.OrdinalIgnoreCase))) return null;
        var spawn = Regex.Match(text, "\"k\"\\s*:\\s*\"stringlist\\.([A-Za-z0-9_']+)\\.new\\.\\d+\"");
        if (!spawn.Success) return HudCalloutNames.Events.Keys.FirstOrDefault(x => Canonical(x) == Canonical(text));
        string key = spawn.Groups[1].Value;
        if (key == "AI_Alien_Taunt") return null;
        switch (key)
        {
            case "Lord_of_the_Lost_Lands": return "Lord of the Lost Lands";
            case "Flying_Behemoth_Egg": return "Flying Behemoth";
            case "Temple_Encounter": return "Jade and Garnet Statues";
            case "Beer_Encounter_Spawner": return "Beer God";
            case "EH_Event_Taunt_Controller": return "Killer Bee Nest";
        }
        int jsonStart = text.LastIndexOf('{', spawn.Index);
        string display = jsonStart > 0 ? text.Substring(0, jsonStart).Trim() : "";
        if (display.Contains("{") || display.Contains("\"")) display = "";
        if (display.Length == 0) display = key.Replace('_', ' ').Replace("APOSs", "'s").Replace("APOS", "'");
        return HudCalloutNames.Events.Keys.FirstOrDefault(x => Canonical(x) == Canonical(display)) ?? display;
    }

    public static string[] ParseRoster(string text)
    {
        var match = Regex.Match((text ?? "").Trim(), @"^Players online \(\d+\):\s*(.*)$", RegexOptions.Singleline);
        if (!match.Success) return null;
        return match.Groups[1].Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string PlayerName(string raw) => (raw ?? "").Split(',')[0].Trim().TrimStart('#').Trim();
    public static string ParseKeyOpener(string text)
    {
        var match = Regex.Match(text ?? "", @"(?:^|\s)Opened by\s+([^\r\n]+)$", RegexOptions.IgnoreCase);
        return match.Success ? PlayerName(match.Groups[1].Value) : null;
    }
    public static string KeyThanks(string opener, string localPlayer, HudCalloutSettings settings)
    {
        opener = PlayerName(opener); localPlayer = PlayerName(localPlayer);
        if (opener.Length == 0 || opener.Equals(localPlayer, StringComparison.OrdinalIgnoreCase)) return "";
        return FormatThanks(settings?.KeyPopThanks ?? "Thanks {player} for the key!", opener, "key");
    }
    public static HudAreaUnlock ParseAreaUnlock(string text)
    {
        text = (text ?? "").Trim(); string subject = null, player = null;
        if (text.Contains("s.dungeon_unlocked_by") || text.Contains("s.something_by_player"))
        {
            subject = JsonString(text, "name"); player = JsonString(text, "player");
            if (subject != null && subject.EndsWith(" has been activated", StringComparison.Ordinal)) subject = subject.Substring(0, subject.Length - " has been activated".Length);
        }
        else
        {
            foreach (string marker in new[] { " unlocked by ", " has been activated by " })
            {
                int position = text.IndexOf(marker, StringComparison.Ordinal);
                if (position < 0) continue;
                subject = text.Substring(0, position).Trim(); player = text.Substring(position + marker.Length); break;
            }
        }
        player = PlayerName(player);
        if (subject == null || player.Length == 0) return null;
        if (subject == "Wine Cellar") return new HudAreaUnlock { Title = "Wine Cellar unlocked", Item = "inc", Player = player };
        if (subject == "The Void") return new HudAreaUnlock { Title = "The Void unlocked", Item = "vial", Player = player };
        string[] monuments = { "Sword", "Shield", "Helmet" };
        for (int i = 0; i < monuments.Length; i++)
            if (subject == "The " + monuments[i] + " Monument") return new HudAreaUnlock { Title = "Sanctuary runes", Item = monuments[i].ToLowerInvariant() + " rune", Player = player, MonumentIndex = i };
        return null;
    }
    public static string AreaThanks(IEnumerable<HudAreaUnlock> contributions, string localPlayer, HudCalloutSettings settings)
    {
        localPlayer = PlayerName(localPlayer);
        var eligible = (contributions ?? Enumerable.Empty<HudAreaUnlock>()).Where(x => x != null && PlayerName(x.Player).Length > 0 && !PlayerName(x.Player).Equals(localPlayer, StringComparison.OrdinalIgnoreCase)).ToList();
        if (eligible.Count == 0) return "";
        string players = string.Join(", ", eligible.Select(x => PlayerName(x.Player)).Distinct(StringComparer.OrdinalIgnoreCase));
        string item = eligible.Count == 1 ? eligible[0].Item : eligible.All(x => x.MonumentIndex >= 0) ? "runes" : string.Join(", ", eligible.Select(x => x.Item).Distinct());
        return FormatThanks(settings?.AreaUnlockThanks ?? "Thanks {player} for the {item}!", players, item);
    }
    private static string FormatThanks(string template, string player, string item) => CleanText((template ?? "").Replace("{player}", player).Replace("{item}", item));
    private static string JsonString(string text, string field)
    {
        // The game's notification strings can contain trailing commas; decode
        // the individual JSON string while leaving the surrounding text alone.
        var match = Regex.Match(text, "\"" + Regex.Escape(field) + "\"\\s*:\\s*(\"(?:\\\\.|[^\"\\\\])*\")");
        if (!match.Success) return null;
        try { return new JavaScriptSerializer().Deserialize<string>(match.Groups[1].Value); }
        catch (ArgumentException) { return null; }
    }
}
