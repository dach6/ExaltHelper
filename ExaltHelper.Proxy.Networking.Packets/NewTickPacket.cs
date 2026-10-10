using System.Collections.Generic;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class NewTickPacket : Packet
{
	public int TickId;

	public int TickTime;

	public int ServerRealTimeMS;

	public ushort ServerLastRTTMS;

	public List<ObjectStatsData> Statuses;

	public override void Read(PacketReader reader)
	{
		TickId = reader.ReadInt32();
		TickTime = reader.ReadInt32();
		ServerRealTimeMS = reader.ReadInt32();
		ServerLastRTTMS = reader.ReadUInt16();
		int num = reader.ReadInt16();
		Statuses = new List<ObjectStatsData>();
		for (int i = 0; i < num; i++)
		{
			Statuses.Add((ObjectStatsData)new ObjectStatsData().Read(reader));
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(TickId);
		writer.Write(TickTime);
		writer.Write(ServerRealTimeMS);
		writer.Write(ServerLastRTTMS);
		writer.Write((short)Statuses.Count);
		foreach (ObjectStatsData item in Statuses)
		{
			item.Write(writer);
		}
	}
}
