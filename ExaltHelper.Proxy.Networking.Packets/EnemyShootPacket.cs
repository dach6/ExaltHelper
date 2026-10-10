namespace ExaltHelper.Proxy.Networking.Packets;

internal class EnemyShootPacket : Packet
{
	private const int PropertyKill = 1;

	private const int PropertyArmorPiercing = 2;

	private const int PropertyUnknown = 4;

	public int OwnerId;

	public byte[] _effects;

	public ushort _damageAmount;

	public byte _damageProperties;

	public bool _kill;

	public bool _armorPiercing;

	public bool _unknownBool;

	public ushort _bulletId;

	public int ObjectId;

	public int TargetId => OwnerId;

	public byte[] Effects => _effects;

	public ushort DamageAmount => _damageAmount;

	public byte DamageProperties => _damageProperties;

	public bool Kill => _kill;

	public bool ArmorPiercing => _armorPiercing;

	public ushort BulletId => _bulletId;

	public int AttackerId => ObjectId;

	public override void Read(PacketReader reader)
	{
		OwnerId = reader.ReadInt32();
		_effects = new byte[reader.ReadByte()];
		for (int i = 0; i < _effects.Length; i++)
		{
			_effects[i] = reader.ReadByte();
		}
		_damageAmount = reader.ReadUInt16();
		_damageProperties = reader.ReadByte();
		_bulletId = reader.ReadUInt16();
		ObjectId = reader.ReadInt32();
		_kill = (_damageProperties & 1) != 0;
		_armorPiercing = (_damageProperties & 2) != 0;
		_unknownBool = (_damageProperties & 4) != 0;
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(OwnerId);
		writer.Write((byte)_effects.Length);
		byte[] array = _effects;
		foreach (byte value in array)
		{
			writer.Write(value);
		}
		writer.Write(_damageAmount);
		writer.Write(_damageProperties);
		writer.Write(_bulletId);
		writer.Write(ObjectId);
	}
}
