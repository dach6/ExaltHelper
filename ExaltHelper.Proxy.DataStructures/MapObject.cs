using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.DataStructures;

internal class MapObject
{
	public ushort ObjectType;

	public int ObjectId;

	public WorldPosData Position = WorldPosData.Zero;

	public WorldPosData TargetPosition = WorldPosData.Zero;

	public int LastMoveTime;

	public long LastTickTime;

	public WorldPosData Velocity = new WorldPosData(0f, 0f);

	public bool IsPlayer;

	public bool IsEnemy;

	public bool IsCharacter;

	public bool OccupySquare;

	public bool FullOccupy;

	public bool EnemyOccupySquare;

	public bool IsStatic;

	public string ItemDataString;

	public int[] Exaltations = new int[8];

	[CompilerGenerated]
	private int _maxHp;

	[CompilerGenerated]
	private int _hp;

	[CompilerGenerated]
	private int _size;

	[CompilerGenerated]
	private int _maxMp;

	[CompilerGenerated]
	private int _mp;

	[CompilerGenerated]
	private int _attack;

	[CompilerGenerated]
	private int _defense;

	[CompilerGenerated]
	private int _speed;

	[CompilerGenerated]
	private int _wisdom;

	[CompilerGenerated]
	private int _dexterity;

	[CompilerGenerated]
	private int _skin;

	[CompilerGenerated]
	private int _healthBonus;

	[CompilerGenerated]
	private int _manaBonus;

	[CompilerGenerated]
	private int _attackBonus;

	[CompilerGenerated]
	private int _defenseBonus;

	[CompilerGenerated]
	private int _speedBonus;

	[CompilerGenerated]
	private int _vitalityBonus;

	[CompilerGenerated]
	private int _wisdomBonus;

	[CompilerGenerated]
	private int _dexterityBonus;

	[CompilerGenerated]
	private int _nextLevelExperience;

	[CompilerGenerated]
	private int _experience;

	[CompilerGenerated]
	private int _level;

	[CompilerGenerated]
	private int _credits;

	[CompilerGenerated]
	private string _name;

	[CompilerGenerated]
	private string _playerName;

	[CompilerGenerated]
	private int _effects;

	[CompilerGenerated]
	private int _effects2;

	[CompilerGenerated]
	private int _stars;

	[CompilerGenerated]
	private int _characterFame;

	[CompilerGenerated]
	private int _characterFameGoal;

	[CompilerGenerated]
	private string _guildName;

	[CompilerGenerated]
	private int _backpackSlots;

	[CompilerGenerated]
	private bool _isInvulnerable;

	public int[] Inventory = new int[28]
	{
		-1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
		-1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
		-1, -1, -1, -1, -1, -1, -1, -1
	};

	public bool IsPortalUsable;

	public string AccountId;

	[CompilerGenerated]
	private string _ownerAccountId;

	[CompilerGenerated]
	private bool _isStasis;

	public int AccountFame;

	public int GuildRank;

	[CompilerGenerated]
	private string _customName;

	[CompilerGenerated]
	private int _merchandiseType;

	[CompilerGenerated]
	private int _petInstanceId;

	[CompilerGenerated]
	private string _petName;

	[CompilerGenerated]
	private int _vitality;

	[CompilerGenerated]
	private int _petType;

	[CompilerGenerated]
	private int _petRarity;

	[CompilerGenerated]
	private int _petMaximumLevel;

	[CompilerGenerated]
	private int _petFamily;

	[CompilerGenerated]
	private int _petPoints0;

	[CompilerGenerated]
	private int _petPoints1;

	[CompilerGenerated]
	private int _petPoints2;

	[CompilerGenerated]
	private int _petLevel0;

	[CompilerGenerated]
	private int _petLevel1;

	[CompilerGenerated]
	private int _petLevel2;

	[CompilerGenerated]
	private int _petAbilityType0;

	[CompilerGenerated]
	private int _petAbilityType1;

	[CompilerGenerated]
	private int _petAbilityType2;

	[CompilerGenerated]
	private int _oxygenBar;

	[CompilerGenerated]
	private int _damageMultiplier = 1000;

	[CompilerGenerated]
	private bool _isDead;

	[CompilerGenerated]
	private string _crucibleId;

	[CompilerGenerated]
	private bool _isQuest;

	[CompilerGenerated]
	private bool _isGod;

	[CompilerGenerated]
	private bool _isCube;

