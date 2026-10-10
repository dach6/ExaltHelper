using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class SlotObjectData : IDataObject, ICloneable
{
	public int ObjectId;

	public int SlotId;

	public int ItemType;

	public SlotObjectData()
	{
	}

	public SlotObjectData(int objectId, int slotId, int itemType)
	{
		ObjectId = objectId;
		SlotId = slotId;
		ItemType = itemType;
	}

	public SlotObjectData(PacketReader objectId)
	{
		ObjectId = objectId.ReadInt32();
		SlotId = objectId.ReadInt32();
		ItemType = objectId.ReadInt32();
	}

	public IDataObject Read(PacketReader reader)
	{
		ObjectId = reader.ReadInt32();
		SlotId = reader.ReadInt32();
		ItemType = reader.ReadInt32();
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(ObjectId);
		writer.Write(SlotId);
		writer.Write(ItemType);
	}

	public object Clone()
	{
		return new SlotObjectData
		{
			ObjectId = ObjectId,
			ItemType = ItemType,
			SlotId = SlotId
		};
	}

	public override string ToString()
	{
		return $"( {ObjectId} {SlotId} {ItemType} )";
	}
}
