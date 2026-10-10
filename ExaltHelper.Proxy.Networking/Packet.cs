using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper.Proxy.Networking;

internal class Packet
{
	public bool Send = true;

	public byte Id;

	public byte[] ExtraBytes = new byte[0];

	private byte[] _rawPayload;

	private static Dictionary<Type, byte> TypeToIdMap;

	private static Dictionary<byte, Type> IdToTypeMap;

	private static Dictionary<Type, Func<object>> PacketConstructors;

	protected Packet()
	{
		Id = (TypeToIdMap.TryGetValue(GetType(), out var value) ? value : byte.MaxValue);
	}

	public virtual void Read(PacketReader reader)
	{
		_rawPayload = reader.ReadBytes((int)reader.BaseStream.Length - 5);
	}

	public virtual void Write(PacketWriter writer)
	{
		writer.Write(_rawPayload);
	}

	static Packet()
	{
		TypeToIdMap = new Dictionary<Type, byte>
		{
			{ typeof(DamageBoostPacket), 148 },
			{ typeof(CrucibleResponsePacket), 183 },
			{
				typeof(FailurePacket),
				0
			},
			{
				typeof(TeleportPacket),
				1
			},
			{
				typeof(GenericFailurePacket),
				9
			},
			{
				typeof(NewTickPacket),
				10
			},
			{
				typeof(ShowEffectPacket),
				11
			},
			{
				typeof(PlayerShootServerPacket),
				12
			},
			{
				typeof(UseItemPacket),
				13
			},
			{
				typeof(EnemyHitPacket),
				25
			},
			{
				typeof(PlayerShootPacket),
				30
			},
			{
				typeof(TeleportIdPacket),
				31
			},
			{
				typeof(ServerPlayerShootPacket),
				35
			},
			{
				typeof(UpdatePacket),
				42
			},
			{
				typeof(TextPacket),
				44
			},
			{
				typeof(ReconnectPacket),
				45
			},
			{
				typeof(InvDropPacket),
				55
			},
			{
				typeof(MovePacket),
				62
			},
			{
				typeof(AoePacket),
				64
			},
			{
				typeof(NotificationPacket),
				67
			},
			{
				typeof(HelloPacket),
				74
			},
			{
				typeof(EnemyShootPacket),
				75
			},
			{
				typeof(PlayerTextPacket),
				77
			},
			{
				typeof(DamagePacket),
				82
			},
			{
				typeof(GotoAckPacket),
				89
			},
			{
				typeof(ShootAckPacket),
				90
			},
			{
				typeof(MapInfoPacket),
				92
			},
			{
				typeof(InvSwapPacket),
				95
			},
			{
				typeof(GotoPacket),
				101
			},
			{
				typeof(GroundDamagePacket),
				103
			},
			{
				typeof(PongPacket),
				105
			},
			{
				typeof(VaultContentPacket),
				117
			},
			{
				typeof(ReskinPacket),
				138
			},
			{
				typeof(OtherHitPacket),
				157
			},
			{
				typeof(StasisPacket),
				166
			},
			{
				typeof(PartyActionResultPacket),
				204
			},
			{
				typeof(PartyActionPacket),
				207
			},
			{
				typeof(IncomingPartyMemberInfoPacket),
				210
			},
			{
				typeof(PartyMemberAddedPacket),
				212
			},
			{
				typeof(PartyListPacket),
				214
			}
		};
		IdToTypeMap = TypeToIdMap.ToDictionary((KeyValuePair<Type, byte> entry) => entry.Value, (KeyValuePair<Type, byte> entry) => entry.Key);
		PacketConstructors = new Dictionary<Type, Func<object>>();
		foreach (Type value in IdToTypeMap.Values)
		{
			PacketConstructors.Add(value, Expression.Lambda<Func<object>>(Expression.New(value), Array.Empty<ParameterExpression>()).Compile());
		}
	}

	public static Packet Create(byte[] data)
	{
		using PacketReader packetReader = new PacketReader(new MemoryStream(data));
		packetReader.ReadInt32();
		byte b = packetReader.ReadByte();
		Packet packet;
		if (IdToTypeMap.TryGetValue(b, out var value))
		{
			try
			{
				packet = (Packet)PacketConstructors[value]();
				packet.Read(packetReader);
				long position = packetReader.BaseStream.Position;
				long length = packetReader.BaseStream.Length;
				if (position < length)
				{
					int count = (int)(length - position);
					packet.ExtraBytes = packetReader.ReadBytes(count);
				}
				return packet;
			}
			catch (Exception ex)
			{
				try
				{
					File.AppendAllText("disconnect_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [Packet.Create Fallback] Error reading packet {value.Name} (ID {b}): {ex.Message}. Falling back to raw forwarding.\r\n");
				}
				catch
				{
				}
			}
		}
		packet = new Packet();
		packet.Id = b;
		packetReader.BaseStream.Position = 5L;
		packet.Read(packetReader);
		return packet;
	}

	public override string ToString()
	{
		FieldInfo[] fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(GetType().Name + "(" + Id + ") Packet Instance");
		FieldInfo[] array = fields;
		foreach (FieldInfo fieldInfo in array)
		{
			if (fieldInfo.FieldType == typeof(byte[]))
			{
				stringBuilder.Append("\n\t" + fieldInfo.Name + " => " + CommonUtils.ToHexString(fieldInfo.GetValue(this) as byte[]));
			}
			else
			{
				stringBuilder.Append("\n\t" + fieldInfo.Name + " => " + fieldInfo.GetValue(this));
			}
		}
		return stringBuilder.ToString();
	}

	public bool HasNullFields(Packet packet)
	{
		FieldInfo[] fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (fieldInfo.GetValue(this) == null)
			{
				Program.LogWarning("Send", $"Packet {packet.Id} has null field: {fieldInfo.Name}");
				return true;
			}
		}
		return false;
	}
}
