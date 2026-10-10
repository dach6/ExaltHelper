using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace ExaltHelper.Proxy;

internal static class ServerManager
{
	public static readonly string PreferredServerPrefKey = "ProductionpreferredServer_h3390581990";

	public static Dictionary<string, string> ServersByName { get; private set; } = new Dictionary<string, string>();

	public static Dictionary<string, string> ServerAbbreviations { get; private set; } = new Dictionary<string, string>();

	public static Dictionary<string, string> ServersByNameAlias => ServersByName;

	public static Dictionary<string, string> ServerAbbreviationsAlias => ServerAbbreviations;

	public static string PreferredServerPrefKeyAlias => PreferredServerPrefKey;

	public static void LoadServers()
	{
		Program.LogInfo("core", "Loading RotMG game servers...");
		XmlDocument xmlDocument = new XmlDocument();
		string xml = "<Servers><Server><Name>EUEast</Name><DNS>18.184.218.174</DNS></Server><Server><Name>EUSouthWest</Name><DNS>35.180.67.120</DNS></Server><Server><Name>USEast2</Name><DNS>54.209.152.223</DNS></Server><Server><Name>EUNorth</Name><DNS>18.159.133.120</DNS></Server><Server><Name>USEast</Name><DNS>54.234.226.24</DNS></Server><Server><Name>USWest4</Name><DNS>54.235.235.140</DNS></Server><Server><Name>EUWest2</Name><DNS>52.16.86.215</DNS></Server><Server><Name>Asia</Name><DNS>3.0.147.127</DNS></Server><Server><Name>USSouth3</Name><DNS>52.207.206.31</DNS></Server><Server><Name>EUWest</Name><DNS>15.237.60.223</DNS></Server><Server><Name>USWest</Name><DNS>54.86.47.176</DNS></Server><Server><Name>USMidWest2</Name><DNS>3.140.254.133</DNS></Server><Server><Name>USMidWest</Name><DNS>18.221.120.59</DNS></Server><Server><Name>USSouth</Name><DNS>3.82.126.16</DNS></Server><Server><Name>USWest3</Name><DNS>18.144.30.153</DNS></Server><Server><Name>Australia</Name><DNS>3.107.164.237</DNS></Server><Server><Name>USSouthWest</Name><DNS>54.153.13.68</DNS></Server><Server><Name>USNorthWest</Name><DNS>34.238.176.119</DNS></Server></Servers>";
		if (File.Exists("servers.xml"))
		{
			try
			{
				xml = File.ReadAllText("servers.xml");
				Program.LogInfo("core", "Loaded custom servers from servers.xml");
			}
			catch (Exception ex)
			{
				Program.LogWarning("core", "Failed reading custom servers.xml: " + ex.Message);
			}
		}
		xmlDocument.LoadXml(xml);
		ServersByName.Clear();
		ServerAbbreviations.Clear();
		foreach (XmlNode item in xmlDocument.SelectNodes("Servers/Server") ?? throw new InvalidOperationException("Parsing server list failed: no <Server> nodes found."))
		{
			XmlNode xmlNode2 = item.SelectSingleNode("Name");
			XmlNode xmlNode3 = item.SelectSingleNode("DNS");
			if (xmlNode2 != null && xmlNode3 != null)
			{
				string innerText = xmlNode2.InnerText;
				string innerText2 = xmlNode3.InnerText;
				ServersByName[innerText] = innerText2;
				int num = 0;
				string key;
				do
				{
					StringBuilder stringBuilder = new StringBuilder();
					int num2 = 0;
					for (int i = 0; i < innerText.Length; i++)
					{
						char c = innerText[i];
						if (char.IsUpper(c) || char.IsDigit(c))
						{
							stringBuilder.Append(c);
						}
						if (char.IsUpper(c))
						{
							num2 = i;
						}
					}
					if (num > 0)
					{
						for (int j = num2 + 1; j < num2 + 1 + num && j < innerText.Length; j++)
						{
							stringBuilder.Append(innerText[j]);
						}
					}
					key = stringBuilder.ToString().ToLowerInvariant();
					num++;
				}
				while (ServerAbbreviations.ContainsKey(key) && num < innerText.Length);
				ServerAbbreviations[key] = innerText;
				ServerAbbreviations[innerText.ToLowerInvariant()] = innerText;
			}
			else
			{
				Program.LogWarning("core", "Failed parsing server " + item.OuterXml);
			}
		}
		if (ServersByName.Count == 0)
		{
			throw new InvalidOperationException("Was not able to parse any RotMG servers!");
		}
	}

	public static void InitServers()
	{
		LoadServers();
	}
}
