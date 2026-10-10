using System.ComponentModel;

namespace ExaltHelper.Proxy;

internal enum EffectType
{
	[Description("Unknown effect")]
	Unknown,
	[Description("Particles around player when they are healed")]
	Heal,
	[Description("Blue particles around players after they teleport")]
	Teleport,
	[Description("Unused Necromancer particle stream")]
	Stream,
	[Description("Bombs, Traps, Poisons")]
	Throw,
	[Description("AoE Buff particles")]
	Nova,
	[Description("Particles on a poisoned enemy")]
	Poison,
	[Description("Line effects like spellbomb to player and pet heals")]
	Line,
	[Description("AoE effects")]
	Burst,
	[Description("Unused Necromancer particle stream")]
	Flow,
	[Description("AoE effects")]
	Ring,
	[Description("Scepter lightning")]
	Lightning,
	[Description("Mystic Orb effect")]
	Collapse,
	[Description("Unused 2D conical effect")]
	Coneblast,
	[Description("Realm Earthquake")]
	Jitter,
	[Description("Loot Bag despawn effect")]
	Flash,
	[Description("Enemy projectile toss")]
	ThrowProjectile,
	[Description("Pet Electric particles")]
	Shocker,
	[Description("Electrified Enemy effect")]
	Shockee,
	[Description("Pet Rising Fury effect")]
	RisingFury,
	[Description("AoE Buff effects")]
	NovaNoAoe,
	[Description("Unknown")]
	InspiredEffect,
	[Description("Unknown")]
	HolyBeamEffect,
	[Description("Unknown")]
	CircleTelegraphEffect,
	[Description("Unknown")]
	ChaosBeamEffect,
	[Description("Unknown")]
	TeleportMonsterEffect,
	[Description("Unknown")]
	MeteorEffect,
	[Description("Transformed Item Effects")]
	GildedBuff,
	[Description("Transformed Item Effects")]
	JadeBuff,
	[Description("Transformed Item Effects")]
	ChaosBuff,
	[Description("Transformed Item Effects")]
	ThunderBuff,
	[Description("Unknown")]
	StatusFlash,
	[Description("Transformed Item Effects")]
	FireOrbBuff
}
