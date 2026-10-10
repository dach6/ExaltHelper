using System.Collections.Generic;
using System.Linq;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper.Proxy.Mods;

public struct Projectile
{
	private static readonly Dictionary<int, List<byte>> BulletHitRecordMap = new Dictionary<int, List<byte>>();

	private static readonly Dictionary<int, List<byte>> BulletTargetMap = new Dictionary<int, List<byte>>();

	public static readonly Dictionary<int, Dictionary<byte, ProjectileStructure>> ObjectTypeToProjectileIdStructureMap = new Dictionary<int, Dictionary<byte, ProjectileStructure>>();

	private static bool IsTrackingInitialized = false;

	public int OwnerId;

	public ushort Id;

	public byte ProjectileType;

	public int Damage;

	public ProjectileStructure Structure;

	public static bool IsPiercing(int enemyType, byte projectileType)
	{
		if (BulletHitRecordMap.ContainsKey(enemyType))
		{
			return BulletHitRecordMap[enemyType].Contains(projectileType);
		}
		return false;
	}

	public static bool IsArmorBreaking(int enemyType, byte projectileType)
	{
		if (BulletTargetMap.ContainsKey(enemyType))
		{
			return BulletTargetMap[enemyType].Contains(projectileType);
		}
		return false;
	}

	public static void Initialize()
	{
		if (IsTrackingInitialized)
		{
			return;
		}
		IsTrackingInitialized = true;
		GameData.Objects.Map.Values.ForEach(delegate(ObjectStructure objectDefinition)
		{
			if (objectDefinition.Projectiles.Any())
			{
				List<byte> list = new List<byte>();
				List<byte> list2 = new List<byte>();
				Dictionary<byte, ProjectileStructure> dictionary = new Dictionary<byte, ProjectileStructure>();
				ProjectileStructure[] projectiles = objectDefinition.Projectiles;
				foreach (ProjectileStructure projectileStructure in projectiles)
				{
					if (projectileStructure.ArmorPiercing)
					{
						list.Add(projectileStructure.ID);
					}
					if (projectileStructure.StatusEffects.ContainsKey("Armor Broken"))
					{
						list2.Add(projectileStructure.ID);
					}
					dictionary.Add(projectileStructure.ID, projectileStructure);
				}
				BulletHitRecordMap.Add(objectDefinition.ID, list);
				BulletTargetMap.Add(objectDefinition.ID, list2);
				ObjectTypeToProjectileIdStructureMap.Add(objectDefinition.ID, dictionary);
			}
		});
	}
}
