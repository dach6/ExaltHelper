using System;

namespace ExaltHelper;

[Serializable]
public class Account
{
	public string Label { get; set; }

	public int Icon { get; set; }

	public string Email { get; set; }

	public string Password { get; set; }
}
