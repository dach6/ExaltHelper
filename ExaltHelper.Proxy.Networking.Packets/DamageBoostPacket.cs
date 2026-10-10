namespace ExaltHelper.Proxy.Networking.Packets;

// Incoming combat boost message (148). Its values are intentionally left opaque
// until a real cloak trace establishes their meaning. Forward every byte intact.
internal class DamageBoostPacket : Packet
{
	public byte[] Payload = new byte[0];

	public override void Read(PacketReader reader)
	{
		Payload = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
	}

	public override void Write(PacketWriter writer) => writer.Write(Payload);
}
