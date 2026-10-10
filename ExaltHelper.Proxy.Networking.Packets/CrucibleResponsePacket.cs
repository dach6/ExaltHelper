using System;
using System.IO;

namespace ExaltHelper.Proxy.Networking.Packets;

internal class CrucibleResponsePacket : Packet
{
	public int[] CrucibleIds = Array.Empty<int>();
	public string[] Definitions = Array.Empty<string>();
	public override void Read(PacketReader reader)
	{
		int count = reader.ReadInt16();
		if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / 4) throw new InvalidDataException();
		CrucibleIds = new int[count];
		for (int i = 0; i < count; i++) CrucibleIds[i] = reader.ReadInt32();
		count = reader.ReadInt16();
		if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / 2) throw new InvalidDataException();
		Definitions = new string[count];
		for (int i = 0; i < count; i++) Definitions[i] = reader.ReadString();
	}
	public override void Write(PacketWriter writer)
	{
		writer.Write((short)CrucibleIds.Length);
		foreach (int id in CrucibleIds) writer.Write(id);
		writer.Write((short)Definitions.Length);
		foreach (string json in Definitions) writer.Write(json);
	}
}
