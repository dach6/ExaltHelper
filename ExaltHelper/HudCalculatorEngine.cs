using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static ExaltHelper.HudCalculatorData;

namespace ExaltHelper;

internal sealed class HudCalculatorStats
{
    public readonly double[] Gear = new double[8];
    public readonly int[] Effective = new int[8];
    public double HpRegen, MpRegen, HpStatRegen, MpStatRegen, HpGearRegen, MpGearRegen, MpMultiplier = 1, RateMultiplier = 1;
    public double LootBonus, XpBonus, DustBonus;
    public readonly List<string> Notes = new List<string>();
}

internal sealed class HudCalculatorLine
{
    public string Name = "", Detail = "";
    public double Damage, MultiTargetDamage;
    public bool Conditional;
}

internal sealed class HudCalculatorAbility
{
    public readonly List<HudCalculatorLine> Lines = new List<HudCalculatorLine>();
    public readonly List<string> Notes = new List<string>();
    public double DamagePerUse => Lines.Where(x => !x.Conditional).Sum(x => x.Damage);
    public double MultiTargetDamage => Lines.Where(x => !x.Conditional).Sum(x => x.MultiTargetDamage);
}

internal sealed class HudCalculatorProficiency
{
    public double[] StatBonuses = new double[8];
    public double MasteryPercent, XpPercent, LootPercent, ArmorCombatReduction;
}

internal static class HudCalculatorEngine
{
    public static readonly string[] StatNames = { "HP", "MP", "ATT", "DEF", "SPD", "DEX", "VIT", "WIS" };
    public static int Round(double n) => (int)Math.Round(n, MidpointRounding.AwayFromZero);
    private static int Positive(double n) => Math.Max(0, Round(n));
    private static double At(double[] a, int i) => a != null && i < a.Length ? a[i] : 0;
    private static double Stat(int[] stats, string name) { int i = StatIndex(name); return i < 0 || stats == null || i >= stats.Length ? 0 : stats[i]; }
    private static double Delta(XElement e, int[] stats, double scale = 1, string fallbackStat = null)
        => Math.Max(0, Stat(stats, (string)e.Attribute("scalingStat") ?? fallbackStat) - Attr(e, "statModScalingMin")) * scale;
    private static string Scaling(XElement e) => e.Attribute("scalingStat") == null ? "" : "; scales with " + (string)e.Attribute("scalingStat") + " above " + Attr(e, "statModScalingMin").ToString("0.#");

    public static HudCalculatorProficiency Proficiency(AccountSnapshot snapshot, int classType)
    {
        var result = new HudCalculatorProficiency();
        int[] Levels(int type)
        {
            snapshot.Exaltations.TryGetValue(type.ToString(System.Globalization.CultureInfo.InvariantCulture), out int[] raw);
            return Enumerable.Range(0, 8).Select(i => raw != null && i < raw.Length ? AccountWorkspace.ExaltLevel(raw[i]) : 0).ToArray();
        }
        int[] levels = Levels(classType);
        result.StatBonuses = levels.Select((x, i) => (double)x * (i < 2 ? 5 : 1)).ToArray();
        result.MasteryPercent = Math.Min(4, levels.Min()) * 2.5;
        result.XpPercent = Math.Min(4, levels.Sum() / 8) * 5;
        int[][] weaponGroups = { new[] { 768, 800, 804 }, new[] { 775, 802, 796 }, new[] { 782, 801, 803 }, new[] { 784, 805, 817, 819 }, new[] { 797, 798, 799 }, new[] { 806, 785, 818 } };
        int[][] armorGroups = { new[] { 797, 798, 799, 785, 818 }, new[] { 768, 775, 806, 802, 800, 804, 819 }, new[] { 782, 784, 801, 803, 805, 817, 796 } };
        result.LootPercent = (weaponGroups.FirstOrDefault(g => g.Contains(classType)) ?? new[] { classType }).Min(t => Levels(t).Min()) * 5;
        result.ArmorCombatReduction = (armorGroups.FirstOrDefault(g => g.Contains(classType)) ?? new[] { classType }).Min(t => Levels(t).Min()) * .2;
        return result;
    }

