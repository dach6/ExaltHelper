using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class GotoAckPacket : Packet
{
	public int _time;

	public WorldPosData _position;

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

	public override void Read(PacketReader reader)
	{
		_time = reader.ReadInt32();
		_position = new WorldPosData(reader);
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_time);
		_position.Write(writer);
	}
}
