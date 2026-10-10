namespace ExaltHelper.Proxy.Networking.Packets;

internal class StasisPacket : Packet
{
	public int EntityId;

	public byte[] UnknownByteArray;

	public float StasisDuration;

	public override void Read(PacketReader reader)
	{
		EntityId = reader.ReadInt32();
		UnknownByteArray = reader.ReadBytes(12);
		StasisDuration = reader.ReadSingle();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(EntityId);
		writer.Write(UnknownByteArray ?? new byte[12]);
		writer.Write(StasisDuration);
	}
}
