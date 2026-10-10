using System;
using System.Collections.Generic;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class ObjectStatsData : IDataObject, ICloneable
{
	public int ObjectId;

	public WorldPosData Position = new WorldPosData();

	public List<StatData> StatList;

	public IDataObject Read(PacketReader reader)
	{
		ObjectId = reader.ReadCompressedInt();
		Position.Read(reader);
		int num = reader.ReadCompressedInt();
		StatList = new List<StatData>(num);
		for (int i = 0; i < num; i++)
		{
			StatList.Add(new StatData(reader));
		}
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.WriteCompressedInt(ObjectId);
		Position.Write(writer);
		writer.WriteCompressedInt(StatList.Count);
		foreach (StatData item in StatList)
		{
			item.Write(writer);
		}
	}

	public object Clone()
	{
		return new ObjectStatsData
		{
			StatList = new List<StatData>(StatList),
			ObjectId = ObjectId,
			Position = (WorldPosData)Position.Clone()
		};
	}
}
