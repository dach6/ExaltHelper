using System;

namespace ExaltHelper.Proxy.DataStructures;

[Flags]
internal enum ConditionEffectBits
{
	Dead = 1,
	Quiet = 2,
	Weak = 4,
	Slowed = 8,
	Sick = 0x10,
	Dazed = 0x20,
	Stunned = 0x40,
	Blind = 0x80,
	Hallucinating = 0x100,
	Drunk = 0x200,
	Confused = 0x400,
	StunImmune = 0x800,
	Invisible = 0x1000,
	Paralyzed = 0x2000,
	Speedy = 0x4000,
	Bleeding = 0x8000,
	ArmorBreakImmune = 0x10000,
	Healing = 0x20000,
	Damaging = 0x40000,
	Berserk = 0x80000,
	InCombat = 0x100000,
	Stasis = 0x200000,
	StasisImmune = 0x400000,
	Invincible = 0x800000,
	Invulnerable = 0x1000000,
	Armored = 0x2000000,
	ArmorBroken = 0x4000000,
	Hexed = 0x8000000,
	NinjaSpeedy = 0x10000000,
	Unstable = 0x20000000,
	Darkness = 0x40000000,
	InCombatDuplicate = InCombat
}
