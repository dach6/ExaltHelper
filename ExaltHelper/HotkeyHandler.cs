using System;
using System.Windows.Forms;

namespace ExaltHelper;

[Serializable]
public class HotkeyHandler
{
	public const int Hold = -2;

	public const int Toggle = -1;

	public Keys Key { get; set; }

	public string Setting { get; set; }

	public int Result { get; set; }

	public override string ToString()
	{
		string keyName = Result switch
		{
			-2 => "hold", 
			-1 => "toggle", 
			_ => Result.ToString(), 
		};
		return $"[{Key}] {Setting} => {keyName}";
	}
}
