using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class PartyActionPacket : Packet
{
	public string PartyLeader;

	public short PartyId;

	public byte ActionId;

	public byte PartyActionType;

	public byte MaxPartyMembers;

	public PartyPrivacy PartyPrivacy;

	public byte PartyActivity;

	public override void Read(PacketReader reader)
	{
		PartyLeader = reader.ReadString();
		PartyId = reader.ReadInt16();
		ActionId = reader.ReadByte();
		PartyActionType = reader.ReadByte();
		MaxPartyMembers = reader.ReadByte();
		PartyPrivacy = (PartyPrivacy)reader.ReadByte();
		PartyActivity = reader.ReadByte();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(PartyLeader);
		writer.Write(PartyId);
		writer.Write(ActionId);
		writer.Write(PartyActionType);
		writer.Write(MaxPartyMembers);
		writer.Write((byte)PartyPrivacy);
		writer.Write(PartyActivity);
	}
}
