namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class PartyData
{
	public string PartyId;

	public int PartyLeaderId;

	public short PartySize;

	public byte MinLevel;

	public byte MaxLevel;

	public PartyActivity Activity;

	public PartyPrivacy Privacy;

	public byte MemberCount;

	public byte MaxMembers;

	public PartyData(PacketReader reader)
	{
		ReadFromPacket(reader);
	}

	public void ReadFromPacket(PacketReader reader)
	{
		PartyId = reader.ReadString();
		PartyLeaderId = reader.ReadInt32();
		PartySize = reader.ReadInt16();
		MinLevel = reader.ReadByte();
		MaxLevel = reader.ReadByte();
		Activity = (PartyActivity)reader.ReadByte();
		Privacy = (PartyPrivacy)reader.ReadByte();
		MemberCount = reader.ReadByte();
		MaxMembers = reader.ReadByte();
	}

	public void WriteToPacket(PacketWriter writer)
	{
		writer.Write(PartyId);
		writer.Write(PartyLeaderId);
		writer.Write(PartySize);
		writer.Write(MinLevel);
		writer.Write(MaxLevel);
		writer.Write((byte)Activity);
		writer.Write((byte)Privacy);
		writer.Write(MemberCount);
		writer.Write(MaxMembers);
	}

	public override string ToString()
	{
		return $"{{Name: {PartyId}, PartyId: {PartyLeaderId}, PowerLevelMin: {PartySize}, PartySizeCurrent: {MinLevel}, PartySizeMax: {MaxLevel}, Activity: {Activity}, Privacy: {Privacy}, StatsMin: {MemberCount}, ServerIndex: {MaxMembers}}}";
	}
}
