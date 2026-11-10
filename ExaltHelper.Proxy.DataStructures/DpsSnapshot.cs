using System;
using System.Collections.Generic;
using System.Linq;

namespace ExaltHelper.Proxy.DataStructures;

internal class DpsSnapshot
{
	public string DamageEstimateNote { get; set; }
	public string DungeonName { get; set; } = "Realm";

	public DateTime DungeonStartTime { get; set; } = DateTime.Now;

	public double ElapsedDungeonSeconds { get; set; }

	public int KillsCount { get; set; }

	public long TotalDungeonDamage { get; set; }

	public double TotalGroupDps { get; set; }

	public EnemyCombatSnapshot ActiveEnemy { get; set; }

	public List<EnemyCombatSnapshot> DefeatedEnemies { get; set; } = new List<EnemyCombatSnapshot>();

	// Every observed encounter, including simultaneous living targets. The compact
	// overlay keeps its existing focus/defeated selection independently.
	public List<EnemyCombatSnapshot> Encounters { get; set; } = new List<EnemyCombatSnapshot>();

	public EnemyCombatSnapshot AllCombinedEnemy { get; set; }

	public int SelectedMonsterMode { get; set; } = -1;

	public int HistoryIndex { get; set; } = -1;

	public int HistoryTotalCount { get; set; }

	public string HistoryTimeAgo { get; set; } = "";

	public List<PlayerParseEntry> Players { get; set; } = new List<PlayerParseEntry>();

	public List<PartyMemberEntry> PartyMembers { get; set; } = new List<PartyMemberEntry>();

	public LocalPlayerInfo LocalPlayer { get; set; } = new LocalPlayerInfo();

	public List<LootDropEntry> LootDrops { get; set; } = new List<LootDropEntry>();

	public List<LootBagDrop> LootBags { get; set; } = new List<LootBagDrop>();

	public List<AbilityLogEntry> AbilityLog { get; set; } = new List<AbilityLogEntry>();

	public DateTime Timestamp { get; set; } = DateTime.Now;

	public EnemyCombatSnapshot GetCurrentDisplayEnemy()
	{
		if (SelectedMonsterMode == -2)
		{
			return AllCombinedEnemy ?? ActiveEnemy ?? new EnemyCombatSnapshot
			{
				Name = "All Monsters (Total)"
			};
		}
		if (SelectedMonsterMode >= 0 && SelectedMonsterMode < DefeatedEnemies.Count)
		{
			return DefeatedEnemies[SelectedMonsterMode];
		}
		// Live mode follows an active target while one is alive. Once the target
		// dies, keep the most recently defeated encounter visible so the last kill
		// remains inspectable until the next target is observed.
		if (ActiveEnemy != null && !ActiveEnemy.IsDead)
		{
			return ActiveEnemy;
		}
		return (DefeatedEnemies ?? new List<EnemyCombatSnapshot>()).FirstOrDefault(x => x != null)
			?? ActiveEnemy ?? AllCombinedEnemy ?? new EnemyCombatSnapshot
		{
			Name = "Waiting for Target..."
		};
	}
}
