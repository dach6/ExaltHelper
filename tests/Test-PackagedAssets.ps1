param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotPath = '',
    [switch]$NamesOnly
)
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path $AssemblyPath).Path
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('ExaltAssetTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$harness = Join-Path $testDirectory 'AssetTest.dll'
Add-Type -OutputAssembly $harness -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
public sealed class PackagedAssetTest : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static int checks;
    static object Field(object obj, string name) { return obj.GetType().GetField(name,F).GetValue(obj); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    public void Run(string path, string screenshot, bool namesOnly)
    {
        var assembly = Assembly.LoadFrom(path);
        string prefix = assembly.GetName().Name == "ExaltHUD" ? "ExaltHUD" : "ExaltHelper";
        Type type = assembly.GetType(prefix + ".ItemSpriteManager", true);
        var metadata = type.GetMethod("GetItemMetadata",F);
        var sprite = type.GetMethod("GetItemSprite",F);
        // Query during the background warm-up. Every caller must see final names.
        Parallel.For(0, 12, i => {
            var item = metadata.Invoke(null, new object[]{7099});
            if ((string)Field(item,"Name") != "Ghost of Skuld") throw new Exception("Early metadata request returned a placeholder");
        });
        Check((string)Field(metadata.Invoke(null,new object[]{7097}),"Name") == "Pumpkin King", "Pumpkin King's embedded name is missing");
        Check((bool)Field(metadata.Invoke(null,new object[]{1230}),"IsShiny"),"Embedded shiny labels are missing");
        if (namesOnly) { Console.WriteLine("PASS: embedded names load without external assets."); return; }
        Check((string)Field(metadata.Invoke(null,new object[]{1230}),"Rarity") == "Legendary", "Packaged forge rarity missing");
        var weapon = metadata.Invoke(null,new object[]{17842});
        var bullets = (IList)Field(weapon,"Projectiles");
        Check(bullets.Count == 6, "Fractal Blades must expose all six projectile definitions");
        foreach (var bullet in bullets) {
            int id=(int)Field(bullet,"Id");
            Check((int)Field(bullet,"MinDamage") == (id<2?75:55) && (int)Field(bullet,"MaxDamage") == (id<2?105:75), "Fractal damage range incorrect: "+id);
            Check(Math.Abs((float)Field(bullet,"RateOfFire") - 0.9f)<0.0001f, "Fractal fire rate incorrect: "+id);
        }
        int[] gear = {17842, 21228, 14471, 3813};
        string[] labels = {"Fractal Blades", "Darkened Sun", "Abandoned Shadows", "T7 Vitality"};
        using (var preview = new Bitmap(800, 185))
        using (var graphics = Graphics.FromImage(preview))
        using (var font = new Font("Segoe UI", 10))
        {
            graphics.Clear(Color.FromArgb(24,26,35));
            for (int i=0;i<gear.Length;i++) {
                var icon=(Bitmap)sprite.Invoke(null,new object[]{gear[i]});
                Check(icon != null, "Missing packaged sprite: "+labels[i]);
                int opaque=0;
                for(int y=0;y<icon.Height;y++)for(int x=0;x<icon.Width;x++)if(icon.GetPixel(x,y).A>0)opaque++;
                Check(opaque>5,"Empty sprite: "+labels[i]);
                type.GetMethod("DrawEquipmentSlot",F).Invoke(null,new object[]{graphics, i*200+60, 35, 80, 80, gear[i], i, null, -1, false});
                graphics.DrawString(labels[i],font,Brushes.White,i*200+12,135);
            }
            if (!string.IsNullOrEmpty(screenshot)) preview.Save(screenshot, ImageFormat.Png);
        }
        Console.WriteLine("PASS: "+checks+" packaged asset assertions (isolated application directory).");
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($harness))
$setup = New-Object AppDomainSetup
$setup.ApplicationBase = Split-Path $assembly -Parent
$domain = [AppDomain]::CreateDomain('Exalt packaged assets', $null, $setup)
try {
    $runner = $domain.CreateInstanceFromAndUnwrap($harness, 'PackagedAssetTest')
    $runner.Run($assembly, $ScreenshotPath, $NamesOnly.IsPresent)
} finally {
    [AppDomain]::Unload($domain)
    Remove-Item -LiteralPath $harness -Force
    Remove-Item -LiteralPath $testDirectory -Force
}
