using System;

namespace ExaltHelper.Proxy.Networking.Packets;

internal enum PartyActionType : byte
{
	None = 0,
	Failed = 1,
	Kicked = 2,
	KickNotFound = 3,
	PromotedToLeader = 4,
	PromoteNotFound = 5,
	LeftParty = 6
}

internal class PartyActionResultPacket : Packet
{
	public short PlayerId { get; set; }

	public PartyActionType Action { get; set; }

	public override void Read(PacketReader reader)
	{
		PlayerId = reader.ReadInt16();
		Action = (PartyActionType)reader.ReadByte();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(PlayerId);
		writer.Write((byte)Action);
	}
}
