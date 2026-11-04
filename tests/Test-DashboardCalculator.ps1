param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Core -TypeDefinition @'
using System;
using System.Collections;
using System.IO;
using System.Reflection;

public static class DashboardCalculatorRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly;
    static int checks;
    static Type T(string name) { return assembly.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static void Set(object o, string name, object value) { o.GetType().GetField(name, F).SetValue(o, value); }
    static void Expect(double actual, double expected, string name)
    { if (Math.Abs(actual - expected) > 0.00001) throw new Exception(name + ": expected " + expected + ", received " + actual); checks++; }
    static object Weapon(int min, int max, int count, float rate, bool piercing)
    {
        object item = New("ItemSpriteManager+ItemMeta");
        Set(item, "MinDamage", min); Set(item, "MaxDamage", max); Set(item, "NumProjectiles", count);
        Set(item, "RateOfFire", rate); Set(item, "ArmorPiercing", piercing); return item;
    }
    static double Dps(object item, double att, double dex, double def, double bonus, bool damaging, bool berserk, bool curse, bool broken)
    {
        return (double)T("HudCalculatorPage").GetMethod("WeaponDps", F).Invoke(null,
            new object[] { item, att, dex, def, bonus, damaging, berserk, curse, broken });
    }
    public static void Run(string path)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        // Independent fixed arithmetic cases from RealmHound's damage.rs/derived.rs:
        // variable weapon rolls exclude max; each scaled shot and cursed hit truncate to int.
        Expect(Dps(Weapon(100, 101, 1, 1f, false), 75, 75, 100, 0, false, false, false, false), 800, "Exclusive upper damage bound");
        Expect(Dps(Weapon(1, 1, 1, 1f, false), 1, 0, 0, 0, false, false, false, false), 0, "Per-shot integer truncation");
        Expect(Dps(Weapon(100, 100, 1, 1f, false), 0, 0, 100, 0, false, false, false, false), 7.5, "Ten-percent defense floor");
        Expect(Dps(Weapon(101, 101, 1, 1f, false), 75, 75, 91, 0, false, false, true, false), 1104, "Curse integer truncation");
        Expect(Dps(Weapon(100, 100, 1, 1f, true), 75, 75, 10000, 0, false, false, false, false), 1600, "Armor-piercing ignores defense");
        Expect(Dps(Weapon(100, 100, 1, 1f, false), 75, 75, 10000, 0, false, false, false, true), 1600, "Armor-broken target ignores defense");
        Expect(Dps(Weapon(100, 100, 1, 1f, false), 75, 75, 70, 10, true, true, true, false), 2560, "Combined damage, speed and exaltation buffs");
        object multi = Weapon(0, 0, 1, 1f, false);
        var bullets = (IList)multi.GetType().GetField("Projectiles", F).GetValue(multi);
        object first = New("ItemSpriteManager+ProjectileInfo");
        Set(first, "MinDamage", 100); Set(first, "MaxDamage", 100); Set(first, "NumProjectiles", 2); Set(first, "RateOfFire", 1f);
        object second = New("ItemSpriteManager+ProjectileInfo");
        Set(second, "MinDamage", 50); Set(second, "MaxDamage", 50); Set(second, "NumProjectiles", 3); Set(second, "RateOfFire", .5f);
        bullets.Add(first); bullets.Add(second);
        Expect(Dps(multi, 75, 75, 0, 0, false, false, false, false), 4400, "Independent multi-projectile rates and counts");
        Console.WriteLine("PASS: " + checks + " theoretical DPS arithmetic assertions.");
    }
}
'@
try { [DashboardCalculatorRegression]::Run($AssemblyPath) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
