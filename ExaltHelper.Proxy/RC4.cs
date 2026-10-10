using System;

namespace ExaltHelper.Proxy;

internal class RC4
{
	private const int StateSize = 256;

	private byte[] _state;

	private int _i;

	private int _j;

	private byte[] _key;

	public RC4(byte[] key)
	{
		_key = key;
		KeySetup(_key);
	}

	public RC4(string hexKey)
	{
		_key = HexStringToByteArray(hexKey);
		KeySetup(_key);
	}

	public void Crypt(byte[] packetBuffer)
	{
		Process(packetBuffer, 5, packetBuffer.Length - 5, packetBuffer, 5);
	}

	public void Reset()
	{
		KeySetup(_key);
	}

	private void Process(byte[] inBuffer, int inOffset, int length, byte[] outBuffer, int outOffset)
	{
		for (int i = 0; i < length; i++)
		{
			_i = (_i + 1) & 0xFF;
			_j = (_state[_i] + _j) & 0xFF;
			byte b = _state[_i];
			_state[_i] = _state[_j];
			_state[_j] = b;
			outBuffer[i + outOffset] = (byte)(inBuffer[i + inOffset] ^ _state[(_state[_i] + _state[_j]) & 0xFF]);
		}
	}

	private void KeySetup(byte[] key)
	{
		_key = key;
		_i = 0;
		_j = 0;
		if (_state == null)
		{
			_state = new byte[256];
		}
		for (int i = 0; i < 256; i++)
		{
			_state[i] = (byte)i;
		}
		int num = 0;
		int num2 = 0;
		for (int j = 0; j < 256; j++)
		{
			num2 = ((key[num] & 0xFF) + _state[j] + num2) & 0xFF;
			byte b = _state[j];
			_state[j] = _state[num2];
			_state[num2] = b;
			num = (num + 1) % key.Length;
		}
	}

	public static byte[] HexStringToByteArray(string hex)
	{
		if (hex.Length % 2 != 0)
		{
			throw new ArgumentException("Invalid hex string length.");
		}
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < hex.Length; i += 2)
		{
			array[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
		}
		return array;
	}
}
