using System;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class PartyPlayerData
{
	public short Id { get; set; }

	public string Name { get; set; } = "";

	public short ObjectId { get; set; }

	public short Unknown { get; set; }

	public PartyPlayerData()
	{
	}

	public PartyPlayerData(PacketReader reader)
	{
		Read(reader);
	}

	public void Read(PacketReader reader)
	{
		Id = reader.ReadInt16();
		Name = reader.ReadString();
		ObjectId = reader.ReadInt16();
		Unknown = reader.ReadInt16();
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(Id);
		writer.Write(Name ?? "");
		writer.Write(ObjectId);
		writer.Write(Unknown);
	}
}

internal class IncomingPartyMemberInfoPacket : Packet
{
	public int PartyId { get; set; }

	public short Unknown { get; set; }

	public byte MaxSize { get; set; }

	public PartyPlayerData[] PartyPlayers { get; set; } = new PartyPlayerData[0];

	public string Description { get; set; } = "";

	public override void Read(PacketReader reader)
	{
		PartyId = reader.ReadInt32();
		Unknown = reader.ReadInt16();
		MaxSize = reader.ReadByte();
		short count = reader.ReadInt16();
		PartyPlayers = new PartyPlayerData[count];
		for (int i = 0; i < count; i++)
		{
			PartyPlayers[i] = new PartyPlayerData(reader);
		}
		Description = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(PartyId);
		writer.Write(Unknown);
		writer.Write(MaxSize);
		writer.Write((short)(PartyPlayers?.Length ?? 0));
		if (PartyPlayers != null)
		{
			foreach (PartyPlayerData player in PartyPlayers)
			{
				player.Write(writer);
			}
		}
		writer.Write(Description ?? "");
	}
}
