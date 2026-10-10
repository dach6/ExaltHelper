namespace ExaltHelper.Proxy.Networking.Packets;

internal class FailurePacket : Packet
{
	public int ErrorId;

	public string ErrorMessage;

	public override void Read(PacketReader reader)
	{
		ErrorId = reader.ReadInt32();
		ErrorMessage = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(ErrorId);
		writer.Write(ErrorMessage);
	}
}
