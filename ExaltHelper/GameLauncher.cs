using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.Helpers;

namespace ExaltHelper;

internal class GameLauncher
{
	private const string VersionDllName = "version.dll";

	private static bool IsVersionDllLoaded;

	public static Process Launch(bool useSteam)
	{
		if (!File.Exists(useSteam ? Settings.Default.ExaltLauncherSteamPath : Settings.Default.ExaltLauncherPath))
		{
			using LocateLauncherForm locateLauncherForm = new LocateLauncherForm(useSteam);
			locateLauncherForm.ShowDialog();
			if (!locateLauncherForm.IsCustom)
			{
				return null;
			}
		}
		if (!EnsureHook())
		{
			return null;
		}
		string text = (useSteam ? Settings.Default.ExaltLauncherSteamPath : Settings.Default.ExaltLauncherPath);
		return Process.Start(new ProcessStartInfo
		{
			FileName = text,
			WorkingDirectory = Path.GetDirectoryName(text)
		});
	}

	public static Process LaunchDirect(string guid, string secret, string clientToken, string accessToken, string tokenTimestamp, string tokenExpiration)
	{
		if (!EnsureHook())
		{
			return null;
		}
		if (!GetExaltExecutablePath(out var launchArguments))
		{
			Program.ShowError("Failed to find Exalt Client install location!\n\nPlease run RotMG using the official Exalt Launcher first.");
			return null;
		}
		return Process.Start(new ProcessStartInfo
		{
			FileName = launchArguments,
			WorkingDirectory = Path.GetDirectoryName(launchArguments),
			Arguments = BuildLaunchArguments(guid, secret, clientToken, accessToken, tokenTimestamp, tokenExpiration)
		});
	}

	public static void DeleteHook()
	{
		if (GetExaltClientDirectory(out var clientDirectory))
		{
			string path = Path.Combine(clientDirectory, "version.dll");
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	public static bool EnsureHook()
	{
		try
		{
			if (!GetExaltClientDirectory(out var clientDirectory))
			{
				Program.ShowError("Failed to find Exalt Client install location!\n\nPlease run RotMG using the official Exalt Launcher first.");
				return false;
			}
			string path = Path.Combine(clientDirectory, "version.dll");
			byte[] array = File.ReadAllBytes(ResourceHelper.VersionPath);
			if (File.Exists(path))
			{
				if (IsVersionDllLoaded)
				{
					return true;
				}
				try
				{
					if (File.ReadAllBytes(path).SequenceEqual(array))
					{
						IsVersionDllLoaded = true;
						return true;
					}
					File.Delete(path);
				}
				catch (Exception ex)
				{
					Program.ShowError("Failed to update hook!\n\nPlease close all instances of RotMG Exalt or try restarting.\n\n" + ex);
					return false;
				}
			}
			File.WriteAllBytes(path, array);
			IsVersionDllLoaded = true;
			return true;
		}
		catch (Exception exception)
		{
			Program.ShowError($"Failed to initialize hook!\n\n{exception}");
			return false;
		}
	}

	public static bool GetExaltClientDirectory(out string directory)
	{
		string path = "RealmOfTheMadGod\\Production";
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path);
		if (Directory.Exists(text))
		{
			directory = text;
			return true;
		}
		string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), path);
		if (Directory.Exists(text2))
		{
			directory = text2;
			return true;
		}
		string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path);
		if (Directory.Exists(text3))
		{
			directory = text3;
			return true;
		}
		string text4 = Path.Combine("C:\\", path);
		if (Directory.Exists(text4))
		{
			directory = text4;
			return true;
		}
		directory = string.Empty;
		return false;
	}

	public static bool GetExaltExecutablePath(out string executablePath)
	{
		string path = "RealmOfTheMadGod\\Production\\RotMG Exalt.exe";
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path);
		if (File.Exists(text))
		{
			executablePath = text;
			return true;
		}
		if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), path)))
		{
			executablePath = text;
			return true;
		}
		string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path);
		if (File.Exists(text2))
		{
			executablePath = text2;
			return true;
		}
		string text3 = Path.Combine("C:\\", path);
		if (File.Exists(text3))
		{
			executablePath = text3;
			return true;
		}
		executablePath = string.Empty;
		return false;
	}

	private static string BuildLaunchArguments(string guid, string secret, string clientToken, string accessToken, string tokenTimestamp, string tokenExpiration)
	{
		string value = CommonUtils.ToBase64(secret);
		string text = CommonUtils.ToBase64(accessToken);
		string text2 = CommonUtils.ToBase64(tokenTimestamp);
		string text3 = CommonUtils.ToBase64(tokenExpiration);
		string text4;
		if (guid.Contains("@"))
		{
			text4 = CommonUtils.ToBase64(guid);
			return $"data:{{platform:Deca,guid:{text4},token:{text},tokenTimestamp:{text2},tokenExpiration:{text3},env:4,h:{clientToken}}},p:{ProxyServer.LocalPortAlias}";
		}
		if (clientToken.StartsWith("token|"))
		{
			text4 = CommonUtils.ToBase64(guid);
			return $"data:{{platform:Deca,guid:{text4},token:{text},tokenTimestamp:{text2},tokenExpiration:{text3},env:4,h:{clientToken.Split('|')[1]}}},p:{ProxyServer.LocalPortAlias}";
		}
		guid = guid.Replace("steamworks_", "steamworks:").Replace("kongregate_", "kongregate:");
		text4 = CommonUtils.ToBase64(guid);
		string[] array = guid.Split(':');
		if (array.Length != 2)
		{
			throw new Exception("Malformed Steam GUID: " + guid);
		}
		string value2 = CommonUtils.ToBase64(array[1]);
		StringBuilder stringBuilder = new StringBuilder("data:{platform:Steam,env:4");
		stringBuilder.Append(",guid:");
		stringBuilder.Append(text4);
		stringBuilder.Append(",secret:");
		stringBuilder.Append(value);
		stringBuilder.Append(",steamId:");
		stringBuilder.Append(value2);
		stringBuilder.Append(",platformToken:");
		stringBuilder.Append(CommonUtils.ToBase64(""));
		stringBuilder.Append(",token:");
		stringBuilder.Append(text);
		stringBuilder.Append(",tokenTimestamp:");
		stringBuilder.Append(text2);
		stringBuilder.Append(",tokenExpiration:");
		stringBuilder.Append(text3);
		stringBuilder.Append("}");
		return stringBuilder.ToString();
	}
}
