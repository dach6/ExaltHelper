using System;

namespace ExaltHelper.Proxy.Networking.Packets.DataObjects;

internal class WorldPosData : IDataObject, ICloneable
{
	public double X;

	public double Y;

	public static WorldPosData Zero => new WorldPosData(0f, 0f);

	public WorldPosData()
	{
	}

	public WorldPosData(PacketReader reader)
	{
		X = reader.ReadSingle();
		Y = reader.ReadSingle();
	}

	public WorldPosData(float x, float y)
	{
		X = x;
		Y = y;
	}

	public WorldPosData(double x, double y)
	{
		X = x;
		Y = y;
	}

	public virtual IDataObject Read(PacketReader reader)
	{
		X = reader.ReadSingle();
		Y = reader.ReadSingle();
		return this;
	}

	public virtual void Write(PacketWriter writer)
	{
		writer.Write((float)X);
		writer.Write((float)Y);
	}

	public double DistanceSquaredTo(WorldPosData position)
	{
		double num = position.X - X;
		double num2 = position.Y - Y;
		return num * num + num2 * num2;
	}

	public double DistanceTo(WorldPosData position)
	{
		return Math.Sqrt(DistanceSquaredTo(position));
	}

	public double GetAngle(WorldPosData from, WorldPosData to)
	{
		double x = to.X - from.X;
		return Math.Atan2(to.Y - from.Y, x);
	}

	public double GetAngle(double fromX, double fromY, double toX, double toY)
	{
		double x = toX - fromX;
		return Math.Atan2(toY - fromY, x);
	}

	public WorldPosData OffsetByAngle(double angle, double distance)
	{
		double resultX = X + Math.Cos(angle) * distance;
		double resultY = Y + Math.Sin(angle) * distance;
		return new WorldPosData(resultX, resultY);
	}

	public static WorldPosData FromPolarOffset(double originX, double originY, double angle, double distance)
	{
		double x = originX + Math.Cos(angle) * distance;
		double y = originY + Math.Sin(angle) * distance;
		return new WorldPosData(x, y);
	}

	public WorldPosData Add(WorldPosData position)
	{
		return new WorldPosData(X + position.X, Y + position.Y);
	}

	public WorldPosData Offset(float offsetX, float offsetY)
	{
		return new WorldPosData(X + (double)offsetX, Y + (double)offsetY);
	}

	public WorldPosData Subtract(WorldPosData position)
	{
		return new WorldPosData(X - position.X, Y - position.Y);
	}

	public override bool Equals(object other)
	{
		WorldPosData worldPosData = (WorldPosData)other;
		if (worldPosData == null)
		{
			return false;
		}
		if (X == worldPosData.X)
		{
			return Y == worldPosData.Y;
		}
		return false;
	}

	public void ScaleInPlace(double factor)
	{
		X *= factor;
		Y *= factor;
	}

	public double Dot(WorldPosData position)
	{
		return X * position.X + Y * position.Y;
	}

	public virtual object Clone()
	{
		return new WorldPosData
		{
			X = X,
			Y = Y
		};
	}

	public override string ToString()
	{
		return $"{{ X={X}, Y={Y} }}";
	}

	public string ToCoordString()
	{
		return $"{{ X={X:F2}, Y={Y:F2} }}";
	}

	public override int GetHashCode()
	{
		return (X, Y).GetHashCode();
	}
}