	[CompilerGenerated]
	private ObjectStructure _objectStructure;

	private static readonly List<int> IgnoredEntityIds = new List<int>
	{
		1337, 2048, 2340, 2349, 3448, 3449, 3452, 3613, 3622, 4312,
		4324, 4325, 4326, 5943, 8200, 24092, 24327, 24351, 24363, 24587,
		29003, 29021, 29039, 29341, 29342, 29723, 29764, 30026, 45104, 45371,
		45076, 28618, 28619, 32751, 29793
	};

	public int TotalExaltations
	{
		get
		{
			if (Exaltations == null)
			{
				return 0;
			}
			return Exaltations.Sum();
		}
	}

	public int MaxHp
	{
		[CompilerGenerated]
		get
		{
			return _maxHp;
		}
		[CompilerGenerated]
		set
		{
			_maxHp = value;
		}
	}

	public int Hp
	{
		[CompilerGenerated]
		get
		{
			return _hp;
		}
		[CompilerGenerated]
		set
		{
			_hp = value;
		}
	}

	public int Size
	{
		[CompilerGenerated]
		get
		{
			return _size;
		}
		[CompilerGenerated]
		set
		{
			_size = value;
		}
	}

	public int MaxMp
	{
		[CompilerGenerated]
		get
		{
			return _maxMp;
		}
		[CompilerGenerated]
		set
		{
			_maxMp = value;
		}
	}

	public int Mp
	{
		[CompilerGenerated]
		get
		{
			return _mp;
		}
		[CompilerGenerated]
		set
		{
			_mp = value;
		}
	}

	public int Attack
	{
		[CompilerGenerated]
		get
		{
			return _attack;
		}
		[CompilerGenerated]
		set
		{
			_attack = value;
		}
	}

	public int Defense
	{
		[CompilerGenerated]
		get
		{
			return _defense;
		}
		[CompilerGenerated]
		set
		{
			_defense = value;
		}
	}

	public int Speed
	{
		[CompilerGenerated]
		get
		{
			return _speed;
		}
		[CompilerGenerated]
		set
		{
			_speed = value;
		}
	}

	public int Wisdom
	{
		[CompilerGenerated]
		get
		{
			return _wisdom;
		}
		[CompilerGenerated]
		set
		{
			_wisdom = value;
		}
	}

	public int Dexterity
	{
		[CompilerGenerated]
		get
		{
			return _dexterity;
		}
		[CompilerGenerated]
		set
		{
			_dexterity = value;
		}
	}

	public int Skin
	{
		[CompilerGenerated]
		get
		{
			return _skin;
		}
		[CompilerGenerated]
		set
		{
			_skin = value;
		}
	}

	public int HealthBonus
	{
		[CompilerGenerated]
		get
		{
			return _healthBonus;
		}
		[CompilerGenerated]
		set
		{
			_healthBonus = value;
		}
	}

	public int ManaBonus
	{
		[CompilerGenerated]
		get
		{
			return _manaBonus;
		}
		[CompilerGenerated]
		set
		{
			_manaBonus = value;
		}
	}

	public int AttackBonus
	{
		[CompilerGenerated]
		get
		{
			return _attackBonus;
		}
		[CompilerGenerated]
		set
		{
			_attackBonus = value;
		}
	}

	public int DefenseBonus
	{
		[CompilerGenerated]
		get
		{
			return _defenseBonus;
		}
		[CompilerGenerated]
		set
		{
			_defenseBonus = value;
		}
	}

	public int SpeedBonus
	{
		[CompilerGenerated]
		get
		{
			return _speedBonus;
		}
		[CompilerGenerated]
		set
		{
			_speedBonus = value;
		}
	}

	public int VitalityBonus
	{
		[CompilerGenerated]
		get
		{
			return _vitalityBonus;
		}
		[CompilerGenerated]
		set
		{
			_vitalityBonus = value;
		}
	}

	public int WisdomBonus
	{
		[CompilerGenerated]
		get
		{
			return _wisdomBonus;
		}
		[CompilerGenerated]
		set
		{
			_wisdomBonus = value;
		}
	}

	public int DexterityBonus
	{
		[CompilerGenerated]
		get
		{
			return _dexterityBonus;
		}
		[CompilerGenerated]
		set
		{
			_dexterityBonus = value;
		}
	}

