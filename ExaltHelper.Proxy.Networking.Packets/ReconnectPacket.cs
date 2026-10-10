using ExaltHelper.Proxy.Helpers;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class ReconnectPacket : Packet
{
	public string MapName;

	public string Host;

	public ushort Port;

	public int GameId;

	public int KeyTime;

	public byte[] Key;

	private const char DelimiterChar = '|';

	public override void Read(PacketReader reader)
	{
		MapName = reader.ReadString();
		Host = reader.ReadString();
		Port = reader.ReadUInt16();
		GameId = reader.ReadInt32();
		KeyTime = reader.ReadInt32();
		Key = reader.ReadBytes(reader.ReadInt16());
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(MapName);
		writer.Write(Host);
		writer.Write(Port);
		writer.Write(GameId);
		writer.Write(KeyTime);
		writer.Write((short)Key.Length);
		writer.Write(Key);
	}

	public string SerializeToString()
	{
		return MapName + "|" + Host + "|" + Port + "|" + GameId + "|" + KeyTime + "|" + Key.ToHexString();
	}

	public static ReconnectPacket DeserializeFromString(string serializedPacket)
	{
		string[] array = serializedPacket.Split('|');
		return new ReconnectPacket
		{
			MapName = array[0],
			Host = array[1],
			Port = ushort.Parse(array[2]),
			GameId = int.Parse(array[3]),
			KeyTime = int.Parse(array[4]),
			Key = ((array.Length == 6) ? array[5].ToByteArray() : null)
		};
	}
}
