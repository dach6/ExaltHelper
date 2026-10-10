namespace ExaltHelper.Proxy.Networking.Packets;

internal class ShootAckPacket : Packet
{
	public ushort _bulletId;

	public int _objectId;

	public ushort BulletId
	{
		get
		{
			return _bulletId;
		}
		set
		{
			_bulletId = value;
		}
	}

	public int ObjectId
	{
		get
		{
			return _objectId;
		}
		set
		{
			_objectId = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		_bulletId = reader.ReadUInt16();
		_objectId = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_bulletId);
		writer.Write(_objectId);
	}
}
