using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class MovePacket : Packet
{
	public int TickId;

	public int Time;

	public MoveRecord[] Positions;

	public override void Read(PacketReader reader)
	{
		TickId = reader.ReadInt32();
		Time = reader.ReadInt32();
		Positions = new MoveRecord[reader.ReadInt16()];
		for (int i = 0; i < Positions.Length; i++)
		{
			Positions[i] = new MoveRecord(reader);
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(TickId);
		writer.Write(Time);
		writer.Write((short)Positions.Length);
		MoveRecord[] array = Positions;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Write(writer);
		}
	}
}
