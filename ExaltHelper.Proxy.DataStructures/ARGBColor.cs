using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using ExaltHelper.Proxy.Networking;

namespace ExaltHelper.Proxy.DataStructures;

[StructLayout(LayoutKind.Explicit)]
internal struct ARGBColor
{
	[FieldOffset(3)]
	public byte A;

	[FieldOffset(2)]
	public byte R;

	[FieldOffset(1)]
	public byte G;

	[FieldOffset(0)]
	public byte B;

	[FieldOffset(0)]
	public uint Value;

	public ARGBColor(uint packedArgb)
	{
		A = 0;
		R = 0;
		G = 0;
		B = 0;
		Value = packedArgb;
	}

	public ARGBColor(byte alpha, byte red, byte green, byte blue)
	{
		Value = 0u;
		A = alpha;
		R = red;
		G = green;
		B = blue;
	}

	public static ARGBColor ReadFromPacket(PacketReader reader)
	{
		return new ARGBColor
		{
			A = reader.ReadByte(),
			R = reader.ReadByte(),
			G = reader.ReadByte(),
			B = reader.ReadByte()
		};
	}

	public void WriteToPacket(PacketWriter writer)
	{
		writer.Write(A);
		writer.Write(R);
		writer.Write(G);
		writer.Write(B);
	}

	public void WriteToBinary(BinaryWriter writer)
	{
		writer.Write(A);
		writer.Write(R);
		writer.Write(G);
		writer.Write(B);
	}

	public override string ToString()
	{
		return Color.FromArgb(A, R, G, B).Name;
	}
}
