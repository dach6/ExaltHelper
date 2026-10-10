using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper.Proxy.Mods;

internal class AntiAfkMod
{
	private const int MsgLButtonDown = 513;

	private const int MsgLButtonUp = 514;

	private DateTime _lastActivityTime = DateTime.Now;

	private WorldPosData _lastPosition = WorldPosData.Zero;

	[DllImport("user32.dll", EntryPoint = "PostMessage")]
	private static extern bool PostMessage(IntPtr windowHandle, uint message, int wParam, int lParam);

	public void OnMove(MovePacket packet)
	{
		if (!Settings.Default.EnableAntiAFK)
		{
			return;
		}
		DateTime now = DateTime.Now;
		WorldPosData worldPosData = packet.Positions.Last().GetWorldPos();
		if (_lastPosition.X != worldPosData.X || _lastPosition.Y != worldPosData.Y)
		{
			_lastActivityTime = now;
			_lastPosition = worldPosData;
		}
		if (now.Subtract(_lastActivityTime) > TimeSpan.FromMinutes(5.0))
		{
			_lastActivityTime = now;
			Process[] processesByName = Process.GetProcessesByName("RotMG Exalt");
			Program.LogNotice("client", "Activating Anti AFK");
			Process[] array = processesByName;
			foreach (Process obj in array)
			{
				PostMessage(obj.MainWindowHandle, 513u, 1, 1);
				Thread.Sleep(32);
				PostMessage(obj.MainWindowHandle, 514u, 0, 1);
			}
		}
	}
}
