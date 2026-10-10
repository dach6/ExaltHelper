using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;
using ExaltHelper.Proxy.Networking;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy;

internal class Client
{
	[CompilerGenerated]
	private sealed class ClientSessionTokenContext
	{
		public string Text;

		internal void CopyTextToClipboard()
		{
			Clipboard.SetText(Text);
		}
	}

	private const string DefaultClientKey = "5a4d2016bc16dc64883194ffd9";

	private const string DefaultServerKey = "c91d9eec420160730d825604e0";

	private readonly object _localClientLock = new object();

	private readonly object _remoteClientLock = new object();

	private readonly object _packetBufferLock = new object();

	public int _lastTickTime;

	public int _serverTime;

	public int _clientTime;

	private readonly RC4 _localIncomingRC4 = new RC4("5a4d2016bc16dc64883194ffd9");

	private readonly RC4 _remoteOutgoingRC4 = new RC4("c91d9eec420160730d825604e0");

	private readonly RC4 _remoteIncomingRC4 = new RC4("c91d9eec420160730d825604e0");

	private readonly RC4 _localOutgoingRC4 = new RC4("5a4d2016bc16dc64883194ffd9");

	private readonly PacketBuffer _localPacketBuffer = new PacketBuffer();

	private readonly PacketBuffer _remotePacketBuffer = new PacketBuffer();

	private TcpClient _localClient;

	private TcpClient _remoteClient;

	private NetworkStream _localStream;

	private NetworkStream _remoteStream;

	private bool _isDisconnected;

	public ProxyServer ServerProxy;

	private byte[] _pendingKeyBytes = new byte[0];

	private int _reconnectKeyTime;

	private int _reconnectGameId = -2;

	public int ClientId = -1;

	public int CharacterId = -1;

	public int MapWidth;

	public int MapHeight;

	public string _currentServerName = "";

	private byte _nextBulletId = 1;

	public MapTile[] Tiles = new MapTile[0];

	public readonly Dictionary<int, MapObject> Entities = new Dictionary<int, MapObject>();

	public readonly Dictionary<int, MapObject> Enemies = new Dictionary<int, MapObject>();

	public readonly Dictionary<int, MapObject> Portals = new Dictionary<int, MapObject>();

	public readonly Dictionary<int, Dictionary<int, Projectile>> Projectiles = new Dictionary<int, Dictionary<int, Projectile>>();

	public readonly PotionInfo[] QuickSlotPotions = new PotionInfo[3]
	{
		new PotionInfo(-1, 0),
		new PotionInfo(-1, 0),
		new PotionInfo(-1, 0)
	};

	public bool HasPotionBelt;

	public static int[] HealthPotionTypes;

	public static int[] ManaPotionTypes;

	public static Dictionary<string, Dictionary<int, int>> PetHealingByAccountAndCharacter;

	public int PetHealAmount;

	private readonly AntiAfkMod antiAfkMod;

	private readonly AntiDebuffMod antiDebuffMod;

	private readonly AntiLagMod antiLagMod;

	private readonly SpamFilterMod spamFilterMod;

	public readonly AutoAbilityMod autoAbilityMod;

	private readonly AutoLootMod autoLootMod;

	public readonly PlayerTrackerMod playerTrackerMod;

	public readonly EntityTrackerMod entityTrackerMod;

	private readonly SafeWalkMod safeWalkMod;

	private readonly CommandMod commandMod;

	private readonly FollowMod followMod;

	public readonly DpsTrackerMod dpsTracker;

	private readonly ReconnectMod reconnectMod;

	private readonly BazaarTimerMod bazaarTimerMod;

	private readonly LostHallsMod lostHallsMod;

	public readonly OryxSanctuaryMod oryxSanctuaryMod;

	public readonly MoonlightVillageMod moonlightVillageMod;

	private readonly DateTime _connectedTimestamp = DateTime.Now;

	[CompilerGenerated]
	private MapObject _player;

	[CompilerGenerated]
	private MapObject _targetFollowObject;

	public int EstimatedServerTime => _serverTime + (Environment.TickCount - _lastTickTime);

	public int SessionDurationMs => (int)(DateTime.Now - _connectedTimestamp).TotalMilliseconds;

	public int PlayerId => Player?.ObjectId ?? ClientId;

	public MapObject Player
	{
		[CompilerGenerated]
		get
		{
			return _player;
		}
		[CompilerGenerated]
		set
		{
			_player = value;
		}
	}

	public MapObject FollowTargetObject
	{
		[CompilerGenerated]
		get
		{
			return _targetFollowObject;
		}
		[CompilerGenerated]
		set
		{
			_targetFollowObject = value;
		}
	}

	public bool IsConnected => !_isDisconnected;

	public TcpClient ClientSocket => _localClient;

	public Client(ProxyServer proxy, TcpClient clientSocket)
	{
		ClientsHelper.RegisterClient(this);
		ServerProxy = proxy;
		_localClient = clientSocket;
		_localStream = _localClient.GetStream();
		_localClient.NoDelay = true;
		antiAfkMod = new AntiAfkMod();
		antiDebuffMod = new AntiDebuffMod(this);
		antiLagMod = new AntiLagMod(this);
		spamFilterMod = new SpamFilterMod(this);
		autoAbilityMod = new AutoAbilityMod(this);
		autoLootMod = new AutoLootMod(this);
		playerTrackerMod = new PlayerTrackerMod(this);
		entityTrackerMod = new EntityTrackerMod(this);
		safeWalkMod = new SafeWalkMod(this);
		commandMod = new CommandMod(this);
		followMod = new FollowMod(this);
		dpsTracker = new DpsTrackerMod(this);
		reconnectMod = new ReconnectMod(this);
		bazaarTimerMod = new BazaarTimerMod(this);
		lostHallsMod = new LostHallsMod();
		oryxSanctuaryMod = new OryxSanctuaryMod(this);
		moonlightVillageMod = new MoonlightVillageMod(this);
		BeginReadPacketBytes(0, 4, fromClient: true);
	}

