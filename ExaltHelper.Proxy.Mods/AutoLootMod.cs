using System;
using System.Collections.Generic;
using System.Linq;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class AutoLootMod
{
	private readonly Client _client;

	private readonly Dictionary<int, DateTime> _publicBagUpdateTimes = new Dictionary<int, DateTime>();

	private int _lastInventorySwapTime = -1;

	private bool _inVault;

	private int _lastSwapSlot = -1;

	private int _lastSwapItem = -1;

	private int _originalQuestObjectId = -1;

	private int _pendingLootBagId = -1;

	private int _remainingDashCount;

	private int _lastDashActivationTime;

	private WorldPosData _lastPlayerPos = WorldPosData.Zero;

	private int _stationaryMoveCount;

	private static readonly string[] _statPotions = new string[25]
	{
		"Mystery Stat Pot", "Potion of Life", "Potion of Mana", "Potion of Defense", "Potion of Speed", "Potion of Attack", "Potion of Dexterity", "Potion of Wisdom", "Potion of Vitality", "Potion of Life (SB)",
		"Potion of Mana (SB)", "Potion of Defense (SB)", "Potion of Speed (SB)", "Potion of Attack (SB)", "Potion of Dexterity (SB)", "Potion of Wisdom (SB)", "Potion of Vitality (SB)", "Greater Potion of Life", "Greater Potion of Mana", "Greater Potion of Defense",
		"Greater Potion of Speed", "Greater Potion of Attack", "Greater Potion of Dexterity", "Greater Potion of Wisdom", "Greater Potion of Vitality"
	};

	public AutoLootMod(Client client)
	{
		_client = client;
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		_inVault = packet.MapName.ToLower().Contains("vault");
	}

	public void OnQuestTarget(DamagePacket packet)
	{
		_originalQuestObjectId = packet.ObjectId;
	}

	public void OnUpdate(UpdatePacket packet)
	{
		if (_pendingLootBagId != -1 && packet.Drops.Contains(_pendingLootBagId))
		{
			_pendingLootBagId = -1;
			if (_originalQuestObjectId != -1)
			{
				DamagePacket damagePacket = new DamagePacket();
				damagePacket.ObjectId = _originalQuestObjectId;
				damagePacket.List = new int[0];
				_client.SendToClient(damagePacket);
			}
		}
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (!Enum.IsDefined(typeof(Bags), objectData.ObjectType))
			{
				continue;
			}
			if (Settings.Default.AutoLootBigBags)
			{
				foreach (StatData item in objectData.Stats.StatList)
				{
					if (item.StatTypeField == StatType.Size)
					{
						item.StatValue = 175;
					}
				}
			}
			if (Enum.IsDefined(typeof(PublicBags), objectData.ObjectType))
			{
				if (_publicBagUpdateTimes.ContainsKey(objectData.Stats.ObjectId))
				{
					_publicBagUpdateTimes[objectData.Stats.ObjectId] = DateTime.Now;
				}
				else
				{
					_publicBagUpdateTimes.Add(objectData.Stats.ObjectId, DateTime.Now);
				}
			}
		}
	}

	public void OnNewTick(NewTickPacket packet)
	{
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (_client.Portals.TryGetValue(item.ObjectId, out var value) && Enum.IsDefined(typeof(PublicBags), value.ObjectType))
			{
				if (_publicBagUpdateTimes.ContainsKey(value.ObjectId))
				{
					_publicBagUpdateTimes[value.ObjectId] = DateTime.Now;
				}
				else
				{
					_publicBagUpdateTimes.Add(value.ObjectId, DateTime.Now);
				}
			}
		}
	}

	public void OnInventorySwapRequest(InvDropPacket packet)
	{
		if (Settings.Default.EnableAutoLoot)
		{
			if (packet.Time - _lastInventorySwapTime < 500)
			{
				packet.Send = false;
				Program.LogNotice("client", "Canceled invswap!");
			}
			else if (_client.Player != null && packet.SlotObject1.SlotId < 1000000 && packet.SlotObject1.ObjectId == _client.PlayerId && packet.SlotObject1.SlotId < _client.Player.Inventory.Length)
			{
				_lastSwapSlot = packet.SlotObject1.SlotId;
				_lastSwapItem = _client.Player.Inventory[packet.SlotObject1.SlotId];
				_client.Player.Inventory[packet.SlotObject1.SlotId] = packet.SlotObject1.ItemType;
			}
			else if (_client.Player != null && packet.SlotObject2.SlotId < 1000000 && packet.SlotObject2.ObjectId == _client.PlayerId && packet.SlotObject2.SlotId < _client.Player.Inventory.Length)
			{
				_lastSwapSlot = packet.SlotObject2.SlotId;
				_lastSwapItem = _client.Player.Inventory[packet.SlotObject2.SlotId];
				_client.Player.Inventory[packet.SlotObject2.SlotId] = packet.SlotObject2.ItemType;
			}
			_lastInventorySwapTime = packet.Time;
		}
	}

	public void OnInventoryResult(InvSwapPacket packet)
	{
		if (Settings.Default.EnableAutoLoot && !packet.SwapFlag && _lastSwapSlot > -1 && _client.Player != null)
		{
			_client.Player.Inventory[_lastSwapSlot] = _lastSwapItem;
			Program.LogNotice("client", "Swap failed");
		}
		if (packet.SwapFlag && _client.Player != null && _client.Player.ObjectType == 818 && packet.UseType == 1 && packet.SlotObject1.ObjectId == _client.ClientId && packet.SlotObject1.SlotId == 1 && packet.SlotObject2.ObjectId == 0)
		{
			IEnumerable<Activate> source = GameData.Items.GetById((ushort)packet.SlotObject1.ItemType).Activations.Where((Activate activate) => activate.Name == ActivateType.ChannelDash);
			if (source.Any())
			{
				_remainingDashCount = source.First().Amount;
				_lastDashActivationTime = _client._serverTime;
			}
		}
	}

	public void OnDashConsumed()
	{
		_remainingDashCount--;
	}

	public void OnMove(MovePacket packet)
	{
		if (_client._currentServerName == "Daily Quest Room" || _client._currentServerName.StartsWith("Pet Yard"))
		{
			return;
		}
		MoveRecord moveRecord = packet.Positions.Last();
		WorldPosData worldPosData = packet.Positions.Last().GetWorldPos();
		if (Settings.Default.AutoLootAutoDisable && worldPosData.Equals(_lastPlayerPos))
		{
			if (_stationaryMoveCount++ > 100)
			{
				return;
			}
		}
		else
		{
			_lastPlayerPos = worldPosData;
			_stationaryMoveCount = 0;
		}
		if (!Settings.Default.EnableAutoLoot || _inVault || (_remainingDashCount > 0 && _client._serverTime - _lastDashActivationTime < 21500) || moveRecord.Time - _lastInventorySwapTime < 600)
		{
			return;
		}
		foreach (MapObject item in _client.Portals.Values.ToList())
		{
			if (!Enum.IsDefined(typeof(Bags), item.ObjectType))
			{
				continue;
			}
			bool flag = item.TargetPosition.DistanceSquaredTo(worldPosData) <= 1.0;
			if (_publicBagUpdateTimes.TryGetValue(item.ObjectId, out var value) && (DateTime.Now.Subtract(value) < TimeSpan.FromSeconds(0.2) || (Settings.Default.AutoLootDelay && DateTime.Now.Subtract(value) < TimeSpan.FromSeconds(2.0))))
			{
				continue;
			}
			List<int> list = new List<int>();
			for (byte b = 0; b < item.Inventory.Length; b++)
			{
				int num = item.Inventory[b];
				if (num != -1)
				{
					if (Settings.Default.AutoLootHealingPotions || Settings.Default.AutoLootManaPotions)
					{
						bool flag2 = Client.HealthPotionTypes.Contains(num) && Settings.Default.AutoLootHealingPotions;
						bool flag3 = Client.ManaPotionTypes.Contains(num) && Settings.Default.AutoLootManaPotions;
						if (flag2 | flag3)
						{
							for (int i = 0; i < _client.QuickSlotPotions.Length && (i != 2 || (_client.Player != null && _client.Player.IsDrawText)); i++)
							{
								PotionInfo potionInfo = _client.QuickSlotPotions[i];
								list.Add(potionInfo.Type);
								ItemStructure itemStructure = GameData.Items.GetById((ushort)potionInfo.Type);
								if (potionInfo.Type == num && potionInfo.Quantity < itemStructure.QuickslotMaxStack)
								{
									if (flag)
									{
										string itemName = ((potionInfo.Type == -1) ? "Empty" : itemStructure.Name);
										SendInventorySwap(moveRecord.Time, worldPosData, item.ObjectId, num, b, _client.ClientId, potionInfo.Type, 1000000 + i, itemName);
										return;
									}
									SetLootBagQuestTarget(item.ObjectId);
									break;
								}
							}
							if (list.Contains(-1) && !list.Contains(num))
							{
								int num2 = (_client.HasPotionBelt ? 3 : 2);
								for (int j = 0; j < num2; j++)
								{
									PotionInfo potionInfo2 = _client.QuickSlotPotions[j];
									if (potionInfo2.Type == -1)
									{
										if (flag)
										{
											string name = GameData.Items.GetById((ushort)num).Name;
											SendInventorySwap(moveRecord.Time, worldPosData, item.ObjectId, num, b, _client.ClientId, potionInfo2.Type, 1000000 + j, name);
											return;
										}
										SetLootBagQuestTarget(item.ObjectId);
										break;
									}
								}
							}
							if ((flag2 && Settings.Default.AutoLootOverFillHP) || (flag3 && Settings.Default.AutoLootOverFillMP))
							{
								int num3 = FindEmptyInventorySlot();
								if (num3 > 0)
								{
									if (flag)
									{
										string name2 = GameData.Items.GetById((ushort)num).Name;
										SendInventorySwap(moveRecord.Time, worldPosData, item.ObjectId, num, b, _client.ClientId, -1, num3, name2);
										return;
									}
									SetLootBagQuestTarget(item.ObjectId);
									break;
								}
							}
						}
					}
					if (ShouldLootItem((ushort)num))
					{
						if (flag)
						{
							MoveLootToInventory(worldPosData, moveRecord.Time, item.ObjectId, num, b);
							return;
						}
						SetLootBagQuestTarget(item.ObjectId);
					}
				}
			}
		}
		if (!Settings.Default.AutoLootMoveConsumables)
		{
			return;
		}
		for (int k = 0; k < _client.QuickSlotPotions.Length; k++)
		{
			PotionInfo potionInfo3 = _client.QuickSlotPotions[k];
			if (potionInfo3.Type == -1)
			{
				continue;
			}
			ItemStructure itemStructure2 = GameData.Items.GetById((ushort)potionInfo3.Type);
			if (potionInfo3.Quantity >= itemStructure2.QuickslotMaxStack)
			{
				continue;
			}
			for (int l = 4; l < _client.Player.Inventory.Length; l++)
			{
				if (_client.Player.Inventory[l] == potionInfo3.Type)
				{
					SendInventorySwap(moveRecord.Time, worldPosData, _client.ClientId, potionInfo3.Type, l, _client.ClientId, potionInfo3.Type, 1000000 + k, null);
					return;
				}
			}
		}
	}

	private void SetLootBagQuestTarget(int bagObjectId)
	{
		if (Settings.Default.AutoLootQuests)
		{
			DamagePacket damagePacket = new DamagePacket();
			damagePacket.ObjectId = bagObjectId;
			damagePacket.List = new int[0];
			_pendingLootBagId = bagObjectId;
			_client.SendToClient(damagePacket);
		}
	}

	private int FindEmptyInventorySlot()
	{
		for (int i = 4; i < _client.Player.BackpackSlotCount; i++)
		{
			if (_client.Player.Inventory[i] == -1)
			{
				return i;
			}
		}
		return -1;
	}

	private void MoveLootToInventory(WorldPosData playerPosition, int time, int bagObjectId, int itemType, byte sourceSlot, int destinationSlot = -1)
	{
		if (destinationSlot >= 1000000)
		{
			SendInventorySwap(time, playerPosition, bagObjectId, itemType, sourceSlot, _client.Player.ObjectId, itemType, destinationSlot, GameData.Items.GetById((ushort)itemType).Name);
			return;
		}
		for (byte b = 4; b < _client.Player.BackpackSlotCount; b++)
		{
			if (_client.Player.Inventory[b] == -1)
			{
				SendInventorySwap(time, playerPosition, bagObjectId, itemType, sourceSlot, _client.Player.ObjectId, -1, b, GameData.Items.GetById((ushort)itemType).Name);
				break;
			}
		}
	}

	private void SendInventorySwap(int time, WorldPosData playerPosition, int sourceObjectId, int sourceItemType, int sourceSlot, int destinationObjectId, int destinationItemType, int destinationSlot, string itemName)
	{
		InvDropPacket invDropPacket = new InvDropPacket();
		invDropPacket.Time = time;
		invDropPacket.Position = playerPosition;
		SlotObjectData slotObjectData = new SlotObjectData
		{
			ObjectId = sourceObjectId,
			ItemType = sourceItemType,
			SlotId = sourceSlot
		};
		SlotObjectData slotObjectData2 = new SlotObjectData
		{
			ObjectId = destinationObjectId,
			ItemType = destinationItemType,
			SlotId = destinationSlot
		};
		if (slotObjectData.ItemType == -1)
		{
			invDropPacket.SlotObject1 = slotObjectData2;
			invDropPacket.SlotObject2 = slotObjectData;
		}
		else
		{
			invDropPacket.SlotObject1 = slotObjectData;
			invDropPacket.SlotObject2 = slotObjectData2;
		}
		_lastInventorySwapTime = time;
		_client.SendToServer(invDropPacket);
		if (!string.IsNullOrEmpty(itemName))
		{
			_client.SendNotification("AutoLoot", "Looting " + itemName);
		}
	}

	private bool ShouldLootItem(ushort itemType)
	{
		ItemStructure itemStructure = GameData.Items.GetById(itemType);
		if (itemStructure == null)
		{
			return false;
		}
		if (Settings.Default.AutoLootStatPotions && _statPotions.Contains(itemStructure.Name))
		{
			return true;
		}
		if (Settings.Default.AutoLootUTs && itemStructure.Tier == Tiers.UT && itemStructure.SlotType != 10 && itemStructure.SlotType != 26)
		{
			return true;
		}
		if (Settings.Default.AutoLootMarks && itemStructure.Name.Contains("Mark of "))
		{
			return true;
		}
		if (Settings.Default.AutoLootEggs && itemStructure.Name.EndsWith(" Egg"))
		{
			return true;
		}
		switch (itemStructure.SlotType)
		{
		case 1:
		case 2:
		case 3:
		case 8:
		case 17:
		case 24:
			return (int)itemStructure.Tier >= Settings.Default.AutoLootWeaponTierThreshold;
		case 6:
		case 7:
		case 14:
			return (int)itemStructure.Tier >= Settings.Default.AutoLootArmorTierThreshold;
		case 9:
			return (int)itemStructure.Tier >= Settings.Default.AutoLootRingTierThreshold;
		case 4:
		case 5:
		case 11:
		case 12:
		case 13:
		case 15:
		case 16:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
		case 23:
		case 25:
		case 27:
		case 28:
		case 29:
		case 30:
		case 31:
			return (int)itemStructure.Tier >= Settings.Default.AutoLootAbilityTierThreshold;
		default:
			return false;
		}
	}
}
