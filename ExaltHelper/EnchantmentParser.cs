using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml;

namespace ExaltHelper;

internal static class EnchantmentParser
{
	private static readonly Dictionary<short, string> enchantNames = new Dictionary<short, string>();

	private static readonly Dictionary<short, float> statModMultipliers = new Dictionary<short, float>();

	private static volatile bool isInitialized;

	public static readonly Color ColorDivine = Color.FromArgb(255, 215, 0);

	public static readonly Color ColorLegendary = Color.FromArgb(200, 0, 255);

	public static readonly Color ColorRare = Color.FromArgb(0, 200, 255);

	public static readonly Color ColorUncommon = Color.FromArgb(0, 255, 0);

	public static readonly Color ColorCommon = Color.FromArgb(175, 180, 195);

	private sealed class ProjectileModifier
	{
		public int ProjectileId;
		public float Min = 1f;
		public float Max = 1f;
	}

	private static readonly Dictionary<short, List<ProjectileModifier>> projectileModifiers = new Dictionary<short, List<ProjectileModifier>>();
	private static readonly object initializationLock = new object();

	private static void EnsureLoaded()
	{
		if (isInitialized) return;
		string applicationDirectory = Path.GetDirectoryName(typeof(EnchantmentParser).Assembly.Location);
		foreach (string relative in new[] { "assets", Path.Combine("Sniffer", "assets"), Path.Combine("..", "Sniffer", "assets") })
		{
			string directory = Path.Combine(applicationDirectory, relative);
			if (!File.Exists(Path.Combine(directory, "xml", "enchantments.xml"))) continue;
			Initialize(directory);
			return;
		}
	}

	public static void Initialize(string snifferAssetsDir)
	{
		lock (initializationLock)
		{
			if (isInitialized || string.IsNullOrEmpty(snifferAssetsDir)) return;
			string path = Path.Combine(snifferAssetsDir, "xml", "enchantments.xml");
			if (!File.Exists(path)) return;
			try
			{
				var document = new XmlDocument { XmlResolver = null };
				document.Load(path);
				foreach (XmlElement enchant in document.SelectNodes("//Enchantment"))
				{
					string type = enchant.GetAttribute("type");
					short id;
					bool parsed = type.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
						? short.TryParse(type.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)
						: short.TryParse(type, out id);
					if (!parsed || id <= 0) continue;
					string name = enchant["DisplayId"]?.InnerText;
					if (!string.IsNullOrWhiteSpace(name)) enchantNames[id] = name;
					foreach (XmlElement effect in enchant.SelectNodes(".//ActivateOnEquip"))
						if (effect.InnerText.Trim() == "StatModMult" && TryReadMultiplier(effect.GetAttribute("amount"), out float amount))
							statModMultipliers[id] = amount;
					var modifiers = new List<ProjectileModifier>();
					foreach (XmlElement effect in enchant.SelectNodes("Mutators/MultiplyMinDamage | Mutators/MultiplyMaxDamage"))
					{
						if (!TryReadMultiplier(effect.InnerText, out float amount)) continue;
						int projectileId = -1;
						if (effect.HasAttribute("projectileId") && !int.TryParse(effect.GetAttribute("projectileId"), out projectileId)) continue;
						modifiers.Add(new ProjectileModifier {
							ProjectileId = projectileId,
							Min = effect.Name == "MultiplyMinDamage" ? amount : 1f,
							Max = effect.Name == "MultiplyMaxDamage" ? amount : 1f
						});
					}
					if (modifiers.Count > 0) projectileModifiers[id] = modifiers;
				}
				isInitialized = true;
			}
			catch (Exception ex)
			{
				Console.WriteLine("[EnchantmentParser] Could not load enchantments: " + ex.Message);
			}
		}
	}

