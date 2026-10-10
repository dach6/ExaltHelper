using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class LostHallsMod
{
	private bool _isLostHallsActive;

	private const int CultistPortalId = 45239;

	private const int VoidPortalId = 45243;

	public void OnMapInfo(MapInfoPacket packet)
	{
		_isLostHallsActive = packet.MapName == "Lost Halls";
	}

	public void OnUpdate(UpdatePacket packet)
	{
		if (!_isLostHallsActive || !Settings.Default.ShowRealLHPot)
		{
			return;
		}
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (objectData.ObjectType >= 45239 && objectData.ObjectType <= 45243)
			{
				OnObjectStatsUpdate(objectData.Stats);
			}
		}
	}

	private void OnObjectStatsUpdate(ObjectStatsData status)
	{
		foreach (StatData item in status.StatList)
		{
			if (!(item.StatTypeField != StatType.Size))
			{
				item.StatValue = 200;
				return;
			}
		}
		status.StatList.Add(new StatData
		{
			StatValue = 200,
			StatStringValue = string.Empty,
			SecondaryStatValue = 0
		});
	}
}
