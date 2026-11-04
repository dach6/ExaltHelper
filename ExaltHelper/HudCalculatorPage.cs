using System;
using System.Collections.Generic;
using System.Linq;

namespace ExaltHelper;

internal sealed partial class HudCalculatorPage
{
    public static double WeaponDps(ItemSpriteManager.ItemMeta item, double att, double dex, double def, double bonus, bool damaging, bool berserk, bool cursed, bool broken)
    {
        double aps = (1.5 + 6.5 * dex / 75) * (berserk ? 1.25 : 1);
        double multiplier = (.5 + att / 50) * (1 + bonus / 100) * (damaging ? 1.25 : 1);
        IEnumerable<ItemSpriteManager.ProjectileInfo> bullets = item.Projectiles;
        if (!bullets.Any()) bullets = new[] { new ItemSpriteManager.ProjectileInfo { MinDamage = item.MinDamage, MaxDamage = item.MaxDamage, RateOfFire = item.RateOfFire, NumProjectiles = item.NumProjectiles, ArmorPiercing = item.ArmorPiercing } };
        double damage = 0;
        foreach (var bullet in bullets)
        {
            // Expected post-defense damage across the uniform projectile roll, including the minimum-damage floor.
            int low = Math.Max(0, bullet.MinDamage), high = Math.Max(low + 1, bullet.MaxDamage);
            double total = 0;
            for (int roll = low; roll < high; roll++)
            {
                int raw = (int)((float)roll * (float)multiplier);
                int applied = Math.Max(raw * 2 / 20, raw - (broken || bullet.ArmorPiercing ? 0 : (int)def));
                total += cursed ? (int)(applied * 1.25) : applied;
            }
            damage += total / (high - low) * Math.Max(1, bullet.NumProjectiles) * bullet.RateOfFire * aps;
        }
        return damage;
    }
}
