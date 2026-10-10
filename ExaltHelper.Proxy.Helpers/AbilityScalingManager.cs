using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper.Proxy.Helpers;

internal static class AbilityScalingManager
{
	public enum StatType
	{
		MAX_HP_STAT = 0,
		MAX_MP_STAT = 3,
		ATTACK_STAT = 20,
		DEFENSE_STAT = 21,
		SPEED_STAT = 22,
		VITALITY_STAT = 26,
		WISDOM_STAT = 27,
		DEXTERITY_STAT = 28
	}

	public class AbilityScalingData
	{
		public int WeaponId { get; }

		public StatType? ScalingStat { get; }

		public int ScalingMin { get; }

		public float DamagePerStat { get; }

		public int NumShots { get; }
		public bool IsLethalStrike { get; set; }
		public float BaseFlat { get; set; }
		public float BaseDefenseFraction { get; set; }
		public float DefenseFractionPerStat { get; set; }

		public AbilityScalingData(int weaponId, StatType? scalingStat, int scalingMin, float damagePerStat, int numShots)
		{
			WeaponId = weaponId;
			ScalingStat = scalingStat;
			ScalingMin = scalingMin;
			DamagePerStat = damagePerStat;
			NumShots = numShots;
		}

		public bool HasScaling()
		{
			return ScalingStat.HasValue && (DamagePerStat > 0f || IsLethalStrike);
		}
	}

	private static readonly Dictionary<int, AbilityScalingData> scalingData = new Dictionary<int, AbilityScalingData>();

	private static readonly Dictionary<int, int> projectileToWeaponMap = new Dictionary<int, int>();

	private static readonly object syncLock = new object();

	private static volatile bool isInitialized = false;

