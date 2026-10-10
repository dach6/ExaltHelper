using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class InvSwapPacket : Packet
{
	public bool SwapFlag;

	public byte UseType;

	public SlotObjectData SlotObject1;

	public SlotObjectData SlotObject2;

	public long UnknownBool;

	public bool Result
	{
		get
		{
			return SwapFlag;
		}
		set
		{
			SwapFlag = value;
		}
	}

	public byte Code
	{
		get
		{
			return UseType;
		}
		set
		{
			UseType = value;
		}
	}

	public SlotObjectData Slot1
	{
		get
		{
			return SlotObject1;
		}
		set
		{
			SlotObject1 = value;
		}
	}

	public SlotObjectData Slot2
	{
		get
		{
			return SlotObject2;
		}
		set
		{
			SlotObject2 = value;
		}
	}

	public long Timestamp
	{
		get
		{
			return UnknownBool;
		}
		set
		{
			UnknownBool = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		SwapFlag = reader.ReadBoolean();
		UseType = reader.ReadByte();
		SlotObject1 = new SlotObjectData(reader);
		SlotObject2 = new SlotObjectData(reader);
		UnknownBool = reader.ReadInt64();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(SwapFlag);
		writer.Write(UseType);
		SlotObject1.Write(writer);
		SlotObject2.Write(writer);
		writer.Write(UnknownBool);
	}
}
