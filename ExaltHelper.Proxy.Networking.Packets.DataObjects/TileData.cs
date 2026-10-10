using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class TileData : IDataObject, ICloneable
{
	public short X;

	public short Y;

	public ushort TileType;

	public TileData()
	{
	}

	public TileData(short x, short y, ushort tileType)
	{
		X = x;
		Y = y;
		TileType = tileType;
	}

	public IDataObject Read(PacketReader reader)
	{
		X = reader.ReadInt16();
		Y = reader.ReadInt16();
		TileType = reader.ReadUInt16();
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(X);
		writer.Write(Y);
		writer.Write(TileType);
	}

	public object Clone()
	{
		return new TileData(X, Y, TileType);
	}

	public override string ToString()
	{
		return $"{{ X={X}, Y={Y}, Type={TileType} }}";
	}
}
