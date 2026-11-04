using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper;

internal static partial class AccountWorkspace
{
    private static readonly Lazy<Dictionary<int, HashSet<string>>> itemTags = new Lazy<Dictionary<int, HashSet<string>>>(() =>
    {
        var result = new Dictionary<int, HashSet<string>>();
        using (var stream = typeof(AccountWorkspace).Assembly.GetManifestResourceStream("ObjectID.list"))
        {
            if (stream == null) return result;
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split(';');
                    if (fields.Length > 6 && int.TryParse(fields[0], out int id)) result[id] = new HashSet<string>(fields[6].Split(','), StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        return result;
    });
    internal static bool ItemHasTag(int item, string tag) => itemTags.Value.TryGetValue(item, out var tags) && tags.Contains(tag);

    private static T[] Resize<T>(T[] source, int count)
    {
        if (source != null && source.Length == count) return source;
        var result = new T[count];
        if (source != null) Array.Copy(source, result, Math.Min(source.Length, count));
        return result;
    }

    private static string[] SplitEnchants(string value, int count) => Resize((value ?? "").Split(','), count);

    private sealed class SlotBinding
    {
        public int Item;
        public string Enchant;
        public int Count = 1;
        public Action<int, string, int> Set;
    }

    private static SlotBinding BindSlot(SlotObjectData slot)
    {
        if (slot == null || slot.SlotId < 0 || slot.ObjectId < 0) return null;
        int index = slot.SlotId;
        var character = slot.ObjectId == activeObjectId ? state.Characters.FirstOrDefault(c => c.Id == state.CurrentCharacterId) : null;
        if (character != null && index < character.Inventory.Length)
        {
            character.Enchants = Resize(character.Enchants, character.Inventory.Length);
            return new SlotBinding { Item = character.Inventory[index], Enchant = character.Enchants[index],
                Set = (id, enchant, count) => { character.Inventory[index] = id; character.Enchants[index] = enchant; character.UpdatedUtc = DateTime.UtcNow; } };
        }
        var storage = state.Storage.FirstOrDefault(s => s.ObjectId == slot.ObjectId && s.Seasonal == currentSeasonal);
        if (storage != null && index < storage.Items.Length)
        {
            storage.Enchants = Resize(storage.Enchants, storage.Items.Length);
            return new SlotBinding { Item = storage.Items[index], Enchant = storage.Enchants[index],
                Set = (id, enchant, count) => { storage.Items[index] = id; storage.Enchants[index] = enchant; storage.UpdatedUtc = DateTime.UtcNow; } };
        }
        if (petObjects.TryGetValue(slot.ObjectId, out int petId))
        {
            var pet = state.Pets.FirstOrDefault(p => p.Id == petId && p.Seasonal == currentSeasonal);
            if (pet != null && index < pet.Inventory.Length)
            {
                pet.Enchants = Resize(pet.Enchants, pet.Inventory.Length); pet.StackCounts = Resize(pet.StackCounts, pet.Inventory.Length);
                return new SlotBinding { Item = pet.Inventory[index], Enchant = pet.Enchants[index], Count = Math.Max(1, pet.StackCounts[index]),
                    Set = (id, enchant, count) => { pet.Inventory[index] = id; pet.Enchants[index] = enchant; pet.StackCounts[index] = id > 0 ? count : 0; pet.UpdatedUtc = DateTime.UtcNow; } };
            }
        }
        return null;
    }

    private static void ApplySwapLocked(InvSwapPacket packet)
    {
        if (!packet.Result || packet.Slot1 == null || packet.Slot2 == null) return;
        var from = BindSlot(packet.Slot1); var to = BindSlot(packet.Slot2);
        // Slot IDs in successful results are absolute indexes, including gift storage.
        // Only carry metadata when the cached type still matches the packet's old item type.
        string fromEnchant = from?.Item == packet.Slot1.ItemType ? from.Enchant : "";
        string toEnchant = to?.Item == packet.Slot2.ItemType ? to.Enchant : "";
        int fromCount = from?.Item == packet.Slot1.ItemType ? from.Count : 1;
        int toCount = to?.Item == packet.Slot2.ItemType ? to.Count : 1;
        from?.Set(packet.Slot2.ItemType, toEnchant, toCount);
        to?.Set(packet.Slot1.ItemType, fromEnchant, fromCount);
        if (from != null || to != null) TouchLocked();
    }

    // Older caches may lack newly added fields. Reject null collection entries and
    // restore the bounded array shapes that rendering and packet updates require.
    private static void Normalize(AccountSnapshot value)
    {
        value.Name = value.Name ?? "Account"; value.Status = value.Status ?? "Cached account data.";
        value.CurrentCharacterId = -1;
        value.Characters = (value.Characters ?? new List<AccountCharacter>()).Where(c => c != null).Take(10000).ToList();
        value.DeadCharacters = (value.DeadCharacters ?? new List<AccountCharacter>()).Where(c => c != null).Take(10000).ToList();
        value.DeadCharacters = value.DeadCharacters.Concat(value.Characters.Where(c => c.IsDead)).GroupBy(c => c.Id).Select(g => g.First()).ToList();
        value.Characters = value.Characters.Where(c => !c.IsDead).GroupBy(c => c.Id).Select(g => g.First()).ToList();
        value.TreasurySectionOrder = OrderedKeys(value.TreasurySectionOrder, TreasurySections);
        value.TreasuryCategoryOrder = OrderedKeys(value.TreasuryCategoryOrder, TreasuryCategories);
        value.CollapsedTreasurySections = (value.CollapsedTreasurySections ?? Array.Empty<string>()).Where(TreasurySections.Contains).Distinct().ToArray();
        value.BloodRituals = (value.BloodRituals ?? new List<AccountChallenge>()).Where(r => r != null).Take(4096).ToList();
        value.Accelerators = (value.Accelerators ?? new List<AccountAccelerator>()).Where(a => a != null).Take(1000).ToList();
        foreach (var challenge in value.BloodRituals.Concat(value.Crucible == null ? Array.Empty<AccountChallenge>() : new[] { value.Crucible }))
        {
            challenge.FlatStats = Resize(challenge.FlatStats, 8);
            if (challenge.StatMultipliers == null || challenge.StatMultipliers.Length != 8) challenge.StatMultipliers = Enumerable.Repeat(1d, 8).ToArray();
            challenge.Effects = challenge.Effects ?? Array.Empty<string>(); challenge.Id = challenge.Id ?? ""; challenge.Title = challenge.Title ?? "";
        }
        value.Storage = (value.Storage ?? new List<AccountStorage>()).Where(s => s != null).Take(20).ToList();
        value.Pets = (value.Pets ?? new List<AccountPet>()).Where(p => p != null).Take(10000).ToList();
        foreach (var c in value.Characters.Concat(value.DeadCharacters))
        {
            c.Name = c.Name ?? ""; c.Created = c.Created ?? ""; c.Stats = Resize(c.Stats, 8);
            c.Label = c.Label ?? ""; if (c.Label.Length > 80) c.Label = c.Label.Substring(0, 80);
            c.KilledBy = c.KilledBy ?? ""; c.ObservedStats = c.ObservedStats == null || c.ObservedStats.Length != 8 ? Array.Empty<int>() : c.ObservedStats;
            c.BloodRitualId = c.BloodRitualId ?? "";
            c.DeathBonuses = (c.DeathBonuses ?? new List<AccountDeathBonus>()).Where(b => b != null).Take(1000).ToList();
            c.Inventory = (c.Inventory ?? Array.Empty<int>()).Take(28).ToArray(); c.Enchants = Resize(c.Enchants, c.Inventory.Length);
            c.QuickSlots = c.QuickSlots == null ? new[] { -1, -1, -1 } : Resize(c.QuickSlots, 3);
            c.QuickSlotCounts = Resize(c.QuickSlotCounts, 3); c.QuickSlotEnchants = Resize(c.QuickSlotEnchants, 3);
            c.PcStats = c.PcStats ?? new Dictionary<string, int>();
        }
        foreach (var dead in value.DeadCharacters) dead.IsDead = true;
        var deadIds = new HashSet<int>(value.DeadCharacters.Select(c => c.Id));
        value.Characters.RemoveAll(c => deadIds.Contains(c.Id));
        foreach (var s in value.Storage)
        {
            s.ObjectId = -1; s.Name = s.Name ?? "Vault";
            s.Items = (s.Items ?? Array.Empty<int>()).Take(100000).ToArray(); s.Enchants = Resize(s.Enchants, s.Items.Length);
        }
        foreach (var p in value.Pets)
        {
            p.Name = p.Name ?? "Pet"; p.Inventory = (p.Inventory ?? Array.Empty<int>()).Take(1000).ToArray();
            p.StackCounts = Resize(p.StackCounts, p.Inventory.Length); p.Enchants = Resize(p.Enchants, p.Inventory.Length);
            p.AbilityTypes = Resize(p.AbilityTypes, 3); p.AbilityLevels = Resize(p.AbilityLevels, 3); p.AbilityPowers = Resize(p.AbilityPowers, 3);
        }
        value.Exaltations = (value.Exaltations ?? new Dictionary<string, int[]>()).ToDictionary(p => p.Key, p => Resize(p.Value, 8));
        value.RegularMaterials = Resize(value.RegularMaterials, 4); value.SeasonalMaterials = Resize(value.SeasonalMaterials, 4);
        value.RegularMaterialCaps = Resize(value.RegularMaterialCaps, 4); value.SeasonalMaterialCaps = Resize(value.SeasonalMaterialCaps, 4);
        value.RegularDustAmounts = Resize(value.RegularDustAmounts, 3); value.SeasonalDustAmounts = Resize(value.SeasonalDustAmounts, 3);
        value.RegularDustCaps = Resize(value.RegularDustCaps, 3); value.SeasonalDustCaps = Resize(value.SeasonalDustCaps, 3);
        value.OwnedSkins = value.OwnedSkins ?? Array.Empty<int>(); value.OwnedEmotes = value.OwnedEmotes ?? Array.Empty<int>();
        value.OwnedPetSkins = value.OwnedPetSkins ?? Array.Empty<int>(); value.RegularBlueprints = value.RegularBlueprints ?? Array.Empty<int>();
        value.SeasonalBlueprints = value.SeasonalBlueprints ?? Array.Empty<int>();
        value.SeasonName = value.SeasonName ?? ""; value.BattlePassName = value.BattlePassName ?? "";
    }
}

internal sealed class AccountEnchantmentInfo
{
    public int Filled;
    public int Empty;
    public int Locked;
    public int Total => Filled + Empty + Locked;
    public string Rarity => new[] { "Common", "Uncommon", "Rare", "Legendary", "Divine" }[Math.Min(4, Total)];
    public string Description = "";

    public static AccountEnchantmentInfo Parse(string code)
    {
        var result = new AccountEnchantmentInfo();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 4096) return result;
        byte[] bytes = EnchantmentParser.SixBitStringToBytes(code);
        if (bytes.Length < 3 || (bytes[1] | bytes[2] << 8) != 1026) return result;
        var filled = new List<byte>();
        for (int pos = 3; pos + 1 < bytes.Length && pos < 11; pos += 2)
        {
            short slot = (short)(bytes[pos] | bytes[pos + 1] << 8);
            if (slot == -3) break;
            if (slot == -2) result.Locked++;
            else if (slot == -1) result.Empty++;
            else if (slot >= 0) { result.Filled++; filled.Add(bytes[pos]); filled.Add(bytes[pos + 1]); }
        }
        byte[] normalized = new byte[filled.Count + 5]; normalized[1] = 2; normalized[2] = 4;
        filled.CopyTo(normalized, 3); normalized[normalized.Length - 2] = 253;
        var names = EnchantmentParser.ParseSlotEnchants(Convert.ToBase64String(normalized).Replace('+', '-').Replace('/', '_')).names;
        if (result.Total > 0) result.Description = result.Rarity + " enchant slots: " + result.Filled + " filled, " + result.Empty + " empty, " + result.Locked + " locked"
            + (names.Count == 0 ? "" : "\n" + string.Join("\n", names));
        return result;
    }
}
