using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class PlayerShootPacket : Packet
{
	public int Time;

	public ushort BulletId;

	public short WeaponId;

	public sbyte ProjectileTypeId;

	public WorldPosData StartingPosition;

	public float Angle;

	public byte BurstFlag;

	public sbyte PatternIndex;

	public sbyte AttackType;

	public WorldPosData PlayerPosition;

	public override void Read(PacketReader reader)
	{
		Time = reader.ReadInt32();
		BulletId = reader.ReadUInt16();
		WeaponId = reader.ReadInt16();
		ProjectileTypeId = reader.ReadSByte();
		StartingPosition = new WorldPosData(reader);
		Angle = reader.ReadSingle();
		BurstFlag = reader.ReadByte();
		PatternIndex = reader.ReadSByte();
		AttackType = reader.ReadSByte();
		PlayerPosition = new WorldPosData(reader);
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Time);
		writer.Write(BulletId);
		writer.Write(WeaponId);
		writer.Write(ProjectileTypeId);
		StartingPosition.Write(writer);
		writer.Write(Angle);
		writer.Write(BurstFlag);
		writer.Write(PatternIndex);
		writer.Write(AttackType);
		PlayerPosition.Write(writer);
	}
}