	public static void EnsureLoaded()
	{
		if (isInitialized)
		{
			return;
		}
		lock (syncLock)
		{
			if (isInitialized)
			{
				return;
			}
			try
			{
				string text = FindEquipXmlPath();
				if (!string.IsNullOrEmpty(text) && File.Exists(text))
				{
					ParseEquipXml(text);
					Console.WriteLine($"[AbilityScalingManager] Initialized with {scalingData.Count} abilities and {projectileToWeaponMap.Count} proc mappings.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[AbilityScalingManager] Failed to initialize: " + ex);
			}
			finally
			{
				isInitialized = true;
			}
		}
	}

	private static string FindEquipXmlPath()
	{
		string[] array = new string[]
		{
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExaltHelper_Data", "Objects.xml"),
			ResourceHelper.ObjectsXmlPath,
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "xml", "equip.xml"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sniffer", "assets", "xml", "equip.xml"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "assets", "xml", "equip.xml"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Sniffer", "assets", "xml", "equip.xml"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "equip.xml")
		};
		foreach (string text in array)
		{
			if (File.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	private static void ParseEquipXml(string filePath)
	{
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(filePath);
		XmlNodeList elementsByTagName = xmlDocument.GetElementsByTagName("Object");
		foreach (XmlNode item in elementsByTagName)
		{
			if (item.NodeType != XmlNodeType.Element)
			{
				continue;
			}
			XmlElement xmlElement = (XmlElement)item;
			int num = ParseWeaponId(xmlElement);
			if (num == -1)
			{
				continue;
			}
			foreach (XmlNode childNode in xmlElement.ChildNodes)
			{
				if (childNode.NodeType != XmlNodeType.Element)
				{
					continue;
				}
				string name = childNode.Name;
				if (name == "OnPlayerShootActivate" || name == "Activate" || name == "OnPlayerAbilityActivate")
				{
					XmlElement xmlElement2 = (XmlElement)childNode;
					string attribute = xmlElement2.GetAttribute("type");
					if (!string.IsNullOrEmpty(attribute))
					{
						int num2 = ParseHex(attribute);
						if (num2 > 0)
						{
							projectileToWeaponMap[num2] = num;
						}
					}
					else if (!string.IsNullOrEmpty(xmlElement2.InnerText))
					{
						int num3 = ParseHex(xmlElement2.InnerText);
						if (num3 > 0)
						{
							projectileToWeaponMap[num3] = num;
						}
					}
				}
			}
			ParseActivateElements(xmlElement, num, "Activate");
			if (!scalingData.ContainsKey(num))
			{
				ParseActivateElements(xmlElement, num, "OnConditionEndActivate");
			}
		}
	}

	private static int ParseWeaponId(XmlElement objectElement)
	{
		string attribute = objectElement.GetAttribute("type");
		if (string.IsNullOrEmpty(attribute))
		{
			return -1;
		}
		return ParseHex(attribute);
	}

	private static int ParseHex(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return -1;
		}
		string text = value.Trim();
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
			return -1;
		}
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
		{
			return result2;
		}
		int num = text.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
		if (num >= 0)
		{
			int i;
			for (i = num + 2; i < text.Length && Uri.IsHexDigit(text[i]); i++)
			{
			}
			if (i > num + 2 && int.TryParse(text.Substring(num + 2, i - (num + 2)), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result3))
			{
				return result3;
			}
		}
		return -1;
	}

	private static void ParseActivateElements(XmlElement objectElement, int weaponId, string tagName)
	{
		XmlNodeList elementsByTagName = objectElement.GetElementsByTagName(tagName);
		for (int i = 0; i < elementsByTagName.Count; i++)
		{
			if (elementsByTagName[i] is XmlElement activateElement)
			{
				AbilityScalingData abilityScalingData = ParseScalingData(activateElement, weaponId);
				if (abilityScalingData != null)
				{
					scalingData[weaponId] = abilityScalingData;
					break;
				}
			}
		}
	}

	private static AbilityScalingData ParseScalingData(XmlElement activateElement, int weaponId)
	{
		string attribute = activateElement.GetAttribute("scalingStat");
		if (string.IsNullOrEmpty(attribute))
		{
			return null;
		}
		StatType? statType = ParseStatType(attribute);
		if (!statType.HasValue)
		{
			return null;
		}
		if (activateElement.InnerText.Trim() == "LethalStrike")
		{
			return new AbilityScalingData(weaponId, statType, ParseScalingMin(activateElement),
				ReadFloat(activateElement, "statModFlat"), 1)
			{
				IsLethalStrike = true,
				BaseFlat = ReadFloat(activateElement, "ignoreFlat"),
				BaseDefenseFraction = ReadFloat(activateElement, "ignorePerc"),
				DefenseFractionPerStat = ReadFloat(activateElement, "statModPerc")
			};
		}
		float num = CalculateDamagePerStat(activateElement);
		if (num <= 0f)
		{
			return null;
		}
		int scalingMin = ParseScalingMin(activateElement);
		int numShots = ParseNumShots(activateElement);
		return new AbilityScalingData(weaponId, statType, scalingMin, num, numShots);
	}

	private static float ReadFloat(XmlElement element, string attribute)
	{
		return float.TryParse(element.GetAttribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture,
			out float value) ? value : 0f;
	}

	public static int CalculateProjectileDamage(int baseDamage, int projectileType, int? statSnapshot, MapObject player, int targetDefense)
	{
		var data = GetScalingData(projectileType);
		if (data == null || player == null) return baseDamage;
		if (!data.IsLethalStrike) return baseDamage + CalculateStatBonus(projectileType, statSnapshot, player);
		// The observed server proc value is only the base projectile damage (6 at
		// both VIT 94 and 102). Lethal Strike adds a flat bonus plus a fraction of
		// the actual target's defense. Preserve the supplied base and its bonuses.
		int stat = statSnapshot ?? GetPlayerStatValue(player, data.ScalingStat.Value);
		float extra = Math.Max(0, stat - data.ScalingMin) * GetStatDamageMultiplier(player, projectileType);
		return baseDamage + (int)(data.BaseFlat + extra * data.DamagePerStat +
			(data.BaseDefenseFraction + extra * data.DefenseFractionPerStat) * Math.Max(0, targetDefense));
	}

	private static float CalculateDamagePerStat(XmlElement activateElement)
	{
		string attribute = activateElement.GetAttribute("statModDamage");
		if (!string.IsNullOrEmpty(attribute) && float.TryParse(attribute, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 0f;
	}

	private static StatType? ParseStatType(string statName)
	{
		if (string.IsNullOrEmpty(statName))
		{
			return null;
		}
		return statName.ToUpperInvariant() switch
		{
			"WIS" or "WISDOM" => StatType.WISDOM_STAT,
			"DEX" or "DEXTERITY" => StatType.DEXTERITY_STAT,
			"ATT" or "ATTACK" => StatType.ATTACK_STAT,
			"DEF" or "DEFENSE" => StatType.DEFENSE_STAT,
			"SPD" or "SPEED" => StatType.SPEED_STAT,
			"VIT" or "VITALITY" => StatType.VITALITY_STAT,
			"HP" or "HEALTH" or "LIFE" => StatType.MAX_HP_STAT,
			"MP" or "MANA" => StatType.MAX_MP_STAT,
			_ => null,
		};
	}

	private static int ParseScalingMin(XmlElement activateElement)
	{
		string attribute = activateElement.GetAttribute("statModScalingMin");
		if (!string.IsNullOrEmpty(attribute) && int.TryParse(attribute, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 50;
	}

	private static int ParseNumShots(XmlElement activateElement)
	{
		string attribute = activateElement.GetAttribute("numShots");
		if (!string.IsNullOrEmpty(attribute) && int.TryParse(attribute, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 1;
	}

	public static AbilityScalingData GetScalingData(int weaponId)
	{
		EnsureLoaded();
		if (scalingData.TryGetValue(weaponId, out var value))
		{
			return value;
		}
		if (projectileToWeaponMap.TryGetValue(weaponId, out var value2) && scalingData.TryGetValue(value2, out var value3))
		{
			return value3;
		}
		return null;
	}

	public static bool HasScaling(int weaponId)
	{
		AbilityScalingData abilityScalingData = GetScalingData(weaponId);
		return abilityScalingData != null && abilityScalingData.HasScaling();
	}

	public static bool IsLethalStrikeProjectile(int projectileType)
	{
		EnsureLoaded();
		return projectileToWeaponMap.TryGetValue(projectileType, out int abilityType) &&
			scalingData.TryGetValue(abilityType, out var data) && data.IsLethalStrike;
	}

	public static int GetPlayerStatValue(MapObject player, StatType statType)
	{
		if (player == null)
		{
			return 0;
		}
		return statType switch
		{
			StatType.WISDOM_STAT => player.Wisdom,
			StatType.DEXTERITY_STAT => player.Dexterity,
			StatType.ATTACK_STAT => player.Attack,
			StatType.DEFENSE_STAT => player.Defense,
			StatType.SPEED_STAT => player.Speed,
			StatType.VITALITY_STAT => player.Vitality,
			StatType.MAX_HP_STAT => player.MaxHp,
			StatType.MAX_MP_STAT => player.MaxMp,
			_ => 0,
		};
	}

	public static float GetStatDamageMultiplier(MapObject player, int weaponId)
	{
		if (player?.Inventory == null || player.Inventory.Length < 2)
		{
			return 1f;
		}
		int num = player.Inventory[1];
		bool flag = num == weaponId;
		if (!flag && projectileToWeaponMap.TryGetValue(weaponId, out var value))
		{
			flag = value == num;
		}
		if (!flag || string.IsNullOrEmpty(player.ItemDataString))
		{
			return 1f;
		}
		string[] array = player.ItemDataString.Split(',');
		if (array.Length > 1 && !string.IsNullOrEmpty(array[1]))
		{
			return EnchantmentParser.GetStatDamageMultiplier(array[1]);
		}
		return 1f;
	}

	public static int CalculateStatBonus(int weaponId, int? statSnapshot, MapObject player)
	{
		AbilityScalingData abilityScalingData = GetScalingData(weaponId);
		if (abilityScalingData == null || abilityScalingData.IsLethalStrike || !abilityScalingData.HasScaling() || player == null)
		{
			return 0;
		}
		int num = (statSnapshot.HasValue ? statSnapshot.Value : GetPlayerStatValue(player, abilityScalingData.ScalingStat.Value));
		if (num <= abilityScalingData.ScalingMin)
		{
			return 0;
		}
		int num2 = num - abilityScalingData.ScalingMin;
		float num3 = (float)num2 * abilityScalingData.DamagePerStat;
		float statDamageMultiplier = GetStatDamageMultiplier(player, weaponId);
		return (int)(num3 * statDamageMultiplier);
	}
}
