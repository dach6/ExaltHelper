using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class OtherHitPacket : Packet
{
	public int _time;

	public sbyte _ability;

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

	public sbyte Ability
	{
		get
		{
			return _ability;
		}
		set
		{
			_ability = value;
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
		_ability = reader.ReadSByte();
		_position = new WorldPosData(reader);
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_time);
		writer.Write(_ability);
		_position.Write(writer);
	}
}
