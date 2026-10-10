using System;
using System.IO;
using System.Net;
using System.Text;

namespace ExaltHelper.Proxy.Networking;

internal class PacketReader : BinaryReader
{
	public PacketReader(MemoryStream stream)
		: base(stream, Encoding.UTF8)
	{
	}

	public override short ReadInt16()
	{
		return IPAddress.NetworkToHostOrder(base.ReadInt16());
	}

	public override ushort ReadUInt16()
	{
		return (ushort)IPAddress.NetworkToHostOrder((short)base.ReadUInt16());
	}

	public override int ReadInt32()
	{
		return IPAddress.NetworkToHostOrder(base.ReadInt32());
	}

	public override uint ReadUInt32()
	{
		return unchecked((uint)IPAddress.NetworkToHostOrder(unchecked((int)base.ReadUInt32())));
	}

	public override float ReadSingle()
	{
		byte[] array = base.ReadBytes(4);
		Array.Reverse(array);
		return BitConverter.ToSingle(array, 0);
	}

	public override string ReadString()
	{
		return Encoding.UTF8.GetString(ReadBytes(ReadInt16()));
	}

	public string ReadUTF()
	{
		return Encoding.UTF8.GetString(ReadBytes(ReadInt32()));
	}

	public int ReadCompressedInt()
	{
		byte b = base.ReadByte();
		bool flag = (b & 0x40) != 0;
		byte b2 = 6;
		int num = b & 0x3F;
		while ((b & 0x80) != 0)
		{
			b = base.ReadByte();
			num |= (b & 0x7F) << (int)b2;
			b2 += 7;
		}
		if (flag)
		{
			num = -num;
		}
		return num;
	}
}