    public static HudCalculatorStats Stats(double[] baseStats, int[] equipment, string[] codes, double[] exalt = null, double[] adjustment = null)
    {
        var result = new HudCalculatorStats();
        var relatives = new List<Tuple<int, int, double, bool>>();
        double flatHp = 0, flatMp = 0, pctHp = 0, pctMp = 0;
        void Apply(XElement e)
        {
            string type = e.Value.Trim(); int stat = StatIndex((string)e.Attribute("stat")); double amount = Attr(e, "amount");
            if (type == "IncrementStat" && stat >= 0) result.Gear[stat] += amount;
            else if (type == "IncrementStatRelative" || type == "BonusStatRelative")
            {
                int relative = StatIndex((string)e.Attribute("statRelativeTo"));
                if (stat >= 0 && relative >= 0) relatives.Add(Tuple.Create(stat, relative, amount / 100, type == "BonusStatRelative"));
            }
            else if (type == "AbilityUseDiscount") result.MpMultiplier *= Attr(e, "multiplier", 1);
            else if (type == "FlatRegen") { if (stat == 0) flatHp += amount; if (stat == 1) flatMp += amount; }
            else if (type == "PercentageRegen") { if (stat == 0) pctHp += amount; if (stat == 1) pctMp += amount; }
            else if (type == "LootBonus") result.LootBonus += amount;
            else if (type == "XPBonus") result.XpBonus += amount;
            else if (type == "DustBonus") result.DustBonus += amount;
            else if (type == "DamageMultSelf") result.Notes.Add("DamageMultSelf equipment multiplier requires its condition; not included automatically.");
        }
        for (int slot = 0; slot < Math.Min(4, equipment?.Length ?? 0); slot++)
        {
            if (equipment[slot] <= 0) continue;
            var item = Item(equipment[slot]);
            if (item == null) { result.Notes.Add("Unknown equipment " + equipment[slot] + "; stats incomplete."); continue; }
            result.XpBonus += item.XpBonus;
            foreach (var e in item.EquipEffects) Apply(e);
            string code = codes != null && slot < codes.Length ? codes[slot] : null;
            if (HasUnknownEnchants(code)) result.Notes.Add("Unknown enchantment; imported bonuses may be incomplete.");
            foreach (var enchant in Enchants(code))
                foreach (var e in enchant.Elements("Mutators").Elements())
                {
                    if (e.Name.LocalName == "ActivateOnEquip") Apply(e);
                    else if (e.Name.LocalName == "MultiplyMPCost" && slot == 1) result.MpMultiplier *= Value(e, 1);
                    else if (e.Name.LocalName == "MultiplyRateOfFire" && slot == 0 && Attr(e, "subAttackIndex", -1) <= 0) result.RateMultiplier *= Value(e, 1);
                    else if (e.Name.LocalName.StartsWith("AddOn", StringComparison.Ordinal) || e.Name.LocalName == "SetSubAttack" || e.Name.LocalName == "AddSubAttack")
                        result.Notes.Add("Conditional enchant procs and added/replaced attacks are excluded.");
                }
        }
        double[] flat = (double[])result.Gear.Clone();
        foreach (var relative in relatives)
            result.Gear[relative.Item1] += (flat[relative.Item2] + At(exalt, relative.Item2) + (relative.Item4 ? 0 : At(baseStats, relative.Item2))) * relative.Item3;
        for (int i = 0; i < 8; i++) result.Effective[i] = Positive(At(baseStats, i) + result.Gear[i] + At(exalt, i) + At(adjustment, i));
        result.HpStatRegen = 2 * (1 + .12 * result.Effective[6]); result.MpStatRegen = 2 * (.5 + .06 * result.Effective[7]);
        result.HpGearRegen = 2 * (flatHp + pctHp * result.Effective[0]); result.MpGearRegen = 2 * (flatMp + pctMp * result.Effective[1]);
        result.HpRegen = result.HpStatRegen + result.HpGearRegen; result.MpRegen = result.MpStatRegen + result.MpGearRegen;
        return result;
    }

