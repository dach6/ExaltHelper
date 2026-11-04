// Curated classifications and difficulty ratings adapted from RealmHound (MIT,
// 2026 Lovens & Pillar), a4c6f74, and the bundled ObjectID.list catalog.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal sealed class HudCombatPreferences
{
    public Dictionary<string, bool> Groups { get; set; } = HudCombatPolicy.Groups.Keys.ToDictionary(x => x, x => true, StringComparer.OrdinalIgnoreCase);
    public bool TrackSolo { get; set; } = true;

    public void Normalize()
    {
        var saved = Groups;
        Groups = HudCombatPolicy.Groups.Keys.ToDictionary(x => x, x => true, StringComparer.OrdinalIgnoreCase);
        if (saved != null) foreach (var entry in saved) if (Groups.ContainsKey(entry.Key)) Groups[entry.Key] = entry.Value;
    }

    public bool Tracks(string groupId) => groupId == null || !HudCombatPolicy.Groups.ContainsKey(groupId)
        || Groups == null || !Groups.TryGetValue(groupId, out bool enabled) || enabled;
}

internal static class HudCombatPolicy
{
    public static readonly IReadOnlyDictionary<string, string> Groups = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "exaltation", "Exaltation dungeon bosses" },
        { "expert", "Expert dungeon bosses" },
        { "adept", "Adept dungeon bosses" },
        { "beginner", "Rookie dungeon bosses" },
        { "beacon_guardian", "Beacon guardians" },
        { "rookie_hero", "Rookie Heroes of Oryx" },
        { "adept_hero", "Adept Heroes of Oryx" },
        { "veteran_hero", "Veteran Heroes of Oryx" },
        { "adept_encounter", "Adept encounters" },
        { "veteran_encounter", "Veteran encounters" },
        { "seasonal_encounter", "Seasonal and special bosses" },
        { "treasure_crate", "Treasure crates" }
    });

    public static readonly IReadOnlyDictionary<string, double> DungeonDifficulties = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        { "Abyss of Demons", 4.0 },
        { "Advanced Kogbold Steamworks", 9.0 },
        { "Ancient Ruins", 3.0 },
        { "Battle for the Nexus", 5.0 },
        { "Beachzone", 0.0 },
        { "Belladonna's Garden", 5.5 },
        { "Candyland Hunting Grounds", 3.5 },
        { "Cave of a Thousand Treasures", 2.5 },
        { "Chess", 9.0 },
        { "Cnidarian Reef", 5.5 },
        { "Court of Oryx", 4.5 },
        { "Crystal Cavern", 7.5 },
        { "Cultist Hideout", 7.0 },
        { "Cursed Library", 4.0 },
        { "Davy Jones' Locker", 5.0 },
        { "Deadwater Docks", 5.5 },
        { "Forax", 5.0 },
        { "Forbidden Jungle", 1.5 },
        { "Forest Maze", 1.0 },
        { "Fungal Cavern", 7.5 },
        { "Haunted Cemetery", 4.5 },
        { "Heroic Undead Lair", 6.0 },
        { "Hidden Interregnum", 7.0 },
        { "High Tech Terror", 6.5 },
        { "Ice Citadel", 7.0 },
        { "Ice Tomb", 6.0 },
        { "Infernal Abyss of Demons", 6.0 },
        { "Katalund", 5.0 },
        { "Kogbold Steamworks", 8.0 },
        { "Lair of Draconis", 6.0 },
        { "Lair of Shaitan", 6.0 },
        { "Legacy Heroic Abyss of Demons", 5.0 },
        { "Legacy Heroic Undead Lair", 5.0 },
        { "Legacy Abyss of Demons", 3.0 },
        { "Legacy Deadwater Docks", 5.0 },
        { "Legacy Deadwater Docks & Grotto", 5.0 },
        { "Legacy Forest Maze", 1.0 },
        { "Legacy Lair of Draconis", 4.0 },
        { "Legacy Lair of Draconis & Ivory", 4.0 },
        { "Legacy Lair of Shaitan", 5.5 },
        { "Legacy Pirate Cave", 1.0 },
        { "Legacy Spider Den", 1.0 },
        { "Legacy Sprite World", 2.0 },
        { "Legacy The Crawling Depths", 5.0 },
        { "Legacy The Shatters", 5.0 },
        { "Legacy Undead Lair", 4.0 },
        { "Legacy Woodland Labyrinth", 5.0 },
        { "Lost Halls", 8.0 },
        { "Mad God Mayhem", 6.0 },
        { "Mad Lab", 4.0 },
        { "Magic Woods", 3.0 },
        { "Malogia", 5.0 },
        { "Manor of the Immortals", 4.0 },
        { "Moonlight Village", 9.0 },
        { "Mountain Temple", 6.0 },
        { "Neo Forax", 8.0 },
        { "Neo Katalund", 8.0 },
        { "Neo Malogia", 8.0 },
        { "Neo Untaris", 8.0 },
        { "Ocean Trench", 5.0 },
        { "Oryx's Castle", 4.5 },
        { "Oryx's Chamber", 4.5 },
        { "Oryx's Sanctuary", 9.5 },
        { "Oryxmania", 8.0 },
        { "Parasite Chambers", 5.5 },
        { "Pirate Cave", 1.0 },
        { "Plagued Nest", 8.5 },
        { "Puppet Master's Encore", 5.5 },
        { "Puppet Master's Theatre", 4.0 },
        { "Queen Bunny Chamber", 5.5 },
        { "Rainbow Road", 0.0 },
        { "Santa's Workshop", 0.0 },
        { "Secluded Thicket", 6.5 },
        { "Snake Pit", 2.5 },
        { "Spectral Penitentiary", 8.0 },
        { "Spider Den", 1.5 },
        { "Sprite World", 2.5 },
        { "Stromwell's Rift I", 5.0 },
        { "Stromwell's Rift II", 7.5 },
        { "Stromwell's Rift III", 10.0 },
        { "Sulfurous Wetlands", 6.0 },
        { "The Crawling Depths", 5.5 },
        { "The Hive", 2.0 },
        { "The Inner Workings", 0.0 },
        { "The Machine", 4.5 },
        { "The Nest", 7.0 },
        { "The Shatters", 10.0 },
        { "The Tavern", 5.5 },
        { "The Third Dimension", 6.0 },
        { "The Trials of Cronus", 6.5 },
        { "The Void", 8.5 },
        { "Remnant of the Void", 8.5 },
        { "Tomb of the Ancients", 6.0 },
        { "Toxic Sewers", 4.0 },
        { "Undead Lair", 3.5 },
        { "Untaris", 5.0 },
        { "White Snake Invasion I", 6.5 },
        { "White Snake Invasion II", 7.5 },
        { "White Snake Invasion III", 8.5 },
        { "Wine Cellar", 6.0 },
        { "Woodland Labyrinth", 5.5 },
    });

    private static readonly Dictionary<string, double> Ratings = CreateRatings();
    private static Dictionary<string, double> CreateRatings()
    {
        var result = DungeonDifficulties.ToDictionary(p => DungeonKey(p.Key), p => p.Value, StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "oryx's castle", "Oryx's Castle" },
            { "oryx's chamber", "Oryx's Chamber" },
            { "wine cellar", "Wine Cellar" },
            { "ice cave", "Ice Tomb" },
            { "davy jones' locker", "Davy Jones' Locker" },
            { "belladonna's garden", "Belladonna's Garden" },
            { "santa's workshop", "Santa's Workshop" },
            { "santa workshop", "Santa's Workshop" },
            { "shatters", "The Shatters" },
            { "nest", "The Nest" },
            { "void", "The Void" },
            { "hive", "The Hive" },
            { "crawling depths", "The Crawling Depths" },
            { "third dimension", "The Third Dimension" },
            { "machine", "The Machine" },
            { "tavern", "The Tavern" },
            { "bella's", "Belladonna's Garden" },
            { "davy's", "Davy Jones' Locker" },
            { "shaitan's", "Lair of Shaitan" },
            { "reef", "Cnidarian Reef" },
            { "lab", "Mad Lab" },
            { "candy", "Candyland Hunting Grounds" },
            { "cemetery", "Haunted Cemetery" },
            { "manor", "Manor of the Immortals" },
            { "theatre", "Puppet Master's Theatre" },
            { "totem", "Forbidden Jungle" },
            { "treasure map", "Cave of a Thousand Treasures" },
            { "trials of cronus", "The Trials of Cronus" },
            { "the realm", "Realm" },
            { "bilgewater's grotto", "Legacy Deadwater Docks & Grotto" },
            { "legacy bilgewater's grotto", "Legacy Deadwater Docks & Grotto" },
            { "legacy deadwater docks", "Legacy Deadwater Docks & Grotto" },
            { "ivory wyvern portal", "Legacy Lair of Draconis & Ivory" },
            { "the ivory wyvern", "Legacy Lair of Draconis & Ivory" },
            { "legacy lair of draconis", "Legacy Lair of Draconis & Ivory" },
        };
        foreach (var alias in aliases) if (DungeonDifficulties.TryGetValue(alias.Value, out double value)) result[DungeonKey(alias.Key)] = value;
        return result;
    }

    private static string DungeonKey(string name)
    {
        string value = (name ?? "").Trim();
        if (value.StartsWith("{s.", StringComparison.OrdinalIgnoreCase) && value.EndsWith("}", StringComparison.Ordinal)) value = value.Substring(3, value.Length - 4);
        value = value.Replace('_', ' ').Trim();
        if (value.EndsWith(" Portal", StringComparison.OrdinalIgnoreCase)) value = value.Substring(0, value.Length - 7).Trim();
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    public static double? DungeonDifficulty(string name) => Ratings.TryGetValue(DungeonKey(name), out double rating) ? rating : (double?)null;
    public static string DungeonTier(string name) => DungeonDifficulty(name) is double rating ? DifficultyTier(rating) : "Unknown";
    public static string DifficultyTier(double rating) => double.IsNaN(rating) || double.IsInfinity(rating) || rating < 0 ? "Unknown"
        : rating <= 2 ? "Rookie" : rating <= 4.5 ? "Adept" : rating <= 6.5 ? "Expert" : "Exaltation";

    private static readonly HashSet<int> TreasureTypes = new HashSet<int> { 620, 621, 622, 2078, 2202, 2203, 2204, 2205, 3372, 5893, 5943, 6869, 6870, 6871, 6872, 9110, 18376, 18377, 18378, 20789, 20790, 20791, 20792, 20793, 20794, 20812, 28065, 28671, 28867, 29023, 33018, 45906, 45907, 45908, 45909, 46404, 56425, 56426, 56427, 56428, 56429, 56430, 56431, 56432 };
    private static readonly HashSet<int> SpecialTypes = new HashSet<int> { 8813, 19247, 19249, 38282, 38283, 38284, 42255, 42371, 44020, 47414, 49708, 49718, 49730, 49743, 49755 };
    private static readonly string[] SeasonalNames = { "keyper", "gardener", "appetizer", "biff", "snowy", "jack frost", "permafrost lord", "totalia", "bonegrind", "zombie horde" };
    private static readonly Dictionary<int, string> HeroTypes = new Dictionary<int, string>
    {
        { 44158, "rookie_hero" }, // Beached Buccaneer
        { 2331, "rookie_hero" }, // Lich
        { 2335, "rookie_hero" }, // Ent Ancient
        { 3522, "rookie_hero" }, // Great Coil Snake
        { 1656, "rookie_hero" }, // Oasis Giant
        { 1653, "rookie_hero" }, // Phoenix Lord
        { 21901, "rookie_hero" }, // New Phoenix Lord
        { 21904, "rookie_hero" }, // New Phoenix Reborn
        { 21946, "rookie_hero" }, // New Great Coil Snake
        { 2344, "rookie_hero" }, // Ghost King
        { 34508, "adept_hero" }, // Maiden of the Sea
        { 22034, "adept_hero" }, // Beach Bum
        { 34570, "adept_hero" }, // Artificial Sprite God
        { 44151, "adept_hero" }, // Celestial Sprite
        { 44153, "adept_hero" }, // Cosmic Sprite
        { 2907, "adept_hero" }, // Elder Sprite Tree
        { 24012, "adept_hero" }, // Alpha Werewolf
        { 34515, "adept_hero" }, // Flesh Golem
        { 34600, "adept_hero" }, // Sinister Scarecrow
        { 24023, "adept_hero" }, // Animal Merchant
        { 34499, "adept_hero" }, // Washed-Up Captain
        { 44164, "adept_hero" }, // Death Knight
        { 21943, "adept_hero" }, // Kage Kami
        { 17004, "adept_hero" }, // Lantern Holder
        { 24015, "adept_hero" }, // Shady Sect Leader
        { 34551, "adept_hero" }, // Demonic Effigy
        { 34573, "adept_hero" }, // Eternal Tormentor
        { 34476, "adept_hero" }, // Infernal Ironsmith
        { 1640, "adept_hero" }, // Red Demon
        { 34599, "adept_hero" }, // Dragon's Treasure
        { 34598, "adept_hero" }, // Slumbering Dragon
        { 16995, "adept_hero" }, // Insurgent Rebel Commander
        { 34553, "adept_hero" }, // Sword in the Stone?
        { 2369, "adept_hero" }, // Crystal Prisoner
        { 56341, "adept_hero" }, // Alien Reactor
        { 1033, "veteran_hero" }, // Sea Dragon
        { 34519, "veteran_hero" }, // Sunken Treasure
        { 34481, "veteran_hero" }, // Critter Brood
        { 34509, "veteran_hero" }, // Organ Harvester
        { 1986, "veteran_hero" }, // Scout Colony
        { 34480, "veteran_hero" }, // Assembled Giant
        { 34518, "veteran_hero" }, // Monstrous Grizzly
        { 16999, "veteran_hero" }, // Alluring Blossom
        { 51055, "veteran_hero" }, // Elder Ent Ancient
        { 34603, "veteran_hero" }, // Hornet's Nest
        { 56342, "veteran_hero" }, // Alien Reactor
    };
    private static readonly Dictionary<string, string> HeroNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Beached Buccaneer", "rookie_hero" },
        { "Lich", "rookie_hero" },
        { "Ent Ancient", "rookie_hero" },
        { "Great Coil Snake", "rookie_hero" },
        { "Oasis Giant", "rookie_hero" },
        { "Phoenix Lord", "rookie_hero" },
        { "New Phoenix Lord", "rookie_hero" },
        { "New Phoenix Reborn", "rookie_hero" },
        { "New Great Coil Snake", "rookie_hero" },
        { "Ghost King", "rookie_hero" },
        { "Maiden of the Sea", "adept_hero" },
        { "Beach Bum", "adept_hero" },
        { "Artificial Sprite God", "adept_hero" },
        { "Celestial Sprite", "adept_hero" },
        { "Cosmic Sprite", "adept_hero" },
        { "Elder Sprite Tree", "adept_hero" },
        { "Alpha Werewolf", "adept_hero" },
        { "Flesh Golem", "adept_hero" },
        { "Sinister Scarecrow", "adept_hero" },
        { "Animal Merchant", "adept_hero" },
        { "Washed-Up Captain", "adept_hero" },
        { "Death Knight", "adept_hero" },
        { "Kage Kami", "adept_hero" },
        { "Lantern Holder", "adept_hero" },
        { "Shady Sect Leader", "adept_hero" },
        { "Demonic Effigy", "adept_hero" },
        { "Eternal Tormentor", "adept_hero" },
        { "Infernal Ironsmith", "adept_hero" },
        { "Red Demon", "adept_hero" },
        { "Dragon's Treasure", "adept_hero" },
        { "Slumbering Dragon", "adept_hero" },
        { "Insurgent Rebel Commander", "adept_hero" },
        { "Sword in the Stone?", "adept_hero" },
        { "Crystal Prisoner", "adept_hero" },
        { "Alien Reactor", "adept_hero" },
        { "Sea Dragon", "veteran_hero" },
        { "Sunken Treasure", "veteran_hero" },
        { "Critter Brood", "veteran_hero" },
        { "Organ Harvester", "veteran_hero" },
        { "Scout Colony", "veteran_hero" },
        { "Assembled Giant", "veteran_hero" },
        { "Monstrous Grizzly", "veteran_hero" },
        { "Alluring Blossom", "veteran_hero" },
        { "Elder Ent Ancient", "veteran_hero" },
        { "Hornet's Nest", "veteran_hero" },
    };
    private static readonly Dictionary<int, string> TypeOverrides = new Dictionary<int, string>
    {
        { 22042, "adept_encounter" },
        { 4312, "veteran_encounter" },
        { 4324, "veteran_encounter" },
        { 4325, "veteran_encounter" },
        { 4326, "veteran_encounter" },
        { 8200, "adept" },
        { 3448, "adept" },
        { 3449, "adept" },
        { 3428, "adept_encounter" },
        { 22026, "adept_encounter" },
    };
    // Catalog labels are resolved at build time: combat capture does not load game files.
    // Duplicate catalog rows are merged; beacon labels take precedence over encounter labels.
    private static readonly Dictionary<int, string> LabeledTypes = new Dictionary<int, string>
    {
        { 16935, "veteran_encounter" }, // Sentient Monolith
        { 16990, "veteran_encounter" }, // Ravenous Rot
        { 20475, "adept_encounter" }, // Bilgewater's Galleon
        { 20744, "veteran_encounter" }, // Flying Behemoth
        { 20799, "veteran_encounter" }, // Behemoth's Egg
        { 21959, "veteran_encounter" }, // Avatar of the Forgotten King
        { 21983, "adept_encounter" }, // Rock Dragon
        { 21996, "veteran_encounter" }, // Lord of the Lost Lands
        { 22000, "adept_encounter" }, // Withered Sphinx
        { 22003, "adept_encounter" }, // Skull Shrine
        { 22009, "adept_encounter" }, // Calcium God
        { 22019, "adept_encounter" }, // Pentaract
        { 22023, "adept_encounter" }, // Reanimated Hermit God
        { 22034, "adept_encounter" }, // New Beach Bum
        { 22109, "veteran_encounter" }, // Kogbold Expedition Engine
        { 22110, "veteran_encounter" }, // Kogbold Expedition Engine
        { 22128, "adept_encounter" }, // Beer God
        { 22138, "veteran_encounter" }, // Crystal Worm Father
        { 22140, "veteran_encounter" }, // New EH Red Loot
        { 22141, "veteran_encounter" }, // New EH Blue Loot
        { 22142, "veteran_encounter" }, // New EH Yellow Loot
        { 22146, "veteran_encounter" }, // Lost Sentry
        { 22147, "veteran_encounter" }, // Ethereal Shrine
        { 22148, "adept_encounter" }, // New Temple Encounter
        { 22149, "adept_encounter" }, // Blue Oni
        { 22150, "adept_encounter" }, // Red Oni
        { 24005, "adept_encounter" }, // Legion General
        { 24008, "veteran_encounter" }, // Mutant Overgrowth
        { 34456, "veteran_encounter" }, // Adult Baneserpent
        { 34460, "adept_encounter" }, // Well of Souls
        { 34463, "adept_encounter" }, // Eye of the Underworld
        { 34489, "adept_encounter" }, // The Plague Doctor
        { 34493, "veteran_encounter" }, // Skeletal Centipede
        { 34510, "adept_encounter" }, // Sigma Werewolf
        { 34537, "adept_encounter" }, // Skull Knight
        { 34538, "adept_encounter" }, // World's Oyster
        { 34539, "adept_encounter" }, // Gates of the Nether
        { 34550, "adept_encounter" }, // Goblin Patriarch
        { 34558, "veteran_encounter" }, // Ancient Kaiju
        { 34565, "adept_encounter" }, // Maze Minotaur
        { 34581, "veteran_encounter" }, // Bloodroot Heart
        { 34584, "adept_encounter" }, // The Lich King
        { 34605, "adept_encounter" }, // Daughter of Limon
        { 45683, "adept_encounter" }, // Alien UFO
        { 45863, "adept_encounter" }, // Alien UFO
        { 47909, "adept_encounter" }, // Towering Perfection
        { 47927, "veteran_encounter" }, // Cube Deity
        { 47930, "adept_encounter" }, // Man-eating Barnacle
        { 47951, "adept_encounter" }, // Cold Soul
        { 47956, "adept_encounter" }, // Stygian Mirror
        { 47973, "adept_encounter" }, // Artificial Slop
        { 48027, "adept_encounter" }, // Cube God
        { 51031, "adept_encounter" }, // Eye of the Storm
        { 51032, "adept_encounter" }, // Possessed Pumpkin
        { 51035, "veteran_encounter" }, // Commander Calbrik
        { 51061, "adept_encounter" }, // Astral Rift
        { 51072, "adept_encounter" }, // Crab Sovereign
        { 51075, "veteran_encounter" }, // Aerial Warship
        { 51078, "veteran_encounter" }, // Corrupted Bramblethorn
        { 52990, "beacon_guardian" }, // Legion Beach Guard
        { 52991, "beacon_guardian" }, // Legion Nature Ruins Guard
        { 52992, "beacon_guardian" }, // Legion Undead Forest Guard
        { 52993, "beacon_guardian" }, // Legion Desert Guard
        { 52994, "beacon_guardian" }, // Legion Plains Guard
        { 52995, "beacon_guardian" }, // Legion City Guardian
        { 52996, "beacon_guardian" }, // Legion Reef Guardian
        { 52997, "beacon_guardian" }, // Legion Church Guardian
        { 52999, "beacon_guardian" }, // Legion Graveyard Guardian
        { 53000, "beacon_guardian" }, // Legion Hell Guardian
        { 53001, "beacon_guardian" }, // Legion Cove Guardian
        { 53002, "beacon_guardian" }, // Legion Fey Guardian
        { 53003, "beacon_guardian" }, // Legion Colonel
        { 53008, "beacon_guardian" }, // Legion Abyssal Defender
        { 53009, "beacon_guardian" }, // Legion Commissioner
        { 53013, "beacon_guardian" }, // Legion Missionary
        { 53021, "beacon_guardian" }, // Legion Principal
    };
    // Unlabeled encounter members inherit their curated anchor's realm group.
    private static readonly Dictionary<int, string> EncounterMembers = new Dictionary<int, string>
    {
        { 1337, "adept_encounter" }, // Pentaract
        { 3422, "adept_encounter" }, // Pentaract
        { 3423, "adept_encounter" }, // Pentaract
        { 4238, "veteran_encounter" }, // Killer Bee Nest
        { 4312, "veteran_encounter" }, // Killer Bee Nest
        { 4324, "veteran_encounter" }, // Killer Bee Nest
        { 4325, "veteran_encounter" }, // Killer Bee Nest
        { 4326, "veteran_encounter" }, // Killer Bee Nest
        { 16935, "veteran_encounter" }, // Sentient Monolith
        { 16936, "veteran_encounter" }, // Sentient Monolith
        { 20744, "veteran_encounter" }, // Flying Behemoth
        { 20799, "veteran_encounter" }, // Flying Behemoth
        { 21959, "veteran_encounter" }, // Avatar of the Forgotten King
        { 21963, "veteran_encounter" }, // Avatar of the Forgotten King
        { 21964, "veteran_encounter" }, // Avatar of the Forgotten King
        { 21967, "veteran_encounter" }, // Avatar of the Forgotten King
        { 21976, "veteran_encounter" }, // Avatar of the Forgotten King
        { 22018, "adept_encounter" }, // Pentaract
        { 22019, "adept_encounter" }, // Pentaract
        { 22149, "adept_encounter" }, // Jade and Garnet Statues
        { 22150, "adept_encounter" }, // Jade and Garnet Statues
        { 24020, "adept_hero" }, // Animal Merchant
        { 24021, "adept_hero" }, // Animal Merchant
        { 24023, "adept_hero" }, // Animal Merchant
        { 28618, "adept_encounter" }, // Jade and Garnet Statues
        { 28619, "adept_encounter" }, // Jade and Garnet Statues
        { 29517, "veteran_encounter" }, // Avatar of the Forgotten King
        { 34480, "veteran_hero" }, // Assembled Giant
        { 34485, "veteran_hero" }, // Assembled Giant
        { 34486, "veteran_hero" }, // Assembled Giant
        { 34489, "adept_encounter" }, // The Plague Doctor
        { 34490, "adept_encounter" }, // The Plague Doctor
        { 34538, "adept_encounter" }, // World's Oyster
        { 34546, "adept_encounter" }, // World's Oyster
        { 34598, "adept_hero" }, // Slumbering Dragon
        { 34599, "adept_hero" }, // Slumbering Dragon
        { 34603, "veteran_hero" }, // Hornet's Nest
        { 34604, "veteran_hero" }, // Hornet's Nest
        { 44894, "adept_encounter" }, // Towering Perfection
        { 44895, "adept_encounter" }, // Towering Perfection
        { 47909, "adept_encounter" }, // Towering Perfection
        { 47914, "adept_encounter" }, // Towering Perfection
        { 47915, "adept_encounter" }, // Towering Perfection
        { 47916, "adept_encounter" }, // Towering Perfection
        { 47917, "adept_encounter" }, // Towering Perfection
        { 51031, "adept_encounter" }, // Eye of the Storm
        { 51033, "adept_encounter" }, // Eye of the Storm
        { 51061, "adept_encounter" }, // Astral Rift
        { 51062, "adept_encounter" }, // Astral Rift
        { 51072, "adept_encounter" }, // Crab Sovereign
        { 51073, "adept_encounter" }, // Crab Sovereign
        { 53013, "beacon_guardian" }, // Legion Missionary
        { 53017, "beacon_guardian" }, // Legion Missionary
        { 53018, "beacon_guardian" }, // Legion Missionary
    };

    public static string Classify(string dungeon, int objectType, string bossName)
    {
        if (TreasureTypes.Contains(objectType)) return "treasure_crate";
        if (SpecialTypes.Contains(objectType)) return "seasonal_encounter";
        string name = (bossName ?? "").Trim();
        if (SeasonalNames.Any(fragment => name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)) return "seasonal_encounter";
        if (DungeonDifficulty(dungeon) is double difficulty) return difficulty >= 7 ? "exaltation" : difficulty >= 5 ? "expert" : difficulty >= 2.5 ? "adept" : "beginner";
        if (HeroTypes.TryGetValue(objectType, out string group) || HeroNames.TryGetValue(name, out group)) return group;
        if (TypeOverrides.TryGetValue(objectType, out group) || LabeledTypes.TryGetValue(objectType, out group) || EncounterMembers.TryGetValue(objectType, out group)) return group;
        return "unknown";
    }

    public static bool ShouldRecord(HudCombatPreferences preferences, DpsSnapshot snapshot, EnemyCombatSnapshot enemy)
    {
        if (snapshot == null || enemy == null) return false;
        if (preferences == null) return true;
        string localName = snapshot.LocalPlayer?.Name?.Trim();
        if (!preferences.TrackSolo && !(enemy.Damagers?.Any(p => p != null && !p.IsLocalPlayer && !string.IsNullOrWhiteSpace(p.Name)
            && !string.Equals(p.Name.Trim(), localName, StringComparison.OrdinalIgnoreCase)) ?? false)) return false;
        // Dungeon switches describe bosses. Ordinary enemies remain unknown unless
        // their type/name explicitly identifies a curated event, hero, or crate.
        return preferences.Tracks(Classify(enemy.IsBoss ? snapshot.DungeonName : null, enemy.ObjectType, enemy.Name));
    }
}
