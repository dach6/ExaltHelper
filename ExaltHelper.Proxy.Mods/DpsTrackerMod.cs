using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Helpers;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class DpsTrackerMod : IDisposable
{
	public class RealmSharkBullet
	{
		public int TotalDmg;

		public bool ArmorPiercing;

		public int SummonerId;

		public int ContainerType;

		public int OriginAbilityItem = -1;

		public int OriginScalingStat = int.MinValue;
		public int OriginEffects;
		public int OriginEffects2;
		public int OriginVitality = int.MinValue;
		public bool ScalingApplied;
		public bool ServerCreated;
		public string ShotContext = ",,,,,";
		public int ShooterId;
		public int BulletId;
		public readonly Dictionary<int, ProjectileHit> Hits = new Dictionary<int, ProjectileHit>();
	}

	public class ProjectileHit
	{
		public int Damage;
		public bool ServerConfirmed;
		public bool WasGuarding;
	}

	private class ServerBulletData
	{
		public short Damage;

		public int ContainerType;

		public int OwnerId;

		public int SummonerId;

		public byte BulletType;

		public bool ArmorPiercing;
	}

	private class PlayerDropRecord
	{
		public string Name;

		public int EntityId;

		public int LastHp;

		public int MaxHp;

		public bool IsDead;

		public DateTime DropTime;

		public int LastHpPercent
		{
			get
			{
				if (MaxHp <= 0)
				{
					return 0;
				}
				return (int)Math.Max(0.0, Math.Min(100.0, Math.Round((double)LastHp / (double)MaxHp * 100.0)));
			}
		}
	}

	private class PlayerCombatData
	{
		public string Name;

		public string ClassName;

		public long Damage;

		public int Hits;

		public int GuardHits;

		public long GuardDamage;

		public bool IsLocalPlayer;
	}

	private class EnemyCombatTracker
	{
		public int EntityId;

		public string Name = "Monster";

		public int MaxHp;

		public int CurrentHp;

		public int LastHp;

		public long TotalDamage;

		public int StartTick;

		public int EndTick;

		public int LastDamageTick;

		public int DefeatedTick;

		public bool IsDead;

		public bool IsBoss;

		public bool IsGuarding;

		public int TotalGuardHits;

		public long TotalGuardDamage;

		public int Condition1;

		public int Condition2;

		public readonly Dictionary<string, PlayerCombatData> Damagers = new Dictionary<string, PlayerCombatData>(StringComparer.OrdinalIgnoreCase);

		public double GetFightDurationSec()
		{
			if (StartTick == 0)
			{
				return 0.0;
			}
			double num = (double)(((EndTick > 0) ? EndTick : Environment.TickCount) - StartTick) / 1000.0;
			if (!(num < 0.5))
			{
				return num;
			}
			return 0.5;
		}

		public EnemyCombatSnapshot ToSnapshot()
		{
			double dur = GetFightDurationSec();
			double dps = ((dur > 0.0) ? Math.Round((double)TotalDamage / dur, 1) : 0.0);
			List<PlayerDamageEntry> list = new List<PlayerDamageEntry>();
			lock (Damagers)
			{
				foreach (PlayerCombatData value4 in Damagers.Values)
				{
					string text = value4.Name;
					string className = value4.ClassName;
					bool isLocalPlayer = value4.IsLocalPlayer;
					if (text != null && text.StartsWith("__PENDING_") && int.TryParse(text.Substring(10).TrimEnd('_'), out var result))
					{
						if (activeTrackerInstance != null)
						{
							PlayerParseEntry value;
							int value2;
							PlayerParseEntry value3;
							if (activeTrackerInstance.IsLocalPlayerId(result))
							{
								text = activeTrackerInstance.GetLocalPlayerName();
								className = activeTrackerInstance.GetLocalPlayerClassName();
								isLocalPlayer = true;
							}
							else if (activeTrackerInstance.playerRoster.TryGetValue(result, out value) && !string.IsNullOrEmpty(value.Name))
							{
								text = value.Name;
								className = value.ClassName;
							}
							else if (activeTrackerInstance.minionOwnerMap.TryGetValue(result, out value2) && activeTrackerInstance.playerRoster.TryGetValue(value2, out value3) && !string.IsNullOrEmpty(value3.Name))
							{
								text = value3.Name;
								className = value3.ClassName;
							}
							else if (activeTrackerInstance.droppedPlayers.Values.FirstOrDefault(d => d.EntityId == result) is PlayerDropRecord dropRec && !string.IsNullOrEmpty(dropRec.Name))
							{
								text = dropRec.Name;
							}
							else
							{
								text = $"Player_{result}";
							}
						}
						else
						{
							text = $"Player_{result}";
						}
					}
					double dps2 = ((dur > 0.0) ? Math.Round((double)value4.Damage / dur, 1) : 0.0);
					double percentage = ((TotalDamage > 0) ? Math.Round((double)value4.Damage / (double)TotalDamage * 100.0, 1) : 0.0);
					PlayerCombatStatus status = PlayerCombatStatus.Alive;
					int lastHpPercent = 100;
					if (activeTrackerInstance != null)
					{
						status = activeTrackerInstance.GetPlayerCombatStatus(text, out lastHpPercent);
					}
					list.Add(new PlayerDamageEntry
					{
						Name = text,
						ClassName = className,
						Damage = value4.Damage,
						Dps = dps2,
						Percentage = percentage,
						IsLocalPlayer = isLocalPlayer,
						GuardHits = value4.GuardHits,
						GuardDamage = value4.GuardDamage,
						Status = status,
						LastHpPercent = lastHpPercent
					});
				}
			}
			if (list.Any((PlayerDamageEntry p) => list.Count((PlayerDamageEntry x) => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase)) > 1))
			{
				list = (from g in list.GroupBy((PlayerDamageEntry p) => p.Name, StringComparer.OrdinalIgnoreCase)
					select new PlayerDamageEntry
					{
						Name = g.Key,
						ClassName = (g.FirstOrDefault((PlayerDamageEntry x) => x.ClassName != "Player")?.ClassName ?? g.First().ClassName),
						Damage = g.Sum((PlayerDamageEntry x) => x.Damage),
						Dps = ((dur > 0.0) ? Math.Round((double)g.Sum((PlayerDamageEntry x) => x.Damage) / dur, 1) : 0.0),
						Percentage = ((TotalDamage > 0) ? Math.Round((double)g.Sum((PlayerDamageEntry x) => x.Damage) / (double)TotalDamage * 100.0, 1) : 0.0),
						IsLocalPlayer = g.Any((PlayerDamageEntry x) => x.IsLocalPlayer),
						GuardHits = g.Sum((PlayerDamageEntry x) => x.GuardHits),
						GuardDamage = g.Sum((PlayerDamageEntry x) => x.GuardDamage),
						Status = (g.Any((PlayerDamageEntry x) => x.Status == PlayerCombatStatus.Dead) ? PlayerCombatStatus.Dead : (g.Any((PlayerDamageEntry x) => x.Status == PlayerCombatStatus.Nexused) ? PlayerCombatStatus.Nexused : PlayerCombatStatus.Alive)),
						LastHpPercent = (g.FirstOrDefault((PlayerDamageEntry x) => x.Status != PlayerCombatStatus.Alive)?.LastHpPercent ?? g.FirstOrDefault()?.LastHpPercent ?? 100)
					}).ToList();
			}
			list = list.OrderByDescending((PlayerDamageEntry p) => p.Damage).ToList();
			for (int num = 0; num < list.Count; num++)
			{
				list[num].Rank = num + 1;
			}
			return new EnemyCombatSnapshot
			{
				EntityId = EntityId,
				Name = Name,
				MaxHp = MaxHp,
				CurrentHp = CurrentHp,
				IsDead = IsDead,
				IsBoss = IsBoss,
				IsGuarding = IsGuarding,
				TotalGuardHits = TotalGuardHits,
				TotalGuardDamage = TotalGuardDamage,
				FightDurationSeconds = dur,
				TotalDamage = TotalDamage,
				Dps = dps,
				Damagers = list,
				Condition1 = Condition1,
				Condition2 = Condition2
			};
		}
	}

	public struct DungeonHistoryItem
	{
		public int Index;

		public string DungeonName;

		public string TimeStr;

		public int Kills;

		public long Damage;

		public int PlayerCount;

		public bool IsCurrent;
	}

	private readonly Client client;

	private string currentDungeonName = "Nexus";

	private DateTime dungeonStartTime = DateTime.Now;

	private int dungeonStartTick = Environment.TickCount;

	private int totalKillsCount;

	private readonly Dictionary<int, EnemyCombatTracker> allEnemies = new Dictionary<int, EnemyCombatTracker>();

	private readonly List<EnemyCombatTracker> defeatedEnemies = new List<EnemyCombatTracker>();

	private EnemyCombatTracker combinedDungeonCombat = new EnemyCombatTracker
	{
		Name = "All Monsters (Total)",
		IsBoss = false
	};

	private int activeFocusId = -1;

	private readonly Dictionary<int, int> recentShooters = new Dictionary<int, int>();

	private readonly Dictionary<int, PlayerParseEntry> playerRoster = new Dictionary<int, PlayerParseEntry>();

	private static readonly List<DpsSnapshot> sessionDungeonHistory = new List<DpsSnapshot>();

	private static int currentHistoryIndex = -1;

	private static int selectedMonsterMode = -1;

	private static volatile DpsTrackerMod activeTrackerInstance;

	private static DpsSnapshot lastLiveSnapshot;

	private static readonly List<LootBagDrop> sessionLootBags = new List<LootBagDrop>();

	private static readonly List<LootDropEntry> sessionLootDrops = new List<LootDropEntry>();

	private static readonly List<AbilityLogEntry> sessionAbilityLog = new List<AbilityLogEntry>();

	private static readonly object abilityLogLock = new object();

	private class PartyTrackerEntry
	{
		public short PlayerId;
		public string Name = "";
		public short ObjectId;
		public string ClassName = "Unknown";
		public int ClassId;
	}

	private static readonly object partyLock = new object();

	private static readonly Dictionary<string, PartyTrackerEntry> sessionPartyMembers = new Dictionary<string, PartyTrackerEntry>(StringComparer.OrdinalIgnoreCase);

	private static int sessionPartyId = 0;

	private static readonly HashSet<int> DecoyObjectTypes = new HashSet<int>
	{
		1813, 2016, 5136, 5198, 5739, 14663, 17141, 17142, 17143, 17144, 17145, 17146, 17147, 17148,
		19253, 19255, 19256, 19283, 25736, 26279, 28831, 30047, 33043, 45313, 45953, 48176, 52494
	};

	private readonly Dictionary<int, int> playerLastMp = new Dictionary<int, int>();

	private int lastDecoySpawnTick = 0;

	private WorldPosData lastDecoyPos = WorldPosData.Zero;

	private readonly HashSet<int> seenBagEntityIds = new HashSet<int>();

	private readonly Dictionary<int, int> minionOwnerMap = new Dictionary<int, int>();

	private RealmSharkRng realmSharkRng;
	private readonly EventDamageModifiers eventDamageModifiers = new EventDamageModifiers();
	private readonly DpsDamageDiagnostics damageDiagnostics = new DpsDamageDiagnostics();
	public bool EventDamageModifiersResolved { get; private set; } = true;
	private bool hasIncompleteEventDamage;

	public void OnCrucibleResponse(CrucibleResponsePacket packet)
	{
		eventDamageModifiers.Update(packet?.Definitions);
	}

	private readonly RealmSharkBullet[] playerBullets = new RealmSharkBullet[512];

	private readonly Dictionary<long, RealmSharkBullet> playerProjectiles = new Dictionary<long, RealmSharkBullet>();

	private readonly object playerBulletsLock = new object();

	private readonly Dictionary<ushort, int> localFiredBullets = new Dictionary<ushort, int>();

	private readonly Dictionary<long, int> recentLocalHits = new Dictionary<long, int>();

	private static readonly Random combatRng = new Random();

	private readonly Dictionary<long, ServerBulletData> serverBullets = new Dictionary<long, ServerBulletData>();

	private readonly Queue<long> serverBulletsQueue = new Queue<long>();

	private readonly HashSet<int> activeGuardingEntities = new HashSet<int>();

	private readonly Dictionary<string, PlayerDropRecord> droppedPlayers = new Dictionary<string, PlayerDropRecord>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<int, (string Name, int Hp, int MaxHp)> knownPlayerStats = new Dictionary<int, (string, int, int)>();

	private readonly HashSet<string> deadPlayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private int lastSnapshotPushTick;

	public DpsTrackerMod(Client client2)
	{
		client = client2;
		activeTrackerInstance = this;
	}

	public void OnMapChange(MapInfoPacket packet)
	{
		try
		{
			activeTrackerInstance = this;
			ArchiveCurrentDungeon();
			if (packet != null)
			{
				string text = ((!string.IsNullOrEmpty(packet.DisplayName)) ? packet.DisplayName : packet.MapName);
				if (!string.IsNullOrEmpty(text))
				{
					if (text.StartsWith("{s.") && text.EndsWith("}"))
					{
						text = text.Substring(3, text.Length - 4);
						if (text.Length > 0)
						{
							text = char.ToUpper(text[0]) + text.Substring(1);
						}
					}
					currentDungeonName = text;
				}
			}
			lock (playerRoster)
			{
				playerRoster.Clear();
			}
			lock (seenBagEntityIds)
			{
				seenBagEntityIds.Clear();
			}
			ResetDungeonEncounter();
			if (packet != null)
			{
				realmSharkRng = new RealmSharkRng(packet.Fp);
			}
			currentHistoryIndex = -1;
			selectedMonsterMode = -1;
			PushSnapshotToOverlay();
		}
		catch
		{
		}
	}

	private void ArchiveCurrentDungeon()
	{
		try
		{
			damageDiagnostics.Save();
			if (combinedDungeonCombat.TotalDamage <= 0 && totalKillsCount <= 0)
			{
				return;
			}
			lock (allEnemies)
			{
				foreach (KeyValuePair<int, EnemyCombatTracker> allEnemy in allEnemies)
				{
					EnemyCombatTracker value = allEnemy.Value;
					if (value == null || value.TotalDamage <= 0)
					{
						continue;
					}
					lock (defeatedEnemies)
					{
						if (!defeatedEnemies.Contains(value))
						{
							defeatedEnemies.Add(value);
						}
					}
				}
			}
			DpsSnapshot dpsSnapshot = CompileCurrentSnapshot();
			dpsSnapshot.HistoryIndex = -1;
			lock (sessionDungeonHistory)
			{
				sessionDungeonHistory.Add(dpsSnapshot);
				if (sessionDungeonHistory.Count > 30)
				{
					sessionDungeonHistory.RemoveAt(0);
					if (currentHistoryIndex > 0)
					{
						currentHistoryIndex--;
					}
				}
			}
		}
		catch
		{
		}
	}

	public void ResetDungeonEncounter()
	{
		damageDiagnostics.Reset();
		hasIncompleteEventDamage = false;
		dungeonStartTime = DateTime.Now;
		dungeonStartTick = Environment.TickCount;
		totalKillsCount = 0;
		activeFocusId = -1;
		lock (allEnemies)
		{
			allEnemies.Clear();
		}
		lock (defeatedEnemies)
		{
			defeatedEnemies.Clear();
		}
		lock (combinedDungeonCombat)
		{
			combinedDungeonCombat = new EnemyCombatTracker
			{
				Name = "All Monsters (Total)",
				IsBoss = false
			};
		}
		lock (recentShooters)
		{
			recentShooters.Clear();
		}
		lock (minionOwnerMap)
		{
			minionOwnerMap.Clear();
		}
		lock (playerBulletsLock)
		{
			Array.Clear(playerBullets, 0, playerBullets.Length);
			playerProjectiles.Clear();
		}
		lock (recentLocalHits)
		{
			recentLocalHits.Clear();
		}
		lock (localFiredBullets)
		{
			localFiredBullets.Clear();
		}
		lock (serverBullets)
		{
			serverBullets.Clear();
			serverBulletsQueue.Clear();
		}
		lock (activeGuardingEntities)
		{
			activeGuardingEntities.Clear();
		}
		lock (droppedPlayers)
		{
			droppedPlayers.Clear();
		}
		lock (knownPlayerStats)
		{
			knownPlayerStats.Clear();
		}
		lock (deadPlayerNames)
		{
			deadPlayerNames.Clear();
		}
	}

	public void OnNewTick(NewTickPacket packet)
	{
		try
		{
			if (client == null)
			{
				return;
			}
			int tickCount = Environment.TickCount;
			if (packet.Statuses != null)
			{
				foreach (ObjectStatsData item in packet.Statuses)
				{
					if (item.StatList == null)
					{
						continue;
					}
					foreach (StatData item2 in item.StatList)
					{
						if (item2.StatTypeField == StatType.UnknownStat125)
						{
							int StatValue = item2.StatValue;
							bool flag = StatValue == -935464302 || StatValue == -918686683;
							lock (activeGuardingEntities)
							{
								if (flag)
								{
									activeGuardingEntities.Add(item.ObjectId);
								}
								else
								{
									activeGuardingEntities.Remove(item.ObjectId);
								}
							}
							lock (allEnemies)
							{
								if (allEnemies.TryGetValue(item.ObjectId, out var value))
								{
									value.IsGuarding = flag;
								}
							}
						}
						else if (item2.StatTypeField == StatType.Condition1)
						{
							lock (allEnemies)
							{
								if (allEnemies.TryGetValue(item.ObjectId, out var value2))
								{
									value2.Condition1 = item2.StatValue;
								}
							}
						}
						else
						{
							if (item2.StatTypeField == StatType.Condition2)
							{
								lock (allEnemies)
								{
									if (allEnemies.TryGetValue(item.ObjectId, out var value3))
									{
										value3.Condition2 = item2.StatValue;
									}
								}
							}
							else if ((byte)item2.StatTypeField == 4)
							{
								CheckPlayerMpDrop(item.ObjectId, item2.StatValue);
							}
						}
					}
				}
			}
			if (client.Entities != null)
			{
				MapObject[] array;
				lock (client.Entities)
				{
					array = client.Entities.Values.ToArray();
				}
				MapObject[] array2 = array;
				foreach (MapObject mapObject in array2)
				{
					if (mapObject != null && mapObject.IsPlayer && !string.IsNullOrEmpty(mapObject.PlayerName))
					{
						UpdatePlayerRosterEntry(mapObject);
						lock (knownPlayerStats)
						{
							knownPlayerStats[mapObject.ObjectId] = (mapObject.PlayerName, mapObject.Hp, mapObject.MaxHp);
						}
					}
				}
			}
			if (client.Enemies != null)
			{
				MapObject[] array3;
				lock (client.Enemies)
				{
					array3 = client.Enemies.Values.ToArray();
				}
				MapObject[] array2 = array3;
				foreach (MapObject mapObject2 in array2)
				{
					if (mapObject2 == null || IsLocalPlayerId(mapObject2.ObjectId))
					{
						continue;
					}
					int entityId = mapObject2.ObjectId;
					int Hp = mapObject2.Hp;
					int MaxHp = mapObject2.MaxHp;
					string name = mapObject2.StructureName ?? "Monster";
					ItemSpriteManager.ItemMeta itemMetadata = ItemSpriteManager.GetItemMetadata(mapObject2.ObjectType);
					if (itemMetadata != null && !string.IsNullOrWhiteSpace(itemMetadata.Name) && !itemMetadata.Name.StartsWith("{s.") && !itemMetadata.Name.StartsWith("Item #"))
					{
						name = itemMetadata.Name;
					}
					bool flag2 = mapObject2.IsQuest || MaxHp >= 20000;
					EnemyCombatTracker value4;
					lock (allEnemies)
					{
						if (!allEnemies.TryGetValue(entityId, out value4))
						{
							if (flag2 && !string.IsNullOrEmpty(name) && name != "Monster")
							{
								EnemyCombatTracker enemyCombatTracker = allEnemies.Values.FirstOrDefault((EnemyCombatTracker e) => e.IsBoss && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) && e.EntityId != entityId && (e.IsDead || (client.Enemies != null && !client.Enemies.ContainsKey(e.EntityId))));
								if (enemyCombatTracker != null)
								{
									value4 = enemyCombatTracker;
									value4.EntityId = entityId;
									value4.IsDead = false;
									value4.CurrentHp = Hp;
									if (Hp > 0)
									{
										value4.LastHp = Hp;
									}
									if (MaxHp > 0)
									{
										value4.MaxHp += MaxHp;
									}
									allEnemies[entityId] = value4;
									lock (defeatedEnemies)
									{
										defeatedEnemies.Remove(value4);
									}
								}
							}
							if (value4 == null)
							{
								bool isGuarding = false;
								lock (activeGuardingEntities)
								{
									isGuarding = activeGuardingEntities.Contains(entityId);
								}
								value4 = new EnemyCombatTracker
								{
									EntityId = entityId,
									Name = name,
									MaxHp = MaxHp,
									CurrentHp = Hp,
									LastHp = Hp,
									IsBoss = flag2,
									IsGuarding = isGuarding
								};
								allEnemies[entityId] = value4;
							}
						}
					}
					if (!string.IsNullOrWhiteSpace(name) && name != "Monster" && !name.StartsWith("Item #") && !name.StartsWith("{s.")) value4.Name = name;
					value4.IsBoss |= flag2;
					if (value4.MaxHp == 0) value4.MaxHp = MaxHp;
					value4.CurrentHp = Hp;
					value4.LastHp = Hp;
					value4.Condition1 = mapObject2.Effects;
					value4.Condition2 = mapObject2.Effects2;
					lock (activeGuardingEntities)
					{
						if (activeGuardingEntities.Contains(entityId))
						{
							value4.IsGuarding = true;
						}
					}
					if (flag2 && (activeFocusId == -1 || !client.Enemies.ContainsKey(activeFocusId)))
					{
						activeFocusId = entityId;
					}
					if (Hp <= 0 && !value4.IsDead && value4.TotalDamage > 0)
					{
						MarkEnemyDefeated(value4);
					}
					else if (Hp == 1 && value4.TotalDamage > 0 && value4.EndTick == 0 &&
						(mapObject2.ObjectType == MoonlightVillageMod.SageGenjiId ||
						 mapObject2.ObjectType == MoonlightVillageMod.DancerMikoId ||
						 mapObject2.ObjectType == MoonlightVillageMod.DrummerKaguyaId))
					{
						// Dancers remain alive at 1 HP; do not keep diluting their completed DPS.
						value4.EndTick = value4.LastDamageTick != 0 ? value4.LastDamageTick : tickCount;
					}
				}
			}
			damageDiagnostics.SaveInBackground();
			if (tickCount - lastSnapshotPushTick > 100)
			{
				lastSnapshotPushTick = tickCount;
				PushSnapshotToOverlay();
			}
		}
		catch
		{
		}
	}

	private void MarkEnemyDefeated(EnemyCombatTracker tracker)
	{
		tracker.IsDead = true;
		tracker.DefeatedTick = Environment.TickCount;
		tracker.EndTick = tracker.LastDamageTick != 0 ? tracker.LastDamageTick : Environment.TickCount;
		tracker.CurrentHp = 0;
		damageDiagnostics.SaveInBackground(force: true);
		lock (defeatedEnemies)
		{
			if (!defeatedEnemies.Contains(tracker))
			{
				defeatedEnemies.Insert(0, tracker);
				totalKillsCount++;
			}
		}
		if (activeFocusId == tracker.EntityId)
		{
			activeFocusId = -1;
		}
	}

	public void OnUpdate(UpdatePacket packet)
	{
		try
		{
			if (packet == null)
			{
				return;
			}
			if (packet.NewObjects != null)
			{
				ObjectData[] NewObjects = packet.NewObjects;
				foreach (ObjectData objectData in NewObjects)
				{
					if (objectData?.Stats == null)
					{
						continue;
					}
					CheckDecoySpawned(objectData);
					int ObjectId = objectData.Stats.ObjectId;
					if (client.Entities != null && client.Entities.TryGetValue(ObjectId, out var value) && value.IsPlayer && !string.IsNullOrEmpty(value.PlayerName))
					{
						UpdatePlayerRosterEntry(value);
						lock (knownPlayerStats)
						{
							knownPlayerStats[value.ObjectId] = (value.PlayerName, value.Hp, value.MaxHp);
						}
					}
					if (objectData.Stats.StatList != null)
					{
						foreach (StatData item in objectData.Stats.StatList)
						{
							if ((byte)item.StatTypeField == 114 && item.StatValue > 0)
							{
								lock (minionOwnerMap)
								{
									minionOwnerMap[ObjectId] = item.StatValue;
								}
								ResolvePendingMinionOwner(ObjectId, item.StatValue);
							}
							else if ((byte)item.StatTypeField == 1)
							{
								lock (knownPlayerStats)
								{
									if (knownPlayerStats.TryGetValue(ObjectId, out (string, int, int) value2))
									{
										knownPlayerStats[ObjectId] = (value2.Item1, item.StatValue, value2.Item3);
									}
								}
							}
							else if ((byte)item.StatTypeField == 0)
							{
								lock (knownPlayerStats)
								{
									if (knownPlayerStats.TryGetValue(ObjectId, out (string, int, int) value3))
									{
										knownPlayerStats[ObjectId] = (value3.Item1, value3.Item2, item.StatValue);
									}
								}
							}
							else if ((byte)item.StatTypeField == 4)
							{
								CheckPlayerMpDrop(ObjectId, item.StatValue);
							}
							else if (item.StatTypeField == StatType.UnknownStat125)
							{
								int StatValue = item.StatValue;
								bool flag = StatValue == -935464302 || StatValue == -918686683;
								lock (activeGuardingEntities)
								{
									if (flag)
									{
										activeGuardingEntities.Add(ObjectId);
									}
									else
									{
										activeGuardingEntities.Remove(ObjectId);
									}
								}
								lock (allEnemies)
								{
									if (allEnemies.TryGetValue(ObjectId, out var value4))
									{
										value4.IsGuarding = flag;
									}
								}
							}
							else if (item.StatTypeField == StatType.Condition1)
							{
								lock (allEnemies)
								{
									if (allEnemies.TryGetValue(ObjectId, out var value5))
									{
										value5.Condition1 = item.StatValue;
									}
								}
							}
							else
							{
								if (!(item.StatTypeField == StatType.Condition2))
								{
									continue;
								}
								lock (allEnemies)
								{
									if (allEnemies.TryGetValue(ObjectId, out var value6))
									{
										value6.Condition2 = item.StatValue;
									}
								}
							}
						}
					}
					if (IsQualifyingLootBag(objectData.ObjectType, out var _, out var _))
					{
						ProcessLootBag(objectData);
					}
				}
			}
			if (packet.Drops == null)
			{
				return;
			}
			int[] Drops = packet.Drops;
			bool playerRosterChanged = false;
			foreach (int num in Drops)
			{
				string text = null;
				int lastHp = 0;
				int maxHp = 0;
				lock (knownPlayerStats)
				{
					if (knownPlayerStats.TryGetValue(num, out (string, int, int) value7))
					{
						(text, lastHp, maxHp) = value7;
						knownPlayerStats.Remove(num);
					}
				}
				lock (playerRoster)
				{
					if (playerRoster.TryGetValue(num, out var value8))
					{
						if (string.IsNullOrEmpty(text))
						{
							text = value8.Name;
						}
						playerRoster.Remove(num);
						playerRosterChanged = true;
					}
				}
				if (!string.IsNullOrEmpty(text))
				{
					lock (playerRoster)
					{
						var staleKeys = playerRoster
							.Where(kvp => string.Equals(kvp.Value.Name, text, StringComparison.OrdinalIgnoreCase))
							.Select(kvp => kvp.Key)
							.ToList();
						foreach (int staleKey in staleKeys)
						{
							playerRoster.Remove(staleKey);
							playerRosterChanged = true;
						}
					}
					lock (droppedPlayers)
					{
						bool isDead = false;
						lock (deadPlayerNames)
						{
							isDead = deadPlayerNames.Contains(text);
						}
						droppedPlayers[text] = new PlayerDropRecord
						{
							Name = text,
							EntityId = num,
							LastHp = lastHp,
							MaxHp = maxHp,
							IsDead = isDead,
							DropTime = DateTime.Now
						};
					}
				}
				lock (minionOwnerMap)
				{
					minionOwnerMap.Remove(num);
				}
				lock (allEnemies)
				{
					if (allEnemies.TryGetValue(num, out var value9) && !value9.IsDead && value9.TotalDamage > 0)
					{
						MarkEnemyDefeated(value9);
					}
				}
			}
			if (playerRosterChanged)
			{
				PushSnapshotToOverlay();
			}
		}
		catch
		{
		}
	}

	private static bool IsQualifyingLootBag(ushort type, out string bagName, out Color bagColor)
	{
		bagName = "";
		bagColor = Color.White;
		switch (type)
		{
		case 1292:
		case 1296:
			bagName = "White Bag";
			bagColor = Color.FromArgb(255, 255, 255);
			return true;
		case 1295:
		case 1727:
			bagName = "Orange Bag";
			bagColor = Color.FromArgb(255, 140, 0);
			return true;
		case 1708:
		case 1728:
			bagName = "Red Bag";
			bagColor = Color.FromArgb(255, 60, 60);
			return true;
		case 1294:
		case 1724:
			bagName = "Gold Bag";
			bagColor = Color.FromArgb(255, 215, 0);
			return true;
		case 1289:
		case 4358:
			bagName = "Cyan Bag";
			bagColor = Color.FromArgb(0, 225, 255);
			return true;
		case 1291:
			bagName = "Blue Bag";
			bagColor = Color.FromArgb(56, 189, 248);
			return true;
		case 1288:
		case 1723:
			bagName = "Egg Basket";
			bagColor = Color.FromArgb(240, 200, 80);
			return true;
		default:
			return false;
		}
	}

	private void ProcessLootBag(ObjectData obj)
	{
		if (obj?.Stats == null)
		{
			return;
		}
		int ObjectId = obj.Stats.ObjectId;
		lock (seenBagEntityIds)
		{
			if (!seenBagEntityIds.Add(ObjectId))
			{
				return;
			}
		}
		if (!IsQualifyingLootBag(obj.ObjectType, out var bagName, out var bagColor) || obj.Stats.StatList == null)
		{
			return;
		}
		string text = "Monster";
		lock (defeatedEnemies)
		{
			if (defeatedEnemies.Count > 0)
			{
				EnemyCombatTracker enemyCombatTracker = defeatedEnemies[0];
				if (Environment.TickCount - enemyCombatTracker.DefeatedTick < 12000)
				{
					text = enemyCombatTracker.Name;
				}
			}
		}
		if (text == "Monster")
		{
			text = ((!string.IsNullOrEmpty(currentDungeonName)) ? currentDungeonName : "Encounter");
		}
		LootBagDrop lootBagDrop = new LootBagDrop
		{
			BagEntityId = ObjectId,
			BagType = bagName,
			BagColor = bagColor,
			SourceMonster = text,
			DropTime = DateTime.Now
		};
		string[] slotItemDatas = null;
		StatData itemDataStat = obj.Stats.StatList.FirstOrDefault(s => s.StatTypeField == 80);
		if (itemDataStat != null && !string.IsNullOrEmpty(itemDataStat.StatStringValue))
		{
			slotItemDatas = itemDataStat.StatStringValue.Split(',');
		}
		foreach (StatData item2 in obj.Stats.StatList)
		{
			int num = item2.StatTypeField;
			if (num < 8 || num > 15)
			{
				continue;
			}
			int StatValue = item2.StatValue;
			if (StatValue <= 0)
			{
				continue;
			}
			int slotIdx = num - 8;
			lootBagDrop.ItemIds.Add(StatValue);
			ItemSpriteManager.ItemMeta itemMetadata = ItemSpriteManager.GetItemMetadata(StatValue);

			string slotCode = (slotItemDatas != null && slotItemDatas.Length > slotIdx) ? slotItemDatas[slotIdx] : null;
			var (enchantCount, enchantRarity, enchantNames) = EnchantmentParser.ParseSlotEnchants(slotCode);

			bool isShiny = itemMetadata.IsShiny;
			string effectiveRarity = itemMetadata.Rarity;
			if (enchantCount > 0 && !string.IsNullOrEmpty(enchantRarity) && enchantRarity != "Common")
			{
				effectiveRarity = enchantRarity;
			}

			lootBagDrop.ItemRarities.Add(effectiveRarity);
			lootBagDrop.ItemIsShiny.Add(isShiny);
			lootBagDrop.ItemEnchantCounts.Add(enchantCount);
			lootBagDrop.ItemEnchants.Add(enchantNames);

			LootDropEntry item = new LootDropEntry
			{
				ItemId = StatValue,
				ItemName = itemMetadata.Name,
				ItemRarity = effectiveRarity,
				IsShiny = isShiny,
				EnchantCount = enchantCount,
				Enchants = enchantNames,
				BagType = bagName,
				BagColor = bagColor,
				SourceMonster = text,
				DropTime = DateTime.Now
			};
			lock (sessionLootDrops)
			{
				sessionLootDrops.Insert(0, item);
				if (sessionLootDrops.Count > 100)
				{
					sessionLootDrops.RemoveAt(sessionLootDrops.Count - 1);
				}
			}
		}
		if (lootBagDrop.ItemIds.Count <= 0)
		{
			return;
		}
		lock (sessionLootBags)
		{
			sessionLootBags.Insert(0, lootBagDrop);
			if (sessionLootBags.Count > 100)
			{
				sessionLootBags.RemoveAt(sessionLootBags.Count - 1);
			}
		}
		if (lootBagDrop.HasShinyItem)
		{
			string shinyNames = string.Join(", ", lootBagDrop.ItemIds.Where((id, idx) => lootBagDrop.ItemIsShiny != null && lootBagDrop.ItemIsShiny.Count > idx && lootBagDrop.ItemIsShiny[idx]).Select(id => ItemSpriteManager.GetItemMetadata(id).Name));
			client.SendNotification("✨ [SHINY DROP!] " + shinyNames + " from " + text + "! ✨");
		}
		else
		{
			switch (bagName)
			{
			case "White Bag":
			case "Orange Bag":
			case "Red Bag":
			{
				string text2 = string.Join(", ", lootBagDrop.ItemIds.Select((int id) => ItemSpriteManager.GetItemMetadata(id).Name));
				client.SendNotification("[" + bagName.ToUpper() + "] Dropped: " + text2 + " from " + text + "!");
				break;
			}
			}
		}
		PushOverlaySnapshot();
	}

	private bool IsLocalPlayerId(int entityId)
	{
		if (entityId <= 0 || client == null)
		{
			return false;
		}
		if (client.ClientId > 0 && entityId == client.ClientId)
		{
			return true;
		}
		if (client.Player != null && entityId == client.Player.ObjectId)
		{
			return true;
		}
		if (client.FollowTargetObject != null && entityId == client.FollowTargetObject.ObjectId)
		{
			return true;
		}
		string localPlayerName = GetLocalPlayerName();
		if (!string.IsNullOrEmpty(localPlayerName) && localPlayerName != "You" && localPlayerName != "Player")
		{
			if (client.Entities != null && client.Entities.TryGetValue(entityId, out var value) && !string.IsNullOrEmpty(value.PlayerName) && string.Equals(value.PlayerName, localPlayerName, StringComparison.OrdinalIgnoreCase))
			{
				if (client.ClientId <= 0)
				{
					client.ClientId = entityId;
				}
				if (client.Player == null)
				{
					client.Player = value;
				}
				return true;
			}
			if (playerRoster.TryGetValue(entityId, out var value2) && !string.IsNullOrEmpty(value2.Name) && string.Equals(value2.Name, localPlayerName, StringComparison.OrdinalIgnoreCase))
			{
				if (client.ClientId <= 0)
				{
					client.ClientId = entityId;
				}
				return true;
			}
		}
		lock (minionOwnerMap)
		{
			if (minionOwnerMap.TryGetValue(entityId, out var value3))
			{
				if ((client.ClientId > 0 && value3 == client.ClientId) || (client.Player != null && value3 == client.Player.ObjectId))
				{
					return true;
				}
				if (!string.IsNullOrEmpty(localPlayerName) && localPlayerName != "You" && localPlayerName != "Player" && playerRoster.TryGetValue(value3, out var value4) && string.Equals(value4.Name, localPlayerName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	private string GetLocalPlayerName()
	{
		if (client?.Player != null && !string.IsNullOrEmpty(client.Player.PlayerName))
		{
			return client.Player.PlayerName;
		}
		if (client != null && client.ClientId > 0 && client.Entities != null && client.Entities.TryGetValue(client.ClientId, out var value) && !string.IsNullOrEmpty(value.PlayerName))
		{
			client.Player = value;
			return value.PlayerName;
		}
		return "You";
	}

	private string GetLocalPlayerClassName()
	{
		if (client?.Player != null)
		{
			return GetClassName(client.Player.ObjectType);
		}
		if (client != null && client.ClientId > 0 && client.Entities != null && client.Entities.TryGetValue(client.ClientId, out var value))
		{
			client.Player = value;
			return GetClassName(value.ObjectType);
		}
		return "Player";
	}

	private int GetLocalPlayerHpPercent()
	{
		if (client?.Player != null && client.Player.MaxHp > 0)
		{
			return (int)Math.Max(0.0, Math.Min(100.0, Math.Round((double)client.Player.Hp / (double)client.Player.MaxHp * 100.0)));
		}
		return 100;
	}

	public PlayerCombatStatus GetPlayerCombatStatus(string playerName, out int lastHpPercent)
	{
		lastHpPercent = 100;
		if (string.IsNullOrEmpty(playerName))
		{
			return PlayerCombatStatus.Alive;
		}
		lock (deadPlayerNames)
		{
			if (deadPlayerNames.Contains(playerName))
			{
				lastHpPercent = 0;
				return PlayerCombatStatus.Dead;
			}
		}
		string localPlayerName = GetLocalPlayerName();
		if (string.Equals(playerName, localPlayerName, StringComparison.OrdinalIgnoreCase))
		{
			lastHpPercent = GetLocalPlayerHpPercent();
			return PlayerCombatStatus.Alive;
		}
		bool flag = false;
		if (client?.Entities != null)
		{
			lock (client.Entities)
			{
				foreach (MapObject value2 in client.Entities.Values)
				{
					if (value2 != null && value2.IsPlayer && string.Equals(value2.PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
					{
						flag = true;
						if (value2.MaxHp > 0)
						{
							lastHpPercent = (int)Math.Max(0.0, Math.Min(100.0, Math.Round((double)value2.Hp / (double)value2.MaxHp * 100.0)));
						}
						break;
					}
				}
			}
		}
		if (flag)
		{
			return PlayerCombatStatus.Alive;
		}
		lock (droppedPlayers)
		{
			if (droppedPlayers.TryGetValue(playerName, out var value))
			{
				if (value.IsDead)
				{
					lastHpPercent = 0;
					return PlayerCombatStatus.Dead;
				}
				lastHpPercent = value.LastHpPercent;
				return PlayerCombatStatus.Nexused;
			}
		}
		return PlayerCombatStatus.Alive;
	}

	private static string ExtractPlayerDeathName(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return null;
		}
		try
		{
			if (message.Contains("\"player\""))
			{
				int startIndex = message.IndexOf("\"player\"", StringComparison.OrdinalIgnoreCase);
				int num = message.IndexOf(':', startIndex);
				if (num > 0)
				{
					int num2 = message.IndexOf('"', num);
					if (num2 > 0)
					{
						int num3 = message.IndexOf('"', num2 + 1);
						if (num3 > num2)
						{
							return message.Substring(num2 + 1, num3 - num2 - 1);
						}
					}
				}
			}
			string[] array = message.Split('"');
			if (array.Length > 9 && message.Contains("player_death"))
			{
				return array[9];
			}
			if (message.Contains(" died at Level "))
			{
				int num4 = message.IndexOf(" died at Level ", StringComparison.OrdinalIgnoreCase);
				if (num4 > 0)
				{
					string text = message.Substring(0, num4).Trim();
					int num5 = text.LastIndexOf(' ');
					if (num5 >= 0)
					{
						text = text.Substring(num5 + 1);
					}
					return text;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public void RecordPlayerDeath(string playerName)
	{
		if (string.IsNullOrWhiteSpace(playerName))
		{
			return;
		}
		lock (deadPlayerNames)
		{
			deadPlayerNames.Add(playerName);
		}
		lock (droppedPlayers)
		{
			if (droppedPlayers.TryGetValue(playerName, out var value))
			{
				value.IsDead = true;
			}
			else
			{
				droppedPlayers[playerName] = new PlayerDropRecord
				{
					Name = playerName,
					IsDead = true,
					DropTime = DateTime.Now
				};
			}
		}
		PushSnapshotToOverlay();
	}

	public void OnNotification(NotificationPacket packet)
	{
		if (packet == null)
		{
			return;
		}
		try
		{
			if (packet.NotificationType == 7)
			{
				string text = ExtractPlayerDeathName(packet.Message);
				if (!string.IsNullOrEmpty(text))
				{
					RecordPlayerDeath(text);
				}
			}
		}
		catch
		{
		}
	}

	public void OnText(TextPacket packet)
	{
		if (packet == null)
		{
			return;
		}
		try
		{
			string text = packet.CleanText ?? packet.Text ?? "";
			if (!string.IsNullOrEmpty(text))
			{
				string text2 = null;
				if (text.Contains("player_death"))
				{
					text2 = ExtractPlayerDeathName(text);
				}
				else if (text.Contains(" died at Level "))
				{
					text2 = ExtractPlayerDeathName(text);
				}
				if (!string.IsNullOrEmpty(text2))
				{
					RecordPlayerDeath(text2);
				}
				if (text.IndexOf("left the party", StringComparison.OrdinalIgnoreCase) >= 0 ||
				    text.IndexOf("party has been disbanded", StringComparison.OrdinalIgnoreCase) >= 0 ||
				    text.IndexOf("kicked from the party", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					if (text.IndexOf("You left", StringComparison.OrdinalIgnoreCase) >= 0 ||
					    text.IndexOf("You have left", StringComparison.OrdinalIgnoreCase) >= 0 ||
					    text.IndexOf("disbanded", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						ClearParty();
					}
				}
			}
		}
		catch
		{
		}
	}

	private void RecordRecentLocalHit(int targetId, ushort bulletId)
	{
		long num = ((long)targetId << 32) | (bulletId & 0xFFFF);
		long num2 = ((long)targetId << 32) | (bulletId % 256);
		int tickCount = Environment.TickCount;
		lock (recentLocalHits)
		{
			recentLocalHits[num] = tickCount;
			if (num2 != num)
			{
				recentLocalHits[num2] = tickCount;
			}
			if (recentLocalHits.Count <= 1000)
			{
				return;
			}
			List<long> list = null;
			foreach (KeyValuePair<long, int> recentLocalHit in recentLocalHits)
			{
				if (tickCount - recentLocalHit.Value > 2000)
				{
					if (list == null)
					{
						list = new List<long>();
					}
					list.Add(recentLocalHit.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (long item in list)
			{
				recentLocalHits.Remove(item);
			}
		}
	}

	private bool IsRecentLocalHit(int targetId, ushort bulletId)
	{
		long key = ((long)targetId << 32) | (bulletId & 0xFFFF);
		long key2 = ((long)targetId << 32) | (bulletId % 256);
		int tickCount = Environment.TickCount;
		lock (recentLocalHits)
		{
			if (recentLocalHits.TryGetValue(key, out var value) || recentLocalHits.TryGetValue(key2, out value))
			{
				if (tickCount - value < 1500)
				{
					return true;
				}
				recentLocalHits.Remove(key);
				recentLocalHits.Remove(key2);
			}
			return false;
		}
	}

	private static bool IsMainWeaponSlot(int slotType)
	{
		switch (slotType)
		{
		case 1:
		case 2:
		case 3:
		case 8:
		case 17:
		case 24:
		case 28:
			return true;
		default:
			return false;
		}
	}

	private float GetRealmSharkPlayerStatsMultiplier()
	{
		if (client?.Player == null)
		{
			return 1f;
		}
		MapObject Player = client.Player;
		int Effects = Player.Effects;
		if ((Effects & 0x04) != 0)
		{
			return 0.5f;
		}
		bool num = (Effects & 0x40000) != 0;
		float num2 = (float)(Player.Attack + 25) * 0.02f;
		if (num)
		{
			num2 *= 1.25f;
		}
		float num3 = ((Player.ExaltationBonusDamage > 0) ? ((float)Player.ExaltationBonusDamage / 1000f) : 1f);
		EventDamageModifiersResolved = eventDamageModifiers.TryGetMultiplier(Player.CrucibleId, Player.BloodRitualId, out double eventMultiplier);
		if (!EventDamageModifiersResolved) hasIncompleteEventDamage = true;
		return num2 * num3 * (float)eventMultiplier;
	}

	private static int RealmSharkDamageWithDefense(int damage, bool armorPiercing, int defence, int cond0, int cond1)
	{
		if (damage <= 0)
		{
			return 0;
		}
		if (armorPiercing || (cond0 & 0x4000000) != 0)
		{
			defence = 0;
		}
		else if ((cond0 & 0x2000000) != 0)
		{
			defence = (int)((double)defence * 1.5);
		}
		if ((cond1 & 0x20000) != 0)
		{
			defence -= 20;
		}
		int num = Math.Max(damage * 2 / 20, damage - defence);
		if ((cond0 & 0x1000000) != 0)
		{
			num = 0;
		}
		if ((cond1 & 8) != 0)
		{
			num = (int)((double)num * 0.9);
		}
		if ((cond1 & 0x40) != 0)
		{
			num = (int)((double)num * 1.25);
		}
		return num;
	}

	private void RecordCombatDamage(int targetId, int dmgAmount, bool isLocal, string attackerName, string className)
	{
		try
		{
			if (dmgAmount <= 0)
			{
				return;
			}
			int tickCount = Environment.TickCount;
			EnemyCombatTracker value;
			lock (allEnemies)
			{
				if (!allEnemies.TryGetValue(targetId, out value))
				{
					string enemyName = "Monster";
					int num = 0;
					int num2 = 0;
					bool flag = false;
					if (client.Enemies != null && client.Enemies.TryGetValue(targetId, out var value2))
					{
						enemyName = value2.StructureName ?? "Monster";
						ItemSpriteManager.ItemMeta itemMetadata = ItemSpriteManager.GetItemMetadata(value2.ObjectType);
						if (itemMetadata != null && !string.IsNullOrWhiteSpace(itemMetadata.Name) && !itemMetadata.Name.StartsWith("{s.") && !itemMetadata.Name.StartsWith("Item #"))
						{
							enemyName = itemMetadata.Name;
						}
						num = value2.MaxHp;
						num2 = value2.Hp;
						flag = value2.IsQuest || num >= 20000;
					}
					else if (client.Portals != null && client.Portals.TryGetValue(targetId, out value2))
					{
						enemyName = value2.StructureName ?? "Monster";
						ItemSpriteManager.ItemMeta itemMetadata2 = ItemSpriteManager.GetItemMetadata(value2.ObjectType);
						if (itemMetadata2 != null && !string.IsNullOrWhiteSpace(itemMetadata2.Name) && !itemMetadata2.Name.StartsWith("{s.") && !itemMetadata2.Name.StartsWith("Item #"))
						{
							enemyName = itemMetadata2.Name;
						}
						num = value2.MaxHp;
						num2 = value2.Hp;
						flag = value2.IsQuest || num >= 20000;
					}
					if (flag && !string.IsNullOrEmpty(enemyName) && enemyName != "Monster")
					{
						EnemyCombatTracker enemyCombatTracker = allEnemies.Values.FirstOrDefault((EnemyCombatTracker e) => e.IsBoss && string.Equals(e.Name, enemyName, StringComparison.OrdinalIgnoreCase) && e.EntityId != targetId && (e.IsDead || (client.Enemies != null && !client.Enemies.ContainsKey(e.EntityId))));
						if (enemyCombatTracker != null)
						{
							value = enemyCombatTracker;
							value.EntityId = targetId;
							value.IsDead = false;
							value.CurrentHp = num2;
							if (num2 > 0)
							{
								value.LastHp = num2;
							}
							if (num > 0)
							{
								value.MaxHp += num;
							}
							allEnemies[targetId] = value;
							lock (defeatedEnemies)
							{
								defeatedEnemies.Remove(value);
							}
						}
					}
					if (value == null)
					{
						value = new EnemyCombatTracker
						{
							EntityId = targetId,
							Name = enemyName,
							MaxHp = num,
							CurrentHp = num2,
							LastHp = num2,
							IsBoss = flag
						};
						allEnemies[targetId] = value;
					}
				}
			}
			if (value.StartTick == 0)
			{
				value.StartTick = tickCount;
			}
			if (combinedDungeonCombat.StartTick == 0)
			{
				combinedDungeonCombat.StartTick = tickCount;
			}
			value.TotalDamage += dmgAmount;
			value.LastDamageTick = tickCount;
			value.EndTick = value.IsDead ? tickCount : 0; // Late hits keep a defeated encounter frozen; live phases resume.
			combinedDungeonCombat.TotalDamage += dmgAmount;
			combinedDungeonCombat.LastDamageTick = tickCount;
			if (string.IsNullOrEmpty(attackerName))
			{
				if (activeFocusId == -1 || value.IsBoss || (client.Enemies != null && !client.Enemies.ContainsKey(activeFocusId)))
				{
					activeFocusId = targetId;
				}
				return;
			}
			string key = (isLocal ? "__LOCAL__" : attackerName);
			lock (value.Damagers)
			{
				if (!value.Damagers.TryGetValue(key, out var value3))
				{
					value3 = new PlayerCombatData
					{
						Name = attackerName,
						ClassName = className,
						Damage = 0L,
						Hits = 0,
						IsLocalPlayer = isLocal
					};
					value.Damagers[key] = value3;
				}
				else if (isLocal)
				{
					value3.Name = attackerName;
					value3.ClassName = className;
					value3.IsLocalPlayer = true;
				}
				value3.Damage += dmgAmount;
				value3.Hits++;
				bool flag2 = value.IsGuarding;
				if (!flag2)
				{
					lock (activeGuardingEntities)
					{
						flag2 = activeGuardingEntities.Contains(targetId);
					}
					if (flag2)
					{
						value.IsGuarding = true;
					}
				}
				if (flag2)
				{
					value3.GuardHits++;
					value3.GuardDamage += dmgAmount;
					value.TotalGuardHits++;
					value.TotalGuardDamage += dmgAmount;
				}
			}
			lock (combinedDungeonCombat.Damagers)
			{
				if (!combinedDungeonCombat.Damagers.TryGetValue(key, out var value4))
				{
					value4 = new PlayerCombatData
					{
						Name = attackerName,
						ClassName = className,
						Damage = 0L,
						Hits = 0,
						IsLocalPlayer = isLocal
					};
					combinedDungeonCombat.Damagers[key] = value4;
				}
				else if (isLocal)
				{
					value4.Name = attackerName;
					value4.ClassName = className;
					value4.IsLocalPlayer = true;
				}
				value4.Damage += dmgAmount;
				value4.Hits++;
				bool flag3 = value.IsGuarding;
				if (!flag3)
				{
					lock (activeGuardingEntities)
					{
						flag3 = activeGuardingEntities.Contains(targetId);
					}
				}
				if (flag3)
				{
					value4.GuardHits++;
					value4.GuardDamage += dmgAmount;
					combinedDungeonCombat.TotalGuardHits++;
					combinedDungeonCombat.TotalGuardDamage += dmgAmount;
				}
			}
			if (activeFocusId == -1 || value.IsBoss || (client.Enemies != null && !client.Enemies.ContainsKey(activeFocusId)))
			{
				activeFocusId = targetId;
			}
		}
		catch
		{
		}
	}

	private void RecordLocalProjectileDamage(RealmSharkBullet projectile, int targetId, int damage, bool serverConfirmed)
	{
		lock (playerBulletsLock)
		{
			if (projectile.Hits.TryGetValue(targetId, out var hit))
			{
				if (!serverConfirmed || hit.ServerConfirmed)
				{
					LogProjectileDamage(projectile, targetId, serverConfirmed ? "duplicate_server" : "duplicate_local", damage, hit.Damage, 0);
					return;
				}
				// Replace the estimate with the server amount. A confirmation is not
				// a new hit and must not restart an encounter or change guard timing.
				int delta = damage - hit.Damage;
				int hitDelta = (damage > 0 ? 1 : 0) - (hit.Damage > 0 ? 1 : 0);
				lock (allEnemies)
				{
					if (allEnemies.TryGetValue(targetId, out var enemy))
						AdjustLocalProjectileDamage(enemy, delta, hitDelta, hit.WasGuarding);
				}
				AdjustLocalProjectileDamage(combinedDungeonCombat, delta, hitDelta, hit.WasGuarding);
				LogProjectileDamage(projectile, targetId, "server_correction", damage, hit.Damage, delta);
				hit.Damage = damage;
				hit.ServerConfirmed = true;
				return;
			}
			// Keep zero confirmations as well: the later client estimate must not
			// resurrect damage the server reported as zero.
			hit = new ProjectileHit { Damage = damage, ServerConfirmed = serverConfirmed };
			projectile.Hits[targetId] = hit;
			if (damage > 0) RecordCombatDamage(targetId, damage, true, GetLocalPlayerName(), GetLocalPlayerClassName());
			LogProjectileDamage(projectile, targetId, serverConfirmed ? "server_first" : "local_estimate", damage, 0, damage);
			lock (allEnemies)
			{
				if (allEnemies.TryGetValue(targetId, out var enemy)) hit.WasGuarding = enemy.IsGuarding;
			}
		}
	}

	private void CaptureProjectileContext(RealmSharkBullet projectile)
	{
		MapObject player = client?.Player;
		if (player == null) return;
		projectile.OriginEffects = player.Effects;
		projectile.OriginEffects2 = player.Effects2;
		projectile.OriginVitality = player.Vitality;
		if (player.Inventory != null && player.Inventory.Length > 1)
			projectile.OriginAbilityItem = player.Inventory[1];
		var scaling = AbilityScalingManager.GetScalingData(projectile.ContainerType);
		if (scaling?.ScalingStat != null)
			projectile.OriginScalingStat = AbilityScalingManager.GetPlayerStatValue(player, scaling.ScalingStat.Value);
	}

	public void OnDamageBoost(DamageBoostPacket packet)
	{
		if (packet?.Payload?.Length != 8 || client?.Player == null) return;
		var context = new RealmSharkBullet { ShooterId = client.ClientId };
		CaptureProjectileContext(context);
		// Observation only: do not guess float/int fields or alter damage totals.
		LogProjectileDamage(context, 0, "combat_boost", 0, 0, 0, BitConverter.ToString(packet.Payload));
	}

	private void LogProjectileDamage(RealmSharkBullet projectile, int targetId, string kind, int reported, int previous, int delta, string combatBoostPayload = null)
	{
		MapObject target = null;
		if (client.Enemies != null) client.Enemies.TryGetValue(targetId, out target);
		if (target == null && client.Portals != null) client.Portals.TryGetValue(targetId, out target);
		combinedDungeonCombat.Damagers.TryGetValue("__LOCAL__", out var local);
		bool eventKnown = eventDamageModifiers.TryGetMultiplier(client.Player?.CrucibleId, client.Player?.BloodRitualId, out double eventMultiplier);
		damageDiagnostics.Record(string.Format(CultureInfo.InvariantCulture,
			"{0:O},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35}",
			DateTime.UtcNow, kind, projectile.ShooterId, projectile.BulletId, targetId, target?.ObjectType ?? 0,
			projectile.ContainerType, projectile.ServerCreated ? 1 : 0, projectile.TotalDmg, reported, previous, delta,
			target?.Defense ?? 0, target?.Effects ?? 0, target?.Effects2 ?? 0,
			client.Player?.Attack ?? 0, client.Player?.Vitality ?? 0, client.Player?.Wisdom ?? 0,
			client.Player?.ExaltationBonusDamage ?? 0, local?.Damage ?? 0,
			client.Player?.Effects ?? 0, client.Player?.Effects2 ?? 0, eventMultiplier, eventKnown ? 1 : 0,
			DpsDamageDiagnostics.CsvValue(client.Player?.CrucibleId), DpsDamageDiagnostics.CsvValue(client.Player?.BloodRitualId), projectile.ShotContext,
			projectile.OriginAbilityItem, projectile.OriginScalingStat == int.MinValue ? "" : projectile.OriginScalingStat.ToString(CultureInfo.InvariantCulture),
			projectile.OriginEffects, projectile.OriginEffects2,
			projectile.OriginVitality == int.MinValue ? "" : projectile.OriginVitality.ToString(CultureInfo.InvariantCulture),
			projectile.ScalingApplied ? 1 : 0, projectile.SummonerId, client.Player?.ObjectType ?? 0,
			DpsDamageDiagnostics.CsvValue(combatBoostPayload)));
	}

	private static void AdjustLocalProjectileDamage(EnemyCombatTracker combat, int delta, int hitDelta, bool wasGuarding)
	{
		combat.TotalDamage += delta;
		lock (combat.Damagers)
		{
			if (combat.Damagers.TryGetValue("__LOCAL__", out var player))
			{
				player.Damage += delta;
				player.Hits += hitDelta;
				if (wasGuarding)
				{
					player.GuardDamage += delta;
					player.GuardHits += hitDelta;
				}
			}
		}
		if (wasGuarding)
		{
			combat.TotalGuardDamage += delta;
			combat.TotalGuardHits += hitDelta;
		}
	}

	public void OnEnemyHit(EnemyHitPacket packet)
	{
		try
		{
			if (packet == null || !packet.Send || client == null)
			{
				return;
			}
			int OwnerId = packet.OwnerId;
			int TargetId = packet.TargetId;
			ushort BulletId = packet.BulletId;
			if (client.ClientId <= 0)
			{
				if (client.Player != null && client.Player.ObjectId > 0)
				{
					client.ClientId = client.Player.ObjectId;
				}
				else if (TargetId > 0)
				{
					client.ClientId = TargetId;
				}
			}
			if (client.Player == null && client.ClientId > 0 && client.Entities != null && client.Entities.TryGetValue(client.ClientId, out var value))
			{
				client.Player = value;
			}
			lock (recentShooters)
			{
				if (client.ClientId > 0)
				{
					recentShooters[client.ClientId] = Environment.TickCount;
				}
				else if (TargetId > 0)
				{
					recentShooters[TargetId] = Environment.TickCount;
				}
			}
			if (OwnerId <= 0 || IsLocalPlayerId(OwnerId) || (client.Entities != null && client.Entities.ContainsKey(OwnerId)))
			{
				return;
			}
			RealmSharkBullet value2 = null;
			lock (playerBulletsLock)
			{
				long key = ((long)TargetId << 32) | (long)((ulong)BulletId & 0xFFFFFFFFuL);
				if (!playerProjectiles.TryGetValue(key, out value2))
				{
					int num = ((client.ClientId > 0) ? client.ClientId : (client.Player?.ObjectId ?? 0));
					if (num > 0 && num != TargetId && IsLocalPlayerId(TargetId))
					{
						long key2 = ((long)num << 32) | (long)((ulong)BulletId & 0xFFFFFFFFuL);
						playerProjectiles.TryGetValue(key2, out value2);
					}
				}
			}
			if (value2 == null)
			{
				if (IsLocalPlayerId(TargetId))
					LogProjectileDamage(new RealmSharkBullet { ShooterId = TargetId, BulletId = BulletId },
						OwnerId, "untracked_hit", 0, 0, 0);
				return;
			}
			int num3 = TargetId;
			if (value2.SummonerId != 0)
			{
				num3 = value2.SummonerId;
			}
			bool flag = IsLocalPlayerId(num3);
			if (!flag && num3 <= 0 && value2.ContainerType > 0)
			{
				flag = true;
			}
			if (!flag)
			{
				return;
			}
			MapObject value3 = null;
			if ((client.Enemies == null || !client.Enemies.TryGetValue(OwnerId, out value3)) && client.Portals != null)
			{
				client.Portals.TryGetValue(OwnerId, out value3);
			}
			int defence = value3?.Defense ?? 0;
			int cond = value3?.Effects ?? 0;
			int cond2 = value3?.Effects2 ?? 0;
			if (value3 == null)
			{
				lock (allEnemies)
				{
					if (allEnemies.TryGetValue(OwnerId, out var value4))
					{
						cond = value4.Condition1;
						cond2 = value4.Condition2;
					}
				}
			}
			int baseDmg = value2.TotalDmg;
			if (!value2.ScalingApplied && value2.ContainerType > 0 && AbilityScalingManager.HasScaling(value2.ContainerType))
			{
				int? statSnapshot = (value2.OriginScalingStat != int.MinValue) ? (int?)value2.OriginScalingStat : null;
				baseDmg = AbilityScalingManager.CalculateProjectileDamage(value2.TotalDmg, value2.ContainerType,
					statSnapshot, client.Player, defence);
			}
			int num4 = RealmSharkDamageWithDefense(baseDmg, value2.ArmorPiercing, defence, cond, cond2);
			if (num4 > 0)
			{
				RecordLocalProjectileDamage(value2, OwnerId, num4, serverConfirmed: false);
			}
			else LogProjectileDamage(value2, OwnerId, "local_zero", 0, 0, 0);
		}
		catch
		{
		}
	}

	public void OnPlayerShootServer(PlayerShootServerPacket packet)
	{
		try
		{
			if (packet == null)
			{
				return;
			}
			int ownerId = packet.OwnerId;
			int summonerId = packet.SummonerId;
			if (summonerId != 0 && ownerId != 0)
			{
				lock (minionOwnerMap)
				{
					minionOwnerMap[ownerId] = summonerId;
				}
				ResolvePendingMinionOwner(ownerId, summonerId);
			}
			bool armorPiercing;
			RealmSharkData.TryGetProjectile(packet.ContainerType, packet.BulletType, out _, out _, out armorPiercing);
			int num = Math.Max(1, (int)packet.BulletCount);
			RealmSharkBullet realmSharkBullet = new RealmSharkBullet
			{
				TotalDmg = packet.Damage,
				ArmorPiercing = armorPiercing,
				SummonerId = summonerId,
				ContainerType = packet.ContainerType
			};
			CaptureProjectileContext(realmSharkBullet);
			lock (playerBulletsLock)
			{
				for (int i = 0; i < num; i++)
				{
					// Each pellet is a distinct hit; only the shooter owns its bullet namespace.
					int bulletId = (packet.BulletId + i) % 256 + 256;
					var pellet = new RealmSharkBullet
					{
						TotalDmg = realmSharkBullet.TotalDmg,
						ArmorPiercing = realmSharkBullet.ArmorPiercing,
						SummonerId = summonerId,
						ContainerType = packet.ContainerType,
						// Lethal Strike procs carry base damage only. Reconstruct their
						// bonus at impact, when target DEF is known. Ordinary abilities
						// and summons retain the damage supplied by their shot packet.
						ScalingApplied = summonerId != 0 || !AbilityScalingManager.IsLethalStrikeProjectile(packet.ContainerType),
						ServerCreated = true,
						ShooterId = ownerId,
						BulletId = bulletId,
						OriginAbilityItem = realmSharkBullet.OriginAbilityItem,
						OriginScalingStat = realmSharkBullet.OriginScalingStat,
						OriginEffects = realmSharkBullet.OriginEffects,
						OriginEffects2 = realmSharkBullet.OriginEffects2,
						OriginVitality = realmSharkBullet.OriginVitality
					};
					if (ownerId > 0) playerProjectiles[((long)ownerId << 32) | (uint)bulletId] = pellet;
					if (IsLocalPlayerId(summonerId != 0 ? summonerId : ownerId))
						LogProjectileDamage(pellet, 0, "server_shot", 0, 0, 0);
				}
			}
			int num6 = ((summonerId != 0) ? summonerId : ownerId);
			if (num6 <= 0)
			{
				return;
			}
			lock (recentShooters)
			{
				recentShooters[num6] = Environment.TickCount;
			}
		}
		catch
		{
		}
	}

	private void ResolvePendingMinionOwner(int minionId, int ownerId)
	{
		try
		{
			if (minionId > 0 && ownerId > 0)
			{
				string fromKey = $"__PENDING_{minionId}__";
				string text = null;
				string className = "Player";
				bool isLocal = false;
				MapObject value;
				PlayerParseEntry value2;
				if (IsLocalPlayerId(ownerId))
				{
					isLocal = true;
					text = GetLocalPlayerName();
					className = GetLocalPlayerClassName();
				}
				else if (client?.Entities != null && client.Entities.TryGetValue(ownerId, out value) && !string.IsNullOrEmpty(value.PlayerName))
				{
					text = value.PlayerName;
					className = GetClassName(value.ObjectType);
				}
				else if (playerRoster.TryGetValue(ownerId, out value2) && !string.IsNullOrEmpty(value2.Name))
				{
					text = value2.Name;
					className = value2.ClassName;
				}
				else
				{
					text = $"__PENDING_{ownerId}__";
				}
				MigratePendingDamagerKey(fromKey, text, className, isLocal);
			}
		}
		catch
		{
		}
	}

	private void ResolvePendingDamagers(int playerId, string playerName, string className)
	{
		try
		{
			if (playerId > 0 && !string.IsNullOrEmpty(playerName))
			{
				string fromKey = $"__PENDING_{playerId}__";
				bool isLocal = IsLocalPlayerId(playerId);
				MigratePendingDamagerKey(fromKey, playerName, className, isLocal);
			}
		}
		catch
		{
		}
	}

	private void MigratePendingDamagerKey(string fromKey, string toKey, string className, bool isLocal)
	{
		if (string.IsNullOrEmpty(fromKey) || string.IsNullOrEmpty(toKey) || string.Equals(fromKey, toKey, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string key = (isLocal ? "__LOCAL__" : toKey);
		lock (allEnemies)
		{
			foreach (EnemyCombatTracker value5 in allEnemies.Values)
			{
				lock (value5.Damagers)
				{
					if (!value5.Damagers.TryGetValue(fromKey, out var value))
					{
						continue;
					}
					value5.Damagers.Remove(fromKey);
					if (!value5.Damagers.TryGetValue(key, out var value2))
					{
						value.Name = toKey;
						value.ClassName = ((!string.IsNullOrEmpty(className)) ? className : value.ClassName);
						value.IsLocalPlayer = isLocal;
						value5.Damagers[key] = value;
						continue;
					}
					value2.Damage += value.Damage;
					value2.Hits += value.Hits;
					value2.GuardHits += value.GuardHits;
					value2.GuardDamage += value.GuardDamage;
					if (isLocal)
					{
						value2.IsLocalPlayer = true;
					}
					if (!string.IsNullOrEmpty(className) && (string.IsNullOrEmpty(value2.ClassName) || value2.ClassName == "Player"))
					{
						value2.ClassName = className;
					}
				}
			}
		}
		lock (combinedDungeonCombat.Damagers)
		{
			if (!combinedDungeonCombat.Damagers.TryGetValue(fromKey, out var value3))
			{
				return;
			}
			combinedDungeonCombat.Damagers.Remove(fromKey);
			if (!combinedDungeonCombat.Damagers.TryGetValue(key, out var value4))
			{
				value3.Name = toKey;
				value3.ClassName = ((!string.IsNullOrEmpty(className)) ? className : value3.ClassName);
				value3.IsLocalPlayer = isLocal;
				combinedDungeonCombat.Damagers[key] = value3;
				return;
			}
			value4.Damage += value3.Damage;
			value4.Hits += value3.Hits;
			value4.GuardHits += value3.GuardHits;
			value4.GuardDamage += value3.GuardDamage;
			if (isLocal)
			{
				value4.IsLocalPlayer = true;
			}
			if (!string.IsNullOrEmpty(className) && (string.IsNullOrEmpty(value4.ClassName) || value4.ClassName == "Player"))
			{
				value4.ClassName = className;
			}
		}
	}

	public void OnServerPlayerShoot(ServerPlayerShootPacket packet)
	{
		try
		{
			if (packet == null)
			{
				return;
			}
			lock (recentShooters)
			{
				recentShooters[packet._ownerId] = Environment.TickCount;
			}
		}
		catch
		{
		}
	}

	public void OnPlayerShoot(PlayerShootPacket packet)
	{
		try
		{
			if (packet == null || client == null)
			{
				return;
			}
			lock (recentShooters)
			{
				if (client.ClientId > 0)
				{
					recentShooters[client.ClientId] = Environment.TickCount;
				}
			}
			int num = unchecked((ushort)packet.WeaponId);
			if ((num == 0 || num == ushort.MaxValue) && client.Player?.Inventory != null && client.Player.Inventory.Length != 0)
			{
				num = client.Player.Inventory[0];
			}
			int projectileId = ((packet.ProjectileTypeId >= 0) ? packet.ProjectileTypeId : 0);
			ushort BulletId = packet.BulletId;
			int min = 0;
			int max = 0;
			bool ap = false;
			if (!RealmSharkData.TryGetProjectile(num, projectileId, out min, out max, out ap))
			{
				// Do not manufacture damage from a different projectile or a guessed range.
				return;
			}
			// ItemDataString has one enchantment payload per inventory slot. Match the
			// actual fired item so an equipped weapon never scales an unrelated proc.
			string shotEnchants = null;
			if (client.Player?.Inventory != null && !string.IsNullOrEmpty(client.Player.ItemDataString))
			{
				string[] slots = client.Player.ItemDataString.Split(',');
				for (int slot = 0; slot < Math.Min(4, client.Player.Inventory.Length) && slot < slots.Length; slot++)
					if (client.Player.Inventory[slot] == num) { shotEnchants = slots[slot]; break; }
			}
			// Apply only damage mutators; the packet stream already counts extra shots and fire rate.
			EnchantmentParser.ApplyProjectileDamageModifiers(shotEnchants, projectileId, ref min, ref max);
			int num2 = min;
			if (min != max)
			{
				long num3 = ((realmSharkRng != null) ? realmSharkRng.Next() : 0);
				num2 = (int)(min + num3 % (uint)(max - min));
			}
			int num4 = RealmSharkData.GetSlotType(num);
			if (num4 <= 0)
			{
				num4 = ItemSpriteManager.GetItemMetadata(num)?.SlotType ?? 0;
			}
			bool isMainWeapon = IsMainWeaponSlot(num4);
			if (!isMainWeapon && AbilityScalingManager.HasScaling(num))
			{
				int statBonus = AbilityScalingManager.CalculateStatBonus(num, null, client?.Player);
				num2 += statBonus;
			}
			float num5 = (isMainWeapon ? GetRealmSharkPlayerStatsMultiplier() : 1f);
			int totalDmg = (int)((float)num2 * num5);
			RealmSharkBullet realmSharkBullet = new RealmSharkBullet
			{
				TotalDmg = totalDmg,
				ArmorPiercing = ap,
				SummonerId = 0,
				ContainerType = num,
				ShooterId = client.ClientId,
				BulletId = BulletId,
				ShotContext = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5}", projectileId, min, max, num2, num5, DpsDamageDiagnostics.CsvValue(shotEnchants)),
				ScalingApplied = AbilityScalingManager.GetScalingData(num)?.IsLethalStrike != true
			};
			CaptureProjectileContext(realmSharkBullet);
			LogProjectileDamage(realmSharkBullet, 0, "local_shot", 0, 0, 0);
			lock (playerBulletsLock)
			{
				playerBullets[BulletId % 512] = realmSharkBullet;
				if (BulletId < 512)
				{
					playerBullets[BulletId] = realmSharkBullet;
				}
				if (client.ClientId > 0)
				{
					long key = ((long)client.ClientId << 32) | BulletId;
					playerProjectiles[key] = realmSharkBullet;
				}
				if (client.Player != null && client.Player.ObjectId > 0)
				{
					long key2 = ((long)client.Player.ObjectId << 32) | BulletId;
					playerProjectiles[key2] = realmSharkBullet;
				}
			}
		}
		catch
		{
		}
	}

	public void OnShowEffect(ShowEffectPacket packet)
	{
		try
		{
			if (packet == null || packet.OwnerId <= 0)
			{
				return;
			}
			lock (recentShooters)
			{
				recentShooters[packet.OwnerId] = Environment.TickCount;
			}
			if (packet.EffectType == EffectType.Collapse)
			{
				HandleMysticCollapse(packet.OwnerId);
			}
		}
		catch
		{
		}
	}

	public static void LogAbilityUsage(string playerName, string className, string abilityType, string abilityItemName, string details, bool isLocal)
	{
		try
		{
			if (string.IsNullOrEmpty(playerName))
			{
				playerName = "Unknown";
			}
			lock (abilityLogLock)
			{
				DateTime now = DateTime.Now;
				for (int i = sessionAbilityLog.Count - 1; i >= Math.Max(0, sessionAbilityLog.Count - 6); i--)
				{
					AbilityLogEntry entry = sessionAbilityLog[i];
					if (entry.PlayerName == playerName && entry.AbilityType == abilityType && (now - entry.Timestamp).TotalMilliseconds < 800.0)
					{
						return;
					}
				}
				sessionAbilityLog.Add(new AbilityLogEntry
				{
					Timestamp = now,
					PlayerName = playerName,
					ClassName = className,
					AbilityType = abilityType,
					AbilityItemName = abilityItemName,
					Details = details,
					IsLocalPlayer = isLocal
				});
				if (sessionAbilityLog.Count > 250)
				{
					sessionAbilityLog.RemoveAt(0);
				}
			}
		}
		catch
		{
		}
	}

	public static void ClearAbilityLog()
	{
		lock (abilityLogLock)
		{
			sessionAbilityLog.Clear();
		}
		PushOverlaySnapshot();
	}

	public void OnPartyMemberInfo(IncomingPartyMemberInfoPacket packet)
	{
		if (packet == null) return;
		try
		{
			lock (partyLock)
			{
				sessionPartyId = packet.PartyId;
				if (packet.PartyPlayers == null || packet.PartyPlayers.Length == 0)
				{
					sessionPartyMembers.Clear();
				}
				else
				{
					HashSet<string> currentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (var p in packet.PartyPlayers)
					{
						if (p == null || string.IsNullOrWhiteSpace(p.Name)) continue;
						string name = p.Name.Trim();
						currentNames.Add(name);

						if (!sessionPartyMembers.TryGetValue(name, out var entry))
						{
							entry = new PartyTrackerEntry
							{
								PlayerId = p.Id,
								Name = name,
								ObjectId = p.ObjectId
							};
							sessionPartyMembers[name] = entry;
						}
						else
						{
							entry.PlayerId = p.Id;
							entry.ObjectId = p.ObjectId;
						}

						if (entry.ClassName == "Unknown" || string.IsNullOrEmpty(entry.ClassName))
						{
							if (client?.Player != null && string.Equals(client.Player.PlayerName, name, StringComparison.OrdinalIgnoreCase))
							{
								entry.ClassName = GetClassName(client.Player.ObjectType);
							}
							else
							{
								lock (playerRoster)
								{
									var match = playerRoster.Values.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
									if (match != null)
									{
										entry.ClassName = match.ClassName;
									}
								}
							}
						}
					}

					var toRemove = sessionPartyMembers.Keys.Where(k => !currentNames.Contains(k)).ToList();
					foreach (var k in toRemove)
					{
						sessionPartyMembers.Remove(k);
					}
				}
			}
			PushOverlaySnapshot();
		}
		catch
		{
		}
	}

	public void OnPartyMemberAdded(PartyMemberAddedPacket packet)
	{
		if (packet == null || string.IsNullOrWhiteSpace(packet.Name)) return;
		try
		{
			lock (partyLock)
			{
				string name = packet.Name.Trim();
				string cName = GetClassName((ushort)packet.ClassId);
				if (string.IsNullOrEmpty(cName)) cName = "Unknown";

				if (sessionPartyMembers.TryGetValue(name, out var existing))
				{
					existing.PlayerId = packet.PlayerId;
					existing.ClassId = packet.ClassId;
					if (cName != "Unknown") existing.ClassName = cName;
				}
				else
				{
					sessionPartyMembers[name] = new PartyTrackerEntry
					{
						PlayerId = packet.PlayerId,
						Name = name,
						ClassId = packet.ClassId,
						ClassName = cName
					};
				}
			}
			PushOverlaySnapshot();
		}
		catch
		{
		}
	}

	public void OnPartyActionResult(PartyActionResultPacket packet)
	{
		if (packet == null) return;
		try
		{
			lock (partyLock)
			{
				if (packet.Action == PartyActionType.LeftParty || packet.Action == PartyActionType.Kicked)
				{
					var match = sessionPartyMembers.Values.FirstOrDefault(m => m.PlayerId == packet.PlayerId);
					if (match != null)
					{
						if (client?.Player != null && string.Equals(match.Name, client.Player.PlayerName, StringComparison.OrdinalIgnoreCase))
						{
							sessionPartyMembers.Clear();
						}
						else
						{
							sessionPartyMembers.Remove(match.Name);
						}
					}
				}
			}
			PushOverlaySnapshot();
		}
		catch
		{
		}
	}

	public static void ClearParty()
	{
		lock (partyLock)
		{
			sessionPartyMembers.Clear();
			sessionPartyId = 0;
		}
		PushOverlaySnapshot();
	}

	public static void AddPartyMember(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return;
		lock (partyLock)
		{
			string n = name.Trim();
			if (!sessionPartyMembers.ContainsKey(n))
			{
				sessionPartyMembers[n] = new PartyTrackerEntry
				{
					Name = n,
					ClassName = "Unknown"
				};
			}
		}
		PushOverlaySnapshot();
	}

	public static void RemovePartyMember(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return;
		lock (partyLock)
		{
			sessionPartyMembers.Remove(name.Trim());
		}
		PushOverlaySnapshot();
	}

	private void HandleMysticCollapse(int ownerId)
	{
		try
		{
			if (ownerId <= 0) return;
			string playerName = "Mystic";
			string className = "Mystic";
			string abilityName = "Stasis Orb";
			bool isLocal = IsLocalPlayerId(ownerId);
			if (isLocal)
			{
				playerName = GetLocalPlayerName();
				className = GetLocalPlayerClassName();
				if (client?.Player?.Inventory != null && client.Player.Inventory.Length > 1)
				{
					abilityName = ItemSpriteManager.GetItemMetadata(client.Player.Inventory[1]).Name;
				}
			}
			else if (playerRoster.TryGetValue(ownerId, out var entry))
			{
				playerName = entry.Name;
				className = entry.ClassName;
				if (entry.EquipmentNames != null && entry.EquipmentNames.Length > 1)
				{
					abilityName = entry.EquipmentNames[1];
				}
			}
			else if (client?.Entities != null && client.Entities.TryGetValue(ownerId, out var ent))
			{
				if (!string.IsNullOrEmpty(ent.PlayerName)) playerName = ent.PlayerName;
				className = GetClassName(ent.ObjectType);
				if (ent.Inventory != null && ent.Inventory.Length > 1)
				{
					abilityName = ItemSpriteManager.GetItemMetadata(ent.Inventory[1]).Name;
				}
			}
			LogAbilityUsage(playerName, className, "STASIS", abilityName, "Stasis Cast", isLocal);
		}
		catch
		{
		}
	}

	public void OnStasis(StasisPacket packet)
	{
		try
		{
			if (packet == null || packet.StasisDuration <= 0f) return;
			lock (abilityLogLock)
			{
				for (int i = sessionAbilityLog.Count - 1; i >= Math.Max(0, sessionAbilityLog.Count - 6); i--)
				{
					var entry = sessionAbilityLog[i];
					if (entry.AbilityType == "STASIS" && (DateTime.Now - entry.Timestamp).TotalMilliseconds < 1500.0)
					{
						entry.Details = $"Stasis ({packet.StasisDuration:0.0}s)";
						return;
					}
				}
				sessionAbilityLog.Add(new AbilityLogEntry
				{
					Timestamp = DateTime.Now,
					PlayerName = "Mystic",
					ClassName = "Mystic",
					AbilityType = "STASIS",
					AbilityItemName = "Stasis Orb",
					Details = $"Stasis ({packet.StasisDuration:0.0}s)",
					IsLocalPlayer = false
				});
			}
		}
		catch
		{
		}
	}

	public void OnUseItem(UseItemPacket packet)
	{
		try
		{
			if (packet == null || packet.Item == null) return;
			if (packet.Item.SlotId != 1) return;
			int itemId = packet.Item.ItemType;
			if (itemId <= 0 && client?.Player?.Inventory != null && client.Player.Inventory.Length > 1)
			{
				itemId = client.Player.Inventory[1];
			}
			ItemSpriteManager.ItemMeta meta = ItemSpriteManager.GetItemMetadata(itemId);
			string name = GetLocalPlayerName();
			string cls = GetLocalPlayerClassName();
			string itemText = meta.Name;

			if (cls == "Trickster" || meta.SlotType == 10)
			{
				LogAbilityUsage(name, "Trickster", "DECOY", itemText, "Decoy Thrown", true);
			}
			else if (cls == "Mystic" || meta.SlotType == 21)
			{
				LogAbilityUsage(name, "Mystic", "STASIS", itemText, "Stasis Cast", true);
			}
			else if (cls == "Knight" || meta.SlotType == 5)
			{
				if (itemText.IndexOf("Ogmur", StringComparison.OrdinalIgnoreCase) >= 0)
					LogAbilityUsage(name, "Knight", "ARMOR BREAK", itemText, "Ogmur Armor Break", true);
				else
					LogAbilityUsage(name, "Knight", "STUN", itemText, "Shield Stun", true);
			}
			else if (cls == "Archer" || meta.SlotType == 4)
			{
				LogAbilityUsage(name, "Archer", "PARALYZE", itemText, "Quiver Shot", true);
			}
			else if (cls == "Huntress" || meta.SlotType == 18)
			{
				LogAbilityUsage(name, "Huntress", "SLOW/TRAP", itemText, "Trap Thrown", true);
			}
		}
		catch
		{
		}
	}

	private void CheckDecoySpawned(ObjectData objectData)
	{
		try
		{
			if (objectData == null) return;
			if (DecoyObjectTypes.Contains(objectData.ObjectType))
			{
				lastDecoySpawnTick = Environment.TickCount;
				lastDecoyPos = objectData.Stats?.Position ?? WorldPosData.Zero;
				List<PlayerParseEntry> tricksters = null;
				lock (playerRoster)
				{
					tricksters = playerRoster.Values.Where(p => p.ClassName == "Trickster").ToList();
				}
				if (tricksters != null && tricksters.Count == 1)
				{
					var t = tricksters[0];
					string item = (t.EquipmentNames != null && t.EquipmentNames.Length > 1) ? t.EquipmentNames[1] : "Prism";
					LogAbilityUsage(t.Name, "Trickster", "DECOY", item, "Decoy Spawned", t.IsLocalPlayer);
				}
			}
		}
		catch
		{
		}
	}

	private void CheckPlayerMpDrop(int objectId, int newMp)
	{
		try
		{
			if (objectId <= 0) return;
			bool hadMp = playerLastMp.TryGetValue(objectId, out int oldMp);
			playerLastMp[objectId] = newMp;
			if (!hadMp || oldMp <= newMp) return;
			int mpSpent = oldMp - newMp;
			if (mpSpent < 30) return;

			bool isLocal = IsLocalPlayerId(objectId);
			string playerName = isLocal ? GetLocalPlayerName() : null;
			string className = isLocal ? GetLocalPlayerClassName() : null;
			int abilityId = -1;

			if (isLocal)
			{
				if (client?.Player?.Inventory != null && client.Player.Inventory.Length > 1)
				{
					abilityId = client.Player.Inventory[1];
				}
			}
			else if (playerRoster.TryGetValue(objectId, out var rosterEntry))
			{
				playerName = rosterEntry.Name;
				className = rosterEntry.ClassName;
				if (rosterEntry.EquipmentIds != null && rosterEntry.EquipmentIds.Length > 1)
				{
					abilityId = rosterEntry.EquipmentIds[1];
				}
			}
			else if (client?.Entities != null && client.Entities.TryGetValue(objectId, out var mapObj) && mapObj.IsPlayer)
			{
				playerName = mapObj.PlayerName;
				className = GetClassName(mapObj.ObjectType);
				if (mapObj.Inventory != null && mapObj.Inventory.Length > 1)
				{
					abilityId = mapObj.Inventory[1];
				}
			}

			if (string.IsNullOrEmpty(playerName) || abilityId <= 0) return;
			ItemSpriteManager.ItemMeta meta = ItemSpriteManager.GetItemMetadata(abilityId);
			string abilityName = meta.Name;

			if (className == "Trickster" || meta.SlotType == 10)
			{
				int tick = Environment.TickCount;
				if (Math.Abs(tick - lastDecoySpawnTick) < 1500)
				{
					LogAbilityUsage(playerName, "Trickster", "DECOY", abilityName, "Decoy Spawned", isLocal);
				}
			}
			else if (className == "Knight" || meta.SlotType == 5)
			{
				if (abilityName.IndexOf("Ogmur", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					LogAbilityUsage(playerName, "Knight", "ARMOR BREAK", abilityName, "Ogmur Armor Break", isLocal);
				}
				else
				{
					LogAbilityUsage(playerName, "Knight", "STUN", abilityName, "Shield Stun", isLocal);
				}
			}
			else if (className == "Mystic" || meta.SlotType == 21)
			{
				LogAbilityUsage(playerName, "Mystic", "STASIS", abilityName, "Stasis Cast", isLocal);
			}
			else if (className == "Archer" || meta.SlotType == 4)
			{
				LogAbilityUsage(playerName, "Archer", "PARALYZE", abilityName, "Quiver Shot", isLocal);
			}
			else if (className == "Huntress" || meta.SlotType == 18)
			{
				LogAbilityUsage(playerName, "Huntress", "SLOW/TRAP", abilityName, "Trap Thrown", isLocal);
			}
		}
		catch
		{
		}
	}

	public void OnEnemyShootDamage(EnemyShootPacket packet)
	{
		try
		{
			if (packet == null || client == null)
			{
				return;
			}
			int OwnerId = packet.OwnerId;
			if (OwnerId <= 0 || IsLocalPlayerId(OwnerId) || (client.Entities != null && client.Entities.ContainsKey(OwnerId)))
			{
				return;
			}
			ushort _damageAmount = packet._damageAmount;
			_ = packet.BulletId;
			int ObjectId = packet.ObjectId;
			if (ObjectId <= 0 || ObjectId == 16777215 || ObjectId == 16777215 || ObjectId >= 10000000)
			{
				RecordCombatDamage(OwnerId, _damageAmount, isLocal: false, null, null);
			}
			else
			{
				bool isKnownSummon;
				lock (minionOwnerMap) isKnownSummon = minionOwnerMap.ContainsKey(ObjectId);
				if (!isKnownSummon && client.Enemies != null && client.Enemies.ContainsKey(ObjectId))
				{
					return;
				}
				string text = null;
				string text2 = "Player";
				bool flag = false;
				MapObject value;
				PlayerParseEntry value2;
				if (IsLocalPlayerId(ObjectId))
				{
					flag = true;
					text = GetLocalPlayerName();
					text2 = GetLocalPlayerClassName();
				}
				else if (client.Entities != null && client.Entities.TryGetValue(ObjectId, out value) && !string.IsNullOrEmpty(value.PlayerName))
				{
					text = value.PlayerName;
					text2 = GetClassName(value.ObjectType);
					if (string.Equals(text, GetLocalPlayerName(), StringComparison.OrdinalIgnoreCase))
					{
						flag = true;
						if (client.ClientId <= 0)
						{
							client.ClientId = ObjectId;
						}
					}
					else
					{
						flag = false;
					}
				}
				else if (playerRoster.TryGetValue(ObjectId, out value2) && !string.IsNullOrEmpty(value2.Name))
				{
					text = value2.Name;
					text2 = value2.ClassName;
					if (string.Equals(text, GetLocalPlayerName(), StringComparison.OrdinalIgnoreCase))
					{
						flag = true;
						if (client.ClientId <= 0)
						{
							client.ClientId = ObjectId;
						}
					}
					else
					{
						flag = false;
					}
				}
				else
				{
					int value3 = 0;
					lock (minionOwnerMap)
					{
						minionOwnerMap.TryGetValue(ObjectId, out value3);
					}
					if (value3 <= 0)
					{
						RecordCombatDamage(OwnerId, _damageAmount, isLocal: false, null, null);
						return;
					}
					MapObject value4;
					if (IsLocalPlayerId(value3))
					{
						flag = true;
						text = GetLocalPlayerName();
						text2 = GetLocalPlayerClassName();
					}
					else if (client.Entities != null && client.Entities.TryGetValue(value3, out value4) && !string.IsNullOrEmpty(value4.PlayerName))
					{
						text = value4.PlayerName;
						text2 = GetClassName(value4.ObjectType);
						if (string.Equals(text, GetLocalPlayerName(), StringComparison.OrdinalIgnoreCase))
						{
							flag = true;
							if (client.ClientId <= 0)
							{
								client.ClientId = value3;
							}
						}
						else
						{
							flag = false;
						}
					}
					else
					{
						if (!playerRoster.TryGetValue(value3, out var value5) || string.IsNullOrEmpty(value5.Name))
						{
							RecordCombatDamage(OwnerId, _damageAmount, isLocal: false, null, null);
							return;
						}
						text = value5.Name;
						text2 = value5.ClassName;
						if (string.Equals(text, GetLocalPlayerName(), StringComparison.OrdinalIgnoreCase))
						{
							flag = true;
							if (client.ClientId <= 0)
							{
								client.ClientId = value3;
							}
						}
						else
						{
							flag = false;
						}
					}
				}
				// Reconcile a matching estimate even when the server reports zero.
				if (flag)
				{
					lock (playerBulletsLock)
					{
						long key = ((long)ObjectId << 32) | packet.BulletId;
						if (playerProjectiles.TryGetValue(key, out var projectile))
						{
							RecordLocalProjectileDamage(projectile, OwnerId, _damageAmount, serverConfirmed: true);
							return;
						}
					}
				}
				RecordCombatDamage(OwnerId, _damageAmount, flag, text, text2);
				if (flag)
					LogProjectileDamage(new RealmSharkBullet { ShooterId = ObjectId, BulletId = packet.BulletId },
						OwnerId, "server_unmatched", _damageAmount, 0, _damageAmount);
			}
		}
		catch
		{
		}
	}

	private void UpdatePlayerRosterEntry(MapObject player)
	{
		if (player != null && !string.IsNullOrEmpty(player.PlayerName))
		{
			string PlayerName = player.PlayerName;
			string className = GetClassName(player.ObjectType);
			(string display, int count) maxedStats = GetMaxedStats(player);
			string item = maxedStats.display;
			int item2 = maxedStats.count;
			int[] array = new int[4];
			string[] array2 = new string[4];
			string[] array3 = new string[4];
			string[] array4 = new string[4];
			int[] array5 = new int[4];
			List<string>[] array6 = new List<string>[4];
			string[] array7 = null;
			if (!string.IsNullOrEmpty(player.ItemDataString))
			{
				array7 = player.ItemDataString.Split(',');
			}
			for (int i = 0; i < 4; i++)
			{
				int itemId = (array[i] = ((player.Inventory != null && player.Inventory.Length > i) ? player.Inventory[i] : (-1)));
				ItemSpriteManager.ItemMeta itemMetadata = ItemSpriteManager.GetItemMetadata(itemId);
				array2[i] = itemMetadata.Name;
				var (num, text, list) = EnchantmentParser.ParseSlotEnchants((array7 != null && array7.Length > i) ? array7[i] : null);
				array4[i] = text;
				array5[i] = num;
				array6[i] = list;
				array3[i] = GetItemTier(itemId);
			}
			int totalExalts = 0;
			int[] array8 = new int[8];
			if (player.Exaltations != null)
			{
				Array.Copy(player.Exaltations, array8, Math.Min(8, player.Exaltations.Length));
				totalExalts = player.TotalExaltations;
			}
			PlayerParseEntry value = new PlayerParseEntry
			{
				Name = PlayerName,
				ClassName = className,
				MaxedStats = item,
				MaxedCount = item2,
				Exaltations = array8,
				TotalExalts = totalExalts,
				EquipmentIds = array,
				EquipmentNames = array2,
				EquipmentTiers = array3,
				EquipmentRarities = array4,
				EquipmentEnchantCounts = array5,
				EquipmentEnchants = array6,
				Stars = player.PetType,
				GuildName = (player.PetName ?? ""),
				CurrentHp = player.Hp,
				MaxHp = player.MaxHp,
				IsLocalPlayer = IsLocalPlayerId(player.ObjectId)
			};
			lock (playerRoster)
			{
				var staleKeys = playerRoster
					.Where(kvp => kvp.Key != player.ObjectId && string.Equals(kvp.Value.Name, PlayerName, StringComparison.OrdinalIgnoreCase))
					.Select(kvp => kvp.Key)
					.ToList();
				foreach (int oldKey in staleKeys)
				{
					playerRoster.Remove(oldKey);
				}
				playerRoster[player.ObjectId] = value;
			}
			ResolvePendingDamagers(player.ObjectId, PlayerName, className);
		}
	}

	public static void NavigatePreviousDungeon()
	{
		lock (sessionDungeonHistory)
		{
			if (sessionDungeonHistory.Count == 0)
			{
				return;
			}
			if (currentHistoryIndex == -1)
			{
				currentHistoryIndex = sessionDungeonHistory.Count - 1;
			}
			else if (currentHistoryIndex > 0)
			{
				currentHistoryIndex--;
			}
		}
		selectedMonsterMode = -1;
		PushOverlaySnapshot();
	}

	public static void NavigateNextDungeon()
	{
		lock (sessionDungeonHistory)
		{
			if (currentHistoryIndex == -1)
			{
				return;
			}
			if (currentHistoryIndex < sessionDungeonHistory.Count - 1)
			{
				currentHistoryIndex++;
			}
			else
			{
				currentHistoryIndex = -1;
			}
		}
		selectedMonsterMode = -1;
		PushOverlaySnapshot();
	}

	public static void NavigateLiveDungeon()
	{
		currentHistoryIndex = -1;
		selectedMonsterMode = -1;
		PushOverlaySnapshot();
	}

	public static List<DungeonHistoryItem> GetDungeonHistoryList()
	{
		List<DungeonHistoryItem> list = new List<DungeonHistoryItem>();
		string dungeonName = activeTrackerInstance?.currentDungeonName ?? lastLiveSnapshot?.DungeonName ?? "Nexus";
		double num = ((activeTrackerInstance != null && activeTrackerInstance.dungeonStartTick > 0) ? ((double)(Environment.TickCount - activeTrackerInstance.dungeonStartTick) / 1000.0) : (lastLiveSnapshot?.ElapsedDungeonSeconds ?? 0.0));
		int num2 = (int)(num / 60.0);
		int num3 = (int)(num % 60.0);
		int kills = activeTrackerInstance?.totalKillsCount ?? lastLiveSnapshot?.KillsCount ?? 0;
		long damage = activeTrackerInstance?.combinedDungeonCombat?.TotalDamage ?? lastLiveSnapshot?.TotalDungeonDamage ?? 0;
		int playerCount = activeTrackerInstance?.playerRoster?.Count ?? (lastLiveSnapshot?.Players?.Count).GetValueOrDefault();
		list.Add(new DungeonHistoryItem
		{
			Index = -1,
			DungeonName = dungeonName,
			TimeStr = $"{num2:D2}:{num3:D2}",
			Kills = kills,
			Damage = damage,
			PlayerCount = playerCount,
			IsCurrent = (currentHistoryIndex == -1)
		});
		lock (sessionDungeonHistory)
		{
			for (int num4 = sessionDungeonHistory.Count - 1; num4 >= 0; num4--)
			{
				DpsSnapshot dpsSnapshot = sessionDungeonHistory[num4];
				int num5 = (int)(dpsSnapshot.ElapsedDungeonSeconds / 60.0);
				int num6 = (int)(dpsSnapshot.ElapsedDungeonSeconds % 60.0);
				list.Add(new DungeonHistoryItem
				{
					Index = num4,
					DungeonName = dpsSnapshot.DungeonName,
					TimeStr = $"{num5:D2}:{num6:D2}",
					Kills = dpsSnapshot.KillsCount,
					Damage = dpsSnapshot.TotalDungeonDamage,
					PlayerCount = (dpsSnapshot.Players?.Count ?? 0),
					IsCurrent = (currentHistoryIndex == num4)
				});
			}
			return list;
		}
	}

	public static void SelectDungeonByIndex(int index)
	{
		lock (sessionDungeonHistory)
		{
			if (index >= 0 && index < sessionDungeonHistory.Count)
			{
				currentHistoryIndex = index;
			}
			else
			{
				currentHistoryIndex = -1;
			}
		}
		selectedMonsterMode = -1;
		PushOverlaySnapshot();
	}

	public static void SetSelectedMonsterMode(int mode)
	{
		selectedMonsterMode = mode;
		PushOverlaySnapshot();
	}

	public static void PushOverlaySnapshot()
	{
		lock (sessionDungeonHistory)
		{
			if (currentHistoryIndex >= 0 && currentHistoryIndex < sessionDungeonHistory.Count)
			{
				DpsSnapshot dpsSnapshot = sessionDungeonHistory[currentHistoryIndex];
				dpsSnapshot.HistoryIndex = currentHistoryIndex;
				dpsSnapshot.HistoryTotalCount = sessionDungeonHistory.Count;
				dpsSnapshot.SelectedMonsterMode = selectedMonsterMode;
				DpsOverlayManager.UpdateSnapshot(dpsSnapshot);
				return;
			}
		}
		if (activeTrackerInstance != null)
		{
			activeTrackerInstance.PushSnapshotToOverlay();
		}
		else if (lastLiveSnapshot != null)
		{
			lastLiveSnapshot.HistoryIndex = -1;
			lock (sessionDungeonHistory)
			{
				lastLiveSnapshot.HistoryTotalCount = sessionDungeonHistory.Count;
			}
			lastLiveSnapshot.SelectedMonsterMode = selectedMonsterMode;
			DpsOverlayManager.UpdateSnapshot(lastLiveSnapshot);
		}
	}

	public void PushSnapshotToOverlay()
	{
		try
		{
			activeTrackerInstance = this;
			lock (sessionDungeonHistory)
			{
				if (currentHistoryIndex >= 0 && currentHistoryIndex < sessionDungeonHistory.Count)
				{
					lastLiveSnapshot = CompileCurrentSnapshot();
					return;
				}
			}
			DpsSnapshot dpsSnapshot = CompileCurrentSnapshot();
			dpsSnapshot.HistoryIndex = -1;
			lock (sessionDungeonHistory)
			{
				dpsSnapshot.HistoryTotalCount = sessionDungeonHistory.Count;
			}
			dpsSnapshot.SelectedMonsterMode = selectedMonsterMode;
			lastLiveSnapshot = dpsSnapshot;
			DpsOverlayManager.UpdateSnapshot(dpsSnapshot);
		}
		catch
		{
		}
	}

	private DpsSnapshot CompileCurrentSnapshot()
	{
		double num = ((dungeonStartTick > 0) ? ((double)(Environment.TickCount - dungeonStartTick) / 1000.0) : 1.0);
		if (num < 1.0)
		{
			num = 1.0;
		}
		double totalGroupDps = ((num > 0.0) ? Math.Round((double)combinedDungeonCombat.TotalDamage / num, 1) : 0.0);
		List<EnemyCombatSnapshot> list = new List<EnemyCombatSnapshot>();
		lock (defeatedEnemies)
		{
			foreach (EnemyCombatTracker defeatedEnemy in defeatedEnemies)
			{
				list.Add(defeatedEnemy.ToSnapshot());
			}
		}
		list = (from e in list
			orderby e.IsBoss descending, e.TotalDamage descending
			select e).ToList();
		EnemyCombatTracker value = null;
		if (activeFocusId != -1)
		{
			lock (allEnemies)
			{
				allEnemies.TryGetValue(activeFocusId, out value);
			}
		}
		if (value == null || value.IsDead)
		{
			lock (allEnemies)
			{
				EnemyCombatTracker enemyCombatTracker = allEnemies.Values.FirstOrDefault((EnemyCombatTracker e) => !e.IsDead && e.IsBoss && e.TotalDamage > 0);
				if (enemyCombatTracker != null)
				{
					value = enemyCombatTracker;
					activeFocusId = enemyCombatTracker.EntityId;
				}
				else
				{
					EnemyCombatTracker enemyCombatTracker2 = allEnemies.Values.FirstOrDefault((EnemyCombatTracker e) => !e.IsDead && e.TotalDamage > 0);
					if (enemyCombatTracker2 != null)
					{
						value = enemyCombatTracker2;
						activeFocusId = enemyCombatTracker2.EntityId;
					}
				}
			}
		}
		EnemyCombatSnapshot activeEnemy = ((value != null) ? value.ToSnapshot() : ((list.Count <= 0) ? new EnemyCombatSnapshot
		{
			Name = "Waiting for Target...",
			MaxHp = 0,
			CurrentHp = 0
		} : list[0]));
		EnemyCombatSnapshot allCombinedEnemy = combinedDungeonCombat.ToSnapshot();
		List<PlayerParseEntry> players;
		lock (playerRoster)
		{
			players = playerRoster.Values
				.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
				.Select(g => g.Last())
				.OrderByDescending(p => p.MaxedCount)
				.ThenBy(p => p.Name)
				.ToList();
		}
		LocalPlayerInfo localPlayerInfo = new LocalPlayerInfo();
		if (client != null && client.Player != null)
		{
			MapObject Player = client.Player;
			localPlayerInfo.Name = ((!string.IsNullOrEmpty(Player.PlayerName)) ? Player.PlayerName : "Player");
			localPlayerInfo.ClassName = GetClassName(Player.ObjectType);
			localPlayerInfo.Stars = Player.Stars;
			localPlayerInfo.CurrentHp = Player.Hp;
			localPlayerInfo.MaxHp = Player.MaxHp;
			localPlayerInfo.CurrentMp = Player.Mp;
			localPlayerInfo.MaxMp = Player.MaxMp;
			localPlayerInfo.Attack = Player.Attack;
			localPlayerInfo.Defense = Player.Defense;
			localPlayerInfo.Speed = Player.Speed;
			localPlayerInfo.Vitality = Player.Vitality;
			localPlayerInfo.Wisdom = Player.Wisdom;
			localPlayerInfo.Dexterity = Player.Dexterity;
			localPlayerInfo.BaseLife = Player.MaxHp - Player.HealthBonus;
			localPlayerInfo.BaseMana = Player.MaxMp - Player.ManaBonus;
			localPlayerInfo.BaseAttack = Player.Attack - Player.AttackBonus;
			localPlayerInfo.BaseDefense = Player.Defense - Player.DefenseBonus;
			localPlayerInfo.BaseSpeed = Player.Speed - Player.SpeedBonus;
			localPlayerInfo.BaseVitality = Player.Vitality - Player.VitalityBonus;
			localPlayerInfo.BaseWisdom = Player.Wisdom - Player.WisdomBonus;
			localPlayerInfo.BaseDexterity = Player.Dexterity - Player.DexterityBonus;
			if (ClassStats.Map != null && ClassStats.Map.TryGetValue(Player.ObjectType, out var value2))
			{
				localPlayerInfo.MaxLife = value2.LifeMax;
				localPlayerInfo.MaxMana = value2.ManaMax;
				localPlayerInfo.MaxAttack = value2.AttackMax;
				localPlayerInfo.MaxDefense = value2.DefenseMax;
				localPlayerInfo.MaxSpeed = value2.SpeedMax;
				localPlayerInfo.MaxVitality = value2.VitalityMax;
				localPlayerInfo.MaxWisdom = value2.WisdomMax;
				localPlayerInfo.MaxDexterity = value2.DexterityMax;
				int num2 = 0;
				if (localPlayerInfo.BaseLife >= value2.LifeMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseMana >= value2.ManaMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseAttack >= value2.AttackMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseDefense >= value2.DefenseMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseSpeed >= value2.SpeedMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseDexterity >= value2.DexterityMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseVitality >= value2.VitalityMax)
				{
					num2++;
				}
				if (localPlayerInfo.BaseWisdom >= value2.WisdomMax)
				{
					num2++;
				}
				localPlayerInfo.MaxedCount = num2;
				localPlayerInfo.MaxedStats = $"{num2}/8";
			}
			if (Player.Exaltations != null)
			{
				Array.Copy(Player.Exaltations, localPlayerInfo.Exaltations, Math.Min(8, Player.Exaltations.Length));
				localPlayerInfo.TotalExalts = Player.TotalExaltations;
			}
			string[] array = null;
			if (!string.IsNullOrEmpty(Player.ItemDataString))
			{
				array = Player.ItemDataString.Split(',');
			}
			for (int num3 = 0; num3 < 4; num3++)
			{
				int num4 = ((Player.Inventory != null && Player.Inventory.Length > num3) ? Player.Inventory[num3] : (-1));
				localPlayerInfo.EquipmentIds[num3] = num4;
				ItemSpriteManager.ItemMeta itemMetadata = ItemSpriteManager.GetItemMetadata(num4);
				localPlayerInfo.EquipmentNames[num3] = itemMetadata.Name;
				var (num5, text, list2) = EnchantmentParser.ParseSlotEnchants((array != null && array.Length > num3) ? array[num3] : null);
				localPlayerInfo.EquipmentRarities[num3] = text;
				localPlayerInfo.EquipmentEnchantCounts[num3] = num5;
				localPlayerInfo.EquipmentEnchants[num3] = list2;
			}
			int num6 = localPlayerInfo.EquipmentIds[0];
			if (num6 > 0)
			{
				ItemSpriteManager.ItemMeta itemMetadata2 = ItemSpriteManager.GetItemMetadata(num6);
				if (itemMetadata2.Projectiles != null && itemMetadata2.Projectiles.Count > 0 && itemMetadata2.Projectiles.Exists((ItemSpriteManager.ProjectileInfo p) => p.MaxDamage > 0))
				{
					double exaltMult = ((Player != null && Player.ExaltationBonusDamage > 0) ? ((double)Player.ExaltationBonusDamage / 1000.0) : 1.0);
					double attMult = 0.5 + (double)localPlayerInfo.Attack / 50.0;
					double dexFactor = 1.5 + 6.5 * ((double)localPlayerInfo.Dexterity / 75.0);
					double totalWeaponDps = 0.0;
					double totalTrueSetDps = 0.0;
					int totalBullets = 0;
					foreach (ItemSpriteManager.ProjectileInfo proj in itemMetadata2.Projectiles)
					{
						if (proj.MinDamage > 0 || proj.MaxDamage > 0)
						{
							double avgDmg = (double)(proj.MinDamage + proj.MaxDamage) / 2.0;
							double aps = (double)proj.RateOfFire * dexFactor;
							double bWeaponDps = avgDmg * aps * (double)proj.NumProjectiles;
							double bTrueDps = avgDmg * exaltMult * attMult * aps * (double)proj.NumProjectiles;
							totalWeaponDps += bWeaponDps;
							totalTrueSetDps += bTrueDps;
							totalBullets += proj.NumProjectiles;
						}
					}
					localPlayerInfo.WeaponDps = Math.Round(totalWeaponDps, 1);
					localPlayerInfo.TrueSetDps = Math.Round(totalTrueSetDps, 1);
					if (itemMetadata2.Projectiles.Count == 1)
					{
						ItemSpriteManager.ProjectileInfo projectileInfo = itemMetadata2.Projectiles[0];
						localPlayerInfo.WeaponBulletInfo = $"{projectileInfo.NumProjectiles}x {projectileInfo.MinDamage}-{projectileInfo.MaxDamage}  ROF: {(int)(projectileInfo.RateOfFire * 100f)}%";
					}
					else
					{
						float rateOfFire = itemMetadata2.Projectiles[0].RateOfFire;
						localPlayerInfo.WeaponBulletInfo = $"{totalBullets} Bullets  ROF: {(int)(rateOfFire * 100f)}%";
					}
				}
				else if (itemMetadata2.MinDamage > 0 && itemMetadata2.MaxDamage > 0)
				{
					double num7 = (double)(itemMetadata2.MinDamage + itemMetadata2.MaxDamage) / 2.0;
					double num8 = 0.5 + (double)localPlayerInfo.Attack / 50.0;
					double num9 = (double)itemMetadata2.RateOfFire * (1.5 + 6.5 * ((double)localPlayerInfo.Dexterity / 75.0));
					localPlayerInfo.WeaponDps = Math.Round(num7 * num9 * (double)itemMetadata2.NumProjectiles, 1);
					localPlayerInfo.TrueSetDps = Math.Round(num7 * num8 * num9 * (double)itemMetadata2.NumProjectiles, 1);
					localPlayerInfo.WeaponBulletInfo = $"{itemMetadata2.NumProjectiles}x {itemMetadata2.MinDamage}-{itemMetadata2.MaxDamage}  ROF: {(int)(itemMetadata2.RateOfFire * 100f)}%";
				}
				else
				{
					localPlayerInfo.WeaponBulletInfo = itemMetadata2.Name;
				}
			}
		}
		List<LootDropEntry> lootDrops;
		lock (sessionLootDrops)
		{
			lootDrops = sessionLootDrops.ToList();
		}
		List<LootBagDrop> lootBags;
		lock (sessionLootBags)
		{
			lootBags = sessionLootBags.ToList();
		}
		List<AbilityLogEntry> abilityLog;
		lock (abilityLogLock)
		{
			abilityLog = sessionAbilityLog.ToList();
		}
		List<PartyMemberEntry> partyMembers = new List<PartyMemberEntry>();
		lock (partyLock)
		{
			foreach (var kvp in sessionPartyMembers)
			{
				PartyTrackerEntry p = kvp.Value;
				PlayerParseEntry inDungeonPlayer = null;
				lock (playerRoster)
				{
					inDungeonPlayer = playerRoster.Values.FirstOrDefault(r => string.Equals(r.Name, p.Name, StringComparison.OrdinalIgnoreCase));
				}

				bool isLocal = client != null && client.Player != null && string.Equals(client.Player.PlayerName, p.Name, StringComparison.OrdinalIgnoreCase);

				var pEntry = new PartyMemberEntry
				{
					Name = p.Name,
					PlayerId = p.PlayerId,
					ObjectId = p.ObjectId,
					ClassName = p.ClassName ?? "Unknown"
				};

				if (isLocal)
				{
					pEntry.IsInDungeon = true;
					pEntry.IsLocalPlayer = true;
					pEntry.ClassName = GetClassName(client.Player.ObjectType);
					pEntry.CurrentHp = client.Player.Hp;
					pEntry.MaxHp = client.Player.MaxHp;
					pEntry.Status = "YOU";
					pEntry.MaxedCount = localPlayerInfo.MaxedCount;
				}
				else if (inDungeonPlayer != null)
				{
					pEntry.IsInDungeon = true;
					pEntry.ClassName = inDungeonPlayer.ClassName;
					pEntry.CurrentHp = inDungeonPlayer.CurrentHp;
					pEntry.MaxHp = inDungeonPlayer.MaxHp;
					pEntry.MaxedCount = inDungeonPlayer.MaxedCount;
					pEntry.Status = "IN DUNGEON";
				}
				else
				{
					pEntry.IsInDungeon = false;
					PlayerDropRecord dropRec = null;
					lock (droppedPlayers)
					{
						dropRec = droppedPlayers.Values.FirstOrDefault(d => string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase));
					}
					if (dropRec != null)
					{
						if (dropRec.IsDead)
						{
							pEntry.Status = "DIED";
							pEntry.CurrentHp = 0;
							pEntry.MaxHp = dropRec.MaxHp;
						}
						else
						{
							pEntry.Status = $"NEXUSED ({dropRec.LastHpPercent}%)";
							pEntry.CurrentHp = dropRec.LastHp;
							pEntry.MaxHp = dropRec.MaxHp;
						}
					}
					else
					{
						pEntry.Status = "NOT IN DUNGEON";
					}
				}

				partyMembers.Add(pEntry);
			}
		}
		return new DpsSnapshot
		{
			DamageEstimateNote = hasIncompleteEventDamage ? "Event bonuses missing for some shots; reopen event panels." : null,
			DungeonName = currentDungeonName,
			DungeonStartTime = dungeonStartTime,
			ElapsedDungeonSeconds = num,
			KillsCount = totalKillsCount,
			TotalDungeonDamage = combinedDungeonCombat.TotalDamage,
			TotalGroupDps = totalGroupDps,
			ActiveEnemy = activeEnemy,
			DefeatedEnemies = list,
			AllCombinedEnemy = allCombinedEnemy,
			Players = players,
			PartyMembers = partyMembers,
			LocalPlayer = localPlayerInfo,
			LootDrops = lootDrops,
			LootBags = lootBags,
			AbilityLog = abilityLog,
			Timestamp = DateTime.Now
		};
	}

	public void HandleCommand(GenericFailurePacket packet)
	{
		if (packet == null)
		{
			return;
		}
		if (packet.IsCommand("dps", out var commandArguments))
		{
			packet.Send = false;
			EnemyCombatSnapshot currentDisplayEnemy = CompileCurrentSnapshot().GetCurrentDisplayEnemy();
			List<PlayerDamageEntry> list = currentDisplayEnemy.Damagers.Take(5).ToList();
			if (list.Count == 0)
			{
				client.SendNotification("No combat damage recorded yet.");
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine($"[DPS - {currentDisplayEnemy.Name} ({currentDisplayEnemy.TotalDamage:N0} dmg)]");
			foreach (PlayerDamageEntry item in list)
			{
				stringBuilder.AppendLine($"#{item.Rank} {item.Name}: {item.Damage:N0} ({item.Dps:N0}/s, {item.Percentage:F1}%)");
			}
			client.SendNotification(stringBuilder.ToString().TrimEnd());
		}
		else if (packet.IsCommand("parse", out commandArguments))
		{
			packet.Send = false;
			List<PlayerParseEntry> list2;
			lock (playerRoster)
			{
				list2 = playerRoster.Values
					.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
					.Select(g => g.Last())
					.ToList();
			}
			int count = list2.Count;
			int num = list2.Count((PlayerParseEntry p) => p.MaxedCount == 8);
			IOrderedEnumerable<string> values = from p in list2
				group p by p.ClassName into g
				select $"{g.Key}: {g.Count()}" into s
				orderby s descending
				select s;
			string usageMessage = string.Format("[DUNGEON PARSE - {0}]\nPlayers: {1} (8/8s: {2})\nClasses: {3}", currentDungeonName, count, num, string.Join(", ", values));
			client.SendNotification(usageMessage);
		}
		else if (packet.IsCommand("resetdps", out commandArguments))
		{
			packet.Send = false;
			ResetDungeonEncounter();
			client.SendNotification("DPS Meter reset.");
			PushSnapshotToOverlay();
		}
		else if (packet.IsCommand("party", out commandArguments) || packet.IsCommand("pclear", out commandArguments))
		{
			packet.Send = false;
			if (packet.IsCommand("pclear", out _) || (commandArguments != null && commandArguments.Length > 0 && commandArguments[0].Equals("clear", StringComparison.OrdinalIgnoreCase)))
			{
				ClearParty();
				client.SendNotification("Party tracker cleared.");
				PushSnapshotToOverlay();
				return;
			}
			DpsSnapshot snap = CompileCurrentSnapshot();
			int total = snap.PartyMembers.Count;
			int inDungeon = snap.PartyMembers.Count(p => p.IsInDungeon);
			int missing = total - inDungeon;
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"[PARTY TRACKER - {inDungeon}/{total} in Dungeon]");
			if (missing > 0)
			{
				var missingNames = snap.PartyMembers.Where(p => !p.IsInDungeon).Select(p => $"{p.Name} ({p.Status})");
				sb.AppendLine($"Missing ({missing}): {string.Join(", ", missingNames)}");
			}
			else if (total > 0)
			{
				sb.AppendLine("All party members are present in the dungeon!");
			}
			else
			{
				sb.AppendLine("No party members tracked.");
			}
			client.SendNotification(sb.ToString().TrimEnd());
		}
		else if (packet.IsCommand("padd", out commandArguments))
		{
			packet.Send = false;
			if (commandArguments != null && commandArguments.Length > 0)
			{
				string addName = string.Join(" ", commandArguments).Trim();
				AddPartyMember(addName);
				client.SendNotification($"Added '{addName}' to party tracker.");
				PushSnapshotToOverlay();
			}
		}
		else if (packet.IsCommand("prm", out commandArguments))
		{
			packet.Send = false;
			if (commandArguments != null && commandArguments.Length > 0)
			{
				string rmName = string.Join(" ", commandArguments).Trim();
				RemovePartyMember(rmName);
				client.SendNotification($"Removed '{rmName}' from party tracker.");
				PushSnapshotToOverlay();
			}
		}
	}

	private static string GetClassName(ushort classId)
	{
		if (GameData.Objects != null)
		{
			ObjectStructure objectStructure = GameData.Objects.GetById(classId);
			if (objectStructure != null)
			{
				return objectStructure.Name ?? objectStructure.ObjectClass ?? "Player";
			}
		}
		return "Player";
	}

	private static string GetItemTier(int itemId)
	{
		if (itemId <= 0)
		{
			return "-";
		}
		if (GameData.Items != null)
		{
			ItemStructure itemStructure = GameData.Items.GetById((ushort)itemId);
			if (itemStructure != null)
			{
				if (itemStructure.Tier == Tiers.UT)
				{
					return "UT";
				}
				return itemStructure.Tier.ToString();
			}
		}
		return "-";
	}

	private static (string display, int count) GetMaxedStats(MapObject p)
	{
		if (p == null)
		{
			return (display: "0/8", count: 0);
		}
		if (ClassStats.Map == null || !ClassStats.Map.TryGetValue(p.ObjectType, out var value))
		{
			return (display: "0/8", count: 0);
		}
		int num = 0;
		int num2 = p.MaxHp - p.HealthBonus;
		int num3 = p.MaxMp - p.ManaBonus;
		int num4 = p.Attack - p.AttackBonus;
		int num5 = p.Defense - p.DefenseBonus;
		int num6 = p.Speed - p.SpeedBonus;
		int num7 = p.Dexterity - p.DexterityBonus;
		int num8 = p.Vitality - p.VitalityBonus;
		int num9 = p.Wisdom - p.WisdomBonus;
		if (num2 >= value.LifeMax)
		{
			num++;
		}
		if (num3 >= value.ManaMax)
		{
			num++;
		}
		if (num4 >= value.AttackMax)
		{
			num++;
		}
		if (num5 >= value.DefenseMax)
		{
			num++;
		}
		if (num6 >= value.SpeedMax)
		{
			num++;
		}
		if (num7 >= value.DexterityMax)
		{
			num++;
		}
		if (num8 >= value.VitalityMax)
		{
			num++;
		}
		if (num9 >= value.WisdomMax)
		{
			num++;
		}
		return (display: $"{num}/8", count: num);
	}

	public bool HasBossCombat(string nameContains, out int entityId, out int totalDamage)
	{
		lock (allEnemies)
		{
			foreach (var enemy in allEnemies.Values)
			{
				if (!enemy.IsDead && enemy.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0 && enemy.TotalDamage > 0)
				{
					entityId = enemy.EntityId;
					totalDamage = (int)Math.Min(int.MaxValue, enemy.TotalDamage);
					return true;
				}
			}
		}
		entityId = -1;
		totalDamage = 0;
		return false;
	}

	public void Dispose()
	{
		ArchiveCurrentDungeon();
		ResetDungeonEncounter();
		if (activeTrackerInstance == this)
		{
			activeTrackerInstance = null;
		}
	}
}
