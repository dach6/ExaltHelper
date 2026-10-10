namespace ExaltHelper.Proxy.Networking.Packets;

internal class PetActionPacket : Packet
{
	public const int ActionFeed = 1;

	public const int ActionFuse = 2;

	public const int ActionUpgrade = 3;

	public byte PetActionType;

	public int PetInstanceId;

	public override void Read(PacketReader reader)
	{
		PetActionType = reader.ReadByte();
		PetInstanceId = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(PetActionType);
		writer.Write(PetInstanceId);
	}
}
