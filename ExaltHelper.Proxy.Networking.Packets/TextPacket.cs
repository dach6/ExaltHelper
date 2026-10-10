namespace ExaltHelper.Proxy.Networking.Packets;

internal class TextPacket : Packet
{
	public string Name;

	public int ObjectId;

	public short NumStars;

	public byte BubbleTime;

	public string Recipient;

	public string Text;

	public string CleanText;

	public bool IsRecipientIgnored;

	public int StarsRequired;

	public override void Read(PacketReader reader)
	{
		Name = reader.ReadString();
		ObjectId = reader.ReadInt32();
		NumStars = reader.ReadInt16();
		BubbleTime = reader.ReadByte();
		Recipient = reader.ReadString();
		Text = reader.ReadString();
		CleanText = reader.ReadString();
		IsRecipientIgnored = reader.ReadBoolean();
		StarsRequired = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Name);
		writer.Write(ObjectId);
		writer.Write(NumStars);
		writer.Write(BubbleTime);
		writer.Write(Recipient);
		writer.Write(Text);
		writer.Write(CleanText);
		writer.Write(IsRecipientIgnored);
		writer.Write(StarsRequired);
	}

	public static TextPacket CreateNotificationText(string name, string message)
	{
		return new TextPacket
		{
			BubbleTime = 0,
			CleanText = message,
			Name = "#" + name,
			NumStars = -1,
			ObjectId = -1,
			Recipient = "",
			Text = message
		};
	}

	public static TextPacket CreateSystemText(string message)
	{
		return new TextPacket
		{
			BubbleTime = 0,
			CleanText = message,
			Name = string.Empty,
			NumStars = -1,
			ObjectId = -1,
			Recipient = string.Empty,
			Text = message
		};
	}

	public static TextPacket CreateNamedText(string name, string message)
	{
		return new TextPacket
		{
			BubbleTime = 0,
			CleanText = message,
			Name = name,
			NumStars = -1,
			ObjectId = -1,
			Recipient = string.Empty,
			Text = message
		};
	}
}
