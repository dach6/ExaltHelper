namespace ExaltHelper.Proxy.Networking.Packets;

internal class EnemyHitPacket : Packet
{
	public int Time;

	public ushort BulletId;

	public int TargetId;

	public int OwnerId;

	public bool _kill;

	public int _ownerId;

	public override void Read(PacketReader reader)
	{
		Time = reader.ReadInt32();
		BulletId = reader.ReadUInt16();
		TargetId = reader.ReadInt32();
		OwnerId = reader.ReadInt32();
		_kill = reader.ReadBoolean();
		_ownerId = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Time);
		writer.Write(BulletId);
		writer.Write(TargetId);
		writer.Write(OwnerId);
		writer.Write(_kill);
		writer.Write(_ownerId);
	}
}