    public static ItemSpriteManager.ItemMeta Weapon(HudCalculatorItem item, string code, double rangeMultiplier, double rateMultiplier)
    {
        if (item == null) return null;
        // Clone metadata: calculator changes must never alter the live damage catalog.
        var source = ItemSpriteManager.GetItemMetadata(item.Id);
        var result = new ItemSpriteManager.ItemMeta { ItemId = item.Id, Name = item.Name, IsWeapon = true, SlotType = item.Slot,
            RateOfFire = (float)(source.RateOfFire * rateMultiplier), NumProjectiles = source.NumProjectiles };
        IEnumerable<ItemSpriteManager.ProjectileInfo> projectiles = source.Projectiles;
        if (!projectiles.Any()) projectiles = new[] { new ItemSpriteManager.ProjectileInfo { Id = 0, MinDamage = source.MinDamage, MaxDamage = source.MaxDamage,
            RateOfFire = source.RateOfFire, NumProjectiles = source.NumProjectiles, ArmorPiercing = source.ArmorPiercing } };
        foreach (var p in projectiles)
        {
            int min = p.MinDamage, max = p.MaxDamage;
            EnchantmentParser.ApplyProjectileDamageModifiers(code, p.Id, ref min, ref max);
            min = Math.Max(0, (int)((float)min * (float)rangeMultiplier)); max = Math.Max(min, (int)((float)max * (float)rangeMultiplier));
            result.Projectiles.Add(new ItemSpriteManager.ProjectileInfo { Id = p.Id, MinDamage = min, MaxDamage = max,
                RateOfFire = (float)(p.RateOfFire * rateMultiplier), NumProjectiles = p.NumProjectiles, ArmorPiercing = p.ArmorPiercing });
        }
        if (result.Projectiles.Count > 0) { result.MinDamage = result.Projectiles[0].MinDamage; result.MaxDamage = result.Projectiles[0].MaxDamage; }
        return result;
    }

    public static double ProcRate(double probability, double cooldown, double shotsPerSecond)
    {
        double p = Math.Max(0, Math.Min(1, probability));
        if (p == 0 || shotsPerSecond <= 0) return 0;
        if (cooldown <= 0) return shotsPerSecond * p;
        return shotsPerSecond / (Math.Max(1, Math.Ceiling(cooldown * shotsPerSecond)) + (1 - p) / p);
    }

    public static double WeaponDpsWithConditions(ItemSpriteManager.ItemMeta item, double att, double dex, double defense, double bonus,
        bool damaging, bool berserk, bool cursed, bool broken, bool weak, bool dazed, bool armored, bool exposed)
    {
        if (item == null) return 0;
        double rate = (1.5 + 6.5 * (dazed ? 0 : dex) / 75) * (berserk && !dazed ? 1.25 : 1);
        double multiplier = weak ? .5 : (.5 + att / 50) * (1 + bonus / 100) * (damaging ? 1.25 : 1);
        IEnumerable<ItemSpriteManager.ProjectileInfo> bullets = item.Projectiles;
        if (!bullets.Any()) bullets = new[] { new ItemSpriteManager.ProjectileInfo { MinDamage = item.MinDamage, MaxDamage = item.MaxDamage, RateOfFire = item.RateOfFire, NumProjectiles = item.NumProjectiles, ArmorPiercing = item.ArmorPiercing } };
        double damage = 0;
        foreach (var p in bullets)
        {
            int def = broken || p.ArmorPiercing ? 0 : armored ? (int)(defense * 1.5) : (int)defense;
            if (exposed) def -= 20;
            int min = Math.Max(0, p.MinDamage), max = Math.Max(min + 1, p.MaxDamage); double sum = 0;
            for (int roll = min; roll < max; roll++)
            {
                int raw = (int)((float)roll * (float)multiplier);
                // A zero-damage projectile remains zero even against Exposed.
                int hit = raw == 0 ? 0 : Math.Max(raw * 2 / 20, raw - def);
                sum += cursed ? (int)(hit * 1.25) : hit;
            }
            damage += sum / (max - min) * Math.Max(1, p.NumProjectiles) * p.RateOfFire * rate;
        }
        return damage;
    }

