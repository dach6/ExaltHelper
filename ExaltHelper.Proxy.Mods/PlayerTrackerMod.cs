using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class PlayerTrackerMod
{
	[CompilerGenerated]
	private static class PlayerTrackingHelper
	{
		public static Func<string, IEnumerable<int>> StatParseFunc;
	}

	private const int MinDamageInterval = 500;

	private readonly Client _client;

	private readonly List<AoeDamageInfo> _aoeDamageList = new List<AoeDamageInfo>();

	public bool _hasNexused;

	private int _nexusHpThreshold;

	private int _autoPotHpThreshold;

	private int _autoPotMpThreshold;

	private int _clientHp = 100;

	private int _serverHp = 100;

	private float _fractionalHealthChange;

	private int _lastRegenerationTime;

	private int _lastHealthPotionTime;

	private int _lastManaPotionTime;

	private bool _inSafeMap;

	private bool _usesOxygen;

	private WorldPosData _lastPosition = WorldPosData.Zero;

	private bool _isInCombat;

	private int _pendingHealAmount;

	private int _flatRegenerationBonus;

	private float _percentHealthRegenerationBonus;

	private float _estimatedHpScale = 1f;

	private float _healingEffectRate;

	private int _targetEntityId = -1;

	private readonly List<string> _safeMaps = new List<string>
	{
		"Nexus", "Vault", "Guild Hall", "Guild Hall 2", "Guild Hall 3", "Guild Hall 4", "Guild Hall 5", "Cloth Bazaar", "Nexus Explanation", "Vault Explanation",
		"Guild Explanation", "Daily Quest Room", "Daily Login Room", "Pet Yard", "Pet Yard 2", "Pet Yard 3", "Pet Yard 4", "Pet Yard 5"
	};

	private readonly Dictionary<int, ushort> _projectileOwners = new Dictionary<int, ushort>();

	private int _lastSyncTickId = int.MinValue;

	public PlayerTrackerMod(Client client)
	{
		_client = client;
	}

	private void TriggerAutoNexus(int damage, string source = "Unknown")
	{
		if (_hasNexused)
		{
			_client.SendToServer(new PongPacket());
			return;
		}
		_hasNexused = true;
		if (Settings.Default.AutoNexusShowInformation)
		{
			int num = (_client.Player != null && _client.Player.MaxHp > 0) ? (_clientHp * 100 / _client.Player.MaxHp) : 0;
			_client.SendNotification("AutoNexus", $"AutoNexused at {num}% HP\nSource: {damage} {source}");
		}
		Program.LogError("client", $"Autonexusing from {source}, {damage}");
		try
		{
			System.IO.File.AppendAllText("disconnect_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [AutoNexus Triggered] Source={source} Damage={damage} ClientHp={_clientHp} ServerHp={_serverHp} Threshold={_nexusHpThreshold}\r\n");
		}
		catch
		{
		}
		_client.SendToServer(new PongPacket());
		if (Settings.Default.AutoNexusInstantNexus)
		{
			_client.ReconnectToNexus();
		}
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		_inSafeMap = _safeMaps.Contains(packet.MapName);
		_usesOxygen = false;
		_hasNexused = false;
		_clientHp = (_client?.Player?.Hp > 0) ? _client.Player.Hp : 100;
		_serverHp = (_client?.Player?.Hp > 0) ? _client.Player.Hp : 100;
		_projectileOwners.Clear();
		_aoeDamageList.Clear();
		UpdateHealthThresholds();
	}

	public void CheckAutoNexusEnabled()
	{
		UpdateHealthThresholds();
		if (!Settings.Default.EnableAutoNexus || !Settings.Default.EnableAutoNexusOnly)
		{
			_client.SendNotification("Warning, autonexus is disabled!");
		}
		else
		{
			_client.SendNotification("AutoNexus", $"AutoNexus active at {Settings.Default.AutoNexusPercentageThreshold}% HP ({_nexusHpThreshold} HP)");
		}
	}

	public void OnDeath()
	{
		Program.LogWarning("client", $"We died at {_clientHp} health, when nexusing at {Settings.Default.AutoNexusPercentageThreshold}% ({_nexusHpThreshold} hp)!");
	}

	public void OnUpdateNewObjects(UpdatePacket packet)
	{
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (objectData.Stats.ObjectId == _client.ClientId)
			{
				UpdatePlayerStats(objectData.Stats, updateCurrentHealth: true);
				break;
			}
		}
	}

	public void OnUpdateExistingObjects(UpdatePacket packet)
	{
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (objectData.Stats.ObjectId == _client.ClientId)
			{
				OnObjectStats(objectData.Stats, updatePredictedHealth: true);
			}
			if (!_projectileOwners.ContainsKey(objectData.Stats.ObjectId))
			{
				_projectileOwners.Add(objectData.Stats.ObjectId, objectData.ObjectType);
			}
		}
	}

	public void OnNewTickAuthoritative(NewTickPacket packet)
	{
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (item.ObjectId == _client.ClientId)
			{
				UpdatePlayerStats(item);
				break;
			}
		}
	}

	public void OnNewTickPredicted(NewTickPacket packet)
	{
		bool flag = false;
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (item.ObjectId == _client.ClientId)
			{
				OnObjectStats(item);
				flag = true;
				break;
			}
		}
		if (Settings.Default.AutoNexusSyncHp && Math.Abs(_clientHp - _serverHp) > 30 && packet.TickId - _lastSyncTickId > 5)
		{
			_client.SendNotification($"Synced client HP with server ({_clientHp} -> {_serverHp})");
			_clientHp = _serverHp;
		}
		if (_pendingHealAmount != 0)
		{
			_clientHp += _pendingHealAmount;
			if (_client.Player != null && _clientHp > _client.Player.MaxHp)
			{
				_clientHp = _client.Player.MaxHp;
			}
			_pendingHealAmount = 0;
		}
		UpdateHealthThresholds();
	}

	public void OnShowEffect(ShowEffectPacket packet)
	{
		if (_client.Player == null || packet.EffectType != EffectType.Nova || !_client.Entities.ContainsKey(packet.OwnerId))
		{
			return;
		}
		MapObject mapObject = _client.Entities[packet.OwnerId];
		if (mapObject.Inventory[1] == -1)
		{
			return;
		}
		IEnumerable<Activate> source = GameData.Items.GetById((ushort)mapObject.Inventory[1]).Activations.Where((Activate activate2) => activate2.Name == ActivateType.ConditionEffectAura);
		if (source.Any())
		{
			Activate activate = source.First();
			if (activate.Color == packet.Color.Value && !(mapObject.TargetPosition.DistanceSquaredTo(_client.Player.TargetPosition) > packet.TargetPos.X * packet.TargetPos.X))
			{
				int num = (int)((float)mapObject.Wisdom * _estimatedHpScale);
				int num2 = ((num > activate.StatModScalingMinimum) ? (activate.Amount + (int)((float)(num - activate.StatModScalingMinimum) * activate.StatModAmount)) : activate.Amount);
				_healingEffectRate = num2;
			}
		}
	}

	private void UpdatePlayerStats(ObjectStatsData status, bool updateCurrentHealth = false)
	{
		foreach (StatData item in status.StatList)
		{
			if (item.StatTypeField == StatType.MaximumHP)
			{
				if (_client?.Player != null)
				{
					_client.Player.MaxHp = item.StatValue;
				}
				ClampPredictedHealth(item.StatValue);
			}
			else if ((item.StatTypeField == StatType.HP) & updateCurrentHealth)
			{
				if (_client?.Player != null)
				{
					_client.Player.Hp = item.StatValue;
				}
				_clientHp = item.StatValue;
				_serverHp = item.StatValue;
				ClampPredictedHealth(item.StatValue);
			}
		}
	}

	private void OnObjectStats(ObjectStatsData status, bool updatePredictedHealth = false)
	{
		foreach (StatData item in status.StatList)
		{
			if (item.StatTypeField == StatType.MaximumHP)
			{
				UpdateHealthThresholds();
			}
			if (item.StatTypeField == StatType.HP)
			{
				if (updatePredictedHealth)
				{
					_clientHp = item.StatValue;
					ClampPredictedHealth(item.StatValue);
				}
				_serverHp = item.StatValue;
			}
			else if (item.StatTypeField == StatType.Effects)
			{
				_isInCombat = (item.StatValue & 0x100000) == 1048576;
			}
			else if (item.StatTypeField == StatType.OxygenBar && item.StatValue > 0)
			{
				_usesOxygen = true;
			}
			else if (item.StatTypeField == StatType.ItemData)
			{
				UpdateEnchantmentManaCostMultiplier(item.StatStringValue);
			}
			else if (item.StatTypeField == StatType.AltTextureIndex)
			{
				if (Settings.Default.HideBattlepassXp)
				{
					item.StatValue = 0;
				}
			}
			else if (item.StatTypeField == StatType.Inventory0)
			{
				UpdateEquipmentManaCostMultiplier(0, item.StatValue);
			}
			else if (item.StatTypeField == StatType.Inventory1)
			{
				UpdateEquipmentManaCostMultiplier(1, item.StatValue);
			}
			else if (item.StatTypeField == StatType.Inventory2)
			{
				UpdateEquipmentManaCostMultiplier(2, item.StatValue);
			}
			else if (item.StatTypeField == StatType.Inventory3)
			{
				UpdateEquipmentManaCostMultiplier(3, item.StatValue);
			}
			else if (item.StatTypeField == StatType.UnknownStat150)
			{
				_targetEntityId = item.StatValue;
			}
		}
	}

	private void UpdateEquipmentManaCostMultiplier(int equipmentSlot, int itemType)
	{
		if (_client.Player == null)
		{
			return;
		}
		int[] array = _client.Player.Inventory.Take(4).ToArray();
		array[equipmentSlot] = itemType;
		float manaCostMultiplier = (from num in array
			where num != -1
			select GameData.Items.GetById((ushort)num)).Aggregate(1f, (float num, ItemStructure itemStructure2) => num * itemStructure2.AbilityUseDiscount);
		_client.SetEquipmentManaCostMultiplier(manaCostMultiplier);
	}

	private void UpdateEnchantmentManaCostMultiplier(string itemData)
	{
		if (string.IsNullOrEmpty(itemData))
		{
			return;
		}
		IEnumerable<int> enumerable = (from value in itemData.Split(',').Take(4)
			where !string.IsNullOrEmpty(value)
			select value).Select(Client.ParseStatValues).SelectMany((IEnumerable<int> result) => result);
		_flatRegenerationBonus = 0;
		_percentHealthRegenerationBonus = 0f;
		float num = 1f;
		float num2 = 1f;
		foreach (int item in enumerable)
		{
			switch (item)
			{
			case 423:
				num2 *= 0.96f;
				break;
			case 424:
				num2 *= 0.94f;
				break;
			case 425:
				num2 *= 0.92f;
				break;
			case 426:
				num2 *= 0.9f;
				break;
			case 1505:
				num *= 1.06f;
				break;
			case 1506:
				num *= 1.09f;
				break;
			case 1507:
				num *= 1.12f;
				break;
			case 1508:
				num *= 1.15f;
				break;
			case 1795:
				num *= 0.96f;
				break;
			case 1794:
				num *= 0.89f;
				break;
			case 1793:
				num *= 0.82f;
				break;
			case 1792:
				num *= 0.75f;
				break;
			case 45:
				_flatRegenerationBonus += 4;
				break;
			case 380:
				_flatRegenerationBonus += 6;
				break;
			case 381:
				_flatRegenerationBonus += 8;
				break;
			case 382:
				_flatRegenerationBonus += 10;
				break;
			case 53:
				_percentHealthRegenerationBonus += 0.005f;
				break;
			case 412:
				_percentHealthRegenerationBonus += 0.0075f;
				break;
			case 413:
				_percentHealthRegenerationBonus += 0.01f;
				break;
			case 414:
				_percentHealthRegenerationBonus += 0.0125f;
				break;
			case 1605:
				_flatRegenerationBonus += 6;
				break;
			case 1607:
				_flatRegenerationBonus += 8;
				break;
			case 1610:
				num2 *= 0.94f;
				break;
			case 1598:
				num2 *= 0.9f;
				break;
			case 1602:
				num2 *= 0.9f;
				break;
			case 430:
				num2 *= 0.92f;
				break;
			case 451:
				num2 *= 0.95f;
				break;
			}
		}
		_percentHealthRegenerationBonus *= 2f;
		_flatRegenerationBonus *= 2;
		_estimatedHpScale = num;
		_client.SetEnchantmentManaCostMultiplier(num2);
	}

	private void ApplyStatUpdates(List<StatData> stats)
	{
	}

	public void OnMove(MovePacket packet)
	{
		if (_hasNexused)
		{
			packet.Send = false;
		}
		int SessionDurationMs = _client.SessionDurationMs;
		MoveRecord moveRecord = packet.Positions.Last();
		WorldPosData worldPosData = moveRecord.GetWorldPos();
		if (packet.TickId == 0)
		{
			_lastRegenerationTime = SessionDurationMs;
			LogDamageEvent(moveRecord.Time, SessionDurationMs, 0, worldPosData);
		}
		else
		{
			LogDamageEvent(moveRecord.Time, SessionDurationMs, SessionDurationMs - _lastRegenerationTime, worldPosData);
			_lastRegenerationTime = SessionDurationMs;
		}
		TryAutoDrinkManaPotion(moveRecord.Time, SessionDurationMs, worldPosData);
		_lastPosition = moveRecord.GetWorldPos();
	}

	public void OnNotification(NotificationPacket packet)
	{
		if (_client?.Player == null || packet.ObjectId != _client.ClientId || !packet.Message.Contains("s.plus_symbol") || packet.Color != 65280)
		{
			return;
		}
		string[] array = packet.Message.Split(new string[1] { "\"amount\":\"" }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length != 2)
		{
			Program.LogError("client", "Malformed notification message: " + packet.Message);
			return;
		}
		string[] array2 = array[1].Split(new string[1] { "\",}}" }, StringSplitOptions.RemoveEmptyEntries);
		int result;
		if (array2.Length != 1)
		{
			Program.LogError("client", "Malformed notification message: " + packet.Message);
		}
		else if (!int.TryParse(array2[0], out result))
		{
			Program.LogError("client", "Malformed notification message: " + packet.Message);
		}
		else if (result + _clientHp > _client.Player.MaxHp)
		{
			QueuePredictedHealing(result);
		}
		else
		{
			ApplyPredictedHealing(result, "Notif Heal");
		}
	}

	public void OnAoe(AoePacket packet)
	{
		_aoeDamageList.Add(new AoeDamageInfo(packet));
	}

	public void OnGotoAck(GotoAckPacket packet)
	{
		if (_hasNexused)
		{
			packet.Send = false;
		}
		_lastPosition = packet.Position;
		if (_client.Player == null)
		{
			_aoeDamageList.Clear();
			return;
		}
		foreach (AoeDamageInfo item in _aoeDamageList)
		{
			if (_lastPosition.DistanceSquaredTo(item.Position) < (double)(item.Radius * item.Radius))
			{
				string text = $"(Type: {item.EffectType})";
				ObjectStructure objectStructure = GameData.Objects.GetById(item.EffectType);
				if (objectStructure != null)
				{
					text = objectStructure.Name;
				}
				ApplyPredictedDamage(CalculateReceivedDamage(item.Damage, _client.Player.Defense, item.Duration, _client.Player.DamageMultiplier), "AoE damage (" + text + ")");
			}
		}
		_aoeDamageList.Clear();
	}

	public void OnGroundDamage(GroundDamagePacket packet)
	{
		if (_hasNexused)
		{
			packet.Send = false;
		}
		if (_client.Player == null)
		{
			return;
		}
		MapTile mapTile = _client.GetTileAtPosition(packet.Position);
		if (mapTile == null)
		{
			Program.LogError("client", $"Null tile at {packet.Position} when taking GroundDamage");
			_client.SendNotification("AutoNexus", "Took ground damage from an unknown source, autonexus may not be fully functional!");
		}
		else
		{
			ApplyPredictedDamage((int)((float)(int)mapTile.TileStructure.MaxDamage * ((float)_client.Player.DamageMultiplier / 1000f)), "Ground damage (" + mapTile.TileStructure.Name + ")");
		}
	}

	public void OnShootAck(ShootAckPacket packet)
	{
		if (_hasNexused)
		{
			packet.Send = false;
		}
		if (_client.Player == null)
		{
			return;
		}
		string text = packet.ObjectId.ToString();
		if (_projectileOwners.ContainsKey(packet.ObjectId))
		{
			text = GameData.Objects.GetById(_projectileOwners[packet.ObjectId]).Name;
		}
		if (!_client.Projectiles.ContainsKey(packet.ObjectId))
		{
			Program.LogError("client", $"We got hit by a shot that is not logged, owner: {text}, bid: {packet.BulletId}");
			_client.SendNotification("AutoNexus", "Took projectile damage from an unknown source, autonexus may not be fully functional!");
			return;
		}
		Dictionary<int, Projectile> dictionary = _client.Projectiles[packet.ObjectId];
		if (dictionary.ContainsKey(packet.BulletId))
		{
			Projectile projectile = dictionary[packet.BulletId];
			bool armorPiercing = Projectile.IsPiercing(_projectileOwners[projectile.OwnerId], projectile.ProjectileType);
			int num = CalculateReceivedDamage((ushort)projectile.Damage, _client.Player.Defense, armorPiercing, _client.Player.DamageMultiplier);
			if (num > 0)
			{
				_ = _client.Player.CrucibleId == "6396203608506368";
			}
			ApplyPredictedDamage(num, "Projectile damage (" + text + ")");
		}
	}

	private int CalculateReceivedDamage(ushort damage, int defense, bool armorPiercing, int damageMultiplier)
	{
		if (_client.Player == null)
		{
			return 0;
		}
		int num = defense;
		if (armorPiercing || _client.Player.IsArmorBroken())
		{
			num = 0;
		}
		else if (_client.Player.IsArmored())
		{
			num = (int)((double)num * 1.5);
		}
		if (_client.Player.IsDarkness())
		{
			num -= 20;
		}
		float num2 = (int)damage;
		float val = num2 * 0.1f;
		float val2 = num2 - (float)num;
		num2 = Math.Max(val, val2);
		float num3 = (float)damageMultiplier / 1000f;
		num2 *= num3;
		if (_client.Player.IsInvulnerable())
		{
			num2 = 0f;
		}
		if (_client.Player.IsSilenced())
		{
			num2 = (int)(num2 * 0.9f);
		}
		if (_client.Player.IsExposed())
		{
			num2 = (int)(num2 * 1.25f);
		}
		return (int)num2;
	}

	private void LogDamageEvent(int time, int potionTime, int elapsedMilliseconds, WorldPosData position)
	{
		int num = _clientHp;
		ApplyHealthRegeneration(elapsedMilliseconds);
		if (ShouldTriggerNexus())
		{
			TriggerAutoNexus(num - _clientHp, "(Health drain)");
		}
		else
		{
			TryAutoDrinkHealthPotion(time, potionTime, position);
		}
	}

	private void TryAutoDrinkManaPotion(int time, int potionTime, WorldPosData position)
	{
		if (_client.Player == null)
		{
			return;
		}
		if (!_inSafeMap && Settings.Default.EnableAutoNexus && Settings.Default.EnableAutoPotMP && !_client.Player.IsQuiet() && !_client.Player.IsHexed() && _client.Player.Mp <= _autoPotMpThreshold && potionTime - _lastManaPotionTime > 500)
		{
			UseManaPotion(time, potionTime, position);
		}
	}

	private void TryAutoDrinkHealthPotion(int time, int potionTime, WorldPosData position)
	{
		if (_client.Player == null)
		{
			return;
		}
		if (!_inSafeMap && Settings.Default.EnableAutoNexus && Settings.Default.EnableAutoPotHP && !_client.Player.IsSick() && !_client.Player.IsEnergized() && _client.Player.Hp <= _autoPotHpThreshold && _clientHp <= _autoPotHpThreshold && _serverHp <= _autoPotHpThreshold && potionTime - _lastHealthPotionTime > Settings.Default.AutoNexusHpPotDelay)
		{
			UseHealthPotion(time, potionTime, position);
		}
	}

	private void UseHealthPotion(int time, int potionTime, WorldPosData position)
	{
		if (_client.Player == null)
		{
			return;
		}
		SlotObjectData slotObjectData = null;
		if (Settings.Default.AutoNexusDrinkFromInventory)
		{
			for (int i = 0; i < _client.Player.Inventory.Length; i++)
			{
				if (IsHealthPotion(_client.Player.Inventory[i]))
				{
					slotObjectData = new SlotObjectData(_client.ClientId, i, _client.Player.Inventory[i]);
					break;
				}
			}
		}
		if (slotObjectData == null)
		{
			for (int j = 0; j < _client.QuickSlotPotions.Length; j++)
			{
				PotionInfo potionInfo = _client.QuickSlotPotions[j];
				if (potionInfo.Quantity > 0 && IsHealthPotion(potionInfo.Type))
				{
					slotObjectData = new SlotObjectData(_client.ClientId, 1000000 + j, potionInfo.Type);
					break;
				}
			}
		}
		if (slotObjectData != null)
		{
			SendPotionUse(time, slotObjectData, position);
			_lastHealthPotionTime = potionTime;
		}
	}

	private void UseManaPotion(int time, int potionTime, WorldPosData position)
	{
		SlotObjectData slotObjectData = null;
		if (Settings.Default.AutoNexusDrinkFromInventory)
		{
			for (int i = 0; i < _client.Player.Inventory.Length; i++)
			{
				if (IsManaPotion(_client.Player.Inventory[i]))
				{
					slotObjectData = new SlotObjectData(_client.ClientId, i, _client.Player.Inventory[i]);
					break;
				}
			}
		}
		if (slotObjectData == null)
		{
			for (int j = 0; j < _client.QuickSlotPotions.Length; j++)
			{
				PotionInfo potionInfo = _client.QuickSlotPotions[j];
				if (potionInfo.Quantity > 0 && IsManaPotion(potionInfo.Type))
				{
					slotObjectData = new SlotObjectData(_client.ClientId, 1000000 + j, potionInfo.Type);
					break;
				}
			}
		}
		if (slotObjectData != null)
		{
			SendPotionUse(time, slotObjectData, position);
			_lastManaPotionTime = potionTime;
		}
	}

	private bool IsHealthPotion(int itemType)
	{
		return Client.HealthPotionTypes.Contains(itemType);
	}

	private bool IsManaPotion(int itemType)
	{
		return Client.ManaPotionTypes.Contains(itemType);
	}

	private void SendPotionUse(int time, SlotObjectData item, WorldPosData position)
	{
		UseItemPacket useItemPacket = new UseItemPacket();
		useItemPacket.Item = item;
		useItemPacket.Time = time;
		useItemPacket.ItemUsePos = position;
		useItemPacket.UseType = ((item.SlotId < 1000000) ? ((byte)1) : ((byte)0));
		_client.SendToServer(useItemPacket);
		_client.entityTrackerMod.OnSlotObjectUpdate(item);
	}

	private void ApplyHealthRegeneration(int elapsedMilliseconds)
	{
		if (_client.Player == null)
		{
			return;
		}
		float num = (float)elapsedMilliseconds * 0.001f;
		float num2 = 2f * (1f + 0.12f * (float)_client.Player.Vitality);
		num2 += _percentHealthRegenerationBonus * (float)_client.Player.MaxHp;
		num2 += (float)_flatRegenerationBonus;
		if (_isInCombat)
		{
			num2 /= 2f;
		}
		bool num3 = (_usesOxygen && _client.Player.OxygenBar == 0) || _targetEntityId >= 100;
		if (!_client.Player.IsSick())
		{
			if (_client.Player.IsHealing())
			{
				_fractionalHealthChange += (_healingEffectRate + num2) * num;
			}
			else
			{
				_fractionalHealthChange += num2 * num;
			}
		}
		if (_client.Player.IsBleeding())
		{
			_fractionalHealthChange -= 20f * num;
		}
		if (num3)
		{
			_fractionalHealthChange -= 96f * num;
		}
		int num4 = (int)_fractionalHealthChange;
		float num5 = _fractionalHealthChange - (float)num4;
		_fractionalHealthChange = num5;
		_clientHp += num4;
		if (_client.Player != null && _clientHp > _client.Player.MaxHp)
		{
			_clientHp = _client.Player.MaxHp;
		}
	}

	public void UpdateHealthThresholds()
	{
		if (_client?.Player == null)
		{
			return;
		}
		if (_client.Player.MaxHp > 0)
		{
			_nexusHpThreshold = (int)((float)Settings.Default.AutoNexusPercentageThreshold * 0.01f * (float)_client.Player.MaxHp);
			_autoPotHpThreshold = (int)((float)Settings.Default.AutoNexusDrinkThreshold * 0.01f * (float)_client.Player.MaxHp);
			_autoPotMpThreshold = (int)((float)Settings.Default.AutoNexusDrinkMpThreshold * 0.01f * (float)_client.Player.MaxMp);
			if (_clientHp <= 100 && _client.Player.Hp > 100)
			{
				_clientHp = _client.Player.Hp;
			}
			if (_serverHp <= 100 && _client.Player.Hp > 100)
			{
				_serverHp = _client.Player.Hp;
			}
		}
	}

	private bool ShouldTriggerNexus()
	{
		if (_inSafeMap)
		{
			return false;
		}
		if (!Settings.Default.EnableAutoNexus || !Settings.Default.EnableAutoNexusOnly)
		{
			return false;
		}
		if (_client?.Player == null)
		{
			return false;
		}
		if (Settings.Default.AutoNexusUseClientHp && _clientHp <= _nexusHpThreshold)
		{
			return true;
		}
		if (_client.Player.Hp <= _nexusHpThreshold || _serverHp <= _nexusHpThreshold)
		{
			return true;
		}
		return false;
	}

	private void ClampPredictedHealth(int healthLimit)
	{
		if (_clientHp > healthLimit)
		{
			_clientHp = healthLimit;
		}
		UpdateHealthThresholds();
	}

	private void QueuePredictedHealing(int amount)
	{
		_pendingHealAmount = amount;
	}

	private void ApplyPredictedHealing(int amount, string source)
	{
		_clientHp += amount;
		if (_client.Player != null && _clientHp > _client.Player.MaxHp)
		{
			_clientHp = _client.Player.MaxHp;
		}
	}

	private void ApplyPredictedDamage(int damage, string source)
	{
		_lastSyncTickId = _client._clientTime;
		_clientHp -= damage;
		_serverHp -= damage;
		if (ShouldTriggerNexus())
		{
			TriggerAutoNexus(damage, source);
		}
	}

	public void OnGenericFailure(GenericFailurePacket packet)
	{
		string[] commandArguments;
		if (packet.IsCommand("reset", out var _))
		{
			if (_client.Player != null)
			{
				_clientHp = _client.Player.Hp;
			}
		}
		else if (packet.IsCommand("an", out commandArguments))
		{
			int result;
			if (commandArguments.Length == 0)
			{
				_client.SendNotification($"Autonexus health threshold currently {Settings.Default.AutoNexusPercentageThreshold}% (at {_nexusHpThreshold} health)");
			}
			else if (int.TryParse(commandArguments[0], out result))
			{
				Settings.Default.AutoNexusPercentageThreshold = CommonUtils.Clamp(1, 100, result);
				Settings.Default.Change();
				UpdateHealthThresholds();
				_client.SendNotification($"Set autonexus health threshold to {result}% (at {_nexusHpThreshold} health)");
			}
		}
	}

	public void CleanupTrackingState()
	{
		if (_client != null && _client.ClientId != -1 && _client.Player != null)
		{
			_client.SendNotification($"Reset client HP {_clientHp} to server HP {_client.Player.Hp}");
			_clientHp = _client.Player.Hp;
		}
	}
}
