using System;
using System.Collections.Generic;
using System.Text;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

public class PhaseInfo
{
	public int PhaseNumber { get; set; } // 1 to 11
	public int BossIndex { get; set; }   // 1, 2, or 3
	public string PhaseName { get; set; }
	public bool IsLanternPhase { get; set; }
	public bool IsFinale { get; set; }
	public int FlamesEarned { get; set; } // 0 to 8
	public bool IsCompleted { get; set; }
}

internal class MoonlightVillageMod
{
	public static MoonlightVillageMod ActiveInstance { get; private set; }

	// Dancer and Boss Object IDs from game data
	public const int SageGenjiId = 20450;       // 0x4fe2
	public const int DancerMikoId = 20451;      // 0x4fe3
	public const int DrummerKaguyaId = 20452;   // 0x4fe4
	public const int KitsuneUmiId = 20493;      // 0x500d
	public const int UmiDeuxId = 20601;         // 0x5079
	public const int UmiCompleteId = 49339;     // 0xc0bb: invisible reward prop, not a combat boss

	// Real reward flame counter & lantern mechanics from packet analysis
	public const int MvTotalCounterId = 20518;   // 0x5026 ('MV Total Counter' - real reward flame object)
	public const int MvLanternSystemId = 20454;  // 0x4fe6 ('MV Lantern System')

	private readonly Client client;
	private readonly PhaseInfo[] villagePhases;
	private readonly PhaseInfo[] umiPhases;
	private readonly HashSet<int> currentPhaseWispIds = new HashSet<int>();
	private readonly HashSet<int> allSeenFlameEntityIds = new HashSet<int>();
	private readonly HashSet<string> announcedDancerEncounters = new HashSet<string>();

	public bool IsUmiMode { get; private set; } = false;
	private PhaseInfo[] CurrentPhases => IsUmiMode ? umiPhases : villagePhases;
	public int MaxPhases => IsUmiMode ? 7 : 11;
	public int MaxTotalFlames => IsUmiMode ? 56 : 88;

	private int currentPhaseIndex = 0; // 0 to 10 (or 0 to 6 in Umi mode)
	private int currentBossIndex = 1;  // 1, 2, or 3
	private bool isCombatActive = false;
	private int activeBossEntityId = -1;
	private int activeLanternEntityId = -1;
	private bool isLanternSystemAlive = false;
	private DateTime lanternDespawnTime = DateTime.MinValue;
	private bool lanternPendingCompletion = false;
	private DateTime currentPhaseStartTime = DateTime.UtcNow;
	private DateTime lastWispSpawnTime = DateTime.MinValue;
	private bool isWispBurstActive = false;
	private int lastCompletedPhaseIndex = -1;
	private int lastCompletedPhaseFlames = 0;
	private DateTime phaseCompleteHoldUntil = DateTime.MinValue;

	public bool IsInMoonlightVillage { get; private set; }
	public string CurrentDancer { get; private set; } = "None";

	public string Boss1Dancer { get; private set; } = "Dancer 1";
	public string Boss2Dancer { get; private set; } = "Dancer 2";
	public string Boss3Dancer { get; private set; } = "Dancer 3";

	public int DancersCompleted { get; private set; }

	// Reaching phase 11 is not completion: require its reward or the closing dialogue.
	private bool CanAutoStartKitsune => IsInMoonlightVillage &&
		DancersCompleted >= 3 && villagePhases[10].IsCompleted;

	public static bool EnableOverlay { get; set; } = false;
	public static bool EnableChatNotifications { get; set; } = false;
	public static bool EnableFloatingNotifications { get; set; } = false;

	public int CurrentPhaseNumber => currentPhaseIndex + 1;
	public string CurrentPhaseName => (currentPhaseIndex >= 0 && currentPhaseIndex < CurrentPhases.Length) ? CurrentPhases[currentPhaseIndex].PhaseName : "Complete";
	public int CurrentPhaseFlames => Math.Min(8, currentPhaseWispIds.Count);
	public bool IsLanternActive => isLanternSystemAlive;

	public int Boss1Flames => villagePhases[0].FlamesEarned + villagePhases[1].FlamesEarned;
	public int Boss2Flames => villagePhases[2].FlamesEarned + villagePhases[3].FlamesEarned + villagePhases[4].FlamesEarned + villagePhases[5].FlamesEarned;
	public int Boss3Flames => villagePhases[6].FlamesEarned + villagePhases[7].FlamesEarned + villagePhases[8].FlamesEarned + villagePhases[9].FlamesEarned + villagePhases[10].FlamesEarned;

	public int UmiAct1Flames => umiPhases[0].FlamesEarned + umiPhases[1].FlamesEarned;
	public int UmiAct2Flames => umiPhases[2].FlamesEarned + umiPhases[3].FlamesEarned;
	public int UmiAct3Flames => umiPhases[4].FlamesEarned + umiPhases[5].FlamesEarned + umiPhases[6].FlamesEarned;
	public int UmiFlames => UmiAct1Flames + UmiAct2Flames + UmiAct3Flames;

	public int MikoFlames { get; private set; }
	public int GenjiFlames { get; private set; }
	public int KaguyaFlames { get; private set; }

	public int TotalFlames
	{
		get
		{
			PhaseInfo[] active = CurrentPhases;
			int sum = 0;
			for (int i = 0; i < active.Length; i++)
			{
				if (active[i].IsCompleted)
				{
					sum += active[i].FlamesEarned;
				}
			}
			if (isWispBurstActive)
			{
				sum += Math.Min(8, currentPhaseWispIds.Count);
			}
			return Math.Min(MaxTotalFlames, sum);
		}
	}

	public MoonlightVillageMod(Client client)
	{
		this.client = client;
		ActiveInstance = this;
		villagePhases = CreateDefaultPhases();
		umiPhases = CreateUmiPhases();
	}

	private static PhaseInfo[] CreateDefaultPhases()
	{
		return new PhaseInfo[]
		{
			// 1st Boss (2 phases, 16 max flames)
			new PhaseInfo { PhaseNumber = 1, BossIndex = 1, PhaseName = "DPS Phase", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 2, BossIndex = 1, PhaseName = "Lantern Phase", IsLanternPhase = true },

			// 2nd Boss (4 phases, 32 max flames)
			new PhaseInfo { PhaseNumber = 3, BossIndex = 2, PhaseName = "DPS Phase 1", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 4, BossIndex = 2, PhaseName = "Lantern Phase 1", IsLanternPhase = true },
			new PhaseInfo { PhaseNumber = 5, BossIndex = 2, PhaseName = "DPS Phase 2", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 6, BossIndex = 2, PhaseName = "Lantern Phase 2", IsLanternPhase = true },

			// 3rd Boss (5 phases, 40 max flames)
			new PhaseInfo { PhaseNumber = 7, BossIndex = 3, PhaseName = "DPS Phase 1", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 8, BossIndex = 3, PhaseName = "Lantern Phase 1", IsLanternPhase = true },
			new PhaseInfo { PhaseNumber = 9, BossIndex = 3, PhaseName = "DPS Phase 2", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 10, BossIndex = 3, PhaseName = "Lantern Phase 2", IsLanternPhase = true },
			new PhaseInfo { PhaseNumber = 11, BossIndex = 3, PhaseName = "Sicken Lantern Finale", IsLanternPhase = true, IsFinale = true }
		};
	}

