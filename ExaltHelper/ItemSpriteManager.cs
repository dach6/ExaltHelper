using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ExaltHelper;

internal static class ItemSpriteManager
{
	public class ProjectileInfo
	{
		public int Id;

		public int MinDamage;

		public int MaxDamage;

		public float RateOfFire = 1f;

		public int NumProjectiles = 1;

		public bool ArmorPiercing;
	}

	public class EquipXmlMeta
	{
		public float RateOfFire = -1f;

		public int NumProjectiles = 0;

		public Dictionary<int, (int min, int max, bool ap)> Projectiles = new Dictionary<int, (int, int, bool)>();

		public List<ProjectileInfo> Bullets = new List<ProjectileInfo>();
	}

	public class ItemMeta
	{
		public int ItemId;

		public string Name;

		public string SheetName;

		public int SheetIndex;

		public string Rarity = "Common";

		public Color RarityColor = ColorCommon;

		public Color GlowColor = Color.FromArgb(15, 175, 180, 195);

		public bool IsShiny;

		public int SlotType;

		public int MinDamage;

		public int MaxDamage;

		public int NumProjectiles = 1;

		public float RateOfFire = 1f;

		public bool IsWeapon;

		public bool ArmorPiercing;

		public List<ProjectileInfo> Projectiles = new List<ProjectileInfo>();

		public Rectangle SpriteRect;

		public bool HasSpriteRect;
	}

	private static readonly object syncLock;

	private static volatile bool isInitialized;

	private static readonly Dictionary<int, ItemMeta> itemDb;

	private static readonly Dictionary<string, Dictionary<int, Rectangle>> spriteSheetCoords;

	private static readonly ConcurrentDictionary<int, Bitmap> spriteCache;

	private static Bitmap mapObjectsAtlas;

	private static Bitmap charactersAtlas;

	public static readonly Color ColorDivine;

	public static readonly Color ColorLegendary;

	public static readonly Color ColorRare;

	public static readonly Color ColorUncommon;

	public static readonly Color ColorCommon;

	static ItemSpriteManager()
	{
		syncLock = new object();
		isInitialized = false;
		itemDb = new Dictionary<int, ItemMeta>();
		spriteSheetCoords = new Dictionary<string, Dictionary<int, Rectangle>>(StringComparer.OrdinalIgnoreCase);
		spriteCache = new ConcurrentDictionary<int, Bitmap>();
		mapObjectsAtlas = null;
		charactersAtlas = null;
		ColorDivine = Color.FromArgb(255, 215, 0);
		ColorLegendary = Color.FromArgb(200, 0, 255);
		ColorRare = Color.FromArgb(0, 200, 255);
		ColorUncommon = Color.FromArgb(0, 255, 0);
		ColorCommon = Color.FromArgb(175, 180, 195);
		Task.Run((Action)InitializeAssets);
	}

