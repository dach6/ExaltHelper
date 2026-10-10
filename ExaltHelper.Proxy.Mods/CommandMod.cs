using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper.Proxy.Mods;

internal class CommandMod
{
	[CompilerGenerated]
	private sealed class CommandModBinding
	{
		public string CommandText;

		internal bool FilterTargetObject(MapObject entity)
		{
			return entity.PlayerName.Equals(CommandText, StringComparison.OrdinalIgnoreCase);
		}
	}

	private Client _client;

	private int _questObjectId = -1;

	public CommandMod(Client client)
	{
		_client = client;
	}

	public void OnQuestTarget(DamagePacket packet)
	{
		_questObjectId = packet.ObjectId;
	}

	public void HandleCommand(GenericFailurePacket packet)
	{
		string[] commandArguments;
		string[] serverArguments;
		if (packet.IsCommand("tp", out var teleportArguments))
		{
			if (!Settings.Default.EnableTeleportToPlayerCommand)
			{
				return;
			}
			packet.Send = false;
			if (teleportArguments.Length == 0 && Settings.Default.EnableTeleportToSelf)
			{
				_client.SendNotification("Teleporting to self!");
				TeleportPacket teleportPacket = new TeleportPacket();
				teleportPacket.ObjectId = _client.Player.ObjectId;
				teleportPacket.PlayerName = _client.Player.Name;
				_client.SendToServer(teleportPacket);
				return;
			}
			if (teleportArguments.Length != 1)
			{
				_client.SendNotification("Usage: /tp [partial or full player name]");
				return;
			}
			string text = teleportArguments[0].ToLower();
			foreach (MapObject value in _client.Entities.Values)
			{
				if (!value.IsInvisible() && value.Name.ToLower().Contains(text))
				{
					_client.SendNotification("Teleporting to " + value.Name + "!");
					TeleportPacket teleportPacket2 = new TeleportPacket();
					teleportPacket2.ObjectId = value.ObjectId;
					teleportPacket2.PlayerName = value.Name;
					_client.SendToServer(teleportPacket2);
					return;
				}
			}
			_client.SendNotification("Player with the name similar to " + text + " not found!");
		}
		else if (packet.IsCommand("tpq", out commandArguments))
		{
			if (!Settings.Default.EnableTeleportToPlayerClosestToQuestCommand)
			{
				return;
			}
			packet.Send = false;
			IEnumerable<MapObject> source = _client.Enemies.Values.Where((MapObject mapObject3) => mapObject3.ObjectId == _questObjectId);
			if (!source.Any())
			{
				_client.SendNotification("Quest not found!");
				return;
			}
			MapObject mapObject = null;
			double num = 0.0;
			MapObject mapObject2 = source.First();
			foreach (MapObject value2 in _client.Entities.Values)
			{
				if (value2 != _client.Player && !value2.IsInvisible() && (mapObject == null || value2.Position.DistanceSquaredTo(mapObject2.Position) < num))
				{
					mapObject = value2;
					num = value2.Position.DistanceSquaredTo(mapObject2.Position);
				}
			}
			if (mapObject == null)
			{
				_client.SendNotification("No players found to teleport to!");
				return;
			}
			_client.SendNotification("Teleporting to " + mapObject.Name + "!");
			TeleportPacket teleportPacket3 = new TeleportPacket();
			teleportPacket3.ObjectId = mapObject.ObjectId;
			teleportPacket3.PlayerName = mapObject.Name;
			_client.SendToServer(teleportPacket3);
		}
		else if (packet.IsCommand("join", out serverArguments) || packet.IsCommand("con", out serverArguments) || packet.IsCommand("goto", out serverArguments))
		{
			if (Settings.Default.EnableConnectCommand)
			{
				packet.Send = false;
				if (serverArguments.Length == 1)
				{
					_client.ConnectToNamedServer(serverArguments[0].ToLower());
				}
				else
				{
					_client.SendNotification("Usage: /join [server name]");
				}
			}
		}
		else if (packet.IsCommand("l", out commandArguments) && Settings.Default.EnableLocCommand)
		{
			packet.Send = false;
			_client.SendNotification($"Current location: {Math.Round(_client.Player.Position.X, 1)}, {Math.Round(_client.Player.Position.Y, 1)}");
		}
	}

	public void TeleportToAnchor()
	{
		string CommandText = Settings.Default.TeleportAnchorTarget;
		if (!string.IsNullOrEmpty(CommandText))
		{
			MapObject mapObject = _client.Entities.Values.ToArray().FirstOrDefault((MapObject entity) => entity.PlayerName.Equals(CommandText, StringComparison.OrdinalIgnoreCase));
			if (mapObject == null)
			{
				_client.SendNotification("Player " + CommandText + " not found");
				return;
			}
			TeleportPacket teleportPacket = new TeleportPacket();
			teleportPacket.ObjectId = mapObject.ObjectId;
			teleportPacket.PlayerName = mapObject.Name;
			_client.SendToServer(teleportPacket);
			_client.SendNotification("Teleporting to " + mapObject.PlayerName);
		}
	}

	[CompilerGenerated]
	private bool IsQuestTarget(MapObject entity)
	{
		return entity.ObjectId == _questObjectId;
	}
}
