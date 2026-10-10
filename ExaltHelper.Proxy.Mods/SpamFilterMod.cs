using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper.Proxy.Mods;

internal class SpamFilterMod
{
	[CompilerGenerated]
	private sealed class SpamRuleBinding
	{
		public string SpamFilterPattern;

		internal bool MatchesSpamPattern(string keyword)
		{
			return SpamFilterPattern.Contains(keyword);
		}
	}

	private Client _client;

	private static readonly string[] SpamKeywordPatterns = new string[70]
	{
		"oryxsh0p", "whtebagnet", "realmshopinfo", "realmshoplnfo", "rotmgmarketc", "realmitems", "reaimitems", "reaimltems", "realmltems", "realmpowernet",
		"reaimpowernet", "rea!mkingsxyz", "buyrotmgc", "lifepotorg", "rotmgmaxme", "oryxln", "rwtmg", "rotmgio", "realmpower", "reaimpower",
		"rwtstore", "rwtshop", "rotmgrwt", "realmgood", "reaimgood", "rpgrip", "rpgrlp", "realmshop", "reaimshop", "realmsh0p",
		"reaimsh0p", "realmp0wer", "reaimp0wer", "relmgood", "reimgood", "hyuk3d", "realmservices", "rotmgstore", "discordgg", "discordcom",
		"discordrwt", "discorddupe", "dlscord", "dlsc0rd", "0rdgg", "ordgg", "realmdupe", "reaimdupe", "relmshop", "relmsh0p",
		"rotmgnetwork", "0ryxsh0p", "exaltedshop", "goodsco", "rotmgcc", "rpgdotrip", "oryxshop", "realmhop", "realmbaron", "rbrnshop",
		"rbrndotshop", "rotmgarsenal", "r0tmgarsenal", "r0tmgarenal", "rotmgarenal", "r3almsh0p", "r34lmsh0p", "r341msh0p", "oryxspin", "rpgdotrlp"
	};

	public SpamFilterMod(Client client)
	{
		_client = client;
	}

	public void OnText(TextPacket packet)
	{
		if (!string.IsNullOrEmpty(packet.Name) && _client.Player != null && !string.IsNullOrEmpty(_client.Player.Name) && !(packet.Name.Split(',')[0] == _client.Player.Name) && packet.NumStars != -1)
		{
			string text = new string(packet.Text.Where((char c) => char.IsLetter(c) || char.IsNumber(c)).ToArray()).ToLower();
			string SpamFilterPattern = text + "|" + text.Normalize(NormalizationForm.FormC) + "|" + text.Normalize(NormalizationForm.FormD) + "|" + text.Normalize(NormalizationForm.FormKC) + "|" + text.Normalize(NormalizationForm.FormKD);
			IEnumerable<string> source = SpamKeywordPatterns.Where((string value) => SpamFilterPattern.Contains(value));
			packet.Send = !source.Any();
		}
	}
}
