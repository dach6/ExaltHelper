namespace ExaltHelper.Proxy.Networking.Packets;

internal class MapInfoPacket : Packet
{
	public int Width;

	public int Height;

	public string MapName;

	public string DisplayName;

	public string RealmName;

	public uint Fp;

	public float Difficulty;

	public int Background;

	public bool AllowPlayerTeleport;

	public bool NoSave;

	public bool ShowDisplays;

	public short MaxPlayerCount;

	public int GameOpenedTime;

	public string VersionNumber;

	public short UnknownShort1;

	public short ViewDistance;

	public bool UnknownBool;

	public int UnknownInt;

	public string DungeonModifiers;

	public short BgColor;

	public int MaxRealmScore = -1;

	public int CurrentRealmScore = -1;

	public override void Read(PacketReader reader)
	{
		Width = reader.ReadInt32();
		Height = reader.ReadInt32();
		MapName = reader.ReadString();
		DisplayName = reader.ReadString();
		RealmName = reader.ReadString();
		Fp = reader.ReadUInt32();
		Background = reader.ReadInt32();
		Difficulty = reader.ReadSingle();
		AllowPlayerTeleport = reader.ReadBoolean();
		NoSave = reader.ReadBoolean();
		ShowDisplays = reader.ReadBoolean();
		MaxPlayerCount = reader.ReadInt16();
		GameOpenedTime = reader.ReadInt32();
		VersionNumber = reader.ReadString();
		UnknownShort1 = reader.ReadInt16();
		ViewDistance = reader.ReadInt16();
		UnknownBool = reader.ReadBoolean();
		UnknownInt = reader.ReadInt32();
		DungeonModifiers = reader.ReadString();
		BgColor = reader.ReadInt16();
		if (reader.BaseStream.Length - reader.BaseStream.Position >= 4)
		{
			CurrentRealmScore = reader.ReadInt32();
		}
		if (reader.BaseStream.Length - reader.BaseStream.Position >= 4)
		{
			MaxRealmScore = reader.ReadInt32();
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(Width);
		writer.Write(Height);
		writer.Write(MapName);
		writer.Write(DisplayName);
		writer.Write(RealmName);
		writer.Write(Fp);
		writer.Write(Background);
		writer.Write(Difficulty);
		writer.Write(AllowPlayerTeleport);
		writer.Write(NoSave);
		writer.Write(ShowDisplays);
		writer.Write(MaxPlayerCount);
		writer.Write(GameOpenedTime);
		writer.Write(VersionNumber);
		writer.Write(UnknownShort1);
		writer.Write(ViewDistance);
		writer.Write(UnknownBool);
		writer.Write(UnknownInt);
		writer.Write(DungeonModifiers);
		writer.Write(BgColor);
		if (CurrentRealmScore != -1)
		{
			writer.Write(CurrentRealmScore);
		}
		if (MaxRealmScore != -1)
		{
			writer.Write(MaxRealmScore);
		}
	}
}
