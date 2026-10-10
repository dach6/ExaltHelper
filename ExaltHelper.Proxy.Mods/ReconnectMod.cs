using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper.Proxy.Mods;

internal class ReconnectMod
{
	private readonly Client _client;

	public string CurrentServerIp { get; private set; }

	public string ServerIp => CurrentServerIp;

	public ReconnectMod(Client client)
	{
		_client = client;
	}

	public void SetCurrentServerIp(string ip)
	{
		CurrentServerIp = ip;
	}

	public void SetServerIp(string ip)
	{
		SetCurrentServerIp(ip);
	}

	public void OnGenericFailure(GenericFailurePacket packet)
	{
		string[] commandArguments;
		if (Settings.Default.DisableSendingIp && IsValidIPv4(packet.TextValue))
		{
			packet.Send = false;
		}
		else if (Settings.Default.EnableGotoCommand && packet.IsCommand("ip", out commandArguments))
		{
			_client.SendNotification("Server IP: " + CurrentServerIp);
			Thread thread = new Thread((ThreadStart)delegate
			{
				Clipboard.SetText(CurrentServerIp);
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}
	}

	public void HandleGenericFailure(GenericFailurePacket packet)
	{
		OnGenericFailure(packet);
	}

	public static bool IsValidIPv4(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string[] array = text.Split('.');
		if (array.Length != 4)
		{
			return false;
		}
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			if (text2.Length == 0 || text2.Length > 3)
			{
				return false;
			}
			if (!text2.All(char.IsDigit))
			{
				return false;
			}
			if (!int.TryParse(text2, out var result) || result < 0 || result > 255)
			{
				return false;
			}
		}
		return true;
	}

	public static bool IsReconnectCommand(string text)
	{
		return IsValidIPv4(text);
	}
}
