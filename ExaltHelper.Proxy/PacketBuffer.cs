using System;

namespace ExaltHelper.Proxy;

internal class PacketBuffer
{
	public int BytesRead;

	public byte[] Buffer;

	public int BytesReadAlias
	{
		get
		{
			return BytesRead;
		}
		set
		{
			BytesRead = value;
		}
	}

	public byte[] BufferAlias
	{
		get
		{
			return Buffer;
		}
		set
		{
			Buffer = value;
		}
	}

	public PacketBuffer()
	{
		Buffer = new byte[4];
		BytesRead = 0;
	}

	public void Resize(int newSize)
	{
		if (newSize > 1048576)
		{
			throw new ArgumentException("Packet buffer size exceeds maximum permitted threshold (1 MB).");
		}
		byte[] buffer = Buffer;
		Buffer = new byte[newSize];
		Buffer[0] = buffer[0];
		Buffer[1] = buffer[1];
		Buffer[2] = buffer[2];
		Buffer[3] = buffer[3];
	}

	public void ResizeForPacket(int newSize)
	{
		Resize(newSize);
	}

	public void Advance(int count)
	{
		BytesRead += count;
	}

	public void AdvanceReadPosition(int count)
	{
		Advance(count);
	}

	public void Reset()
	{
		Buffer = new byte[4];
		BytesRead = 0;
	}

	public void ResetForNextPacket()
	{
		Reset();
	}

	public int RemainingBytes()
	{
		return Buffer.Length - BytesRead;
	}

	public int GetRemainingByteCount()
	{
		return RemainingBytes();
	}

	public void Dispose()
	{
		Buffer = null;
	}

	public void ReleaseBuffer()
	{
		Dispose();
	}
}
