using System.Xml.Linq;
using ExaltHelper.Proxy.Helpers;

namespace ExaltHelper.Proxy.DataStructures;

internal static class GameData
{
	public static LookupTable<ushort, ItemStructure> Items;

	public static LookupTable<ushort, TileStructure> Tiles;

	public static LookupTable<ushort, ObjectStructure> Objects;

	public static void LoadGameData()
	{
		Program.LogInfo("core", "Loading XML...");
		XDocument objectsDocument = XDocument.Load(ResourceHelper.ObjectsXmlPath);
		XDocument tilesDocument = XDocument.Load(ResourceHelper.GroundTypesXmlPath);
		Objects = new LookupTable<ushort, ObjectStructure>(ObjectStructure.LoadFromXml(objectsDocument));
		Items = new LookupTable<ushort, ItemStructure>(ItemStructure.LoadFromXml(objectsDocument));
		Tiles = new LookupTable<ushort, TileStructure>(TileStructure.LoadFromXml(tilesDocument));
	}
}
