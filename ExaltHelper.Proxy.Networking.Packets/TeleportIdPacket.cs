namespace ExaltHelper.Proxy.Networking.Packets;

internal class TeleportIdPacket : Packet
{
	public int _serial;

	public int _time;

	public int Serial
	{
		get
		{
			return _serial;
		}
		set
		{
			_serial = value;
		}
	}

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

	public override void Read(PacketReader reader)
	{
		_serial = reader.ReadInt32();
		_time = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_serial);
		writer.Write(_time);
	}
}
