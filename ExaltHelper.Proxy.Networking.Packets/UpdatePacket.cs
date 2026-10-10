using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class UpdatePacket : Packet
{
	public WorldPosData PlayerPosition;

	public byte UpdateType;

	public TileData[] Tiles;

	public ObjectData[] NewObjects;

	public int[] Drops;

	public override void Read(PacketReader reader)
	{
		PlayerPosition = new WorldPosData(reader);
		UpdateType = reader.ReadByte();
		Tiles = new TileData[reader.ReadCompressedInt()];
		for (int i = 0; i < Tiles.Length; i++)
		{
			Tiles[i] = (TileData)new TileData().Read(reader);
		}
		NewObjects = new ObjectData[reader.ReadCompressedInt()];
		for (int j = 0; j < NewObjects.Length; j++)
		{
			NewObjects[j] = (ObjectData)new ObjectData().Read(reader);
		}
		Drops = new int[reader.ReadCompressedInt()];
		for (int k = 0; k < Drops.Length; k++)
		{
			Drops[k] = reader.ReadCompressedInt();
		}
	}

	public override void Write(PacketWriter writer)
	{
		PlayerPosition.Write(writer);
		writer.Write(UpdateType);
		writer.WriteCompressedInt(Tiles.Length);
		TileData[] array = Tiles;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Write(writer);
		}
		writer.WriteCompressedInt(NewObjects.Length);
		ObjectData[] array2 = NewObjects;
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j].Write(writer);
		}
		writer.WriteCompressedInt(Drops.Length);
		int[] array3 = Drops;
		foreach (int objectId in array3)
		{
			writer.WriteCompressedInt(objectId);
		}
	}
}
