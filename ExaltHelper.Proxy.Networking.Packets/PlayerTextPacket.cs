namespace ExaltHelper.Proxy.Networking.Packets;

internal class PlayerTextPacket : Packet
{
	public string MapName;

	public string ChatText;

	public string Name
	{
		get
		{
			return MapName;
		}
		set
		{
			MapName = value;
		}
	}

	public string GuildName
	{
		get
		{
			return ChatText;
		}
		set
		{
			ChatText = value;
		}
	}

	public override void Read(PacketReader reader)
	{
		MapName = reader.ReadString();
		ChatText = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(MapName);
		writer.Write(ChatText);
	}
}