	private static bool TryReadMultiplier(string text, out float amount)
	{
		return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)
			&& !float.IsNaN(amount) && !float.IsInfinity(amount) && amount >= 0f;
	}

	// Mutate the range before rolling this client-created projectile. Server damage
	// already includes its modifiers and must never pass through this method.
	public static void ApplyProjectileDamageModifiers(string code, int projectileId, ref int min, ref int max)
	{
		EnsureLoaded();
		byte[] bytes = SixBitStringToBytes(code);
		if (bytes.Length < 3 || (bytes[1] | (bytes[2] << 8)) != 1026) return;
		float minMultiplier = 1f, maxMultiplier = 1f;
		for (int offset = 3; offset + 1 < bytes.Length && offset < 11; offset += 2)
		{
			short id = (short)(bytes[offset] | (bytes[offset + 1] << 8));
			if (id == 253 || id == -3) break;
			if (!projectileModifiers.TryGetValue(id, out var modifiers)) continue;
			foreach (var modifier in modifiers)
			{
				if (modifier.ProjectileId != -1 && modifier.ProjectileId != projectileId) continue;
				minMultiplier *= modifier.Min;
				maxMultiplier *= modifier.Max;
			}
		}
		min = Math.Max(0, (int)(min * minMultiplier));
		max = Math.Max(min, (int)(max * maxMultiplier));
	}

	private static int CharValue(char c)
	{
		if (c >= '0' && c <= '9')
		{
			return c + 4;
		}
		if (c >= 'A' && c <= 'Z')
		{
			return c - 65;
		}
		if (c >= 'a' && c <= 'z')
		{
			return c - 71;
		}
		return c switch
		{
			'-' => 62, 
			'_' => 63, 
			'=' => 0, 
			_ => c, 
		};
	}

	public static byte[] SixBitStringToBytes(string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return new byte[0];
		}
		while (str.Length % 4 != 0)
		{
			str += "=";
		}
		int num = str.IndexOf('=');
		int length = str.Length;
		int num2 = ((num > -1) ? (length - num) : 0);
		int num3 = length / 4 * 3 - num2;
		if (num3 <= 0)
		{
			return new byte[0];
		}
		byte[] array = new byte[num3];
		int num4 = 0;
		for (int i = 0; i < length; i += 4)
		{
			if (num4 >= num3)
			{
				break;
			}
			int num5 = CharValue(str[i]);
			int num6 = ((i + 1 < length) ? CharValue(str[i + 1]) : 0);
			char c = ((i + 2 < length) ? str[i + 2] : '=');
			char c2 = ((i + 3 < length) ? str[i + 3] : '=');
			int num7 = CharValue(c);
			int num8 = CharValue(c2);
			array[num4] = (byte)((num5 << 2) | (num6 >> 4));
			if (c != '=' && num4 + 1 < num3)
			{
				array[num4 + 1] = (byte)(((num6 & 0xF) << 4) | (num7 >> 2));
				if (c2 != '=' && num4 + 2 < num3)
				{
					array[num4 + 2] = (byte)(((num7 & 3) << 6) | num8);
				}
			}
			num4 += 3;
		}
		return array;
	}

	public static (int count, string rarity, List<string> names) ParseSlotEnchants(string code)
	{
		EnsureLoaded();
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(code))
		{
			return (count: 0, rarity: "Common", names: list);
		}
		byte[] array = SixBitStringToBytes(code);
		if (array.Length < 3)
		{
			return (count: 0, rarity: "Common", names: list);
		}
		if ((short)(array[1] | (array[2] << 8)) != 1026)
		{
			return (count: 0, rarity: "Common", names: list);
		}
		int num = 0;
		int num2 = 3;
		while (num2 + 1 < array.Length && num2 < 11)
		{
			short num3 = (short)(array[num2] | (array[num2 + 1] << 8));
			num2 += 2;
			if (num3 == 253)
			{
				break;
			}
			if (num3 != 254 && num3 != -1 && num3 > 0)
			{
				num++;
				if (enchantNames.TryGetValue(num3, out var value))
				{
					list.Add(value);
				}
				else
				{
					list.Add($"Enchant #{num3}");
				}
			}
		}
		string item = "Common";
		if (num >= 4)
		{
			item = "Divine";
		}
		else
		{
			switch (num)
			{
			case 3:
				item = "Legendary";
				break;
			case 2:
				item = "Rare";
				break;
			case 1:
				item = "Uncommon";
				break;
			}
		}
		return (count: num, rarity: item, names: list);
	}

	public static Color GetRarityColor(string rarity)
	{
		if (string.IsNullOrEmpty(rarity))
		{
			return ColorCommon;
		}
		return rarity.ToLowerInvariant() switch
		{
			"shiny" => ColorDivine,
			"divine" => ColorDivine, 
			"legendary" => ColorLegendary, 
			"rare" => ColorRare, 
			"uncommon" => ColorUncommon, 
			_ => ColorCommon, 
		};
	}

	public static float GetStatDamageMultiplier(string code)
	{
		EnsureLoaded();
		if (string.IsNullOrEmpty(code))
		{
			return 1f;
		}
		byte[] array = SixBitStringToBytes(code);
		if (array.Length < 3)
		{
			return 1f;
		}
		if ((short)(array[1] | (array[2] << 8)) != 1026)
		{
			return 1f;
		}
		float num = 1f;
		int num2 = 3;
		while (num2 + 1 < array.Length && num2 < 11)
		{
			short num3 = (short)(array[num2] | (array[num2 + 1] << 8));
			num2 += 2;
			if (num3 == 253 || num3 == -3)
			{
				break;
			}
			if (num3 > 0 && statModMultipliers.TryGetValue(num3, out var value))
			{
				num *= value;
			}
		}
		return num;
	}
}
