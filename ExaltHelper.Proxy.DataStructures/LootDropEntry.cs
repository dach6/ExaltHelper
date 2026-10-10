using System;
using System.Collections.Generic;
using System.Drawing;

namespace ExaltHelper.Proxy.DataStructures;

internal class LootDropEntry
{
	public int ItemId { get; set; }

	public string ItemName { get; set; } = "Unknown Item";

	public string ItemRarity { get; set; } = "Common";

	public bool IsShiny { get; set; }

	public int EnchantCount { get; set; }

	public List<string> Enchants { get; set; } = new List<string>();

	public string BagType { get; set; } = "Blue Bag";

	public Color BagColor { get; set; } = Color.SkyBlue;

	public string SourceMonster { get; set; } = "Monster";

	public DateTime DropTime { get; set; } = DateTime.Now;
}
