using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class ServerPlayerShootPacket : Packet
{
	public ushort _bulletId;

	public int _ownerId;

	public byte _bulletType;

	public WorldPosData _startingPos;

	public float _angle;

	public short _angleRaw;

	public byte _damage;

	public float _unknownFloat;

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

	public int OwnerId
	{
		get
		{
			return _ownerId;
		}
		set
		{
			_ownerId = value;
		}
	}

	public byte BulletType
	{
		get
		{
			return _bulletType;
		}
		set
		{
			_bulletType = value;
		}
	}

	public WorldPosData StartingPos
	{
		get
		{
			return _startingPos;
		}
		set
		{
			_startingPos = value;
		}
	}

	public float Angle
	{
		get
		{
			return _angle;
		}
		set
		{
			_angle = value;
		}
	}

	public short Damage
	{
		get
		{
			return _angleRaw;
		}
		set
		{
			_angleRaw = value;
		}
	}

	public byte NumShots
	{
		get
		{
			return _damage;
		}
		set
		{
			_damage = value;
		}
	}

	public float AngleInc
	{
		get
		{
			return _unknownFloat;
		}
		set
		{
			_unknownFloat = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		_bulletId = reader.ReadUInt16();
		_ownerId = reader.ReadInt32();
		_bulletType = reader.ReadByte();
		_startingPos = new WorldPosData(reader);
		_angle = reader.ReadSingle();
		_angleRaw = reader.ReadInt16();
		if (reader.BaseStream.Position < reader.BaseStream.Length)
		{
			_damage = reader.ReadByte();
			_unknownFloat = reader.ReadSingle();
		}
		else
		{
			_damage = 1;
			_unknownFloat = 0f;
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_bulletId);
		writer.Write(_ownerId);
		writer.Write(_bulletType);
		_startingPos.Write(writer);
		writer.Write(_angle);
		writer.Write(_angleRaw);
		if (_damage != 1)
		{
			writer.Write(_damage);
			writer.Write(_unknownFloat);
		}
	}
}
