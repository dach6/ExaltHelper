using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class FollowMod : IDisposable
{
	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	private readonly Client client;

	private string followTargetName;

	private string lastFollowTargetName;

	private int followTargetId = -1;

	private volatile bool isFollowing;

	private Thread followThread;

	private IntPtr clientWindowHandle = IntPtr.Zero;

	private bool wasEnterDown;

	private bool wasSlashDown;

	private bool wasTabDown;

	private bool wasEscDown;

	private volatile bool isChatOpen;

	private int chatOpenedTick;

	private bool isWDown;

	private bool isADown;

	private bool isSDown;

	private bool isDDown;

	private int currentDirection = -1;

	private static readonly (bool W, bool A, bool S, bool D)[] DirectionKeys = new(bool, bool, bool, bool)[8]
	{
		(false, false, false, true),
		(false, false, true, true),
		(false, false, true, false),
		(false, true, true, false),
		(false, true, false, false),
		(true, true, false, false),
		(true, false, false, false),
		(true, false, false, true)
	};

	private readonly List<WorldPosData> breadcrumbs = new List<WorldPosData>();

	private WorldPosData lastAuthoritativeServerPos;

	private WorldPosData latestClientPos;

	private int lastClientMoveTime;

	private int lastTargetSeenTick;

	private int mySpeedStat = 65;

	private double lastLeaderPosX;

	private double lastLeaderPosY;

	private int lastLeaderTickTime;

	private const double MaxActivationDistance = 10.0;

	private const double MaxFollowDistance = 30.0;

	private const double StopDistance = 0.25;

	private const uint WM_KEYDOWN = 256u;

	private const uint WM_KEYUP = 257u;

	private const uint KEYEVENTF_KEYUP = 2u;

	private const int SW_RESTORE = 9;

	private static double NormalizeAngleDiff(double diff)
	{
		while (diff > 180.0)
		{
			diff -= 360.0;
		}
		while (diff < -180.0)
		{
			diff += 360.0;
		}
		return diff;
	}

	private static int GetBestDirection(double targetAngleDeg, int currentDir)
	{
		if (currentDir >= 0 && currentDir < 8)
		{
			double num = (double)currentDir * 45.0;
			if (Math.Abs(NormalizeAngleDiff(targetAngleDeg - num)) <= 30.5)
			{
				return currentDir;
			}
		}
		int result = 0;
		double num2 = double.MaxValue;
		for (int i = 0; i < 8; i++)
		{
			double num3 = (double)i * 45.0;
			double num4 = Math.Abs(NormalizeAngleDiff(targetAngleDeg - num3));
			if (num4 < num2)
			{
				num2 = num4;
				result = i;
			}
		}
		return result;
	}

	[DllImport("user32.dll")]
	private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint uCode, uint uMapType);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	[DllImport("iphlpapi.dll", SetLastError = true)]
	private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0u);

	public FollowMod(Client client2)
	{
		client = client2;
	}

	public void HandleCommand(GenericFailurePacket packet)
	{
		if (packet.IsCommand("unfollow", out var commandArguments) || packet.IsCommand("u", out commandArguments) || packet.IsCommand("stop", out commandArguments) || packet.IsCommand("s", out commandArguments))
		{
			packet.Send = false;
			StopFollowing("Follow mode disabled.");
		}
		else
		{
			if (!packet.IsCommand("follow", out commandArguments) && !packet.IsCommand("f", out commandArguments))
			{
				return;
			}
			packet.Send = false;
			if (commandArguments.Length == 0)
			{
				if (isFollowing)
				{
					StopFollowing("Follow mode disabled.");
				}
				else if (!string.IsNullOrEmpty(lastFollowTargetName))
				{
					StartFollowing(lastFollowTargetName);
				}
				else
				{
					client.SendNotification("Usage: /follow <player name> or /u");
				}
			}
			else
			{
				string searchName = string.Join(" ", commandArguments);
				StartFollowing(searchName);
			}
		}
	}

	public void StartFollowing(string searchName)
	{
		if (string.IsNullOrWhiteSpace(searchName))
		{
			return;
		}
		searchName = searchName.Trim().ToLower();
		MapObject Player = client.Player;
		if (Player == null)
		{
			client.SendNotification("Cannot follow: your player is not loaded yet.");
			return;
		}
		MapObject mapObject = null;
		lock (client.Entities)
		{
			foreach (MapObject value in client.Entities.Values)
			{
				if (value.ObjectId != Player.ObjectId && !value.IsInvisible() && value.Name != null && value.Name.ToLower().Contains(searchName))
				{
					mapObject = value;
					break;
				}
			}
		}
		if (mapObject == null)
		{
			client.SendNotification("Player \"" + searchName + "\" not found nearby.");
			return;
		}
		double num = Player.Position.DistanceTo(mapObject.Position);
		if (num > 10.0)
		{
			client.SendNotification("Player \"" + mapObject.PlayerName + "\" is too far away (" + Math.Round(num, 1) + " tiles). Must be within " + 10.0 + " tiles.");
			return;
		}
		followTargetName = mapObject.PlayerName;
		lastFollowTargetName = mapObject.PlayerName;
		followTargetId = mapObject.ObjectId;
		isFollowing = true;
		isChatOpen = false;
		lastAuthoritativeServerPos = (WorldPosData)Player.Position.Clone();
		latestClientPos = (WorldPosData)Player.Position.Clone();
		lastClientMoveTime = 0;
		lastTargetSeenTick = Environment.TickCount;
		lastLeaderPosX = 0.0;
		lastLeaderPosY = 0.0;
		lastLeaderTickTime = 0;
		lock (breadcrumbs)
		{
			breadcrumbs.Clear();
		}
		clientWindowHandle = GetGameWindowHandle();
		if (clientWindowHandle != IntPtr.Zero)
		{
			try
			{
				if (IsIconic(clientWindowHandle))
				{
					ShowWindow(clientWindowHandle, 9);
				}
				SetForegroundWindow(clientWindowHandle);
			}
			catch
			{
			}
		}
		if (followThread == null || !followThread.IsAlive)
		{
			followThread = new Thread(FollowLoop)
			{
				IsBackground = true,
				Name = "FollowMod_InputController"
			};
			followThread.Start();
		}
		client.SendNotification("Now following " + mapObject.PlayerName + "! Stop: /u, F10, or leader says 'stop'.");
	}

	public void OnText(TextPacket packet)
	{
		if (packet == null)
		{
			return;
		}
		string text = packet.Name;
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		int num = text.IndexOf(',');
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		text = text.TrimStart('#', '@').Trim();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		string text2 = (packet.CleanText ?? packet.Text ?? "").Trim().ToLower();
		if (string.IsNullOrEmpty(text2))
		{
			return;
		}
		int num2;
		switch (text2)
		{
		default:
			num2 = ((text2 == "s") ? 1 : 0);
			break;
		case "unfollow":
		case "/unfollow":
		case "stop":
		case "/stop":
		case "stay":
		case "halt":
		case "wait":
		case "u":
		case "/u":
			num2 = 1;
			break;
		}
		bool flag = (byte)num2 != 0;
		int num3;
		switch (text2)
		{
		default:
			num3 = ((text2 == "/f") ? 1 : 0);
			break;
		case "follow":
		case "/follow":
		case "f":
			num3 = 1;
			break;
		}
		bool flag2 = (byte)num3 != 0;
		bool flag3 = !string.IsNullOrEmpty(followTargetName) && string.Equals(text, followTargetName, StringComparison.OrdinalIgnoreCase);
		bool flag4 = !string.IsNullOrEmpty(lastFollowTargetName) && string.Equals(text, lastFollowTargetName, StringComparison.OrdinalIgnoreCase);
		bool flag5 = client.Player != null && !string.IsNullOrEmpty(client.Player.PlayerName) && string.Equals(packet.Recipient, client.Player.PlayerName, StringComparison.OrdinalIgnoreCase);
		if (isFollowing)
		{
			if (flag && (flag3 | flag5))
			{
				StopFollowing("Follow stopped by " + text + ".");
			}
		}
		else if (flag2 && (flag4 | flag5))
		{
			StartFollowing(text);
		}
	}

	private static bool IsHotkeyDown(Keys key)
	{
		return (GetAsyncKeyState((int)key) & 0x8000) != 0;
	}

	private void CheckChatState()
	{
		bool flag = (GetAsyncKeyState(13) & 0x8000) != 0;
		bool flag2 = (GetAsyncKeyState(191) & 0x8000) != 0 || (GetAsyncKeyState(111) & 0x8000) != 0;
		bool flag3 = (GetAsyncKeyState(9) & 0x8000) != 0;
		bool flag4 = (GetAsyncKeyState(27) & 0x8000) != 0;
		int tickCount = Environment.TickCount;
		if (flag && !wasEnterDown)
		{
			isChatOpen = !isChatOpen;
			if (isChatOpen)
			{
				chatOpenedTick = tickCount;
			}
		}
		else if (flag2 && !wasSlashDown)
		{
			if (!isChatOpen)
			{
				isChatOpen = true;
				chatOpenedTick = tickCount;
			}
		}
		else if (flag3 && !wasTabDown)
		{
			if (!isChatOpen)
			{
				isChatOpen = true;
				chatOpenedTick = tickCount;
			}
		}
		else if (flag4 && !wasEscDown)
		{
			isChatOpen = false;
		}
		if (isChatOpen && tickCount - chatOpenedTick > 15000)
		{
			isChatOpen = false;
		}
		wasEnterDown = flag;
		wasSlashDown = flag2;
		wasTabDown = flag3;
		wasEscDown = flag4;
	}

	private void FollowLoop()
	{
		while (isFollowing)
		{
			try
			{
				IntPtr gameWindowHandle = GetGameWindowHandle();
				if (gameWindowHandle != IntPtr.Zero)
				{
					if (IsIconic(gameWindowHandle))
					{
						ShowWindow(gameWindowHandle, 9);
					}
					if (IsHotkeyDown(Keys.Pause) || (IsGameWindowFocused(gameWindowHandle) && (IsHotkeyDown(Keys.F10) || IsHotkeyDown(Keys.End))))
					{
						StopFollowing("Follow stopped via hotkey.");
						break;
					}
					if (IsGameWindowFocused(gameWindowHandle))
					{
						CheckChatState();
					}
					else
					{
						isChatOpen = false;
					}
					if (isChatOpen)
					{
						ReleaseAllKeys(gameWindowHandle);
					}
					else
					{
						UpdateKeyboardMovement(gameWindowHandle);
					}
				}
			}
			catch (Exception ex)
			{
				Program.LogWarning("FollowMod", "FollowLoop error: " + ex.Message);
			}
			Thread.Sleep(25);
		}
		ReleaseAllKeys(GetGameWindowHandle());
	}

	private void UpdateKeyboardMovement(IntPtr hWnd)
	{
		MapObject Player = client.Player;
		if (Player == null || Player.Position == null)
		{
			ReleaseAllKeys(hWnd);
			return;
		}
		WorldPosData worldPosData = latestClientPos ?? lastAuthoritativeServerPos ?? Player.Position;
		if (worldPosData == null)
		{
			ReleaseAllKeys(hWnd);
			return;
		}
		int tickCount = Environment.TickCount;
		double num = worldPosData.X;
		double num2 = worldPosData.Y;
		if (lastClientMoveTime > 0 && (isWDown || isADown || isSDown || isDDown))
		{
			int num3 = tickCount - lastClientMoveTime;
			if (num3 > 0 && num3 < 400)
			{
				double num4 = 4.0 + 5.6 * ((double)mySpeedStat / 75.0);
				double num5 = (double)num3 / 1000.0;
				double num6 = 0.0;
				double num7 = 0.0;
				if (isDDown)
				{
					num6++;
				}
				if (isADown)
				{
					num6--;
				}
				if (isSDown)
				{
					num7++;
				}
				if (isWDown)
				{
					num7--;
				}
				if (num6 != 0.0 || num7 != 0.0)
				{
					double num8 = Math.Sqrt(num6 * num6 + num7 * num7);
					num += num6 / num8 * num4 * num5;
					num2 += num7 / num8 * num4 * num5;
				}
			}
		}
		MapObject mapObject = null;
		lock (client.Entities)
		{
			if (client.Entities.ContainsKey(followTargetId))
			{
				mapObject = client.Entities[followTargetId];
			}
		}
		if (mapObject == null || mapObject.Position == null)
		{
			ReleaseAllKeys(hWnd);
			return;
		}
		double num9 = ((lastLeaderPosX != 0.0) ? lastLeaderPosX : ((mapObject.TargetPosition != null && mapObject.TargetPosition.X != 0.0) ? mapObject.TargetPosition.X : mapObject.Position.X));
		double num10 = ((lastLeaderPosY != 0.0) ? lastLeaderPosY : ((mapObject.TargetPosition != null && mapObject.TargetPosition.Y != 0.0) ? mapObject.TargetPosition.Y : mapObject.Position.Y));
		double num11 = num9 - num;
		double num12 = num10 - num2;
		double num13 = Math.Sqrt(num11 * num11 + num12 * num12);
		if (num13 <= 0.25)
		{
			lock (breadcrumbs)
			{
				breadcrumbs.Clear();
			}
			ReleaseAllKeys(hWnd);
			return;
		}
		WorldPosData worldPosData2 = new WorldPosData
		{
			X = (float)num9,
			Y = (float)num10
		};
		lock (breadcrumbs)
		{
			while (breadcrumbs.Count > 0 && Math.Sqrt(Math.Pow(breadcrumbs[0].X - num, 2.0) + Math.Pow(breadcrumbs[0].Y - num2, 2.0)) < 1.2)
			{
				breadcrumbs.RemoveAt(0);
			}
			if (num13 <= 5.0 || breadcrumbs.Count == 0)
			{
				worldPosData2 = new WorldPosData
				{
					X = (float)num9,
					Y = (float)num10
				};
			}
			else
			{
				worldPosData2 = breadcrumbs[0];
				for (int i = 0; i < breadcrumbs.Count; i++)
				{
					if (Math.Sqrt(Math.Pow(breadcrumbs[i].X - num, 2.0) + Math.Pow(breadcrumbs[i].Y - num2, 2.0)) >= 2.0)
					{
						worldPosData2 = breadcrumbs[i];
						break;
					}
					worldPosData2 = breadcrumbs[i];
				}
			}
		}
		double x = worldPosData2.X - num;
		double num14 = Math.Atan2(worldPosData2.Y - num2, x) * (180.0 / Math.PI);
		if (num14 < 0.0)
		{
			num14 += 360.0;
		}
		int num15 = (currentDirection = GetBestDirection(num14, currentDirection));
		var (want, want2, want3, want4) = DirectionKeys[num15];
		ApplyKey(hWnd, Keys.W, want, ref isWDown);
		ApplyKey(hWnd, Keys.A, want2, ref isADown);
		ApplyKey(hWnd, Keys.S, want3, ref isSDown);
		ApplyKey(hWnd, Keys.D, want4, ref isDDown);
	}

	private void ApplyKey(IntPtr hWnd, Keys key, bool want, ref bool current)
	{
		uint num = MapVirtualKey((uint)key, 0u);
		if (want)
		{
			if (!current)
			{
				uint num2 = 1 | (num << 16);
				PostMessage(hWnd, 256u, (IntPtr)(int)key, (IntPtr)num2);
				if (IsGameWindowFocused(hWnd))
				{
					keybd_event((byte)key, (byte)num, 0u, UIntPtr.Zero);
				}
				current = true;
			}
			else if (!IsGameWindowFocused(hWnd))
			{
				uint num3 = 1 | (num << 16) | 0x40000000;
				PostMessage(hWnd, 256u, (IntPtr)(int)key, (IntPtr)num3);
			}
		}
		else if (current)
		{
			uint num4 = 1 | (num << 16) | 0x40000000 | 0x80000000u;
			PostMessage(hWnd, 257u, (IntPtr)(int)key, (IntPtr)num4);
			keybd_event((byte)key, (byte)num, 2u, UIntPtr.Zero);
			current = false;
		}
	}

	private static bool IsGameWindowFocused(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		IntPtr foregroundWindow = Win32.GetForegroundWindow();
		if (foregroundWindow == hWnd)
		{
			return true;
		}
		if (foregroundWindow == IntPtr.Zero)
		{
			return false;
		}
		GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
		GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId2);
		if (lpdwProcessId != 0)
		{
			return lpdwProcessId == lpdwProcessId2;
		}
		return false;
	}

	private void ReleaseAllKeys(IntPtr hWnd)
	{
		currentDirection = -1;
		if (isWDown)
		{
			ApplyKey(hWnd, Keys.W, want: false, ref isWDown);
		}
		if (isADown)
		{
			ApplyKey(hWnd, Keys.A, want: false, ref isADown);
		}
		if (isSDown)
		{
			ApplyKey(hWnd, Keys.S, want: false, ref isSDown);
		}
		if (isDDown)
		{
			ApplyKey(hWnd, Keys.D, want: false, ref isDDown);
		}
		keybd_event(87, (byte)MapVirtualKey(87u, 0u), 2u, UIntPtr.Zero);
		keybd_event(65, (byte)MapVirtualKey(65u, 0u), 2u, UIntPtr.Zero);
		keybd_event(83, (byte)MapVirtualKey(83u, 0u), 2u, UIntPtr.Zero);
		keybd_event(68, (byte)MapVirtualKey(68u, 0u), 2u, UIntPtr.Zero);
		isWDown = (isADown = (isSDown = (isDDown = false)));
	}

	public void OnMove(MovePacket packet)
	{
		if (packet.Positions == null || packet.Positions.Length == 0)
		{
			return;
		}
		MapObject Player = client.Player;
		WorldPosData worldPosData = packet.Positions.Last().GetWorldPos();
		latestClientPos = (WorldPosData)worldPosData.Clone();
		lastAuthoritativeServerPos = (WorldPosData)worldPosData.Clone();
		lastClientMoveTime = Environment.TickCount;
		if (Player != null && Player.Position != null)
		{
			Player.Position.X = worldPosData.X;
			Player.Position.Y = worldPosData.Y;
		}
		if (!isFollowing)
		{
			return;
		}
		if (Player == null)
		{
			StopFollowing("Follow stopped: lost player reference.");
			return;
		}
		MapObject mapObject = null;
		lock (client.Entities)
		{
			if (client.Entities.ContainsKey(followTargetId))
			{
				mapObject = client.Entities[followTargetId];
			}
		}
		int tickCount = Environment.TickCount;
		if (mapObject == null || mapObject.Position == null)
		{
			if (lastTargetSeenTick == 0)
			{
				lastTargetSeenTick = tickCount;
			}
			else if (tickCount - lastTargetSeenTick > 5000)
			{
				StopFollowing("Follow stopped: " + (followTargetName ?? "target") + " left the area.");
			}
			return;
		}
		lastTargetSeenTick = tickCount;
		WorldPosData worldPosData2 = ((mapObject.TargetPosition != null && mapObject.TargetPosition.X != 0.0) ? mapObject.TargetPosition : mapObject.Position);
		double num = worldPosData.DistanceTo(worldPosData2);
		if (num > 30.0)
		{
			StopFollowing("Follow stopped: " + followTargetName + " moved too far away (" + Math.Round(num, 1) + " tiles).");
			return;
		}
		lock (breadcrumbs)
		{
			if (breadcrumbs.Count == 0)
			{
				if (worldPosData.DistanceTo(worldPosData2) > 1.2)
				{
					breadcrumbs.Add((WorldPosData)worldPosData2.Clone());
				}
			}
			else if (breadcrumbs[breadcrumbs.Count - 1].DistanceTo(worldPosData2) > 0.7)
			{
				breadcrumbs.Add((WorldPosData)worldPosData2.Clone());
			}
			if (breadcrumbs.Count > 200)
			{
				breadcrumbs.RemoveAt(0);
			}
		}
	}

	public void OnPlayerShoot(PlayerShootPacket packet)
	{
	}

	public void OnNewTick(NewTickPacket packet)
	{
		if (packet?.Statuses == null)
		{
			return;
		}
		foreach (ObjectStatsData item in packet.Statuses)
		{
			if (item.ObjectId == client.ClientId && item.StatList != null)
			{
				foreach (StatData item2 in item.StatList)
				{
					if (item2.StatTypeField == 22)
					{
						mySpeedStat = item2.StatValue;
					}
				}
			}
			if (isFollowing && item.ObjectId == followTargetId && item.Position != null)
			{
				lastLeaderPosX = item.Position.X;
				lastLeaderPosY = item.Position.Y;
				lastLeaderTickTime = Environment.TickCount;
			}
		}
	}

	public void OnUpdate(UpdatePacket packet)
	{
		if (packet.NewObjects == null)
		{
			return;
		}
		ObjectData[] NewObjects = packet.NewObjects;
		foreach (ObjectData objectData in NewObjects)
		{
			if (objectData.Stats != null && objectData.Stats.ObjectId == client.ClientId && objectData.Stats.StatList != null)
			{
				foreach (StatData item in objectData.Stats.StatList)
				{
					if (item.StatTypeField == 22)
					{
						mySpeedStat = item.StatValue;
					}
				}
			}
			if (objectData.Stats != null && isFollowing && objectData.Stats.ObjectId == followTargetId && objectData.Stats.Position != null)
			{
				lastLeaderPosX = objectData.Stats.Position.X;
				lastLeaderPosY = objectData.Stats.Position.Y;
				lastLeaderTickTime = Environment.TickCount;
			}
		}
	}

	public void OnMapChange()
	{
		if (isFollowing)
		{
			StopFollowing("Follow stopped: map changed.");
		}
		lastAuthoritativeServerPos = null;
		latestClientPos = null;
		lastClientMoveTime = 0;
		lastTargetSeenTick = 0;
		lastLeaderPosX = 0.0;
		lastLeaderPosY = 0.0;
		lastLeaderTickTime = 0;
		lock (breadcrumbs)
		{
			breadcrumbs.Clear();
		}
	}

	private IntPtr GetGameWindowHandle()
	{
		if (clientWindowHandle != IntPtr.Zero && IsWindow(clientWindowHandle))
		{
			return clientWindowHandle;
		}
		try
		{
			if (client.ClientSocket?.Client?.RemoteEndPoint is IPEndPoint iPEndPoint)
			{
				int pidForPort = GetPidForPort(iPEndPoint.Port);
				if (pidForPort > 0)
				{
					Process processById = Process.GetProcessById(pidForPort);
					if (processById != null && !processById.HasExited)
					{
						if (processById.MainWindowHandle != IntPtr.Zero && IsWindow(processById.MainWindowHandle))
						{
							clientWindowHandle = processById.MainWindowHandle;
							return clientWindowHandle;
						}
						IntPtr intPtr = FindWindowForProcess(pidForPort);
						if (intPtr != IntPtr.Zero)
						{
							clientWindowHandle = intPtr;
							return clientWindowHandle;
						}
					}
				}
			}
		}
		catch
		{
		}
		try
		{
			Process[] processesByName = Process.GetProcessesByName("RotMG Exalt");
			if (processesByName.Length == 1 && !processesByName[0].HasExited)
			{
				if (processesByName[0].MainWindowHandle != IntPtr.Zero && IsWindow(processesByName[0].MainWindowHandle))
				{
					clientWindowHandle = processesByName[0].MainWindowHandle;
					return clientWindowHandle;
				}
				IntPtr intPtr2 = FindWindowForProcess(processesByName[0].Id);
				if (intPtr2 != IntPtr.Zero)
				{
					clientWindowHandle = intPtr2;
					return clientWindowHandle;
				}
			}
		}
		catch
		{
		}
		return IntPtr.Zero;
	}

	private static IntPtr FindWindowForProcess(int pid)
	{
		IntPtr result = IntPtr.Zero;
		try
		{
			EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
			{
				GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
				if (lpdwProcessId == pid && IsWindowVisible(hWnd))
				{
					result = hWnd;
					return false;
				}
				return true;
			}, IntPtr.Zero);
		}
		catch
		{
		}
		return result;
	}

	private static int GetPidForPort(int port)
	{
		int pdwOutBufLen = 0;
		GetExtendedTcpTable(IntPtr.Zero, ref pdwOutBufLen, sort: false, 2, 5);
		if (pdwOutBufLen <= 0)
		{
			return -1;
		}
		IntPtr intPtr = Marshal.AllocHGlobal(pdwOutBufLen);
		try
		{
			if (GetExtendedTcpTable(intPtr, ref pdwOutBufLen, sort: false, 2, 5) == 0)
			{
				int num = Marshal.ReadInt32(intPtr);
				IntPtr intPtr2 = (IntPtr)((long)intPtr + 4);
				for (int i = 0; i < num; i++)
				{
					int num2 = (Marshal.ReadByte(intPtr2, 8) << 8) | Marshal.ReadByte(intPtr2, 9);
					int result = Marshal.ReadInt32(intPtr2, 20);
					if (num2 == port)
					{
						return result;
					}
					intPtr2 = (IntPtr)((long)intPtr2 + 24);
				}
			}
		}
		catch
		{
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
		return -1;
	}

	private void StopFollowing(string message)
	{
		if (isFollowing || message != null)
		{
			isFollowing = false;
			if (followTargetName != null)
			{
				lastFollowTargetName = followTargetName;
			}
			followTargetName = null;
			followTargetId = -1;
			isChatOpen = false;
			lastLeaderPosX = 0.0;
			lastLeaderPosY = 0.0;
			lastLeaderTickTime = 0;
			lock (breadcrumbs)
			{
				breadcrumbs.Clear();
			}
			ReleaseAllKeys(GetGameWindowHandle());
			if (!string.IsNullOrEmpty(message))
			{
				client.SendNotification(message);
			}
		}
	}

	public void Dispose()
	{
		StopFollowing(null);
	}
}
