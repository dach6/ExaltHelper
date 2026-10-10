namespace ExaltHelper.Proxy.Networking.Packets;

internal class ClaimDailyRewardResponsePacket : Packet
{
	public int ClaimDailyRewardResult;

	public override void Read(PacketReader reader)
	{
		ClaimDailyRewardResult = reader.ReadInt32();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(ClaimDailyRewardResult);
	}
}
