using System;
using System.Globalization;
using System.Xml.Linq;

namespace ExaltHelper.Proxy.DataStructures;

internal class Activate
{
	public ActivateType Name;

	public int Amount;

	public int SkinType;

	public float StatModAmount;

	public int Color;

	public int StatModScalingMinimum;

	public Activate(XElement activate)
	{
		if (!Enum.TryParse<ActivateType>(activate.Value, out Name))
		{
			Program.LogWarning("core", $"Failed to parse {activate} as an ActivateType");
		}
		Amount = activate.GetAttributeValue("amount", "0").ParseInt();
		string text = activate.GetAttributeValue("skinType", "0");
		if (text.Contains("0x"))
		{
			SkinType = int.Parse(text.Replace("0x", ""), NumberStyles.HexNumber);
		}
		else
		{
			SkinType = text.ParseInt();
		}
		string text2 = activate.GetAttributeValue("color", "0").ToLower();
		if (text2.Length >= 6)
		{
			Color = int.Parse(text2.Replace("0x", ""), NumberStyles.HexNumber);
		}
		StatModAmount = activate.GetAttributeValue("statModAmount", "0").ParseFloat();
		StatModScalingMinimum = activate.GetAttributeValue("statModScalingMin", "0").ParseInt();
	}
}
