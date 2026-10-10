namespace ExaltHelper.Proxy.Networking.Packets;

internal class ChooseNamePacket : Packet
{
	public string CharacterName;

	public override void Read(PacketReader reader)
	{
		CharacterName = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(CharacterName);
	}
}
