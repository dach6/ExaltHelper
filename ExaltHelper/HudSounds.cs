using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal sealed class HudSoundRule
{
    public bool Enabled { get; set; }
    public int Volume { get; set; } = 100;
    public string Tone { get; set; } = "Chime";
    public string File { get; set; } = "";
}

internal sealed class HudSoundSettings
{
    public int MasterVolume { get; set; } = 50;
    public string CustomChatWords { get; set; } = "";
    public Dictionary<string, HudSoundRule> Rules { get; set; } = HudSounds.Categories.Keys.ToDictionary(k => k, _ => new HudSoundRule());
    public void Normalize()
    {
        MasterVolume = Math.Max(0, Math.Min(100, MasterVolume)); CustomChatWords ??= "";
        if (CustomChatWords.Length > 1000) CustomChatWords = CustomChatWords.Substring(0, 1000);
        Rules = (Rules ?? new Dictionary<string, HudSoundRule>()).Where(p => HudSounds.Categories.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value ?? new HudSoundRule());
        foreach (string key in HudSounds.Categories.Keys) if (!Rules.ContainsKey(key)) Rules[key] = new HudSoundRule();
        foreach (var rule in Rules.Values)
        {
            rule.Volume = Math.Max(0, Math.Min(100, rule.Volume)); rule.File ??= "";
            if (!HudSounds.Tones.Contains(rule.Tone)) rule.Tone = "Chime";
        }
    }
}