	static Client()
	{
		PetHealingByAccountAndCharacter = new Dictionary<string, Dictionary<int, int>>();
		ushort[] source = new ushort[8] { 8779, 8798, 8795, 8780, 8764, 8765, 2736, 2781 };
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		foreach (ItemStructure value in GameData.Items.Map.Values)
		{
			if (value.IsConsumable && !source.Contains(value.ID))
			{
				if (value.Activations.Any((Activate activate) => (activate.Amount > 0 && activate.Name == ActivateType.Heal) || activate.Name == ActivateType.HealNova))
				{
					list.Add(value.ID);
				}
				if (value.Activations.Any((Activate activate) => (activate.Amount > 0 && activate.Name == ActivateType.Magic) || activate.Name == ActivateType.MagicNova))
				{
					list2.Add(value.ID);
				}
			}
		}
		HealthPotionTypes = list.ToArray();
		ManaPotionTypes = list2.ToArray();
	}

	private void OnRemoteConnected(IAsyncResult asyncResult)
	{
		try
		{
			_remoteClient.EndConnect(asyncResult);
			_remoteStream = _remoteClient.GetStream();
			string text = (_remoteClient.Client.RemoteEndPoint as IPEndPoint).Address.ToString();
			reconnectMod.SetServerIp(text);
			SendToServer(asyncResult.AsyncState as Packet);
			BeginReadPacketBytes(0, 4, fromClient: false);
			ServerProxy.OnClientConnectedProxy(this);
			Program.LogInfo("client", "Connected to remote host " + text);
		}
		catch (Exception exception)
		{
			Disconnect(exception);
		}
	}

	private void SendRetryReconnect()
	{
		try
		{
			Program.LogInfo("client", "Sending Retry Reconnect to client");
			ReconnectPacket reconnectPacket = new ReconnectPacket();
			reconnectPacket.Host = "";
			reconnectPacket.Port = ProxyServer.LocalPortAlias;
			reconnectPacket.MapName = "Retry";
			reconnectPacket.Key = _pendingKeyBytes;
			reconnectPacket.KeyTime = _reconnectKeyTime;
			reconnectPacket.GameId = _reconnectGameId;
			SendToClient(reconnectPacket);
			_localStream.Flush();
		}
		catch (Exception exception)
		{
			Program.LogWarning("client", $"Failed to send Retry Reconnect: {exception}");
		}
	}

	public void Disconnect(Exception exception = null)
	{
		if (!_isDisconnected)
		{
			try
			{
				string text = ((exception != null) ? (exception.GetType().Name + ": " + exception.Message + "\r\nStack:\r\n" + exception.StackTrace) : "Clean disconnect (socket closed or remote closed)");
				File.AppendAllText("disconnect_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [Disconnect] Client ID={ClientId} Name='{Player?.PlayerName}' Reason: {text}\r\nCaller:\r\n{new StackTrace(1, fNeedFileInfo: true)}\r\n\r\n");
			}
			catch
			{
			}
			ClientsHelper.UnregisterClient(this);
			_isDisconnected = true;
			ServerProxy.OnClientDisconnectedProxy(this);
			_localStream?.Close();
			_remoteStream?.Close();
			_localClient?.Close();
			_remoteClient?.Close();
			_localPacketBuffer?.ReleaseBuffer();
			_remotePacketBuffer?.ReleaseBuffer();
			followMod?.Dispose();
			dpsTracker?.Dispose();
			if (exception != null && !(exception is SocketException) && !(exception is IOException))
			{
				Program.LogInfo("client", "Disposed\n" + exception.Message);
			}
			else
			{
				Program.LogInfo("client", "Disconnected");
			}
		}
	}

	public void SendToClient(Packet packet)
	{
		SendInternalPacket(packet, toClient: true);
	}

	public void SendToServer(Packet packet)
	{
		SendInternalPacket(packet, toClient: false);
	}

	private void SendInternalPacket(Packet packet, bool toClient)
	{
		lock (toClient ? _remoteClientLock : _localClientLock)
		{
			try
			{
				if (packet == null)
				{
					Program.LogWarning("Send", $"Packet being sent is null! Client packet: {toClient}");
					return;
				}
				if (packet.HasNullFields(packet))
				{
					try
					{
						File.AppendAllText("disconnect_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [NullFieldDrop] Dropped packet {packet.GetType().Name} (ID {packet.Id})\r\n");
						return;
					}
					catch
					{
						return;
					}
				}
				using MemoryStream memoryStream = new MemoryStream();
				using (PacketWriter packetWriter = new PacketWriter(memoryStream))
				{
					packetWriter.Write(0);
					packetWriter.Write(packet.Id);
					packet.Write(packetWriter);
					if (packet.ExtraBytes.Length != 0)
					{
						packetWriter.Write(packet.ExtraBytes);
					}
				}
				byte[] array = memoryStream.ToArray();
				PacketWriter.WriteBuffer(array, array.Length);
				if (_remoteIncomingRC4 == null || _localStream == null || _localOutgoingRC4 == null || _remoteStream == null)
				{
					Program.LogWarning("Send", $"Tried sending packet when client has already disposed (client: {toClient})");
				}
				else if (toClient)
				{
					_remoteIncomingRC4.Crypt(array);
					_localStream.Write(array, 0, array.Length);
				}
				else
				{
					_localOutgoingRC4.Crypt(array);
					_remoteStream.Write(array, 0, array.Length);
				}
			}
			catch (Exception exception)
			{
				Disconnect(exception);
			}
		}
	}

	private void BeginReadPacketBytes(int offset, int count, bool fromClient)
	{
		PacketBuffer packetBuffer = (fromClient ? _localPacketBuffer : _remotePacketBuffer);
		NetworkStream networkStream = (fromClient ? _localStream : _remoteStream);
		try
		{
			networkStream.BeginRead(packetBuffer.BufferAlias, offset, count, OnPacketBytesRead, fromClient);
		}
		catch (Exception exception)
		{
			Disconnect(exception);
		}
	}

