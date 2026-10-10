using System.Linq;
using System.Runtime.CompilerServices;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class SafeWalkMod
{
	[CompilerGenerated]
	private sealed class SafeWalkTileBinding
	{
		public TileData TileDataInstance;

		internal bool IsCustomSafeTile(TileStructure tile)
		{
			return tile.ID == TileDataInstance.TileType;
		}
	}

	private Client _client;

	private bool[] _groundProtectedTiles;

	private bool _isShaitanMap;

	private bool _isNexusMap;

	private readonly ushort _secretFloorTileId = GameData.Tiles.GetByName("EH Secret Floor").ID;

	private readonly ushort _shallowWaterTileId = GameData.Tiles.GetByName("Crystal Cave Shallow Water").ID;

	private TileStructure[] _customSafeTiles = new TileStructure[5]
	{
		GameData.Tiles.GetByName("Pool"),
		GameData.Tiles.GetById(14605),
		GameData.Tiles.GetById(14788),
		GameData.Tiles.GetById(14812),
		GameData.Tiles.GetById(14771)
	};

	public SafeWalkMod(Client client)
	{
		_client = client;
	}

	private bool IsGroundProtected(int x, int y)
	{
		int num = x * _client.MapWidth + y;
		return _groundProtectedTiles[num];
	}

	private void SetGroundProtected(int x, int y, bool isProtected)
	{
		int num = x * _client.MapWidth + y;
		_groundProtectedTiles[num] = isProtected;
	}

	public void OnMapInfo(MapInfoPacket packet)
	{
		_isShaitanMap = packet.DisplayName.ToLower().Contains("shaitan");
		_isNexusMap = packet.MapName == "Nexus";
		_groundProtectedTiles = new bool[packet.Width * packet.Height];
	}

	public void OnUpdate(UpdatePacket packet)
	{
		if (!Settings.Default.EnableSafeWalk)
		{
			return;
		}
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			ObjectStructure objectStructure = GameData.Objects.GetById(objectData.ObjectType);
			if (objectStructure != null && objectStructure.ProtectFromGroundDamage)
			{
				SetGroundProtected((int)objectData.Stats.Position.X, (int)objectData.Stats.Position.Y, isProtected: true);
			}
		}
		TileData[] Tiles = packet.Tiles;
		foreach (TileData TileDataInstance in Tiles)
		{
			MapTile mapTile = _client.GetTileAtCoords(TileDataInstance.X, TileDataInstance.Y);
			if (Settings.Default.EnableSafeWalk && (!_isShaitanMap || Settings.Default.SafeWalkInShatters) && mapTile.TileStructure.MinDamage > 0 && !IsGroundProtected(TileDataInstance.X, TileDataInstance.Y))
			{
				TileDataInstance.TileType = _secretFloorTileId;
			}
			else if (Settings.Default.EnableCustomNexus && _isNexusMap && _customSafeTiles.Any((TileStructure tileStructure) => tileStructure.ID == TileDataInstance.TileType))
			{
				TileDataInstance.TileType = _shallowWaterTileId;
			}
		}
	}
}