    public static double Poison(XElement e, int[] stats, bool delivered)
    {
        double delta = Delta(e, stats), impact = Positive(Attr(e, "impactDamage") + Attr(e, "statModImpactDamage") * delta / 10);
        double total = Positive(Attr(e, "totalDamage") + Attr(e, "statModDamage") * delta / 10);
        double dot = Math.Max(0, total - impact), duration = Attr(e, "duration");
        if (delivered && duration > 0) dot = Positive(dot * .2 * Math.Max(0, Math.Floor(duration / .2 - .0001)) / duration);
        return impact + dot;
    }

    public static List<HudCalculatorLine> WeaponProcs(HudCalculatorItem item, int[] stats, double shotsPerSecond)
    {
        var rows = new List<HudCalculatorLine>(); if (item == null) return rows;
        foreach (var e in item.Procs)
        {
            bool conditional = new[] { "requiredConditions", "requiredCondition", "mustWear", "mustNotWear" }.Any(a => e.Attribute(a) != null);
            if (e.Value.Trim() != "PoisonGrenade") continue;
            double damage = Poison(e, stats, true), rate = ProcRate(Attr(e, "proc", 1), Attr(e, "cooldown"), shotsPerSecond);
            rows.Add(new HudCalculatorLine { Name = conditional ? "Conditional poison proc" : "Poison proc", Damage = damage * rate, Conditional = conditional,
                Detail = damage.ToString("N0") + " delivered / activation; " + rate.ToString("0.###") + "/s; ignores DEF; expiry tick excluded" + (conditional ? "; required equipment/condition not assumed" : "") });
        }
        var bleed = item.Projectiles.Values.SelectMany(p => p.Elements("ConditionEffect")).Where(x => x.Value.Trim() == "Bleeding")
            .OrderByDescending(e => Attr(e, "bleedDamage", Attr(e, "amount", 20)) * Attr(e, "duration")).FirstOrDefault();
        if (bleed != null)
        {
            double damage = Attr(bleed, "bleedDamage", Attr(bleed, "amount", 20)), duration = Attr(bleed, "duration");
            if (damage > 0 && duration > 0) rows.Add(new HudCalculatorLine { Name = "Bleeding stacks", Damage = damage * shotsPerSecond * duration,
                Detail = damage.ToString("0.#") + "/s per stack; " + duration.ToString("0.#") + "s; strongest bleeding type, one new stack per volley; target HP cap not applied" });
        }
        return rows;
    }

    public static double CastRate(double desired, double cooldown, double cost, double maxMp, double regen)
    {
        if (desired <= 0 || cost > maxMp) return 0;
        double rate = cooldown > 0 ? Math.Min(desired, 1 / cooldown) : desired;
        return cost > 0 ? Math.Max(0, Math.Min(rate, regen / cost)) : Math.Max(0, rate);
    }

