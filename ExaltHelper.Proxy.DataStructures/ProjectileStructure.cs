using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ExaltHelper.Proxy.DataStructures;

public class ProjectileStructure : IGameEntity<byte>
{
	[CompilerGenerated]
	private sealed class ProjectileStructureParser
	{
		public Dictionary<string, float> EffectsByName;

		internal void ParseProjectileXml(XElement element)
		{
			EffectsByName[element.Value] = element.GetAttributeValue("duration", "0").ParseFloat();
		}
	}

	[CompilerGenerated]
	private byte _idField;

	public bool ArmorPiercing;

	public Dictionary<string, float> StatusEffects;

	[CompilerGenerated]
	private string _nameField;

	public byte ID
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

	public ProjectileStructure(XElement projectile)
	{
		ID = (byte)projectile.GetAttributeValue("id", "0").ParseInt();
		ArmorPiercing = projectile.HasElement("ArmorPiercing");
		Dictionary<string, float> effectsByName = new Dictionary<string, float>();
		projectile.Elements("ConditionEffect").ForEach(delegate(XElement element)
		{
			effectsByName[element.Value] = element.GetAttributeValue("duration", "0").ParseFloat();
		});
		StatusEffects = effectsByName;
		Name = projectile.GetStringValue("ObjectId", "");
	}

	public override string ToString()
	{
		return $"Projectile: {Name} (0x{ID:X})";
	}
}
