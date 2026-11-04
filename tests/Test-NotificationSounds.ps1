param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/notification-sounds"
)
$ErrorActionPreference = 'Stop'
if ([IO.DriveInfo]::new('C:\').AvailableFreeSpace -le 5GB -or (Test-Path 'C:/Users/Administrator/Desktop/code/.exalthelper-tools/disk-guard/STOP')) { throw 'STOP: disk guard' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'data'
Add-Type -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class NotificationSoundRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app; static object prefs, settings; static int checks;
    static readonly List<byte[]> played = new List<byte[]>();
    static readonly ManualResetEvent entered = new ManualResetEvent(false), release = new ManualResetEvent(false);
    static bool block; static int callbacks, callbackThread;
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object New(string name) { return Activator.CreateInstance(T(name), true); }
    static object Get(object o, string name) { Type t = o as Type ?? o.GetType(); var field = t.GetField(name, F); return field != null ? field.GetValue(o is Type ? null : o) : t.GetProperty(name, F).GetValue(o is Type ? null : o, null); }
    static void Set(object o, string name, object value) { Type t = o as Type ?? o.GetType(); var field = t.GetField(name, F); if (field != null) field.SetValue(o is Type ? null : o, value); else t.GetProperty(name, F).SetValue(o is Type ? null : o, value, null); }
    static object Call(object o, string name, params object[] args) { Type t = o as Type ?? o.GetType(); return t.GetMethod(name, F).Invoke(o is Type ? null : o, args); }
    static object Sound(string name, params object[] args) { return Call(T("HudSounds"), name, args); }
    static void Check(bool passed, string message) { if (!passed) throw new Exception(message); checks++; }
    static IDictionary Rules { get { return (IDictionary)Get(settings, "Rules"); } }
    static IDictionary Last { get { return (IDictionary)Get(T("HudSounds"), "lastPlayed"); } }
    static object Rule(string category) { return Rules[category]; }
    static void Enable(params string[] categories) { foreach (string c in categories) { Set(Rule(c), "Enabled", true); if (c == "Whisper") Set(prefs, "SoundAlerts", true); } }
    static bool WasQueued(string category) { return Last.Contains(category); }
    static int Count { get { lock (played) return played.Count; } }
    static void Capture(byte[] wave)
    {
        callbackThread = Thread.CurrentThread.ManagedThreadId;
        if (Interlocked.Increment(ref callbacks) == 1 && block) { entered.Set(); if (!release.WaitOne(5000)) throw new InvalidOperationException("Fixture playback was not released."); }
        lock (played) played.Add((byte[])wave.Clone());
    }
    static void Drain()
    {
        var timeout = Stopwatch.StartNew();
        while (Get(T("HudSounds"), "worker") != null) { if (timeout.ElapsedMilliseconds > 5000) throw new Exception("Audio worker did not finish."); Thread.Sleep(5); }
    }
    static void Reset()
    {
        release.Set(); Drain(); block = false; callbacks = 0; entered.Reset(); release.Reset();
        lock (played) played.Clear(); Last.Clear(); Call(Get(T("HudSounds"), "lootIds"), "Clear"); Call(Get(T("HudSounds"), "lootOrder"), "Clear");
        settings = New("HudSoundSettings"); Set(prefs, "Sounds", settings); Set(prefs, "SoundAlerts", false); Set(T("HudSounds"), "lastError", "");
    }
    static byte[] Tone(string name, double gain) { return (byte[])Sound("ToneWave", name, gain); }
    static byte[] Scale(byte[] wave, double gain) { return (byte[])Sound("ScaleWave", wave, gain); }
    static bool Rejected(byte[] bytes)
    {
        try { Scale(bytes, .5); return false; } catch (TargetInvocationException ex) { if (!(ex.InnerException is InvalidDataException)) throw; return true; }
    }
    static byte[] With(byte[] source, int position, byte[] bytes) { var changed = (byte[])source.Clone(); Buffer.BlockCopy(bytes, 0, changed, position, bytes.Length); return changed; }
    static byte[] AppendChunk(byte[] source, string name, byte[] payload)
    {
        var output = new byte[source.Length + 8 + payload.Length + (payload.Length & 1)]; Buffer.BlockCopy(source, 0, output, 0, source.Length);
        Buffer.BlockCopy(Encoding.ASCII.GetBytes(name), 0, output, source.Length, 4); Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, output, source.Length + 4, 4); Buffer.BlockCopy(payload, 0, output, source.Length + 8, payload.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(output.Length - 8), 0, output, 4, 4); return output;
    }
    static byte[] Wave8()
    {
        var bytes = new byte[48]; using (var writer = new BinaryWriter(new MemoryStream(bytes))) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(40); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(8000); writer.Write((short)1); writer.Write((short)8);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(4); writer.Write(new byte[] { 0, 128, 255, 64 });
        } return bytes;
    }
    static void ModelAndPcm()
    {
        Check(Rules.Count > 50 && Rules.Values.Cast<object>().All(x => !(bool)Get(x, "Enabled")), "New sound categories must all be opt-in.");
        foreach (string key in new[] { "Whisper", "Guild", "Party", "Trade", "NameMention", "CustomChat", "Dust", "Reminder", "Dungeon", "Dimitus", "DangerousModifiers", "Shiny", "KeyPop:Unknown", "Loot:White Bag", "Event:Alien Calbrik" }) Check(Rules.Contains(key), "Missing category: " + key);
        Set(settings, "MasterVolume", 130); Set(settings, "CustomChatWords", null); Set(Rule("Guild"), "Volume", -12); Set(Rule("Guild"), "Tone", "unknown"); Rules["Party"] = null; Rules["obsolete"] = New("HudSoundRule"); Call(settings, "Normalize");
        Check((int)Get(settings, "MasterVolume") == 100 && (int)Get(Rule("Guild"), "Volume") == 0 && (string)Get(Rule("Guild"), "Tone") == "Chime", "Malformed volumes or tones were not normalized.");
        Check(!Rules.Contains("obsolete") && Rule("Party") != null && (string)Get(settings, "CustomChatWords") == "", "Malformed category settings were not repaired.");
        Set(settings, "Rules", null); Call(settings, "Normalize"); Check(Rules.Count > 50, "Missing rule map did not restore safe defaults.");
        byte[] original = Tone("Ping", 1), unity = Scale(original, 1), half = Scale(original, .5), mute = Scale(original, 0);
        Check(!ReferenceEquals(original, unity) && original.SequenceEqual(unity), "Scaling modified source data or changed unity audio.");
        Check(Enumerable.Range(0, (original.Length - 44) / 2).All(i => BitConverter.ToInt16(half, 44 + i * 2) == (short)Math.Round(BitConverter.ToInt16(original, 44 + i * 2) * .5)), "16-bit gain was not applied to every signed sample.");
        Check(mute.Skip(44).All(x => x == 0) && original.Skip(44).Any(x => x != 0), "Zero volume is not silent or the original samples changed.");
        Check(Scale(Wave8(), .5).Skip(44).SequenceEqual(new byte[] { 64, 128, 192, 96 }), "8-bit volume lost the unsigned PCM midpoint.");
        var stereo = With(With(With(Wave8(), 22, BitConverter.GetBytes((short)2)), 28, BitConverter.GetBytes(16000)), 32, BitConverter.GetBytes((short)2));
        Check(Scale(stereo, .5).Skip(44).SequenceEqual(new byte[] { 64, 128, 192, 96 }), "Stereo channels were not scaled equally.");
        foreach (string tone in new[] { "Chime", "Ping", "Soft", "Alert" }) Check(Scale(Tone(tone, .5), 1).Length > 44, "Generated tone is not valid PCM: " + tone);
        Check(!Tone("Soft", .5).SequenceEqual(Tone("Ping", .5)), "Built-in tones are not distinct.");
        Check(Rejected(null) && Rejected(new byte[44]) && Rejected(original.Take(original.Length - 1).ToArray()), "Invalid or truncated files were accepted.");
        Check(Rejected(With(original, 4, BitConverter.GetBytes(0))) && Rejected(With(original, 40, BitConverter.GetBytes(uint.MaxValue))), "RIFF or chunk bounds were ignored.");
        Check(Rejected(With(original, 20, BitConverter.GetBytes((short)3))) && Rejected(With(original, 34, BitConverter.GetBytes((short)24))), "Unsupported codecs or sample sizes were accepted.");
        Check(Rejected(With(original, 28, BitConverter.GetBytes(1))) && Rejected(With(original, 32, BitConverter.GetBytes((short)4))), "Inconsistent PCM rates or alignment were accepted.");
        Check(Rejected(AppendChunk(Wave8(), "data", new byte[8000 * 6])), "A second audio chunk bypassed duration limits and volume scaling.");
        Check(Rejected(AppendChunk(original, "fmt ", original.Skip(20).Take(16).ToArray())) && Rejected(AppendChunk(original, "fmt ", new byte[2])), "Duplicate or incomplete format chunks were accepted.");
        Check(Scale(AppendChunk(Wave8(), "JUNK", new byte[] { 1, 2, 3 }), .5).Skip(44).Take(4).SequenceEqual(new byte[] { 64, 128, 192, 96 }), "Valid padded metadata chunks damaged PCM scaling.");
        var longWave = new byte[44 + 6 * 8000]; Buffer.BlockCopy(Wave8(), 0, longWave, 0, 44); Buffer.BlockCopy(BitConverter.GetBytes(longWave.Length - 8), 0, longWave, 4, 4); Buffer.BlockCopy(BitConverter.GetBytes(longWave.Length - 44), 0, longWave, 40, 4);
        Check(Rejected(longWave) && Rejected(new byte[2 * 1024 * 1024 + 1]), "Duration or file-size limit was ignored.");
    }
    static void Routing()
    {
        Reset(); Sound("Play", "Guild"); Sound("Play", "missing"); Check(Count == 0 && Last.Count == 0, "Disabled or unknown categories played.");
        Set(Rule("Whisper"), "Enabled", true); Sound("Play", "Whisper"); Check(!WasQueued("Whisper"), "Legacy direct-message toggle was bypassed.");
        Set(prefs, "SoundAlerts", true); Set(Rule("Whisper"), "Enabled", false); Sound("Play", "Whisper"); Drain(); Check(Count == 1 && callbackThread != Thread.CurrentThread.ManagedThreadId, "Direct messages did not honor the legacy setting or played on the caller thread.");
        Sound("Play", "Whisper"); Drain(); Check(Count == 1, "Repeated alerts bypassed category rate limiting.");
        Last["Whisper"] = DateTime.UtcNow.AddSeconds(-2); Sound("Play", "Whisper"); Drain(); Check(Count == 2, "An expired rate limit prevented a new alert.");
        Reset(); Sound("Preview", "Guild"); Drain(); Check(Count == 1 && !(bool)Get(Rule("Guild"), "Enabled"), "Preview failed for a disabled category or enabled it.");
        Reset(); Enable("Guild"); Set(settings, "MasterVolume", 0); Sound("Preview", "Guild"); Check(Last.Count == 0, "Muted master volume allowed a preview."); Set(settings, "MasterVolume", 100); Set(Rule("Guild"), "Volume", 0); Sound("Play", "Guild"); Check(Last.Count == 0, "Muted category volume played.");
        Reset(); Enable("NameMention"); Sound("PlayChat", "Other", "", "Heroic greetings", "Hero", "Game"); Check(!WasQueued("NameMention"), "Name mentions matched part of another name."); Sound("PlayChat", "Other", "", "hello HERO!", "Hero", "Game"); Drain(); Check(WasQueued("NameMention"), "Whole-word case-insensitive name mention was missed.");
        Reset(); Enable("NameMention", "CustomChat"); Set(settings, "CustomChatWords", "ready now, dungeon open"); Sound("PlayChat", "Hero", "", "Hero ready now", "Hero", "Game"); Check(Last.Count == 0, "Local messages triggered personal mention/phrase alerts."); Sound("PlayChat", "Other", "", "DUNGEON OPEN", "Hero", "Game"); Drain(); Check(WasQueued("CustomChat") && !WasQueued("NameMention"), "Custom phrase routing failed.");
        Reset(); Enable("Trade"); Sound("PlayChat", "Other", "", "trade", "Hero", "Game"); Check(!WasQueued("Trade"), "Ordinary chat triggered a trade request sound."); Sound("PlayChat", "#System", "", "Trade request", "Hero", "Game"); Drain(); Check(WasQueued("Trade"), "System trade request was missed.");
        foreach (string channel in new[] { "Whisper", "Guild", "Party" }) { Reset(); Enable(channel); Sound("PlayChat", "Other", "Hero", "hello", "Hero", channel); Drain(); Check(WasQueued(channel), "Channel sound was missed: " + channel); }
        Reset(); Enable("Event", "Event:Alien Calbrik"); Sound("PlayEvent", "Alien Calbrik"); Drain(); Check(WasQueued("Event:Alien Calbrik") && !WasQueued("Event"), "Specific event sound did not override the general event sound.");
        Reset(); Enable("Event"); Sound("PlayEvent", "Unknown observed event"); Drain(); Check(WasQueued("Event"), "General event fallback was missed.");
        Reset(); Enable("Event:Alien invasion wave"); Sound("PlayEvent", "Alien Invasion Wave 2"); Drain(); Check(WasQueued("Event:Alien invasion wave"), "Alien waves did not use their dedicated category.");
        Reset(); Enable("KeyPop:Exaltation"); ((IList)Get(prefs, "HiddenKeyPopTiers")).Add("Exaltation"); Sound("PlayKeyPop", "Lost Halls", "Other"); Drain(); Check(WasQueued("KeyPop:Exaltation"), "Hiding feed key openings also silenced their independent sound setting."); ((IList)Get(prefs, "HiddenKeyPopTiers")).Clear();
        Reset(); Enable("KeyPop:Unknown"); Sound("PlayKeyPop", "Unknown Dungeon", "Other"); Drain(); Check(WasQueued("KeyPop:Unknown"), "Unknown key difficulty lost its explicit sound category.");
        ((Task)Call(T("HudCalloutModifiers"), "LoadDefaultAsync")).Wait(); Check((int)Get(Get(T("HudCalloutModifiers"), "Current"), "Count") > 100, "Bundled modifier assets were not available to the sound fixture.");
        foreach (string token in new[] { "EXPOSED_4", "SICKPET", "SILENTPET", "SPONGY", "GLASSBOSS_3", "FAST_WORMHOLE" }) Check((bool)Sound("IsDangerousModifier", token), "Dangerous asset modifier was missed: " + token);
        foreach (string token in new[] { "WEAKBOSS_3", "ARMORED_2", "LOOTING", "ANEMIA_2", "EXPOSED_5", "NOT_REAL" }) Check(!(bool)Sound("IsDangerousModifier", token), "Benign or unknown modifier caused a danger alert: " + token);
        Reset(); Enable("DangerousModifiers"); Sound("PlayDungeon", "WEAKBOSS_3;LOOTING|S"); Check(!WasQueued("DangerousModifiers"), "Beneficial dungeon modifiers triggered danger audio."); Sound("PlayDungeon", "EXPOSED_4|S"); Drain(); Check(WasQueued("DangerousModifiers"), "Real negative asset modifier did not trigger danger audio.");
        Reset(); Enable("Dimitus", "Dungeon"); Sound("PlayDungeon", "DIMITUS|S"); Drain(); Check(WasQueued("Dimitus") && WasQueued("Dungeon"), "Dimitus or dungeon-entry category was missed.");
        Reset(); Enable("Loot:White Bag", "Shiny", "Enchantment", "LegendaryEnchantment", "UniqueEnchantment", "AwakenedEnchantment");
        object bag = Activator.CreateInstance(app.GetType("ExaltHelper.Proxy.DataStructures.LootBagDrop", true), true);
        Set(bag, "BagType", "White Bag"); ((IList)Get(bag, "ItemIds")).Add(1); ((IList)Get(bag, "ItemIsShiny")).Add(true); ((IList)Get(bag, "ItemEnchantCounts")).Add(3); ((IList)Get(bag, "ItemEnchants")).Add(new List<string> { "Unique power", "Awakened power" });
        Sound("PlayLoot", bag); Drain(); Check(Last.Count == 6, "Bag, shiny, or enchantment categories were not independently routed."); int recorded = Count; Last.Clear(); Sound("PlayLoot", bag); Drain(); Check(Count == recorded, "Duplicate bag snapshots replayed their sounds.");
        Set(bag, "DropId", Guid.NewGuid().ToString("N")); Set(bag, "ItemEnchants", null); Set(bag, "ItemEnchantCounts", null); Set(bag, "ItemIsShiny", null); Sound("PlayLoot", bag); Drain(); Check(Count == recorded + 1, "Partial bag metadata prevented its known bag sound.");
    }
    static void QueueAndFiles(string root)
    {
        Reset(); Enable("Guild", "Party"); block = true; Sound("Play", "Guild"); Check(entered.WaitOne(2000), "Playback worker did not start.");
        string wav = Path.Combine(root, "sample.wav"); File.WriteAllBytes(wav, Tone("Ping", 1)); Set(Rule("Party"), "File", wav); Set(Rule("Party"), "Volume", 25);
        var time = Stopwatch.StartNew(); Sound("Play", "Party"); time.Stop(); Set(Rule("Party"), "File", "missing-after-enqueue.wav"); Set(settings, "MasterVolume", 100); Set(Rule("Party"), "Volume", 100);
        Check(time.ElapsedMilliseconds < 200, "A blocked sound stalled the alert caller."); release.Set(); Drain(); Check(Count == 2 && played[1].SequenceEqual(Scale(File.ReadAllBytes(wav), .125)), "Queued audio used later mutable settings or incorrect combined volume.");
        Reset(); Enable("Guild"); Set(Rule("Guild"), "File", Path.Combine(root, "missing.wav")); Sound("Play", "Guild"); Drain(); Check(Count == 0 && ((string)Get(T("HudSounds"), "LastError")).Contains("Sound unavailable"), "Missing custom file failed silently or reached playback.");
        Last.Clear(); Set(Rule("Guild"), "File", ""); Sound("Play", "Guild"); Drain(); Check(Count == 1 && (string)Get(T("HudSounds"), "LastError") == "", "Audio worker did not recover after a bad custom file.");
        Reset(); Enable("Guild"); string corrupt = Path.Combine(root, "corrupt.wav"); File.WriteAllText(corrupt, "not wave data"); Set(Rule("Guild"), "File", corrupt); Sound("Play", "Guild"); Drain(); Check(Count == 0 && ((string)Get(T("HudSounds"), "LastError")).Contains("PCM WAV"), "Malformed custom audio was not rejected with actionable feedback.");
        Reset(); block = true; string[] keys = Rules.Keys.Cast<string>().Take(12).ToArray(); Enable(keys); Sound("Play", keys[0]); Check(entered.WaitOne(2000), "Queue-limit worker did not start."); foreach (string key in keys.Skip(1)) Sound("Play", key);
        var queue = (IEnumerable)Get(T("HudSounds"), "queue"); Check(queue.Cast<object>().Count() == 8 && Last.Count == 9, "Sound queue exceeded its bounded pending capacity.");
        Set(queue.Cast<object>().First(), "CreatedUtc", DateTime.UtcNow.AddSeconds(-20)); release.Set(); Drain(); Check(Count == 8, "Stale queued audio played after its useful alert window.");
    }
    static C Control<C>(Form form, string name) where C : Control { var control = form.Controls.Find(name, true).SingleOrDefault() as C; if (control == null) throw new Exception("Missing sound control: " + name); return control; }
    static void Click(Button button) { typeof(Button).GetMethod("OnClick", F).Invoke(button, new object[] { EventArgs.Empty }); }
    static void UserInterface(string root)
    {
        Reset(); Set(prefs, "Page", "settings"); Set(prefs, "CalendarWarnings", false);
        using (var form = (Form)New("ExaltDashboardForm")) {
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-2500, -2500); form.Show(); Application.DoEvents();
            var category = Control<ComboBox>(form, "sound_category"); var enabled = Control<CheckBox>(form, "sound_enabled"); var direct = Control<CheckBox>(form, "sound_direct_messages");
            Check(category.Items.Count == Rules.Count && !enabled.Checked && !direct.Checked, "Sound category editor did not expose safe default settings.");
            direct.Checked = true; Check(enabled.Checked && (bool)Get(prefs, "SoundAlerts"), "Legacy direct-message checkbox did not synchronize with the category editor.");
            enabled.Checked = false; Check(!direct.Checked && !(bool)Get(prefs, "SoundAlerts"), "Category editor did not synchronize the legacy direct-message checkbox.");
            category.SelectedItem = "Chat / guild"; enabled.Checked = true; Control<NumericUpDown>(form, "sound_master_volume").Value = 37; Control<NumericUpDown>(form, "sound_volume").Value = 62; Control<ComboBox>(form, "sound_tone").SelectedItem = "Soft";
            Check((bool)Get(Rule("Guild"), "Enabled") && (int)Get(settings, "MasterVolume") == 37 && (int)Get(Rule("Guild"), "Volume") == 62 && (string)Get(Rule("Guild"), "Tone") == "Soft", "Sound controls did not update their selected category.");
            var phrases = Control<TextBox>(form, "sound_chat_phrases"); phrases.Text = "ready now, dungeon open"; typeof(Control).GetMethod("OnLeave", F).Invoke(phrases, new object[] { EventArgs.Empty });
            string wav = Path.Combine(root, "sample.wav"); Set(Rule("Guild"), "File", wav); category.SelectedIndex = 0; category.SelectedItem = "Chat / guild";
            Check(Control<TextBox>(form, "sound_file").ReadOnly && Control<TextBox>(form, "sound_file").Text == wav && !Control<ComboBox>(form, "sound_tone").Enabled, "Custom file selection was not reflected by the editor.");
            Click(Control<Button>(form, "sound_reset")); Check((string)Get(Rule("Guild"), "File") == "" && Control<ComboBox>(form, "sound_tone").Enabled, "Built-in reset did not clear the custom file.");
            Click(Control<Button>(form, "sound_preview")); Drain(); Check(Count == 1, "Sound preview control did not route through the worker.");
            object restored = Call(T("HudPreferences"), "Load"); object restoredSounds = Get(restored, "Sounds"); object restoredGuild = ((IDictionary)Get(restoredSounds, "Rules"))["Guild"];
            Check((int)Get(restoredSounds, "MasterVolume") == 37 && (int)Get(restoredGuild, "Volume") == 62 && (string)Get(restoredGuild, "Tone") == "Soft" && (bool)Get(restoredGuild, "Enabled") && (string)Get(restoredSounds, "CustomChatWords") == phrases.Text, "Sound settings did not survive a real save and reload.");
            var parent = category.Parent as ScrollableControl;
            if (parent != null) { var master = Control<NumericUpDown>(form, "sound_master_volume"); parent.AutoScrollPosition = new Point(0, master.Top - parent.AutoScrollPosition.Y - 30); }
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(root, "sound-settings.png"), ImageFormat.Png); }
        }
        DateTime now = DateTime.UtcNow; Set(T("HudJournal"), "RealmCastleUtc", now.AddSeconds(65)); Check((string)Call(T("ExaltDashboardForm"), "RealmWidgetValue", now) == "Castle 01:05", "Realm widget did not show the remaining castle countdown.");
        Check((string)Call(T("ExaltDashboardForm"), "RealmWidgetValue", now.AddSeconds(66)) == "Closed", "Elapsed castle countdown did not become Closed."); Set(T("HudJournal"), "RealmCastleUtc", null);
    }
    static void Shutdown()
    {
        Reset(); block = true; Enable("Guild", "Party"); Sound("Play", "Guild"); Check(entered.WaitOne(2000), "Shutdown fixture worker did not start."); Sound("Play", "Party");
        Task shutdown = Task.Run(delegate { Sound("Shutdown"); }); var timeout = Stopwatch.StartNew(); while (!(bool)Get(T("HudSounds"), "stopping") && timeout.ElapsedMilliseconds < 1500) Thread.Sleep(5);
        Sound("Preview", "Dust"); release.Set(); Check(shutdown.Wait(2000), "Sound shutdown did not finish promptly."); Drain(); Check(Count == 1 && !WasQueued("Dust") && !((IEnumerable)Get(T("HudSounds"), "queue")).Cast<object>().Any(), "Shutdown played pending alerts or accepted new previews.");
    }
    public static void Run(string path, string root)
    {
        app = Assembly.LoadFrom(Path.GetFullPath(path)); prefs = Get(T("HudPreferences"), "Current"); settings = Get(prefs, "Sounds");
        Set(T("HudSounds"), "playback", new Action<byte[]>(Capture));
        try { ModelAndPcm(); Routing(); QueueAndFiles(root); UserInterface(root); Shutdown(); Console.WriteLine("PASS " + checks + " notification sound checks (silent playback substitute). Artifacts: " + root); }
        catch (Exception ex) { Console.Error.WriteLine("After " + checks + " checks: " + ex); throw; }
        finally { release.Set(); try { Sound("Shutdown"); } catch (Exception ex) { Console.Error.WriteLine("Shutdown: " + ex); } }
    }
}
'@
[NotificationSoundRegression]::Run((Resolve-Path $AssemblyPath).Path, $testRoot)
