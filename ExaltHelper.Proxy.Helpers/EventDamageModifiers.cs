using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace ExaltHelper.Proxy.Helpers;

// The server supplies challenge definitions; never hard-code a seasonal bonus.
internal sealed class EventDamageModifiers
{
	// Definitions survive map reconnects; active IDs always come from this client's player.
	private static readonly Dictionary<string, double> multipliers = new Dictionary<string, double>();
	private static readonly object sync = new object();

	public void Update(string[] definitions)
	{
		if (definitions == null) return;
		var parsed = new Dictionary<string, double>();
		try
		{
			var serializer = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024, RecursionLimit = 64 };
			foreach (string json in definitions) Collect(serializer.DeserializeObject(json), parsed);
		}
		catch (ArgumentException) { return; }
		catch (InvalidOperationException) { return; }
		lock (sync) foreach (var entry in parsed) multipliers[entry.Key] = entry.Value;
	}

	private static void Collect(object node, Dictionary<string, double> result)
	{
		if (node is Dictionary<string, object> obj)
		{
			if (obj.TryGetValue("id", out var id))
			{
				double amount = FindDamageBonus(obj) ?? 1.0;
				if (!double.IsNaN(amount) && !double.IsInfinity(amount) && amount >= 0)
					result[Convert.ToString(id, CultureInfo.InvariantCulture)] = amount;
			}
			foreach (object value in obj.Values) Collect(value, result);
		}
		else if (node is object[] array) foreach (object value in array) Collect(value, result);
	}

	private static double? FindDamageBonus(object node)
	{
		if (node is Dictionary<string, object> obj)
		{
			if (obj.TryGetValue("bonuses", out var bonuses) && bonuses is object[] array)
				foreach (object value in array)
					if (value is Dictionary<string, object> bonus && bonus.TryGetValue("type", out var type) &&
						Convert.ToString(type, CultureInfo.InvariantCulture) == "5" && bonus.TryGetValue("amount", out var amount) &&
						double.TryParse(Convert.ToString(amount, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier))
						return multiplier;
			foreach (object value in obj.Values) { var found = FindDamageBonus(value); if (found.HasValue) return found; }
		}
		else if (node is object[] array)
			foreach (object value in array) { var found = FindDamageBonus(value); if (found.HasValue) return found; }
		return null;
	}

	public bool TryGetMultiplier(string crucibleId, string ritualId, out double multiplier)
	{
		multiplier = 1;
		bool known = true;
		lock (sync)
		{
			foreach (string id in new[] { crucibleId, ritualId })
			{
				if (string.IsNullOrEmpty(id)) continue;
				if (multipliers.TryGetValue(id, out double value)) multiplier *= value;
				else known = false;
			}
		}
		return known;
	}
}
