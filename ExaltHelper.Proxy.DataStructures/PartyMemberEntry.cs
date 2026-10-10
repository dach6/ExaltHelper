using System;

namespace ExaltHelper.Proxy.DataStructures;

internal class PartyMemberEntry
{
	public string Name { get; set; } = "";

	public string ClassName { get; set; } = "Unknown";

	public int CurrentHp { get; set; }

	public int MaxHp { get; set; }

	public bool IsInDungeon { get; set; }

	public bool IsLocalPlayer { get; set; }

	public short PlayerId { get; set; }

	public short ObjectId { get; set; }

	public string Status { get; set; } = "";

	public int MaxedCount { get; set; } = -1;

	public double HpPercentage
	{
		get
		{
			if (MaxHp <= 0)
			{
				return 100.0;
			}
			return Math.Round((double)CurrentHp / (double)MaxHp * 100.0, 1);
		}
	}
}
