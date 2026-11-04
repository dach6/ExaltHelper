using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal partial class ExaltDashboardForm
{
    private static void ItemColumn(HudTablePage page, string column, Func<object, IEnumerable<HudItemView>> items,
        bool showNames = false, bool singleItem = false)
    {
        var field = page.Grid.Columns[column];
        if (singleItem)
        {
            field.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            field.MinimumWidth = 84; field.Width = 112;
            if (page.Grid.Columns.Contains("Rarity"))
            {
                page.Grid.Columns["Rarity"].Visible = false;
                field.Width = 184; field.HeaderText = "ITEM / RARITY";
            }
        }
        else { field.MinimumWidth = 140; field.FillWeight = Math.Max(220, field.FillWeight); }
        page.Grid.RowTemplate.Height = Math.Max(44, page.Grid.RowTemplate.Height);
        foreach (DataGridViewRow row in page.Grid.Rows) row.Height = page.Grid.RowTemplate.Height;
        HudItemPresentation.Attach(page.Grid, column, items, showNames);
    }

    private static HudItemView LootItem(HudLootRecord item)
        => HudItemPresentation.Create(item.ItemId, item.Item, item.Rarity, item.Shiny, item.Enchantments,
            string.Join(" · ", new[] { item.Enemy, item.Dungeon, item.Bag }.Where(x => !string.IsNullOrWhiteSpace(x))),
            "Character: " + item.Character + "\nFound: " + item.Time.ToLocalTime().ToString("g"));

    private static IEnumerable<HudItemView> FeedItems(object value)
    {
        if (!(value is HudLogEntry row) || row.FeedLoot == null) return Array.Empty<HudItemView>();
        var loot = row.FeedLoot;
        return (loot.ItemIds ?? Array.Empty<int>()).Select((id, slot) => HudItemPresentation.Create(id,
            rarity: FeedItemRarity(loot, slot),
            shiny: (loot.Shiny ?? Array.Empty<bool>()).ElementAtOrDefault(slot),
            enchantments: string.Join(", ", (loot.Enchantments ?? Array.Empty<string[]>()).ElementAtOrDefault(slot) ?? Array.Empty<string>()),
            source: string.Join(" · ", new[] { row.Sender, row.Location, loot.BagType }.Where(x => !string.IsNullOrWhiteSpace(x))),
            details: "Found: " + row.Time.ToLocalTime().ToString("g")));
    }
    private static string FeedItemRarity(HudFeedLoot loot, int slot)
    {
        string recorded = (loot.Rarities ?? Array.Empty<string>()).ElementAtOrDefault(slot);
        if (!string.IsNullOrWhiteSpace(recorded)) return recorded;
        // Older cached feed entries retained the enchantment count, before the
        // observed rarity was stored. Match the game's existing enchant parser.
        int count = (loot.EnchantCounts ?? Array.Empty<int>()).ElementAtOrDefault(slot);
        return count >= 4 ? "Divine" : count == 3 ? "Legendary" : count == 2 ? "Rare" : count == 1 ? "Uncommon" : null;
    }

    private static string EquipmentTooltip(PlayerParseEntry player)
    {
        if (player == null) return "Equipment has not been observed.";
        return string.Join("\n", Enumerable.Range(0, Math.Min(4, player.EquipmentIds?.Length ?? 0)).Select(i =>
            player.EquipmentIds[i] + ": " + player.EquipmentNames.ElementAtOrDefault(i) + " · " + player.EquipmentRarities.ElementAtOrDefault(i)
            + " " + string.Join(", ", player.EquipmentEnchants.ElementAtOrDefault(i) ?? new List<string>())));
    }

    private static HudItemView EquipmentItem(PlayerParseEntry player, int slot)
    {
        string position = new[] { "Weapon", "Ability", "Armor", "Ring" }[slot];
        int id = (player.EquipmentIds ?? Array.Empty<int>()).ElementAtOrDefault(slot);
        return HudItemPresentation.Create(id, id > 0 ? player.EquipmentNames.ElementAtOrDefault(slot) : "Empty " + position.ToLowerInvariant() + " slot",
            id > 0 ? player.EquipmentRarities.ElementAtOrDefault(slot) : "Empty",
            enchantments: string.Join(", ", player.EquipmentEnchants.ElementAtOrDefault(slot) ?? new List<string>()),
            details: position + " · " + player.Name);
    }

    private IEnumerable<HudItemView> PlayerItems(object name)
    {
        var player = CurrentLiveSnapshot.Players.FirstOrDefault(x => x.Name == name as string);
        return player == null ? Array.Empty<HudItemView>() : Enumerable.Range(0, 4).Select(slot => EquipmentItem(player, slot));
    }

    private static IEnumerable<HudItemView> AbilityItems(object value)
    {
        if (!(value is AbilityLogEntry entry) || string.IsNullOrWhiteSpace(entry.AbilityItemName)) return Array.Empty<HudItemView>();
        return new[] { HudItemPresentation.Create(HudItemPresentation.ResolveItemId(entry.AbilityItemName), entry.AbilityItemName,
            details: entry.PlayerName + " · " + entry.AbilityType + "\n" + entry.Details) };
    }
}
