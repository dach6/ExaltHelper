using System;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class GenericFailurePacket : Packet
{
	public string TextValue;

	public string Text
	{
		get
		{
			return TextValue;
		}
		set
		{
			TextValue = value;
		}
	}

	public bool IsCommand(string command, out string[] arguments)
	{
		string[] array = TextValue.Trim().Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			arguments = null;
			return false;
		}
		if (!array[0].StartsWith("/") && array[0] != "/")
		{
			arguments = null;
			return false;
		}
		if (array[0].ToLower().Substring(1) != command)
		{
			arguments = null;
			return false;
		}
		Send = false;
		if (array.Length == 1)
		{
			arguments = new string[0];
			return true;
		}
		arguments = new string[array.Length - 1];
		Array.Copy(array, 1, arguments, 0, arguments.Length);
		return true;
	}

	public override void Read(PacketReader reader)
	{
		TextValue = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(TextValue);
	}
}