	private static PhaseInfo[] CreateUmiPhases()
	{
		return new PhaseInfo[]
		{
			// Act 1 (2 phases, 16 max flames)
			new PhaseInfo { PhaseNumber = 1, BossIndex = 1, PhaseName = "DPS Phase 1", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 2, BossIndex = 1, PhaseName = "Danmaku 1 (Procession)", IsLanternPhase = true },

			// Act 2 (2 phases, 16 max flames)
			new PhaseInfo { PhaseNumber = 3, BossIndex = 2, PhaseName = "DPS Phase 2", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 4, BossIndex = 2, PhaseName = "Danmaku 2 (Rage)", IsLanternPhase = true },

			// Act 3 & Finale (3 phases, 24 max flames)
			new PhaseInfo { PhaseNumber = 5, BossIndex = 3, PhaseName = "DPS Phase 3", IsLanternPhase = false },
			new PhaseInfo { PhaseNumber = 6, BossIndex = 3, PhaseName = "Danmaku 3 (Remembrance)", IsLanternPhase = true },
			new PhaseInfo { PhaseNumber = 7, BossIndex = 3, PhaseName = "Finale (Princess & Fox)", IsLanternPhase = true, IsFinale = true }
		};
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		string mapName = packet.MapName ?? string.Empty;
		bool enteredMV = mapName.IndexOf("Moonlight Village", StringComparison.OrdinalIgnoreCase) >= 0;

		if (enteredMV)
		{
			IsInMoonlightVillage = true;
			ResetCounters();
			MoonlightVillagePacketLogger.StartSession(mapName);
			SendChatNotification($"🌸 Moonlight Village Sniffer Active! Logging to: {MoonlightVillagePacketLogger.GetLogPath()}");
			UpdateOverlay();
		}
		else if (IsInMoonlightVillage)
		{
			IsInMoonlightVillage = false;
			isCombatActive = false;
			activeBossEntityId = -1;
			MoonlightVillagePacketLogger.EndSession();
			UpdateOverlay();
		}
	}

	public void OnUpdate(UpdatePacket packet)
	{
		if (!IsInMoonlightVillage)
		{
			return;
		}

		// Sniffer: log all new object spawns and drops
		if (packet.NewObjects != null)
		{
			foreach (ObjectData obj in packet.NewObjects)
			{
				MoonlightVillagePacketLogger.LogObjectSpawn(obj.ObjectType, obj.Stats.ObjectId, obj.Stats.Position);
			}
		}
		if (packet.Drops != null)
		{
			foreach (int dropId in packet.Drops)
			{
				MoonlightVillagePacketLogger.LogObjectDespawn(dropId);
			}
		}

		bool stateChanged = false;

		// 1. Detect active dancer in arena from client.Enemies (must be vulnerable and not an already completed boss)
		if (client.Enemies != null)
		{
			foreach (var kvp in client.Enemies)
			{
				MapObject mo = kvp.Value;
				if (mo == null) continue;
				if (mo.ObjectType == DancerMikoId || mo.ObjectType == SageGenjiId || mo.ObjectType == DrummerKaguyaId ||
				    mo.ObjectType == KitsuneUmiId || mo.ObjectType == UmiDeuxId)
				{
					bool isInvuln = (mo.Effects & 0x01800000) != 0;
					if (!isInvuln)
					{
						if (mo.ObjectType == KitsuneUmiId || mo.ObjectType == UmiDeuxId)
						{
							// The Kitsune object also exists during the village introduction.
							if (!IsUmiMode && !CanAutoStartKitsune) continue;
							if (!IsUmiMode)
							{
								ActivateUmiMode();
							}
							isCombatActive = true;
							activeBossEntityId = mo.ObjectId;
							CurrentDancer = "Umi";
							break;
						}
						else
						{
							if (IsUmiMode || DancersCompleted >= 3) continue;
							string dancer = mo.ObjectType == DancerMikoId ? "Miko" : mo.ObjectType == SageGenjiId ? "Genji" : "Kaguya";
							if ((DancersCompleted >= 1 && dancer == Boss1Dancer) ||
							    (DancersCompleted >= 2 && dancer == Boss2Dancer)) continue;
							isCombatActive = true;
							activeBossEntityId = mo.ObjectId;
							RegisterDancer(dancer);
							break;
						}
					}
				}
			}
		}

		// 2. Process newly spawned objects
		if (packet.NewObjects != null)
		{
			foreach (ObjectData obj in packet.NewObjects)
			{
				int typeId = obj.ObjectType;
				int entityId = obj.Stats.ObjectId;
				WorldPosData pos = obj.Stats.Position;

				// REAL FLAME REWARD: 20518 (0x5026 'MV Total Counter')
				if (typeId == MvTotalCounterId)
				{
					double dx = pos.X - 122.5;
					double dy = pos.Y - 113.5;
					double distSq = dx * dx + dy * dy;

					// Flames spawn directly in arena center (122.5, 113.5)
					if (distSq <= 25.0 && allSeenFlameEntityIds.Add(entityId))
					{
						if (currentPhaseWispIds.Add(entityId))
						{
							isWispBurstActive = true;
							lastWispSpawnTime = DateTime.UtcNow;
							stateChanged = true;
						}
					}
				}
				else if (typeId == MvLanternSystemId)
				{
					isLanternSystemAlive = true;
					activeLanternEntityId = entityId;

					// If we are currently on a DPS phase, lantern spawn signals end of DPS phase and start of Lantern phase!
					PhaseInfo[] activePhases = CurrentPhases;
					if (currentPhaseIndex >= 0 && currentPhaseIndex < activePhases.Length && !activePhases[currentPhaseIndex].IsLanternPhase)
					{
						if (isWispBurstActive)
						{
							FinishWispBurst();
						}
						else if (!activePhases[currentPhaseIndex].IsCompleted)
						{
							CommitCurrentPhase(0);
						}
						stateChanged = true;
					}
				}
			}
		}

		// 3. Process despawns (specifically MV Lantern System despawn)
		if (packet.Drops != null && activeLanternEntityId != -1)
		{
			foreach (int dropId in packet.Drops)
			{
				if (dropId == activeLanternEntityId)
				{
					isLanternSystemAlive = false;
					activeLanternEntityId = -1;
					lanternDespawnTime = DateTime.UtcNow;
					lanternPendingCompletion = true;
					break;
				}
			}
		}

		// 4. Check if flame burst or 0-flame lantern phase needs finalizing
		if (isWispBurstActive && (DateTime.UtcNow - lastWispSpawnTime).TotalSeconds >= 1.2)
		{
			FinishWispBurst();
			lanternPendingCompletion = false;
			stateChanged = true;
		}
		else if (lanternPendingCompletion && !isWispBurstActive && (DateTime.UtcNow - lanternDespawnTime).TotalSeconds >= 1.5)
		{
			lanternPendingCompletion = false;
			PhaseInfo[] activePhases = CurrentPhases;
			if (currentPhaseIndex >= 0 && currentPhaseIndex < activePhases.Length && activePhases[currentPhaseIndex].IsLanternPhase && !activePhases[currentPhaseIndex].IsCompleted)
			{
				CommitCurrentPhase(0);
				stateChanged = true;
			}
		}

		if (stateChanged)
		{
			UpdateOverlay();
		}
	}

	public void OnNewTick(NewTickPacket packet)
	{
		if (!IsInMoonlightVillage)
		{
			return;
		}

		if (packet.Statuses != null)
		{
			foreach (ObjectStatsData stats in packet.Statuses)
			{
				MoonlightVillagePacketLogger.LogTickStatus(stats);
			}
		}

		// Cancel hold early if new flames start spawning or lanterns activate
		if (isWispBurstActive || isLanternSystemAlive)
		{
			phaseCompleteHoldUntil = DateTime.MinValue;
		}

		// Check if flame burst completed (1.2 seconds of silence after flames spawn)
		if (isWispBurstActive && (DateTime.UtcNow - lastWispSpawnTime).TotalSeconds >= 1.2)
		{
			FinishWispBurst();
			lanternPendingCompletion = false;
			UpdateOverlay();
		}
		else if (lanternPendingCompletion && !isWispBurstActive && (DateTime.UtcNow - lanternDespawnTime).TotalSeconds >= 1.5)
		{
			lanternPendingCompletion = false;
			PhaseInfo[] activePhases = CurrentPhases;
			if (currentPhaseIndex >= 0 && currentPhaseIndex < activePhases.Length && activePhases[currentPhaseIndex].IsLanternPhase && !activePhases[currentPhaseIndex].IsCompleted)
			{
				CommitCurrentPhase(0);
				UpdateOverlay();
			}
		}
	}

	public void OnText(TextPacket packet)
	{
		if (!IsInMoonlightVillage)
		{
			return;
		}

		MoonlightVillagePacketLogger.LogText(packet);

		string sender = packet.Name ?? string.Empty;
		string text = packet.Text ?? string.Empty;

		ParseBattleDialogue(sender, text);
	}