	public int NextLevelExperience
	{
		[CompilerGenerated]
		get
		{
			return _nextLevelExperience;
		}
		[CompilerGenerated]
		set
		{
			_nextLevelExperience = value;
		}
	}

	public int Experience
	{
		[CompilerGenerated]
		get
		{
			return _experience;
		}
		[CompilerGenerated]
		set
		{
			_experience = value;
		}
	}

	public int Level
	{
		[CompilerGenerated]
		get
		{
			return _level;
		}
		[CompilerGenerated]
		set
		{
			_level = value;
		}
	}

	public int Credits
	{
		[CompilerGenerated]
		get
		{
			return _credits;
		}
		[CompilerGenerated]
		set
		{
			_credits = value;
		}
	}

	public string Name
	{
		[CompilerGenerated]
		get
		{
			return _name;
		}
		[CompilerGenerated]
		set
		{
			_name = value;
		}
	}

	public string PlayerName
	{
		[CompilerGenerated]
		get
		{
			return _playerName;
		}
		[CompilerGenerated]
		set
		{
			_playerName = value;
		}
	}

	public int Effects
	{
		[CompilerGenerated]
		get
		{
			return _effects;
		}
		[CompilerGenerated]
		set
		{
			_effects = value;
		}
	}

	public int Effects2
	{
		[CompilerGenerated]
		get
		{
			return _effects2;
		}
		[CompilerGenerated]
		set
		{
			_effects2 = value;
		}
	}

	public int Stars
	{
		[CompilerGenerated]
		get
		{
			return _stars;
		}
		[CompilerGenerated]
		set
		{
			_stars = value;
		}
	}

	public int CharacterFame
	{
		[CompilerGenerated]
		get
		{
			return _characterFame;
		}
		[CompilerGenerated]
		set
		{
			_characterFame = value;
		}
	}

	public int CharacterFameGoal
	{
		[CompilerGenerated]
		get
		{
			return _characterFameGoal;
		}
		[CompilerGenerated]
		set
		{
			_characterFameGoal = value;
		}
	}

	public string GuildName
	{
		[CompilerGenerated]
		get
		{
			return _guildName;
		}
		[CompilerGenerated]
		set
		{
			_guildName = value;
		}
	}

	public int BackpackSlots
	{
		[CompilerGenerated]
		get
		{
			return _backpackSlots;
		}
		[CompilerGenerated]
		set
		{
			_backpackSlots = value;
		}
	}

	public int BackpackSlotCount => BackpackSlots + 12;

	public bool IsDrawText
	{
		[CompilerGenerated]
		get
		{
			return _isInvulnerable;
		}
		[CompilerGenerated]
		set
		{
			_isInvulnerable = value;
		}
	}

	public string OwnerAccountId
	{
		[CompilerGenerated]
		get
		{
			return _ownerAccountId;
		}
		[CompilerGenerated]
		set
		{
			_ownerAccountId = value;
		}
	}

	public bool IsNameChosen
	{
		[CompilerGenerated]
		get
		{
			return _isStasis;
		}
		[CompilerGenerated]
		set
		{
			_isStasis = value;
		}
	}

	public string StructureName
	{
		[CompilerGenerated]
		get
		{
			return _customName;
		}
		[CompilerGenerated]
		set
		{
			_customName = value;
		}
	}

	public int MerchandiseType
	{
		[CompilerGenerated]
		get
		{
			return _merchandiseType;
		}
		[CompilerGenerated]
		set
		{
			_merchandiseType = value;
		}
	}

	public int PetInstanceId
	{
		[CompilerGenerated]
		get
		{
			return _petInstanceId;
		}
		[CompilerGenerated]
		set
		{
			_petInstanceId = value;
		}
	}

	public string PetName
	{
		[CompilerGenerated]
		get
		{
			return _petName;
		}
		[CompilerGenerated]
		set
		{
			_petName = value;
		}
	}

	public int Vitality
	{
		[CompilerGenerated]
		get
		{
			return _vitality;
		}
		[CompilerGenerated]
		set
		{
			_vitality = value;
		}
	}

	public int PetType
	{
		[CompilerGenerated]
		get
		{
			return _petType;
		}
		[CompilerGenerated]
		set
		{
			_petType = value;
		}
	}

	public int PetRarity
	{
		[CompilerGenerated]
		get
		{
			return _petRarity;
		}
		[CompilerGenerated]
		set
		{
			_petRarity = value;
		}
	}

