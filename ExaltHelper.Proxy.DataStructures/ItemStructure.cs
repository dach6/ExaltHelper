using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ExaltHelper.Proxy.DataStructures;

internal class ItemStructure : IGameEntity<ushort>
{
	[CompilerGenerated]
	private sealed class ItemStructureParser
	{
		public Dictionary<ushort, ItemStructure> ItemsById;

		internal void ParseItemXml(XElement element)
		{
			ItemStructure itemStructure = new ItemStructure(element);
			ItemsById[itemStructure.ID] = itemStructure;
		}
	}

	[CompilerGenerated]
	private ushort _idField;

	public Tiers Tier;

	public byte SlotType;

	public byte MpCost;

	public float Cooldown;

	public float ActivationRadius;

	public float AbilityUseDiscount = 1f;

	public bool IsConsumable;

	[CompilerGenerated]
	private string _nameField;

	public int QuickslotMaxStack;

	public (float? Cooldown, int? MpCost)[] AbilityOverrides = Array.Empty<(float?, int?)>();

	public List<Activate> Activations = new List<Activate>();

	public ushort ID
	{
		[CompilerGenerated]
		get
		{
			return _idField;
		}
		[CompilerGenerated]
		private set
		{
			_idField = value;
		}
	}

	public string Name
	{
		[CompilerGenerated]
		get
		{
			return _nameField;
		}
		[CompilerGenerated]
		private set
		{
			_nameField = value;
		}
	}

	internal static Dictionary<ushort, ItemStructure> LoadFromXml(XDocument document)
	{
		Dictionary<ushort, ItemStructure> itemsById = new Dictionary<ushort, ItemStructure>();
		(from itemElement in document.Element("Objects").Elements("Object")
			where itemElement.HasElement("Item")
			select itemElement).ForEach(delegate(XElement itemElement)
		{
			ItemStructure itemStructure = new ItemStructure(itemElement);
			itemsById[itemStructure.ID] = itemStructure;
		});
		return itemsById;
	}

	public ItemStructure(XElement element)
	{
		ID = (ushort)element.GetAttributeValue("type", "0x0").ParseHexInt();
		Tier = (element.HasElement("Tier") ? ((Tiers)element.Element("Tier").Value.ParseInt()) : Tiers.UT);
		SlotType = (byte)element.GetStringValue("SlotType", "0").ParseInt();
		MpCost = (byte)element.GetStringValue("MpCost", "0").ParseInt();
		Cooldown = element.GetStringValue("Cooldown", "0").ParseFloat();
		if (element.HasElement("Activate"))
		{
			ParseActivates(element.Elements("Activate"));
		}
		if (element.HasElement("Activate"))
		{
			foreach (XElement item in element.Elements("Activate"))
			{
				XAttribute xAttribute = item.Attribute("radius");
				if (xAttribute != null)
				{
					float.TryParse(xAttribute.Value, out ActivationRadius);
				}
			}
		}
		if (element.HasElement("ActivateOnEquip"))
		{
			foreach (XElement item2 in element.Elements("ActivateOnEquip"))
			{
				if (item2.Value == "AbilityUseDiscount")
				{
					XAttribute xAttribute2 = item2.Attribute("multiplier");
					if (xAttribute2 != null)
					{
						float.TryParse(xAttribute2.Value, out AbilityUseDiscount);
					}
				}
			}
		}
		if (element.HasElement("Ability"))
		{
			AbilityOverrides = (from xElement in element.Elements("Ability")
				select (xElement.HasElement("Cooldown") ? new float?(xElement.Element("Cooldown").Value.ParseFloat()) : ((float?)null), xElement.HasElement("MpCost") ? new int?(xElement.Element("MpCost").Value.ParseInt()) : ((int?)null))).ToArray();
		}
		IsConsumable = element.HasElement("Consumable");
		string text = element.GetStringValue("DisplayId", "");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = element.GetAttributeValue("displayId", "");
		}
		string text2 = element.GetAttributeValue("id", "");
		Name = ((!string.IsNullOrWhiteSpace(text) && !text.StartsWith("{s.")) ? text : text2);
		if (element.HasElement("QuickslotAllowed"))
		{
			int.TryParse(element.Element("QuickslotAllowed").GetAttributeValue("maxstack", "6"), out QuickslotMaxStack);
		}
	}

	private void ParseActivates(IEnumerable<XElement> activations)
	{
		foreach (XElement item in activations)
		{
			Activations.Add(new Activate(item));
		}
	}

	public override string ToString()
	{
		return $"Item: {Name} (0x{ID:X})";
	}
}
