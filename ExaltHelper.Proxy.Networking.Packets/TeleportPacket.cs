namespace ExaltHelper.Proxy.Networking.Packets;

internal class TeleportPacket : Packet
{
	public int ObjectId;

	public string PlayerName;

	public override void Read(PacketReader reader)
	{
		ObjectId = reader.ReadInt32();
		PlayerName = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(ObjectId);
		writer.Write(PlayerName);
	}
}
