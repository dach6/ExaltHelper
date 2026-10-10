using System;
using System.Runtime.InteropServices;

namespace ExaltHelper;

public static class Win32
{
	public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

	public struct KBDLLHOOKSTRUCT
	{
		public int vkCode;

		public int scanCode;

		public int flags;

		public int time;

		public IntPtr dwExtraInfo;
	}

	public const int WH_KEYBOARD_LL = 13;

	public const int WM_KEYDOWN = 256;

	public const int WM_KEYUP = 257;

	public const int SW_HIDE = 0;

	public const int SW_SHOW = 5;

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool SetProcessDPIAware();

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll")]
	public static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

	[DllImport("user32.dll")]
	public static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport("kernel32.dll")]
	public static extern IntPtr GetModuleHandle(string lpModuleName);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool AllocConsole();
}