internal static class HudSounds
{
    public static readonly string[] Tones = { "Chime", "Ping", "Soft", "Alert" };
    public static readonly IReadOnlyDictionary<string, string> Categories = CreateCategories();
    private static readonly object Gate = new object();
    private sealed class Request { public string Category, Tone, File; public double Gain; public DateTime CreatedUtc; }
    private static readonly Queue<Request> queue = new Queue<Request>();
    private static readonly Dictionary<string, DateTime> lastPlayed = new Dictionary<string, DateTime>();
    private static readonly HashSet<string> lootIds = new HashSet<string>();
    private static readonly Queue<string> lootOrder = new Queue<string>();
    private static Task worker;
    private static bool stopping, nativePlaying;
    private static readonly ManualResetEvent shutdownSignal = new ManualResetEvent(false);
    private static string lastError = "";
    private static Action<byte[]> playback = PlayWave;
    public static string LastError => Volatile.Read(ref lastError);
    private static IReadOnlyDictionary<string, string> CreateCategories()
    {
        var values = new Dictionary<string, string> {
            ["Whisper"] = "Chat / direct messages", ["Guild"] = "Chat / guild", ["Party"] = "Chat / party and watchlist", ["Trade"] = "Chat / trade requests",
            ["NameMention"] = "Chat / your name mentioned", ["CustomChat"] = "Chat / custom phrases", ["Event"] = "Realm / all events",
            ["RealmOpened"] = "Realm / newly opened", ["RealmClosing"] = "Realm / closing", ["OryxWarning"] = "Realm / Oryx warning",
            ["Dust"] = "Account / dust storage full", ["Reminder"] = "Account / reward reminders", ["Dungeon"] = "Dungeon / entered",
            ["Dimitus"] = "Dungeon / Dimitus", ["DangerousModifiers"] = "Dungeon / dangerous modifiers", ["Shiny"] = "Loot / shiny item",
            ["Enchantment"] = "Loot / any enchantment", ["UniqueEnchantment"] = "Loot / unique enchantment", ["AwakenedEnchantment"] = "Loot / awakened enchantment",
            ["LegendaryEnchantment"] = "Loot / Legendary or Divine enchantment"
        };
        foreach (string tier in new[] { "Rookie", "Adept", "Expert", "Exaltation", "Unknown" }) values["KeyPop:" + tier] = "Key pop / " + tier;
        foreach (string bag in HudLootPolicy.BagNames) values["Loot:" + bag] = "Loot / " + bag;
        foreach (string name in HudCalloutNames.Events.Keys) values["Event:" + name] = "Realm event / " + name;
        foreach (string name in new[] { "Alien invasion wave", "Alien UFO", "Alien Calbrik" }) values["Event:" + name] = "Realm event / " + name;
        return values;
    }
    public static void Play(string category) => Enqueue(category, false);
    public static void Preview(string category) => Enqueue(category, true);
    private static void Enqueue(string category, bool preview)
    {
        var prefs = HudPreferences.Current; var settings = prefs.Sounds;
        if (category == null || !settings.Rules.TryGetValue(category, out var rule)) return;
        bool enabled = category == "Whisper" ? prefs.SoundAlerts : rule.Enabled;
        if ((!preview && !enabled) || settings.MasterVolume <= 0 || rule.Volume <= 0) return;
        var now = DateTime.UtcNow;
        lock (Gate)
        {
            if (stopping || queue.Count >= 8 || lastPlayed.TryGetValue(category, out var last) && now - last < TimeSpan.FromMilliseconds(1500)) return;
            lastPlayed[category] = now;
            queue.Enqueue(new Request { Category = category, Tone = rule.Tone, File = rule.File,
                Gain = Math.Max(0, Math.Min(1, settings.MasterVolume / 100d * rule.Volume / 100d)), CreatedUtc = now });
            if (worker == null) worker = Task.Run(Drain);
        }
    }
    private static void Drain()
    {
        while (true)
        {
            Request request;
            lock (Gate)
            {
                if (stopping || queue.Count == 0) { worker = null; return; }
                request = queue.Dequeue();
            }
            if (DateTime.UtcNow - request.CreatedUtc > TimeSpan.FromSeconds(10)) continue;
            try
            {
                byte[] wave;
                if (string.IsNullOrWhiteSpace(request.File)) wave = ToneWave(request.Tone, request.Gain);
                else
                {
                    using var file = new FileStream(request.File, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (file.Length > 2 * 1024 * 1024) throw new InvalidDataException("Custom WAV files must be no larger than 2 MB.");
                    var bytes = new byte[(int)file.Length];
                    for (int offset = 0; offset < bytes.Length;)
                    {
                        int read = file.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) throw new InvalidDataException("The WAV file ended before its audio data.");
                        offset += read;
                    }
                    wave = ScaleWave(bytes, request.Gain);
                }
                lock (Gate) if (stopping) continue;
                playback(wave); Volatile.Write(ref lastError, "");
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException
                || ex is NotSupportedException || ex is System.Security.SecurityException || ex is DllNotFoundException || ex is EntryPointNotFoundException)
            { Volatile.Write(ref lastError, "Sound unavailable for " + request.Category + ": " + (ex is InvalidDataException ? ex.Message : "choose a readable PCM WAV file or a built-in tone.")); }
        }
    }
    public static void PlayEvent(string eventName)
    {
        string key = "Event:" + eventName;
        if ((eventName ?? "").StartsWith("Alien Invasion ", StringComparison.OrdinalIgnoreCase)) key = "Event:Alien invasion wave";
        else if ((eventName ?? "").IndexOf("Calbrik", StringComparison.OrdinalIgnoreCase) >= 0) key = "Event:Alien Calbrik";
        else if ((eventName ?? "").IndexOf("UFO", StringComparison.OrdinalIgnoreCase) >= 0) key = "Event:Alien UFO";
        var settings = HudPreferences.Current.Sounds;
        if (settings.Rules.TryGetValue(key, out var rule) && rule.Enabled) Play(key); else Play("Event");
    }
    public static void PlayChat(string sender, string recipient, string text, string localName, string category)
    {
        if (category == "Whisper" || category == "Guild" || category == "Party") Play(category);
        if ((text ?? "").IndexOf("trade", StringComparison.OrdinalIgnoreCase) >= 0 && (sender ?? "").StartsWith("#", StringComparison.Ordinal)) Play("Trade");
        if (HudCallouts.PlayerName(sender).Equals(HudCallouts.PlayerName(localName), StringComparison.OrdinalIgnoreCase)) return;
        if (!string.IsNullOrWhiteSpace(localName) && Regex.IsMatch(text ?? "", @"(?<![\p{L}\p{N}_])" + Regex.Escape(localName) + @"(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase)) Play("NameMention");
        if ((HudPreferences.Current.Sounds.CustomChatWords ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Take(50).Any(s => (text ?? "").IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) Play("CustomChat");
    }
    public static void PlayLoot(LootBagDrop bag)
    {
        if (bag == null || bag.ItemIds == null || bag.ItemIds.Count == 0) return;
        if (!string.IsNullOrEmpty(bag.DropId)) lock (Gate)
        {
            if (!lootIds.Add(bag.DropId)) return; lootOrder.Enqueue(bag.DropId);
            while (lootOrder.Count > 1024) lootIds.Remove(lootOrder.Dequeue());
        }
        Play("Loot:" + bag.BagType);
        if (bag.ItemIsShiny?.Any(x => x) == true) Play("Shiny");
        if (bag.ItemEnchantCounts?.Any(x => x > 0) == true) Play("Enchantment");
        if (bag.ItemEnchantCounts?.Any(x => x >= 3) == true) Play("LegendaryEnchantment");
        var categories = (bag.ItemEnchants ?? new List<List<string>>()).Where(x => x != null).SelectMany(x => x).Select(EnchantmentParser.EnchantmentCategory).Distinct().ToArray();
        if (categories.Contains("Unique")) Play("UniqueEnchantment");
        if (categories.Contains("Awakened")) Play("AwakenedEnchantment");
    }
    public static void PlayKeyPop(string dungeon, string opener)
    {
        string tier = HudCombatPolicy.DungeonTier(dungeon);
        Play("KeyPop:" + tier);
    }
    public static void PlayDungeon(string modifiers)
    {
        Play("Dungeon");
        var tokens = HudCallouts.ParseModifierTokens(modifiers);
        if (tokens.Any(x => HudCallouts.Canonical(x) == "DIMITUS")) Play("Dimitus");
        if (tokens.Any(IsDangerousModifier)) Play("DangerousModifiers");
    }
    // RealmHound's whole-callout red-outline rules (MIT, a4c6f74). Token
    // aliases also work before the asynchronous modifier catalog has loaded.
    private static readonly HashSet<string> DangerousBases = new HashSet<string> {
        "EXPOSED", "ARMORBROKEN", "SPONGY", "SICKPET", "SILENTPET", "GLASSBOSS", "GLASSMINIONS",
        "FEROCIOUSBOSS", "FEROCIOUSMINIONS", "ELITEBOSS", "ELITEMINIONS", "LETHAL", "HAUNTEDHALLS", "CRABRAVE", "OUTOFTIME"
    };
    internal static bool IsDangerousModifier(string token)
    {
        string key = Regex.Replace(HudCallouts.Canonical(token), "[1-4]$", "");
        if (key == "FASTWORMHOLE" || DangerousBases.Contains(key)) return true;
        string name = HudCalloutModifiers.Current.Get(token)?.Name;
        return name != null && DangerousBases.Contains(HudCallouts.Canonical(Regex.Replace(name.Trim(), @"\s+(IV|III|II|I)$", "")));
    }
    internal static byte[] ToneWave(string tone, double gain)
    {
        const int rate = 22050; int samples = (int)(rate * (tone == "Alert" ? .35 : .22));
        var bytes = new byte[44 + samples * 2];
        using (var stream = new MemoryStream(bytes)) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes.Length - 8); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            double frequency = tone == "Alert" ? 330 : tone == "Soft" ? 440 : tone == "Ping" ? 880 : 660;
            for (int i = 0; i < samples; i++)
            {
                double envelope = Math.Min(1, i / (rate * .01)) * Math.Min(1, (samples - 1 - i) / (rate * .04));
                double note = Math.Sin(2 * Math.PI * frequency * i / rate);
                if (tone == "Chime") note = .65 * note + .35 * Math.Sin(2 * Math.PI * 990 * i / rate);
                writer.Write((short)Math.Round(note * envelope * 9000 * Math.Max(0, Math.Min(1, gain))));
            }
        }
        return bytes;
    }
    internal static byte[] ScaleWave(byte[] input, double gain)
    {
        if (input == null || input.Length < 44 || input.Length > 2 * 1024 * 1024 || Encoding.ASCII.GetString(input, 0, 4) != "RIFF" || Encoding.ASCII.GetString(input, 8, 4) != "WAVE")
            throw new InvalidDataException("Choose an uncompressed PCM WAV file (8 or 16 bit, at most 5 seconds and 2 MB).");
        if (BitConverter.ToUInt32(input, 4) != input.Length - 8) throw new InvalidDataException("The WAV file length does not match its RIFF header.");
        int format = 0, channels = 0, rate = 0, bits = 0, align = 0, byteRate = 0, start = -1, length = 0, chunkPosition = 12;
        bool hasFormat = false;
        for (; chunkPosition <= input.Length - 8;)
        {
            uint size = BitConverter.ToUInt32(input, chunkPosition + 4); if (size > input.Length - chunkPosition - 8) throw new InvalidDataException("The WAV file contains an incomplete chunk.");
            string name = Encoding.ASCII.GetString(input, chunkPosition, 4); int body = chunkPosition + 8;
            if (name == "fmt ")
            {
                if (hasFormat || size < 16) throw new InvalidDataException("The WAV file must have one complete PCM format chunk.");
                hasFormat = true; format = BitConverter.ToUInt16(input, body); channels = BitConverter.ToUInt16(input, body + 2); rate = BitConverter.ToInt32(input, body + 4); byteRate = BitConverter.ToInt32(input, body + 8); align = BitConverter.ToUInt16(input, body + 12); bits = BitConverter.ToUInt16(input, body + 14);
            }
            if (name == "data")
            {
                if (start >= 0) throw new InvalidDataException("The WAV file must have one audio data chunk.");
                start = body; length = (int)size;
            }
            chunkPosition = checked(body + (int)size + ((int)size & 1));
        }
        if (chunkPosition != input.Length || format != 1 || channels < 1 || channels > 2 || rate < 8000 || rate > 96000 || (bits != 8 && bits != 16)
            || align != channels * bits / 8 || byteRate != rate * align || start < 0 || length == 0 || length % align != 0 || length / (double)(rate * align) > 5)
            throw new InvalidDataException("Custom sounds require 8/16-bit PCM mono or stereo WAV audio, 8–96 kHz, no longer than 5 seconds.");
        var output = (byte[])input.Clone(); gain = Math.Max(0, Math.Min(1, gain));
        for (int pos = start; pos < start + length; pos += bits / 8)
        {
            if (bits == 8) output[pos] = (byte)Math.Max(0, Math.Min(255, 128 + Math.Round((input[pos] - 128) * gain)));
            else { short value = (short)Math.Round(BitConverter.ToInt16(input, pos) * gain); output[pos] = (byte)value; output[pos + 1] = (byte)(value >> 8); }
        }
        return output;
    }
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NativePlay(IntPtr wave, IntPtr module, uint flags);
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NativeStop(IntPtr wave, IntPtr module, uint flags);
    private static void PlayWave(byte[] wave)
    {
        int byteRate = 1, length = 0;
        for (int pos = 12; pos <= wave.Length - 8;)
        {
            int size = BitConverter.ToInt32(wave, pos + 4); string name = Encoding.ASCII.GetString(wave, pos, 4);
            if (name == "fmt ") byteRate = BitConverter.ToInt32(wave, pos + 16);
            if (name == "data") length = size;
            pos += 8 + size + (size & 1);
        }
        var pinned = GCHandle.Alloc(wave, GCHandleType.Pinned);
        try
        {
            lock (Gate)
            {
                if (stopping) return;
                if (!NativePlay(pinned.AddrOfPinnedObject(), IntPtr.Zero, 0x0001 | 0x0004 | 0x0002)) throw new InvalidOperationException("Audio playback failed.");
                nativePlaying = true;
            }
            shutdownSignal.WaitOne((int)Math.Ceiling(1000d * length / byteRate) + 100);
        }
        finally
        {
            lock (Gate) { if (nativePlaying) { NativeStop(IntPtr.Zero, IntPtr.Zero, 0); nativePlaying = false; } }
            pinned.Free();
        }
    }
    public static void Shutdown()
    {
        Task pending;
        lock (Gate)
        {
            stopping = true; queue.Clear(); pending = worker;
            if (nativePlaying) { NativeStop(IntPtr.Zero, IntPtr.Zero, 0); nativePlaying = false; }
            shutdownSignal.Set();
        }
        pending?.Wait(1000);
    }
}
