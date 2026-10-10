using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class PlayerShootServerPacket : Packet
{
	// Incoming packets may end after any of the optional fields. Appending a
	// missing count as zero can make the client render no ability projectiles.
	private int _optionalFieldBytes = 6;

	public short BulletId { get; set; }

	public int OwnerId { get; set; }

	public int ContainerType { get; set; }

	public WorldPosData StartingPos { get; set; }

	public float Angle { get; set; }

	public short Damage { get; set; }

	public int SummonerId { get; set; }

	public byte BulletType { get; set; }

	public byte BulletCount { get; set; } = 1;

	public float AnglesBetweenBullets { get; set; }

	public override void Read(PacketReader reader)
	{
		BulletId = reader.ReadInt16();
		OwnerId = reader.ReadInt32();
		ContainerType = reader.ReadInt32();
		StartingPos = new WorldPosData(reader);
		Angle = reader.ReadSingle();
		Damage = reader.ReadInt16();
		SummonerId = reader.ReadInt32();
		_optionalFieldBytes = 0;
		BulletType = 0;
		BulletCount = 1;
		AnglesBetweenBullets = 0f;
		if (reader.BaseStream.Position >= reader.BaseStream.Length)
		{
			return;
		}
		BulletType = reader.ReadByte();
		_optionalFieldBytes = 1;
		if (reader.BaseStream.Position < reader.BaseStream.Length)
		{
			BulletCount = reader.ReadByte();
			_optionalFieldBytes = 2;
			if (reader.BaseStream.Position < reader.BaseStream.Length)
			{
				AnglesBetweenBullets = reader.ReadSingle();
				_optionalFieldBytes = 6;
			}
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(BulletId);
		writer.Write(OwnerId);
		writer.Write(ContainerType);
		if (StartingPos != null)
		{
			StartingPos.Write(writer);
		}
		else
		{
			writer.Write(0f);
			writer.Write(0f);
		}
		writer.Write(Angle);
		writer.Write(Damage);
		writer.Write(SummonerId);
		if (_optionalFieldBytes >= 1) writer.Write(BulletType);
		if (_optionalFieldBytes >= 2) writer.Write(BulletCount);
		if (_optionalFieldBytes >= 6) writer.Write(AnglesBetweenBullets);
	}
}
