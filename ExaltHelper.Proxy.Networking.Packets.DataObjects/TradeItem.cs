using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class TradeItem : IDataObject, ICloneable
{
	public int Item;

	public int SlotType;

	public bool Tradeable;

	public bool Included;

	public string ItemData;

	public TradeItem()
	{
	}

	public TradeItem(PacketReader reader)
	{
		Item = reader.ReadInt32();
		SlotType = reader.ReadInt32();
		Tradeable = reader.ReadBoolean();
		Included = reader.ReadBoolean();
		ItemData = reader.ReadString();
	}

	public IDataObject Read(PacketReader reader)
	{
		Item = reader.ReadInt32();
		SlotType = reader.ReadInt32();
		Tradeable = reader.ReadBoolean();
		Included = reader.ReadBoolean();
		ItemData = reader.ReadString();
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(Item);
		writer.Write(SlotType);
		writer.Write(Tradeable);
		writer.Write(Included);
		writer.Write(ItemData);
	}

	public object Clone()
	{
		return new TradeItem
		{
			Item = Item,
			SlotType = SlotType,
			Tradeable = Tradeable,
			Included = Included,
			ItemData = ItemData
		};
	}

	public override string ToString()
	{
		return "{ ItemItem=" + Item + ", SlotType=" + SlotType + ", Tradable=" + Tradeable + ", Included=" + Included + ", ItemData=" + ItemData + " }";
	}
}
