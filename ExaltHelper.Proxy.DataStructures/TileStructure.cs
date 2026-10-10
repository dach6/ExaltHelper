using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ExaltHelper.Proxy.DataStructures;

public class TileStructure : IGameEntity<ushort>
{
	[CompilerGenerated]
	private sealed class TileStructureParser
	{
		public Dictionary<ushort, TileStructure> TilesById;

		internal void ParseTileXml(XElement element)
		{
			TileStructure tileStructure = new TileStructure(element);
			TilesById[tileStructure.ID] = tileStructure;
		}
	}

	[CompilerGenerated]
	private ushort _idField;

	public bool NoWalk;

	public float Speed;

	public bool Sink;

	public ushort MinDamage;

	public ushort MaxDamage;

	[CompilerGenerated]
	private string _nameField;

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

	internal static Dictionary<ushort, TileStructure> LoadFromXml(XDocument document)
	{
		Dictionary<ushort, TileStructure> tilesById = new Dictionary<ushort, TileStructure>();
		document.Element("GroundTypes").Elements("Ground").ForEach(delegate(XElement tile)
		{
			TileStructure tileStructure = new TileStructure(tile);
			tilesById[tileStructure.ID] = tileStructure;
		});
		return tilesById;
	}

	public TileStructure(XElement tile)
	{
		ID = (ushort)tile.GetAttributeValue("type", "0x0").ParseHexInt();
		NoWalk = tile.HasElement("NoWalk");
		Speed = tile.GetStringValue("Speed", "1").ParseFloat();
		Sink = tile.HasElement("Sink");
		MinDamage = (ushort)tile.GetStringValue("MinDamage", "0").ParseInt();
		MaxDamage = (ushort)tile.GetStringValue("MaxDamage", "0").ParseInt();
		Name = tile.GetAttributeValue("id", "");
	}

	public override string ToString()
	{
		return $"Tile: {Name} (0x{ID:X})";
	}
}
