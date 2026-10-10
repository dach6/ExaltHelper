namespace ExaltHelper.Proxy.Networking.Packets;

internal class DamagePacket : Packet
{
	public int _objectId;

	public int[] _list;

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

	public int[] List
	{
		get
		{
			return _list;
		}
		set
		{
			_list = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		_objectId = reader.ReadInt32();
		int num = reader.ReadCompressedInt();
		_list = new int[num];
		for (int i = 0; i < num; i++)
		{
			_list[i] = reader.ReadCompressedInt();
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(_objectId);
		int[] array = _list;
		writer.WriteCompressedInt((array != null) ? array.Length : 0);
		if (_list != null)
		{
			int[] array2 = _list;
			foreach (int entry in array2)
			{
				writer.WriteCompressedInt(entry);
			}
		}
	}
}
