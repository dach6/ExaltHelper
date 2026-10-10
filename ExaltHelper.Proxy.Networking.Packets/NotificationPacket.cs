namespace ExaltHelper.Proxy.Networking.Packets;

internal class NotificationPacket : Packet
{
	public const int NotificationStatIncrease = 0;

	public const int NotificationServerMessage = 1;

	public const int NotificationErrorMessage = 2;

	public const int NotificationKeepDictionary = 3;

	public const int NotificationSticky = 4;

	public const int NotificationDungeonOpened = 5;

	public const int NotificationReward = 6;

	public const int NotificationPlayer = 7;

	public const int NotificationDefault = 8;

	public const int NotificationCustom = 20;

	public const int NotificationQueue = 21;

	public const int NotificationObjectText = 22;

	public const int NotificationDeath = 23;

	public byte NotificationType;

	public byte NotificationPicture;

	public string Message = string.Empty;

	public int ObjectId;

	public int Color;

	public override void Read(PacketReader reader)
	{
		NotificationType = reader.ReadByte();
		NotificationPicture = reader.ReadByte();
		if (NotificationType == 6)
		{
			Message = reader.ReadString();
			ObjectId = reader.ReadInt32();
			Color = reader.ReadInt32();
		}
		else if (NotificationType == 7 || NotificationType == 8)
		{
			Message = reader.ReadString();
			ObjectId = reader.ReadInt32();
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(NotificationType);
		writer.Write(NotificationPicture);
		if (NotificationType == 6)
		{
			writer.Write(Message);
			writer.Write(ObjectId);
			writer.Write(Color);
		}
		else if (NotificationType == 7 || NotificationType == 8)
		{
			writer.Write(Message);
			writer.Write(ObjectId);
		}
	}

	public static NotificationPacket CreateAmountNotification(int objectId, string amount)
	{
		return CreateColoredAmountNotification(objectId, 65535, amount);
	}

	public static NotificationPacket CreateColoredAmountNotification(int objectId, int color, string amount)
	{
		return new NotificationPacket
		{
			NotificationType = 6,
			ObjectId = objectId,
			Message = "{\"key\":\"server.plus_symbol\",\"tokens\":{\"amount\":\"" + amount + "\"}}",
			Color = color
		};
	}
}
