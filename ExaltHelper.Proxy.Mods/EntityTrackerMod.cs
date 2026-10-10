using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class EntityTrackerMod
{
	[CompilerGenerated]
	private sealed class EntityTrackerBinding
	{
		public string ClipboardText;

		internal void CopyTextToClipboard()
		{
			Clipboard.SetText(ClipboardText);
		}
	}

	private Client _client;

	private int _lastMovementTimestamp = -1;

	private int _mapWidth;

	private readonly int[] _selectedEntityId = new int[10] { 65339, 50307, 26730, 26734, 20605, 20466, 50814, 20893, 20900, 22522 };

	private string[] _accountIds = new string[0];

	private bool _isFirstVaultContents = true;

	private bool _isFirstGiftContents = true;

	private bool _isFirstPotionContents = true;

	public EntityTrackerMod(Client client)
	{
		_client = client;
		Projectile.Initialize();
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		_mapWidth = packet.Width;
		_client.MapWidth = packet.Width;
		_client.MapHeight = packet.Height;
		_client._currentServerName = packet.MapName;
		_client.Tiles = new MapTile[packet.Width * packet.Height];
		_client.Entities.Clear();
		_client.Enemies.Clear();
		_client.Portals.Clear();
		foreach (KeyValuePair<int, Dictionary<int, Projectile>> item in _client.Projectiles)
		{
			item.Value.Clear();
		}
		_client.Projectiles.Clear();
	}

	public void ScheduleConnectionNotice()
	{
		if (!Settings.Default.DisableSystemMessages)
		{
			Task.Run((Action)SendDelayedConnectionNotice);
		}
	}

	private void SendDelayedConnectionNotice()
	{
		Thread.Sleep(500);
		_client.SendNotification("ExaltHelper", "Connected to ExaltHelper Proxy!");
	}

	public void OnSlotObjectUpdate(SlotObjectData slot)
	{
		if (!_client.Portals.ContainsKey(slot.ObjectId))
		{
			return;
		}
		MapObject mapObject = _client.Portals[slot.ObjectId];
		if (slot.ObjectId == _client.ClientId && slot.SlotId >= 1000000 && slot.SlotId <= 1000002)
		{
			_client.QuickSlotPotions[slot.SlotId - 1000000].Quantity--;
			if (_client.QuickSlotPotions[slot.SlotId - 1000000].Quantity <= 0)
			{
				_client.QuickSlotPotions[slot.SlotId - 1000000].Quantity = 0;
			}
			return;
		}
		if (!GameData.Items.Map.ContainsKey((ushort)slot.ItemType))
		{
			Program.LogWarning("ObjectTracker", $"Item {slot.ItemType} doesn't exist in map");
			return;
		}
		ItemStructure itemStructure = GameData.Items.Map[(ushort)slot.ItemType];
		if (itemStructure == null)
		{
			Program.LogWarning("ObjectTracker", $"Item {slot.ItemType} null");
		}
		else if (itemStructure.IsConsumable)
		{
			mapObject.Inventory[slot.SlotId] = -1;
		}
	}

	public void OnSlotObjectSwap(SlotObjectData slot, SlotObjectData slot2)
	{
		RecordSlotObject(new SlotObjectData(slot.ObjectId, slot.SlotId, slot2.ItemType));
		RecordSlotObject(new SlotObjectData(slot2.ObjectId, slot2.SlotId, slot.ItemType));
	}

	public void OnUpdate(UpdatePacket packet)
	{
		TileData[] Tiles = packet.Tiles;
		foreach (TileData tileData in Tiles)
		{
			_client.Tiles[tileData.X * _mapWidth + tileData.Y] = new MapTile(GameData.Tiles.GetById(tileData.TileType), tileData.X, tileData.Y, tileData.TileType);
		}
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			MapObject mapObject = new MapObject(objectData);
			if (mapObject.IsEnemy)
			{
				if (_client.Enemies.ContainsKey(objectData.Stats.ObjectId))
				{
					_client.Enemies.Remove(objectData.Stats.ObjectId);
				}
				_client.Enemies.Add(objectData.Stats.ObjectId, mapObject);
			}
			else if (mapObject.IsPlayer)
			{
				if (_client.Entities.ContainsKey(objectData.Stats.ObjectId))
				{
					_client.Entities.Remove(objectData.Stats.ObjectId);
				}
				_client.Entities.Add(objectData.Stats.ObjectId, mapObject);
				if (mapObject.ObjectId == _client.ClientId)
				{
					_client.Player = mapObject;
					_client.LoadPetHealingForAccount(mapObject.AccountId);
					UpdateQuickslotPotions(objectData.Stats.StatList);
					_client.playerTrackerMod.UpdateHealthThresholds();
					if (Settings.Default.EnableGlow)
					{
						foreach (StatData item in objectData.Stats.StatList)
						{
							if (item.StatTypeField == StatType.Glowing)
							{
								item.StatValue = 100;
								break;
							}
						}
					}
					else if (Settings.Default.PurpleGlow)
					{
						foreach (StatData item2 in objectData.Stats.StatList)
						{
							if (item2.StatTypeField == StatType.SupporterStat && item2.StatValue == 0)
							{
								item2.StatValue = 1;
								break;
							}
						}
					}
				}
			}
			else if (mapObject.ObjectId == _client.ClientId + 1 && mapObject.ObjectDef.Pet)
			{
				_client.FollowTargetObject = mapObject;
			}
			if (_client.Portals.ContainsKey(objectData.Stats.ObjectId))
			{
				_client.Portals.Remove(objectData.Stats.ObjectId);
			}
			_client.Portals.Add(objectData.Stats.ObjectId, mapObject);
		}
		int[] Drops = packet.Drops;
		foreach (int key in Drops)
		{
			if (_client.Enemies.ContainsKey(key))
			{
				_client.Enemies.Remove(key);
			}
			if (_client.Entities.ContainsKey(key))
			{
				_client.Entities.Remove(key);
			}
			if (_client.Portals.ContainsKey(key))
			{
				_client.Portals.Remove(key);
			}
		}
	}

	private void UpdateQuickslotPotions(IEnumerable<StatData> stats)
	{
		foreach (StatData item in stats)
		{
			if (item.StatTypeField == StatType.Potion1)
			{
				_client.QuickSlotPotions[0] = new PotionInfo(item.StatValue, item.SecondaryStatValue);
			}
			else if (item.StatTypeField == StatType.Potion2)
			{
				_client.QuickSlotPotions[1] = new PotionInfo(item.StatValue, item.SecondaryStatValue);
			}
			else if (item.StatTypeField == StatType.Potion3)
			{
				_client.QuickSlotPotions[2] = new PotionInfo(item.StatValue, item.SecondaryStatValue);
			}
			else if (item.StatTypeField == StatType.PotionBelt)
			{
				_client.HasPotionBelt = true;
			}
		}
	}

	public void OnNewTick(NewTickPacket packet)
	{
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (_client.Portals.ContainsKey(item.ObjectId))
			{
				_client.Portals[item.ObjectId].UpdateFromObjectStatsData(item, packet.TickTime, packet.TickId, packet.TickId, _lastMovementTimestamp);
			}
			if (item.ObjectId == _client.ClientId)
			{
				UpdateQuickslotPotions(item.StatList);
			}
		}
	}

	public void OnMove(MovePacket packet)
	{
		_lastMovementTimestamp = Environment.TickCount;
		if (_client.Player != null)
		{
			_client.Player.Position = packet.Positions.Last().GetWorldPos();
		}
	}

	public void OnServerPlayerShoot(ServerPlayerShootPacket packet)
	{
		if (!_client.Portals.ContainsKey(packet._ownerId))
		{
			return;
		}
		MapObject mapObject = _client.Portals[packet._ownerId];
		if (!Projectile.ObjectTypeToProjectileIdStructureMap.ContainsKey(mapObject.ObjectType))
		{
			Program.LogError("client", $"Unable to find enemy type in map, enemytype: {mapObject.ObjectType} ({mapObject.StructureName})");
			return;
		}
		Dictionary<byte, ProjectileStructure> dictionary = Projectile.ObjectTypeToProjectileIdStructureMap[mapObject.ObjectType];
		if (!dictionary.ContainsKey(packet._bulletType))
		{
			Program.LogError("client", $"Unable to find enemy projectile type in map, enemytype: {mapObject.ObjectType} ({mapObject.StructureName})");
			return;
		}
		Dictionary<int, Projectile> dictionary2;
		if (!_client.Projectiles.ContainsKey(packet._ownerId))
		{
			dictionary2 = new Dictionary<int, Projectile>();
			_client.Projectiles.Add(packet._ownerId, dictionary2);
		}
		else
		{
			dictionary2 = _client.Projectiles[packet._ownerId];
		}
		ProjectileStructure structure = dictionary[packet._bulletType];
		for (int i = 0; i < packet._damage; i++)
		{
			ushort num = (ushort)((packet.BulletId + i) % 65535);
			Projectile value = new Projectile
			{
				Id = num,
				OwnerId = packet._ownerId,
				Damage = packet._angleRaw,
				ProjectileType = packet._bulletType,
				Structure = structure
			};
			if (dictionary2.ContainsKey(num))
			{
				dictionary2[num] = value;
			}
			else
			{
				dictionary2.Add(num, value);
			}
		}
	}

	public void OnInvSwap(InvSwapPacket packet)
	{
		RecordSlotObject(packet.SlotObject1);
		RecordSlotObject(packet.SlotObject2);
	}

	private void RecordSlotObject(SlotObjectData slot)
	{
		if (slot.SlotId >= 1000000 || (_client._currentServerName == "Vault" && slot.ObjectId != _client.ClientId))
		{
			return;
		}
		if (slot.SlotId == -1)
		{
			Program.LogError("client", $"Bad InvResult slot {slot}");
		}
		else
		{
			if (!_client.Portals.ContainsKey(slot.ObjectId))
			{
				return;
			}
			MapObject mapObject = _client.Portals[slot.ObjectId];
			if (mapObject == null)
			{
				return;
			}
			if (slot.SlotId >= mapObject.Inventory.Length)
			{
				int i = mapObject.Inventory.Length;
				Array.Resize(ref mapObject.Inventory, slot.SlotId + 1);
				for (; i < mapObject.Inventory.Length; i++)
				{
					mapObject.Inventory[i] = -1;
				}
			}
			mapObject.Inventory[slot.SlotId] = slot.ItemType;
		}
	}

	public void OnAccountList(AccountListPacket packet)
	{
		_accountIds = packet.AccountIds;
	}

	public void OnGenericFailure(GenericFailurePacket packet)
	{
		if (packet.IsCommand("lefttomax", out var commandArguments))
		{
			MapObject Player = _client.Player;
			ClassStats classStats = ClassStats.Map[_client.Player.ObjectType];
			StringBuilder stringBuilder = new StringBuilder("Your ");
			stringBuilder.Append(Player.ObjectDef.Name);
			stringBuilder.Append("'s stats left to max:\nLife pots remaining: ");
			int num = classStats.LifeMax - Player.MaxHp + Player.HealthBonus;
			int value = (int)Math.Ceiling((double)(int)((double)num * 0.2) + (double)((num % 5 > 0) ? 1 : 0) / 5.0);
			stringBuilder.Append(value);
			stringBuilder.Append("\nMana pots remaining: ");
			int num2 = classStats.ManaMax - Player.MaxMp + Player.ManaBonus;
			int value2 = (int)Math.Ceiling((double)(int)((double)num2 * 0.2) + (double)((num2 % 5 > 0) ? 1 : 0) / 5.0);
			stringBuilder.Append(value2);
			stringBuilder.Append("\nAttack remaining: ");
			stringBuilder.Append(classStats.AttackMax - (Player.Attack - Player.AttackBonus));
			stringBuilder.Append("\nDefense remaining: ");
			stringBuilder.Append(classStats.DefenseMax - (Player.Defense - Player.DefenseBonus));
			stringBuilder.Append("\nSpeed remaining: ");
			stringBuilder.Append(classStats.SpeedMax - (Player.Speed - Player.SpeedBonus));
			stringBuilder.Append("\nDexterity remaining: ");
			stringBuilder.Append(classStats.DexterityMax - (Player.Dexterity - Player.DexterityBonus));
			stringBuilder.Append("\nVitality remaining: ");
			stringBuilder.Append(classStats.VitalityMax - (Player.Vitality - Player.VitalityBonus));
			stringBuilder.Append("\nWisdom remaining: ");
			stringBuilder.Append(classStats.WisdomMax - (Player.Wisdom - Player.WisdomBonus));
			string statsMessage = stringBuilder.ToString();
			_client.SendNotification(statsMessage);
		}
		else if (packet.IsCommand("stats", out commandArguments) && ClassStats.Map.ContainsKey(_client.Player.ObjectType))
		{
			MapObject player = _client.Player;
			StringBuilder stringBuilder2 = new StringBuilder($"Your level {player.Level} ");
			stringBuilder2.Append(player.ObjectDef.Name);
			stringBuilder2.Append("'s current stats:\nLife is: ");
			stringBuilder2.Append(player.MaxHp - player.HealthBonus);
			stringBuilder2.Append("\nMana is: ");
			stringBuilder2.Append(player.MaxMp - player.ManaBonus);
			stringBuilder2.Append("\nAttack is: ");
			stringBuilder2.Append(player.Attack - player.AttackBonus);
			stringBuilder2.Append("\nDefense is: ");
			stringBuilder2.Append(player.Defense - player.DefenseBonus);
			stringBuilder2.Append("\nDexterity is: ");
			stringBuilder2.Append(player.Dexterity - player.DexterityBonus);
			stringBuilder2.Append("\nSpeed is: ");
			stringBuilder2.Append(player.Speed - player.SpeedBonus);
			stringBuilder2.Append("\nVitality is: ");
			stringBuilder2.Append(player.Vitality - player.VitalityBonus);
			stringBuilder2.Append("\nWisdom is: ");
			stringBuilder2.Append(player.Wisdom - player.WisdomBonus);
			if (!string.IsNullOrEmpty(player.CrucibleId))
			{
				stringBuilder2.Append("\n\nWarning: Your character is in the crucible, be sure to take into account the crucible modifiers to your stats.");
			}
			string ClipboardText = stringBuilder2.ToString();
			_client.SendNotification(ClipboardText);
			Thread thread = new Thread((ThreadStart)delegate
			{
				Clipboard.SetText(ClipboardText);
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}
	}

	public void OnVaultContent(VaultContentPacket packet)
	{
		if (packet.VaultContents.Length < 8)
		{
			Array.Resize(ref packet.VaultContents, 8);
			NormalizeEmptyInventorySlots(packet.VaultContents);
		}
		if (packet.GiftContents.Length < 8)
		{
			Array.Resize(ref packet.GiftContents, 8);
			NormalizeEmptyInventorySlots(packet.GiftContents);
		}
		if (packet.PotionContents.Length < 8)
		{
			Array.Resize(ref packet.PotionContents, 8);
			NormalizeEmptyInventorySlots(packet.PotionContents);
		}
		if (_client.Portals.TryGetValue(packet.VaultChestObjectId, out var value))
		{
			if (_isFirstVaultContents)
			{
				value.Inventory = packet.VaultContents;
				_isFirstVaultContents = false;
			}
			else
			{
				value.Inventory = _client.Portals[packet.VaultChestObjectId].Inventory.Concat(packet.VaultContents).ToArray();
			}
		}
		else
		{
			MapObject mapObject = new MapObject(packet.VaultChestObjectId);
			_client.Portals.Add(packet.VaultChestObjectId, mapObject);
			mapObject.Inventory = packet.VaultContents;
		}
		if (_client.Portals.TryGetValue(packet.GiftChestObjectId, out var value2))
		{
			if (_isFirstGiftContents)
			{
				value2.Inventory = packet.GiftContents;
				_isFirstGiftContents = false;
			}
			else
			{
				value2.Inventory = _client.Portals[packet.GiftChestObjectId].Inventory.Concat(packet.GiftContents).ToArray();
			}
		}
		else
		{
			MapObject mapObject2 = new MapObject(packet.GiftChestObjectId);
			_client.Portals.Add(packet.GiftChestObjectId, mapObject2);
			mapObject2.Inventory = packet.GiftContents;
		}
		if (_client.Portals.TryGetValue(packet.PotionStorageObjectId, out var value3))
		{
			if (_isFirstPotionContents)
			{
				value3.Inventory = packet.PotionContents;
				_isFirstPotionContents = false;
			}
			else
			{
				value3.Inventory = _client.Portals[packet.PotionStorageObjectId].Inventory.Concat(packet.PotionContents).ToArray();
			}
		}
		else
		{
			MapObject mapObject3 = new MapObject(packet.PotionStorageObjectId);
			_client.Portals.Add(packet.PotionStorageObjectId, mapObject3);
			mapObject3.Inventory = packet.PotionContents;
		}
	}

	private static void NormalizeEmptyInventorySlots(IList<int> items)
	{
		for (int i = 0; i < items.Count; i++)
		{
			if (items[i] == 0)
			{
				items[i] = -1;
			}
		}
	}
}
