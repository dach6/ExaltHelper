using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper.Proxy;

internal class ProxyServer
{
	private TcpListener _listener;

	public static ushort LocalPort;

	public static ushort LocalPortAlias
	{
		get
		{
			return LocalPort;
		}
		set
		{
			LocalPort = value;
		}
	}

	public ConcurrentDictionary<string, ReconnectPacket> PendingReconnections { get; private set; } = new ConcurrentDictionary<string, ReconnectPacket>();

	public ConcurrentDictionary<string, ReconnectPacket> PendingReconnectionsAlias
	{
		get
		{
			return PendingReconnections;
		}
		set
		{
			PendingReconnections = value;
		}
	}

	public event ClientEventHandler ClientBeginConnect;

	public event ClientEventHandler ClientConnected;

	public event ClientEventHandler ClientDisconnected;

	public void Start()
	{
		Program.LogInfo("listener", "Starting local listener....");
		PendingReconnections = new ConcurrentDictionary<string, ReconnectPacket>();
		_listener = new TcpListener(IPAddress.Loopback, 0);
		_listener.Start();
		LocalPort = (ushort)((IPEndPoint)_listener.LocalEndpoint).Port;
		_listener.BeginAcceptTcpClient(OnAcceptTcpClient, null);
		if (LocalPort == 0)
		{
			throw new InvalidOperationException("Unable to bind proxy port.");
		}
		Program.LogInfo("listener", $"Started local listener on port {LocalPort}.");
		Settings.Default.CurrentPort = LocalPort;
		RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", "CurrentPort", LocalPort);
	}

	public void StartProxy()
	{
		Start();
	}

	private void OnAcceptTcpClient(IAsyncResult ar)
	{
		try
		{
			TcpClient clientSocket = _listener.EndAcceptTcpClient(ar);
			Client client = new Client(this, clientSocket);
			Program.LogInfo("listener", " Client connection received.");
			ClientBeginConnect?.Invoke(client);
		}
		catch (Exception ex)
		{
			Program.LogWarning("listener", "Failed to accept client:\n" + ex);
		}
		try
		{
			_listener?.BeginAcceptTcpClient(OnAcceptTcpClient, null);
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Failed to continue listening for clients.", innerException);
		}
	}

	public void OnClientConnected(Client client)
	{
		ClientConnected?.Invoke(client);
	}

	public void OnClientConnectedProxy(Client client)
	{
		OnClientConnected(client);
	}

	public void OnClientDisconnected(Client client)
	{
		ClientDisconnected?.Invoke(client);
	}

	public void OnClientDisconnectedProxy(Client client)
	{
		OnClientDisconnected(client);
	}
}
