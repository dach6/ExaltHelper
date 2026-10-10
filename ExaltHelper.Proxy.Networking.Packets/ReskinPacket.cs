namespace ExaltHelper.Proxy.Networking.Packets;

internal class ReskinPacket : Packet
{
	public int _skinId;

	public int SkinId
	{
		get
		{
			return _skinId;
		}
		set
		{
			_skinId = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		_skinId = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_skinId);
	}
}
