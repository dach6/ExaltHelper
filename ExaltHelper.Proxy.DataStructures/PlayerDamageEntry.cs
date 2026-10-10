namespace ExaltHelper.Proxy.DataStructures;

internal class PlayerDamageEntry
{
	public int Rank { get; set; }

	public string Name { get; set; }

	public string ClassName { get; set; }

	public long Damage { get; set; }

	public double Dps { get; set; }

	public double Percentage { get; set; }

	public bool IsLocalPlayer { get; set; }

	public int GuardHits { get; set; }

	public long GuardDamage { get; set; }

	public PlayerCombatStatus Status { get; set; }

	public int LastHpPercent { get; set; } = 100;

	public bool IsHeavyGuardShooter
	{
		get
		{
			if (Damage > 0 && GuardDamage > 0)
			{
				return (double)GuardDamage / (double)Damage >= 0.2;
			}
			return false;
		}
	}

	public double GuardDamagePercent
	{
		get
		{
			if (Damage <= 0)
			{
				return 0.0;
			}
			return (double)GuardDamage / (double)Damage * 100.0;
		}
	}
}
