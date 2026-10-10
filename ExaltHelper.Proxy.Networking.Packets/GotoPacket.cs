namespace ExaltHelper.Proxy.Networking.Packets;

internal class GotoPacket : Packet
{
	public int _objectId;

	public int _charId;

	public string _xml;

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

	public int CharId
	{
		get
		{
			return _charId;
		}
		set
		{
			_charId = value;
		}
	}

	public string Xml
	{
		get
		{
			return _xml;
		}
		set
		{
			_xml = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		_objectId = reader.ReadInt32();
		_charId = reader.ReadInt32();
		_xml = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_objectId);
		writer.Write(_charId);
		writer.Write(_xml);
	}
}
