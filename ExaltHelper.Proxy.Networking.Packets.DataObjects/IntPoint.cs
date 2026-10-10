using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class IntPoint : IDataObject, ICloneable
{
	public int X;

	public int Y;

	public byte[] ExtraData = new byte[0];

	public IDataObject Read(PacketReader reader)
	{
		X = reader.ReadInt32();
		Y = reader.ReadInt32();
		ExtraData = new byte[X * Y * 4];
		ExtraData = reader.ReadBytes(ExtraData.Length);
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(X);
		writer.Write(Y);
		writer.Write(ExtraData);
	}

	public object Clone()
	{
		byte[] destinationArray = new byte[X * Y * 4];
		Array.Copy(ExtraData, destinationArray, ExtraData.Length);
		return new IntPoint
		{
			X = X,
			Y = Y,
			ExtraData = destinationArray
		};
	}

	public override string ToString()
	{
		return "{ Width=" + X + ", Height=" + Y + " }";
	}
}