	public int PetMaximumLevel
	{
		[CompilerGenerated]
		get
		{
			return _petMaximumLevel;
		}
		[CompilerGenerated]
		set
		{
			_petMaximumLevel = value;
		}
	}

	public int PetFamily
	{
		[CompilerGenerated]
		get
		{
			return _petFamily;
		}
		[CompilerGenerated]
		set
		{
			_petFamily = value;
		}
	}

	public int PetPoints0
	{
		[CompilerGenerated]
		get
		{
			return _petPoints0;
		}
		[CompilerGenerated]
		set
		{
			_petPoints0 = value;
		}
	}

	public int PetPoints1
	{
		[CompilerGenerated]
		get
		{
			return _petPoints1;
		}
		[CompilerGenerated]
		set
		{
			_petPoints1 = value;
		}
	}

	public int PetPoints2
	{
		[CompilerGenerated]
		get
		{
			return _petPoints2;
		}
		[CompilerGenerated]
		set
		{
			_petPoints2 = value;
		}
	}

	public int PetLevel0
	{
		[CompilerGenerated]
		get
		{
			return _petLevel0;
		}
		[CompilerGenerated]
		set
		{
			_petLevel0 = value;
		}
	}

	public int PetLevel1
	{
		[CompilerGenerated]
		get
		{
			return _petLevel1;
		}
		[CompilerGenerated]
		set
		{
			_petLevel1 = value;
		}
	}

	public int PetLevel2
	{
		[CompilerGenerated]
		get
		{
			return _petLevel2;
		}
		[CompilerGenerated]
		set
		{
			_petLevel2 = value;
		}
	}

	public int PetAbilityType0
	{
		[CompilerGenerated]
		get
		{
			return _petAbilityType0;
		}
		[CompilerGenerated]
		set
		{
			_petAbilityType0 = value;
		}
	}

	public int PetAbilityType1
	{
		[CompilerGenerated]
		get
		{
			return _petAbilityType1;
		}
		[CompilerGenerated]
		set
		{
			_petAbilityType1 = value;
		}
	}

	public int PetAbilityType2
	{
		[CompilerGenerated]
		get
		{
			return _petAbilityType2;
		}
		[CompilerGenerated]
		set
		{
			_petAbilityType2 = value;
		}
	}

	public int OxygenBar
	{
		[CompilerGenerated]
		get
		{
			return _oxygenBar;
		}
		[CompilerGenerated]
		private set
		{
			_oxygenBar = value;
		}
	}

	public int DamageMultiplier
	{
		[CompilerGenerated]
		get
		{
			return _damageMultiplier;
		}
		[CompilerGenerated]
		private set
		{
			_damageMultiplier = value;
		}
	}

	public bool IsDead
	{
		[CompilerGenerated]
		get
		{
			return _isDead;
		}
		[CompilerGenerated]
		set
		{
			_isDead = value;
		}
	}

	public int ExaltationBonusDamage { get; set; } = 1000;
	public string BloodRitualId { get; set; }

	public string CrucibleId
	{
		[CompilerGenerated]
		get
		{
			return _crucibleId;
		}
		[CompilerGenerated]
		set
		{
			_crucibleId = value;
		}
	}

	public bool IsQuest
	{
		[CompilerGenerated]
		get
		{
			return _isQuest;
		}
		[CompilerGenerated]
		private set
		{
			_isQuest = value;
		}
	}

	public bool IsGod
	{
		[CompilerGenerated]
		get
		{
			return _isGod;
		}
		[CompilerGenerated]
		private set
		{
			_isGod = value;
		}
	}

	public bool IsCube
	{
		[CompilerGenerated]
		get
		{
			return _isCube;
		}
		[CompilerGenerated]
		private set
		{
			_isCube = value;
		}
	}

	public ObjectStructure ObjectDef
	{
		[CompilerGenerated]
		get
		{
			return _objectStructure;
		}
		[CompilerGenerated]
		private set
		{
			_objectStructure = value;
		}
	}

	public double TileMoveSpeed => 4.0 + 5.6 * ((double)Speed / 75.0);

	public MapObject(int objectId)
	{
		ObjectId = objectId;
	}

	public MapObject(ObjectData objectData)
	{
		ObjectType = objectData.ObjectType;
		ObjectId = objectData.Stats.ObjectId;
		InitializeFromObjectData(objectData);
	}

