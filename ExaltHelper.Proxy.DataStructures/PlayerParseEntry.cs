using System.Collections.Generic;

namespace ExaltHelper.Proxy.DataStructures;

internal class PlayerParseEntry
{
	public string Name { get; set; }

	public string ClassName { get; set; }

	public int CurrentHp { get; set; }

	public int MaxHp { get; set; }

	public string MaxedStats { get; set; } = "0/8";

	public int MaxedCount { get; set; }

	public int[] Exaltations { get; set; } = new int[8];

	public int TotalExalts { get; set; }

	public int[] EquipmentIds { get; set; } = new int[4];

	public string[] EquipmentNames { get; set; } = new string[4] { "-", "-", "-", "-" };

	public string[] EquipmentTiers { get; set; } = new string[4] { "-", "-", "-", "-" };

	public string[] EquipmentRarities { get; set; } = new string[4] { "-", "-", "-", "-" };

	public int[] EquipmentEnchantCounts { get; set; } = new int[4];

	public List<string>[] EquipmentEnchants { get; set; } = new List<string>[4]
	{
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>()
	};

	public int Stars { get; set; }

	public string GuildName { get; set; } = "";

	public bool IsLocalPlayer { get; set; }
}
