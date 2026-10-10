using System.Collections.Generic;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class AntiDebuffMod
{
	private readonly Client _client;

	private readonly HashSet<string> _blockedProjectileEffects = new HashSet<string>();

	private bool _wasInsideAoe;

	public AntiDebuffMod(Client client)
	{
		_client = client;
	}

	public void OnNewTick(NewTickPacket packet)
	{
		if (!Settings.Default.EnableAntiDebuffs || _client?.Player == null)
		{
			return;
		}
		ObjectStatsData objectStatsData = null;
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (item.ObjectId == _client.Player.ObjectId)
			{
				objectStatsData = item;
				break;
			}
		}
		if (objectStatsData == null)
		{
			objectStatsData = new ObjectStatsData
			{
				StatList = new List<StatData>(),
				ObjectId = _client.ClientId,
				Position = _client.Player.TargetPosition
			};
			objectStatsData.StatList.Add(CreatePrimaryEffectsStat());
			objectStatsData.StatList.Add(CreateSecondaryEffectsStat());
			packet.Statuses.Add(objectStatsData);
		}
		FilterConditionEffects(objectStatsData.StatList);
	}

	private StatData CreatePrimaryEffectsStat()
	{
		return new StatData
		{
			StatTypeField = StatType.Effects,
			StatValue = _client.Player?.Effects ?? 0,
			StatStringValue = string.Empty,
			SecondaryStatValue = 65
		};
	}

	private StatData CreateSecondaryEffectsStat()
	{
		return new StatData
		{
			StatTypeField = StatType.Effects2,
			StatValue = _client.Player?.Effects2 ?? 0,
			StatStringValue = string.Empty,
			SecondaryStatValue = 65
		};
	}

	public void OnAoe(AoePacket packet)
	{
		if (_client?.Player == null)
		{
			return;
		}
		if (_client.Player.Position.DistanceSquaredTo(packet._startingPos) <= (double)(packet.Radius * packet.Radius))
		{
			_wasInsideAoe = true;
		}
	}

	public void OnPlayerHit(ShootAckPacket packet)
	{
		if (!Settings.Default.EnableAntiDebuffs || !_client.Projectiles.ContainsKey(packet.ObjectId))
		{
			return;
		}
		Dictionary<int, Projectile> dictionary = _client.Projectiles[packet.ObjectId];
		if (!dictionary.ContainsKey(packet.BulletId))
		{
			return;
		}
		Dictionary<string, float> statusEffects = dictionary[packet.BulletId].Structure.StatusEffects;
		bool flag = false;
		if (Settings.Default.IgnoreQuiet && statusEffects.ContainsKey("Quiet"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreWeak && statusEffects.ContainsKey("Weak"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreSlowed && statusEffects.ContainsKey("Slowed"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreSick && statusEffects.ContainsKey("Sick"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreDazed && statusEffects.ContainsKey("Dazed"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreStunned && statusEffects.ContainsKey("Stunned"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreParalyzed && statusEffects.ContainsKey("Paralyzed"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreBleeding && statusEffects.ContainsKey("Bleeding"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreArmorBreak && statusEffects.ContainsKey("Armor Broken"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnorePetStasis && statusEffects.ContainsKey("Stasis"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnorePetrified && statusEffects.ContainsKey("Petrified"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreSilence && statusEffects.ContainsKey("Silence"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreCurse && statusEffects.ContainsKey("Curse"))
		{
			flag = true;
		}
		else if (Settings.Default.IgnoreDrought && statusEffects.ContainsKey("Drought"))
		{
			flag = true;
		}
		if (!flag)
		{
			return;
		}
		foreach (KeyValuePair<string, float> item in statusEffects)
		{
			if (!_blockedProjectileEffects.Contains(item.Key))
			{
				_blockedProjectileEffects.Add(item.Key);
			}
		}
		packet.Send = false;
	}

	private void FilterConditionEffects(List<StatData> stats)
	{
		StatData statData = null;
		StatData statData2 = null;
		foreach (StatData item in stats)
		{
			if (item.StatTypeField == StatType.Effects)
			{
				statData = item;
			}
			else if (item.StatTypeField == StatType.Effects2)
			{
				statData2 = item;
			}
			if (statData != null && statData2 != null)
			{
				break;
			}
		}
		if (statData == null)
		{
			statData = CreatePrimaryEffectsStat();
			stats.Add(statData);
		}
		if (statData2 == null)
		{
			statData2 = CreateSecondaryEffectsStat();
			stats.Add(statData2);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (_blockedProjectileEffects.Contains("Quiet") && (2 & statData.StatValue) == 0)
		{
			num |= 2;
		}
		if (_blockedProjectileEffects.Contains("Weak") && (4 & statData.StatValue) == 0)
		{
			num |= 4;
		}
		if (_blockedProjectileEffects.Contains("Slowed") && (8 & statData.StatValue) == 0)
		{
			num |= 8;
		}
		if (_blockedProjectileEffects.Contains("Sick") && (0x10 & statData.StatValue) == 0)
		{
			num |= 0x10;
		}
		if (_blockedProjectileEffects.Contains("Dazed") && (0x20 & statData.StatValue) == 0)
		{
			num |= 0x20;
		}
		if (_blockedProjectileEffects.Contains("Stunned") && (0x40 & statData.StatValue) == 0)
		{
			num |= 0x40;
		}
		if (_blockedProjectileEffects.Contains("Paralyzed") && (0x2000 & statData.StatValue) == 0)
		{
			num |= 0x2000;
		}
		if (_blockedProjectileEffects.Contains("Bleeding") && (0x8000 & statData.StatValue) == 0)
		{
			num |= 0x8000;
		}
		if (_blockedProjectileEffects.Contains("Armor Broken") && (0x4000000 & statData.StatValue) == 0)
		{
			num |= 0x4000000;
		}
		if (_blockedProjectileEffects.Contains("Pet Stasis") && (0x200000 & statData.StatValue) == 0)
		{
			num |= 0x200000;
		}
		if (_blockedProjectileEffects.Contains("Petrified") && (8 & statData2.StatValue) == 0)
		{
			num2 |= 8;
		}
		if (_blockedProjectileEffects.Contains("Silence") && (0x10000 & statData2.StatValue) == 0)
		{
			num2 |= 0x10000;
		}
		_blockedProjectileEffects.Clear();
		if (Settings.Default.IgnoreBlind)
		{
			num3 |= 0x80;
		}
		if (Settings.Default.IgnoreHallucinating)
		{
			num3 |= 0x100;
		}
		if (Settings.Default.IgnoreDrunk)
		{
			num3 |= 0x200;
		}
		if (Settings.Default.IgnoreConfused)
		{
			num3 |= 0x400;
		}
		if (Settings.Default.IgnoreUnstable)
		{
			num3 |= 0x20000000;
		}
		if (Settings.Default.IgnoreDarkness)
		{
			num3 |= 0x40000000;
		}
		statData.StatValue &= ~num3;
		if (_wasInsideAoe)
		{
			_wasInsideAoe = false;
		}
		if (_wasInsideAoe)
		{
			_wasInsideAoe = false;
		}
	}
}
