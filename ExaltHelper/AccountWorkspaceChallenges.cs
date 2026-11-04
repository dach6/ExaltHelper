using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExaltHelper.Proxy.Networking;

namespace ExaltHelper;

internal sealed class AccountChallenge
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public double[] FlatStats { get; set; } = new double[8];
    public double[] StatMultipliers { get; set; } = Enumerable.Repeat(1d, 8).ToArray();
    public double DamageMultiplier { get; set; } = 1;
    public double LootPercent { get; set; }
    public double XpPercent { get; set; }
    public double BattlePassPercent { get; set; }
    public string[] Effects { get; set; } = Array.Empty<string>();
    public AccountChallenge Clone() => new AccountChallenge { Id = Id, Title = Title, FlatStats = (double[])FlatStats.Clone(), StatMultipliers = (double[])StatMultipliers.Clone(),
        DamageMultiplier = DamageMultiplier, LootPercent = LootPercent, XpPercent = XpPercent, BattlePassPercent = BattlePassPercent, Effects = (string[])Effects.Clone() };
}

internal static partial class AccountWorkspace
{
    private static string ReadPacketString(PacketReader reader)
    {
        int length = reader.ReadUInt16(); byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        return Encoding.UTF8.GetString(bytes);
    }
    private static void ApplyChallengeFeedLocked(PacketReader reader)
    {
        int ids = reader.ReadInt16(); if (ids < 0 || ids > 4096) throw new InvalidDataException();
        for (int i = 0; i < ids; i++) reader.ReadInt32();
        int count = reader.ReadInt16(); if (count < 0 || count > 4096) throw new InvalidDataException();
        var jsons = new List<string>(); for (int i = 0; i < count; i++) jsons.Add(ReadPacketString(reader));
        ApplyChallengeJsonLocked(jsons.ToArray());
    }
    private static IEnumerable<Dictionary<string, object>> JsonObjects(object value) => value is IEnumerable values && !(value is string)
        ? values.Cast<object>().OfType<Dictionary<string, object>>() : Enumerable.Empty<Dictionary<string, object>>();
    private static object JsonValue(Dictionary<string, object> value, string key) => value != null && value.TryGetValue(key, out object item) ? item : null;
    private static double JsonNumber(Dictionary<string, object> value, string key, double fallback = 0) => double.TryParse(Convert.ToString(JsonValue(value, key), CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsNaN(number) && !double.IsInfinity(number) ? number : fallback;
    private static string JsonText(Dictionary<string, object> value, string key) => Convert.ToString(JsonValue(value, key), CultureInfo.InvariantCulture) ?? "";
    private static void ApplyChallengeJsonLocked(string[] jsons)
    {
        AccountChallenge crucible = null; var rituals = new List<AccountChallenge>(); bool parsed = false;
        foreach (string json in jsons)
        {
            Dictionary<string, object> root;
            try { root = Serializer().DeserializeObject(json) as Dictionary<string, object>; }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { continue; }
            if (root == null || !(JsonValue(root, "array") is IEnumerable entries) || entries is string) continue;
            parsed = true;
            foreach (var entry in JsonObjects(JsonValue(root, "array")))
            {
                bool ritual = JsonValue(entry, "bloodRitual") is Dictionary<string, object>;
                var body = JsonValue(entry, ritual ? "bloodRitual" : "crucible") as Dictionary<string, object>;
                if (body == null) continue;
                var def = new AccountChallenge { Id = JsonText(entry, "id"), Title = JsonText(entry, "title") };
                var effects = new List<string>();
                foreach (var restriction in JsonObjects(JsonValue(body, "restrictions")))
                {
                    int kind = (int)JsonNumber(restriction, "type");
                    if (kind == 2)
                    {
                        int stat = Array.IndexOf(new[] { "HP", "MP", "ATT", "DEF", "SPD", "DEX", "VIT", "WIS" }, JsonText(restriction, "stat").ToUpperInvariant());
                        if (stat < 0) continue;
                        if (JsonNumber(restriction, "percent") != 0) def.StatMultipliers[stat] *= JsonNumber(restriction, "value", 1);
                        else def.FlatStats[stat] += JsonNumber(restriction, "value");
                    }
                    else if (kind == 5) effects.Add(JsonText(restriction, "effects") + ": " + (JsonNumber(restriction, "effectChance") * 100).ToString("0.#")
                        + "% chance on hits of " + JsonNumber(restriction, "threshold") + "+ damage; " + JsonNumber(restriction, "effectsDuration") + "s duration / " + JsonNumber(restriction, "effectsCooldown") + "s cooldown");
                }
                def.Effects = effects.ToArray();
                foreach (var bonus in JsonObjects(JsonValue(body, "bonuses")))
                {
                    if (ritual && JsonNumber(bonus, "percent") == 0) continue;
                    double amount = JsonNumber(bonus, "amount", 1); int kind = (int)JsonNumber(bonus, "type");
                    if (kind == 1) def.BattlePassPercent = (amount - 1) * 100;
                    if (kind == 2) def.XpPercent = (amount - 1) * 100;
                    if (kind == 3) def.LootPercent = (amount - 1) * 100;
                    if (kind == 4 && ritual) def.StatMultipliers[0] = amount;
                    if (kind == 5) def.DamageMultiplier = amount;
                }
                if (ritual) rituals.Add(def); else if (crucible == null) crucible = def;
            }
        }
        if (crucible != null)
        {
            if (state.Crucible != null && state.Crucible.Id != crucible.Id) foreach (var c in state.Characters) c.Crucible = false;
            state.Crucible = crucible;
        }
        if (parsed) state.BloodRituals = rituals;
        if (parsed || crucible != null) TouchLocked();
    }
}
