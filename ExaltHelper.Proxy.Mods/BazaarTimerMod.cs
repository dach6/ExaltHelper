using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper.Proxy.Mods;

internal class BazaarTimerMod
{
	private Client _client;

	private int _bazaarTimer = 30;

	public BazaarTimerMod(Client client)
	{
		_client = client;
	}

	public void ResetTimer()
	{
		_bazaarTimer = 30;
	}

	public void OnMove(MovePacket packet)
	{
		if (Settings.Default.EnableBazaarTimer && _client._currentServerName.Contains("Bazaar") && _bazaarTimer > 0 && _bazaarTimer-- % 5 == 0)
		{
			_client.SendNotification("Bazaar Timer", _bazaarTimer + " seconds until you can enter portals");
		}
	}

	private int GetPercentageColor(int percentage)
	{
		if (percentage > 50)
		{
			return 65280 + 327680 * (100 - percentage);
		}
		return 16776960 - 1280 * (50 - percentage);
	}
}
