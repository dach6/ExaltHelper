namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class MoveRecord : WorldPosData
{
	public int Time;

	public MoveRecord(int time, double x, double y)
	{
		Time = time;
		X = x;
		Y = y;
	}

	public MoveRecord(PacketReader time)
	{
		Time = time.ReadInt32();
		base.Read(time);
	}

	public WorldPosData GetWorldPos()
	{
		return new WorldPosData(X, Y);
	}

	public override IDataObject Read(PacketReader reader)
	{
		Time = reader.ReadInt32();
		base.Read(reader);
		return this;
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Time);
		base.Write(writer);
	}

	public override object Clone()
	{
		return new MoveRecord(Time, X, Y);
	}

	public override string ToString()
	{
		return "{ Time=" + Time + ", X=" + X + ", Y=" + Y + " }";
	}
}
