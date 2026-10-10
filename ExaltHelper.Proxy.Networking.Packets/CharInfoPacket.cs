namespace ExaltHelper.Proxy.Networking.Packets;

internal class CharInfoPacket : Packet
{
	public string CharacterXml;

	public override void Read(PacketReader reader)
	{
		CharacterXml = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(CharacterXml);
	}
}
