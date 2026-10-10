using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class UseItemPacket : Packet
{
	public int Time;

	public SlotObjectData Item;

	public WorldPosData ItemUsePos;

	public byte UseType;

	public int UnknownInt;

	public override void Read(PacketReader reader)
	{
		Time = reader.ReadInt32();
		Item = new SlotObjectData(reader);
		ItemUsePos = new WorldPosData(reader);
		UseType = reader.ReadByte();
		UnknownInt = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Time);
		Item.Write(writer);
		ItemUsePos.Write(writer);
		writer.Write(UseType);
		writer.Write(UnknownInt);
	}
}
