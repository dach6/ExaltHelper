param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotDirectory = "$PSScriptRoot/../artifacts/calculator"
)
$ErrorActionPreference = 'Stop'
if ([IO.DriveInfo]::new('C:\').AvailableFreeSpace -le 5GB) { throw 'STOP: C: free space is at or below 5 GB.' }
$env:EXALTHELPER_HUD_DATA = Join-Path ([IO.Path]::GetTempPath()) ('exalthelper-calculator-' + [Guid]::NewGuid().ToString('N'))
Add-Type -ReferencedAssemblies System.Core,System.Xml.Linq,System.Xml,System.Windows.Forms,System.Drawing -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;

public static class ExpandedCalculatorRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly assembly; static int checks;
    static Type T(string n) { return assembly.GetType("ExaltHelper." + n, true); }
    static object New(string n) { return Activator.CreateInstance(T(n), true); }
    static object Field(object o, string n) { return o.GetType().GetField(n,F).GetValue(o); }
    static object Property(object o,string n) { return o.GetType().GetProperty(n,F).GetValue(o,null); }
    static void Set(object o, string n, object v) { var f=o.GetType().GetField(n,F); if(f!=null)f.SetValue(o,v);else o.GetType().GetProperty(n,F).SetValue(o,v,null); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n,F).Invoke(o,a); }
    static object Static(string t, string n, params object[] a) { return T(t).GetMethod(n,F).Invoke(null,a); }
    static double N(object o, string n) { var p=o.GetType().GetProperty(n,F); return Convert.ToDouble(p==null?Field(o,n):p.GetValue(o,null)); }
    static void Expect(double actual,double expected,string name) { if(double.IsNaN(actual)||double.IsInfinity(actual)||Math.Abs(actual-expected)>0.00001)throw new Exception(name+": expected "+expected+", got "+actual);checks++; }
    static void True(bool value,string name) { if(!value)throw new Exception(name);checks++; }
    static object Parse(string body,int id) { return Static("HudCalculatorData","ParseItem",XElement.Parse("<Object type='"+id+"' id='Test fixture'><Item/><SlotType>11</SlotType>"+body+"</Object>")); }
    static object Ability(object item,int[] stats,int hex) { return Static("HudCalculatorEngine","Ability",item,((IList)Field(item,"Modes"))[0],stats,hex,"",100d); }
    static object Effect(string effect,int[] stats,int hex) { return Ability(Parse(effect,900001),stats,hex); }
    static string Details(object result) { return string.Join(" ",((IEnumerable)Field(result,"Lines")).Cast<object>().Select(x=>Convert.ToString(Field(x,"Detail")))); }
    static double Proc(object item,int[] stats,double rate) { return ((IEnumerable)Static("HudCalculatorEngine","WeaponProcs",item,stats,rate)).Cast<object>().Where(x=>!(bool)Field(x,"Conditional")).Sum(x=>N(x,"Damage")); }
    static object Weapon(int min,int max,bool piercing) { var w=New("ItemSpriteManager+ItemMeta");Set(w,"MinDamage",min);Set(w,"MaxDamage",max);Set(w,"ArmorPiercing",piercing);return w; }
    static double Dps(object w,bool weak,bool dazed,bool armored,bool exposed,bool broken) {return (double)Static("HudCalculatorEngine","WeaponDpsWithConditions",w,75d,75d,100d,0d,true,true,false,broken,weak,dazed,armored,exposed);}

    public static void Run(string path,string screenshotDirectory)
    {
        assembly=Assembly.LoadFrom(Path.GetFullPath(path));
        int[] stats={700,400,75,25,70,75,40,80};
        object spell=Effect("<Projectile><MinDamage>100</MinDamage><MaxDamage>200</MaxDamage></Projectile><NumProjectiles>3</NumProjectiles><Activate scalingStat='ATT' statModScalingMin='50' statModDamage='8'>Shoot</Activate>",stats,0);
        Expect(N(spell,"DamagePerUse"),1050,"Projectile stat scaling and all-shot potential");
        var low=(int[])stats.Clone();low[2]=20;
        Expect(N(Effect("<Projectile><MinDamage>100</MinDamage><MaxDamage>100</MaxDamage></Projectile><Activate scalingStat='ATT' statModScalingMin='50' statModDamage='8'>Shoot</Activate>",low,0),"DamagePerUse"),100,"Scaling floor does not subtract damage");
        Expect(N(Effect("<Activate duration='3' impactDamage='1000' totalDamage='1500' scalingStat='MAXMP' statModScalingMin='300' statModDamage='50' statModImpactDamage='15'>PoisonGrenade</Activate>",stats,0),"DamagePerUse"),2000,"Poison total includes impact and uses one-tenth scaling");
        Expect((double)Static("HudCalculatorEngine","Poison",XElement.Parse("<Activate duration='1' impactDamage='380' totalDamage='990'>PoisonGrenade</Activate>"),stats,true),868,"Poison expiry tick excludes final tick");
        Expect(N(Effect("<Activate totalDamage='425' bombDamage='600' scalingStat='SPD' statModScalingMin='50' statModTrapDamage='5' statModBombDamage='13'>Trap</Activate>",stats,0),"DamagePerUse"),1385,"Trap and follow-up bomb scale separately");
        object lightning=Effect("<Activate totalDamage='500' maxTargets='3' decrDamage='50' wisDamageBase='10' wisMin='50' wisPerTarget='10' aoeDamage='100' aoeActivationCount='2' aoeMaxTargets='2' scalingStat='WIS' statModScalingMin='50' statModDamage='20' statModPerTarget='10'>Lightning</Activate>",stats,0);
        Expect(N(lightning,"DamagePerUse"),850,"Lightning boss case uses one chain target and all shock triggers");
        Expect(N(lightning,"MultiTargetDamage"),4030,"Lightning AoE applies target scaling and chain falloff");
        Expect(N(Effect("<Activate totalDamage='400' wisDamageBase='2' wisMin='50' ignoreDef='30'>VampireBlast</Activate>",stats,0),"DamagePerUse"),460,"Skull uses direct WIS scaling");
        Expect(N(Effect("<Activate minDamage='100' maxDamage='100' activationCount='2' scalingStat='WIS' statModScalingMin='70' statModDamage='7.2' statModActivationCount='.15'>DamageNova</Activate>",stats,0),"DamagePerUse"),516,"Nova scales damage directly and floors additional waves");
        string hex="<Activate baseDamage='50' stackDamage='5' stackMultiplier='.01' scalingStat='MAXMP' statModScalingMin='300' statModBaseDamage='1' statModStackDamage='.1'>DetonateHex</Activate>";
        Expect(N(Effect(hex,stats,20),"DamagePerUse"),540,"Hex explicit stacks with nonlinear multiplier");
        Expect(N(Effect(hex,stats,500),"DamagePerUse"),3300,"Hex stacks capped at 100");
        object thresholds=Effect("<OnDetonateHexActivate minHex='25' maxHex='49' totalDamage='500'>PoisonGrenade</OnDetonateHexActivate><OnDetonateHexActivate minHex='50' maxHex='100' totalDamage='650'>PoisonGrenade</OnDetonateHexActivate>",stats,50);
        Expect(N(thresholds,"DamagePerUse"),650,"Hex threshold chooses only matching tier");
        True(Details(Effect("<Activate amount='140' scalingStat='WIS' statModScalingMin='70' statModAmount='.99'>Heal</Activate>",stats,0)).Contains("150 HP"),"Restore has direct scaling and midpoint rounding");
        object cloak=Effect("<Activate duration='5'>Sneak</Activate><OnConditionEndActivate ignoreFlat='70' ignorePerc='.5' scalingStat='WIS' statModScalingMin='34' statModFlat='3' statModPerc='.005' duration='2.4'>LethalStrike</OnConditionEndActivate>",stats,0);
        Expect(N(cloak,"DamagePerUse"),0,"Lethal Strike not fabricated as ordinary cast damage");
        True(Details(cloak).Contains("281 conditional bonus"),"Lethal Strike includes target DEF in conditional bonus");
        Expect(N(Effect("<Projectile><Damage>100</Damage></Projectile><Activate effect='Berserk'>ShurikenAbility</Activate>",stats,0),"DamagePerUse"),0,"Hold-state shuriken condition does not add damage");
        object modes=Parse("<Ability><MpCost>30</MpCost><Activate totalDamage='100'>PoisonGrenade</Activate></Ability><Ability name='Second'><MpCost>60</MpCost><Activate totalDamage='200'>PoisonGrenade</Activate></Ability>",900002);
        True(((IList)Field(modes,"Modes")).Count==2,"Independent ability modes parsed");
        Expect(N(Ability(modes,stats,0),"DamagePerUse"),100,"One selected mode does not sum alternative modes");
        Expect((double)Static("HudCalculatorEngine","ProcRate",.5d,1d,4d),.8,"Proc cooldown renewal expectation");
        Expect((double)Static("HudCalculatorEngine","ProcRate",1d,.35d,8d),8d/3,"Proc cooldown quantized to eligible volley intervals");
        Expect((double)Static("HudCalculatorEngine","ProcRate",.25d,0d,8d),2,"Ungated proc probability per shot");
        Expect((double)Static("HudCalculatorEngine","ProcRate",0d,1d,8d),0,"Zero probability proc");
        object procItem=Parse("<OnPlayerShootActivate proc='1' cooldown='1' totalDamage='100' duration='0'>PoisonGrenade</OnPlayerShootActivate><OnPlayerShootActivate proc='1' mustWear='0x1' totalDamage='9999'>PoisonGrenade</OnPlayerShootActivate><Projectile id='0'><ConditionEffect duration='3'>Bleeding</ConditionEffect></Projectile><Projectile id='1'><ConditionEffect duration='2' bleedDamage='50'>Bleeding</ConditionEffect></Projectile>",900003);
        Expect(Proc(procItem,stats,8),900,"Unconditional poison plus strongest bleeding; gated synergy excluded");
        Expect((double)Static("HudCalculatorEngine","CastRate",2d,1d,100d,400d,20d),.2,"Sustained casting is mana limited");
        Expect((double)Static("HudCalculatorEngine","CastRate",10d,2d,100d,400d,1000d),.5,"Sustained casting is cooldown limited");
        Expect((double)Static("HudCalculatorEngine","CastRate",1d,0d,500d,400d,1000d),0,"Cannot cast above maximum MP");
        Expect((double)Static("HudCalculatorEngine","CastRate",2d,0d,0d,0d,0d),2,"Zero-cost ability follows requested rate without invented cooldown");
        Expect((double)Static("HudCalculatorEngine","PetThroughput",407,100),90,"Level 100 pet Heal");
        Expect((double)Static("HudCalculatorEngine","PetThroughput",408,100),45,"Level 100 pet Magic Heal");
        Expect((double)Static("HudCalculatorEngine","PetThroughput",402,100),1000,"Attack Close uses game ability ID");
        Expect((double)Static("HudCalculatorEngine","PetThroughput",409,100),0,"Savage is not a damaging pet ability");
        Expect(Convert.ToDouble(Static("HudCalculatorEngine","CombatTrigger",125)),60,"Combat trigger cap");
        Expect(Convert.ToDouble(Static("HudCalculatorEngine","CombatTrigger",35)),30,"Combat trigger defense brackets");
        Expect(Dps(Weapon(100,100,false),true,false,false,false,false),50,"Weak suppresses ATT and Damaging");
        Expect(Dps(Weapon(100,100,false),false,true,false,false,false),225,"Dazed suppresses DEX and Berserk");
        Expect(Dps(Weapon(100,100,false),false,false,true,false,false),1000,"Armored target scales defense before subtraction");
        Expect(Dps(Weapon(100,100,true),false,false,true,true,false),2700,"Exposed applies after armor-piercing clears defense");
        Expect(Dps(Weapon(0,0,false),false,false,false,true,false),0,"Exposed does not turn zero-damage projectiles into hits");

        var catalog=(IDictionary)T("HudCalculatorData").GetProperty("Items",F).GetValue(null,null);
        True(catalog.Count>10000,"Production object catalog loaded");
        var gear=Parse("<ActivateOnEquip stat='ATT' amount='10'>IncrementStat</ActivateOnEquip><ActivateOnEquip stat='WIS' amount='5'>IncrementStat</ActivateOnEquip><ActivateOnEquip stat='MAXHP' amount='20'>IncrementStat</ActivateOnEquip><ActivateOnEquip stat='MAXHP' statRelativeTo='MAXHP' amount='10'>IncrementStatRelative</ActivateOnEquip><ActivateOnEquip stat='ATT' statRelativeTo='WIS' amount='50'>BonusStatRelative</ActivateOnEquip><ActivateOnEquip stat='HP' amount='2'>FlatRegen</ActivateOnEquip><ActivateOnEquip stat='MP' amount='.01'>PercentageRegen</ActivateOnEquip><ActivateOnEquip multiplier='.8'>AbilityUseDiscount</ActivateOnEquip><XPBonus>7</XPBonus>",900004);
        catalog[900004]=gear;
        object derived=Static("HudCalculatorEngine","Stats",new double[]{700,300,75,25,50,75,40,80},new[]{900004},new[]{""},new double[]{5,0,2,0,0,0,0,3},null);
        int[] effective=(int[])Field(derived,"Effective");
        Expect(effective[0],798,"Total-relative gear includes exalt and rounds half away from zero");
        Expect(effective[2],91,"Bonus-relative gear uses flat bonus plus exalt, excluding base");
        Expect(N(derived,"MpMultiplier"),.8,"Equipment MP discount");
        Expect(N(derived,"HpGearRegen"),4,"Flat equipment regen remains separate from stat regen");
        Expect(N(derived,"MpGearRegen"),6,"Percent equipment regen uses effective max MP");
        Expect(N(derived,"XpBonus"),7,"Base equipment XP bonus");
        string attackEnchant=Convert.ToBase64String(new byte[]{0,2,4,7,1,253,0}).Replace('+','-').Replace('/','_');
        object enchanted=Static("HudCalculatorEngine","Stats",new double[]{700,300,75,25,50,75,40,80},new[]{900004},new[]{attackEnchant},null,null);
        Expect(((double[])Field(enchanted,"Gear"))[2],13.9,"Imported real enchant adds fractional ATT before bonus-relative gear");
        object snap=New("AccountSnapshot");Set(snap,"AccountKey","calculator-test-account-a");Set(snap,"Revision",1L);Set(snap,"CurrentCharacterId",7);
        var exalts=(IDictionary)Property(snap,"Exaltations");exalts["782"]=Enumerable.Repeat(30,8).ToArray();
        object proficiency=Static("HudCalculatorEngine","Proficiency",snap,782);
        Expect(N(proficiency,"MasteryPercent"),7.5,"Class mastery uses minimum stat level");
        Expect(N(proficiency,"XpPercent"),15,"Fast Learner uses total exaltation levels");
        Expect(N(proficiency,"LootPercent"),0,"Missing weapon-group classes give zero proficiency");
        foreach(int type in new[]{801,803})exalts[type.ToString()]=Enumerable.Repeat(75,8).ToArray();
        proficiency=Static("HudCalculatorEngine","Proficiency",snap,782);
        Expect(N(proficiency,"LootPercent"),15,"Weapon proficiency uses group minimum");
        var character=New("AccountCharacter");Set(character,"Id",7);Set(character,"ClassType",782);Set(character,"Name","Calculator fixture");Set(character,"PetId",19);
        Set(character,"Stats",stats);Set(character,"Inventory",new[]{-1,0x5cbe,-1,-1});Set(character,"Enchants",new string[4]);
        ((IList)Property(snap,"Characters")).Add(character);
        var pet=New("AccountPet");Set(pet,"Id",19);Set(pet,"Name","Fixture pet");Set(pet,"Rarity",0);Set(pet,"AbilityTypes",new[]{407,408,402});Set(pet,"AbilityLevels",new[]{100,100,100});
        ((IList)Property(snap,"Pets")).Add(pet);
        T("AccountWorkspace").GetField("state",F).SetValue(null,snap);
        var cornea=Static("HudCalculatorData","Item",0x5cbe);
        True(cornea!=null,"Real Command Cornea data resolves");
        Expect(N(Ability(cornea,stats,0),"DamagePerUse"),2000,"Real Command Cornea zero-hex nominal potential");
        Expect(N(Ability(cornea,stats,50),"DamagePerUse"),3250,"Real Command Cornea scaled hex plus selected poison tier");
        var tome=Static("HudCalculatorData","Item",0xb25);
        Expect(N(Ability(tome,stats,0),"DamagePerUse"),1424,"Real Tome of Holy Guidance nova damage");
        int abilityModes=0;
        foreach(object item in catalog.Values)
        {
            if(!(bool)item.GetType().GetProperty("IsAbility",F).GetValue(item,null))continue;
            foreach(object m in (IEnumerable)Field(item,"Modes"))
            {
                var a=Static("HudCalculatorEngine","Ability",item,m,stats,50,"",100d);
                double damage=N(a,"DamagePerUse"); if(double.IsNaN(damage)||double.IsInfinity(damage)||damage<0)throw new Exception("Invalid production ability output.");
                abilityModes++;
            }
        }
        True(abilityModes>700,"Every production ability mode evaluated without invalid numbers");
        Console.WriteLine("Production ability modes evaluated: "+abilityModes);

        using(var form=new Form { Width=1166,Height=960,StartPosition=FormStartPosition.Manual,Location=new Point(-2000,-2000) })
        using(var page=(UserControl)New("HudCalculatorPage"))
        {
            form.Controls.Add(page); form.Show(); Application.DoEvents(); Call(page,"LoadItems");
            var combos=(ComboBox[])Field(page,"equipment");
            combos[0].SelectedItem=combos[0].Items.Cast<object>().First(x=>Convert.ToInt32(Field(x,"Id"))>0);
            combos[1].SelectedItem=combos[1].Items.Cast<object>().First(x=>Convert.ToInt32(Field(x,"Id"))==0x5cbe);
            ((NumericUpDown)Field(page,"hex")).Value=50;
            Application.DoEvents();
            True(((DataGridView)Field(page,"statGrid")).Rows.Count==8,"UI displays all eight stat breakdowns");
            True(((DataGridView)Field(page,"abilityGrid")).Rows.Count>=3,"UI displays ability components");
            Call(page,"SaveSetup"); ((NumericUpDown)Field(page,"hex")).Value=0; Call(page,"RestoreSetup");
            Expect((double)((NumericUpDown)Field(page,"hex")).Value,50,"Setup save/restore retains explicit hex assumption");
            Call(page,"UseCharacter");
            var grid=(DataGridView)Field(page,"statGrid");
            Expect(Convert.ToDouble(grid.Rows[0].Cells[5].Value),715,"API character import applies class HP exaltation once");
            Expect(Convert.ToDouble(grid.Rows[7].Cells[5].Value),93,"API character import applies gear and class WIS exaltation once");
            Expect((double)((NumericUpDown)Field(page,"mastery")).Value,7.5,"API character import sets mastery endpoint multiplier");
            Expect((double)((NumericUpDown)Field(page,"petHeal")).Value,100,"Unlocked pet Heal imported");
            Expect((double)((NumericUpDown)Field(page,"petMagic")).Value,0,"Locked pet Magic Heal excluded");
            var live=New("Proxy.DataStructures.LocalPlayerInfo");
            string[] baseNames={"BaseLife","BaseMana","BaseAttack","BaseDefense","BaseSpeed","BaseDexterity","BaseVitality","BaseWisdom"};
            string[] totalNames={"MaxHp","MaxMp","Attack","Defense","Speed","Dexterity","Vitality","Wisdom"};
            int[] liveTotals={735,435,86,38,73,84,45,102};
            for(int i=0;i<8;i++){Set(live,baseNames[i],stats[i]);Set(live,totalNames[i],liveTotals[i]);}
            Set(live,"EquipmentIds",new[]{-1,0x5cbe,-1,-1});Set(live,"ExaltationBonusDamage",1080);Set(live,"HasChallengeDamageBonus",true);Set(live,"ChallengeDamageMultiplier",1.24);Set(live,"Damaging",true);Set(live,"Berserk",true);
            Call(page,"SetLive",live);Call(page,"UseCurrent");
            for(int i=0;i<8;i++)Expect(Convert.ToDouble(grid.Rows[i].Cells[5].Value),liveTotals[i],"Live import preserves effective "+i);
            Expect((double)((NumericUpDown)Field(page,"exalt")).Value,8,"Observed exalt damage copied as percentage");
            Expect((double)((NumericUpDown)Field(page,"challenge")).Value,24,"Observed event damage multiplier copied");
            True(((CheckBox)Field(page,"damaging")).Checked&&((CheckBox)Field(page,"berserk")).Checked,"Observed damage conditions imported");
            Call(page,"SaveSetup");
            Directory.CreateDirectory(screenshotDirectory);
            using(var bitmap=new Bitmap(page.Width,page.Height)){page.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(screenshotDirectory,"calculator-wide.png"));}
            form.Width=636; Application.DoEvents(); Call(page,"ResizeContent"); Application.DoEvents();
            var body=(FlowLayoutPanel)Field(page,"body");
            True(!body.HorizontalScroll.Visible,"Narrow calculator wraps without horizontal page scroll");
            using(var bitmap=new Bitmap(page.Width,page.Height)){page.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(screenshotDirectory,"calculator-narrow.png"));}
            body.AutoScrollPosition=new Point(0,Math.Max(0,body.DisplayRectangle.Height-body.ClientSize.Height));Application.DoEvents();
            using(var bitmap=new Bitmap(page.Width,page.Height)){page.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(screenshotDirectory,"calculator-details.png"));}
            var other=New("AccountSnapshot");Set(other,"AccountKey","calculator-test-account-b");Set(other,"Revision",2L);T("AccountWorkspace").GetField("state",F).SetValue(null,other);Call(page,"RefreshCharacters");
            True(combos.All(c=>Convert.ToInt32(Field(c.SelectedItem,"Id"))==-1),"Account change clears previous equipment");
            True(((string[])Field(page,"enchantCodes")).All(string.IsNullOrEmpty),"Account change clears imported enchant data");
            Expect((double)((NumericUpDown)Field(page,"mastery")).Value,0,"Account change clears imported damage bonuses");
            Call(page,"RestoreSetup");
            True(((Label)Field(page,"source")).Text.Contains("No saved setup"),"Another account cannot restore prior character setup");
            form.Close();
        }
        Console.WriteLine("PASS: "+checks+" expanded calculator assertions.");
    }
}
'@
try { [ExpandedCalculatorRegression]::Run($AssemblyPath, [IO.Path]::GetFullPath($ScreenshotDirectory)) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
