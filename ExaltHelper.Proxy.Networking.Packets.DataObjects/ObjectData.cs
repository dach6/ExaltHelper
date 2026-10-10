using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class ObjectData : IDataObject, ICloneable
{
	public ushort ObjectType;

	public ObjectStatsData Stats = new ObjectStatsData();

	public IDataObject Read(PacketReader reader)
	{
		ObjectType = reader.ReadUInt16();
		Stats.Read(reader);
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(ObjectType);
		Stats.Write(writer);
	}

	public object Clone()
	{
		return new ObjectData
		{
			ObjectType = ObjectType,
			Stats = (ObjectStatsData)Stats.Clone()
		};
	}
}
