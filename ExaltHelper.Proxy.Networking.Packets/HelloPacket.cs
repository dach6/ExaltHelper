namespace ExaltHelper.Proxy.Networking.Packets;

internal class HelloPacket : Packet
{
	public int GameId;

	public string BuildVersion;

	public string AccessToken;

	public int KeyTime;

	public byte[] Key;

	public string UserPlatform;

	public string PlayPlatform;

	public string PlatformToken;

	public string ClientToken;

	public string UserToken;

	public override void Read(PacketReader reader)
	{
		GameId = reader.ReadInt32();
		BuildVersion = reader.ReadString();
		AccessToken = reader.ReadString();
		KeyTime = reader.ReadInt32();
		Key = reader.ReadBytes(reader.ReadInt16());
		UserPlatform = reader.ReadString();
		PlayPlatform = reader.ReadString();
		PlatformToken = reader.ReadString();
		ClientToken = reader.ReadString();
		UserToken = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(GameId);
		writer.Write(BuildVersion);
		writer.Write(AccessToken);
		writer.Write(KeyTime);
		writer.Write((short)Key.Length);
		writer.Write(Key);
		writer.Write(UserPlatform);
		writer.Write(PlayPlatform);
		writer.Write(PlatformToken);
		writer.Write(ClientToken);
		writer.Write(UserToken);
	}
}
