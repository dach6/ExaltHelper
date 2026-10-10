using System;
using System.Linq;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class QuestData : IDataObject, ICloneable
{
	public string Id;

	public string Name;

	public string Description;

	public string Category;

	public int[] Requirements;

	public int[] Rewards;

	public bool Completed;

	public bool ItemRequirementsMet;

	public bool Repeatable;

	public int Weight;

	public int Tier;

	public QuestData()
	{
	}

	public QuestData(PacketReader itemType)
	{
		Id = itemType.ReadString();
		Name = itemType.ReadString();
		Description = itemType.ReadString();
		Category = itemType.ReadString();
		Tier = itemType.ReadInt32();
		Weight = itemType.ReadInt32();
		Requirements = new int[itemType.ReadInt16()];
		for (int i = 0; i < Requirements.Length; i++)
		{
			Requirements[i] = itemType.ReadInt32();
		}
		Rewards = new int[itemType.ReadInt16()];
		for (int j = 0; j < Rewards.Length; j++)
		{
			Rewards[j] = itemType.ReadInt32();
		}
		Completed = itemType.ReadBoolean();
		ItemRequirementsMet = itemType.ReadBoolean();
		Repeatable = itemType.ReadBoolean();
	}

	public IDataObject Read(PacketReader itemType)
	{
		Id = itemType.ReadString();
		Name = itemType.ReadString();
		Description = itemType.ReadString();
		Category = itemType.ReadString();
		Tier = itemType.ReadInt32();
		Weight = itemType.ReadInt32();
		Requirements = new int[itemType.ReadInt16()];
		for (int i = 0; i < Requirements.Length; i++)
		{
			Requirements[i] = itemType.ReadInt32();
		}
		Rewards = new int[itemType.ReadInt16()];
		for (int j = 0; j < Rewards.Length; j++)
		{
			Rewards[j] = itemType.ReadInt32();
		}
		Completed = itemType.ReadBoolean();
		ItemRequirementsMet = itemType.ReadBoolean();
		Repeatable = itemType.ReadBoolean();
		return this;
	}

	public void Write(PacketWriter itemType)
	{
		itemType.Write(Id);
		itemType.Write(Name);
		itemType.Write(Description);
		itemType.Write(Category);
		itemType.Write(Tier);
		itemType.Write(Weight);
		itemType.Write((short)Requirements.Length);
		int[] array = Requirements;
		foreach (int value in array)
		{
			itemType.Write(value);
		}
		itemType.Write((short)Rewards.Length);
		array = Rewards;
		foreach (int value2 in array)
		{
			itemType.Write(value2);
		}
		itemType.Write(Completed);
		itemType.Write(ItemRequirementsMet);
		itemType.Write(Repeatable);
	}

	public object Clone()
	{
		return new QuestData
		{
			Id = Id,
			Name = Name,
			Description = Description,
			Requirements = Requirements,
			Rewards = Rewards,
			Completed = Completed,
			ItemRequirementsMet = ItemRequirementsMet,
			Weight = Weight,
			Repeatable = Repeatable
		};
	}

	public override string ToString()
	{
		return string.Format("{{ Id={0}, Name={1}, Description={2}, Requirements={3}, Rewards={4}, Completed={5}, ItemOfChoice={6}, Category={7}, Repeatable={8} }}", Id, Name, Description, Requirements.Select((int itemType) => itemType + " "), Rewards.Select((int itemType) => itemType + " "), Completed, ItemRequirementsMet, Weight, Repeatable);
	}

	public string GetDetailsString()
	{
		return "{ Name=" + Name + ", Id=" + Id + " }";
	}
}
