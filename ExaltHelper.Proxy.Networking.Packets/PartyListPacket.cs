using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class PartyListPacket : Packet
{
	public byte PartyCount;

	public PartyData[] Parties;

	public override void Read(PacketReader reader)
	{
		PartyCount = reader.ReadByte();
		short num = reader.ReadInt16();
		Parties = new PartyData[num];
		for (int i = 0; i < num; i++)
		{
			Parties[i] = new PartyData(reader);
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(PartyCount);
		writer.Write((short)Parties.Length);
		PartyData[] array = Parties;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].WriteToPacket(writer);
		}
	}
}