    public static HudCalculatorAbility Ability(HudCalculatorItem item, HudCalculatorMode mode, int[] stats, int hexStacks, string code, double targetDefense)
    {
        var result = new HudCalculatorAbility(); if (item == null || mode == null) return result;
        hexStacks = Math.Max(0, Math.Min(100, hexStacks));
        double scale = EnchantmentParser.GetStatDamageMultiplier(code);
        var effects = mode.Effects.Concat(mode.HexEffects.Where(e => hexStacks >= Attr(e, "minHex") && hexStacks <= Attr(e, "maxHex", 100))).ToList();
        foreach (var e in effects)
        {
            string type = e.Value.Trim(); double delta = Delta(e, stats, scale); string detail = ""; double damage = 0, multi = 0; bool conditional = false;
            switch (type)
            {
                case "Shoot": case "BulletNova": case "BulletCreate": case "ShurikenAbility":
                {
                    if (type == "ShurikenAbility" && e.Attribute("effect") != null)
                    { detail = (string)e.Attribute("effect") + "; hold-state condition, no extra projectile burst"; break; }
                    var owner = e.Attribute("type") == null ? item : Item(Id((string)e.Attribute("type")));
                    int projectileId = (int)Attr(e, "projectileId");
                    XElement p = owner != null && owner.Projectiles.TryGetValue(projectileId, out var found) ? found : owner?.Projectiles.Values.FirstOrDefault();
                    if (p == null) { result.Notes.Add(type + ": projectile definition unavailable."); continue; }
                    double baseMin = Value(p.Element("MinDamage"), Value(p.Element("Damage")));
                    int min = Positive(baseMin + Attr(e, "statModDamage") * delta);
                    int max = Math.Max(min, Positive(Value(p.Element("MaxDamage"), Value(p.Element("Damage"), baseMin)) + Attr(e, "statModDamage") * delta));
                    EnchantmentParser.ApplyProjectileDamageModifiers(code, projectileId, ref min, ref max);
                    int shots = Math.Max(0, (int)Attr(e, "numShots", item.NumProjectiles) + (int)Math.Floor(Attr(e, "statModNumShots") * delta));
                    // Reference display potential uses the rounded range midpoint, not a sampled combat roll.
                    damage = (type == "ShurikenAbility" ? (min + max) / 2 : (min + max + 1) / 2) * shots;
                    detail = min + "–" + max + " × " + shots + " shots; all hit" + (p.Element("ArmorPiercing") != null ? "; armor piercing" : "") + Scaling(e);
                    if (type == "ShurikenAbility") detail += "; release burst only; held MP drain excluded";
                    break;
                }
                case "PoisonGrenade":
                    damage = Poison(e, stats, false);
                    detail = Positive(Attr(e, "impactDamage") + Attr(e, "statModImpactDamage") * Delta(e, stats) / 10).ToString("N0") + " impact; total includes full DoT over " + Attr(e, "duration").ToString("0.##") + "s; ignores DEF" + Scaling(e);
                    result.Notes.Add("Ability poison totals are nominal full-duration potential; reapplication, expiry ticks and target invulnerability affect delivered damage.");
                    break;
                case "Lightning":
                {
                    double wisDelta = Math.Max(0, Stat(stats, "WIS") - Attr(e, "wisMin", Attr(e, "statModScalingMin")));
                    int chain = Positive(Attr(e, "totalDamage")) + Math.Max(0, (int)Math.Floor(Attr(e, "wisDamageBase") / 10 * wisDelta));
                    int targets = Math.Max(0, (int)Attr(e, "maxTargets", 1) + (Attr(e, "wisPerTarget") > 0 ? (int)Math.Floor(wisDelta / Attr(e, "wisPerTarget")) : 0));
                    int shockCount = Math.Max(0, (int)Attr(e, "aoeActivationCount"));
                    int shock = Attr(e, "aoeDamage") > 0 ? Math.Max(0, (int)Math.Floor(Attr(e, "aoeDamage") + Attr(e, "statModDamage") / 10 * Delta(e, stats, scale, "WIS"))) : 0;
                    int shockTargets = Math.Max(0, (int)Attr(e, "aoeMaxTargets") + (int)Math.Floor(wisDelta * Attr(e, "statModPerTarget") / 100));
                    damage = (targets > 0 ? chain : 0) + shock * shockCount;
                    for (int i = 0; i < targets; i++) multi += Math.Max(0, chain - Math.Max(0, (int)Attr(e, "decrDamage")) * i);
                    multi += shock * shockCount * shockTargets;
                    detail = chain + " first chain hit; " + targets + " chain targets; " + shock + " × " + shockCount + " shockblast triggers; " + shockTargets + " shockblast targets";
                    break;
                }
                case "Trap":
                {
                    int trap = Positive(Attr(e, "totalDamage") + Attr(e, "statModTrapDamage") * delta);
                    int bomb = Positive(Attr(e, "bombDamage") + Attr(e, "statModBombDamage") * delta);
                    damage = trap + bomb; detail = trap + " trap + " + bomb + " follow-up bomb" + Scaling(e);
                    if (Attr(e, "bombCount") > 1 || Attr(e, "bombTriggerCount") > 1) result.Notes.Add("Trap readout follows reference: one triggered trap plus one bomb; additional bomb targeting/overlap is not assumed.");
                    break;
                }
                case "VampireBlast":
                    damage = Positive(Attr(e, "totalDamage") + Attr(e, "wisDamageBase") * Math.Max(0, Stat(stats, "WIS") - Attr(e, "wisMin")));
                    detail = "Blast; ignores " + Attr(e, "ignoreDef").ToString("0.#") + " DEF; " + Attr(e, "heal").ToString("0.#") + " base healing; WIS scaling";
                    break;
                case "DamageNova":
                {
                    int hit = Positive((Attr(e, "minDamage") + Attr(e, "maxDamage")) / 2 + Attr(e, "statModDamage") * delta);
                    int waves = Math.Max(0, (int)(Attr(e, "activationCount", 1) + Math.Floor(Attr(e, "statModActivationCount") * delta)));
                    damage = hit * waves; detail = hit + " × " + waves + " waves; radius " + (Attr(e, "radius") + Attr(e, "statModRadius") * delta).ToString("0.##") + Scaling(e); break;
                }
                case "DetonateHex":
                    damage = Positive((Attr(e, "baseDamage") + Attr(e, "statModBaseDamage") * delta + (Attr(e, "stackDamage") + Attr(e, "statModStackDamage") * delta) * hexStacks) * (1 + Attr(e, "stackMultiplier") * hexStacks));
                    detail = hexStacks + " consumed stacks; target must carry those stacks before each use" + Scaling(e); break;
                case "Heal": case "Magic":
                    detail = Positive(Attr(e, "amount") + Attr(e, "statModAmount") * delta) + (type == "Heal" ? " HP restored" : " MP restored") + Scaling(e); break;
                case "StatBoostSelf": case "StatBoostAura":
                    detail = (Attr(e, "amount") + Attr(e, "statModAmount") * delta).ToString("+0.##;-0.##;0") + " " + (string)e.Attribute("stat") + " for " + (Attr(e, "duration") + Attr(e, "statModDuration") * delta).ToString("0.##") + "s; apply in stat inputs when active"; break;
                case "ConditionEffectSelf": case "ConditionEffectAura": case "ApplyCondition":
                    detail = (string)e.Attribute("effect") + " for " + (Attr(e, "duration") + Attr(e, "statModDuration") * delta).ToString("0.##") + "s; set matching condition explicitly"; break;
                case "EffectBlast":
                    detail = (string)e.Attribute("condEffect") + " for " + (Attr(e, "condDuration") + (Attr(e, "wisPerDuration") > 0 ? Math.Max(0, Stat(stats, "WIS") - Attr(e, "wisMin")) / Attr(e, "wisPerDuration") : 0)).ToString("0.##") + "s; radius " + Attr(e, "radius").ToString("0.##"); break;
                case "Decoy": case "Sneak":
                    detail = Attr(e, "duration").ToString("0.##") + "s; situational behavior"; break;
                case "Teleport":
                    detail = Attr(e, "maxDistance").ToString("0.##") + " tiles"; break;
                case "Dash":
                    damage = Attr(e, "damage"); detail = "One dash-trail hit; repeated trail contact not assumed"; break;
                case "SpawnCreep": case "RaiseDead":
                {
                    if ((string)e.Attribute("ignoreOnTooltip") == "true" || (string)e.Attribute("ignoreOnTooltip") == "1") continue;
                    string name = (string)e.Attribute(type == "RaiseDead" ? "undeadId" : "objectId"); var summon = Named(name);
                    if (summon == null) { result.Notes.Add(type + ": summon definition unavailable (" + name + ")."); continue; }
                    bool visible = summon.Projectiles.Values.Any(p => p.Element("DisplayId") != null);
                    foreach (var p in summon.Projectiles.Values.Where(p => (string)p.Attribute("ignoreOnTooltip") != "true" && (!visible || p.Element("DisplayId") != null)))
                    {
                        int min = Positive(Value(p.Element("MinDamage"), Value(p.Element("Damage"))) + Math.Floor(Attr(e, "statModDamage") * delta));
                        int max = Positive(Value(p.Element("MaxDamage"), Value(p.Element("Damage"))) + Math.Floor(Attr(e, "statModDamage") * delta));
                        if (min <= 0 && max <= 0) continue;
                        result.Lines.Add(new HudCalculatorLine { Name = summon.Name, Conditional = true,
                            Detail = min + "–" + max + " per summon projectile" + (p.Element("ArmorPiercing") != null ? "; armor piercing" : "") + Scaling(e) });
                    }
                    result.Notes.Add("Summon projectile damage is shown separately; sustained DPS depends on firing behavior, active summons and uptime."); continue;
                }
                case "GenericActivate": case "ObjectToss": case "ChannelDash": continue;
                default: result.Notes.Add("Unmodeled ability effect: " + type + "."); continue;
            }
            if (multi == 0) multi = damage;
            result.Lines.Add(new HudCalculatorLine { Name = FriendlyEffect(type), Damage = damage, MultiTargetDamage = multi, Detail = detail, Conditional = conditional });
        }
        foreach (var e in item.ConditionalEffects.Concat(mode.ConditionalEffects).Distinct().Where(x => x.Value.Trim() == "LethalStrike"))
        {
            double delta = Delta(e, stats, scale);
            double bonus = Math.Max(0, Attr(e, "ignoreFlat") + Attr(e, "statModFlat") * delta + (Attr(e, "ignorePerc") + Attr(e, "statModPerc") * delta) * Math.Max(0, targetDefense));
            result.Lines.Add(new HudCalculatorLine { Name = "Lethal Strike", Conditional = true,
                Detail = ((int)bonus).ToString("N0") + " conditional bonus per hit vs " + targetDefense.ToString("N0") + " DEF; " + Attr(e, "duration").ToString("0.##") + "s after leaving Sneak; uptime and extra shots excluded" });
        }
        if (item.ConditionalEffects.Concat(mode.ConditionalEffects).Any(e => e.Value.Trim() != "LethalStrike" && e.Value.Trim() != "ApplyCondition")) result.Notes.Add("Conditional/on-switch effects are not added to per-use damage.");
        return result;
    }

