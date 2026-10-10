using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal static class MoonlightVillagePacketLogger
{
	private static readonly object fileLock = new object();
	private static string logFilePath;
	private static volatile bool isSessionActive = false;
	public static bool IsLoggingEnabled { get; set; } = true;
	private static readonly HashSet<int> trackedBossAndPropIds = new HashSet<int>();
	private static readonly Dictionary<int, string> trackedEntityNames = new Dictionary<int, string>();
	private static readonly Dictionary<int, uint> lastKnownConditions = new Dictionary<int, uint>();
	private static readonly Dictionary<int, int> lastKnownAltTextures = new Dictionary<int, int>();

	static MoonlightVillagePacketLogger()
	{
		try
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			logFilePath = Path.Combine(baseDir, "mv_sniff.log");
		}
		catch
		{
			logFilePath = "mv_sniff.log";
		}
	}

	public static string GetLogPath() => logFilePath;

	public static bool IsActive => isSessionActive;

	public static void StartSession(string mapName)
	{
		lock (fileLock)
		{
			isSessionActive = true;
			trackedBossAndPropIds.Clear();
			trackedEntityNames.Clear();
			lastKnownConditions.Clear();
			lastKnownAltTextures.Clear();

			StringBuilder sb = new StringBuilder();
			sb.AppendLine();
			sb.AppendLine("==========================================================================================");
			sb.AppendLine($"[MV SNIFFER SESSION STARTED] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC");
			sb.AppendLine($"Map: {mapName}");
			sb.AppendLine("Target: Capturing all Dialogue, Spawns, Despawns, Status Changes, Effects, and Notifications");
			sb.AppendLine("==========================================================================================");
			sb.AppendLine();

			try
			{
				File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
			}
			catch
			{
			}
		}
	}

	public static void EndSession()
	{
		if (!isSessionActive)
		{
			return;
		}
		lock (fileLock)
		{
			isSessionActive = false;
			StringBuilder sb = new StringBuilder();
			sb.AppendLine();
			sb.AppendLine($"[MV SNIFFER SESSION ENDED] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC");
			sb.AppendLine("==========================================================================================");
			sb.AppendLine();
			try
			{
				File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
			}
			catch
			{
			}
		}
	}

	public static void LogText(TextPacket packet)
	{
		if (!isSessionActive || !IsLoggingEnabled || packet == null)
		{
			return;
		}
		string sender = packet.Name ?? "Unknown";
		string text = packet.Text ?? string.Empty;
		string clean = packet.CleanText ?? string.Empty;

		WriteLine($"[DIALOGUE] From: '{sender}' | Text: \"{text}\" | Clean: \"{clean}\"");
	}

	public static void LogNotification(NotificationPacket packet)
	{
		if (!isSessionActive || !IsLoggingEnabled || packet == null)
		{
			return;
		}
		string entityName = GetEntityName(packet.ObjectId);
		WriteLine($"[NOTIFICATION] EntityId: {packet.ObjectId} ({entityName}) | Msg: \"{packet.Message}\" | Color: 0x{packet.Color:X6} | Type: {packet.NotificationType}");
	}

	public static void LogShowEffect(ShowEffectPacket packet)
	{
		if (!isSessionActive || !IsLoggingEnabled || packet == null)
		{
			return;
		}
		string ownerName = GetEntityName(packet.OwnerId);
		uint colorVal = packet.Color.Value;
		WriteLine($"[SHOW_EFFECT] Effect: {packet.EffectType} ({packet.EffectFlags}) | OwnerId: {packet.OwnerId} ({ownerName}) | Pos1: ({packet.TargetPos.X:F1}, {packet.TargetPos.Y:F1}) | Pos2: ({packet.SecondaryPos.X:F1}, {packet.SecondaryPos.Y:F1}) | Color: 0x{colorVal:X8} | Duration: {packet.Duration:F2}");
	}

	public static void LogObjectSpawn(ushort objectType, int entityId, WorldPosData pos)
	{
		if (!isSessionActive || !IsLoggingEnabled)
		{
			return;
		}

		string name = "Unknown";
		try
		{
			ObjectStructure os = GameData.Objects.GetById(objectType);
			if (os != null && !string.IsNullOrEmpty(os.Name))
			{
				name = os.Name;
			}
		}
		catch
		{
		}

		bool isMvRelated = IsMvRelevantObject(objectType, name);

		if (isMvRelated)
		{
			lock (fileLock)
			{
				trackedBossAndPropIds.Add(entityId);
				trackedEntityNames[entityId] = name;
			}
		}

		WriteLine($"[SPAWN] Type: 0x{objectType:X4} ({objectType}) '{name}' | EntityId: {entityId} | Pos: ({pos.X:F1}, {pos.Y:F1}) | IsMvKey: {isMvRelated}");
	}

	public static void LogObjectDespawn(int entityId)
	{
		if (!isSessionActive || !IsLoggingEnabled)
		{
			return;
		}
		bool isTracked;
		lock (fileLock)
		{
			isTracked = trackedBossAndPropIds.Contains(entityId);
		}
		if (isTracked)
		{
			string name = GetEntityName(entityId);
			WriteLine($"[DESPAWN] EntityId: {entityId} '{name}' disappeared/despawned");
		}
	}

	public static void LogTickStatus(ObjectStatsData stats)
	{
		if (!isSessionActive || !IsLoggingEnabled || stats == null || stats.StatList == null)
		{
			return;
		}
		bool isTracked;
		lock (fileLock)
		{
			isTracked = trackedBossAndPropIds.Contains(stats.ObjectId);
		}
		if (!isTracked)
		{
			return;
		}

		string name = GetEntityName(stats.ObjectId);
		uint cond1 = 0;
		int altTexture = -1;
		int hp = -1;
		int maxHp = -1;

		foreach (StatData sd in stats.StatList)
		{
			int sType = sd.StatTypeField;
			if (sType == 0) // MaximumHP
			{
				maxHp = sd.StatValue;
			}
			else if (sType == 1) // HP
			{
				hp = sd.StatValue;
			}
			else if (sType == 29) // ConditionEffect1 / Effects
			{
				cond1 = (uint)sd.StatValue;
			}
			else if (sType == 61) // AltTextureIndex (Lantern lighting state: 1=Unlit, 2=Low, 3=Half, 4=Full)
			{
				altTexture = sd.StatValue;
			}
		}

		bool condChanged = false;
		bool altChanged = false;

		lock (fileLock)
		{
			if (cond1 != 0)
			{
				if (lastKnownConditions.TryGetValue(stats.ObjectId, out uint prevCond))
				{
					if (prevCond != cond1)
					{
						condChanged = true;
						lastKnownConditions[stats.ObjectId] = cond1;
					}
				}
				else
				{
					lastKnownConditions[stats.ObjectId] = cond1;
					condChanged = true;
				}
			}

			if (altTexture != -1)
			{
				if (lastKnownAltTextures.TryGetValue(stats.ObjectId, out int prevAlt))
				{
					if (prevAlt != altTexture)
					{
						altChanged = true;
						lastKnownAltTextures[stats.ObjectId] = altTexture;
					}
				}
				else
				{
					lastKnownAltTextures[stats.ObjectId] = altTexture;
					altChanged = true;
				}
			}
		}

		if (condChanged)
		{
			bool isInvuln = (cond1 & 0x1800000) != 0;
			WriteLine($"[STATUS CHANGE] EntityId: {stats.ObjectId} '{name}' | Cond1: 0x{cond1:X8} (Invulnerable: {isInvuln}) | HP: {hp}/{maxHp} | Pos: ({stats.Position.X:F1}, {stats.Position.Y:F1})");
		}

		if (altChanged)
		{
			string altName = altTexture switch
			{
				1 => "UnLit (0 Flames)",
				2 => "LowLit (1-2 Flames)",
				3 => "HalfLit (3-4 Flames)",
				4 => "FullLit (5-8 Flames)",
				_ => $"Index {altTexture}"
			};
			WriteLine($"[LANTERN STATE] EntityId: {stats.ObjectId} '{name}' | AltTexture: {altTexture} ({altName})");
		}
	}

	private static string GetEntityName(int entityId)
	{
		lock (fileLock)
		{
			if (trackedEntityNames.TryGetValue(entityId, out string name))
			{
				return name;
			}
		}
		return "Unknown";
	}

	private static bool IsMvRelevantObject(ushort type, string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		if (name.IndexOf("Miko", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Genji", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Kaguya", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Umi", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Wisp", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Flame", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Lantern", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Festival", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		if (name.IndexOf("Event Helper", StringComparison.OrdinalIgnoreCase) >= 0) return true;
		return false;
	}

	public static void WriteLine(string line)
	{
		string formatted = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}";
		lock (fileLock)
		{
			try
			{
				File.AppendAllText(logFilePath, formatted + Environment.NewLine, Encoding.UTF8);
			}
			catch
			{
			}
		}
	}
}
