using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ExaltHelper.Proxy.Helpers;

public static class RealmSharkData
{
	public class Projectile
	{
		public int Min;

		public int Max;

		public bool Ap;

		public int SlotType;

		public Projectile(int min, int max, bool ap, int slotType = 0)
		{
			Min = min;
			Max = max;
			Ap = ap;
			SlotType = slotType;
		}
	}

	private class ItemEntry
	{
		public int Id;

		public string Display;

		public string Clazz;

		public string Group;

		public string RawProjectiles;

		public string IdName;

		public int SlotType;

		public Projectile[] Projectiles;
	}

	private static readonly Dictionary<int, ItemEntry> items = new Dictionary<int, ItemEntry>();

	private static bool loaded = false;

	private static readonly object lockObj = new object();

	public static void EnsureLoaded()
	{
		if (loaded)
		{
			return;
		}
		lock (lockObj)
		{
			if (loaded)
			{
				return;
			}
			try
			{
				AbilityScalingManager.EnsureLoaded();
				Stream stream = FindDataStream("ObjectID.list");
				if (stream != null)
				{
					using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
					{
						string text;
						while ((text = streamReader.ReadLine()) != null)
						{
							if (!string.IsNullOrWhiteSpace(text))
							{
								string[] array = text.Split(';');
								if (array.Length >= 5 && int.TryParse(array[0], out var result))
								{
									string text2 = array[4];
									Projectile[] projectiles = ParseObjectListProjectiles(text2, out var slotType);
									items[result] = new ItemEntry
									{
										Id = result,
										Display = ((array.Length > 1) ? array[1] : ""),
										Clazz = ((array.Length > 2) ? array[2] : ""),
										Group = ((array.Length > 3) ? array[3] : ""),
										RawProjectiles = text2,
										IdName = ((array.Length > 7) ? array[7] : ((array.Length > 1) ? array[1] : "")),
										SlotType = slotType,
										Projectiles = projectiles
									};
								}
							}
						}
					}
					_ = items.Count;
					Console.WriteLine($"[RealmSharkData] Loaded {items.Count} items from ObjectID.list");
				}
				Stream stream2 = FindDataStream("ID3.list");
				if (stream2 == null)
				{
					return;
				}
				using (StreamReader streamReader2 = new StreamReader(stream2, Encoding.UTF8))
				{
					string text3;
					while ((text3 = streamReader2.ReadLine()) != null)
					{
						if (!string.IsNullOrWhiteSpace(text3))
						{
							string[] array2 = text3.Split(':');
							if (array2.Length >= 6 && int.TryParse(array2[0], out var result2) && !items.ContainsKey(result2))
							{
								items[result2] = new ItemEntry
								{
									Id = result2,
									Display = array2[1],
									Clazz = array2[2],
									Group = array2[3],
									RawProjectiles = array2[4],
									IdName = array2[5],
									Projectiles = ParseId3Projectiles(array2[4])
								};
							}
						}
					}
				}
				Console.WriteLine($"[RealmSharkData] Total items after ID3.list: {items.Count}");
			}
			catch (Exception ex)
			{
				Console.WriteLine("[RealmSharkData] Error loading projectile data: " + ex);
			}
			finally
			{
				loaded = true;
			}
		}
	}

