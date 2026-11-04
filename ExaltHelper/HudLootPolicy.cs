using System;
using System.Collections.Generic;
using System.Linq;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal sealed class HudLootPreferences
{
    public List<string> AlwaysTrackBags { get; set; } = new List<string> { "White Bag", "Orange Bag", "Red Bag", "Gold Bag", "Blue Bag", "Cyan Bag", "Egg Basket" };
    public bool Shiny { get; set; } = true;
    public bool AnyEnchantment { get; set; } = true;
    public bool LootEnchantments { get; set; } = true;
    public bool UniqueEnchantments { get; set; } = true;
    public bool AwakenedEnchantments { get; set; } = true;
    public bool LegendaryPlus { get; set; } = true;
    public bool Untiered { get; set; } = true;
    public bool SetTier { get; set; } = true;
    public bool Potions { get; set; } = true;
    public bool Marks { get; set; } = true;
    public bool Tiered { get; set; } = true;
    public int MinimumTier { get; set; } = 10;
}

internal static class HudLootPolicy
{
    public static readonly string[] BagNames = { "Brown Bag", "Soulbound Bag", "Pink Bag", "Purple Bag", "Cyan Bag", "Blue Bag", "Gold Bag", "Orange Bag", "White Bag", "Red Bag", "Egg Basket" };
    // A selected bag type OR any matching content admits the entire bag, keeping
    // its other slots intact. This mirrors the reference's two filter sections.
    public static bool ShouldTrack(LootBagDrop bag) => Matches(bag, HudPreferences.Current.Loot);
    public static bool ShowInFeed(HudLogEntry row)
    {
        if (row.FeedLoot == null) return true;
        return Matches(row.FeedLoot.ToBag(), HudPreferences.Current.FeedLoot);
    }
    public static bool Matches(LootBagDrop bag, HudLootPreferences settings)
    {
        if (bag == null || bag.ItemIds.Count == 0) return false;
        if (settings.AlwaysTrackBags.Contains(bag.BagType)) return true;
        for (int i = 0; i < bag.ItemIds.Count; i++)
        {
            var item = ItemSpriteManager.GetItemMetadata(bag.ItemIds[i]);
            var labels = item.Labels;
            int enchants = bag.ItemEnchantCounts.ElementAtOrDefault(i);
            if (settings.Shiny && (item.IsShiny || bag.ItemIsShiny.ElementAtOrDefault(i)) || settings.AnyEnchantment && enchants > 0 || settings.LegendaryPlus && enchants >= 3) return true;
            if (settings.Untiered && labels.Contains("UT") && !labels.Contains("CONSUMABLE")) return true;
            if (settings.SetTier && labels.Contains("ST")) return true;
            if (settings.Potions && (labels.Contains("STATPOTION") || labels.Contains("GREATERSTATPOTION"))) return true;
            if (settings.Marks && (labels.Contains("MARK") || (item.Name ?? "").StartsWith("Mark of ", StringComparison.Ordinal))) return true;
            if (settings.Tiered && labels.Contains("TIERED") && labels.Any(x => x.Length > 1 && x[0] == 'T' && int.TryParse(x.Substring(1), out int tier) && tier >= settings.MinimumTier)) return true;
            foreach (string name in bag.ItemEnchants.ElementAtOrDefault(i) ?? new List<string>())
            {
                string category = EnchantmentParser.EnchantmentCategory(name);
                if (settings.LootEnchantments && category == "Loot" || settings.UniqueEnchantments && category == "Unique" || settings.AwakenedEnchantments && category == "Awakened") return true;
            }
        }
        return false;
    }
}
