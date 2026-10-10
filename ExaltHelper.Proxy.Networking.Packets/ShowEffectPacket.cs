using System.IO;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class ShowEffectPacket : Packet
{
	private const int EffectBit1 = 1;

	private const int EffectBit2 = 2;

	private const int EffectBit3 = 4;

	private const int EffectBit4 = 8;

	private const int EffectBit5 = 16;

	private const int EffectBit6 = 6;

	private const int EffectBit7 = 24;

	private const int EffectBit8 = 32;

	private const int EffectBit9 = 64;

	private const int EffectBit10 = 128;

	private static readonly ARGBColor DefaultEffectColor = new ARGBColor(uint.MaxValue);

	public byte EffectFlags;

	public EffectType EffectType;

	public int OwnerId;

	public WorldPosData TargetPos = WorldPosData.Zero;

	public WorldPosData SecondaryPos = WorldPosData.Zero;

	public ARGBColor Color;

	public float Duration;

	public byte ColorByte;

	public override void Read(PacketReader reader)
	{
		EffectFlags = reader.ReadByte();
		EffectType = (EffectType)EffectFlags;
		byte b = reader.ReadByte();
		OwnerId = (((b & 0x40) != 0) ? reader.ReadCompressedInt() : 0);
		TargetPos.X = (((b & 2) != 0) ? reader.ReadSingle() : 0f);
		TargetPos.Y = (((b & 4) != 0) ? reader.ReadSingle() : 0f);
		SecondaryPos.X = (((b & 8) != 0) ? reader.ReadSingle() : 0f);
		SecondaryPos.Y = (((b & 0x10) != 0) ? reader.ReadSingle() : 0f);
		Color = (((b & 1) != 0) ? ARGBColor.ReadFromPacket(reader) : DefaultEffectColor);
		Duration = (((b & 0x20) != 0) ? reader.ReadSingle() : 1f);
		ColorByte = (byte)(((b & 0x80) != 0) ? reader.ReadByte() : 100);
	}

	public override void Write(PacketWriter writer)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using PacketWriter packetWriter = new PacketWriter(memoryStream);
		byte b = 0;
		if (OwnerId != 0)
		{
			b |= 0x40;
			packetWriter.WriteCompressedInt(OwnerId);
		}
		if (TargetPos.X != 0.0)
		{
			b |= 2;
			packetWriter.Write((float)TargetPos.X);
		}
		if (TargetPos.Y != 0.0)
		{
			b |= 4;
			packetWriter.Write((float)TargetPos.Y);
		}
		if (SecondaryPos.X != 0.0)
		{
			b |= 8;
			packetWriter.Write((float)SecondaryPos.X);
		}
		if (SecondaryPos.Y != 0.0)
		{
			b |= 0x10;
			packetWriter.Write((float)SecondaryPos.Y);
		}
		if (Color.A != DefaultEffectColor.A || Color.R != DefaultEffectColor.R || Color.G != DefaultEffectColor.G || Color.B != DefaultEffectColor.B)
		{
			b |= 1;
			Color.WriteToPacket(packetWriter);
		}
		if (Duration != 1f)
		{
			b |= 0x20;
			packetWriter.Write(Duration);
		}
		if (ColorByte != 100)
		{
			b |= 0x80;
			packetWriter.Write(ColorByte);
		}
		writer.Write(EffectFlags);
		writer.Write(b);
		memoryStream.WriteTo(writer.BaseStream);
	}
}
