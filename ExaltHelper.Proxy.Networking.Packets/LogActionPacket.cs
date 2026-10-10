namespace ExaltHelper.Proxy.Networking.Packets;

internal class LogActionPacket : Packet
{
	public string Message;

	public byte UnknownShort1;

	public override void Read(PacketReader reader)
	{
		Message = reader.ReadString();
		UnknownShort1 = reader.ReadByte();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Message);
		writer.Write(UnknownShort1);
	}
}