	public void InitializeFromObjectData(ObjectData objectData)
	{
		ObjectType = objectData.ObjectType;
		ObjectId = objectData.Stats.ObjectId;
		ObjectStructure objectStructure = GameData.Objects.GetById(ObjectType);
		if (objectStructure != null)
		{
			MaxHp = 100;
			Hp = 100;
			IsPlayer = objectStructure.Player;
			IsEnemy = objectStructure.Enemy;
			IsCharacter = objectStructure.ObjectClass == "Character";
			OccupySquare = objectStructure.OccupySquare;
			FullOccupy = objectStructure.FullOccupy;
			EnemyOccupySquare = objectStructure.EnemyOccupySquare;
			IsStatic = objectStructure.Static;
			Defense = objectStructure.Defense;
			MaxHp = objectStructure.MaxHP;
			Size = objectStructure.Size;
			StructureName = objectStructure.Name;
			IsQuest = objectStructure.Quest || IgnoredEntityIds.Contains(ObjectType);
			IsGod = objectStructure.God;
			IsCube = objectStructure.Cube;
			ObjectDef = objectStructure;
		}
		else
		{
			Program.LogWarning("core", $"structure null {ObjectType:X}");
		}
		UpdateFromObjectStatsData(objectData.Stats, 0, -1, -1, -1L);
	}

