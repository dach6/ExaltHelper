using System;
using System.IO;
using System.Net;
using System.Text;

namespace ExaltHelper.Proxy.Networking;

internal class PacketWriter : BinaryWriter
{
	public PacketWriter(MemoryStream stream)
		: base(stream)
	{
	}

	public override void Write(short value)
	{
		base.Write(IPAddress.NetworkToHostOrder(value));
	}

	public override void Write(ushort value)
	{
		base.Write((ushort)IPAddress.HostToNetworkOrder((short)value));
	}

	public override void Write(int value)
	{
		base.Write(IPAddress.NetworkToHostOrder(value));
	}

	public override void Write(uint value)
	{
		base.Write(unchecked((uint)IPAddress.HostToNetworkOrder(unchecked((int)value))));
	}

	public override void Write(float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		Write((short)bytes.Length);
		base.Write(bytes);
	}

	public void WriteUTF(string value)
	{
		Write(value.Length);
		Write(Encoding.UTF8.GetBytes(value));
	}

	public void WriteCompressedInt(int value)
	{
		bool num = value < 0;
		uint num2 = (uint)(num ? (-value) : value);
		byte b = (byte)(num2 & 0x3F);
		if (num)
		{
			b |= 0x40;
		}
		num2 >>= 6;
		bool flag = num2 != 0;
		if (flag)
		{
			b |= 0x80;
		}
		Write(b);
		while (flag)
		{
			b = (byte)(num2 & 0x7F);
			num2 >>= 7;
			flag = num2 != 0;
			if (flag)
			{
				b |= 0x80;
			}
			Write(b);
		}
	}

	public static void WriteBuffer(byte[] buffer, int value)
	{
		byte[] bytes = BitConverter.GetBytes(IPAddress.NetworkToHostOrder(value));
		buffer[0] = bytes[0];
		buffer[1] = bytes[1];
		buffer[2] = bytes[2];
		buffer[3] = bytes[3];
	}
}
