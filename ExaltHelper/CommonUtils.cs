using System;
using System.Security.Cryptography;
using System.Text;

namespace ExaltHelper;

internal class CommonUtils
{
	public static int Clamp(int minimum, int maximum, int value)
	{
		return Math.Max(minimum, Math.Min(maximum, value));
	}

	public static double Clamp(double minimum, double maximum, double value)
	{
		return Math.Max(minimum, Math.Min(maximum, value));
	}

	public static string ToBase64(string text)
	{
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
	}

	public static void LogDebug(string category, string message)
	{
	}

	public static string ToHexString(byte[] bytes)
	{
		char[] array = new char[bytes.Length * 2];
		for (int i = 0; i < bytes.Length; i++)
		{
			int num = bytes[i] >> 4;
			array[i * 2] = (char)(55 + num + ((num - 10 >> 31) & -7));
			num = bytes[i] & 0xF;
			array[i * 2 + 1] = (char)(55 + num + ((num - 10 >> 31) & -7));
		}
		return new string(array);
	}

	public unsafe static byte[] FromHexString(string hex)
	{
		if (hex.Length % 2 != 0)
		{
			throw new ArgumentException("Hex string must have an even number of characters");
		}
		byte[] array = new byte[hex.Length / 2];
		fixed (byte* ptr = array)
		{
			for (int i = 0; i < hex.Length; i += 2)
			{
				ptr[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			}
		}
		return array;
	}

	public static string ComputeMd5(byte[] bytes)
	{
		using MD5 mD = MD5.Create();
		mD.TransformFinalBlock(bytes, 0, bytes.Length);
		return ToHexString(mD.Hash);
	}
}
