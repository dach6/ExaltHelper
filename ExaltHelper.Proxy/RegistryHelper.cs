using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ExaltHelper.Proxy;

public class RegistryHelper
{
	private const string ProtocolScheme = "exalthelper";

	private const string ProtocolDescription = "URL: ExaltHelper Protocol";

	public const string DecaKey = "SOFTWARE\\DECA Live Operations GmbH\\RotMGExalt";

	public const string MultitoolKey = "SOFTWARE\\RealmStock\\MultiTool";

	private const string MigratedKey = "Migrated";

	private static readonly string[] RegistrySettingNames = new string[21]
	{
		"AutoAimEnabled", "AutoAimModeMouse", "AutoAimModeHighestHP", "AutoAimModeClosest", "AutoAimRangeLead", "AutoAimMouseDist", "AutoAimFocusBoss", "AutoAimIgnoreWalls", "AutoAimShootInvulnerable", "AutoAimReverseCultStaff",
		"AutoAimOffsetColossusSword", "AutoAimProjectileNoclip", "AutoAimShootWhileStealthed", "FpsForeground", "FpsBackground", "FpsVsync", "SlowWalkKey", "SlowWalkHold", "SlowWalkMultiplier", "SlowWalkPercentOrSpeed",
		"CurrentPort"
	};

	private static readonly string[] DeprecatedLegacySettings = new string[25]
	{
		"Auto Aim", "autoAimFocusBoss", "autoAimIgnoreWalls", "Auto Aim Mode", "autoAimMode", "autoAimMouseDist", "autoAimRangeLead", "autoPotHp", "autoPotMp", "Focus Bosses",
		"Ignore Walls", "incViewRadius", "noclipToggleKey", "passthroughInvuln", "Passthrough Invulnerable Enemies", "Passthrough Solids", "Projectile Noclip", "Proxy Inject", "resetAutoAbiKey", "resetCHpKey",
		"Reverse Cult Staff", "tileViewRadius", "toggle Auto Aim key", "toggle Projectile Noclip key", "wepModReverseCultStaff"
	};

	private static string NormalizeSettingName(string setting)
	{
		if (setting == "AutoAimProjectileNoclip")
		{
			return "WeaponModsProjectileNoclip";
		}
		return setting;
	}

	public static bool IsRegistrySetting(string setting)
	{
		return RegistrySettingNames.Contains(setting);
	}

	public static void ChangeLauncherUserPass(string user, string pass)
	{
		byte[] value = Encoding.ASCII.GetBytes(user).Concat(new byte[1]).ToArray();
		byte[] value2 = Encoding.ASCII.GetBytes(CommonUtils.ToBase64(pass)).Concat(new byte[1]).ToArray();
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DECA Live Operations GmbH\\RotMG Exalt Launcher", writable: true);
		string[] valueNames = registryKey.GetValueNames();
		foreach (string text in valueNames)
		{
			if (text.StartsWith("token_"))
			{
				registryKey.DeleteValue(text);
			}
		}
		registryKey.SetValue("UHJvZHVjdGlvbmd1aWQ=_h808129427", value);
		registryKey.SetValue("UHJvZHVjdGlvbnBz_h3317303335", value2);
	}

	public static void SetSetting(string keyName, string setting, int value)
	{
		setting = NormalizeSettingName(setting);
		using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
		using RegistryKey registryKey2 = registryKey.OpenSubKey(keyName, writable: true) ?? registryKey.CreateSubKey(keyName);
		registryKey2.SetValue(setting, value);
	}

	public static void SetStringSetting(string keyName, string setting, string value)
	{
		using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
		using RegistryKey registryKey2 = registryKey.OpenSubKey(keyName, writable: true) ?? registryKey.CreateSubKey(keyName);
		registryKey2.SetValue(setting, Encoding.ASCII.GetBytes(value + "\0"), RegistryValueKind.Binary);
	}

	public static string GetStringSetting(string keyName, string name)
	{
		using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
		{
			using RegistryKey registryKey2 = registryKey.OpenSubKey(keyName, writable: true) ?? registryKey.CreateSubKey(keyName);
			string[] valueNames = registryKey2.GetValueNames();
			foreach (string text in valueNames)
			{
				if (text.StartsWith(name))
				{
					object value = registryKey2.GetValue(text);
					if (value is byte[] bytes)
					{
						return Encoding.ASCII.GetString(bytes).Replace("\0", "");
					}
					if (value is string text2)
					{
						return text2.Replace("\0", "");
					}
					Program.LogWarning("RegistryHelper", $"Unhandled type of registry value {text}, type: {value.GetType()}");
				}
			}
		}
		return null;
	}

	public static bool HasWritePermissions(string keyName)
	{
		try
		{
			using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
			using RegistryKey registryKey2 = registryKey.OpenSubKey(keyName, writable: true) ?? registryKey.CreateSubKey(keyName);
			return registryKey2 != null;
		}
		catch (SecurityException)
		{
			return false;
		}
	}

