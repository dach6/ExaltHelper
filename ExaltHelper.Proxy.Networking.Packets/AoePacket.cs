using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class AoePacket : Packet
{
	public WorldPosData _startingPos;

	public float Radius;

	public ushort _angleRaw;

	public ConditionEffectIndex _effects;

	public float Duration;

	public ushort OrigType;

	public int Color;

	public bool _armorPiercing;

	public override void Read(PacketReader reader)
	{
		_startingPos = new WorldPosData(reader);
		Radius = reader.ReadSingle();
		_angleRaw = reader.ReadUInt16();
		_effects = (ConditionEffectIndex)reader.ReadByte();
		Duration = reader.ReadSingle();
		OrigType = reader.ReadUInt16();
		Color = reader.ReadInt32();
		_armorPiercing = reader.ReadBoolean();
	}

	public override void Write(PacketWriter writer)
	{
		_startingPos.Write(writer);
		writer.Write(Radius);
		writer.Write(_angleRaw);
		writer.Write((byte)_effects);
		writer.Write(Duration);
		writer.Write(OrigType);
		writer.Write(Color);
		writer.Write(_armorPiercing);
	}
}
