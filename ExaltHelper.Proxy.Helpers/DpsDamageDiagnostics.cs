using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ExaltHelper.Proxy.Helpers;

// A bounded combat-only trace for comparing local estimates with server reports.
// It contains numeric combat data and the eight-byte combat boost payload only;
// account details, chat, and other packet payloads are never recorded here.
internal sealed class DpsDamageDiagnostics
{
	private const int MaxRows = 20000;
	private const int MaxHistoryFiles = 30;
	private string traceFileName = NewTraceFileName();
	private readonly Queue<string> rows = new Queue<string>();
	private int omittedRows;
	private int lastSaveTick;
	private bool savePending;
	private bool hasSaved;
	private static readonly object fileLock = new object();

	public static string CsvValue(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

	// Ticks keep this current even when the user stays in the dungeon after a fight.
	// Disk writes run off the packet thread and are limited to one every five seconds.
	public void SaveInBackground(bool force = false, string directory = null)
	{
		lock (rows)
		{
			if (rows.Count == 0 || savePending || (!force && hasSaved && unchecked(Environment.TickCount - lastSaveTick) < 5000)) return;
			savePending = true;
		}
		ThreadPool.QueueUserWorkItem(_ =>
		{
			try { Save(directory); }
			finally { lock (rows) { lastSaveTick = Environment.TickCount; hasSaved = true; savePending = false; } }
		});
	}

	public void Record(string row)
	{
		lock (rows)
		{
			if (rows.Count == MaxRows) { rows.Dequeue(); omittedRows++; }
			rows.Enqueue(row);
		}
	}

	public void Reset()
	{
		lock (rows)
		{
			rows.Clear();
			omittedRows = 0;
			traceFileName = NewTraceFileName();
			hasSaved = false;
		}
	}

	private static string NewTraceFileName() => "dps-" +
		DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) +
		"-" + Guid.NewGuid().ToString("N") + ".csv";

	public void Save(string directory = null)
	{
		lock (rows)
		{
			if (rows.Count == 0) return;
			try
			{
				directory = directory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Diagnostics");
				Directory.CreateDirectory(directory);
				string path = Path.Combine(directory, "dps-latest.csv");
				lock (fileLock)
				{
					WriteTrace(path);
					// Update one file per dungeon, so changing class/map cannot erase
					// the fight being investigated. Keep a bounded history on disk.
					string history = Path.Combine(directory, "History");
					Directory.CreateDirectory(history);
					string currentTracePath = Path.Combine(history, traceFileName);
					WriteTrace(currentTracePath);
					foreach (string oldFile in Directory.GetFiles(history, "dps-*.csv")
						.Where(file => file != currentTracePath && Regex.IsMatch(Path.GetFileName(file), @"^dps-\d{8}T\d{9}Z-[0-9a-f]{32}\.csv$"))
						.OrderByDescending(File.GetLastWriteTimeUtc).Skip(MaxHistoryFiles - 1))
					{
						try { File.Delete(oldFile); }
						catch (IOException) { }
						catch (UnauthorizedAccessException) { }
					}
				}
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}

	private void WriteTrace(string path)
	{
		using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
		{
			writer.WriteLine("# build=" + typeof(DpsDamageDiagnostics).Assembly.ManifestModule.ModuleVersionId);
			writer.WriteLine("# omitted_older_rows=" + omittedRows.ToString(CultureInfo.InvariantCulture));
			writer.WriteLine("utc,event,shooter_id,bullet_id,target_id,target_type,container_type,server_created,projectile_damage,reported_damage,previous_damage,applied_delta,target_defense,target_effects,target_effects2,attack,vitality,wisdom,exalt_damage_bonus,local_dungeon_damage,shooter_effects,shooter_effects2,event_multiplier,event_definitions_known,crucible_id,blood_ritual_id,projectile_type,shot_min,shot_max,shot_roll,shot_stats_multiplier,shot_enchants,origin_ability_item,origin_scaling_stat,origin_effects,origin_effects2,origin_vitality,scaling_applied,summoner_id,shooter_class,combat_boost_payload");
			foreach (string row in rows) writer.WriteLine(row);
		}
	}
}
