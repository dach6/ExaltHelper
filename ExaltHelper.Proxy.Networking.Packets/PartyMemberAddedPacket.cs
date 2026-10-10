using System;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class PartyMemberAddedPacket : Packet
{
	public short PlayerId { get; set; }

	public string Name { get; set; } = "";

	public short ClassId { get; set; }

	public short SkinId { get; set; }

	public override void Read(PacketReader reader)
	{
		PlayerId = reader.ReadInt16();
		Name = reader.ReadString();
		ClassId = reader.ReadInt16();
		SkinId = reader.ReadInt16();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(PlayerId);
		writer.Write(Name ?? "");
		writer.Write(ClassId);
		writer.Write(SkinId);
	}
}
