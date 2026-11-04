using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExaltHelper;

internal sealed class AccountAccelerator
{
    public int ItemType { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public bool Estimated { get; set; }
    public DateTime ObservedUtc { get; set; }
    public AccountAccelerator Clone() => new AccountAccelerator { ItemType = ItemType, ExpiresUtc = ExpiresUtc, Estimated = Estimated, ObservedUtc = ObservedUtc };
}

internal static partial class AccountWorkspace
{
    private static List<AccountAccelerator> ParseAccelerators(string raw, string timestamp)
    {
        var result = new List<AccountAccelerator>();
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long anchor)) return result;
        string[] tokens = (raw ?? "").Split(',');
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type)
                || !long.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long remaining)) continue;
            try
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(checked(anchor + Math.Max(0, remaining))).UtcDateTime;
                result.Add(new AccountAccelerator { ItemType = type, ExpiresUtc = expiry, ObservedUtc = DateTime.UtcNow });
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is OverflowException) { }
        }
        return result;
    }

    private static void ApplyAcceleratorLocked(int itemType)
    {
        // The activation packet carries only an item type. This item has a fixed 24-hour duration.
        if (itemType != 751) return;
        state.Accelerators.RemoveAll(a => a.ItemType == itemType);
        state.Accelerators.Add(new AccountAccelerator { ItemType = itemType, ExpiresUtc = DateTime.UtcNow.AddDays(1), ObservedUtc = DateTime.UtcNow, Estimated = true });
        TouchLocked();
    }
}
