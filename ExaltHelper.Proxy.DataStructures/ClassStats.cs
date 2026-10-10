using System.Collections.Generic;

namespace ExaltHelper.Proxy.DataStructures;

public class ClassStats
{
	public static Dictionary<ushort, ClassStats> Map = new Dictionary<ushort, ClassStats>();

	public int LifeMax;

	public int ManaMax;

	public int AttackMax;

	public int DefenseMax;

	public int SpeedMax;

	public int DexterityMax;

	public int VitalityMax;

	public int WisdomMax;
}
