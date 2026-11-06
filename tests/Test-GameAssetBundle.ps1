param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ManifestPath = '',
    [string]$SourceDirectory = '',
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts/game-asset-bundle"
)
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
if ([string]::IsNullOrWhiteSpace($ManifestPath)) { $ManifestPath = Join-Path (Split-Path $assembly -Parent) 'assets/game-assets.json' }
$manifest = (Resolve-Path -LiteralPath $ManifestPath).Path
$taskDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputDirectory)))
if ($taskDrive.AvailableFreeSpace -le 5GB -or (Test-Path -LiteralPath 'C:\Users\Administrator\Desktop\code\.exalthelper-tools\disk-guard\STOP')) { throw 'STOP: disk guard threshold reached.' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$priorData = $env:EXALTHELPER_HUD_DATA
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'isolated-data'
$harness = Join-Path $testRoot 'GameAssetBundleTest.dll'
Add-Type -OutputAssembly $harness -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms,System.Xml,System.Xml.Linq,System.Web.Extensions -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

public sealed class GameAssetBundleRegression : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    const string ExpectedVersion = "7.2.0.1.0";
    const string PreviousObjectsSha256 = "FC9151DEEB7FBF1B3EF9788833C8296E1C16C2D5664A9394421914C8E79570CD";
    const string NativeHookSha256 = "AE58C5FEBD1A773A3DE0BC3A4F6908298A08B94453D937E70C1AF5252854F750";
    static Assembly app;
    static string bundleRoot;
    static int checks;
    static readonly List<string> report = new List<string>();
    static readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 10000000 };
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object Get(object value, string name)
    {
        Type type = value as Type ?? value.GetType(); object target = value is Type ? null : value;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target); }
        var property = type.GetProperty(name, F); if (property == null) throw new MissingMemberException(type.FullName, name);
        return property.GetValue(target, null);
    }
    static object Call(object value, string name, params object[] args)
    { Type type = value as Type ?? value.GetType(); return type.GetMethod(name, F).Invoke(value is Type ? null : value, args); }
    static IDictionary<string, object> Map(object value) { return (IDictionary<string, object>)value; }
    static object Value(IDictionary<string, object> value, string key, object fallback = null) { object found; return value.TryGetValue(key, out found) ? found : fallback; }
    static string Text(IDictionary<string, object> value, string key, string fallback = "") { return Convert.ToString(Value(value, key, fallback), CultureInfo.InvariantCulture); }
    static long Number(IDictionary<string, object> value, string key) { return Convert.ToInt64(value[key], CultureInfo.InvariantCulture); }
    static object[] ArrayValue(IDictionary<string, object> value, string key) { return ((IEnumerable)value[key]).Cast<object>().ToArray(); }
    static void Check(bool pass, string message)
    {
        checks++; string line = (pass ? "PASS " : "FAIL ") + message; report.Add(line); Console.WriteLine(line);
        if (!pass) throw new InvalidOperationException(message);
    }
    static string Inside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Expected a relative manifest path: " + relative);
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(prefix, relative));
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Manifest path escapes its root: " + relative);
        return result;
    }
    static string Hash(Stream stream) { using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", ""); }
    static string HashFile(string path) { using (var stream = File.OpenRead(path)) return Hash(stream); }
    static void Fingerprint(Stream stream, IDictionary<string, object> expected, string label)
    {
        Check(stream != null, label + " exists");
        Check(stream.Length == Number(expected, "bytes"), label + " byte count matches the extracted bundle");
        Check(Hash(stream).Equals(Text(expected, "sha256"), StringComparison.OrdinalIgnoreCase), label + " SHA-256 matches the extracted bundle");
    }
    static void SourceEvidence(IDictionary<string, object> manifest, string sourceDirectory)
    {
        var provenance = Map(manifest["provenance"]); var evidence = Map(provenance["versionEvidence"]);
        Check(Text(evidence, "value") == ExpectedVersion && Number(evidence, "byteOffset") >= 0, "Manifest records the expected game-version marker and byte offset");
        var sources = ArrayValue(provenance, "sources").Select(Map).ToArray();
        Check(sources.Any(source => Text(source, "fileName") == "resources.assets") && sources.Any(source => Text(source, "fileName") == "global-metadata.dat"), "Provenance includes game resources and the version-bearing metadata");
        foreach (var source in sources)
        {
            string name = Text(source, "fileName"), relative = Text(source, "relativePath", name);
            Check(name == Path.GetFileName(name) && !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(Text(source, "sha256"), "^[0-9a-fA-F]{64}$") && Number(source, "bytes") > 0, "Source fingerprint is complete without a private absolute path: " + name);
            Inside(bundleRoot, relative);
            if (!string.IsNullOrWhiteSpace(sourceDirectory)) using (var stream = File.OpenRead(Inside(sourceDirectory, relative))) Fingerprint(stream, source, "Original source " + name);
        }
        var versionSource = sources.Single(source => Text(source, "fileName") == Text(evidence, "fileName"));
        byte[] marker = Encoding.UTF8.GetBytes(ExpectedVersion); long offset = Number(evidence, "byteOffset");
        Check(offset + marker.Length <= Number(versionSource, "bytes"), "Version-marker offset falls within the recorded source");
        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            string relative = Text(versionSource, "relativePath", Text(versionSource, "fileName")); byte[] actual = new byte[marker.Length];
            using (var stream = File.OpenRead(Inside(sourceDirectory, relative))) { stream.Position = offset; Check(stream.Read(actual, 0, actual.Length) == actual.Length, "Version marker is readable at its recorded offset"); }
            Check(actual.SequenceEqual(marker), "Original game metadata contains 7.2.0.1.0 at the recorded offset");
        }
        else report.Add("LIMIT Original source files were not supplied; their provenance is recorded, while packaged asset fingerprints are independently verified below.");
    }
    static void Files(IDictionary<string, object> manifest)
    {
        var entries = ArrayValue(manifest, "files").Select(Map).ToArray();
        var paths = new HashSet<string>(entries.Select(entry => Text(entry, "relativePath").Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
        string[] required = { "ExaltHelper_Data/Objects.xml", "ExaltHelper_Data/GroundTypes.xml", "ExaltHelper_Data/version.txt", "ExaltHelper_Data/version.dll", "ObjectID.list", "ID3.list", "TileID.list", "assets/xml/mods.xml", "assets/xml/forgeProperties.xml", "assets/xml/enchantments.xml", "assets/flatbuffer/spritesheetf", "assets/sprites/mapObjects.png", "assets/sprites/characters.png" };
        Check(paths.Count == entries.Length && required.All(paths.Contains), "Manifest covers every runtime game-data input without duplicate paths");
        foreach (var entry in entries)
        {
            string relative = Text(entry, "relativePath"), path = Inside(bundleRoot, relative), resource = Text(entry, "embeddedResource");
            Check(Regex.IsMatch(Text(entry, "sha256"), "^[0-9a-fA-F]{64}$") && Number(entry, "bytes") > 0, "Manifest has a valid nonempty fingerprint: " + relative);
            if (resource.Length > 0) using (var stream = app.GetManifestResourceStream(resource)) Fingerprint(stream, entry, "Embedded " + resource);
            if (resource.Length == 0 || File.Exists(path)) using (var stream = File.OpenRead(path)) Fingerprint(stream, entry, relative);
            if (relative == "ObjectID.list" || relative == "ID3.list")
            {
                Check(resource == relative, relative + " is verified from the executable resource");
                using (var runtime = (Stream)Call(T("Proxy.Helpers.RealmSharkData"), "FindDataStream", relative)) Fingerprint(runtime, entry, "Runtime projectile source " + relative);
                foreach (string directory in new[] { "assets", "Sniffer/assets" })
                {
                    string shadow = Inside(bundleRoot, directory + "/" + relative);
                    if (File.Exists(shadow)) using (var stream = File.OpenRead(shadow)) Fingerprint(stream, entry, "Override " + directory + "/" + relative);
                }
            }
        }
        Check(!HashFile(Inside(bundleRoot, "ExaltHelper_Data/Objects.xml")).Equals(PreviousObjectsSha256, StringComparison.OrdinalIgnoreCase), "Object definitions differ from the old bundle; a version-label-only update is rejected");
        Check(HashFile(Inside(bundleRoot, "ExaltHelper_Data/version.dll")).Equals(NativeHookSha256, StringComparison.OrdinalIgnoreCase), "Game asset replacement preserves the application native hook");
    }
    static int Id(string text)
    {
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : int.Parse(text, CultureInfo.InvariantCulture);
    }
    static string ObjectName(XElement definition)
    {
        string display = (string)definition.Element("DisplayId");
        if (string.IsNullOrWhiteSpace(display)) display = (string)definition.Attribute("displayId");
        return !string.IsNullOrWhiteSpace(display) && !display.StartsWith("{s.", StringComparison.Ordinal) ? display : (string)definition.Attribute("id");
    }
    static Dictionary<int, XElement> ReadDefinitions(string relative, string elementName, HashSet<int> itemIds, Dictionary<ushort, XElement> runtimeDefinitions = null)
    {
        var definitions = new Dictionary<int, XElement>();
        using (var reader = XmlReader.Create(Inside(bundleRoot, relative), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 200000000 }))
            while (reader.Read()) if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1 && reader.LocalName == elementName)
                using (var subtree = reader.ReadSubtree())
                {
                    var element = XElement.Load(subtree); int id = Id((string)element.Attribute("type")); definitions[id] = element;
                    if (runtimeDefinitions != null) runtimeDefinitions[unchecked((ushort)id)] = element;
                    if (itemIds != null && element.Element("Item") != null) itemIds.Add(id);
                }
        return definitions;
    }
    static void DefinitionCounts(IDictionary<string, object> manifest, out Dictionary<int, XElement> objects)
    {
        var itemIds = new HashSet<int>(); var runtimeDefinitions = new Dictionary<ushort, XElement>();
        objects = ReadDefinitions("ExaltHelper_Data/Objects.xml", "Object", itemIds, runtimeDefinitions);
        var grounds = ReadDefinitions("ExaltHelper_Data/GroundTypes.xml", "Ground", null); var counts = Map(manifest["counts"]);
        Check(objects.Count == Number(counts, "objects") && objects.Count > 10000, "Unique source object count matches the 7.2 manifest");
        Check(itemIds.Count == Number(counts, "items") && itemIds.Count > 1000, "Unique source equipment/item count matches the 7.2 manifest");
        Check(grounds.Count == Number(counts, "grounds") && grounds.Count > 100, "Unique source ground count matches the 7.2 manifest");
        Check(itemIds.All(id => id >= 0 && id <= ushort.MaxValue), "All source equipment/item IDs fit the protocol uint16 range");
        Check(grounds.Keys.All(id => id >= 0 && id <= ushort.MaxValue), "All source ground IDs fit the protocol uint16 range");
        if (counts.ContainsKey("runtimeObjects")) Check(runtimeDefinitions.Count == Number(counts, "runtimeObjects"), "Runtime object count accounts for documented source aliases");
        var outliers = new HashSet<int>(objects.Keys.Where(id => id < 0 || id > ushort.MaxValue));
        Check(outliers.SetEquals(new[] { 699470, 796730, 811318 }), "Only the three independently confirmed inherited object IDs exceed the uint16 range");
        var exceptions = ArrayValue(manifest, "sourceIdExceptions").Select(Map).ToArray();
        Check(exceptions.Length == outliers.Count && outliers.SetEquals(exceptions.Select(value => (int)Number(value, "id"))), "Manifest identifies every inherited source ID exception exactly once");
        foreach (var exception in exceptions)
        {
            int id = (int)Number(exception, "id"); ushort runtimeId = unchecked((ushort)id); XElement winner = runtimeDefinitions[runtimeId];
            Check(Number(exception, "runtimeId") == runtimeId && Text(exception, "objectName") == (string)objects[id].Attribute("id") && Text(exception, "runtimeName") == ObjectName(winner), "Source ID exception preserves the original object and winning runtime name: " + id + " -> " + runtimeId);
        }
        Call(T("Proxy.DataStructures.GameData"), "LoadGameData");
        foreach (var expected in new[] { Tuple.Create("Objects", runtimeDefinitions.Count), Tuple.Create("Items", itemIds.Count), Tuple.Create("Tiles", grounds.Count) })
            Check(((IDictionary)Get(Get(T("Proxy.DataStructures.GameData"), expected.Item1), "Map")).Count == expected.Item2, "Runtime " + expected.Item1 + " parses every source definition");
        var runtimeObjects = (IDictionary)Get(Get(T("Proxy.DataStructures.GameData"), "Objects"), "Map");
        Check(runtimeDefinitions.All(pair => runtimeObjects.Contains(pair.Key) && (string)Get(runtimeObjects[pair.Key], "Name") == ObjectName(pair.Value)), "Runtime object IDs and display names agree with source order, including documented aliases");
        var runtimeGrounds = (IDictionary)Get(Get(T("Proxy.DataStructures.GameData"), "Tiles"), "Map");
        Check(grounds.All(pair => runtimeGrounds.Contains((ushort)pair.Key) && (string)Get(runtimeGrounds[(ushort)pair.Key], "Name") == (string)pair.Value.Attribute("id")), "Runtime ground IDs and names agree with every current XML definition");
    }
    static void Close(double actual, double expected, string description) { Check(Math.Abs(actual - expected) <= .0001, description + " [" + actual.ToString(CultureInfo.InvariantCulture) + "]"); }
    static void Sprite(object metadata, IDictionary<string, object> expected, string output)
    {
        int id = (int)Get(metadata, "ItemId"); var bitmap = (Bitmap)Call(T("ItemSpriteManager"), "GetItemSprite", id);
        Check(bitmap != null && bitmap.Width > 0 && bitmap.Height > 0, "Sentinel " + id + " resolves an actual sprite");
        int opaque = 0; for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A > 0) opaque++;
        Check(opaque > 5, "Sentinel " + id + " sprite has visible artwork"); bitmap.Save(Path.Combine(output, "sprite-" + id + ".png"), ImageFormat.Png);
        if (expected.ContainsKey("spriteRect"))
        {
            var rect = Map(expected["spriteRect"]); var wanted = new Rectangle((int)Number(rect, "x"), (int)Number(rect, "y"), (int)Number(rect, "width"), (int)Number(rect, "height"));
            Check((Rectangle)Get(metadata, "SpriteRect") == wanted && bitmap.Size == wanted.Size, "Sentinel " + id + " uses the independently extracted sprite rectangle");
            string atlasPath = Text(rect, "atlasRelativePath").Replace('\\', '/');
            Check(atlasPath == "assets/sprites/mapObjects.png" || atlasPath == "assets/sprites/characters.png", "Sentinel " + id + " identifies its source atlas explicitly");
            var atlas = (Bitmap)Get(T("ItemSpriteManager"), atlasPath.EndsWith("mapObjects.png", StringComparison.Ordinal) ? "mapObjectsAtlas" : "charactersAtlas");
            Check(atlas != null && wanted.Width > 0 && wanted.Height > 0 && new Rectangle(Point.Empty, atlas.Size).Contains(wanted), "Sentinel " + id + " rectangle fits its source atlas");
            bool same = true; for (int y = 0; same && y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).ToArgb() != atlas.GetPixel(wanted.X + x, wanted.Y + y).ToArgb()) { same = false; break; }
            Check(same, "Sentinel " + id + " rendered pixels come from the correct source atlas");
        }
    }
    static void Sentinels(IDictionary<string, object> manifest, Dictionary<int, XElement> objects, string output)
    {
        var sentinels = ArrayValue(manifest, "sentinels").Select(Map).ToArray();
        Check(sentinels.Length >= 2 && sentinels.Any(value => Text(value, "kind") == "added" || Text(value, "kind") == "changed"), "Manifest includes independently identified new or changed game objects");
        Call(T("ItemSpriteManager"), "InitializeAssets"); int spriteCount = 0;
        foreach (var sentinel in sentinels)
        {
            int id = (int)Number(sentinel, "id"); var expected = Map(sentinel["after"]); XElement definition;
            Check(objects.TryGetValue(id, out definition), "7.2 sentinel object is present: " + id);
            Check((string)definition.Attribute("id") == Text(expected, "objectName"), "7.2 sentinel XML name matches: " + id + " " + Text(expected, "objectName"));
            if (expected.ContainsKey("dungeonDifficulty"))
                Check((string)definition.Element("DungeonDifficulty") == Text(expected, "dungeonDifficulty"), "Updated key has the source dungeon difficulty: " + id);
            if (expected.ContainsKey("tooltipEffects"))
            {
                var effects = definition.Element("ExtraTooltipData").Elements("EffectInfo").ToArray();
                var wanted = ArrayValue(expected, "tooltipEffects").Select(Map).ToArray();
                Check(effects.Length == wanted.Length && wanted.All(effect => effects.Any(actual => (string)actual.Attribute("name") == Text(effect, "name") && (string)actual.Attribute("description") == Text(effect, "description"))), "Updated key preserves the current source warning: " + id);
            }
            if (Text(sentinel, "kind") == "changed")
            {
                var before = Map(sentinel["before"]);
                Check(before.Keys.Any(key => expected.ContainsKey(key) && json.Serialize(before[key]) != json.Serialize(expected[key])), "Changed sentinel records a concrete before/after difference: " + id);
            }
            var meta = Call(T("ItemSpriteManager"), "GetItemMetadata", id);
            Check((int)Get(meta, "ItemId") == id && !((string)Get(meta, "Name")).StartsWith("Item #", StringComparison.Ordinal), "7.2 sentinel metadata is embedded and named: " + id);
            if (expected.ContainsKey("displayName")) Check((string)Get(meta, "Name") == Text(expected, "displayName"), "7.2 sentinel display name matches: " + id);
            if (expected.ContainsKey("slotType")) Check((int)Get(meta, "SlotType") == Number(expected, "slotType") && Id((string)definition.Element("SlotType")) == Number(expected, "slotType"), "7.2 sentinel slot agrees across XML and embedded metadata: " + id);
            if (expected.ContainsKey("labels")) Check(ArrayValue(expected, "labels").Cast<string>().All(label => ((string[])Get(meta, "Labels")).Contains(label)), "7.2 sentinel labels survive the embedded catalog: " + id);
            if (expected.ContainsKey("projectiles"))
            {
                var bullets = ((IEnumerable)Get(meta, "Projectiles")).Cast<object>().ToArray(); var expectedBullets = ArrayValue(expected, "projectiles").Select(Map).ToArray();
                Check(bullets.Length == expectedBullets.Length, "7.2 sentinel projectile count matches: " + id);
                foreach (var bullet in expectedBullets)
                {
                    int bulletId = (int)Number(bullet, "id"); object actual = bullets.Single(value => (int)Get(value, "Id") == bulletId);
                    Check((int)Get(actual, "MinDamage") == Number(bullet, "minDamage") && (int)Get(actual, "MaxDamage") == Number(bullet, "maxDamage"), "7.2 sentinel projectile damage matches: " + id + "/" + bulletId);
                    if (bullet.ContainsKey("rateOfFire")) Close(Convert.ToDouble(Get(actual, "RateOfFire")), Convert.ToDouble(bullet["rateOfFire"], CultureInfo.InvariantCulture), "7.2 sentinel projectile rate matches: " + id + "/" + bulletId);
                }
            }
            if (Convert.ToBoolean(Value(expected, "sprite", false), CultureInfo.InvariantCulture)) { Sprite(meta, expected, output); spriteCount++; }
        }
        Check(spriteCount >= 2, "At least two version sentinels verify actual sprite artwork");
        var slottedItems = objects.Where(pair => pair.Value.Element("Item") != null && pair.Value.Element("SlotType") != null).ToArray();
        var slotMismatches = slottedItems.Select(pair => new { Id = pair.Key, Expected = Id((string)pair.Value.Element("SlotType")), Actual = (int)Get(Call(T("ItemSpriteManager"), "GetItemMetadata", pair.Key), "SlotType") }).Where(value => value.Expected != value.Actual).ToArray();
        Check(slottedItems.Length > 1000 && slotMismatches.Length == 0, "Every source item's explicit slot type survives runtime metadata (" + slottedItems.Length + " checked; mismatches: " + string.Join(", ", slotMismatches.Take(8).Select(value => value.Id + " expected " + value.Expected + " actual " + value.Actual)) + ")");
        var map = (Bitmap)Get(T("ItemSpriteManager"), "mapObjectsAtlas"); var characters = (Bitmap)Get(T("ItemSpriteManager"), "charactersAtlas");
        Check(map != null && characters != null, "Both packaged sprite atlases decode");
        var metadata = ((IDictionary)Get(T("ItemSpriteManager"), "itemDb")).Values.Cast<object>().Where(value => (bool)Get(value, "HasSpriteRect") && ((string[])Get(value, "Labels")).Contains("EQUIPMENT")).ToArray();
        Check(metadata.Length > 500, "The packaged sprite map resolves the equipment catalog");
        Check(metadata.All(value => { var rectangle = (Rectangle)Get(value, "SpriteRect"); return rectangle.Width > 0 && rectangle.Height > 0 && (new Rectangle(Point.Empty, map.Size).Contains(rectangle) || new Rectangle(Point.Empty, characters.Size).Contains(rectangle)); }), "Every mapped equipment rectangle fits a packaged atlas");
    }
    static void Versions(string output)
    {
        Check(File.ReadAllText(Inside(bundleRoot, "ExaltHelper_Data/version.txt")).Trim() == ExpectedVersion, "Packaged launcher version is 7.2.0.1.0");
        Check((string)Get(T("Program"), "GameVersion") == ExpectedVersion, "Runtime launcher reads the packaged game version");
        string previous = Environment.CurrentDirectory, empty = Path.Combine(output, "missing-version-fixture"); Directory.CreateDirectory(empty);
        var cached = T("Program").GetField("_gameVersion", F);
        try { cached.SetValue(null, null); Environment.CurrentDirectory = empty; Check((string)Get(T("Program"), "GameVersion") == ExpectedVersion, "Compiled fallback agrees when the version file is unavailable"); }
        finally { Environment.CurrentDirectory = previous; cached.SetValue(null, null); }
    }
    public void Run(string path, string manifestPath, string sourceDirectory, string output)
    {
        app = Assembly.LoadFrom(path); bundleRoot = Path.GetDirectoryName(path); Environment.CurrentDirectory = bundleRoot;
        try
        {
            var manifest = Map(json.DeserializeObject(File.ReadAllText(manifestPath)));
            Check(Number(manifest, "schemaVersion") == 1 && Text(manifest, "gameVersion") == ExpectedVersion, "Bundle manifest identifies the supported 7.2.0.1.0 schema");
            SourceEvidence(manifest, sourceDirectory); Files(manifest); Versions(output);
            Dictionary<int, XElement> objects; DefinitionCounts(manifest, out objects); Sentinels(manifest, objects, output);
            Console.WriteLine("PASS: " + checks + " game-asset bundle assertions.");
        }
        finally { File.WriteAllLines(Path.Combine(output, "review.txt"), report); }
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($harness))
$setup = New-Object AppDomainSetup
$setup.ApplicationBase = Split-Path $assembly -Parent
$domain = [AppDomain]::CreateDomain('Game asset bundle regression', $null, $setup)
$completed = $false
try {
    $runner = $domain.CreateInstanceFromAndUnwrap($harness, 'GameAssetBundleRegression')
    $runner.Run($assembly, $manifest, $SourceDirectory, $testRoot)
    $completed = $true
} finally {
    [AppDomain]::Unload($domain)
    $env:EXALTHELPER_HUD_DATA = $priorData
    @{ Assembly=$assembly; SHA256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash; AssetManifest=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash; Passed=$completed; OriginalSourcesVerified=($completed -and ![string]::IsNullOrWhiteSpace($SourceDirectory)); CreatedUtc=[DateTime]::UtcNow.ToString('o'); Results=$testRoot } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'test-manifest.json') -Encoding UTF8
    Write-Output "Game asset results: $testRoot"
}
