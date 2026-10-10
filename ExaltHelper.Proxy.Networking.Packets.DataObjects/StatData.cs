using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class StatData : IDataObject, ICloneable
{
	public StatType StatTypeField;

	public int StatValue;

	public string StatStringValue;

	public byte SecondaryStatValue;

	public StatType Type
	{
		get
		{
			return StatTypeField;
		}
		set
		{
			StatTypeField = value;
		}
	}

	public int Value
	{
		get
		{
			return StatValue;
		}
		set
		{
			StatValue = value;
		}
	}

	public string StringValue
	{
		get
		{
			return StatStringValue;
		}
		set
		{
			StatStringValue = value;
		}
	}

	public byte SecondaryValue
	{
		get
		{
			return SecondaryStatValue;
		}
		set
		{
			SecondaryStatValue = value;
		}
	}

	public bool IsStringStat => CheckIsStringStat();

	public bool CheckIsStringStat()
	{
		return StatTypeField.UsesStringValue();
	}

	public StatData()
	{
	}

	public StatData(PacketReader reader)
	{
		Read(reader);
	}

	public IDataObject Read(PacketReader reader)
	{
		StatTypeField = reader.ReadByte();
		if (CheckIsStringStat())
		{
			StatStringValue = reader.ReadString();
		}
		else
		{
			StatValue = reader.ReadCompressedInt();
		}
		SecondaryStatValue = reader.ReadByte();
		return this;
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(StatTypeField);
		if (CheckIsStringStat())
		{
			writer.Write(StatStringValue);
		}
		else
		{
			writer.WriteCompressedInt(StatValue);
		}
		writer.Write(SecondaryStatValue);
	}

	public object Clone()
	{
		return new StatData
		{
			StatTypeField = StatTypeField,
			StatValue = StatValue,
			StatStringValue = StatStringValue,
			SecondaryStatValue = SecondaryStatValue
		};
	}

	public override string ToString()
	{
		return $"({Enum.GetName(typeof(StatsTypeEnum), (int)StatTypeField)} = {(CheckIsStringStat() ? StatStringValue : StatValue.ToString())} (Extra: {SecondaryStatValue}))";
	}
}
