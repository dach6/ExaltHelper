using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal interface IDataObject : ICloneable
{
	IDataObject Read(PacketReader reader);

	void Write(PacketWriter writer);
}