	private void OnPacketBytesRead(IAsyncResult asyncResult)
	{
		bool num = (bool)asyncResult.AsyncState;
		PacketBuffer packetBuffer = (num ? _localPacketBuffer : _remotePacketBuffer);
		NetworkStream networkStream = (num ? _localStream : _remoteStream);
		bool flag = networkStream == _localStream;
		RC4 rC = (flag ? _localIncomingRC4 : _remoteOutgoingRC4);
		try
		{
			if (!networkStream.CanRead)
			{
				return;
			}
			int num2 = networkStream.EndRead(asyncResult);
			packetBuffer.AdvanceReadPosition(num2);
			if (num2 == 0)
			{
				Disconnect();
				return;
			}
			if (packetBuffer.BytesReadAlias == 4)
			{
				packetBuffer.ResizeForPacket(IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packetBuffer.BufferAlias, 0)));
				BeginReadPacketBytes(packetBuffer.BytesReadAlias, packetBuffer.GetRemainingByteCount(), flag);
				return;
			}
			if (packetBuffer.GetRemainingByteCount() > 0)
			{
				BeginReadPacketBytes(packetBuffer.BytesReadAlias, packetBuffer.GetRemainingByteCount(), flag);
				return;
			}
			rC.Crypt(packetBuffer.BufferAlias);
			Packet packet = Packet.Create(packetBuffer.BufferAlias);
			lock (_packetBufferLock)
			{
				try
				{
					RoutePacket(packet);
				}
				catch (Exception ex)
				{
					Program.LogError("RoutePacket", $"Error routing packet {packet.GetType().Name}: {ex}");
					try
					{
						File.AppendAllText("disconnect_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [RoutePacket Exception] Packet={packet.GetType().Name} Error: {ex.GetType().Name}: {ex.Message}\r\nStack:\r\n{ex.StackTrace}\r\n\r\n");
					}
					catch
					{
					}
				}
			}
			if (packet.Send)
			{
				SendInternalPacket(packet, !flag);
			}
			packetBuffer.ResetForNextPacket();
			BeginReadPacketBytes(0, 4, flag);
		}
		catch (Exception exception)
		{
			SendRetryReconnect();
			Disconnect(exception);
		}
	}

	private void RoutePacket(Packet packet2)
	{
		if (packet2 is DamageBoostPacket boostPacket)
		{
			dpsTracker?.OnDamageBoost(boostPacket);
			return;
		}
		if (packet2 is CrucibleResponsePacket eventsPacket)
		{
			dpsTracker?.OnCrucibleResponse(eventsPacket);
			return;
		}
		if (packet2 is PlayerShootServerPacket packet)
		{
			dpsTracker?.OnPlayerShootServer(packet);
			return;
		}
		if (packet2 is StasisPacket stasisPacket)
		{
			dpsTracker?.OnStasis(stasisPacket);
			return;
		}
		if (packet2 is IncomingPartyMemberInfoPacket partyMemberInfoPacket)
		{
			dpsTracker?.OnPartyMemberInfo(partyMemberInfoPacket);
			return;
		}
		if (packet2 is PartyMemberAddedPacket partyMemberAddedPacket)
		{
			dpsTracker?.OnPartyMemberAdded(partyMemberAddedPacket);
			return;
		}
		if (packet2 is PartyActionResultPacket partyActionResultPacket)
		{
			dpsTracker?.OnPartyActionResult(partyActionResultPacket);
			return;
		}
		if (!(packet2 is UpdatePacket updatePacket))
		{
			if (!(packet2 is NewTickPacket newTickPacket))
			{
				if (!(packet2 is MovePacket movePacket))
				{
					if (!(packet2 is MapInfoPacket mapInfoPacket))
					{
						if (!(packet2 is GenericFailurePacket genericFailurePacket))
						{
							if (!(packet2 is GotoPacket gotoPacket))
							{
								if (!(packet2 is FailurePacket failurePacket))
								{
									if (!(packet2 is ReconnectPacket reconnectPacket))
									{
										if (!(packet2 is HelloPacket helloPacket))
										{
											if (!(packet2 is ShootAckPacket playerHitPacket))
											{
												if (!(packet2 is AoePacket aoePacket))
												{
													if (!(packet2 is GotoAckPacket positionAckPacket))
													{
														if (!(packet2 is GroundDamagePacket groundDamagePacket))
														{
															if (!(packet2 is DamagePacket questTargetPacket))
															{
																if (!(packet2 is ShowEffectPacket showEffectPacket))
																{
																	if (!(packet2 is EnemyShootPacket enemyShootPacket))
																	{
																		if (!(packet2 is PlayerShootPacket playerShootPacket))
																		{
																			if (!(packet2 is TextPacket textPacket))
																			{
																				if (!(packet2 is UseItemPacket useItemPacket))
																				{
																					if (!(packet2 is ServerPlayerShootPacket serverPlayerShootPacket))
																					{
																						if (!(packet2 is EnemyHitPacket enemyHitPacket))
																						{
																							if (!(packet2 is InvDropPacket inventorySwapPacket))
																							{
																								if (!(packet2 is PongPacket))
																								{
																									if (!(packet2 is InvSwapPacket inventoryResultPacket))
																									{
																										if (!(packet2 is NotificationPacket notificationPacket))
																										{
																											if (!(packet2 is ReskinPacket))
																											{
																												if (!(packet2 is OtherHitPacket abilitySelectionPacket))
																												{
																													if (!(packet2 is VaultContentPacket vaultContentPacket))
																													{
																														if (!(packet2 is CharInfoPacket charInfoPacket))
																														{
																															if (!(packet2 is PetActionPacket petActionPacket))
																															{
																																if (packet2 is LogActionPacket logActionPacket)
																																{
																																	logActionPacket.Send = false;
																																	Console.WriteLine($"LogAction {logActionPacket.UnknownShort1} {logActionPacket.Message}");
																																}
																															}
																															else
																															{
																																if (petActionPacket.PetActionType == 1)
																																{
																																	PetHealAmount = 0;
																																	foreach (MapObject value3 in Portals.Values)
																																	{
																																		if (value3.PetInstanceId == petActionPacket.PetInstanceId)
																																		{
																																			if (value3.PetAbilityType0 == 407)
																																			{
																																				PetHealAmount = GetPetHealAmountForLevel(value3.PetLevel0.ToString());
																																			}
																																			else if (value3.PetAbilityType1 == 407)
																																			{
																																				PetHealAmount = GetPetHealAmountForLevel(value3.PetLevel1.ToString());
																																			}
																																			else if (value3.PetAbilityType2 == 407)
																																			{
																																				PetHealAmount = GetPetHealAmountForLevel(value3.PetLevel2.ToString());
																																			}
																																		}
																																	}
																																	Console.WriteLine($"Set pet heal {PetHealAmount} via pet follow");
																																}
																																else
																																{
																																	PetHealAmount = 0;
																																	Console.WriteLine($"Set pet heal {PetHealAmount} via pet unfollow/release");
																																}
																																if (Player != null && PetHealingByAccountAndCharacter.ContainsKey(Player.AccountId))
																																{
																																	PetHealingByAccountAndCharacter[Player.AccountId][CharacterId] = PetHealAmount;
																																}
																															}
																														}
																														else if (charInfoPacket.CharacterXml.StartsWith("<Char "))
																														{
																															string text = ExtractBetween(charInfoPacket.CharacterXml, "<Char id=\"", "\">");
																															if (text == string.Empty)
																															{
																																return;
																															}
																															int key = int.Parse(text);
																															string text2 = ExtractBetween(charInfoPacket.CharacterXml, "<AccountId>", "</AccountId>");
																															if (text2 == string.Empty)
																															{
																																return;
																															}
																															int value = (PetHealAmount = ReadPetHealAmountFromXml(charInfoPacket.CharacterXml));
																															Console.WriteLine($"Set pet heal {PetHealAmount} from New Char Info");
																															if (PetHealingByAccountAndCharacter.ContainsKey(text2))
																															{
																																PetHealingByAccountAndCharacter[text2].Add(key, value);
																															}
																															else
																															{
																																SendNotification("unhandled edge case, report this please");
																															}
																														}
																													}
																													else
																													{
																														entityTrackerMod.OnVaultContent(vaultContentPacket);
																													}
																												}
																												else
																												{
																													autoAbilityMod.OnOtherHit(abilitySelectionPacket);
																												}
																											}
																											else
																											{
																												autoLootMod.OnDashConsumed();
																											}
																										}
																										else
																										{
																											playerTrackerMod.OnNotification(notificationPacket);
																											dpsTracker?.OnNotification(notificationPacket);
																											moonlightVillageMod?.OnNotification(notificationPacket);
																										}
																									}
																									else
																									{
																										entityTrackerMod.OnInvSwap(inventoryResultPacket);
																										autoLootMod.OnInventoryResult(inventoryResultPacket);
																										autoAbilityMod.OnInvSwap(inventoryResultPacket);
																									}
																								}
																								else if (_currentServerName != "Tutorial")
																								{
																									ReconnectToNexus();
																								}
																							}
																							else
																							{
																								autoLootMod.OnInventorySwapRequest(inventorySwapPacket);
																								autoAbilityMod.OnInvDrop(inventorySwapPacket);
																							}
																						}
																						else
																						{
																							oryxSanctuaryMod.OnEnemyHit(enemyHitPacket);
																							dpsTracker.OnEnemyHit(enemyHitPacket);
																						}
																					}
																					else
																					{
																						entityTrackerMod.OnServerPlayerShoot(serverPlayerShootPacket);
																						dpsTracker.OnServerPlayerShoot(serverPlayerShootPacket);
																					}
																				}
																				else
																				{
																					autoAbilityMod.OnUseItem(useItemPacket);
																					entityTrackerMod.OnSlotObjectUpdate(useItemPacket.Item);
																					dpsTracker?.OnUseItem(useItemPacket);
																				}
																			}
																			else
																			{
																				spamFilterMod.OnText(textPacket);
																				antiLagMod.OnText(textPacket);
																				oryxSanctuaryMod.OnText(textPacket);
																				followMod.OnText(textPacket);
																				dpsTracker?.OnText(textPacket);
																				moonlightVillageMod?.OnText(textPacket);
																			}
																		}
																		else
																		{
																			autoAbilityMod.OnPlayerShoot(playerShootPacket);
																			followMod.OnPlayerShoot(playerShootPacket);
																			dpsTracker.OnPlayerShoot(playerShootPacket);
																		}
																	}
																	else
																	{
																		antiLagMod.OnEnemyShoot(enemyShootPacket);
																		dpsTracker.OnEnemyShootDamage(enemyShootPacket);
																	}
																}
																else
																{
																	antiLagMod.OnShowEffect(showEffectPacket);
																	oryxSanctuaryMod.OnShowEffect(showEffectPacket);
																	playerTrackerMod.OnShowEffect(showEffectPacket);
																	dpsTracker.OnShowEffect(showEffectPacket);
																	moonlightVillageMod?.OnShowEffect(showEffectPacket);
																}
															}
															else
															{
																commandMod.OnQuestTarget(questTargetPacket);
															}
														}
														else
														{
															playerTrackerMod.OnGroundDamage(groundDamagePacket);
														}
													}
													else
													{
														playerTrackerMod.OnGotoAck(positionAckPacket);
														autoAbilityMod.OnGotoAck(positionAckPacket);
													}
												}
												else
												{
													playerTrackerMod.OnAoe(aoePacket);
													antiDebuffMod.OnAoe(aoePacket);
												}
											}
											else
											{
												playerTrackerMod.OnShootAck(playerHitPacket);
												antiDebuffMod.OnPlayerHit(playerHitPacket);
											}
										}
										else
										{
											helloPacket.BuildVersion = Program.GameVersion;
											HandleHelloPacket(helloPacket);
										}
									}
									else
									{
										HandleReconnectPacket(reconnectPacket);
									}
								}
								else if (failurePacket.ErrorId == 0 && string.IsNullOrEmpty(failurePacket.ErrorMessage) && ClientId == -1)
								{
									Program.LogInfo("client", "Failure (Pseudo Account in Use)");
								}
								else
								{
									Program.LogInfo("client", $"Failure {failurePacket.ErrorId} ({failurePacket.ErrorMessage}) {failurePacket}");
									try
									{
										File.AppendAllText("disconnect_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [FailurePacket] Code={failurePacket.ErrorId} Msg='{failurePacket.ErrorMessage}'\r\n");
									}
									catch
									{
									}
									SendNotification($"Server Kick: {failurePacket.ErrorMessage} (Code {failurePacket.ErrorId})");
								}
							}
							else
							{
								ClientId = gotoPacket.ObjectId;
								CharacterId = gotoPacket.CharId;
								if (Entities != null && Entities.TryGetValue(ClientId, out var value2))
								{
									Player = value2;
									playerTrackerMod.UpdateHealthThresholds();
								}
								entityTrackerMod.ScheduleConnectionNotice();
								bazaarTimerMod.ResetTimer();
								playerTrackerMod.CheckAutoNexusEnabled();
							}
						}
						else
						{
							entityTrackerMod.OnGenericFailure(genericFailurePacket);
							commandMod.HandleCommand(genericFailurePacket);
							reconnectMod.HandleGenericFailure(genericFailurePacket);
							playerTrackerMod.OnGenericFailure(genericFailurePacket);
							autoAbilityMod.OnGenericFailure(genericFailurePacket);
							followMod.HandleCommand(genericFailurePacket);
							dpsTracker.HandleCommand(genericFailurePacket);
							moonlightVillageMod?.HandleCommand(genericFailurePacket);
						}
					}
					else
					{
						Program.LogInfo("client", "Map is " + mapInfoPacket.MapName);
						entityTrackerMod.OnMapInfo(mapInfoPacket);
						autoAbilityMod.OnMapInfo(mapInfoPacket);
						safeWalkMod.OnMapInfo(mapInfoPacket);
						playerTrackerMod.OnMapInfo(mapInfoPacket);
						autoLootMod.OnMapInfo(mapInfoPacket);
						antiLagMod.OnMapInfo(mapInfoPacket);
						lostHallsMod.OnMapInfo(mapInfoPacket);
						oryxSanctuaryMod.OnMapInfo(mapInfoPacket);
						followMod.OnMapChange();
						dpsTracker.OnMapChange(mapInfoPacket);
						moonlightVillageMod?.OnMapInfo(mapInfoPacket);
					}
				}
				else
				{
					_serverTime = movePacket.Time;
					_lastTickTime = Environment.TickCount;
					followMod.OnMove(movePacket);
					entityTrackerMod.OnMove(movePacket);
					autoLootMod.OnMove(movePacket);
					antiAfkMod.OnMove(movePacket);
					playerTrackerMod.OnMove(movePacket);
					bazaarTimerMod.OnMove(movePacket);
				}
			}
			else
			{
				_clientTime = newTickPacket.TickId;
				playerTrackerMod.OnNewTickAuthoritative(newTickPacket);
				entityTrackerMod.OnNewTick(newTickPacket);
				playerTrackerMod.OnNewTickPredicted(newTickPacket);
				antiDebuffMod.OnNewTick(newTickPacket);
				antiLagMod.OnNewTick(newTickPacket);
				autoAbilityMod.OnNewTick(newTickPacket);
				autoLootMod.OnNewTick(newTickPacket);
				oryxSanctuaryMod.OnNewTick(newTickPacket);
				followMod.OnNewTick(newTickPacket);
				dpsTracker.OnNewTick(newTickPacket);
				moonlightVillageMod?.OnNewTick(newTickPacket);
			}
		}
		else
		{
			entityTrackerMod.OnUpdate(updatePacket);
			playerTrackerMod.OnUpdateNewObjects(updatePacket);
			playerTrackerMod.OnUpdateExistingObjects(updatePacket);
			safeWalkMod.OnUpdate(updatePacket);
			antiLagMod.OnUpdate(updatePacket);
			autoAbilityMod.OnUpdate(updatePacket);
			autoLootMod.OnUpdate(updatePacket);
			lostHallsMod.OnUpdate(updatePacket);
			oryxSanctuaryMod.OnUpdate(updatePacket);
			followMod.OnUpdate(updatePacket);
			dpsTracker.OnUpdate(updatePacket);
			moonlightVillageMod?.OnUpdate(updatePacket);
		}
		if (packet2.Id == 46)
		{
			playerTrackerMod.OnDeath();
		}
	}

	private void CopyTextToClipboard(string text)
	{
		SendNotification("Copied text!");
		Thread thread = new Thread((ThreadStart)delegate
		{
			Clipboard.SetText(text);
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
	}

	private void HandleHelloPacket(HelloPacket packet)
	{
		_remoteClient = new TcpClient
		{
			NoDelay = true
		};
		string key = FormatBytesToHex(packet.Key);
		if (packet.Key.Length != 0 && ServerProxy.PendingReconnectionsAlias.ContainsKey(key))
		{
			ReconnectPacket reconnectPacket = ServerProxy.PendingReconnectionsAlias[key];
			packet.GameId = reconnectPacket.GameId;
			packet.Key = reconnectPacket.Key;
			packet.KeyTime = reconnectPacket.KeyTime;
			_remoteClient.BeginConnect(reconnectPacket.Host, reconnectPacket.Port, OnRemoteConnected, packet);
			Program.LogInfo("client", "Restored reconnect info.");
		}
		else
		{
			string stringSetting = RegistryHelper.GetStringSetting("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", "ProductionpreferredServer_");
			string host = (ServerManager.ServersByNameAlias.ContainsKey(stringSetting) ? ServerManager.ServersByNameAlias[stringSetting] : ServerManager.ServersByNameAlias.First().Value);
			_remoteClient.BeginConnect(host, 2050, OnRemoteConnected, packet);
			Program.LogInfo("client", "Used default connect info.");
		}
		_pendingKeyBytes = packet.Key;
		_reconnectKeyTime = packet.KeyTime;
		_reconnectGameId = packet.GameId;
		packet.Send = false;
	}

	private void HandleReconnectPacket(ReconnectPacket packet)
	{
		if (packet.Key.Length != 0)
		{
			if (packet.Host.Contains(".com"))
			{
				packet.Host = Dns.GetHostEntry(packet.Host).AddressList[0].ToString();
			}
			ReconnectPacket reconnectPacket = new ReconnectPacket();
			reconnectPacket.GameId = packet.GameId;
			reconnectPacket.Host = (string.IsNullOrWhiteSpace(packet.Host) ? (_remoteClient.Client.RemoteEndPoint as IPEndPoint).Address.ToString() : packet.Host);
			reconnectPacket.Port = (ushort)((packet.Port == 0) ? (_remoteClient.Client.RemoteEndPoint as IPEndPoint).Port : packet.Port);
			reconnectPacket.Key = packet.Key;
			reconnectPacket.KeyTime = packet.KeyTime;
			reconnectPacket.MapName = packet.MapName;
			string key = FormatBytesToHex(packet.Key);
			if (ServerProxy.PendingReconnectionsAlias.ContainsKey(key))
			{
				ServerProxy.PendingReconnectionsAlias.TryRemove(key, out var _);
			}
			ServerProxy.PendingReconnectionsAlias.TryAdd(key, reconnectPacket);
			packet.Host = "127.0.0.1";
			packet.Port = ProxyServer.LocalPortAlias;
			Program.LogInfo("client", "Stored reconnect info.");
		}
	}

	public void ReconnectToNexus()
	{
		ReconnectPacket reconnectPacket = new ReconnectPacket();
		reconnectPacket.GameId = -2;
		reconnectPacket.Host = string.Empty;
		reconnectPacket.Port = 0;
		reconnectPacket.Key = new byte[0];
		reconnectPacket.KeyTime = -1;
		reconnectPacket.MapName = "Nexus";
		SendToClient(reconnectPacket);
		Program.LogInfo("client", "Sent nexus reconnect.");
	}

	public static string FormatBytesToHex(byte[] bytes)
	{
		StringBuilder stringBuilder = new StringBuilder(bytes.Length * 2);
		foreach (byte b in bytes)
		{
			stringBuilder.AppendFormat("{0:x2}", b);
		}
		return stringBuilder.ToString();
	}

	public byte GetNextBulletId()
	{
		byte result = _nextBulletId;
		_nextBulletId = (byte)((_nextBulletId + 1) % 128);
		return result;
	}

	public MapTile GetTileAtPosition(WorldPosData position)
	{
		return GetTileAtCoords((int)position.X, (int)position.Y);
	}

	public MapTile GetTileAtCoords(int x, int y)
	{
		int num = x * MapWidth + y;
		return Tiles[num];
	}

	public void SendNotification(string name, string message)
	{
		if (!Settings.Default.DisableSystemMessages)
		{
			SendToClient(TextPacket.CreateNotificationText(name, message));
		}
	}

	public void SendNotification(string message)
	{
		if (!Settings.Default.DisableSystemMessages)
		{
			SendToClient(TextPacket.CreateSystemText(message));
		}
	}

	public void ConnectToNamedServer(string serverName)
	{
		string value2;
		if (ServerManager.ServersByNameAlias.TryGetValue(serverName, out var value))
		{
			RegistryHelper.SetStringSetting("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", ServerManager.PreferredServerPrefKeyAlias, serverName);
			ConnectServer(value);
		}
		else if (ServerManager.ServerAbbreviationsAlias.TryGetValue(serverName, out value2))
		{
			RegistryHelper.SetStringSetting("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", ServerManager.PreferredServerPrefKeyAlias, value2);
			ConnectServer(ServerManager.ServersByNameAlias[value2]);
		}
		else if (ReconnectMod.IsReconnectCommand(serverName))
		{
			ConnectServer(serverName);
		}
		else
		{
			SendNotification("Unknown server!");
		}
	}

	public void ConnectServer(string serverIp)
	{
		string s = Guid.NewGuid().ToString();
		byte[] bytes = Encoding.ASCII.GetBytes(s);
		string key = FormatBytesToHex(bytes);
		ReconnectPacket reconnectPacket = new ReconnectPacket();
		reconnectPacket.GameId = -2;
		reconnectPacket.Host = serverIp;
		reconnectPacket.Port = 2050;
		reconnectPacket.Key = new byte[0];
		reconnectPacket.KeyTime = -1;
		reconnectPacket.MapName = "Recon";
		ReconnectPacket reconnectPacket2 = new ReconnectPacket();
		reconnectPacket2.GameId = -2;
		reconnectPacket2.Host = "127.0.0.1";
		reconnectPacket2.Port = ProxyServer.LocalPortAlias;
		reconnectPacket2.Key = bytes;
		reconnectPacket2.KeyTime = -1;
		reconnectPacket2.MapName = "Realm";
		ServerProxy.PendingReconnectionsAlias[key] = reconnectPacket;
		SendToClient(reconnectPacket2);
	}

	public void ResetHp()
	{
		playerTrackerMod.CleanupTrackingState();
	}

	public void TeleportAnchor()
	{
		commandMod.TeleportToAnchor();
	}

	public static IEnumerable<int> ParseStatValues(string encodedItemData)
	{
		List<int> list = new List<int>();
		if (string.IsNullOrEmpty(encodedItemData))
		{
			return null;
		}
		byte[] array = Convert.FromBase64String(encodedItemData.Replace('-', '+').Replace('_', '/'));
		if (array.Length == 0)
		{
			return null;
		}
		int num = 0;
		_ = array[num];
		num++;
		while (num < array.Length)
		{
			byte b = array[num];
			num++;
			switch (b)
			{
			case 1:
			{
				_ = array[num];
				num++;
				byte b3 = array[num];
				num++;
				num += b3 * 2;
				break;
			}
			case 2:
			{
				byte b4 = array[num];
				num++;
				for (int j = 0; j < b4; j++)
				{
					short item = BitConverter.ToInt16(array, num);
					num += 2;
					list.Add(item);
				}
				break;
			}
			case 3:
			{
				ushort num2 = BitConverter.ToUInt16(array, num);
				num += 2;
				num += num2 * 2;
				break;
			}
			case 4:
				_ = array[num];
				num++;
				break;
			case 5:
			{
				byte b2 = array[num];
				num++;
				for (int i = 0; i < b2; i++)
				{
					_ = array[num];
					num++;
					BitConverter.ToUInt16(array, num);
					num += 2;
				}
				break;
			}
			}
		}
		return list;
	}

	public void SetEquipmentManaCostMultiplier(float multiplier)
	{
		autoAbilityMod.EquipmentManaCostMultiplier = multiplier;
	}

	public void SetEnchantmentManaCostMultiplier(float multiplier)
	{
		autoAbilityMod.EnchantmentManaCostMultiplier = multiplier;
	}

	public void LoadPetHealingForAccount(string accountId)
	{
		if (!PetHealingByAccountAndCharacter.ContainsKey(accountId))
		{
			string stringSetting = RegistryHelper.GetStringSetting("SOFTWARE\\RealmStock\\MultiTool", "CharList_" + accountId);
			if (string.IsNullOrEmpty(stringSetting))
			{
				SendNotification("Unable to view pet data via registry, please enter your pet yard and unfollow then refollow your pet, otherwise autonexus will not detect pet heals properly.");
				return;
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			string[] array = stringSetting.Split(new string[1] { "</Char>" }, StringSplitOptions.None);
			foreach (string text in array)
			{
				if (!text.StartsWith("<Account>"))
				{
					string text2 = ExtractBetween(text, "<Char id=\"", "\">");
					if (!(text2 == string.Empty))
					{
						int value = ReadPetHealAmountFromXml(text);
						dictionary[int.Parse(text2)] = value;
					}
				}
			}
			PetHealingByAccountAndCharacter.Add(accountId, dictionary);
			if (dictionary.ContainsKey(CharacterId))
			{
				PetHealAmount = dictionary[CharacterId];
				Console.WriteLine($"Set pet heal {PetHealAmount} from registry");
			}
		}
		else
		{
			Dictionary<int, int> dictionary2 = PetHealingByAccountAndCharacter[accountId];
			if (dictionary2.ContainsKey(CharacterId))
			{
				PetHealAmount = dictionary2[CharacterId];
				Console.WriteLine($"Set pet heal {PetHealAmount} via existing");
			}
		}
	}

	public static int ReadPetHealAmountFromXml(string characterXml)
	{
		string text = ExtractBetween(characterXml, "<Pet ", "</Pet>");
		if (text == string.Empty)
		{
			return 0;
		}
		string[] array = ExtractBetween(text, "<Abilities>", "</Abilities>").Split(new string[1] { "<Ability " }, StringSplitOptions.RemoveEmptyEntries);
		int result = 0;
		string[] array2 = array;
		foreach (string abilityXml in array2)
		{
			if (!(ExtractBetween(abilityXml, "type=\"", "\"") != "407"))
			{
				result = GetPetHealAmountForLevel(ExtractBetween(abilityXml, "power=\"", "\""));
			}
		}
		return result;
	}

	public static string ExtractBetween(string text, string startDelimiter, string endDelimiter, string unusedFallback = null)
	{
		int num = text.IndexOf(startDelimiter);
		if (num == -1)
		{
			return string.Empty;
		}
		num += startDelimiter.Length;
		int num2 = text.IndexOf(endDelimiter, num);
		if (num2 == -1)
		{
			return string.Empty;
		}
		return text.Substring(num, num2 - num);
	}

	public static int GetPetHealAmountForLevel(string abilityLevel)
	{
		switch (StringHash.ComputeStringHash(abilityLevel))
		{
		case 873244444u:
			if (abilityLevel == "1")
			{
				return 10;
			}
			break;
		case 923577301u:
			if (abilityLevel == "2")
			{
				return 10;
			}
			break;
		case 906799682u:
			if (abilityLevel == "3")
			{
				return 10;
			}
			break;
		case 822911587u:
			if (abilityLevel == "4")
			{
				return 10;
			}
			break;
		case 806133968u:
			if (abilityLevel == "5")
			{
				return 10;
			}
			break;
		case 856466825u:
			if (abilityLevel == "6")
			{
				return 10;
			}
			break;
		case 839689206u:
			if (abilityLevel == "7")
			{
				return 10;
			}
			break;
		case 1024243015u:
			if (abilityLevel == "8")
			{
				return 10;
			}
			break;
		case 1007465396u:
			if (abilityLevel == "9")
			{
				return 10;
			}
			break;
		case 468396612u:
			if (abilityLevel == "10")
			{
				return 10;
			}
			break;
		case 485174231u:
			if (abilityLevel == "11")
			{
				return 10;
			}
			break;
		case 501951850u:
			if (abilityLevel == "12")
			{
				return 10;
			}
			break;
		case 518729469u:
			if (abilityLevel == "13")
			{
				return 10;
			}
			break;
		case 401286136u:
			if (abilityLevel == "14")
			{
				return 10;
			}
			break;
		case 418063755u:
			if (abilityLevel == "15")
			{
				return 11;
			}
			break;
		case 434841374u:
			if (abilityLevel == "16")
			{
				return 11;
			}
			break;
		case 451618993u:
			if (abilityLevel == "17")
			{
				return 11;
			}
			break;
		case 334175660u:
			if (abilityLevel == "18")
			{
				return 11;
			}
			break;
		case 350953279u:
			if (abilityLevel == "19")
			{
				return 11;
			}
			break;
		case 2381486463u:
			if (abilityLevel == "20")
			{
				return 11;
			}
			break;
		case 2364708844u:
			if (abilityLevel == "21")
			{
				return 12;
			}
			break;
		case 2415041701u:
			if (abilityLevel == "22")
			{
				return 12;
			}
			break;
		case 2398264082u:
			if (abilityLevel == "23")
			{
				return 12;
			}
			break;
		case 2314375987u:
			if (abilityLevel == "24")
			{
				return 12;
			}
			break;
		case 2297598368u:
			if (abilityLevel == "25")
			{
				return 12;
			}
			break;
		case 2347931225u:
			if (abilityLevel == "26")
			{
				return 13;
			}
			break;
		case 2331153606u:
			if (abilityLevel == "27")
			{
				return 13;
			}
			break;
		case 2515707415u:
			if (abilityLevel == "28")
			{
				return 13;
			}
			break;
		case 2498929796u:
			if (abilityLevel == "29")
			{
				return 14;
			}
			break;
		case 2280673654u:
			if (abilityLevel == "30")
			{
				return 14;
			}
			break;
		case 2297451273u:
			if (abilityLevel == "31")
			{
				return 14;
			}
			break;
		case 2247118416u:
			if (abilityLevel == "32")
			{
				return 14;
			}
			break;
		case 2263896035u:
			if (abilityLevel == "33")
			{
				return 15;
			}
			break;
		case 2347784130u:
			if (abilityLevel == "34")
			{
				return 15;
			}
			break;
		case 2364561749u:
			if (abilityLevel == "35")
			{
				return 16;
			}
			break;
		case 2314228892u:
			if (abilityLevel == "36")
			{
				return 16;
			}
			break;
		case 2331006511u:
			if (abilityLevel == "37")
			{
				return 16;
			}
			break;
		case 2414894606u:
			if (abilityLevel == "38")
			{
				return 17;
			}
			break;
		case 2431672225u:
			if (abilityLevel == "39")
			{
				return 17;
			}
			break;
		case 2313390249u:
			if (abilityLevel == "40")
			{
				return 18;
			}
			break;
		case 2296612630u:
			if (abilityLevel == "41")
			{
				return 18;
			}
			break;
		case 2279835011u:
			if (abilityLevel == "42")
			{
				return 19;
			}
			break;
		case 2263057392u:
			if (abilityLevel == "43")
			{
				return 19;
			}
			break;
		case 2380500725u:
			if (abilityLevel == "44")
			{
				return 20;
			}
			break;
		case 2363723106u:
			if (abilityLevel == "45")
			{
				return 20;
			}
			break;
		case 2346945487u:
			if (abilityLevel == "46")
			{
				return 21;
			}
			break;
		case 2330167868u:
			if (abilityLevel == "47")
			{
				return 21;
			}
			break;
		case 2447611201u:
			if (abilityLevel == "48")
			{
				return 22;
			}
			break;
		case 2430833582u:
			if (abilityLevel == "49")
			{
				return 22;
			}
			break;
		case 2212577440u:
			if (abilityLevel == "50")
			{
				return 23;
			}
			break;
		case 2229355059u:
			if (abilityLevel == "51")
			{
				return 24;
			}
			break;
		case 2246132678u:
			if (abilityLevel == "52")
			{
				return 24;
			}
			break;
		case 2262910297u:
			if (abilityLevel == "53")
			{
				return 25;
			}
			break;
		case 2279687916u:
			if (abilityLevel == "54")
			{
				return 26;
			}
			break;
		case 2296465535u:
			if (abilityLevel == "55")
			{
				return 26;
			}
			break;
		case 2313243154u:
			if (abilityLevel == "56")
			{
				return 27;
			}
			break;
		case 2330020773u:
			if (abilityLevel == "57")
			{
				return 28;
			}
			break;
		case 2346798392u:
			if (abilityLevel == "58")
			{
				return 29;
			}
			break;
		case 2363576011u:
			if (abilityLevel == "59")
			{
				return 29;
			}
			break;
		case 367583803u:
			if (abilityLevel == "60")
			{
				return 30;
			}
			break;
		case 350806184u:
			if (abilityLevel == "61")
			{
				return 31;
			}
			break;
		case 401139041u:
			if (abilityLevel == "62")
			{
				return 32;
			}
			break;
		case 384361422u:
			if (abilityLevel == "63")
			{
				return 33;
			}
			break;
		case 434694279u:
			if (abilityLevel == "64")
			{
				return 34;
			}
			break;
		case 417916660u:
			if (abilityLevel == "65")
			{
				return 35;
			}
			break;
		case 468249517u:
			if (abilityLevel == "66")
			{
				return 36;
			}
			break;
		case 451471898u:
			if (abilityLevel == "67")
			{
				return 37;
			}
			break;
		case 233362851u:
			if (abilityLevel == "68")
			{
				return 38;
			}
			break;
		case 216585232u:
			if (abilityLevel == "69")
			{
				return 39;
			}
			break;
		case 2414203058u:
			if (abilityLevel == "70")
			{
				return 40;
			}
			break;
		case 2430980677u:
			if (abilityLevel == "71")
			{
				return 41;
			}
			break;
		case 2380647820u:
			if (abilityLevel == "72")
			{
				return 42;
			}
			break;
		case 2397425439u:
			if (abilityLevel == "73")
			{
				return 43;
			}
			break;
		case 2347092582u:
			if (abilityLevel == "74")
			{
				return 45;
			}
			break;
		case 2363870201u:
			if (abilityLevel == "75")
			{
				return 46;
			}
			break;
		case 2313537344u:
			if (abilityLevel == "76")
			{
				return 47;
			}
			break;
		case 2330314963u:
			if (abilityLevel == "77")
			{
				return 48;
			}
			break;
		case 2548424010u:
			if (abilityLevel == "78")
			{
				return 50;
			}
			break;
		case 2565201629u:
			if (abilityLevel == "79")
			{
				return 51;
			}
			break;
		case 2449582677u:
			if (abilityLevel == "80")
			{
				return 53;
			}
			break;
		case 2432805058u:
			if (abilityLevel == "81")
			{
				return 54;
			}
			break;
		case 2416027439u:
			if (abilityLevel == "82")
			{
				return 55;
			}
			break;
		case 2399249820u:
			if (abilityLevel == "83")
			{
				return 57;
			}
			break;
		case 2382472201u:
			if (abilityLevel == "84")
			{
				return 59;
			}
			break;
		case 2365694582u:
			if (abilityLevel == "85")
			{
				return 60;
			}
			break;
		case 2348916963u:
			if (abilityLevel == "86")
			{
				return 62;
			}
			break;
		case 2332139344u:
			if (abilityLevel == "87")
			{
				return 64;
			}
			break;
		case 2583803629u:
			if (abilityLevel == "88")
			{
				return 65;
			}
			break;
		case 2567026010u:
			if (abilityLevel == "89")
			{
				return 67;
			}
			break;
		case 201234636u:
			if (abilityLevel == "90")
			{
				return 69;
			}
			break;
		case 218012255u:
			if (abilityLevel == "91")
			{
				return 71;
			}
			break;
		case 234789874u:
			if (abilityLevel == "92")
			{
				return 73;
			}
			break;
		case 251567493u:
			if (abilityLevel == "93")
			{
				return 75;
			}
			break;
		case 134124160u:
			if (abilityLevel == "94")
			{
				return 77;
			}
			break;
		case 150901779u:
			if (abilityLevel == "95")
			{
				return 79;
			}
			break;
		case 167679398u:
			if (abilityLevel == "96")
			{
				return 81;
			}
			break;
		case 184457017u:
			if (abilityLevel == "97")
			{
				return 83;
			}
			break;
		case 335455588u:
			if (abilityLevel == "98")
			{
				return 85;
			}
			break;
		case 352233207u:
			if (abilityLevel == "99")
			{
				return 88;
			}
			break;
		case 1731450012u:
			if (abilityLevel == "100")
			{
				return 90;
			}
			break;
		}
		return 0;
	}
}
