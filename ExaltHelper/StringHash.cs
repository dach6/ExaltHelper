using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExaltHelper;

[CompilerGenerated]
internal sealed class StringHash
{
	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 112)]
	internal struct HashStruct4
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 12)]
	internal struct HashStruct1
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 40)]
	internal struct HashStruct3
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
	internal struct HashStruct2
	{
	}

	internal static readonly HashStruct1 HashInstance1;

	internal static readonly HashStruct3 HashInstance2;

	internal static readonly HashStruct4 HashInstance3;

	internal static readonly HashStruct2 HashInstance4;

	internal static uint ComputeStringHash(string text)
	{
		uint num = 0u;
		if (text != null)
		{
			num = 2166136261u;
			for (int i = 0; i < text.Length; i++)
			{
				num = (text[i] ^ num) * 16777619;
			}
		}
		return num;
	}
}
