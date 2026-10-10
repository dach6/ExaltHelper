using System.Collections.Generic;

namespace ExaltHelper.Proxy.Networking;

public static class ClientsHelper
{
	public static object ClientListLock = new object();

	private static HashSet<Client> ActiveClients = new HashSet<Client>();

	private static Client CurrentClient = null;

	internal static void RegisterClient(Client client)
	{
		lock (ClientListLock)
		{
			ActiveClients.Add(client);
			CurrentClient = client;
		}
	}

	internal static void UnregisterClient(Client client)
	{
		lock (ClientListLock)
		{
			ActiveClients.Remove(client);
		}
	}

	public static void ConnectServer(string server)
	{
		lock (ClientListLock)
		{
			if (CurrentClient != null)
			{
				CurrentClient.ConnectServer(server);
			}
		}
	}

	public static void ResetHp()
	{
		lock (ClientListLock)
		{
			foreach (Client item in ActiveClients)
			{
				item.ResetHp();
			}
		}
	}

	public static void TeleportAnchor()
	{
		lock (ClientListLock)
		{
			foreach (Client item in ActiveClients)
			{
				item.TeleportAnchor();
			}
		}
	}
}
