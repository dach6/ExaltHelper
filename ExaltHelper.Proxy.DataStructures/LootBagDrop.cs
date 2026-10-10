using System;
using System.Collections.Generic;
using System.Drawing;

namespace ExaltHelper.Proxy.DataStructures;

internal class LootBagDrop
{
	public int BagEntityId { get; set; }

	public string BagType { get; set; } = "Blue Bag";

	public Color BagColor { get; set; } = Color.SkyBlue;

	public string SourceMonster { get; set; } = "Monster";

	public DateTime DropTime { get; set; } = DateTime.Now;

	public List<int> ItemIds { get; set; } = new List<int>();

	public List<string> ItemRarities { get; set; } = new List<string>();

	public List<bool> ItemIsShiny { get; set; } = new List<bool>();

	public List<int> ItemEnchantCounts { get; set; } = new List<int>();

	public List<List<string>> ItemEnchants { get; set; } = new List<List<string>>();

	public bool HasShinyItem => ItemIsShiny != null && ItemIsShiny.Contains(true);
}