	private void ParseBattleDialogue(string sender, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		// Normalize smart quotes and special characters for bulletproof matching
		string cleanText = text.Replace("’", "'").Replace("‘", "'").Replace("“", "\"").Replace("”", "\"");
		// Logs identify NPC speakers with '#'. Player chat must not drive encounter state.
		bool isVillageDancer = sender == "#Sage Genji" || sender == "#Drummer Kaguya" || sender == "#Dancer Miko";
		bool isKitsune = sender == "#Kitsune Umi";
		if (!isVillageDancer && !isKitsune) return;

		if (isKitsune)
		{
			if (!IsUmiMode && !CanAutoStartKitsune) return;
			if (!IsUmiMode) ActivateUmiMode();
			isCombatActive = true;
			CurrentDancer = "Umi";
			UpdateOverlay();
			return;
		}
		// Late village dialogue must not modify the Kitsune encounter.
		if (IsUmiMode) return;

		// 1. Full dungeon completion line
		if (cleanText.IndexOf("This concludes the Moonlight Dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("This fully concludes the Moonlight Festival", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			if (DancersCompleted >= 3) return;
			if (isWispBurstActive) FinishWispBurst();
			for (int i = 0; i < villagePhases.Length; i++) villagePhases[i].IsCompleted = true;
			currentPhaseIndex = villagePhases.Length - 1;
			CheckBossCompletion(10);
			UpdateOverlay();
			return;
		}

		// 2. Ignore introductory chanting lines, finale chants, and closing lines
		if (cleanText.IndexOf("timeless ritual", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("great beat", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("patron god", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("patron Goddess", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("Witness our", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("Moonlight Dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("Let us begin the final dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("Thank you all for coming", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("We hope to perform", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return;
		}

		// 4. RealmEye Second Dancer Dialogue: mentions prior defeated boss, resolving the entire 3-boss sequence!
		// Genji as Second Dancer:
		if (cleanText.IndexOf("Miko's performance? I shall further aim", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("find Miko's performance", StringComparison.OrdinalIgnoreCase) >= 0 && (sender.IndexOf("Genji", StringComparison.OrdinalIgnoreCase) >= 0 || cleanText.IndexOf("enhance the festival", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			ApplyBossOrder("Miko", "Genji", "Kaguya");
			return;
		}
		if (cleanText.IndexOf("captivated by Kaguya's performance? I shall further aim", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("captivated by Kaguya", StringComparison.OrdinalIgnoreCase) >= 0 && (sender.IndexOf("Genji", StringComparison.OrdinalIgnoreCase) >= 0 || cleanText.IndexOf("enhance the festival", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			ApplyBossOrder("Kaguya", "Genji", "Miko");
			return;
		}

		// Kaguya as Second Dancer:
		if (cleanText.IndexOf("find Miko's performance? I shall get you up", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("moved by Miko's dance then you'll love mine", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			ApplyBossOrder("Miko", "Kaguya", "Genji");
			return;
		}
		if (cleanText.IndexOf("enjoy Genji's performance? I shall get you up", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("moved by Genji's dance then you will be in awe", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			ApplyBossOrder("Genji", "Kaguya", "Miko");
			return;
		}

		// Miko as Second Dancer:
		if (cleanText.IndexOf("Kaguya has been honing her dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("captivated by Kaguya's drum performance", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			ApplyBossOrder("Kaguya", "Miko", "Genji");
			return;
		}
		if (cleanText.IndexOf("Genji has been honing his dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("enjoy Genji's performance? I hope mine will prove adequate", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			ApplyBossOrder("Genji", "Miko", "Kaguya");
			return;
		}

		// 5. RealmEye First Dancer Specific Opening Lines:
		if (cleanText.IndexOf("brave audience willing to perform alongside us", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			SetBossDancer(1, "Genji");
			return;
		}
		if (cleanText.IndexOf("thundering open act", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("thundering opening act", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("Let me start! I'll show these guests", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			SetBossDancer(1, "Kaguya");
			return;
		}
		if (cleanText.IndexOf("display of natural elegance shall entertain them", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("weary of the troubles behind them", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			SetBossDancer(1, "Miko");
			return;
		}

		// 6. RealmEye Third Dancer Specific Opening Lines:
		if (cleanText.IndexOf("other dancers have paved the way to this final part", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			SetBossDancer(3, "Genji");
			return;
		}
		if (cleanText.IndexOf("Watching the others dance before me has revitalized my soul", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			SetBossDancer(3, "Kaguya");
			return;
		}
		if (cleanText.IndexOf("honored to be the final dancer", StringComparison.OrdinalIgnoreCase) >= 0 ||
		    cleanText.IndexOf("honoured to be the final dancer", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			SetBossDancer(3, "Miko");
			return;
		}

		// 7. General Dancer Speaker Identification (packet sender name or unique phrases)
		string speaker = null;
		if (sender.IndexOf("Kaguya", StringComparison.OrdinalIgnoreCase) >= 0) speaker = "Kaguya";
		else if (sender.IndexOf("Genji", StringComparison.OrdinalIgnoreCase) >= 0) speaker = "Genji";
		else if (sender.IndexOf("Miko", StringComparison.OrdinalIgnoreCase) >= 0) speaker = "Miko";

		// Fallback keywords if sender was not present
		if (string.IsNullOrEmpty(speaker))
		{
			if (cleanText.IndexOf("power of thunder", StringComparison.OrdinalIgnoreCase) >= 0 ||
			    cleanText.IndexOf("storm clouds deepen", StringComparison.OrdinalIgnoreCase) >= 0 ||
			    cleanText.IndexOf("invigorated your spirit", StringComparison.OrdinalIgnoreCase) >= 0 ||
			    cleanText.IndexOf("Drummer Kaguya", StringComparison.OrdinalIgnoreCase) >= 0) speaker = "Kaguya";
			else if (cleanText.IndexOf("tide leads us", StringComparison.OrdinalIgnoreCase) >= 0 ||
			         cleanText.IndexOf("refreshed your spirit", StringComparison.OrdinalIgnoreCase) >= 0 ||
			         cleanText.IndexOf("honour you with my own fervour", StringComparison.OrdinalIgnoreCase) >= 0 ||
			         cleanText.IndexOf("Sage Genji", StringComparison.OrdinalIgnoreCase) >= 0) speaker = "Genji";
			else if (cleanText.IndexOf("inspired by my dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
			         cleanText.IndexOf("praise for making it this far", StringComparison.OrdinalIgnoreCase) >= 0 ||
			         cleanText.IndexOf("Dancer Miko", StringComparison.OrdinalIgnoreCase) >= 0) speaker = "Miko";
		}

		if (!string.IsNullOrEmpty(speaker))
		{
			isCombatActive = true;
			RegisterDancer(speaker);
		}
	}

	public void OnNotification(NotificationPacket packet)
	{
		if (!IsInMoonlightVillage)
		{
			return;
		}
		MoonlightVillagePacketLogger.LogNotification(packet);

		// Suppress spammy "{"k":"s.immune"}" floating text near boss when flames drop
		if (packet.Message != null && packet.Message.IndexOf("s.immune", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			packet.Send = false;
		}
	}

	public void OnShowEffect(ShowEffectPacket packet)
	{
		if (!IsInMoonlightVillage)
		{
			return;
		}
		MoonlightVillagePacketLogger.LogShowEffect(packet);
	}

	public void HandleCommand(GenericFailurePacket packet)
	{
		if (packet.IsCommand("umi", out string[] args) || packet.IsCommand("mvumi", out args))
		{
			packet.Send = false;
			if (args.Length == 1 && args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
			{
				ActivateUmiMode();
			}
			else if (args.Length == 1 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
			{
				if (IsUmiMode) ToggleUmiMode();
			}
			else
			{
				ToggleUmiMode();
			}
			return;
		}

		if (packet.IsCommand("mv", out args) || packet.IsCommand("flames", out args))
		{
			packet.Send = false;
			PrintStatus();
			return;
		}

		if (packet.IsCommand("mvfloat", out args) || packet.IsCommand("mvtext", out args))
		{
			packet.Send = false;
			EnableFloatingNotifications = !EnableFloatingNotifications;
			SendChatNotification($"[MV Tracker] Floating screen notifications: {(EnableFloatingNotifications ? "ENABLED" : "DISABLED")}");
			return;
		}

		if (packet.IsCommand("mvsniff", out args) || packet.IsCommand("mvlog", out args))
		{
			packet.Send = false;
			string logPath = MoonlightVillagePacketLogger.GetLogPath();
			SendChatNotification($"[MV Sniffer] Active: {MoonlightVillagePacketLogger.IsActive} | Log File: {logPath}");
			return;
		}

		if (packet.IsCommand("mvreset", out args))
		{
			packet.Send = false;
			bool resetAll = args.Length > 0 && args[0].Equals("all", StringComparison.OrdinalIgnoreCase);
			ResetCounters(resetAll || !IsUmiMode);
			string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";
			SendChatNotification($"{tag} Counters reset.");
			UpdateOverlay();
			return;
		}

		if (packet.IsCommand("mvhud", out args))
		{
			packet.Send = false;
			DpsOverlayManager.SwitchToMoonlightVillageTab();
			SendChatNotification("[MV Tracker] Switched to Moonlight Village tab in overlay HUD.");
			return;
		}

		if (packet.IsCommand("mvnext", out args))
		{
			packet.Send = false;
			NextPhase();
			return;
		}

		if (packet.IsCommand("mvprev", out args))
		{
			packet.Send = false;
			PreviousPhase();
			return;
		}

		if (packet.IsCommand("mvadd", out args) && args.Length == 1 && int.TryParse(args[0], out int addFlames))
		{
			packet.Send = false;
			AddFlameToCurrentPhase(addFlames);
			return;
		}

		if (packet.IsCommand("mvset", out args))
		{
			packet.Send = false;
			PhaseInfo[] activePhases = CurrentPhases;
			int maxP = MaxPhases;
			int maxF = MaxTotalFlames;
			string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";

			if (args.Length == 1 && int.TryParse(args[0], out int flames))
			{
				flames = Math.Max(0, Math.Min(8, flames));
				activePhases[currentPhaseIndex].FlamesEarned = flames;
				activePhases[currentPhaseIndex].IsCompleted = true;
				RecalculateDancerFlames();
				SendChatNotification($"{tag} Phase {CurrentPhaseNumber}/{maxP} ({activePhases[currentPhaseIndex].PhaseName}) set to {flames}/8 flames. Total: {TotalFlames}/{maxF}.");
				UpdateOverlay();
			}
			else if (args.Length == 2 && int.TryParse(args[0], out int phaseNum) && int.TryParse(args[1], out int pFlames))
			{
				if (phaseNum >= 1 && phaseNum <= maxP)
				{
					pFlames = Math.Max(0, Math.Min(8, pFlames));
					activePhases[phaseNum - 1].FlamesEarned = pFlames;
					activePhases[phaseNum - 1].IsCompleted = true;
					RecalculateDancerFlames();
					SendChatNotification($"{tag} Phase {phaseNum}/{maxP} ({activePhases[phaseNum - 1].PhaseName}) set to {pFlames}/8 flames. Total: {TotalFlames}/{maxF}.");
					UpdateOverlay();
				}
				else
				{
					SendChatNotification($"{tag} Phase number must be between 1 and {maxP}.");
				}
			}
			else
			{
				SendChatNotification($"{tag} Usage: /mvset <flames 0-8> OR /mvset <phase 1-{maxP}> <flames 0-8>");
			}
			return;
		}

		if (packet.IsCommand("mvdancer", out args) || packet.IsCommand("mvboss", out args))
		{
			packet.Send = false;
			if (args.Length == 2 && int.TryParse(args[0], out int bIndex))
			{
				string name = args[1];
				string matched = name.IndexOf("gen", StringComparison.OrdinalIgnoreCase) >= 0 ? "Genji" :
				                 name.IndexOf("kag", StringComparison.OrdinalIgnoreCase) >= 0 ? "Kaguya" :
				                 name.IndexOf("mik", StringComparison.OrdinalIgnoreCase) >= 0 ? "Miko" :
				                 name.IndexOf("umi", StringComparison.OrdinalIgnoreCase) >= 0 ? "Umi" : null;
				if (matched != null)
				{
					if (bIndex == 1) Boss1Dancer = matched;
					else if (bIndex == 2) Boss2Dancer = matched;
					else if (bIndex == 3) Boss3Dancer = matched;
					CurrentDancer = matched;
					SendChatNotification($"[MV Tracker] Boss {bIndex} manually set to: {matched}.");
					UpdateOverlay();
				}
				else
				{
					SendChatNotification("[MV Tracker] Unknown dancer name. Choose: Genji, Kaguya, Miko, Umi.");
				}
			}
			else
			{
				SendChatNotification("[MV Tracker] Usage: /mvdancer <1-3> <Genji|Kaguya|Miko>");
			}
			return;
		}

		if (packet.IsCommand("mvhelp", out args))
		{
			packet.Send = false;
			SendChatNotification("[MV Tracker] Commands: /mv (status), /umi (toggle Umi mode), /mvnext (advance), /mvprev (back), /mvadd <flames>, /mvset <flames>, /mvdancer <1-3> <name>, /mvreset (reset).");
			return;
		}
	}

	public void NextPhase()
	{
		phaseCompleteHoldUntil = DateTime.MinValue;
		PhaseInfo[] active = CurrentPhases;
		if (currentPhaseIndex >= 0 && currentPhaseIndex < active.Length)
		{
			active[currentPhaseIndex].IsCompleted = true;
			if (isWispBurstActive)
			{
				FinishWispBurst();
				return;
			}
			int completedPhase = currentPhaseIndex;
			CheckBossCompletion(completedPhase);
			if (currentPhaseIndex < active.Length - 1)
			{
				currentPhaseIndex++;
				currentPhaseStartTime = DateTime.UtcNow;
				string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";
				SendChatNotification($"{tag} Manually advanced to Phase {CurrentPhaseNumber}/{active.Length}: {CurrentPhaseName}");
			}
			currentPhaseWispIds.Clear();
			UpdateOverlay();
		}
	}

	public void PreviousPhase()
	{
		phaseCompleteHoldUntil = DateTime.MinValue;
		if (currentPhaseIndex > 0)
		{
			currentPhaseIndex--;
			CurrentPhases[currentPhaseIndex].IsCompleted = false;
			currentPhaseStartTime = DateTime.UtcNow;
			currentPhaseWispIds.Clear();
			string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";
			SendChatNotification($"{tag} Reverted to Phase {CurrentPhaseNumber}/{CurrentPhases.Length}: {CurrentPhaseName}");
			UpdateOverlay();
		}
	}

	public void AddFlameToCurrentPhase(int delta)
	{
		PhaseInfo[] active = CurrentPhases;
		int targetIndex = (DateTime.UtcNow < phaseCompleteHoldUntil && lastCompletedPhaseIndex >= 0)
			? lastCompletedPhaseIndex
			: currentPhaseIndex;

		if (targetIndex >= 0 && targetIndex < active.Length)
		{
			int current = active[targetIndex].FlamesEarned;
			int updated = Math.Max(0, Math.Min(8, current + delta));
			active[targetIndex].FlamesEarned = updated;
			active[targetIndex].IsCompleted = true;
			if (targetIndex == lastCompletedPhaseIndex)
			{
				lastCompletedPhaseFlames = updated;
			}
			RecalculateDancerFlames();
			string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";
			SendChatNotification($"{tag} Phase {targetIndex + 1}/{active.Length} ({active[targetIndex].PhaseName}) adjusted to {updated}/8 flames (Total: {TotalFlames}/{MaxTotalFlames}).");
			UpdateOverlay();
		}
	}

	public static string DeduceRemainingDancer(string d1, string d2)
	{
		var dancers = new HashSet<string> { "Miko", "Genji", "Kaguya" };
		if (!string.IsNullOrEmpty(d1) && d1 != "Dancer 1") dancers.Remove(d1);
		if (!string.IsNullOrEmpty(d2) && d2 != "Dancer 2") dancers.Remove(d2);
		if (dancers.Count == 1)
		{
			foreach (string d in dancers) return d;
		}
		return "Dancer 3";
	}

	private void ApplyBossOrder(string b1, string b2, string b3)
	{
		bool changed = Boss1Dancer != b1 || Boss2Dancer != b2 || Boss3Dancer != b3;
		Boss1Dancer = b1;
		Boss2Dancer = b2;
		Boss3Dancer = b3;

		if (DancersCompleted == 0)
		{
			CurrentDancer = b1;
			currentBossIndex = 1;
		}
		else if (DancersCompleted == 1)
		{
			CurrentDancer = b2;
			currentBossIndex = 2;
		}
		else if (DancersCompleted == 2)
		{
			CurrentDancer = b3;
			currentBossIndex = 3;
		}

		if (changed)
		{
			SendChatNotification($"[MV Tracker] ⚔ Full Boss Order Detected from Dialogue: 1: {b1} → 2: {b2} → 3: {b3}!");
		}
		UpdateOverlay();
	}

	private void SetBossDancer(int bossIndex, string dancerName)
	{
		if (string.IsNullOrEmpty(dancerName) || dancerName == "None") return;

		if (bossIndex == 1)
		{
			if (Boss1Dancer == "Dancer 1" || Boss1Dancer != dancerName)
			{
				Boss1Dancer = dancerName;
				CurrentDancer = dancerName;
				currentBossIndex = 1;
				AnnounceDancerEncounter(1, dancerName);
			}
		}
		else if (bossIndex == 2)
		{
			if (dancerName == Boss1Dancer) return;
			if (Boss2Dancer == "Dancer 2" || Boss2Dancer != dancerName)
			{
				Boss2Dancer = dancerName;
				CurrentDancer = dancerName;
				currentBossIndex = 2;
				AnnounceDancerEncounter(2, dancerName);

				string deduced = DeduceRemainingDancer(Boss1Dancer, Boss2Dancer);
				if (deduced != "Dancer 3" && Boss3Dancer == "Dancer 3")
				{
					Boss3Dancer = deduced;
					SendChatNotification($"[MV Tracker] ℹ Boss 3 Deduced: {deduced}!");
				}
			}
		}
		else if (bossIndex == 3)
		{
			if (dancerName == Boss1Dancer || dancerName == Boss2Dancer) return;
			if (Boss3Dancer == "Dancer 3" || Boss3Dancer != dancerName)
			{
				Boss3Dancer = dancerName;
				CurrentDancer = dancerName;
				currentBossIndex = 3;
				AnnounceDancerEncounter(3, dancerName);
			}
		}

		UpdateOverlay();
	}

	private void RegisterDancer(string dancerName)
	{
		if (string.IsNullOrEmpty(dancerName) || dancerName == "None") return;
		// UPDATE packets repeatedly observe the same boss. This is not a new engagement.
		int expectedBoss = dancerName == "Umi" ? 4 : Math.Min(3, DancersCompleted + 1);
		if (CurrentDancer == dancerName && currentBossIndex == expectedBoss) return;

		if (dancerName == "Umi")
		{
			CurrentDancer = "Umi";
			currentBossIndex = 4;
			AnnounceDancerEncounter(4, "Kitsune Umi");
			UpdateOverlay();
			return;
		}

		if (DancersCompleted == 0)
		{
			if (Boss1Dancer == "Dancer 1")
			{
				Boss1Dancer = dancerName;
				CurrentDancer = dancerName;
				currentBossIndex = 1;
				AnnounceDancerEncounter(1, dancerName);
			}
			else
			{
				CurrentDancer = Boss1Dancer;
			}
		}
		else if (DancersCompleted == 1)
		{
			// Boss 2 CANNOT be Boss 1!
			if (dancerName == Boss1Dancer) return;

			if (Boss2Dancer == "Dancer 2")
			{
				Boss2Dancer = dancerName;
				CurrentDancer = dancerName;
				currentBossIndex = 2;
				AnnounceDancerEncounter(2, dancerName);

				// Deduce Boss 3 immediately!
				string deduced = DeduceRemainingDancer(Boss1Dancer, Boss2Dancer);
				if (deduced != "Dancer 3" && Boss3Dancer == "Dancer 3")
				{
					Boss3Dancer = deduced;
					SendChatNotification($"[MV Tracker] ℹ Boss 3 Deduced: {deduced}!");
				}
			}
			else
			{
				CurrentDancer = Boss2Dancer;
			}
		}
		else if (DancersCompleted == 2)
		{
			// Boss 3 CANNOT be Boss 1 or Boss 2!
			if (dancerName == Boss1Dancer || dancerName == Boss2Dancer) return;

			if (Boss3Dancer == "Dancer 3" || Boss3Dancer != dancerName)
			{
				Boss3Dancer = dancerName;
			}
			CurrentDancer = Boss3Dancer;
			currentBossIndex = 3;
			AnnounceDancerEncounter(3, Boss3Dancer);
		}
		else
		{
			CurrentDancer = (TotalFlames >= 78) ? "Waiting for Umi" : "Dungeon Completed";
		}

		UpdateOverlay();
	}

	private void SetDancerName(string dancerName)
	{
		RegisterDancer(dancerName);
	}

	private void OnDancerEngaged(string dancerName)
	{
		RegisterDancer(dancerName);
	}

	private void FinishWispBurst()
	{
		isWispBurstActive = false;
		int flames = Math.Min(8, currentPhaseWispIds.Count);
		PhaseInfo[] active = CurrentPhases;

		if (currentPhaseIndex >= 0 && currentPhaseIndex < active.Length)
		{
			active[currentPhaseIndex].FlamesEarned = flames;
			active[currentPhaseIndex].IsCompleted = true;
			RecalculateDancerFlames();

			int total = TotalFlames;
			int maxTotal = MaxTotalFlames;
			int maxPhases = MaxPhases;
			int tier = GetTier(total, IsUmiMode);
			string tierName = GetTierName(total, IsUmiMode);

			if (EnableFloatingNotifications)
			{
				SendFloatingNotification($"+{flames} Flames! ({total}/{maxTotal})", GetTierNotificationColor(tier));
			}

			string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";
			SendChatNotification($"{tag} Phase {CurrentPhaseNumber}/{maxPhases} ({active[currentPhaseIndex].PhaseName}): {flames}/8 Flames! (Total: {total}/{maxTotal}) | {tierName}");

			int completedPhase = currentPhaseIndex;
			lastCompletedPhaseIndex = completedPhase;
			lastCompletedPhaseFlames = flames;
			phaseCompleteHoldUntil = DateTime.UtcNow.AddSeconds(7); // 7-second display hold

			CheckBossCompletion(completedPhase);

			if (currentPhaseIndex < active.Length - 1)
			{
				currentPhaseIndex++;
				currentPhaseStartTime = DateTime.UtcNow;
			}
			else
			{
				if (IsUmiMode)
				{
					SendChatNotification($"[Umi Tracker] 🏆 Kitsune Umi Encounter Completed! Final Total: {total}/56 Flames | {tierName}!");
				}
				else
				{
					SendChatNotification($"[MV Tracker] 🏆 Moonlight Village Completed! Final Total: {total}/88 Flames | {tierName}!");
				}
			}
		}

		currentPhaseWispIds.Clear();
		UpdateOverlay();
	}

	private void CommitCurrentPhase(int flames)
	{
		flames = Math.Max(0, Math.Min(8, flames));
		PhaseInfo[] active = CurrentPhases;

		if (currentPhaseIndex >= 0 && currentPhaseIndex < active.Length)
		{
			active[currentPhaseIndex].FlamesEarned = flames;
			active[currentPhaseIndex].IsCompleted = true;
			RecalculateDancerFlames();

			int total = TotalFlames;
			int maxTotal = MaxTotalFlames;
			int maxPhases = MaxPhases;
			string tierName = GetTierName(total, IsUmiMode);

			string tag = IsUmiMode ? "[Umi Tracker]" : "[MV Tracker]";
			SendChatNotification($"{tag} Phase {CurrentPhaseNumber}/{maxPhases} ({active[currentPhaseIndex].PhaseName}): {flames}/8 Flames. (Total: {total}/{maxTotal}) | {tierName}");

			int completedPhase = currentPhaseIndex;
			lastCompletedPhaseIndex = completedPhase;
			lastCompletedPhaseFlames = flames;
			phaseCompleteHoldUntil = DateTime.UtcNow.AddSeconds(7); // 7-second display hold

			CheckBossCompletion(completedPhase);

			if (currentPhaseIndex < active.Length - 1)
			{
				currentPhaseIndex++;
				currentPhaseStartTime = DateTime.UtcNow;
			}
		}

		currentPhaseWispIds.Clear();
		isWispBurstActive = false;
		UpdateOverlay();
	}

	private void CheckBossCompletion(int completedPhaseIndex)
	{
		if (IsUmiMode)
		{
			if (completedPhaseIndex == 1) // Phase 2 (end of Act 1)
			{
				currentBossIndex = 2;
				SendChatNotification($"[Umi Tracker] ★ Act 1 Finished: {UmiAct1Flames}/16 Flames! (Total: {TotalFlames}/56) | {GetTierName(TotalFlames, true)}");
			}
			else if (completedPhaseIndex == 3) // Phase 4 (end of Act 2)
			{
				currentBossIndex = 3;
				SendChatNotification($"[Umi Tracker] ★ Act 2 Finished: {UmiAct2Flames}/16 Flames! (Total: {TotalFlames}/56) | {GetTierName(TotalFlames, true)}");
			}
			else if (completedPhaseIndex == 6) // Phase 7 (end of Finale)
			{
				isCombatActive = false;
				SendChatNotification($"[Umi Tracker] ★ Finale Finished: {UmiAct3Flames}/24 Flames! (Final Score: {TotalFlames}/56) | {GetTierName(TotalFlames, true)}");
			}
			return;
		}

		if (completedPhaseIndex == 1) // Phase 2 (end of Boss 1)
		{
			DancersCompleted = Math.Max(DancersCompleted, 1);
			SendChatNotification($"[MV Tracker] ★ Boss 1 ({Boss1Dancer}) Finished: {Boss1Flames}/16 Flames! (Total: {TotalFlames}/88) | {GetTierName(TotalFlames, false)}");
			isCombatActive = false;
			CurrentDancer = "Waiting for Boss 2";
		}
		else if (completedPhaseIndex == 5) // Phase 6 (end of Boss 2)
		{
			DancersCompleted = Math.Max(DancersCompleted, 2);
			SendChatNotification($"[MV Tracker] ★ Boss 2 ({Boss2Dancer}) Finished: {Boss2Flames}/32 Flames! (Total: {TotalFlames}/88) | {GetTierName(TotalFlames, false)}");
			isCombatActive = false;
			CurrentDancer = "Waiting for Boss 3";
		}
		else if (completedPhaseIndex == 10) // Phase 11 (end of Boss 3)
		{
			DancersCompleted = Math.Max(DancersCompleted, 3);
			SendChatNotification($"[MV Tracker] ★ Boss 3 ({Boss3Dancer}) Finished: {Boss3Flames}/40 Flames! (Total: {TotalFlames}/88) | {GetTierName(TotalFlames, false)}");
			isCombatActive = false;
			if (TotalFlames >= 78)
			{
				CurrentDancer = "Waiting for Umi";
				SendChatNotification("[MV Tracker] ★ Tier 4 Achieved! Kitsune Umi Encounter Unlocked! ★");
			}
			else
			{
				CurrentDancer = "Dungeon Completed";
			}
		}
	}

	private void OnBossCompletedDialogue()
	{
		if (isWispBurstActive)
		{
			FinishWispBurst();
		}

		int endPhaseIdx = currentBossIndex switch
		{
			1 => 1,
			2 => 5,
			3 => 10,
			_ => currentPhaseIndex
		};

		for (int i = 0; i <= endPhaseIdx; i++)
		{
			if (!villagePhases[i].IsCompleted)
			{
				villagePhases[i].IsCompleted = true;
			}
		}

		if (currentBossIndex == 1 && DancersCompleted < 1)
		{
			DancersCompleted = 1;
			SendChatNotification($"[MV Tracker] ★ Boss 1 ({Boss1Dancer}) Finished: {Boss1Flames}/16 Flames! (Total: {TotalFlames}/88) | {GetTierName(TotalFlames, false)}");
			currentPhaseIndex = 2; // Advance to Boss 2 DPS Phase 1
			currentBossIndex = 2;
			isCombatActive = false;
			CurrentDancer = "Waiting for Boss 2";
		}
		else if (currentBossIndex == 2 && DancersCompleted < 2)
		{
			DancersCompleted = 2;
			SendChatNotification($"[MV Tracker] ★ Boss 2 ({Boss2Dancer}) Finished: {Boss2Flames}/32 Flames! (Total: {TotalFlames}/88) | {GetTierName(TotalFlames, false)}");
			currentPhaseIndex = 6; // Advance to Boss 3 DPS Phase 1
			currentBossIndex = 3;
			isCombatActive = false;
			CurrentDancer = "Waiting for Boss 3";
		}
		else if (currentBossIndex == 3 && DancersCompleted < 3)
		{
			DancersCompleted = 3;
			SendChatNotification($"[MV Tracker] ★ Boss 3 ({Boss3Dancer}) Finished: {Boss3Flames}/40 Flames! (Total: {TotalFlames}/88) | {GetTierName(TotalFlames, false)}");
			SendChatNotification($"[MV Tracker] 🏆 Moonlight Village Finished! Final Score: {TotalFlames}/88 Flames | {GetTierName(TotalFlames, false)}!");
			currentPhaseIndex = 10;
			isCombatActive = false;
			if (TotalFlames >= 78)
			{
				CurrentDancer = "Waiting for Umi";
				SendChatNotification("[MV Tracker] ★ Tier 4 Achieved! Kitsune Umi Encounter Unlocked! ★");
			}
			else
			{
				CurrentDancer = "Dungeon Completed";
			}
		}

		RecalculateDancerFlames();
		UpdateOverlay();
	}

	private void RecalculateDancerFlames()
	{
		MikoFlames = 0;
		GenjiFlames = 0;
		KaguyaFlames = 0;

		AddDancerFlames(Boss1Dancer, Boss1Flames);
		AddDancerFlames(Boss2Dancer, Boss2Flames);
		AddDancerFlames(Boss3Dancer, Boss3Flames);
	}

	private void AddDancerFlames(string dancer, int flames)
	{
		if (dancer == "Miko") MikoFlames += flames;
		else if (dancer == "Genji") GenjiFlames += flames;
		else if (dancer == "Kaguya") KaguyaFlames += flames;
	}

	public void ActivateUmiMode()
	{
		IsUmiMode = true;
		CurrentDancer = "Umi";
		currentPhaseIndex = 0;
		currentBossIndex = 1;
		isCombatActive = false;
		activeBossEntityId = -1;
		activeLanternEntityId = -1;
		isLanternSystemAlive = false;
		lanternPendingCompletion = false;
		lanternDespawnTime = DateTime.MinValue;
		currentPhaseStartTime = DateTime.UtcNow;
		isWispBurstActive = false;
		lastCompletedPhaseIndex = -1;
		lastCompletedPhaseFlames = 0;
		phaseCompleteHoldUntil = DateTime.MinValue;

		currentPhaseWispIds.Clear();

		for (int i = 0; i < umiPhases.Length; i++)
		{
			umiPhases[i].FlamesEarned = 0;
			umiPhases[i].IsCompleted = false;
		}

		SendChatNotification("[Umi Tracker] 🦊 Kitsune Umi Mode Activated! (7 Phases, 0/56 Flames)");
		SendChatNotification("[Umi Tracker] Tiers: T4 (48-56, Top Loot) | T3 (36-46) | T2 (24-34) | T1 (2-22)");
		UpdateOverlay();
	}

	public void ToggleUmiMode()
	{
		if (IsUmiMode)
		{
			IsUmiMode = false;
			CurrentDancer = (DancersCompleted >= 3) ? "Dungeon Completed" : (Boss3Dancer != "Dancer 3" ? Boss3Dancer : "Waiting for Boss 1");
			currentBossIndex = Math.Min(3, Math.Max(1, DancersCompleted + 1));
			currentPhaseIndex = Math.Min(villagePhases.Length - 1, DancersCompleted switch
			{
				1 => 2,
				2 => 6,
				3 => 10,
				_ => 0
			});
			SendChatNotification("[MV Tracker] 🌙 Switched back to Standard Village Mode (11 Phases, 0/88 Flames).");
		}
		else
		{
			ActivateUmiMode();
		}
		UpdateOverlay();
	}

	public void ResetCounters(bool fullReset = true)
	{
		if (fullReset || !IsUmiMode)
		{
			announcedDancerEncounters.Clear();
			IsUmiMode = false;
			currentBossIndex = 1;
			DancersCompleted = 0;
			CurrentDancer = "Waiting for Boss 1";
			Boss1Dancer = "Dancer 1";
			Boss2Dancer = "Dancer 2";
			Boss3Dancer = "Dancer 3";

			MikoFlames = 0;
			GenjiFlames = 0;
			KaguyaFlames = 0;

			for (int i = 0; i < villagePhases.Length; i++)
			{
				villagePhases[i].FlamesEarned = 0;
				villagePhases[i].IsCompleted = false;
			}
		}

		currentPhaseIndex = 0;
		if (IsUmiMode)
		{
			CurrentDancer = "Umi";
			currentBossIndex = 1;
		}

		for (int i = 0; i < umiPhases.Length; i++)
		{
			umiPhases[i].FlamesEarned = 0;
			umiPhases[i].IsCompleted = false;
		}

		isCombatActive = false;
		activeBossEntityId = -1;
		activeLanternEntityId = -1;
		isLanternSystemAlive = false;
		lanternPendingCompletion = false;
		lanternDespawnTime = DateTime.MinValue;
		currentPhaseStartTime = DateTime.UtcNow;
		isWispBurstActive = false;
		lastCompletedPhaseIndex = -1;
		lastCompletedPhaseFlames = 0;
		phaseCompleteHoldUntil = DateTime.MinValue;

		currentPhaseWispIds.Clear();
		allSeenFlameEntityIds.Clear();
	}

	public static int GetTier(int totalFlames, bool isUmi = false)
	{
		if (isUmi)
		{
			if (totalFlames >= 48) return 4;
			if (totalFlames >= 36) return 3;
			if (totalFlames >= 24) return 2;
			if (totalFlames >= 2) return 1;
			return 0; // 0 to 1 flames
		}

		if (totalFlames >= 78) return 4;
		if (totalFlames >= 58) return 3;
		if (totalFlames >= 40) return 2;
		if (totalFlames >= 2) return 1;
		return 0; // 0 to 1 flames
	}

	public static string GetTierName(int totalFlames, bool isUmi = false)
	{
		int tier = GetTier(totalFlames, isUmi);
		if (isUmi)
		{
			return tier switch
			{
				4 => "Tier 4 (White Bag / Top Loot)",
				3 => "Tier 3",
				2 => "Tier 2",
				1 => "Tier 1",
				_ => "0-Flame / Fail"
			};
		}

		return tier switch
		{
			4 => "Tier 4 (100% Umi)",
			3 => "Tier 3",
			2 => "Tier 2",
			1 => "Tier 1",
			_ => "0-Flame / No Tier"
		};
	}

	public int GetMaxPossibleFlames()
	{
		PhaseInfo[] active = CurrentPhases;
		int committed = 0;
		int uncompletedPhases = 0;

		for (int i = 0; i < active.Length; i++)
		{
			if (active[i].IsCompleted)
			{
				committed += active[i].FlamesEarned;
			}
			else if (i == currentPhaseIndex && isWispBurstActive)
			{
				committed += Math.Min(8, currentPhaseWispIds.Count);
			}
			else
			{
				uncompletedPhases++;
			}
		}

		return Math.Min(MaxTotalFlames, committed + (uncompletedPhases * 8));
	}

	public int GetFlamesLost()
	{
		PhaseInfo[] active = CurrentPhases;
		int maxPossibleSoFar = 0;
		int actualSoFar = 0;

		for (int i = 0; i < active.Length; i++)
		{
			if (active[i].IsCompleted)
			{
				maxPossibleSoFar += 8;
				actualSoFar += active[i].FlamesEarned;
			}
		}

		return Math.Max(0, maxPossibleSoFar - actualSoFar);
	}

	public int GetTier4Margin()
	{
		int maxAllowedLoss = IsUmiMode ? 8 : 10;
		int lost = GetFlamesLost();
		return Math.Max(0, maxAllowedLoss - lost);
	}

	public string GetRouteString()
	{
		int total = TotalFlames;
		int maxPossible = GetMaxPossibleFlames();

		if (IsUmiMode)
		{
			if (total >= 48)
			{
				return "★ Tier 4 (Top Loot Guaranteed)! ★";
			}
			if (maxPossible >= 48)
			{
				int margin = maxPossible - 48;
				return $"Tier 4 Reachable (Margin: {margin})";
			}
			if (maxPossible >= 36)
			{
				return $"Tier 3 Max (Need {36 - total} for T3)";
			}
			if (maxPossible >= 24)
			{
				return $"Tier 2 Max (Need {24 - total} for T2)";
			}
			if (maxPossible >= 2)
			{
				return $"Tier 1 Max ({total}/22 flames)";
			}
			return "✦ 0-Flame Challenge / Fail ✦";
		}

		if (total >= 78)
		{
			return "★ Tier 4 (100% Umi Achieved!) ★";
		}

		if (maxPossible >= 78)
		{
			int margin = maxPossible - 78;
			return $"Tier 4 Reachable (Margin: {margin})";
		}

		if (maxPossible >= 58)
		{
			return $"Tier 3 Max (Need {58 - total} for T3)";
		}

		if (maxPossible >= 40)
		{
			return $"Tier 2 Max (Need {40 - total} for T2)";
		}

		if (maxPossible >= 2)
		{
			return $"Tier 1 Max ({total}/38 flames)";
		}

		return "✦ 0-Flame Challenge ✦";
	}

	private void PrintStatus()
	{
		int total = TotalFlames;
		int maxTotal = MaxTotalFlames;
		int tier = GetTier(total, IsUmiMode);
		string tierName = GetTierName(total, IsUmiMode);
		int maxPossible = GetMaxPossibleFlames();
		int margin = GetTier4Margin();
		int maxPhases = MaxPhases;

		StringBuilder sb = new StringBuilder();
		if (IsUmiMode)
		{
			sb.AppendLine($"[Umi Tracker] 🦊 Kitsune Umi Encounter Status:");
			sb.AppendLine($"• Total Flames: {total}/56 | {tierName}");
			sb.AppendLine($"• Current Phase: {CurrentPhaseNumber}/7 ({CurrentPhaseName}) | {CurrentPhaseFlames}/8 flames");
			sb.AppendLine($"• Act 1 (P1-2): {UmiAct1Flames}/16 (DPS: {umiPhases[0].FlamesEarned}/8, Danmaku: {umiPhases[1].FlamesEarned}/8)");
			sb.AppendLine($"• Act 2 (P3-4): {UmiAct2Flames}/16 (DPS: {umiPhases[2].FlamesEarned}/8, Danmaku: {umiPhases[3].FlamesEarned}/8)");
			sb.AppendLine($"• Finale (P5-7): {UmiAct3Flames}/24 (DPS: {umiPhases[4].FlamesEarned}/8, Danmaku: {umiPhases[5].FlamesEarned}/8, Finale: {umiPhases[6].FlamesEarned}/8)");
			sb.AppendLine($"• Tiers: T4 (48-56, Top Loot) | T3 (36-46) | T2 (24-34) | T1 (2-22)");

			if (total >= 48)
			{
				sb.AppendLine($"• Status: ★ Tier 4 Achieved! Top Loot Guaranteed ★");
			}
			else if (maxPossible >= 48)
			{
				sb.AppendLine($"• Status: Tier 4 Reachable! (Buffer: {margin} flames left to lose)");
			}
			else
			{
				sb.AppendLine($"• Status: Tier 4 Locked • Max Possible: {maxPossible}/56 ({GetTierName(maxPossible, true)})");
			}
		}
		else
		{
			sb.AppendLine($"[MV Tracker] Moonlight Village Status:");
			sb.AppendLine($"• Total Flames: {total}/88 | {tierName}");
			sb.AppendLine($"• Current Phase: {CurrentPhaseNumber}/11 ({CurrentPhaseName}) | {CurrentPhaseFlames}/8 flames");
			sb.AppendLine($"• Active Dancer: {CurrentDancer}");
			sb.AppendLine($"• Boss 1 ({Boss1Dancer}): {Boss1Flames}/16 (DPS: {villagePhases[0].FlamesEarned}/8, Lantern: {villagePhases[1].FlamesEarned}/8)");
			sb.AppendLine($"• Boss 2 ({Boss2Dancer}): {Boss2Flames}/32 (DPS1: {villagePhases[2].FlamesEarned}/8, Lant1: {villagePhases[3].FlamesEarned}/8, DPS2: {villagePhases[4].FlamesEarned}/8, Lant2: {villagePhases[5].FlamesEarned}/8)");
			sb.AppendLine($"• Boss 3 ({Boss3Dancer}): {Boss3Flames}/40 (DPS1: {villagePhases[6].FlamesEarned}/8, Lant1: {villagePhases[7].FlamesEarned}/8, DPS2: {villagePhases[8].FlamesEarned}/8, Lant2: {villagePhases[9].FlamesEarned}/8, Finale: {villagePhases[10].FlamesEarned}/8)");
			sb.AppendLine($"• Tiers: T4 (78-88, 100% Umi) | T3 (58-76) | T2 (40-56) | T1 (2-38)");

			if (total >= 78)
			{
				sb.AppendLine($"• Status: ★ Tier 4 Achieved! 100% Umi Guaranteed ★");
			}
			else if (maxPossible >= 78)
			{
				sb.AppendLine($"• Status: Tier 4 Reachable! (Buffer: {margin} flames left to lose)");
			}
			else
			{
				sb.AppendLine($"• Status: Tier 4 Locked • Max Possible: {maxPossible}/88 ({GetTierName(maxPossible, false)})");
			}
		}

		SendChatNotification(sb.ToString());
	}

	private void AnnounceDancerEncounter(int bossIndex, string dancer)
	{
		if (announcedDancerEncounters.Add(bossIndex + ":" + dancer))
			SendChatNotification($"[MV Tracker] Boss {bossIndex}: {dancer} Active!");
	}

	private static string ToGameChatText(string message)
	{
		// The game's chat font logs a stack trace for unsupported glyphs on each render.
		// Keep rich symbols in the overlay, but use ASCII in injected chat messages.
		StringBuilder result = new StringBuilder(message.Length);
		foreach (char c in message)
		{
			if (c <= 127) result.Append(c);
		}
		return result.ToString();
	}

	// MV information is HUD-only, including command responses and floating notices.
	public void SendChatNotification(string message) { }

	public void SendFloatingNotification(string text, int color) { }

	private static int GetTierNotificationColor(int tier)
	{
		return tier switch
		{
			4 => 0xFFD700, // Gold
			3 => 0xC084FC, // Purple
			2 => 0x38BDF8, // Sky blue
			1 => 0x4ADE80, // Green
			_ => 0x94A3B8  // Gray
		};
	}

	private void UpdateOverlay()
	{
		int total = TotalFlames;
		PhaseInfo[] active = CurrentPhases;
		bool isHolding = DateTime.UtcNow < phaseCompleteHoldUntil && lastCompletedPhaseIndex >= 0;
		int displayNum = isHolding ? (lastCompletedPhaseIndex + 1) : CurrentPhaseNumber;
		string displayName = isHolding ? active[lastCompletedPhaseIndex].PhaseName : CurrentPhaseName;
		int displayFlames = isHolding ? lastCompletedPhaseFlames : CurrentPhaseFlames;
		double secRemaining = isHolding ? Math.Max(0, (phaseCompleteHoldUntil - DateTime.UtcNow).TotalSeconds) : 0;

		var snapshot = new MoonlightVillageSnapshot
		{
			IsInDungeon = IsInMoonlightVillage,
			IsUmiMode = IsUmiMode,
			MaxRunFlames = MaxTotalFlames,
			MaxPhases = MaxPhases,
			ActiveDancer = CurrentDancer,
			CurrentBossIndex = currentBossIndex,
			CurrentPhaseNumber = CurrentPhaseNumber,
			CurrentPhaseName = CurrentPhaseName,
			CurrentPhaseFlames = CurrentPhaseFlames,

			IsHoldingPhaseResult = isHolding,
			HoldUntilUtc = phaseCompleteHoldUntil,
			DisplayPhaseNumber = displayNum,
			DisplayPhaseName = displayName,
			DisplayPhaseFlames = displayFlames,
			HoldSecondsRemaining = secRemaining,

			IsLanternActive = isLanternSystemAlive,
			IsLanternPhase = (currentPhaseIndex >= 0 && currentPhaseIndex < active.Length) && active[currentPhaseIndex].IsLanternPhase,
			TotalFlames = total,
			MaxPossibleFlames = GetMaxPossibleFlames(),
			FlamesLost = GetFlamesLost(),
			Tier4Margin = GetTier4Margin(),
			CurrentTier = GetTier(total, IsUmiMode),
			TierName = GetTierName(total, IsUmiMode),
			RouteStatus = GetRouteString(),

			Boss1Name = Boss1Dancer,
			Boss1Flames = Boss1Flames,
			Boss1MaxFlames = 16,

			Boss2Name = Boss2Dancer,
			Boss2Flames = Boss2Flames,
			Boss2MaxFlames = 32,

			Boss3Name = Boss3Dancer,
			Boss3Flames = Boss3Flames,
			Boss3MaxFlames = 40,

			UmiAct1Flames = UmiAct1Flames,
			UmiAct2Flames = UmiAct2Flames,
			UmiAct3Flames = UmiAct3Flames,

			MikoFlames = MikoFlames,
			GenjiFlames = GenjiFlames,
			KaguyaFlames = KaguyaFlames,
			UmiFlames = UmiFlames,
			DancersCompleted = DancersCompleted
		};

		DpsOverlayManager.UpdateMoonlightVillage(snapshot);
	}
}

public class MoonlightVillageSnapshot
{
	public bool IsInDungeon { get; set; }
	public bool IsUmiMode { get; set; }
	public int MaxRunFlames { get; set; } = 88;
	public int MaxPhases { get; set; } = 11;

	public string ActiveDancer { get; set; } = "None";
	public int CurrentBossIndex { get; set; } = 1;
	public int CurrentPhaseNumber { get; set; } = 1;
	public string CurrentPhaseName { get; set; } = "DPS Phase";
	public int CurrentPhaseFlames { get; set; }

	public bool IsHoldingPhaseResult { get; set; }
	public DateTime HoldUntilUtc { get; set; } = DateTime.MinValue;
	public int DisplayPhaseNumber { get; set; } = 1;
	public string DisplayPhaseName { get; set; } = "DPS Phase";
	public int DisplayPhaseFlames { get; set; }
	public double HoldSecondsRemaining { get; set; }

	public bool IsLanternActive { get; set; }
	public bool IsLanternPhase { get; set; }
	public int TotalFlames { get; set; }
	public int MaxPossibleFlames { get; set; } = 88;
	public int FlamesLost { get; set; }
	public int Tier4Margin { get; set; } = 10;
	public int CurrentTier { get; set; } = 0;
	public string TierName { get; set; } = "No Tier";
	public string RouteStatus { get; set; } = "Tier 4 Reachable";

	public string Boss1Name { get; set; } = "Dancer 1";
	public int Boss1Flames { get; set; }
	public int Boss1MaxFlames { get; set; } = 16;

	public string Boss2Name { get; set; } = "Dancer 2";
	public int Boss2Flames { get; set; }
	public int Boss2MaxFlames { get; set; } = 32;

	public string Boss3Name { get; set; } = "Dancer 3";
	public int Boss3Flames { get; set; }
	public int Boss3MaxFlames { get; set; } = 40;

	public int UmiAct1Flames { get; set; }
	public int UmiAct2Flames { get; set; }
	public int UmiAct3Flames { get; set; }

	public int MikoFlames { get; set; }
	public int GenjiFlames { get; set; }
	public int KaguyaFlames { get; set; }
	public int UmiFlames { get; set; }
	public int DancersCompleted { get; set; }
}