	public void UpdateFromObjectStatsData(ObjectStatsData status, int tickDuration, int tickId, int previousTickId, long timestamp, bool unusedUpdateFlag = false)
	{
		TargetPosition = (WorldPosData)status.Position.Clone();
		if (tickDuration != 0)
		{
			UpdateMovement(status.Position.X, status.Position.Y, tickDuration, tickId, previousTickId, timestamp);
		}
		else if (Position == null || Position.X == 0.0)
		{
			Position = (WorldPosData)status.Position.Clone();
		}
		foreach (StatData item in status.StatList)
		{
			switch ((byte)item.StatTypeField)
			{
			case 0:
				MaxHp = item.StatValue;
				break;
			case 1:
				Hp = item.StatValue;
				break;
			case 2:
				Size = item.StatValue;
				break;
			case 3:
				MaxMp = item.StatValue;
				break;
			case 4:
				Mp = item.StatValue;
				break;
			case 5:
				NextLevelExperience = item.StatValue;
				break;
			case 6:
				Experience = item.StatValue;
				break;
			case 7:
				Level = item.StatValue;
				break;
			case 8:
				Inventory[0] = item.StatValue;
				break;
			case 9:
				Inventory[1] = item.StatValue;
				break;
			case 10:
				Inventory[2] = item.StatValue;
				break;
			case 11:
				Inventory[3] = item.StatValue;
				break;
			case 12:
				Inventory[4] = item.StatValue;
				break;
			case 13:
				Inventory[5] = item.StatValue;
				break;
			case 14:
				Inventory[6] = item.StatValue;
				break;
			case 15:
				Inventory[7] = item.StatValue;
				break;
			case 16:
				Inventory[8] = item.StatValue;
				break;
			case 17:
				Inventory[9] = item.StatValue;
				break;
			case 18:
				Inventory[10] = item.StatValue;
				break;
			case 19:
				Inventory[11] = item.StatValue;
				break;
			case 20:
				Attack = item.StatValue;
				break;
			case 21:
				Defense = item.StatValue;
				break;
			case 22:
				Speed = item.StatValue;
				break;
			// Protocol IDs 25-28 are skin, VIT, WIS and DEX respectively.
			// Keep the names accurate: ability scaling consumes these properties.
			case (byte)StatsTypeEnum.Skin:
				Skin = item.StatValue;
				break;
			case (byte)StatsTypeEnum.Vitality:
				Vitality = item.StatValue;
				break;
			case (byte)StatsTypeEnum.Wisdom:
				Wisdom = item.StatValue;
				break;
			case (byte)StatsTypeEnum.Dexterity:
				Dexterity = item.StatValue;
				break;
			case 29:
				Effects = item.StatValue;
				break;
			case 30:
				Stars = item.StatValue;
				break;
			case 31:
				Name = item.StatStringValue;
				PlayerName = (string.IsNullOrEmpty(item.StatStringValue) ? string.Empty : item.StatStringValue.Split(',')[0]);
				break;
			case 34:
				MerchandiseType = item.StatValue;
				break;
			case 35:
				Credits = item.StatValue;
				break;
			case 37:
				IsPortalUsable = item.StatValue == 1;
				break;
			case 38:
				AccountId = item.StatStringValue;
				break;
			case 39:
				AccountFame = item.StatValue;
				break;
			case 46:
				HealthBonus = item.StatValue;
				break;
			case 47:
				ManaBonus = item.StatValue;
				break;
			case 48:
				AttackBonus = item.StatValue;
				break;
			case 49:
				DefenseBonus = item.StatValue;
				break;
			case 50:
				SpeedBonus = item.StatValue;
				break;
			case 51:
				VitalityBonus = item.StatValue;
				break;
			case 52:
				WisdomBonus = item.StatValue;
				break;
			case 53:
				DexterityBonus = item.StatValue;
				break;
			case 54:
				OwnerAccountId = item.StatStringValue;
				break;
			case 56:
				IsNameChosen = item.StatValue == 1;
				break;
			case 57:
				CharacterFame = item.StatValue;
				break;
			case 58:
				CharacterFameGoal = item.StatValue;
				break;
			case 62:
				GuildName = item.StatStringValue;
				break;
			case 63:
				GuildRank = item.StatValue;
				break;
			case 64:
				OxygenBar = item.StatValue;
				break;
			case 74:
				DamageMultiplier = item.StatValue;
				break;
			case 80:
				ItemDataString = item.StatStringValue;
				break;
			case 81:
				PetInstanceId = item.StatValue;
				break;
			case 82:
				PetName = item.StatStringValue;
				break;
			case 83:
				PetType = item.StatValue;
				break;
			case 84:
				PetRarity = item.StatValue;
				break;
			case 85:
				PetMaximumLevel = item.StatValue;
				break;
			case 86:
				PetFamily = item.StatValue;
				break;
			case 87:
				PetPoints0 = item.StatValue;
				break;
			case 88:
				PetPoints1 = item.StatValue;
				break;
			case 89:
				PetPoints2 = item.StatValue;
				break;
			case 90:
				PetLevel0 = item.StatValue;
				break;
			case 91:
				PetLevel1 = item.StatValue;
				break;
			case 92:
				PetLevel2 = item.StatValue;
				break;
			case 93:
				PetAbilityType0 = item.StatValue;
				break;
			case 94:
				PetAbilityType1 = item.StatValue;
				break;
			case 95:
				PetAbilityType2 = item.StatValue;
				break;
			case 96:
				Effects2 = item.StatValue;
				break;
			case 105:
				Exaltations[0] = item.StatValue;
				break;
			case 106:
				Exaltations[1] = item.StatValue;
				break;
			case 107:
				Exaltations[2] = item.StatValue;
				break;
			case 108:
				Exaltations[3] = item.StatValue;
				break;
			case 109:
				Exaltations[4] = item.StatValue;
				break;
			case 110:
				Exaltations[5] = item.StatValue;
				break;
			case 111:
				Exaltations[6] = item.StatValue;
				break;
			case 112:
				Exaltations[7] = item.StatValue;
				break;
			case 113:
				ExaltationBonusDamage = item.StatValue;
				break;
			case 119:
				IsDrawText = item.StatValue == 1;
				break;
			case 24:
				IsDead = item.StatValue == 1;
				break;
			case 128:
				CrucibleId = item.StatStringValue;
				break;
			case 155:
				BloodRitualId = item.StatStringValue;
				break;
			case 130:
				BackpackSlots = item.StatValue;
				break;
			case 131:
				Inventory[12] = item.StatValue;
				break;
			case 132:
				Inventory[13] = item.StatValue;
				break;
			case 133:
				Inventory[14] = item.StatValue;
				break;
			case 134:
				Inventory[15] = item.StatValue;
				break;
			case 135:
				Inventory[16] = item.StatValue;
				break;
			case 136:
				Inventory[17] = item.StatValue;
				break;
			case 137:
				Inventory[18] = item.StatValue;
				break;
			case 138:
				Inventory[19] = item.StatValue;
				break;
			case 139:
				Inventory[20] = item.StatValue;
				break;
			case 140:
				Inventory[21] = item.StatValue;
				break;
			case 141:
				Inventory[22] = item.StatValue;
				break;
			case 142:
				Inventory[23] = item.StatValue;
				break;
			case 143:
				Inventory[24] = item.StatValue;
				break;
			case 144:
				Inventory[25] = item.StatValue;
				break;
			case 145:
				Inventory[26] = item.StatValue;
				break;
			case 146:
				Inventory[27] = item.StatValue;
				break;
			}
		}
	}

