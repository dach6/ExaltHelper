using System;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ExaltHelper.Proxy;

public static class TokenHelper
{
	public static string DeviceUniqueIdentifier;

	static TokenHelper()
	{
		DeviceUniqueIdentifier = string.Empty;
		DeviceUniqueIdentifier = GenerateDeviceUniqueIdentifier();
	}

	public static string GenerateDeviceUniqueIdentifier()
	{
		string text = string.Empty;
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
			foreach (ManagementBaseObject item in managementObjectSearcher.Get())
			{
				text += ((string)item.Properties["SerialNumber"].Value) ?? string.Empty;
			}
		}
		catch (Exception baseBoardException)
		{
			Program.ShowWarning($"Unable to get Win32_BaseBoard SerialNumber: {baseBoardException}");
		}
		try
		{
			using ManagementObjectSearcher managementObjectSearcher2 = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
			foreach (ManagementBaseObject item2 in managementObjectSearcher2.Get())
			{
				text += ((string)item2.Properties["SerialNumber"].Value) ?? string.Empty;
			}
		}
		catch (Exception biosException)
		{
			Program.ShowWarning($"Unable to get Win32_BIOS SerialNumber: {biosException}");
		}
		try
		{
			if (string.IsNullOrEmpty(text))
			{
				text = (string)Registry.GetValue("Computer\\HKEY_CURRENT_USER\\Software\\Unity Technologies", "DeviceId", string.Empty);
				if (string.IsNullOrEmpty(text))
				{
					Program.ShowError("Please run RotMG Exalt through the official launcher once before using Multi-Login.");
				}
			}
		}
		catch (Exception registryException)
		{
			Program.ShowWarning($"Unable to get Registry DeviceId: {registryException}");
		}
		try
		{
			using ManagementObjectSearcher managementObjectSearcher3 = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
			foreach (ManagementBaseObject item3 in managementObjectSearcher3.Get())
			{
				text += ((string)item3.Properties["SerialNumber"].Value) ?? string.Empty;
			}
		}
		catch (Exception operatingSystemException)
		{
			Program.ShowWarning($"Unable to get Win32_OperatingSystem SerialNumber: {operatingSystemException}");
		}
		using SHA1 sHA = SHA1.Create();
		return string.Join("", sHA.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(delegate(byte b2)
		{
			byte b = b2;
			return b.ToString("x2");
		}));
	}

	public static string[] CreateAccountToken(string email, string password, string verifyResult = "")
	{
		if (email.StartsWith("token|"))
		{
			long num = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long num2 = DateTimeOffset.UtcNow.AddDays(7.0).ToUnixTimeSeconds();
			return new string[4]
			{
				email,
				password,
				num.ToString(),
				num2.ToString()
			};
		}
		if (email.StartsWith("steamworks_") || email.StartsWith("kongregate_"))
		{
			email = email.Replace('_', ':');
		}
		email = Encode(email);
		password = Encode(password);
		string url = ((email.StartsWith("steamworks%3A") || email.StartsWith("kongregate%3A")) ? ("https://www.realmofthemadgod.com/account/verify?guid=" + email.Replace('_', ':') + "&secret=" + password + "&clientToken=" + DeviceUniqueIdentifier) : ("https://www.realmofthemadgod.com/account/verify?guid=" + email + "&password=" + password + "&clientToken=" + DeviceUniqueIdentifier));
		string text = ((verifyResult == "") ? UnityUrlLoad(url) : verifyResult);
		if (text.StartsWith("<Error") || !text.Contains("<AccessToken>") || !text.Contains("</AccessToken>"))
		{
			return new string[1] { text };
		}
		var (text5, text6, text7) = ParseFromVerify(text);
		if (UnityUrlLoad("https://www.realmofthemadgod.com/account/verifyAccessTokenClient?game_net=Unity&play_platform=Unity&game_net_user_id=&clientToken=" + DeviceUniqueIdentifier + "&accessToken=" + Encode(text5)) == "<Success/>")
		{
			return new string[4] { DeviceUniqueIdentifier, text5, text6, text7 };
		}
		return new string[0];
	}

	public static Tuple<string, string, string> ParseFromVerify(string verify)
	{
		string item = verify.Split(new string[1] { "<AccessToken>" }, StringSplitOptions.None)[1].Split(new string[1] { "</AccessToken>" }, StringSplitOptions.None)[0];
		string item2 = verify.Split(new string[1] { "<AccessTokenTimestamp>" }, StringSplitOptions.None)[1].Split(new string[1] { "</AccessTokenTimestamp>" }, StringSplitOptions.None)[0];
		string item3 = verify.Split(new string[1] { "<AccessTokenExpiration>" }, StringSplitOptions.None)[1].Split(new string[1] { "</AccessTokenExpiration>" }, StringSplitOptions.None)[0];
		return new Tuple<string, string, string>(item, item2, item3);
	}

	public static string UnityUrlLoad(string url)
	{
		using WebClient webClient = new WebClient();
		webClient.Headers.Add("X-Unity-Version", "6000.0.58f2");
		webClient.Headers.Add("User-Agent", "UnityPlayer/6000.0.58f2 (UnityWebRequest/1.0, libcurl/7.84.0-DEV)");
		return webClient.DownloadString(url);
	}

	public static string Encode(string input)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in input)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
				continue;
			}
			stringBuilder.Append('%');
			stringBuilder.Append(((byte)c).ToString("X2"));
		}
		return stringBuilder.ToString();
	}
}
