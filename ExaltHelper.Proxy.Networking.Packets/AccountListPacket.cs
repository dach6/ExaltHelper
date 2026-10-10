namespace ExaltHelper.Proxy.Networking.Packets;

internal class AccountListPacket : Packet
{
	public byte[] AccountListId;

	public string[] AccountIds;

	public override void Read(PacketReader reader)
	{
		AccountListId = reader.ReadBytes(10);
		short num = reader.ReadInt16();
		AccountIds = new string[num];
		for (int i = 0; i < num; i++)
		{
			AccountIds[i] = reader.ReadString();
		}
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(AccountListId);
		writer.Write((short)AccountIds.Length);
		string[] array = AccountIds;
		foreach (string value in array)
		{
			writer.Write(value);
		}
	}
}
