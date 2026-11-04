using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExaltHelper.Proxy.Networking;

namespace ExaltHelper;

internal sealed class AccountDeathBonus
{
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Fame { get; set; }
    public AccountDeathBonus Clone() => new AccountDeathBonus { Name = Name, Level = Level, Fame = Fame };
}

internal static partial class AccountWorkspace
{
    internal static readonly string[] TreasurySections = { "seasonal.characters", "seasonal.vault", "seasonal.materials", "seasonal.potions", "seasonal.pets", "seasonal.gifts",
        "regular.characters", "regular.vault", "regular.materials", "regular.potions", "regular.pets", "regular.gifts", "regular.spoils" };
    internal static readonly string[] TreasuryCategories = { "Weapons", "Abilities", "Armor", "Rings", "Consumables", "Marks", "Keys", "Skins", "Blueprints" };
    internal static string TreasurySectionName(string key)
    {
        string[] parts = key.Split('.');
        if (parts.Length != 2) return key;
        string name = parts[1] == "characters" ? "Characters" : parts[1] == "pets" ? "Pet inventories" : parts[1] == "spoils" ? "Seasonal spoils" : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[1]);
        return parts[1] == "spoils" ? name : (parts[0] == "seasonal" ? "Seasonal " : "Regular ") + name;
    }

    public static void SetCharacterLabel(int characterId, string label)
    {
        lock (Gate)
        {
            var c = state.Characters.Concat(state.DeadCharacters).FirstOrDefault(x => x.Id == characterId);
            if (c == null) return;
            c.Label = (label ?? "").Trim(); if (c.Label.Length > 80) c.Label = c.Label.Substring(0, 80);
            TouchLocked();
        }
    }

    public static void MoveCharacter(int characterId, int direction)
    {
        lock (Gate)
        {
            var selected = state.Characters.FirstOrDefault(c => c.Id == characterId); if (selected == null) return;
            var list = state.Characters.Where(c => c.Seasonal == selected.Seasonal).OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();
            int index = list.IndexOf(selected), destination = index + Math.Sign(direction);
            if (destination < 0 || destination >= list.Count) return;
            list.RemoveAt(index); list.Insert(destination, selected);
            for (int i = 0; i < list.Count; i++) list[i].Order = i;
            TouchLocked();
        }
    }

    public static void SaveTreasuryLayout(string[] sections, string[] categories)
    {
        lock (Gate)
        {
            state.TreasurySectionOrder = OrderedKeys(sections, TreasurySections);
            state.TreasuryCategoryOrder = OrderedKeys(categories, TreasuryCategories); TouchLocked();
        }
    }
    internal static string[] OrderedKeys(string[] preferred, string[] allowed) => (preferred ?? Array.Empty<string>()).Where(allowed.Contains).Concat(allowed).Distinct().ToArray();
    public static void ToggleTreasurySection(string key)
    {
        if (!TreasurySections.Contains(key)) return;
        lock (Gate)
        {
            var collapsed = new HashSet<string>(state.CollapsedTreasurySections);
            if (!collapsed.Remove(key)) collapsed.Add(key);
            state.CollapsedTreasurySections = collapsed.ToArray(); TouchLocked();
        }
    }

    private static void MarkDeadLocked(int characterId, string killedBy, int fame, int graveType, string pcStats, List<AccountDeathBonus> bonuses)
    {
        var character = state.DeadCharacters.FirstOrDefault(c => c.Id == characterId);
        if (character == null)
        {
            var living = state.Characters.FirstOrDefault(c => c.Id == characterId);
            if (living == null) return;
            character = living.Clone(); state.Characters.Remove(living); state.DeadCharacters.Add(character);
        }
        character.IsDead = true; character.KilledBy = killedBy ?? "Unknown"; character.DeathUtc = character.DeathUtc ?? DateTime.UtcNow;
        if (fame > 0) character.DeathFame = fame;
        if (graveType > 0) character.GraveType = graveType;
        if (bonuses != null) character.DeathBonuses = bonuses;
        if (!string.IsNullOrEmpty(pcStats))
        {
            var decoded = AccountWorkspaceFame.Decode(pcStats);
            if (decoded != null) { character.PcStats = decoded; character.HasPcStats = true; }
        }
        if (state.CurrentCharacterId == characterId) state.CurrentCharacterId = -1;
        TouchLocked();
    }

    private static void ApplyDeathLocked(PacketReader reader)
    {
        string account = ReadPacketString(reader); int character = reader.ReadCompressedInt(); string killedBy = ReadPacketString(reader);
        int grave = reader.ReadInt32(), fame = reader.ReadCompressedInt();
        reader.ReadCompressedInt(); reader.ReadInt32();
        int count = reader.ReadCompressedInt(); if (count < 0 || count > 1000) throw new InvalidDataException("Invalid fame bonus count.");
        var bonuses = new List<AccountDeathBonus>();
        for (int i = 0; i < count; i++) bonuses.Add(new AccountDeathBonus { Name = ReadPacketString(reader), Level = reader.ReadCompressedInt(), Fame = reader.ReadCompressedInt() });
        string stats = ReadPacketString(reader);
        if (string.IsNullOrEmpty(account) || state.AccountKey != HashAccountId(account)) return;
        MarkDeadLocked(character, killedBy, fame, grave, stats, bonuses);
    }
}
