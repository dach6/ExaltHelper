using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.Helpers;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal static class Program
{
	public const string ToolVersion = "1.0.0";

	private static string _gameVersion;

	public static string GameVersion
	{
		get
		{
			if (_gameVersion != null)
			{
				return _gameVersion;
			}
			try
			{
				string gameVersionPath = ResourceHelper.GameVersionPath;
				if (File.Exists(gameVersionPath))
				{
					string text = File.ReadAllText(gameVersionPath).Trim();
					if (!string.IsNullOrEmpty(text))
					{
						_gameVersion = text;
						return _gameVersion;
					}
				}
			}
			catch
			{
			}
			_gameVersion = "7.1.0.1.0";
			return _gameVersion;
		}
	}

	static Program()
	{
		AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
		{
			try
			{
				AssemblyName assemblyName = new AssemblyName(args.Name);
				if (assemblyName.Name == "ExaltHelper" || assemblyName.Name == "ExaltKitGUI" || assemblyName.Name == "MultiTool")
				{
					return Assembly.GetExecutingAssembly();
				}
				if (assemblyName.Name != null && assemblyName.Name.StartsWith("System.Drawing"))
				{
					return typeof(Bitmap).Assembly;
				}
				string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName.Name + ".dll");
				if (File.Exists(text))
				{
					return Assembly.LoadFrom(text);
				}
			}
			catch
			{
			}
			return (Assembly)null;
		};
	}

	[STAThread]
	private static void Main(string[] args)
	{
		Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
		Win32.AllocConsole();
		Win32.ShowWindow(Win32.GetConsoleWindow(), 0);
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "ExaltHelper", out createdNew))
		{
			string text = string.Empty;
			string text2 = ((args.Length == 0) ? null : (args[0].StartsWith("exalthelper://", StringComparison.OrdinalIgnoreCase) ? "exalthelper://" : (args[0].StartsWith("multitool://", StringComparison.OrdinalIgnoreCase) ? "multitool://" : null)));
			if (text2 != null)
			{
				string text3 = args[0].Substring(text2.Length);
				if (text3.EndsWith("/"))
				{
					text3 = text3.Replace("/", "");
				}
				if (ReconnectMod.IsReconnectCommand(text3))
				{
					text = text3;
				}
			}
			if (!createdNew)
			{
				if (!string.IsNullOrEmpty(text))
				{
					IpcPipe.SendIpToInstance(text);
				}
				else
				{
					ShowWarning("Another copy of ExaltHelper is already running!\nPlease close it. You may need to check Task Manager process list to find it.");
				}
				return;
			}
			Win32.SetProcessDPIAware();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			ServicePointManager.Expect100Continue = false;
			if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
			{
				string location = Assembly.GetExecutingAssembly().Location;
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					Verb = "runas",
					FileName = location,
					Arguments = string.Join(",", args),
					WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
				};
				try
				{
					Process.Start(startInfo);
					return;
				}
				catch (Win32Exception)
				{
					return;
				}
			}
			try
			{
				Settings.Default.Save();
			}
			catch (Exception ex2)
			{
				try
				{
					if (!ShowQuestion("Failed to read your settings, do you want to RESET & CLEAR your settings?\nThis will reset your accounts saved in the multi login as well!\nThis is an unexpected issue we will work on fixing\n\nYou can back up your settings file here:\n" + Settings.Path + "\n\nException: " + ex2.Message) || !ShowQuestion("Are you SURE you want to reset your ExaltHelper SETTINGS & Multi Login ACCOUNTS?"))
					{
						return;
					}
					Settings.DeleteSettings();
					Settings.Default.Save();
				}
				catch (Exception settingsException)
				{
					ShowError($"Failed to load settings!\n{settingsException}");
					return;
				}
			}
			IEnumerable<string> enumerable = ResourceHelper.AllResourcesExist();
			if (enumerable.Any())
			{
				ShowError("Please extract all files from the ExaltHelper.zip into its own folder! Missing required files:\n" + string.Join("\n", enumerable));
				return;
			}
			if (!ResourceHelper.CheckResourceVersion())
			{
				ShowError("Resources not up to date! Please extract the ExaltHelper_Data folder to the same folder ExaltHelper.exe is in!");
				return;
			}
			RegistryHelper.TryMigrateSettings("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", "SOFTWARE\\RealmStock\\MultiTool");
			if (RegistryHelper.GetStringSetting("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", ServerManager.PreferredServerPrefKey) == null)
			{
				RegistryHelper.SetStringSetting("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", ServerManager.PreferredServerPrefKey, "USWest");
			}
			if (createdNew && !string.IsNullOrEmpty(text))
			{
				RegistryHelper.SetStringSetting("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt", ServerManager.PreferredServerPrefKey, text);
			}
			if (!RegistryHelper.HasWritePermissions("SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt") || !RegistryHelper.HasWritePermissions("SOFTWARE\\RealmStock\\MultiTool"))
			{
				ShowError("ExaltHelper does not have write permissions to the registry!");
				return;
			}
			try
			{
				RegistryHelper.RegisterProtocol();
			}
			catch (Exception protocolRegistrationException)
			{
				ShowError($"ExaltHelper failed registering url protocol: {protocolRegistrationException}");
			}
			if (Process.GetProcessesByName("MSIAfterburner").Any())
			{
				MessageBox.Show("MSI Afterburner is running! It is known to prevent ExaltHelper from attaching to Exalt, please close MSI Afterburner before launching ExaltHelper.", "ExaltHelper", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			MainForm mainForm = new MainForm();
			try
			{
				IpcPipe.StartServer(mainForm);
			}
			catch (Exception pipeInitializationException)
			{
				ShowError($"Failed initializing named pipe:\n{pipeInitializationException}");
			}
			try
			{
				Application.Run(mainForm);
			}
			catch (Exception fatalException)
			{
				ShowError($"Initialization error, please report:\n{fatalException}");
			}
		}
	}

	public static bool ShowQuestion(string message, Form parentForm = null)
	{
		return MessageBox.Show(parentForm, message, "ExaltHelper", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
	}

	public static void ShowInfo(string message, Form parentForm = null)
	{
		MessageBox.Show(parentForm, message, "ExaltHelper", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
	}

	public static void ShowWarning(string message, Form parentForm = null)
	{
		MessageBox.Show(parentForm, message, "ExaltHelper", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
	}

	public static void ShowError(string message, Form parentForm = null)
	{
		MessageBox.Show(parentForm, message, "ExaltHelper", MessageBoxButtons.OK, MessageBoxIcon.Hand);
	}

	public static void LogInfo(string category, string message)
	{
		LogMessage(category, '*', message);
	}

	public static void LogNotice(string category, string message)
	{
		LogMessage(category, '#', message);
	}

	public static void LogWarning(string category, string message)
	{
		LogMessage(category, '-', message);
	}

	public static void LogError(string category, string message)
	{
		LogMessage(category, '!', message);
	}

	private static void LogMessage(string category, char level, string message)
	{
	}
}
