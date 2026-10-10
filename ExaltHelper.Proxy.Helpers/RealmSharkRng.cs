namespace ExaltHelper.Proxy.Helpers;

public class RealmSharkRng
{
	private long seed;

	public RealmSharkRng(long seed)
	{
		this.seed = seed;
	}

	public long Next()
	{
		long num = (seed >> 16) * 16807;
		long num2 = seed & 0xFFFF;
		num2 *= 16807;
		long num3 = num >> 15;
		long num4 = (num & 0x7FFF) << 16;
		long num5 = num2 + num4 + num3;
		long result = (uint)((int)num5 - int.MaxValue);
		if (num5 <= int.MaxValue)
		{
			result = (uint)num5;
		}
		seed = result;
		return result;
	}
}
