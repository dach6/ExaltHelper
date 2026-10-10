using System;

namespace ExaltHelper.Proxy.DataStructures;

public class AbilityLogEntry
{
	public DateTime Timestamp { get; set; } = DateTime.Now;

	public string TimeString => Timestamp.ToString("HH:mm:ss");

	public string PlayerName { get; set; } = "Unknown";

	public string ClassName { get; set; } = "";

	public string AbilityType { get; set; } = "ABILITY";

	public string AbilityItemName { get; set; } = "";

	public string Details { get; set; } = "";

	public bool IsLocalPlayer { get; set; }
}