	private static Stream FindDataStream(string fileName)
	{
		try
		{
			string[] array = new string[]
			{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", fileName),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sniffer", "assets", fileName),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "assets", fileName),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Sniffer", "assets", fileName),
			};
			foreach (string path in array)
			{
				if (File.Exists(path))
				{
					return File.OpenRead(path);
				}
			}
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string text = executingAssembly.GetManifestResourceNames().FirstOrDefault((string n) => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrEmpty(text))
			{
				return executingAssembly.GetManifestResourceStream(text);
			}
		}
		catch
		{
		}
		return null;
	}

	private static Projectile[] ParseObjectListProjectiles(string projectile, out int slotType)
	{
		slotType = 0;
		if (string.IsNullOrEmpty(projectile))
		{
			return new Projectile[0];
		}
		string[] array = projectile.Split(',');
		if (array.Length == 0)
		{
			return new Projectile[0];
		}
		int.TryParse(array[0], out slotType);
		int num = array.Length - 1;
		if (num < 3)
		{
			return new Projectile[0];
		}
		Projectile[] array2 = new Projectile[num / 3];
		int num2 = 0;
		for (int i = 0; i < num - 2; i += 3)
		{
			int.TryParse(array[1 + i], out var result);
			int.TryParse(array[2 + i], out var result2);
			bool ap = array[3 + i] == "1";
			array2[num2] = new Projectile(result, result2, ap, slotType);
			num2++;
		}
		return array2;
	}

	private static Projectile[] ParseId3Projectiles(string rawProjectiles)
	{
		if (string.IsNullOrEmpty(rawProjectiles))
		{
			return new Projectile[0];
		}
		string[] array = rawProjectiles.Split(',');
		if (array.Length < 3)
		{
			return new Projectile[0];
		}
		Projectile[] array2 = new Projectile[array.Length / 3];
		int num = 0;
		for (int i = 0; i < array.Length - 2; i += 3)
		{
			int.TryParse(array[i], out var result);
			int.TryParse(array[1 + i], out var result2);
			bool ap = array[2 + i] == "1";
			array2[num] = new Projectile(result, result2, ap);
			num++;
		}
		return array2;
	}

	public static int GetSlotType(int weaponId)
	{
		EnsureLoaded();
		if (items.TryGetValue(weaponId, out var value))
		{
			if (value.SlotType > 0)
			{
				return value.SlotType;
			}
			if (value.Projectiles != null && value.Projectiles.Length != 0 && value.Projectiles[0].SlotType > 0)
			{
				return value.Projectiles[0].SlotType;
			}
		}
		return ItemSpriteManager.GetItemMetadata(weaponId)?.SlotType ?? 0;
	}

	public static bool TryGetProjectile(int weaponId, int projectileId, out int min, out int max, out bool ap)
	{
		EnsureLoaded();
		min = 0;
		max = 0;
		ap = false;
		if (items.TryGetValue(weaponId, out var value))
		{
			if (value.Projectiles == null)
			{
				if (!string.IsNullOrEmpty(value.RawProjectiles) && value.RawProjectiles.Contains(","))
				{
					value.Projectiles = ParseObjectListProjectiles(value.RawProjectiles, out var _);
					if (value.Projectiles.Length == 0)
					{
						value.Projectiles = ParseId3Projectiles(value.RawProjectiles);
					}
				}
				else
				{
					value.Projectiles = new Projectile[0];
				}
			}
			if (value.Projectiles != null && value.Projectiles.Length != 0)
			{
				if (projectileId < 0 || projectileId >= value.Projectiles.Length) return false;
				int num = projectileId;
				min = value.Projectiles[num].Min;
				max = value.Projectiles[num].Max;
				ap = value.Projectiles[num].Ap;
				return true;
			}
		}
		ItemSpriteManager.ItemMeta itemMetadata = ItemSpriteManager.GetItemMetadata(weaponId);
		if (itemMetadata != null)
		{
			if (itemMetadata.Projectiles != null && itemMetadata.Projectiles.Count > 0)
			{
				ItemSpriteManager.ProjectileInfo projectileInfo = itemMetadata.Projectiles.FirstOrDefault((ItemSpriteManager.ProjectileInfo p) => p.Id == projectileId);
				if (projectileInfo == null)
				{
					return false;
				}
				min = projectileInfo.MinDamage;
				max = projectileInfo.MaxDamage;
				ap = projectileInfo.ArmorPiercing;
				return true;
			}
			if (projectileId == 0 && itemMetadata.MinDamage > 0)
			{
				min = itemMetadata.MinDamage;
				max = ((itemMetadata.MaxDamage > min) ? itemMetadata.MaxDamage : min);
				ap = itemMetadata.ArmorPiercing;
				return true;
			}
		}
		return false;
	}
}
