using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class InvDropPacket : Packet
{
	public int _time;

	public WorldPosData _position;

	public SlotObjectData _slotObject1;

	public SlotObjectData _slotObject2;

	public int Time
	{
		get
		{
			return _time;
		}
		set
		{
			_time = value;
		}
	}

	public WorldPosData Position
	{
		get
		{
			return _position;
		}
		set
		{
			_position = value;
		}
	}

	public SlotObjectData SlotObject1
	{
		get
		{
			return _slotObject1;
		}
		set
		{
			_slotObject1 = value;
		}
	}

	public SlotObjectData SlotObject2
	{
		get
		{
			return _slotObject2;
		}
		set
		{
			_slotObject2 = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		_time = reader.ReadInt32();
		_position = new WorldPosData(reader);
		_slotObject1 = new SlotObjectData(reader);
		_slotObject2 = new SlotObjectData(reader);
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_time);
		_position.Write(writer);
		_slotObject1.Write(writer);
		_slotObject2.Write(writer);
	}
}
