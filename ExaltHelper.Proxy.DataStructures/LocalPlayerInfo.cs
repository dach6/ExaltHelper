using System.Collections.Generic;

namespace ExaltHelper.Proxy.DataStructures;

internal class LocalPlayerInfo
{
	public string Name { get; set; } = "Player";

	public string ClassName { get; set; } = "Knight";

	public int Level { get; set; } = 20;

	public int Stars { get; set; }

	public int CurrentHp { get; set; }

	public int MaxHp { get; set; }

	public int CurrentMp { get; set; }

	public int MaxMp { get; set; }

	public int Attack { get; set; }

	public int Defense { get; set; }

	public int Speed { get; set; }

	public int Dexterity { get; set; }

	public int Vitality { get; set; }

	public int Wisdom { get; set; }

	public int BaseLife { get; set; }

	public int BaseMana { get; set; }

	public int BaseAttack { get; set; }

	public int BaseDefense { get; set; }

	public int BaseSpeed { get; set; }

	public int BaseDexterity { get; set; }

	public int BaseVitality { get; set; }

	public int BaseWisdom { get; set; }

	public int MaxLife { get; set; }

	public int MaxMana { get; set; }

	public int MaxAttack { get; set; }

	public int MaxDefense { get; set; }

	public int MaxSpeed { get; set; }

	public int MaxDexterity { get; set; }

	public int MaxVitality { get; set; }

	public int MaxWisdom { get; set; }

	public string MaxedStats { get; set; } = "0/8";

	public int MaxedCount { get; set; }

	public int[] EquipmentIds { get; set; } = new int[4];

	public string[] EquipmentNames { get; set; } = new string[4] { "-", "-", "-", "-" };

	public string[] EquipmentRarities { get; set; } = new string[4] { "Common", "Common", "Common", "Common" };

	public int[] EquipmentEnchantCounts { get; set; } = new int[4];

	public List<string>[] EquipmentEnchants { get; set; } = new List<string>[4]
	{
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>()
	};

	public string WeaponBulletInfo { get; set; } = "-";

	public double WeaponDps { get; set; }

	public double TrueSetDps { get; set; }

	public int[] Exaltations { get; set; } = new int[8];

	public int TotalExalts { get; set; }
}
