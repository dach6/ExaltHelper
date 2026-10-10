using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ExaltHelper.Proxy.DataStructures;

public class ObjectStructure : IGameEntity<ushort>
{
	[CompilerGenerated]
	private sealed class ObjectStructureParser
	{
		public Dictionary<ushort, ObjectStructure> ObjectsById;

		internal void ParseObjectXml(XElement element)
		{
			ObjectStructure objectStructure = new ObjectStructure(element);
			ObjectsById[objectStructure.ID] = objectStructure;
		}
	}

	[CompilerGenerated]
	private sealed class ObjectItemParser
	{
		public List<ProjectileStructure> Projectiles;

		internal void AddProjectile(XElement element)
		{
			Projectiles.Add(new ProjectileStructure(element));
		}
	}

	[CompilerGenerated]
	private ushort _idField;

	public string ObjectClass;

	public ushort MaxHP;

	public bool Static;

	public bool OccupySquare;

	public bool EnemyOccupySquare;

	public bool FullOccupy;

	public bool ProtectFromGroundDamage;

	public bool Enemy;

	public bool Player;

	public bool Pet;

	public ushort Size;

	public ushort Defense;

	public bool God;

	public bool Cube;

	public bool Quest;

	public ProjectileStructure[] Projectiles;

	public bool Invulnerable;

	public bool Invincible;

	public bool Container;

	[CompilerGenerated]
	private string _nameField;

	public ushort ID
	{
		[CompilerGenerated]
		get
		{
			return _idField;
		}
		[CompilerGenerated]
		private set
		{
			_idField = value;
		}
	}

	public string Name
	{
		[CompilerGenerated]
		get
		{
			return _nameField;
		}
		[CompilerGenerated]
		private set
		{
			_nameField = value;
		}
	}

	internal static Dictionary<ushort, ObjectStructure> LoadFromXml(XDocument document)
	{
		Dictionary<ushort, ObjectStructure> objectsById = new Dictionary<ushort, ObjectStructure>();
		document.Element("Objects").Elements("Object").ForEach(delegate(XElement obj)
		{
			ObjectStructure objectStructure = new ObjectStructure(obj);
			objectsById[objectStructure.ID] = objectStructure;
		});
		return objectsById;
	}

	public ObjectStructure(XElement obj)
	{
		ID = (ushort)obj.GetAttributeValue("type", "0x0").ParseHexInt();
		ObjectClass = obj.GetStringValue("Class", "GameObject");
		MaxHP = (ushort)obj.GetStringValue("MaxHitPoints", "0").ParseHexInt();
		Static = obj.HasElement("Static");
		OccupySquare = obj.HasElement("OccupySquare");
		EnemyOccupySquare = obj.HasElement("EnemyOccupySquare");
		FullOccupy = obj.HasElement("FullOccupy");
		ProtectFromGroundDamage = obj.HasElement("ProtectFromGroundDamage");
		Enemy = obj.HasElement("Enemy");
		Player = obj.HasElement("Player");
		Pet = obj.HasElement("Pet");
		Size = (ushort)obj.GetStringValue("Size", "0").ParseInt();
		Defense = (ushort)obj.GetStringValue("Defense", "0").ParseInt();
		God = obj.HasElement("God");
		Cube = obj.HasElement("Cube");
		Quest = obj.HasElement("Quest");
		Invulnerable = obj.HasElement("Invulnerable");
		Invincible = obj.HasElement("Invincible");
		Container = ObjectClass == "Container" || ObjectClass == "VaultContainer" || ObjectClass == "VaultGiftContainer" || ObjectClass == "TemporaryGiftContainer";
		List<ProjectileStructure> projectiles = new List<ProjectileStructure>();
		obj.Elements("Projectile").ForEach(delegate(XElement element)
		{
			projectiles.Add(new ProjectileStructure(element));
		});
		Projectiles = projectiles.ToArray();
		string text = obj.GetStringValue("DisplayId", "");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = obj.GetAttributeValue("displayId", "");
		}
		string text2 = obj.GetAttributeValue("id", "");
		Name = ((!string.IsNullOrWhiteSpace(text) && !text.StartsWith("{s.")) ? text : text2);
		if (Player)
		{
			ClassStats classStats = new ClassStats();
			if (!int.TryParse(obj.Element("MaxHitPoints").Attribute("max").Value, out classStats.LifeMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("MaxMagicPoints").Attribute("max").Value, out classStats.ManaMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("Attack").Attribute("max").Value, out classStats.AttackMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("Defense").Attribute("max").Value, out classStats.DefenseMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("Speed").Attribute("max").Value, out classStats.SpeedMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("Dexterity").Attribute("max").Value, out classStats.DexterityMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("HpRegen").Attribute("max").Value, out classStats.VitalityMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			if (!int.TryParse(obj.Element("MpRegen").Attribute("max").Value, out classStats.WisdomMax))
			{
				Console.WriteLine($"Failed parsing player stats of {ID} {ObjectClass}");
			}
			ClassStats.Map.Add(ID, classStats);
		}
	}

	public override string ToString()
	{
		return $"Object: {Name} (0x{ID:X})";
	}
}
