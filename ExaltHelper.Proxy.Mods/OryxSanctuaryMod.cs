using System.Collections.Generic;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class OryxSanctuaryMod
{
	private const int OryxObjectType = 45363;

	private const int FirstTreasureShuffleObjectType = 8701;

	private const int SecondTreasureShuffleObjectType = 8702;

	private const int ThirdTreasureShuffleObjectType = 8703;

	private const int MorningStarObjectType = 9635;

	private readonly List<string> DammahSpeechQuotes = new List<string> { "No more! A steep price is to be paid for this brazen insolence in the face of my own grandeur!", "Greetings, dogged peons! I am Dammah, and I shall be your unmaker!", "Ahem... Your uprising ends here. Lay down your feeble weapons and accept death.", "Do NOT interrupt me, impatient ones!", "I SAID DO NOT INTERRUPT ME! For this I shall hasten your end!" };

	private readonly List<int> OryxShieldStateHashes = new List<int> { -935464302, -918686683 };

	private Client _client;

	private bool _isInSanctuary;

	private int _oryxObjectId = -1;

	private int _firstTreasureShuffleId = -1;

	private int _secondTreasureShuffleId = -1;

	private int _thirdTreasureShuffleId = -1;

	private bool _isOryxShieldActive;

	private int _flashingTreasureShuffleId = -1;

	private bool _isDammahSpeechActive;

	private int _morningStarObjectId = -1;

	public OryxSanctuaryMod(Client client)
	{
		_client = client;
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		_isInSanctuary = packet.MapName == "Oryx's Sanctuary";
	}

	public void OnEnemyHit(EnemyHitPacket packet)
	{
		if (!ShouldAllowHit(packet.OwnerId))
		{
			packet.Send = false;
		}
	}

	public void OnShowEffect(ShowEffectPacket packet)
	{
		if (_isInSanctuary && Settings.Default.EnableO3Helper && packet.EffectType == EffectType.Flash && _client.Portals.ContainsKey(packet.OwnerId))
		{
			MapObject mapObject = _client.Portals[packet.OwnerId];
			if (mapObject != null && (mapObject.ObjectType == 8701 || mapObject.ObjectType == 8702 || mapObject.ObjectType == 8703) && packet.TargetPos.Y == 5.0 && packet.Color.A == 0 && packet.Color.A == 0 && packet.Color.A == 0 && packet.Color.A == 0)
			{
				_flashingTreasureShuffleId = packet.OwnerId;
			}
		}
	}

	public void OnUpdate(UpdatePacket packet)
	{
		if (!_isInSanctuary)
		{
			return;
		}
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (objectData.ObjectType == 45363)
			{
				_oryxObjectId = objectData.Stats.ObjectId;
				OnObjectStatsUpdate(objectData.Stats);
				break;
			}
			if (_oryxObjectId == -1)
			{
				if (objectData.ObjectType == 8701)
				{
					_firstTreasureShuffleId = objectData.Stats.ObjectId;
				}
				else if (objectData.ObjectType == 8702)
				{
					_secondTreasureShuffleId = objectData.Stats.ObjectId;
				}
				else if (objectData.ObjectType == 8703)
				{
					_thirdTreasureShuffleId = objectData.Stats.ObjectId;
				}
				else if (objectData.ObjectType == 9635)
				{
					_morningStarObjectId = objectData.Stats.ObjectId;
				}
			}
		}
	}

	public void OnNewTick(NewTickPacket packet)
	{
		if (!_isInSanctuary)
		{
			return;
		}
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (item.ObjectId == _oryxObjectId)
			{
				OnObjectStatsUpdate(item);
			}
		}
	}

	private void OnObjectStatsUpdate(ObjectStatsData status)
	{
		foreach (StatData item in status.StatList)
		{
			if (!(item.StatTypeField != StatType.UnknownStat125))
			{
				_isOryxShieldActive = OryxShieldStateHashes.Contains(item.StatValue);
				break;
			}
		}
	}

	public void OnText(TextPacket packet)
	{
		if (_isInSanctuary && packet.NumStars <= -1 && !(packet.Name != "#Chancellor Dammah"))
		{
			_isDammahSpeechActive = DammahSpeechQuotes.Contains(packet.Text);
		}
	}

	public void OnUseItem(UseItemPacket packet)
	{
		if (_isInSanctuary && _isOryxShieldActive && packet.Item.ObjectId == _client.ClientId && packet.Item.SlotId == 1)
		{
			packet.Send = false;
		}
	}

	public bool ShouldBlockAbilityUse()
	{
		if (!_isInSanctuary)
		{
			return false;
		}
		if (!Settings.Default.EnableO3Helper)
		{
			return false;
		}
		if (Settings.Default.O3IgnoreShield && _isOryxShieldActive)
		{
			return true;
		}
		if (Settings.Default.O3IgnoreDammah && _isDammahSpeechActive)
		{
			return true;
		}
		return false;
	}

	private bool ShouldAllowHit(int objectId)
	{
		if (!_isInSanctuary)
		{
			return true;
		}
		if (!Settings.Default.EnableO3Helper)
		{
			return true;
		}
		if (Settings.Default.O3IgnoreShield && objectId == _oryxObjectId && _isOryxShieldActive)
		{
			return false;
		}
		if (Settings.Default.O3IgnoreCoins)
		{
			if (objectId == _flashingTreasureShuffleId)
			{
				return true;
			}
			if (objectId != _firstTreasureShuffleId && objectId != _secondTreasureShuffleId)
			{
				return objectId != _thirdTreasureShuffleId;
			}
			return false;
		}
		if (Settings.Default.O3IgnoreDammah && _isDammahSpeechActive && objectId == _morningStarObjectId)
		{
			return false;
		}
		return true;
	}
}