    private static string FriendlyEffect(string type)
    {
        switch (type)
        {
            case "Shoot": case "BulletCreate": return "Projectile burst";
            case "BulletNova": return "Projectile nova";
            case "ShurikenAbility": return "Shuriken";
            case "PoisonGrenade": return "Poison";
            case "VampireBlast": return "Skull blast";
            case "DamageNova": return "Damage nova";
            case "DetonateHex": return "Hex detonation";
            case "StatBoostSelf": case "StatBoostAura": return "Stat boost";
            case "ConditionEffectSelf": case "ConditionEffectAura": case "ApplyCondition": return "Condition";
            case "EffectBlast": return "Condition blast";
            default: return type;
        }
    }

    public static int CombatTrigger(int defense)
    {
        double d = Math.Max(0, defense);
        return (int)(Math.Min(d, 15) + Math.Max(0, Math.Min(d, 35) - 15) * .75 + Math.Max(0, Math.Min(d, 65) - 35) * .5 + Math.Max(0, Math.Min(d, 125) - 65) * .25);
    }

    // The same published anchor interpolation used by the reference; intermediate levels are estimates.
    public static double PetThroughput(int ability, int level)
    {
        if (level <= 0) return 0;
        double[] amounts;
        switch (ability)
        {
            case 407: amounts = new double[] { 10, 14, 23, 40, 69, 90 }; break;
            case 408: amounts = new double[] { 1, 3, 8, 17, 33, 45 }; break;
            case 402: amounts = new double[] { 7, 17, 39, 80, 130, 200 }; break;
            case 404: amounts = new double[] { 5, 12, 25, 60, 121, 150 }; break;
            case 405: amounts = new double[] { 3, 8, 19, 40, 76, 100 }; break;
            case 406: return Interpolate(level, new[] { 1, 30, 50, 70, 100 }, new double[] { 5, 20, 54, 117, 200 }) / Interpolate(level, PetLevels, AttackDelay);
            default: return 0;
        }
        return Interpolate(level, PetLevels, amounts) / Interpolate(level, PetLevels, ability == 407 || ability == 408 ? HealDelay : AttackDelay);
    }
    private static readonly int[] PetLevels = { 1, 30, 50, 70, 90, 100 };
    private static readonly double[] HealDelay = { 10, 3.82, 2.66, 1.96, 1.42, 1 };
    private static readonly double[] AttackDelay = { 5, 2, 1, .6, .4, .2 };
    private static double Interpolate(int level, int[] xs, double[] ys)
    {
        if (level <= xs[0]) return ys[0];
        for (int i = 1; i < xs.Length; i++) if (level <= xs[i]) return ys[i - 1] + (ys[i] - ys[i - 1]) * (level - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return ys[ys.Length - 1];
    }
}
