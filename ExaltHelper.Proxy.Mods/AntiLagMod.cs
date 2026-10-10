using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class AntiLagMod
{
	private static readonly Dictionary<ushort, byte> PetObjectTypes;

	private bool _isInPetYard;

	private Client _client;

	private readonly Dictionary<int, int> _originalObjectSizes = new Dictionary<int, int>();

	private int _allyPlayerSize = 100;

	private int _localPlayerSize = 100;

	private bool _objectSizeRefreshPending;

	private bool _applyToGuildmates = Settings.Default.AntiLagApplyToGuildMates;

	private int _petVisibilityMode = -1;

	private bool _hasGuild;

	static AntiLagMod()
	{
		PetObjectTypes = new Dictionary<ushort, byte>();
		foreach (KeyValuePair<ushort, ObjectStructure> item in GameData.Objects.Map)
		{
			if (item.Value.Pet && !PetObjectTypes.ContainsKey(item.Key))
			{
				PetObjectTypes.Add(item.Key, 0);
			}
		}
	}

	public AntiLagMod(Client client)
	{
		_client = client;
	}

	private int GetPetVisibilityMode()
	{
		if (Settings.Default.AntiLagHideAllyPets)
		{
			return 1;
		}
		if (Settings.Default.AntiLagHideNoPets)
		{
			return 2;
		}
		return 3;
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		if (packet.MapName.Contains("Pet Yard"))
		{
			_isInPetYard = true;
		}
	}

	public void OnEnemyShoot(EnemyShootPacket packet)
	{
		if (Settings.Default.BlockDamageNumbers && (packet.ObjectId == _client.PlayerId || packet.OwnerId == _client.PlayerId))
		{
			packet.Send = false;
		}
	}

	public void OnShowEffect(ShowEffectPacket packet)
	{
		if (Settings.Default.AntiLagIgnoreEffects)
		{
			string name = Enum.GetName(typeof(EffectType), packet.EffectType);
			packet.Send = !Settings.Default.AntiLagIgnoredEffects.Contains(name);
		}
	}

	public void OnText(TextPacket packet)
	{
		if (!_hasGuild || _client.Player == null || packet.NumStars != -1 || !(packet.Recipient == "*Guild*") || !packet.Text.Contains(_client.Player.GuildName))
		{
			return;
		}
		if (packet.Text.StartsWith("{\"key\":\"server.guild_join\",\"tokens\":{\"name\":\""))
		{
			string[] array = packet.Text.Split(new string[1] { "{\"key\":\"server.guild_join\",\"tokens\":{\"name\":\"" }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 1)
			{
				array = array[0].Split(new string[1] { "\",\"guild\":\"" }, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length == 2)
				{
					string joinedPlayerName = array[0].ToLower();
					string joinedGuildName = array[1].Substring(0, array[1].Length - 3);
					MarkGuildMemberSizeChanged(joinedPlayerName, joinedGuildName);
				}
			}
		}
		else if (packet.Text.Contains(" has left "))
		{
			string[] array2 = packet.Text.Split(new string[1] { " has left " }, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length == 2)
			{
				string departedPlayerName = array2[0].ToLower();
				string departedGuildName = array2[1];
				MarkGuildMemberSizeChanged(departedPlayerName, departedGuildName);
			}
		}
	}

	public void OnUpdate(UpdatePacket packet)
	{
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (ShouldTrackObjectSize(objectData))
			{
				if (!_originalObjectSizes.ContainsKey(objectData.Stats.ObjectId))
				{
					_originalObjectSizes.Add(objectData.Stats.ObjectId, 100);
				}
				UpdateObjectSizeStat(objectData.Stats);
				_objectSizeRefreshPending = true;
			}
		}
		int[] Drops = packet.Drops;
		foreach (int key in Drops)
		{
			if (_originalObjectSizes.ContainsKey(key))
			{
				_originalObjectSizes.Remove(key);
			}
		}
	}

	private bool IsPetObject(ObjectData objectData)
	{
		return PetObjectTypes.ContainsKey(objectData.ObjectType);
	}

	private bool IsLocalPlayersPet(int objectId)
	{
		return objectId - 1 == _client.ClientId;
	}

	private bool ShouldTrackObjectSize(ObjectData objectData)
	{
		if (!_client.Entities.ContainsKey(objectData.Stats.ObjectId))
		{
			return IsPetObject(objectData);
		}
		return true;
	}

	public void OnNewTick(NewTickPacket packet)
	{
		foreach (ObjectStatsData item in packet.Statuses)
		{
			UpdateObjectSizeStat(item);
		}
		_hasGuild = _client.Player != null && !string.IsNullOrEmpty(_client.Player.GuildName);
		int num = GetPetVisibilityMode();
		bool num2 = Settings.Default.AntiLagApplyToGuildMates != _applyToGuildmates || num != _petVisibilityMode || Settings.Default.AntiLagAllyPlayerSize != _allyPlayerSize || Settings.Default.AntiLagPlayerSize != _localPlayerSize;
		_allyPlayerSize = Settings.Default.AntiLagAllyPlayerSize;
		_localPlayerSize = Settings.Default.AntiLagPlayerSize;
		_applyToGuildmates = Settings.Default.AntiLagApplyToGuildMates;
		_petVisibilityMode = num;
		if (num2)
		{
			foreach (KeyValuePair<int, int> item2 in _originalObjectSizes)
			{
				if (_client.Portals.ContainsKey(item2.Key))
				{
					MapObject mapObject = _client.Portals[item2.Key];
					int StatValue = CalculateDisplaySize(item2.Key, mapObject.ObjectType);
					packet.Statuses.Add(new ObjectStatsData
					{
						Position = mapObject.TargetPosition,
						ObjectId = item2.Key,
						StatList = new List<StatData>
						{
							new StatData
							{
								StatTypeField = StatType.Size,
								StatValue = StatValue,
								StatStringValue = string.Empty,
								SecondaryStatValue = 65
							}
						}
					});
				}
			}
		}
		_objectSizeRefreshPending = false;
	}

	private int CalculateDisplaySize(int objectId, int unusedObjectType)
	{
		if (!_client.Portals.TryGetValue(objectId, out MapObject mapObject) || mapObject == null)
		{
			return _originalObjectSizes.TryGetValue(objectId, out int fallbackVal) ? fallbackVal : 100;
		}
		int num3 = _originalObjectSizes.TryGetValue(objectId, out int sizeVal) ? sizeVal : 100;
		if (mapObject.IsPlayer)
		{
			bool num = !string.IsNullOrEmpty(mapObject.GuildName) && _client.Player != null && mapObject.GuildName == _client.Player.GuildName;
			bool antiLagApplyToGuildMates = Settings.Default.AntiLagApplyToGuildMates;
			int num2 = ((_client.ClientId == objectId) ? Settings.Default.AntiLagPlayerSize : Settings.Default.AntiLagAllyPlayerSize);
			if (num && !antiLagApplyToGuildMates && objectId != _client.ClientId)
			{
				return num3;
			}
			return (int)((double)num2 / 100.0 * (double)num3);
		}
		if (PetObjectTypes.ContainsKey(mapObject.ObjectType))
		{
			if (_isInPetYard)
			{
				return num3;
			}
			if (_petVisibilityMode == 3)
			{
				return 0;
			}
			if (_petVisibilityMode == 2)
			{
				if (IsLocalPlayersPet(objectId))
				{
					if (Settings.Default.AntiLagPlayerSize != 100)
					{
						return Settings.Default.AntiLagPlayerSize;
					}
					return num3;
				}
				if (Settings.Default.AntiLagAllyPlayerSize != 100)
				{
					return Settings.Default.AntiLagAllyPlayerSize;
				}
				return num3;
			}
			if (_petVisibilityMode == 1)
			{
				if (IsLocalPlayersPet(objectId))
				{
					if (Settings.Default.AntiLagPlayerSize != 100)
					{
						return Settings.Default.AntiLagPlayerSize;
					}
					return num3;
				}
				return 0;
			}
		}
		if (Settings.Default.AntiLagAllyPlayerSize != 100)
		{
			return Settings.Default.AntiLagAllyPlayerSize;
		}
		return num3;
	}

	private void UpdateObjectSizeStat(ObjectStatsData status)
	{
		foreach (StatData item in status.StatList)
		{
			if (!(item.StatTypeField == StatType.Size))
			{
				continue;
			}
			if (_originalObjectSizes.ContainsKey(status.ObjectId))
			{
				_originalObjectSizes[status.ObjectId] = item.StatValue;
				if (Settings.Default.AntiLagAllyPlayerSize != 100)
				{
					item.StatValue = CalculateDisplaySize(status.ObjectId, -1);
				}
			}
			break;
		}
	}

	[CompilerGenerated]
	private void MarkGuildMemberSizeChanged(string playerName, string unusedGuildName)
	{
		foreach (KeyValuePair<int, MapObject> item in _client.Entities)
		{
			if (!(item.Value.Name.ToLower() != playerName))
			{
				_objectSizeRefreshPending = true;
				break;
			}
		}
	}
}
