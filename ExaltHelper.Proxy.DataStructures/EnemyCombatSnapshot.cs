using System.Collections.Generic;

namespace ExaltHelper.Proxy.DataStructures;

internal class EnemyCombatSnapshot
{
	public int EntityId { get; set; }

	public string Name { get; set; } = "Unknown";

	public int MaxHp { get; set; }

	public int CurrentHp { get; set; }

	public bool IsDead { get; set; }

	public bool IsBoss { get; set; }

	public double FightDurationSeconds { get; set; }

	public long TotalDamage { get; set; }

	public double Dps { get; set; }

	public List<PlayerDamageEntry> Damagers { get; set; } = new List<PlayerDamageEntry>();

	public int TotalGuardHits { get; set; }

	public long TotalGuardDamage { get; set; }

	public bool IsGuarding { get; set; }

	public int Condition1 { get; set; }

	public int Condition2 { get; set; }

	public bool IsCursed => (Condition2 & 0x40) != 0;

	public bool IsArmorBroken => (Condition1 & 0x4000000) != 0;

	public bool IsDazed => (Condition1 & 0x20) != 0;

	public bool IsStunned => (Condition1 & 0x40) != 0;

	public bool IsParalyzed => (Condition1 & 0x2000) != 0;

	public bool IsExposed => (Condition2 & 0x20000) != 0;

	public bool IsInvulnerable => (Condition1 & 0x1000000) != 0;
}
