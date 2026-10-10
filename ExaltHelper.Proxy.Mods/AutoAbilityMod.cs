using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class AutoAbilityMod
{
	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct AbilityTargetingContext
	{
		public AutoAbilityMod ModInstance;

		public bool LimitTargetMaxHp;

		public int TargetHpThreshold;

		public float RadiusSquared;
	}

	private readonly Client _client;

	private bool _isInSafeMap;

	private int _abilityCooldownUntil;

	private int _lastAbilityUseTime;

	private bool _wasQuiet;

	private bool _wasHexed;

	private bool _isHexedOrRecentlyHexed;

	private bool _manaPotionPending;

	private WorldPosData _aimPosition = WorldPosData.Zero;

	private int _lastShotBulletId;

	private List<int> _summonedObjectIds = new List<int>();

	private int _selectedAbilityIndex;

	public float EnchantmentManaCostMultiplier = 1f;

	public float EquipmentManaCostMultiplier = 1f;

	private readonly List<string> SafeMapNames = new List<string>
	{
		"Nexus", "Vault", "Guild Hall", "Guild Hall 2", "Guild Hall 3", "Guild Hall 4", "Guild Hall 5", "Cloth Bazaar", "Nexus Explanation", "Vault Explanation",
		"Guild Explanation", "Daily Quest Room", "Daily Login Room", "Pet Yard", "Pet Yard 2", "Pet Yard 3", "Pet Yard 4", "Pet Yard 5"
	};

	private readonly int[] AbilityItemTypes = new int[3] { 14976, 14896, 3929 };

	public AutoAbilityMod(Client client)
	{
		_client = client;
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		_isInSafeMap = SafeMapNames.Contains(packet.MapName);
		_lastAbilityUseTime = 0;
		_abilityCooldownUntil = 0;
	}

	public void OnGenericFailure(GenericFailurePacket packet)
	{
		if (packet.IsCommand("aa", out var _))
		{
			Settings.Default.EnableAutoAbility = !Settings.Default.EnableAutoAbility;
			Settings.Default.Change();
			_client.SendNotification("AutoAbility", Settings.Default.EnableAutoAbility ? "Enabled" : "Disabled");
		}
	}

	public void OnUseItem(UseItemPacket packet)
	{
		if (packet.Item.ObjectId == _client.ClientId && packet.Item.SlotId == 1 && Settings.Default.EnableAutoAbility && packet.Time < _abilityCooldownUntil)
		{
			packet.Send = false;
			_client.SendNotification("AutoAbility", "Ability is on cooldown!");
		}
		else
		{
			_lastAbilityUseTime = packet.Time;
		}
	}

	public void OnNewTick(NewTickPacket packet)
	{
		if (_client?.Player == null)
		{
			return;
		}
		bool flag = _client.Player.IsQuiet();
		bool flag2 = _client.Player.IsHexed();
		if (Settings.Default.EnableAutoAbility && Settings.Default.AutoAbilityAutoMP && _wasQuiet && !flag)
		{
			_wasQuiet = false;
			_manaPotionPending = true;
		}
		_isHexedOrRecentlyHexed = flag2 || _wasHexed;
		_wasQuiet = flag;
		_wasHexed = flag2;
		if (!Settings.Default.EnableAutoAbility)
		{
			return;
		}
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (item.ObjectId == _client.ClientId)
			{
				foreach (StatData item2 in item.StatList)
				{
					if (item2.StatTypeField == StatType.TransformSkinId && item2.StatValue == 819)
					{
						_selectedAbilityIndex = 0;
						break;
					}
				}
			}
			if (item.ObjectId - 1 == _client.ClientId)
			{
				continue;
			}
			foreach (StatData item3 in item.StatList)
			{
				if (!(item3.StatTypeField != StatType.PetOwnerObjectId) && item3.StatValue == _client.ClientId)
				{
					_summonedObjectIds.Add(item.ObjectId);
					return;
				}
			}
		}
	}

	public void OnUpdate(UpdatePacket packet)
	{
		int[] Drops = packet.Drops;
		foreach (int item in Drops)
		{
			if (_summonedObjectIds.Contains(item))
			{
				_summonedObjectIds.Remove(item);
				break;
			}
		}
	}

	public void OnGotoAck(GotoAckPacket packet)
	{
		if (!_manaPotionPending || _client?.Player?.Inventory == null)
		{
			return;
		}
		_manaPotionPending = false;
		int num = (_client.HasPotionBelt ? 3 : 2);
		for (int i = 0; i < num; i++)
		{
			PotionInfo potionInfo = _client.QuickSlotPotions[i];
			if (Client.ManaPotionTypes.Contains(potionInfo.Type) && potionInfo.Quantity > 0)
			{
				UseConsumable(packet.Time, potionInfo.Type, 1000000 + i, packet.Position);
				return;
			}
		}
		for (int j = 4; j < _client.Player.Inventory.Length; j++)
		{
			int num2 = _client.Player.Inventory[j];
			if (Client.ManaPotionTypes.Contains(num2))
			{
				UseConsumable(packet.Time, num2, j, packet.Position);
				break;
			}
		}
	}

	public void OnInvDrop(InvDropPacket packet)
	{
		if (_client?.Player?.Inventory == null)
		{
			return;
		}
		if (packet.SlotObject1.ObjectId == _client.ClientId)
		{
			if (packet.SlotObject1.SlotId == 1)
			{
				_client.Player.Inventory[1] = packet.SlotObject2.ItemType;
			}
		}
		else if (packet.SlotObject2.ObjectId == _client.ClientId && packet.SlotObject2.SlotId == 1)
		{
			_client.Player.Inventory[1] = packet.SlotObject1.ItemType;
		}
	}

	public void OnInvSwap(InvSwapPacket packet)
	{
		if (packet.SwapFlag)
		{
			if (packet.SlotObject1.ObjectId == _client.ClientId && packet.SlotObject1.SlotId == 1)
			{
				_selectedAbilityIndex = 0;
			}
			if (packet.SlotObject2.ObjectId == _client.ClientId && packet.SlotObject2.SlotId == 1)
			{
				_selectedAbilityIndex = 0;
			}
		}
	}

	public void OnOtherHit(OtherHitPacket packet)
	{
		_selectedAbilityIndex = packet.Ability;
	}

	public void OnPlayerShoot(PlayerShootPacket packet)
	{
		if (_client?.Player == null)
		{
			return;
		}
		_aimPosition = _client.Player.Position.OffsetByAngle(packet.Angle, 6.0);
		int Time = packet.Time;
		_lastShotBulletId = packet.BulletId;
		if (_manaPotionPending)
		{
			_manaPotionPending = false;
			int num = (_client.HasPotionBelt ? 3 : 2);
			for (int i = 0; i < num; i++)
			{
				PotionInfo potionInfo = _client.QuickSlotPotions[i];
				if (Client.ManaPotionTypes.Contains(potionInfo.Type) && potionInfo.Quantity > 0)
				{
					UseConsumable(Time, potionInfo.Type, 1000000 + i, _client.Player.Position);
					return;
				}
			}
			for (int j = 4; j < _client.Player.Inventory.Length; j++)
			{
				int num2 = _client.Player.Inventory[j];
				if (Client.ManaPotionTypes.Contains(num2))
				{
					UseConsumable(Time, num2, j, _client.Player.Position);
					return;
				}
			}
		}
		if (!Settings.Default.EnableAutoAbility || _isInSafeMap || Time - _lastAbilityUseTime < 500 || _client.Player == null)
		{
			return;
		}
		int num3 = _client.Player.Inventory[1];
		if (num3 == -1 || Time < _abilityCooldownUntil || (Settings.Default.AutoAbilityCustomDelay != 0 && Time - _lastAbilityUseTime < Settings.Default.AutoAbilityCustomDelay) || _client.Player.MaxMp <= 0 || _client.Player.Mp * 100 / _client.Player.MaxMp < Settings.Default.AutoAbilityMinimumManaLeftThreshold || _client.Player.IsQuiet() || _isHexedOrRecentlyHexed || _client.oryxSanctuaryMod.ShouldBlockAbilityUse())
		{
			return;
		}
		ItemStructure itemStructure = GameData.Items.GetById((ushort)num3);
		if (itemStructure == null)
		{
			return;
		}
		float num4 = (int)itemStructure.MpCost;
		if (itemStructure.AbilityOverrides.Any())
		{
			(float?, int?) tuple = itemStructure.AbilityOverrides[_selectedAbilityIndex];
			if (tuple.Item2.HasValue)
			{
				num4 = tuple.Item2.Value;
			}
		}
		num4 *= EnchantmentManaCostMultiplier;
		num4 *= EquipmentManaCostMultiplier;
		if (num4 > (float)_client.Player.Mp || itemStructure.Activations.Any((Activate activate) => activate.Name == ActivateType.Shoot))
		{
			return;
		}
		switch (_client.Player.ObjectType)
		{
		case 784:
			switch (num3)
			{
			case 28835:
			case 38298:
				TryUseAbilityOnGroup(itemStructure, 2.5f, Time, limitTargetMaxHp: false, -1, 1);
				break;
			default:
				if (_client.Player.IsSick() || _client.Player.Hp * 100 / _client.Player.MaxHp > Settings.Default.AutoAbilityHealHpPercent)
				{
					break;
				}
				goto case 1976;
			case 1976:
			case 5322:
				UseAbility(itemStructure, Time, _client.Player.Position, 1);
				break;
			}
			break;
		case 768:
			if (num3 == 2650 || num3 == 65515)
			{
				break;
			}
			goto IL_08d6;
		case 797:
			if (num3 != 32699)
			{
				if (num3 == 14980 || num3 == 3916 || num3 == 14897)
				{
					if (!TryUseTargetedAbility(itemStructure, Time, double.NaN, 5.18, _client.Player.Position))
					{
						UseAbility(itemStructure, Time, _client.Player.Position, 1);
					}
					break;
				}
				goto case 799;
			}
			goto IL_08d6;
		case 799:
			UseAbility(itemStructure, Time, _client.Player.Position, 1);
			break;
		case 806:
			if (!_client.Player.IsSpeedy())
			{
				if (num3 == 19291)
				{
					TryUseTargetedAbility(itemStructure, Time, double.NaN, 5.625, _client.Player.Position);
				}
				else
				{
					UseAbility(itemStructure, Time, _client.Player.Position, 1);
				}
			}
			break;
		case 802:
			if (!_client.Player.IsUnstable() && num3 == 6213)
			{
				UseAbility(itemStructure, Time, _client.Player.Position, 1);
				break;
			}
			if (_client.Player.IsUnstable())
			{
				break;
			}
			goto IL_091c;
		case 801:
			if (_client.Player.IsUnstable())
			{
				break;
			}
			goto IL_091c;
		case 800:
			if (_client.Player.IsUnstable())
			{
				break;
			}
			goto IL_091c;
		case 805:
			if (!_client.Player.IsUnstable())
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 9.0, _client.Player.Position);
			}
			break;
		case 782:
			if (!_client.Player.IsUnstable())
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 12.0, _client.Player.Position);
			}
			break;
		case 803:
			if (!_client.Player.IsUnstable() && num3 == 19969)
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 9.0, _client.Player.Position);
			}
			else if (!_client.Player.IsUnstable() && (num3 == 14992 || num3 == 2041 || num3 == 14902))
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 5.0, _client.Player.Position);
			}
			else if (!_client.Player.IsUnstable() && num3 == 23742)
			{
				TryUseAbilityOnGroup(itemStructure, itemStructure.ActivationRadius, Time, limitTargetMaxHp: false, -1, 1);
			}
			else if (Settings.Default.AutoAbilityMysticTargetSelf && num3 != 8386 && num3 != 5577)
			{
				UseAbility(itemStructure, Time, _client.Player.Position, 1);
			}
			else
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 7.0, _client.Player.Position);
			}
			break;
		case 785:
			if (!_client.Player.IsUnstable())
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, GetItemRange(itemStructure.ID), _client.Player.Position);
			}
			break;
		case 796:
			switch (num3)
			{
			case 14354:
			{
				double abilityRange = ((_client.Player.Inventory[2] == 14355) ? 10.0 : 5.0);
				TryUseTargetedAbility(itemStructure, Time, double.NaN, abilityRange, _client.Player.Position);
				break;
			}
			case 1329:
			case 19948:
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 7.0, _client.Player.Position);
				break;
			default:
				UseAbility(itemStructure, Time, _client.Player.Position, 1);
				break;
			}
			break;
		case 817:
			if (_summonedObjectIds.Count < 3)
			{
				UseAbility(itemStructure, Time, _client.Player.Position, 1);
			}
			break;
		case 798:
			if (num3 == 28853 || num3 == 38297)
			{
				TryUseTargetedAbility(itemStructure, Time, double.NaN, 3.0, _client.Player.Position);
			}
			break;
		case 819:
			{
				if (Settings.Default.AutoAbilityChargeDruidMeter && _selectedAbilityIndex == 0)
				{
					TryUseTargetedAbility(itemStructure, Time, double.NaN, 12.0, _client.Player.Position);
				}
				break;
			}
			IL_08d6:
			if (!TryUseTargetedAbility(itemStructure, Time, double.NaN, 5.6, _client.Player.Position))
			{
				UseAbility(itemStructure, Time, _client.Player.Position, 1);
			}
			break;
			IL_091c:
			TryUseAbilityOnGroup(itemStructure, itemStructure.ActivationRadius, Time);
			break;
		}
	}

	private float GetItemRange(ushort itemType)
	{
		return itemType switch
		{
			8994 => 4.6f,
			2036 => 4.6f,
			9152 => 6f,
			5164 => 9f,
			5276 => 9f,
			_ => 4.4f,
		};
	}

	private void UseConsumable(int time, int itemType, int slotId, WorldPosData position)
	{
		UseItemPacket useItemPacket = new UseItemPacket();
		useItemPacket.Item = new SlotObjectData
		{
			ObjectId = _client.ClientId,
			ItemType = itemType,
			SlotId = slotId
		};
		useItemPacket.Time = time;
		useItemPacket.ItemUsePos = position;
		useItemPacket.UseType = ((slotId < 1000000) ? ((byte)1) : ((byte)0));
		_client.SendToServer(useItemPacket);
	}

	private void UseAbility(ItemStructure ability, int time, WorldPosData targetPosition = null, byte useType = 1)
	{
		if (targetPosition == null)
		{
			targetPosition = _client.Player.Position;
		}
		float num = ability.Cooldown;
		if (ability.AbilityOverrides.Any())
		{
			(float?, int?) tuple = ability.AbilityOverrides[_selectedAbilityIndex];
			if (tuple.Item1.HasValue)
			{
				num = tuple.Item1.Value;
			}
		}
		num = ((num == 0f) ? 500f : (num * 1000f));
		_abilityCooldownUntil = time + (int)num;
		_lastAbilityUseTime = time;
		UseItemPacket useItemPacket = new UseItemPacket();
		useItemPacket.Item = new SlotObjectData
		{
			ObjectId = _client.Player.ObjectId,
			SlotId = 1,
			ItemType = _client.Player.Inventory[1]
		};
		useItemPacket.Time = time;
		useItemPacket.UseType = useType;
		useItemPacket.ItemUsePos = targetPosition;
		useItemPacket.UnknownInt = _selectedAbilityIndex;
		_client.SendToServer(useItemPacket);
		if (Settings.Default.AutoAbilityNotifications)
		{
			_client.SendNotification("AutoAbility activated!");
		}
	}

	private bool TryUseAbilityOnGroup(ItemStructure ability, float radius, int time, bool limitTargetMaxHp = false, int healthThreshold = -1, int minimumGroupSize = -1)
	{
		AbilityTargetingContext context = new AbilityTargetingContext
		{
			ModInstance = this,
			LimitTargetMaxHp = limitTargetMaxHp,
			RadiusSquared = radius
		};
		context.RadiusSquared *= context.RadiusSquared;
		MapObject mapObject = null;
		int num = -1;
		context.TargetHpThreshold = Settings.Default.AutoAbilityMinimumEnemyHealthThreshold;
		if (healthThreshold != -1)
		{
			context.TargetHpThreshold = healthThreshold;
		}
		int num2 = Settings.Default.AutoAbilityMinimumGroupSizeThreshold;
		if (minimumGroupSize != -1)
		{
			num2 = minimumGroupSize;
		}
		foreach (MapObject value in _client.Enemies.Values)
		{
			if (value.IsCharacter && !value.IsInvulnerable() && !value.IsStasis() && !value.IsInvincible() && (!context.LimitTargetMaxHp || value.MaxHp <= context.TargetHpThreshold) && !(_client.Player.Position.DistanceSquaredTo(value.Position) > 144.0))
			{
				int num3 = CountNearbyTargets(value, ref context);
				if (num3 >= num2 && num3 > num)
				{
					mapObject = value;
					num = num3;
				}
			}
		}
		if (mapObject != null)
		{
			UseAbility(ability, time, mapObject.TargetPosition, 1);
			return true;
		}
		return false;
	}

	private bool TryUseTargetedAbility(ItemStructure ability, int time, double projectileSpeed, double range, WorldPosData preferredPosition)
	{
		range *= range;
		WorldPosData aimPosition = new WorldPosData(_aimPosition.X, _aimPosition.Y);
		WorldPosData Zero = WorldPosData.Zero;
		WorldPosData worldPosData = WorldPosData.Zero;
		double num = double.MaxValue;
		int num2 = int.MinValue;
		int num3 = int.MinValue;
		int num4 = 2;
		if (Settings.Default.AutoAbilityClosestEnemy)
		{
			num4 = 2;
		}
		if (Settings.Default.AutoAbilityStrongestEnemy)
		{
			num4 = 1;
		}
		int num5 = 5;
		num5 *= num5;
		bool enemyIgnore = Settings.Default.EnemyIgnore;
		int autoAbilityMinimumEnemyHealthThreshold = Settings.Default.AutoAbilityMinimumEnemyHealthThreshold;
		bool flag = true;
		bool flag2 = true;
		do
		{
			bool flag3 = false;
			switch (num4)
			{
			case 0:
				foreach (MapObject value in _client.Enemies.Values)
				{
					flag3 = value.IsQuest;
					if (!value.IsCharacter || (flag && !flag3) || value.IsInvulnerable() || value.IsStasis() || value.IsInvincible() || value.Hp < 0 || (!enemyIgnore && Settings.Default.FameIngoredEnemies.Contains(value.ObjectType)) || value.MaxHp < autoAbilityMinimumEnemyHealthThreshold)
					{
						continue;
					}
					Zero = ((!double.IsNaN(projectileSpeed)) ? new WorldPosData(value.Position.X, value.Position.Y) : new WorldPosData(value.TargetPosition.X, value.TargetPosition.Y));
					if (Zero == WorldPosData.Zero)
					{
						continue;
					}
					double num8 = preferredPosition.DistanceSquaredTo(value.Position);
					if (!(num8 <= range))
					{
						continue;
					}
					num8 = preferredPosition.DistanceSquaredTo(aimPosition);
					if (num8 <= (double)num5)
					{
						if (flag & flag3)
						{
							flag2 = false;
							worldPosData = (WorldPosData)Zero.Clone();
							break;
						}
						if (num8 <= num)
						{
							num = num8;
							worldPosData = (WorldPosData)Zero.Clone();
						}
					}
				}
				break;
			case 1:
				foreach (MapObject value2 in _client.Enemies.Values)
				{
					flag3 = value2.IsQuest;
					if (!value2.IsCharacter || (flag && !flag3) || value2.IsInvulnerable() || value2.IsStasis() || value2.IsInvincible() || value2.Hp < 0 || (!enemyIgnore && Settings.Default.FameIngoredEnemies.Contains(value2.ObjectType)) || value2.MaxHp < autoAbilityMinimumEnemyHealthThreshold)
					{
						continue;
					}
					Zero = ((!double.IsNaN(projectileSpeed)) ? new WorldPosData(value2.Position.X, value2.Position.Y) : new WorldPosData(value2.TargetPosition.X, value2.TargetPosition.Y));
					if (Zero == WorldPosData.Zero || value2.MaxHp < num3)
					{
						continue;
					}
					double num7;
					if (value2.MaxHp == num3)
					{
						if (value2.Hp <= num2)
						{
							num7 = preferredPosition.DistanceSquaredTo(value2.Position);
							if ((value2.Hp != num2 || !(num7 > num)) && num7 < range)
							{
								num3 = value2.MaxHp;
								num2 = value2.Hp;
								worldPosData = (WorldPosData)Zero.Clone();
								num = num7;
							}
						}
						continue;
					}
					num7 = preferredPosition.DistanceSquaredTo(value2.Position);
					if (num7 < range)
					{
						if (flag & flag3)
						{
							flag2 = false;
							worldPosData = Zero;
							break;
						}
						num3 = value2.MaxHp;
						num2 = value2.Hp;
						num = num7;
						worldPosData = (WorldPosData)Zero.Clone();
					}
				}
				break;
			case 2:
				foreach (MapObject value3 in _client.Enemies.Values)
				{
					flag3 = value3.IsQuest;
					if (!value3.IsCharacter || (flag && !flag3) || value3.IsInvulnerable() || value3.IsStasis() || value3.IsInvincible() || value3.Hp < 0 || (!enemyIgnore && Settings.Default.FameIngoredEnemies.Contains(value3.ObjectType)) || value3.MaxHp < autoAbilityMinimumEnemyHealthThreshold)
					{
						continue;
					}
					Zero = ((!double.IsNaN(projectileSpeed)) ? new WorldPosData(value3.Position.X, value3.Position.Y) : new WorldPosData(value3.TargetPosition.X, value3.TargetPosition.Y));
					if (Zero == WorldPosData.Zero)
					{
						continue;
					}
					double num6 = preferredPosition.DistanceSquaredTo(value3.Position);
					if (num6 < range)
					{
						if (flag & flag3)
						{
							flag2 = false;
							worldPosData = (WorldPosData)Zero.Clone();
							break;
						}
						if (num6 < num)
						{
							num = num6;
							worldPosData = (WorldPosData)Zero.Clone();
						}
					}
				}
				break;
			}
			if (flag)
			{
				if (flag2)
				{
					flag = false;
				}
			}
			else
			{
				flag2 = false;
			}
		}
		while (flag2);
		if (!worldPosData.Equals(WorldPosData.Zero))
		{
			if ((ability.ID == 13988 || ability.ID == 17720) && Settings.Default.AutoAbilityPenetratingBlastOffset)
			{
				int num9 = _lastShotBulletId % 4;
				int num10 = 0;
				int num11 = 0;
				num10 += ((num9 & 1) * 2 - 1) * -(-((num9 + 2) & 2) >> 31) * 2;
				num11 += ((num9 & 1) * 2 - 1) * -(-(num9 & 2) >> 31) * 2;
				worldPosData.X += num10;
				worldPosData.Y += num11;
			}
			UseAbility(ability, time, worldPosData, 1);
			return true;
		}
		return false;
	}

	[CompilerGenerated]
	private int CountNearbyTargets(MapObject target, ref AbilityTargetingContext context)
	{
		int num = 0;
		foreach (MapObject value in _client.Enemies.Values)
		{
			if (value.IsCharacter && !value.IsInvulnerable() && !value.IsStasis() && !value.IsInvincible() && (!context.LimitTargetMaxHp || value.MaxHp <= context.TargetHpThreshold) && !(target.Position.DistanceSquaredTo(value.Position) > (double)context.RadiusSquared))
			{
				num++;
			}
		}
		return num;
	}
}