	public void SetPositionFloat(float x, float y)
	{
		Position.X = x;
		Position.Y = y;
	}

	public void SetPositionDouble(double x, double y)
	{
		Position.X = (float)x;
		Position.Y = (float)y;
	}

	public void SetPosition(WorldPosData position)
	{
		Position = position;
	}

	public void UpdateMovement(double x, double y, int tickDuration, int tickId, int previousTickId, long timestamp)
	{
		if (LastMoveTime < previousTickId)
		{
			SetPositionDouble(TargetPosition.X, TargetPosition.Y);
		}
		LastTickTime = timestamp;
		TargetPosition.X = (float)x;
		TargetPosition.Y = (float)y;
		Velocity.X = (TargetPosition.X - Position.X) / (double)tickDuration;
		Velocity.Y = (TargetPosition.Y - Position.Y) / (double)tickDuration;
		LastMoveTime = tickId;
	}

	public void SetTargetPosition(WorldPosData position)
	{
		TargetPosition = position;
	}

	public bool IsAttackableTarget()
	{
		if (IsEnemy && IsCharacter)
		{
			return CanBeDamaged();
		}
		return false;
	}

	public bool CanBeDamaged()
	{
		if (!IsStasis() && !IsInvincible())
		{
			return !IsInvulnerable();
		}
		return false;
	}

	public bool IsQuiet()
	{
		return (Effects & 2) != 0;
	}

	public bool IsWeak()
	{
		return (Effects & 4) != 0;
	}

	public bool IsSlowed()
	{
		return (Effects & 8) != 0;
	}

	public bool IsSick()
	{
		return (Effects & 0x10) != 0;
	}

	public bool IsDazed()
	{
		return (Effects & 0x20) != 0;
	}

	public bool IsStunned()
	{
		return (Effects & 0x40) != 0;
	}

	public bool IsBlind()
	{
		return (Effects & 0x80) != 0;
	}

	public bool IsDrunk()
	{
		return (Effects & 0x200) != 0;
	}

	public bool IsConfused()
	{
		return (Effects & 0x400) != 0;
	}

	public bool IsBleeding()
	{
		return (Effects & 0x8000) != 0;
	}

	public bool IsStunImmune()
	{
		return (Effects & 0x800) != 0;
	}

	public bool IsInvisible()
	{
		return (Effects & 0x1000) != 0;
	}

	public bool IsParalyzed()
	{
		return (Effects & 0x2000) != 0;
	}

	public bool IsSpeedy()
	{
		return (Effects & 0x4000) != 0;
	}

	public bool IsNinjaSpeedy()
	{
		return (Effects & 0x10000000) != 0;
	}

	public bool IsHallucinating()
	{
		return (Effects & 0x100) != 0;
	}

	public bool IsHealing()
	{
		return (Effects & 0x20000) != 0;
	}

	public bool IsDamaging()
	{
		return (Effects & 0x40000) != 0;
	}

	public bool IsBerserk()
	{
		return (Effects & 0x80000) != 0;
	}

	public bool IsInCombat()
	{
		return (Effects & 0x100000) != 0;
	}

	public bool IsStasis()
	{
		return (Effects & 0x200000) != 0;
	}

	public bool IsInvincible()
	{
		if (!ObjectDef.Invincible)
		{
			return (Effects & 0x800000) != 0;
		}
		return true;
	}

	public bool IsInvulnerable()
	{
		if (!ObjectDef.Invulnerable)
		{
			return (Effects & 0x1000000) != 0;
		}
		return true;
	}

	public bool IsArmored()
	{
		return (Effects & 0x2000000) != 0;
	}

	public bool IsArmorBroken()
	{
		return (Effects & 0x4000000) != 0;
	}

	public bool IsHexed()
	{
		return (Effects2 & 0x10000) != 0;
	}

	public bool IsUnstable()
	{
		return (Effects & 0x20000000) != 0;
	}

	public bool IsDarkness()
	{
		return (Effects2 & 0x20000) != 0;
	}

	public bool IsSilenced()
	{
		return (Effects2 & 8) != 0;
	}

	public bool IsExposed()
	{
		return (Effects2 & 0x40) != 0;
	}

	public bool IsEnergized()
	{
		return (Effects2 & 0x20000000) != 0;
	}
}
