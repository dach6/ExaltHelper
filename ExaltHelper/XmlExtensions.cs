using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ExaltHelper;

internal static class XmlExtensions
{
	public static IEnumerable<TItem> ForEach<TItem>(this IEnumerable<TItem> items, Action<TItem> action)
	{
		foreach (TItem item in items)
		{
			action(item);
		}
		return items;
	}

	public static bool HasElement(this XElement element, XName name)
	{
		return element.Elements(name).Any();
	}

	public static string GetAttributeValue(this XElement element, XName name, string defaultValue)
	{
		if (!element.Attributes(name).Any())
		{
			return defaultValue;
		}
		return element.Attribute(name).Value;
	}

	public static string GetStringValue(this XElement element, XName name, string defaultValue)
	{
		if (!element.Elements(name).Any())
		{
			return defaultValue;
		}
		return element.Element(name).Value;
	}

	public static int ParseHexInt(this string text)
	{
		return Convert.ToInt32(text, 16);
	}

	public static int ParseInt(this string text)
	{
		return int.Parse(text, CultureInfo.InvariantCulture);
	}

	public static float ParseFloat(this string text)
	{
		return float.Parse(text, CultureInfo.InvariantCulture);
	}
}