	public static void DeleteTest(string keyName)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(keyName, writable: true);
		if (registryKey == null)
		{
			Console.WriteLine("Don't have key \"" + keyName + "\"!");
			return;
		}
		string[] valueNames = registryKey.GetValueNames();
		List<string> list = new List<string>
		{
			"Auto Aim", "autoAimFocusBoss", "autoAimIgnoreWalls", "Auto Aim Mode", "autoAimMode", "autoAimMouseDist", "autoAimRangeLead", "autoPotHp", "autoPotMp", "Focus Bosses",
			"Ignore Walls", "incViewRadius", "noclipToggleKey", "passthroughInvuln", "Passthrough Invulnerable Enemies", "Passthrough Solids", "Projectile Noclip", "Proxy Inject", "resetAutoAbiKey", "resetCHpKey",
			"Reverse Cult Staff", "tileViewRadius", "toggle Auto Aim key", "toggle Projectile Noclip key", "wepModReverseCultStaff"
		};
		StringBuilder stringBuilder = new StringBuilder();
		string[] array = valueNames;
		foreach (string text in array)
		{
			foreach (string item in list)
			{
				if (text.StartsWith(item))
				{
					object value = registryKey.GetValue(text);
					RegistryValueKind valueKind = registryKey.GetValueKind(text);
					Console.WriteLine($"Hack client registry found: {text} = {value} ({valueKind})");
					stringBuilder.AppendLine($"{text}|{valueKind}|{value}");
				}
			}
		}
		File.WriteAllText("RegistryExport.txt", stringBuilder.ToString());
	}

	public static void TryMigrateSettings(string oldKey, string newKey)
	{
		try
		{
			using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
			{
				using (RegistryKey registryKey2 = registryKey.OpenSubKey(newKey))
				{
					if (registryKey2 != null)
					{
						object value = registryKey2.GetValue("Migrated");
						if (value != null && (int)value == 1)
						{
							Program.LogInfo("Registry", "Already migrated settings");
							return;
						}
					}
				}
				using RegistryKey registryKey3 = registryKey.OpenSubKey(oldKey);
				if (registryKey3 == null)
				{
					return;
				}
				using RegistryKey registryKey4 = registryKey.CreateSubKey(newKey);
				if (registryKey4 == null)
				{
					return;
				}
				string[] valueNames = registryKey3.GetValueNames();
				foreach (string text in valueNames)
				{
					if (!IsRegistrySetting(text))
					{
						continue;
					}
					switch (text)
					{
					case "AutoAimModeMouse":
						if ((int)registryKey3.GetValue(text) == 1)
						{
							RegistryValueKind valueKind3 = registryKey3.GetValueKind(text);
							Program.LogInfo("Registry", text + " -> AutoAimMode:2");
							registryKey4.SetValue("AutoAimMode", 2, valueKind3);
						}
						break;
					case "AutoAimModeHighestHP":
						if ((int)registryKey3.GetValue(text) == 1)
						{
							RegistryValueKind valueKind4 = registryKey3.GetValueKind(text);
							Program.LogInfo("Registry", text + " -> AutoAimMode:1");
							registryKey4.SetValue("AutoAimMode", 1, valueKind4);
						}
						break;
					case "AutoAimModeClosest":
						if ((int)registryKey3.GetValue(text) == 1)
						{
							RegistryValueKind valueKind2 = registryKey3.GetValueKind(text);
							Program.LogInfo("Registry", text + " -> AutoAimMode:0");
							registryKey4.SetValue("AutoAimMode", 0, valueKind2);
						}
						break;
					default:
					{
						object value2 = registryKey3.GetValue(text);
						RegistryValueKind valueKind = registryKey3.GetValueKind(text);
						Program.LogInfo("Registry", $"Ported {text}:{value2} ({valueKind})");
						registryKey4.SetValue(text, value2, valueKind);
						break;
					}
					}
				}
				Program.LogInfo("Registry", "Migration completed successfully");
				registryKey4.SetValue("Migrated", 1, RegistryValueKind.DWord);
			}
			HotkeyHandler hotkeyHandler = Settings.Default.Hotkeys.FirstOrDefault((HotkeyHandler hotkeyHandler2) => hotkeyHandler2.Setting == "EnableSlowWalk");
			if (hotkeyHandler != null)
			{
				UpdateSlowWalk(hotkeyHandler);
			}
		}
		catch (Exception exception)
		{
			string text2 = $"Failed to migrate settings:\n{exception}";
			Program.LogWarning("Registry", text2);
			MessageBox.Show(text2, "ExaltHelper", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	public static void RegisterProtocol()
	{
		string fileName = Process.GetCurrentProcess().MainModule.FileName;
		string text = "\"" + fileName + "\" \"%1\"";
		using RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey("exalthelper", writable: true);
		if (registryKey == null)
		{
			using (RegistryKey registryKey2 = Registry.ClassesRoot.CreateSubKey("exalthelper"))
			{
				registryKey2.SetValue("", "URL: ExaltHelper Protocol");
				registryKey2.SetValue("URL Protocol", "");
				using RegistryKey registryKey3 = registryKey2.CreateSubKey("shell");
				using RegistryKey registryKey4 = registryKey3.CreateSubKey("open");
				using RegistryKey registryKey5 = registryKey4.CreateSubKey("command");
				registryKey5.SetValue("", text);
				return;
			}
		}
		using (RegistryKey registryKey6 = registryKey.OpenSubKey("shell\\open\\command", writable: true))
		{
			if (registryKey6 != null)
			{
				if (!string.Equals(registryKey6.GetValue("") as string, text, StringComparison.OrdinalIgnoreCase))
				{
					registryKey6.SetValue("", text);
				}
			}
			else
			{
				using RegistryKey registryKey7 = registryKey.CreateSubKey("shell");
				using RegistryKey registryKey8 = registryKey7.CreateSubKey("open");
				using RegistryKey registryKey9 = registryKey8.CreateSubKey("command");
				registryKey9.SetValue("", text);
			}
		}
		if (!(registryKey.GetValue("URL Protocol") is string))
		{
			registryKey.SetValue("URL Protocol", "");
		}
	}

	public static void UpdateSlowWalk(HotkeyHandler handler)
	{
		SetSetting("SOFTWARE\\RealmStock\\MultiTool", "SlowWalkKey", (int)handler.Key);
		SetSetting("SOFTWARE\\RealmStock\\MultiTool", "SlowWalkHold", (handler.Result == -2) ? 1 : 0);
	}
}
