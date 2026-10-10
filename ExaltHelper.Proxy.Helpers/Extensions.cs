using System;
using System.Text;

namespace ExaltHelper.Proxy.Helpers;

public static class Extensions
{
	public static string ToHexString(this byte[] barray, int length = -1)
	{
		if (length == -1)
		{
			length = barray.Length;
		}
		char[] array = new char[length * 2];
		for (int i = 0; i < length; i++)
		{
			byte b = (byte)(barray[i] >> 4);
			array[i * 2] = (char)((b > 9) ? (b + 55) : (b + 48));
			b = (byte)(barray[i] & 0xF);
			array[i * 2 + 1] = (char)((b > 9) ? (b + 55) : (b + 48));
		}
		return new string(array);
	}

	public static byte[] ToByteArray(this string input)
	{
		if (input.Length % 2 != 0)
		{
			throw new ArgumentException("Invalid hex string!");
		}
		byte[] array = new byte[input.Length / 2];
		StringBuilder stringBuilder = new StringBuilder(2);
		for (int i = 0; i < input.Length; i += 2)
		{
			stringBuilder.Append(input[i]).Append(input[i + 1]);
			byte b = Convert.ToByte(stringBuilder.ToString(), 16);
			stringBuilder.Clear();
			array[i / 2] = b;
		}
		return array;
	}
}