	public static void InitializeAssets()
	{
		lock (syncLock)
		{
			if (isInitialized)
			{
				return;
			}
			try
			{
				// Names come from the embedded object list even if sprites are unavailable.
				string text = FindSnifferAssetsDirectory() ?? Path.Combine(ApplicationDirectory, "assets");
				string text2 = Path.Combine(text, "ObjectID.list");
				string text3 = Path.Combine(text, "flatbuffer", "spritesheetf");
				string path = Path.Combine(text, "sprites", "mapObjects.png");
				string text4 = Path.Combine(text, "xml", "forgeProperties.xml");
				string text5 = Path.Combine(ApplicationDirectory, "ExaltHelper_Data", "Objects.xml");
				if (!File.Exists(text5)) text5 = Path.Combine(text, "xml", "equip.xml");
				Dictionary<int, string> forgeRarities = new Dictionary<int, string>();
				if (File.Exists(text4))
				{
					LoadForgeProperties(text4, forgeRarities);
				}
				EnchantmentParser.Initialize(text);
				ExaltHelper.Proxy.Helpers.AbilityScalingManager.EnsureLoaded();
				Dictionary<int, EquipXmlMeta> equipXmlMap = new Dictionary<int, EquipXmlMeta>();
				if (File.Exists(text5))
				{
					LoadEquipXml(text5, equipXmlMap);
				}
				LoadObjectList(text2, forgeRarities, equipXmlMap);
				if (File.Exists(text3))
				{
					LoadFlatBufferCoordinates(text3);
				}
				if (File.Exists(path))
				{
					using MemoryStream stream = new MemoryStream(File.ReadAllBytes(path));
					mapObjectsAtlas = new Bitmap(Image.FromStream(stream));
				}
				string path2 = Path.Combine(text, "sprites", "characters.png");
				if (File.Exists(path2))
				{
					using MemoryStream stream2 = new MemoryStream(File.ReadAllBytes(path2));
					charactersAtlas = new Bitmap(Image.FromStream(stream2));
				}
				foreach (ItemMeta value3 in itemDb.Values)
				{
					if (!string.IsNullOrEmpty(value3.SheetName) && spriteSheetCoords.TryGetValue(value3.SheetName, out var value) && value.TryGetValue(value3.SheetIndex, out var value2))
					{
						value3.SpriteRect = value2;
						value3.HasSpriteRect = true;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[ItemSpriteManager] Initialization error: " + ex);
			}
			finally
			{
				isInitialized = true;
			}
		}
	}

	private static string ApplicationDirectory => Path.GetDirectoryName(typeof(ItemSpriteManager).Assembly.Location);

	private static string FindSnifferAssetsDirectory()
	{
		string[] array = new string[]
		{
			Path.Combine(ApplicationDirectory, "assets"),
			Path.Combine(ApplicationDirectory, "Sniffer", "assets"),
			Path.Combine(ApplicationDirectory, "..", "Sniffer", "assets")
		};
		foreach (string text in array)
		{
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	private static void LoadForgeProperties(string filePath, Dictionary<int, string> forgeRarities)
	{
		try
		{
			using XmlReader xmlReader = XmlReader.Create(filePath);
			while (xmlReader.Read())
			{
				if (xmlReader.NodeType != XmlNodeType.Element || !(xmlReader.Name == "ForgeProperties"))
				{
					continue;
				}
				string attribute = xmlReader.GetAttribute("type");
				if (string.IsNullOrEmpty(attribute))
				{
					continue;
				}
				int result;
				if (attribute.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				{
					int.TryParse(attribute.Substring(2), NumberStyles.HexNumber, null, out result);
				}
				else
				{
					int.TryParse(attribute, out result);
				}
				using XmlReader xmlReader2 = xmlReader.ReadSubtree();
				while (xmlReader2.Read())
				{
					if (xmlReader2.NodeType == XmlNodeType.Element && xmlReader2.Name == "DismantleRequirements")
					{
						int.TryParse(xmlReader2.GetAttribute("mythical") ?? "0", out var result2);
						int.TryParse(xmlReader2.GetAttribute("legendary") ?? "0", out var result3);
						int.TryParse(xmlReader2.GetAttribute("rare") ?? "0", out var result4);
						int.TryParse(xmlReader2.GetAttribute("common") ?? "0", out var result5);
						if (result2 > 0)
						{
							forgeRarities[result] = "Divine";
						}
						else if (result3 > 0)
						{
							forgeRarities[result] = "Legendary";
						}
						else if (result4 > 0)
						{
							forgeRarities[result] = "Rare";
						}
						else if (result5 > 0)
						{
							forgeRarities[result] = "Uncommon";
						}
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[ItemSpriteManager] Error reading forgeProperties.xml: " + ex);
		}
	}

	private static void LoadEquipXml(string filePath, Dictionary<int, EquipXmlMeta> equipXmlMap)
	{
		try
		{
			using XmlReader xmlReader = XmlReader.Create(filePath);
			while (xmlReader.Read())
			{
				if (xmlReader.NodeType != XmlNodeType.Element || !(xmlReader.Name == "Object"))
				{
					continue;
				}
				string attribute = xmlReader.GetAttribute("type");
				if (string.IsNullOrEmpty(attribute))
				{
					continue;
				}
				int result;
				if (attribute.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				{
					int.TryParse(attribute.Substring(2), NumberStyles.HexNumber, null, out result);
				}
				else
				{
					int.TryParse(attribute, out result);
				}
				float result2 = -1f;
				int result3 = 0;
				Dictionary<int, (int min, int max, bool ap)> projectiles = new Dictionary<int, (int, int, bool)>();
				List<ProjectileInfo> bullets = new List<ProjectileInfo>();
				using (XmlReader xmlReader2 = xmlReader.ReadSubtree())
				{
					while (xmlReader2.Read())
					{
						if (xmlReader2.NodeType != XmlNodeType.Element)
						{
							continue;
						}
						if (xmlReader2.Name == "RateOfFire")
						{
							float.TryParse(xmlReader2.ReadElementContentAsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result2);
						}
						else if (xmlReader2.Name == "NumProjectiles")
						{
							int.TryParse(xmlReader2.ReadElementContentAsString(), out result3);
						}
						else if (xmlReader2.Name == "Subattack")
						{
							string attribute2 = xmlReader2.GetAttribute("projectileId");
							int result4 = 0;
							if (!string.IsNullOrEmpty(attribute2))
							{
								int.TryParse(attribute2, out result4);
							}
							float result5 = -1f;
							int result6 = 1;
							using (XmlReader xmlReader3 = xmlReader2.ReadSubtree())
							{
								while (xmlReader3.Read())
								{
									if (xmlReader3.NodeType == XmlNodeType.Element)
									{
										if (xmlReader3.Name == "projectileId")
										{
											int.TryParse(xmlReader3.ReadElementContentAsString(), out result4);
										}
										else if (xmlReader3.Name == "RateOfFire")
										{
											float.TryParse(xmlReader3.ReadElementContentAsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result5);
										}
										else if (xmlReader3.Name == "NumProjectiles")
										{
											int.TryParse(xmlReader3.ReadElementContentAsString(), out result6);
										}
									}
								}
							}
							bullets.Add(new ProjectileInfo
							{
								Id = result4,
								RateOfFire = result5,
								NumProjectiles = ((result6 > 0) ? result6 : 1)
							});
						}
						else
						{
							if (!(xmlReader2.Name == "Projectile"))
							{
								continue;
							}
							string attribute3 = xmlReader2.GetAttribute("id");
							int result7 = 0;
							if (!string.IsNullOrEmpty(attribute3))
							{
								int.TryParse(attribute3, out result7);
							}
							int result8 = 0;
							int result9 = 0;
							bool armorPiercing = false;
							using (XmlReader xmlReader4 = xmlReader2.ReadSubtree())
							{
								while (xmlReader4.Read())
								{
									if (xmlReader4.NodeType == XmlNodeType.Element)
									{
										if (xmlReader4.Name == "id")
										{
											int.TryParse(xmlReader4.ReadElementContentAsString(), out result7);
										}
										else if (xmlReader4.Name == "MinDamage")
										{
											int.TryParse(xmlReader4.ReadElementContentAsString(), out result8);
										}
										else if (xmlReader4.Name == "MaxDamage")
										{
											int.TryParse(xmlReader4.ReadElementContentAsString(), out result9);
										}
										else if (xmlReader4.Name == "ArmorPiercing")
										{
											armorPiercing = true;
										}
									}
								}
							}
							projectiles[result7] = (result8, result9, armorPiercing);
						}
					}
				}
				if (result2 != -1f && bullets.Count == 0)
				{
					bullets.Add(new ProjectileInfo
					{
						Id = 0,
						NumProjectiles = ((result3 < 1) ? 1 : result3),
						RateOfFire = result2
					});
				}
				else if (result2 != -1f)
				{
					foreach (ProjectileInfo item in bullets)
					{
						if (item.RateOfFire < 0f)
						{
							item.RateOfFire = result2;
						}
					}
				}
				else if (result2 == -1f && bullets.Count == 0 && projectiles.Count > 0)
				{
					foreach (int key in projectiles.Keys)
					{
						bullets.Add(new ProjectileInfo
						{
							Id = key,
							NumProjectiles = 1,
							RateOfFire = 1f
						});
					}
				}
				foreach (ProjectileInfo item2 in bullets)
				{
					if (item2.RateOfFire < 0f)
					{
						item2.RateOfFire = 1f;
					}
					if (item2.NumProjectiles < 1)
					{
						item2.NumProjectiles = 1;
					}
					if (projectiles.TryGetValue(item2.Id, out var value))
					{
						item2.MinDamage = value.min;
						item2.MaxDamage = value.max;
						item2.ArmorPiercing = value.ap;
					}
				}
				if (bullets.Count > 0 || result2 > 0f || projectiles.Count > 0)
				{
					equipXmlMap[result] = new EquipXmlMeta
					{
						RateOfFire = ((result2 > 0f) ? result2 : 1f),
						NumProjectiles = ((result3 > 0) ? result3 : 1),
						Projectiles = projectiles,
						Bullets = bullets
					};
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[ItemSpriteManager] Error reading equip.xml: " + ex);
		}
	}

	private static void LoadObjectList(string filePath, Dictionary<int, string> forgeRarities, Dictionary<int, EquipXmlMeta> equipXmlMap)
	{
		try
		{
			using Stream data = File.Exists(filePath) ? File.OpenRead(filePath) :
				typeof(ItemSpriteManager).Assembly.GetManifestResourceStream("ObjectID.list");
			if (data == null) return;
			using StreamReader streamReader = new StreamReader(data);
			string text;
			while ((text = streamReader.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				string[] array = text.Split(';');
				if (array.Length < 7 || !int.TryParse(array[0], out var result))
				{
					continue;
				}
				string text2 = array[5];
				string tags = array[6];
				string text3 = ((!string.IsNullOrWhiteSpace(array[1])) ? array[1] : ((array.Length > 7) ? array[7] : ""));
				string sheetName = "";
				int result2 = 0;
				if (!string.IsNullOrEmpty(text2) && text2.Contains(","))
				{
					string[] array2 = text2.Split(',');
					int.TryParse(array2[0], out result2);
					if (array2.Length > 1)
					{
						sheetName = array2[1];
					}
				}
				int result3 = 0;
				int result4 = 0;
				int result5 = 0;
				bool armorPiercing = false;
				List<ProjectileInfo> list = new List<ProjectileInfo>();
				if (array.Length > 4 && !string.IsNullOrEmpty(array[4]))
				{
					string[] array3 = array[4].Split(',');
					if (array3.Length >= 4)
					{
						int.TryParse(array3[0], out result3);
						int num = array3.Length - 1;
						int num2 = 0;
						for (int i = 0; i < num - 2; i += 3)
						{
							int.TryParse(array3[1 + i], out var result6);
							int.TryParse(array3[2 + i], out var result7);
							bool armorPiercing2 = array3[3 + i] == "1";
							list.Add(new ProjectileInfo
							{
								Id = num2,
								MinDamage = result6,
								MaxDamage = ((result7 > result6) ? result7 : result6),
								ArmorPiercing = armorPiercing2
							});
							num2++;
						}
						if (list.Count > 0)
						{
							result4 = list[0].MinDamage;
							result5 = list[0].MaxDamage;
							armorPiercing = list[0].ArmorPiercing;
						}
					}
					else if (array3.Length >= 3)
					{
						int.TryParse(array3[0], out result3);
						int.TryParse(array3[1], out result4);
						int.TryParse(array3[2], out result5);
					}
				}
				List<ProjectileInfo> finalBullets = new List<ProjectileInfo>();
				float rateOfFire = 1f;
				int numProjectiles = 1;
				if (equipXmlMap != null && equipXmlMap.TryGetValue(result, out var value2))
				{
					if (value2.Bullets.Count > 0)
					{
						finalBullets = value2.Bullets;
						rateOfFire = value2.RateOfFire;
						numProjectiles = 0;
						foreach (ProjectileInfo item3 in finalBullets)
						{
							numProjectiles += item3.NumProjectiles;
						}
						result4 = finalBullets[0].MinDamage;
						result5 = finalBullets[0].MaxDamage;
						armorPiercing = finalBullets.Exists((ProjectileInfo b) => b.ArmorPiercing);
					}
					else if (list.Count > 0)
					{
						finalBullets = list;
						rateOfFire = ((value2.RateOfFire > 0f) ? value2.RateOfFire : 1f);
						foreach (ProjectileInfo item4 in finalBullets)
						{
							item4.RateOfFire = rateOfFire;
						}
						result4 = list[0].MinDamage;
						result5 = list[0].MaxDamage;
						armorPiercing = list[0].ArmorPiercing;
						numProjectiles = list.Count;
					}
				}
				else if (list.Count > 0)
				{
					finalBullets = list;
					result4 = list[0].MinDamage;
					result5 = list[0].MaxDamage;
					armorPiercing = list[0].ArmorPiercing;
					numProjectiles = list.Count;
				}
				bool flag = result4 > 0 && result5 > 0;
				if (!flag)
				{
					bool flag2;
					switch (result3)
					{
					case 1:
					case 2:
					case 3:
					case 8:
					case 17:
					case 24:
					case 28:
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					flag = flag2;
				}
				bool isWeapon = flag;
				string altName = (array.Length > 7) ? array[7] : "";
				var (rarity, rarityColor, glowColor, isShiny) = DetermineRarity(result, tags, text3, sheetName, altName, forgeRarities);
				if (isShiny && !text3.EndsWith("Shiny") && !text3.EndsWith("(Shiny)"))
				{
					text3 = text3 + " (Shiny)";
				}
				itemDb[result] = new ItemMeta
				{
					ItemId = result,
					Name = ((!string.IsNullOrEmpty(text3)) ? text3 : ("Item #" + result)),
					SheetName = sheetName,
					SheetIndex = result2,
					Rarity = rarity,
					RarityColor = rarityColor,
					GlowColor = glowColor,
					IsShiny = isShiny,
					SlotType = result3,
					MinDamage = result4,
					MaxDamage = result5,
					NumProjectiles = numProjectiles,
					RateOfFire = rateOfFire,
					IsWeapon = isWeapon,
					ArmorPiercing = armorPiercing,
					Projectiles = finalBullets
				};
			}
		}
		catch (Exception ex2)
		{
			Console.WriteLine("[ItemSpriteManager] Error reading ObjectID.list: " + ex2);
		}
	}

	private static (string rarity, Color border, Color glow, bool isShiny) DetermineRarity(int id, string tags, string name, string sheetName, string altName, Dictionary<int, string> forgeRarities)
	{
		if (string.IsNullOrEmpty(tags))
		{
			tags = "";
		}
		bool isShiny = tags.IndexOf("SHINY", StringComparison.OrdinalIgnoreCase) >= 0
			|| (!string.IsNullOrEmpty(sheetName) && sheetName.IndexOf("Shiny", StringComparison.OrdinalIgnoreCase) >= 0)
			|| (!string.IsNullOrEmpty(name) && name.IndexOf("Shiny", StringComparison.OrdinalIgnoreCase) >= 0)
			|| (!string.IsNullOrEmpty(altName) && altName.IndexOf("Shiny", StringComparison.OrdinalIgnoreCase) >= 0);

		if (forgeRarities != null && forgeRarities.TryGetValue(id, out var forgeRarity) && !string.IsNullOrEmpty(forgeRarity))
		{
			Color border = EnchantmentParser.GetRarityColor(forgeRarity);
			Color glow = Color.FromArgb(30, border);
			return (rarity: forgeRarity, border: border, glow: glow, isShiny: isShiny);
		}
		if (tags.Contains("POWERTIER_SS") || tags.Contains("REALMWHITE"))
		{
			return (rarity: "Divine", border: ColorDivine, glow: Color.FromArgb(35, 255, 215, 0), isShiny: isShiny);
		}
		if (tags.Contains("POWERTIER_S"))
		{
			return (rarity: "Legendary", border: ColorLegendary, glow: Color.FromArgb(30, 200, 0, 255), isShiny: isShiny);
		}
		if (tags.Contains("POWERTIER_A"))
		{
			return (rarity: "Rare", border: ColorRare, glow: Color.FromArgb(25, 0, 200, 255), isShiny: isShiny);
		}
		if (tags.Contains("POWERTIER_B"))
		{
			return (rarity: "Uncommon", border: ColorUncommon, glow: Color.FromArgb(20, 0, 255, 0), isShiny: isShiny);
		}
		if (tags.Contains("UT") || tags.Contains("ST"))
		{
			return (rarity: "Rare", border: ColorRare, glow: Color.FromArgb(25, 0, 200, 255), isShiny: isShiny);
		}
		return (rarity: "Common", border: ColorCommon, glow: Color.FromArgb(15, 175, 180, 195), isShiny: isShiny);
	}

	private static void LoadFlatBufferCoordinates(string filePath)
	{
		try
		{
			byte[] array = File.ReadAllBytes(filePath);
			if (array.Length < 32)
			{
				return;
			}
			int num = BitConverter.ToInt32(array, 0);
			int num2 = BitConverter.ToInt32(array, num);
			int num3 = num - num2;
			short num4 = BitConverter.ToInt16(array, num3 + 4);
			int num5 = num + num4 + BitConverter.ToInt32(array, num + num4);
			int num6 = BitConverter.ToInt32(array, num5);
			for (int i = 0; i < num6; i++)
			{
				int num7 = num5 + 4 + i * 4 + BitConverter.ToInt32(array, num5 + 4 + i * 4);
				int num8 = BitConverter.ToInt32(array, num7);
				int num9 = num7 - num8;
				short num10 = BitConverter.ToInt16(array, num9 + 4);
				if (num10 <= 0)
				{
					continue;
				}
				int num11 = num7 + num10 + BitConverter.ToInt32(array, num7 + num10);
				int num12 = BitConverter.ToInt32(array, num11);
				if (num12 <= 0 || num12 > 100)
				{
					continue;
				}
				string key = Encoding.UTF8.GetString(array, num11 + 4, num12);
				short num13 = BitConverter.ToInt16(array, num9 + 8);
				if (num13 <= 0)
				{
					continue;
				}
				int num14 = num7 + num13 + BitConverter.ToInt32(array, num7 + num13);
				int num15 = BitConverter.ToInt32(array, num14);
				Dictionary<int, Rectangle> dictionary = new Dictionary<int, Rectangle>();
				for (int j = 0; j < num15; j++)
				{
					int num16 = num14 + 4 + j * 4 + BitConverter.ToInt32(array, num14 + 4 + j * 4);
					int num17 = BitConverter.ToInt32(array, num16);
					int num18 = num16 - num17;
					short num19 = BitConverter.ToInt16(array, num18 + 10);
					short num20 = BitConverter.ToInt16(array, num18 + 4);
					if (num20 > 0)
					{
						int key2 = ((num19 > 0) ? BitConverter.ToInt32(array, num16 + num19) : 0);
						int num21 = num16 + num20;
						float num22 = BitConverter.ToSingle(array, num21);
						float num23 = BitConverter.ToSingle(array, num21 + 4);
						float num24 = BitConverter.ToSingle(array, num21 + 8);
						float num25 = BitConverter.ToSingle(array, num21 + 12);
						if (num24 > 0f && num25 > 0f)
						{
							dictionary[key2] = new Rectangle((int)num22, (int)num23, (int)num24, (int)num25);
						}
					}
				}
				spriteSheetCoords[key] = dictionary;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[ItemSpriteManager] Error reading spritesheetf: " + ex);
		}
	}

	public static ItemMeta GetItemMetadata(int itemId)
	{
		if (!isInitialized) InitializeAssets();
		if (itemDb.TryGetValue(itemId, out var value))
		{
			return value;
		}
		return new ItemMeta
		{
			ItemId = itemId,
			Name = ((itemId > 0) ? ("Item #" + itemId) : "-"),
			Rarity = "Common",
			RarityColor = ColorCommon,
			GlowColor = Color.Transparent
		};
	}

	public static Bitmap GetItemSprite(int itemId)
	{
		if (!isInitialized) InitializeAssets();
		if (itemId <= 0)
		{
			return null;
		}
		if (spriteCache.TryGetValue(itemId, out var value))
		{
			return value;
		}
		if (itemDb.TryGetValue(itemId, out var value2) && value2.HasSpriteRect)
		{
			Rectangle spriteRect = value2.SpriteRect;
			if (mapObjectsAtlas != null && spriteRect.X >= 0 && spriteRect.Y >= 0 && spriteRect.Right <= mapObjectsAtlas.Width && spriteRect.Bottom <= mapObjectsAtlas.Height)
			{
				try
				{
					lock (mapObjectsAtlas)
					{
						Bitmap bitmap = mapObjectsAtlas.Clone(spriteRect, mapObjectsAtlas.PixelFormat);
						spriteCache[itemId] = bitmap;
						return bitmap;
					}
				}
				catch
				{
				}
			}
			if (charactersAtlas != null && spriteRect.X >= 0 && spriteRect.Y >= 0 && spriteRect.Right <= charactersAtlas.Width && spriteRect.Bottom <= charactersAtlas.Height)
			{
				try
				{
					lock (charactersAtlas)
					{
						Bitmap bitmap2 = charactersAtlas.Clone(spriteRect, charactersAtlas.PixelFormat);
						spriteCache[itemId] = bitmap2;
						return bitmap2;
					}
				}
				catch
				{
				}
			}
		}
		return null;
	}

	public static void DrawEquipmentSlot(Graphics g, int x, int y, int width, int height, int itemId, int slotIndex, string rarityOverride = null, int enchantCount = -1, bool isShiny = false)
	{
		ItemMeta itemMetadata = GetItemMetadata(itemId);
		bool flag = itemId > 0;
		bool itemIsShiny = isShiny || (flag && itemMetadata.IsShiny) || (rarityOverride != null && rarityOverride.Equals("Shiny", StringComparison.OrdinalIgnoreCase));
		if (enchantCount < 0)
		{
			enchantCount = ((!string.IsNullOrEmpty(rarityOverride) && !rarityOverride.Equals("Common", StringComparison.OrdinalIgnoreCase) && !rarityOverride.Equals("Shiny", StringComparison.OrdinalIgnoreCase)) ? (rarityOverride.ToLowerInvariant() switch
			{
				"divine" => 4, 
				"legendary" => 3, 
				"rare" => 2, 
				"uncommon" => 1, 
				_ => 0, 
			}) : 0);
		}
		Color color;
		if (enchantCount >= 4)
		{
			color = ColorDivine;
		}
		else if (enchantCount == 3)
		{
			color = ColorLegendary;
		}
		else if (enchantCount == 2)
		{
			color = ColorRare;
		}
		else if (enchantCount == 1)
		{
			color = ColorUncommon;
		}
		else if (!string.IsNullOrEmpty(rarityOverride) && !rarityOverride.Equals("Common", StringComparison.OrdinalIgnoreCase) && !rarityOverride.Equals("Shiny", StringComparison.OrdinalIgnoreCase))
		{
			color = EnchantmentParser.GetRarityColor(rarityOverride);
		}
		else
		{
			color = (flag ? itemMetadata.RarityColor : Color.FromArgb(50, 54, 66));
		}
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(245, 18, 20, 26)))
		{
			g.FillRectangle(brush, x, y, width, height);
		}
		if (flag && (enchantCount > 0 || color != ColorCommon))
		{
			int num = Math.Min(width, height) - 4;
			int x2 = x + (width - num) / 2;
			int y2 = y + (height - num) / 2;
			using GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(x2, y2, num, num);
			using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(70, color);
			pathGradientBrush.SurroundColors = new Color[1] { Color.Transparent };
			g.FillPath(pathGradientBrush, graphicsPath);
		}
		else if (flag)
		{
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(18, 175, 180, 195));
			g.FillRectangle(brush2, x + 1, y + 1, width - 2, height - 2);
		}
		using (Pen pen = new Pen(color, 1.4f))
		{
			g.DrawRectangle(pen, x, y, width, height);
		}
		if (!flag)
		{
			return;
		}
		Bitmap itemSprite = GetItemSprite(itemId);
		if (itemSprite != null)
		{
			InterpolationMode interpolationMode = g.InterpolationMode;
			PixelOffsetMode pixelOffsetMode = g.PixelOffsetMode;
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = PixelOffsetMode.Half;
			int num2 = Math.Min(width - 6, height - 6);
			int x3 = x + (width - num2) / 2;
			int y3 = y + (height - num2) / 2;
			g.DrawImage(itemSprite, new Rectangle(x3, y3, num2, num2));
			g.InterpolationMode = interpolationMode;
			g.PixelOffsetMode = pixelOffsetMode;
		}
		else
		{
			string slotGlyph = GetSlotGlyph(slotIndex);
			using Font font = new Font("Segoe UI Symbol", Math.Max(8f, (float)height * 0.35f), FontStyle.Bold);
			using SolidBrush brush3 = new SolidBrush(color);
			SizeF sizeF = g.MeasureString(slotGlyph, font);
			g.DrawString(slotGlyph, font, brush3, (float)x + ((float)width - sizeF.Width) / 2f, (float)y + ((float)height - sizeF.Height) / 2f);
		}
		if (itemIsShiny)
		{
			using Font starFont = new Font("Segoe UI", 6f, FontStyle.Bold);
			using SolidBrush starBrush = new SolidBrush(Color.FromArgb(255, 235, 90));
			g.DrawString("★", starFont, starBrush, (float)x + 1f, (float)y - 1f);
		}
		if (enchantCount <= 0)
		{
			return;
		}
		float num3 = Math.Max(2.5f, (float)height * 0.082f);
		Color fillColor = color;
		if (enchantCount >= 4)
		{
			float num4 = (float)(x + width) - num3 * 2.3f - 2f;
			float num5 = (float)(y + height) - num3 * 2.3f - 2f;
			float num6 = num3 * 1.1f;
			DrawDiamond(g, num4, num5 - num6, num3, fillColor);
			DrawDiamond(g, num4 - num6, num5, num3, fillColor);
			DrawDiamond(g, num4 + num6, num5, num3, fillColor);
			DrawDiamond(g, num4, num5 + num6, num3, fillColor);
			return;
		}
		switch (enchantCount)
		{
		case 3:
		{
			float cy3 = (float)(y + height) - num3 - 2.5f;
			float num9 = num3 * 1.85f;
			float num10 = (float)(x + width) - num3 - 2.5f;
			DrawDiamond(g, num10 - num9 * 2f, cy3, num3, fillColor);
			DrawDiamond(g, num10 - num9, cy3, num3, fillColor);
			DrawDiamond(g, num10, cy3, num3, fillColor);
			break;
		}
		case 2:
		{
			float cy2 = (float)(y + height) - num3 - 2.5f;
			float num7 = num3 * 1.85f;
			float num8 = (float)(x + width) - num3 - 2.5f;
			DrawDiamond(g, num8 - num7, cy2, num3, fillColor);
			DrawDiamond(g, num8, cy2, num3, fillColor);
			break;
		}
		case 1:
		{
			float cx = (float)(x + width) - num3 - 2.5f;
			float cy = (float)(y + height) - num3 - 2.5f;
			DrawDiamond(g, cx, cy, num3, fillColor);
			break;
		}
		}
	}

	private static void DrawDiamond(Graphics g, float cx, float cy, float radius, Color fillColor)
	{
		PointF[] points = new PointF[4]
		{
			new PointF(cx, cy - radius),
			new PointF(cx + radius, cy),
			new PointF(cx, cy + radius),
			new PointF(cx - radius, cy)
		};
		using (SolidBrush brush = new SolidBrush(fillColor))
		{
			g.FillPolygon(brush, points);
		}
		using (Pen pen = new Pen(Color.FromArgb(230, 15, 18, 24), 1f))
		{
			g.DrawPolygon(pen, points);
		}
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(180, Color.White));
		g.FillRectangle(brush2, cx - 0.5f, cy - 0.5f, 1f, 1f);
	}

	private static string GetSlotGlyph(int slotIndex)
	{
		return slotIndex switch
		{
			0 => "⚔", 
			1 => "⚡", 
			2 => "\ud83d\udee1", 
			3 => "\ud83d\udc8d", 
			_ => "✦", 
		};
	}
}
