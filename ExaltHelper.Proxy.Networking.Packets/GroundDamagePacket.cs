using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class GroundDamagePacket : Packet
{
	public int Time;

	public WorldPosData Position;

	public override void Read(PacketReader reader)
	{
		Time = reader.ReadInt32();
		Position = new WorldPosData(reader);
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Time);
		Position.Write(writer);
	}
}
