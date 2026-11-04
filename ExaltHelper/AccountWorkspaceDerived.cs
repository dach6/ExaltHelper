using System;
using System.Collections.Generic;
using System.Linq;

namespace ExaltHelper;

internal static class AccountWorkspaceDerived
{
    internal sealed class Line { public string Name, Value, Detail; }
    internal static int RemainingBoost(AccountCharacter character, int? seconds, bool live)
        => RemainingBoostAt(character, seconds, live, character.BoostObservedUtc);
    internal static int RemainingBoostAt(AccountCharacter character, int? seconds, bool live, DateTime? observed)
    {
        if (!seconds.HasValue) return -1;
        DateTime? anchor = observed ?? character.BoostObservedUtc;
        double elapsed = live && anchor.HasValue ? Math.Max(0, (DateTime.UtcNow - anchor.Value).TotalSeconds) : 0;
        return (int)Math.Max(0, seconds.Value - elapsed);
    }
    internal static List<Line> Build(AccountCharacter c, AccountSnapshot account, int targetDefense)
    {
        var lines = new List<Line>();
        void Add(string name, string value, string detail = "") => lines.Add(new Line { Name = name, Value = value, Detail = detail });
        bool live = !c.IsDead && c.Id == account.CurrentCharacterId && c.ObservedUtc.HasValue && DateTime.UtcNow - c.ObservedUtc.Value < TimeSpan.FromSeconds(10);
        var proficiency = HudCalculatorEngine.Proficiency(account, c.ClassType);
        double[] exalt = proficiency.StatBonuses;
        double mastery = proficiency.MasteryPercent;
        var crucible = c.Crucible ? account.Crucible : null;
        var ritual = account.BloodRituals.FirstOrDefault(r => r.Id == c.BloodRitualId && !string.IsNullOrEmpty(c.BloodRitualId));
        double[] challenge = new double[8];
        for (int i = 0; i < 8; i++)
        {
            if (crucible != null) challenge[i] += crucible.FlatStats[i] + HudCalculatorEngine.Round(c.Stats[i] * (crucible.StatMultipliers[i] - 1));
            if (ritual != null) challenge[i] += ritual.FlatStats[i] + HudCalculatorEngine.Round(c.Stats[i] * (ritual.StatMultipliers[i] - 1));
        }
        var stats = HudCalculatorEngine.Stats(c.Stats.Select(n => (double)n).ToArray(), c.Inventory, c.Enchants, exalt, challenge);
        var modeled = (int[])stats.Effective.Clone();
        if (live && c.ObservedStats.Length == 8)
        {
            var adjustment = challenge.Select((n, i) => n + c.ObservedStats[i] - modeled[i]).ToArray();
            stats = HudCalculatorEngine.Stats(c.Stats.Select(n => (double)n).ToArray(), c.Inventory, c.Enchants, exalt, adjustment);
        }
        Add("Snapshot", live ? "Live effective stats" : c.IsDead ? "Final cached loadout" : "Cached loadout estimate",
            live ? "Observed totals include active server buffs and debuffs." : "Equipment, enchants, exaltations and observed seasonal modifiers. Login updates temporary boosts.");
        for (int i = 0; i < 8; i++) Add(HudCalculatorEngine.StatNames[i], stats.Effective[i].ToString(), "Base " + c.Stats[i] + "; gear + enchants " + stats.Gear[i].ToString("+0.##;-0.##;0")
            + "; exalt " + exalt[i] + "; seasonal modifiers " + challenge[i].ToString("+0.##;-0.##;0") + (live ? "; live adjustment " + (stats.Effective[i] - modeled[i]) : ""));
        bool Effect(int bit) => live && (c.Effects & 1 << bit) != 0;
        bool inCombat = Effect(20); double petHp = 0, petMp = 0;
        var pet = account.Pets.FirstOrDefault(p => p.Id == c.PetId && p.Seasonal == c.Seasonal);
        if (pet != null)
        {
            int unlocked = pet.Rarity == 0 ? 1 : pet.Rarity <= 2 ? 2 : 3;
            for (int i = 0; i < Math.Min(unlocked, pet.AbilityTypes.Length); i++)
            {
                int type = pet.AbilityTypes[i], level = i < pet.AbilityLevels.Length && pet.AbilityLevels[i] > 0 ? pet.AbilityLevels[i] : i < pet.AbilityPowers.Length ? pet.AbilityPowers[i] : 0;
                double amount = HudCalculatorEngine.PetThroughput(type, level);
                if (type == 407) petHp += amount; else if (type == 408) petMp += amount;
                Add("Pet " + (type == 407 ? "Heal" : type == 408 ? "Magic Heal" : "ability " + type), amount.ToString("0.##") + " / s", "Level / power " + level + "; unlocked ability. Out of combat, full uptime.");
            }
        }
        double health = (stats.HpStatRegen + petHp) * (inCombat ? .5 : 1) + stats.HpGearRegen;
        double mana = (stats.MpStatRegen + petMp) * (inCombat ? .5 : 1) + stats.MpGearRegen;
        Add("HP regeneration", health.ToString("0.##") + " HP/s", (inCombat ? "In combat: stat and pet regen halved." : "Out of combat.") + " Gear regen remains active.");
        Add("MP regeneration", mana.ToString("0.##") + " MP/s", "WIS + enchantments + unlocked pet Magic Heal.");
        Add("Combat trigger", HudCalculatorEngine.CombatTrigger(stats.Effective[3]).ToString() + " damage", "Minimum post-defense damage from a hit to enter combat.");
        Add("Armor proficiency", "-" + proficiency.ArmorCombatReduction.ToString("0.#") + " s", "In-combat duration reduction; weakest exalt level across the armor group.");
        var weaponItem = c.Inventory.Length > 0 ? HudCalculatorData.Item(c.Inventory[0]) : null;
        if (weaponItem != null && weaponItem.IsWeapon)
        {
            string code = c.Enchants.Length > 0 ? c.Enchants[0] : "";
            double rangeMultiplier = (1 + mastery / 100) * (crucible?.DamageMultiplier ?? 1) * (ritual?.DamageMultiplier ?? 1);
            var weapon = HudCalculatorEngine.Weapon(weaponItem, code, rangeMultiplier, stats.RateMultiplier);
            double exaltPercent = live && c.ExaltDamage.GetValueOrDefault() > 0 ? (c.ExaltDamage.Value / 1000d - 1) * 100 : 0;
            double dps = HudCalculatorEngine.WeaponDpsWithConditions(weapon, stats.Effective[2], stats.Effective[5], targetDefense, exaltPercent, Effect(18), Effect(19), false, false, Effect(2), Effect(5), false, false);
            double volleys = (1.5 + 6.5 * (Effect(5) ? 0 : stats.Effective[5]) / 75) * weapon.RateOfFire * (Effect(19) ? 1.25 : 1);
            Add("Weapon DPS", dps.ToString("N1"), weapon.Name + "; target DEF " + targetDefense + "; all projectiles hit. Conditional procs listed separately.");
            Add("Attack rate", volleys.ToString("0.###") + " volleys/s", "DEX, weapon fire rate, enchantments and observed Berserk/Dazed.");
            foreach (var proc in HudCalculatorEngine.WeaponProcs(weaponItem, stats.Effective, volleys)) Add(proc.Name, proc.Damage.ToString("N1") + " DPS", proc.Detail + (proc.Conditional ? " (conditional)" : ""));
        }
        var ability = c.Inventory.Length > 1 ? HudCalculatorData.Item(c.Inventory[1]) : null;
        if (ability != null) foreach (var mode in ability.Modes)
        {
            string code = c.Enchants.Length > 1 ? c.Enchants[1] : "";
            var result = HudCalculatorEngine.Ability(ability, mode, stats.Effective, 0, code, targetDefense);
            double cost = mode.MpCost * stats.MpMultiplier;
            Add(ability.Name + " / " + mode.Name, cost.ToString("0.#") + " MP", "Cooldown " + mode.Cooldown.ToString("0.##") + "s; sustained rate " + HudCalculatorEngine.CastRate(20, mode.Cooldown, cost, stats.Effective[1], mana).ToString("0.###") + "/s with current regen.");
            foreach (var effect in result.Lines) Add(effect.Name, effect.Damage.ToString("N0") + " / use", effect.Detail + (effect.Conditional ? " (conditional)" : ""));
            foreach (string note in result.Notes.Distinct()) Add("Ability note", "", note);
        }
        int lootSeconds = RemainingBoostAt(c, c.LootBoostSeconds, live, c.LootBoostObservedUtc);
        Add("Loot boost", "+" + stats.LootBonus.ToString("0.#") + "% gear / +" + proficiency.LootPercent.ToString("0.#") + "% proficiency",
            "Clover: " + (lootSeconds < 0 ? "unknown" : lootSeconds == 0 ? "inactive" : "+50%, " + TimeSpan.FromSeconds(lootSeconds).ToString("g") + " remaining") + "; Crucible " + (crucible?.LootPercent ?? 0).ToString("+0.#;-0.#;0") + "%; ritual " + (ritual?.LootPercent ?? 0).ToString("+0.#;-0.#;0") + "%. Components shown separately; game stacking may vary.");
        double gearXp = stats.XpBonus;
        Add("XP boost", "+" + (gearXp + proficiency.XpPercent + (crucible?.XpPercent ?? 0) + (ritual?.XpPercent ?? 0)).ToString("0.#") + "%",
            "Equipment " + gearXp.ToString("0.#") + "%; Fast Learner " + proficiency.XpPercent + "%; Crucible " + (crucible?.XpPercent ?? 0).ToString("0.#") + "%; ritual " + (ritual?.XpPercent ?? 0).ToString("0.#") + "%. XP potion: " + BoostText(c, c.XpBoostSeconds, live, c.XpBoostObservedUtc));
        var dustPotion = account.Accelerators.Where(a => a.ItemType == 751 && a.ExpiresUtc > DateTime.UtcNow).OrderByDescending(a => a.ExpiresUtc).FirstOrDefault();
        Add("Dust boost", "+" + (stats.DustBonus + (dustPotion == null ? 0 : 60)).ToString("0.#") + "%",
            "Equipped enchantments " + stats.DustBonus.ToString("0.#") + "%. Account potion: " + (dustPotion == null ? "inactive" : "+60%, " + (dustPotion.ExpiresUtc - DateTime.UtcNow).ToString("g") + " remaining" + (dustPotion.Estimated ? " (estimated until refresh)" : "")));
        Add("Loot-tier potion", BoostText(c, c.LootTierBoostSeconds, live, c.LootTierBoostObservedUtc));
        Add("Mastery damage", "+" + mastery.ToString("0.#") + "%", "Lowest class exalt level, capped at four; applied to the weapon damage range.");
        if (crucible != null) { Add("Crucible", crucible.Title, "Damage x" + crucible.DamageMultiplier.ToString("0.##") + "; battle-pass XP " + crucible.BattlePassPercent.ToString("+0.#;-0.#;0") + "%"); foreach (string effect in crucible.Effects) Add("Crucible effect", "", effect); }
        else if (c.Crucible) Add("Crucible", "Active", "Connect to receive this season's modifier definitions.");
        if (ritual != null) Add("Blood Ritual", "Tier " + ritual.Title, "Base HP x" + ritual.StatMultipliers[0].ToString("0.##") + "; weapon damage x" + ritual.DamageMultiplier.ToString("0.##") + "; loot " + ritual.LootPercent.ToString("+0.#;-0.#;0") + "%");
        foreach (string note in stats.Notes.Distinct()) Add("Loadout note", "", note);
        return lines;
    }
    private static string BoostText(AccountCharacter c, int? seconds, bool live, DateTime? observed)
    { int remaining = RemainingBoostAt(c, seconds, live, observed); return remaining < 0 ? "Not observed" : remaining == 0 ? "Inactive" : TimeSpan.FromSeconds(remaining).ToString("g") + (live ? " remaining" : " last observed"); }
}
