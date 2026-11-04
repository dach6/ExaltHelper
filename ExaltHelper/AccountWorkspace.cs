using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Networking;
using ExaltHelper.Proxy.Networking.Packets;
using ExaltHelper.Proxy.Networking.Packets.DataObjects;

namespace ExaltHelper;

internal sealed class AccountCharacter
{
    public int Id { get; set; }
    public int ClassType { get; set; }
    public int Level { get; set; }
    public int Fame { get; set; }
    public bool Seasonal { get; set; }
    public bool Crucible { get; set; }
    public int Skin { get; set; }
    public string Name { get; set; } = "";
    public string Created { get; set; } = "";
    public string Label { get; set; } = "";
    public int Order { get; set; } = int.MaxValue;
    public bool IsDead { get; set; }
    public string KilledBy { get; set; } = "";
    public int DeathFame { get; set; }
    public int GraveType { get; set; }
    public DateTime? DeathUtc { get; set; }
    public List<AccountDeathBonus> DeathBonuses { get; set; } = new List<AccountDeathBonus>();
    public int[] ObservedStats { get; set; } = Array.Empty<int>();
    public DateTime? ObservedUtc { get; set; }
    public int? LootBoostSeconds { get; set; }
    public int? XpBoostSeconds { get; set; }
    public int? LootTierBoostSeconds { get; set; }
    public DateTime? BoostObservedUtc { get; set; }
    public DateTime? LootBoostObservedUtc { get; set; }
    public DateTime? XpBoostObservedUtc { get; set; }
    public DateTime? LootTierBoostObservedUtc { get; set; }
    public int Effects { get; set; }
    public int Effects2 { get; set; }
    public int? ExaltDamage { get; set; }
    public string BloodRitualId { get; set; } = "";
    // HP, MP, ATT, DEF, SPD, DEX, VIT, WIS; API values are base stats.
    public int[] Stats { get; set; } = new int[8];
    public int[] Inventory { get; set; } = Array.Empty<int>();
    public string[] Enchants { get; set; } = Array.Empty<string>();
    public int[] QuickSlots { get; set; } = new[] { -1, -1, -1 };
    public int[] QuickSlotCounts { get; set; } = new int[3];
    public string[] QuickSlotEnchants { get; set; } = new string[3];
    public Dictionary<string, int> PcStats { get; set; } = new Dictionary<string, int>();
    public bool HasPcStats { get; set; }
    public int PetId { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public AccountCharacter Clone() => new AccountCharacter { Id = Id, ClassType = ClassType, Level = Level,
        Fame = Fame, Seasonal = Seasonal, Crucible = Crucible, Skin = Skin, Name = Name, Created = Created,
        Label = Label, Order = Order, IsDead = IsDead, KilledBy = KilledBy, DeathFame = DeathFame, GraveType = GraveType, DeathUtc = DeathUtc,
        DeathBonuses = DeathBonuses.Select(b => b.Clone()).ToList(), ObservedStats = (int[])ObservedStats.Clone(), ObservedUtc = ObservedUtc,
        LootBoostSeconds = LootBoostSeconds, XpBoostSeconds = XpBoostSeconds, LootTierBoostSeconds = LootTierBoostSeconds, BoostObservedUtc = BoostObservedUtc, Effects = Effects, Effects2 = Effects2,
        LootBoostObservedUtc = LootBoostObservedUtc, XpBoostObservedUtc = XpBoostObservedUtc, LootTierBoostObservedUtc = LootTierBoostObservedUtc,
        ExaltDamage = ExaltDamage, BloodRitualId = BloodRitualId,
        Stats = (int[])Stats.Clone(), Inventory = (int[])Inventory.Clone(), Enchants = (string[])Enchants.Clone(),
        QuickSlots = (int[])QuickSlots.Clone(), QuickSlotCounts = (int[])QuickSlotCounts.Clone(), QuickSlotEnchants = (string[])QuickSlotEnchants.Clone(), PcStats = new Dictionary<string, int>(PcStats),
        HasPcStats = HasPcStats, PetId = PetId, UpdatedUtc = UpdatedUtc };
}

internal sealed class AccountStorage
{
    public string Name { get; set; } = "Vault";
    public bool Seasonal { get; set; }
    public int ObjectId { get; set; } = -1;
    public int[] Items { get; set; } = Array.Empty<int>();
    public string[] Enchants { get; set; } = Array.Empty<string>();
    public DateTime UpdatedUtc { get; set; }
    public AccountStorage Clone() => new AccountStorage { Name = Name, Seasonal = Seasonal, ObjectId = ObjectId,
        Items = (int[])Items.Clone(), Enchants = (string[])Enchants.Clone(), UpdatedUtc = UpdatedUtc };
}

internal sealed class AccountPet
{
    public int Id { get; set; }
    public string Name { get; set; } = "Pet";
    public int Skin { get; set; }
    public int Type { get; set; }
    public int Rarity { get; set; }
    public bool Seasonal { get; set; }
    public int[] AbilityTypes { get; set; } = Array.Empty<int>();
    public int[] AbilityLevels { get; set; } = Array.Empty<int>();
    public int[] AbilityPowers { get; set; } = Array.Empty<int>();
    public int MaximumLevel { get; set; }
    public bool IdentityFromApi { get; set; }
    public int[] Inventory { get; set; } = Array.Empty<int>();
    public int[] StackCounts { get; set; } = Array.Empty<int>();
    public string[] Enchants { get; set; } = Array.Empty<string>();
    public DateTime UpdatedUtc { get; set; }
    public AccountPet Clone() => new AccountPet { Id = Id, Name = Name, Skin = Skin, Type = Type, Rarity = Rarity,
        Seasonal = Seasonal, AbilityTypes = (int[])AbilityTypes.Clone(), AbilityLevels = (int[])AbilityLevels.Clone(),
        AbilityPowers = (int[])AbilityPowers.Clone(), MaximumLevel = MaximumLevel, IdentityFromApi = IdentityFromApi,
        Inventory = (int[])Inventory.Clone(), StackCounts = (int[])StackCounts.Clone(), Enchants = (string[])Enchants.Clone(), UpdatedUtc = UpdatedUtc };
}

internal sealed class AccountSnapshot
{
    public string AccountKey { get; set; } = "";
    public string Name { get; set; } = "Waiting for account";
    public string Status { get; set; } = "Connect a character, then refresh account data.";
    public int CurrentCharacterId { get; set; } = -1;
    public List<AccountCharacter> Characters { get; set; } = new List<AccountCharacter>();
    public List<AccountCharacter> DeadCharacters { get; set; } = new List<AccountCharacter>();
    public string[] TreasurySectionOrder { get; set; } = Array.Empty<string>();
    public string[] TreasuryCategoryOrder { get; set; } = Array.Empty<string>();
    public string[] CollapsedTreasurySections { get; set; } = Array.Empty<string>();
    public AccountChallenge Crucible { get; set; }
    public List<AccountChallenge> BloodRituals { get; set; } = new List<AccountChallenge>();
    public List<AccountAccelerator> Accelerators { get; set; } = new List<AccountAccelerator>();
    public List<AccountStorage> Storage { get; set; } = new List<AccountStorage>();
    public List<AccountPet> Pets { get; set; } = new List<AccountPet>();
    public Dictionary<string, int[]> Exaltations { get; set; } = new Dictionary<string, int[]>();
    public int? Gold { get; set; }
    public int? Fame { get; set; }
    public int? Stars { get; set; }
    public int? ForgeFire { get; set; }
    public int? RegularForgeFire { get; set; }
    public int? SeasonalForgeFire { get; set; }
    public int? RegularPetYard { get; set; }
    public int? SeasonalPetYard { get; set; }
    public int?[] RegularMaterials { get; set; } = new int?[4];
    public int?[] SeasonalMaterials { get; set; } = new int?[4];
    public int?[] RegularMaterialCaps { get; set; } = new int?[4];
    public int?[] SeasonalMaterialCaps { get; set; } = new int?[4];
    public string SeasonName { get; set; } = "";
    public string BattlePassName { get; set; } = "";
    public DateTime? SeasonEndUtc { get; set; }
    public DateTime? BattlePassEndUtc { get; set; }
    public long PlaytimeMinutes => Characters.Concat(DeadCharacters).Sum(c => c.PcStats.TryGetValue("minutes_active", out int minutes) ? (long)Math.Max(0, minutes) : 0);
    public int? RegularDust { get; set; }
    public int? SeasonalDust { get; set; }
    public int[] RegularDustAmounts { get; set; } = new int[3];
    public int[] SeasonalDustAmounts { get; set; } = new int[3];
    public int?[] RegularDustCaps { get; set; } = new int?[3];
    public int?[] SeasonalDustCaps { get; set; } = new int?[3];
    public int CharacterSlots { get; set; }
    public int[] OwnedSkins { get; set; } = Array.Empty<int>();
    public int[] OwnedEmotes { get; set; } = Array.Empty<int>();
    public int[] OwnedPetSkins { get; set; } = Array.Empty<int>();
    public int[] RegularBlueprints { get; set; } = Array.Empty<int>();
    public int[] SeasonalBlueprints { get; set; } = Array.Empty<int>();
    public DateTime UpdatedUtc { get; set; }
    public long Revision { get; set; }
    public AccountSnapshot Clone() => new AccountSnapshot { AccountKey = AccountKey, Name = Name, Status = Status,
        CurrentCharacterId = CurrentCharacterId, Characters = Characters.Select(c => c.Clone()).ToList(),
        DeadCharacters = DeadCharacters.Select(c => c.Clone()).ToList(), TreasurySectionOrder = (string[])TreasurySectionOrder.Clone(),
        TreasuryCategoryOrder = (string[])TreasuryCategoryOrder.Clone(), CollapsedTreasurySections = (string[])CollapsedTreasurySections.Clone(),
        Crucible = Crucible?.Clone(), BloodRituals = BloodRituals.Select(r => r.Clone()).ToList(), Accelerators = Accelerators.Select(a => a.Clone()).ToList(),
        Storage = Storage.Select(s => s.Clone()).ToList(), Pets = Pets.Select(p => p.Clone()).ToList(),
        Exaltations = Exaltations.ToDictionary(p => p.Key, p => (int[])p.Value.Clone()), Gold = Gold, Fame = Fame,
        Stars = Stars, ForgeFire = ForgeFire, RegularDust = RegularDust, SeasonalDust = SeasonalDust,
        RegularForgeFire = RegularForgeFire, SeasonalForgeFire = SeasonalForgeFire,
        RegularPetYard = RegularPetYard, SeasonalPetYard = SeasonalPetYard,
        RegularMaterials = (int?[])RegularMaterials.Clone(), SeasonalMaterials = (int?[])SeasonalMaterials.Clone(),
        RegularMaterialCaps = (int?[])RegularMaterialCaps.Clone(), SeasonalMaterialCaps = (int?[])SeasonalMaterialCaps.Clone(),
        SeasonName = SeasonName, BattlePassName = BattlePassName, SeasonEndUtc = SeasonEndUtc, BattlePassEndUtc = BattlePassEndUtc,
        RegularDustAmounts = (int[])RegularDustAmounts.Clone(), SeasonalDustAmounts = (int[])SeasonalDustAmounts.Clone(),
        RegularDustCaps = (int?[])RegularDustCaps.Clone(), SeasonalDustCaps = (int?[])SeasonalDustCaps.Clone(),
        CharacterSlots = CharacterSlots, OwnedSkins = (int[])OwnedSkins.Clone(), OwnedEmotes = (int[])OwnedEmotes.Clone(),
        OwnedPetSkins = (int[])OwnedPetSkins.Clone(), RegularBlueprints = (int[])RegularBlueprints.Clone(),
        SeasonalBlueprints = (int[])SeasonalBlueprints.Clone(), UpdatedUtc = UpdatedUtc, Revision = Revision };
}

/// <summary>Small immutable dashboard projection, excluding character gear and storage contents.</summary>
internal sealed class AccountHudSummary
{
    public string AccountKey { get; }
    public string Name { get; }
    public string Status { get; }
    public long Revision { get; }
    public DateTime UpdatedUtc { get; }
    public bool CurrentCharacterSeasonal { get; }
    public int? Gold { get; }
    public int? Fame { get; }
    public int? Stars { get; }
    public int? RegularForgeFire { get; }
    public int? SeasonalForgeFire { get; }
    public int? RegularDust { get; }
    public int? SeasonalDust { get; }
    public IReadOnlyList<int> RegularDustAmounts { get; }
    public IReadOnlyList<int> SeasonalDustAmounts { get; }
    public IReadOnlyList<int?> RegularMaterials { get; }
    public IReadOnlyList<int?> SeasonalMaterials { get; }
    public string SeasonName { get; }
    public string BattlePassName { get; }
    public DateTime? SeasonEndUtc { get; }
    public DateTime? BattlePassEndUtc { get; }
    public bool HasPcStats { get; }
    public long PlaytimeMinutes { get; }
    public int CharacterCount { get; }
    public int CharacterSlots { get; }
    public int SkinCount { get; }

    internal AccountHudSummary(AccountSnapshot account)
    {
        AccountKey = account.AccountKey; Name = account.Name; Status = account.Status;
        Revision = account.Revision; UpdatedUtc = account.UpdatedUtc;
        CurrentCharacterSeasonal = account.Characters.FirstOrDefault(c => c.Id == account.CurrentCharacterId)?.Seasonal == true;
        Gold = account.Gold; Fame = account.Fame; Stars = account.Stars;
        RegularForgeFire = account.RegularForgeFire; SeasonalForgeFire = account.SeasonalForgeFire;
        RegularDust = account.RegularDust; SeasonalDust = account.SeasonalDust;
        RegularDustAmounts = Array.AsReadOnly((int[])account.RegularDustAmounts.Clone());
        SeasonalDustAmounts = Array.AsReadOnly((int[])account.SeasonalDustAmounts.Clone());
        RegularMaterials = Array.AsReadOnly((int?[])account.RegularMaterials.Clone());
        SeasonalMaterials = Array.AsReadOnly((int?[])account.SeasonalMaterials.Clone());
        SeasonName = account.SeasonName; BattlePassName = account.BattlePassName;
        SeasonEndUtc = account.SeasonEndUtc; BattlePassEndUtc = account.BattlePassEndUtc;
        HasPcStats = account.Characters.Any(c => c.HasPcStats) || account.DeadCharacters.Any(c => c.HasPcStats);
        PlaytimeMinutes = account.PlaytimeMinutes;
        CharacterCount = account.Characters.Count; CharacterSlots = account.CharacterSlots; SkinCount = account.OwnedSkins.Length;
    }
}

/// <summary>Read-only account data. Tokens exist only in memory; disk stores a vetted data model.</summary>
internal static partial class AccountWorkspace
{
    private static readonly object Gate = new object();
    private static readonly object SaveGate = new object();
    private static readonly Dictionary<string, AccountSnapshot> pendingSaves = new Dictionary<string, AccountSnapshot>();
    private static readonly Dictionary<string, long> writtenRevisions = new Dictionary<string, long>();
    private static string writtenAccountKey = "";
    private static readonly SemaphoreSlim ApiGate = new SemaphoreSlim(1, 1);
    private static readonly SemaphoreSlim RefreshGate = new SemaphoreSlim(1, 1);
    private static readonly string[] ApiEndpoints = { "char/list", "missions/getClientSeasons", "missions/getPlayerMissions",
        "account/getOwnedPetSkins", "season/seasonInfo", "season/bpInfo" };
    private static AccountSnapshot state = new AccountSnapshot();
    private static string accessToken = "";
    private static int generation;
    private static Client activeClient;
    private static int activeObjectId = -1;
    private static bool currentSeasonal;
    private static bool dirty;
    private static bool vaultBatch;
    private static bool inPetYard;
    private static readonly Dictionary<int, int> petObjects = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> storagePages = new Dictionary<int, int>();
    private static readonly List<AccountStorage> pendingVault = new List<AccountStorage>();
    internal static string DataDirectory = Path.Combine(Environment.GetEnvironmentVariable("EXALTHELPER_HUD_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExaltHelper", "HUD"), "accounts");
    private static readonly System.Threading.Timer saveTimer = new System.Threading.Timer(_ => SavePending(), null, 5000, 5000);

    static AccountWorkspace()
    {
        try
        {
            string keyPath = Path.Combine(DataDirectory, "last-account.txt");
            if (File.Exists(keyPath))
            {
                string key = File.ReadAllText(keyPath).Trim();
                if (key.Length == 64 && key.All(c => "0123456789abcdef".IndexOf(c) >= 0))
                { state = Load(key); state.Status = "Cached account data. Connect a character and refresh to synchronize."; }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
    }

    public static bool IsAuthenticated { get { lock (Gate) return !string.IsNullOrEmpty(accessToken); } }
    public static string AccountKey { get { lock (Gate) return state.AccountKey; } }
    public static long Revision { get { lock (Gate) return state.Revision; } }
    public static AccountCharacter CurrentCharacter { get { lock (Gate) return state.Characters.FirstOrDefault(c => c.Id == state.CurrentCharacterId)?.Clone(); } }
    public static AccountSnapshot Snapshot() { lock (Gate) return state.Clone(); }
    public static AccountSnapshot SnapshotIfChanged(long revision, string accountKey)
    {
        lock (Gate) return state.Revision == revision && state.AccountKey == accountKey ? null : state.Clone();
    }
    private static AccountHudSummary hudSummary;
    private static AccountSnapshot hudSummarySource;
    public static AccountHudSummary ReadHudSummary()
    {
        lock (Gate)
        {
            if (hudSummary == null || !ReferenceEquals(hudSummarySource, state) || hudSummary.Revision != state.Revision || hudSummary.AccountKey != state.AccountKey)
            {
                hudSummary = new AccountHudSummary(state);
                hudSummarySource = state;
            }
            return hudSummary;
        }
    }
    public static Control CreatePage(string key) => new AccountWorkspacePage(key);

    private static AccountSnapshot itemCountSource;
    private static long itemCountRevision = -1;
    private static string itemCountAccount;
    private static Dictionary<int, int> regularItemCounts, seasonalItemCounts;
    public static int GetItemCount(int itemId, bool seasonal)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(itemCountSource, state) || itemCountRevision != state.Revision || itemCountAccount != state.AccountKey)
            {
                var regular = new Dictionary<int, int>(); var season = new Dictionary<int, int>();
                void Add(bool side, int id, int count)
                {
                    var counts = side ? season : regular;
                    counts[id] = checked((counts.TryGetValue(id, out int previous) ? previous : 0) + count);
                }
                void Stacks(bool side, int[] items, int[] counts)
                {
                    for (int i = 0; i < items.Length; i++)
                        if (items[i] > 0) Add(side, items[i], Math.Max(1, i < counts.Length ? counts[i] : 1));
                }
                foreach (var storage in state.Storage) foreach (int id in storage.Items) Add(storage.Seasonal, id, 1);
                foreach (var character in state.Characters)
                {
                    foreach (int id in character.Inventory) Add(character.Seasonal, id, 1);
                    Stacks(character.Seasonal, character.QuickSlots, character.QuickSlotCounts);
                }
                var regularPets = new HashSet<int>(); var seasonalPets = new HashSet<int>();
                foreach (var pet in state.Pets)
                    if ((pet.Seasonal ? seasonalPets : regularPets).Add(pet.Id)) Stacks(pet.Seasonal, pet.Inventory, pet.StackCounts);
                regularItemCounts = regular; seasonalItemCounts = season;
                itemCountSource = state; itemCountRevision = state.Revision; itemCountAccount = state.AccountKey;
            }
            return (seasonal ? seasonalItemCounts : regularItemCounts).TryGetValue(itemId, out int value) ? value : 0;
        }
    }

    internal static int CountItems(int[] items, int[] counts, int itemId) => items.Select((id, i) => id == itemId && id > 0 ? Math.Max(1, i < counts.Length ? counts[i] : 1) : 0).Sum();

    public static async Task<string> FetchApiAsync(string endpoint)
    {
        if (!ApiEndpoints.Contains(endpoint)) throw new ArgumentException("Unsupported account endpoint.", nameof(endpoint));
        string token; int requestGeneration;
        lock (Gate) { token = accessToken; requestGeneration = generation; }
        if (string.IsNullOrEmpty(token)) throw new InvalidOperationException("Connect a character before refreshing.");
        await ApiGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (Gate) if (requestGeneration != generation) throw new InvalidOperationException("Account changed; refresh again.");
            string body = "do_login=true&accessToken=" + Uri.EscapeDataString(token)
                + "&game_net=Unity&play_platform=Unity&game_net_user_id=&muleDump=true&__source=ExaltHelper&ignore="
                + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create("https://realmofthemadgodhrd.appspot.com/" + endpoint);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
            request.UserAgent = "ExaltHelper/1.1";
            request.AllowAutoRedirect = false;
            request.Timeout = request.ReadWriteTimeout = 20000;
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            using (var timeout = new CancellationTokenSource(20000))
            using (timeout.Token.Register(request.Abort))
            {
                using (var output = await request.GetRequestStreamAsync().ConfigureAwait(false))
                    await output.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                {
                    if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Account request was rejected.");
                    if (response.ContentLength > 8 * 1024 * 1024) throw new InvalidDataException("Account response is too large.");
                    using (var input = response.GetResponseStream())
                    using (var output = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192]; int count;
                        while ((count = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                        {
                            if (output.Length + count > 8 * 1024 * 1024) throw new InvalidDataException("Account response is too large.");
                            output.Write(buffer, 0, count);
                        }
                        string text = Encoding.UTF8.GetString(output.ToArray());
                        if (text.IndexOf("<Error", StringComparison.OrdinalIgnoreCase) >= 0)
                            throw new InvalidOperationException("Account refresh was rejected. Reconnect and try again.");
                        lock (Gate) if (requestGeneration != generation) throw new InvalidOperationException("Account changed; refresh again.");
                        return text;
                    }
                }
            }
        }
        catch (WebException ex)
        {
            int code = ex.Response is HttpWebResponse response ? (int)response.StatusCode : 0;
            ex.Response?.Dispose();
            // Do not expose request details or retain credential-bearing transport exceptions.
            throw new InvalidOperationException(code == 429 ? "Refresh limit reached. Wait a minute and try again."
                : code == 401 || code == 403 ? "Session expired. Reconnect and refresh."
                : "Cannot reach the account service. Check your connection and retry.");
        }
        finally { ApiGate.Release(); }
    }

    public static async Task RefreshAsync()
    {
        if (!await RefreshGate.WaitAsync(0).ConfigureAwait(false)) return;
        int requestGeneration;
        DateTime startedUtc = DateTime.UtcNow;
        AccountSnapshot before;
        lock (Gate) { requestGeneration = generation; before = state.Clone(); state.Status = "Refreshing account..."; state.Revision++; }
        try
        {
            string xml = await FetchApiAsync("char/list").ConfigureAwait(false);
            var parsed = ParseAccountXml(xml);
            if (string.IsNullOrEmpty(parsed.AccountKey)) throw new InvalidDataException("Account response did not identify an account.");
            lock (Gate)
            {
                if (generation != requestGeneration) return;
                if (state.AccountKey.Length > 0 && state.AccountKey != parsed.AccountKey)
                    throw new InvalidOperationException("Account identity changed. Reconnect before refreshing.");
                SelectAccountLocked(parsed.AccountKey);
                MergeApiLocked(parsed, startedUtc, before);
                state.Status = "Account synced. Seasonal storage updates when you visit its vault.";
                TouchLocked();
            }
            foreach (string endpoint in new[] { "season/seasonInfo", "season/bpInfo", "account/getOwnedPetSkins" })
            {
                try
                {
                    string extra = await FetchApiAsync(endpoint).ConfigureAwait(false);
                    lock (Gate)
                    {
                        if (generation != requestGeneration) return;
                        if (endpoint == "account/getOwnedPetSkins")
                        {
                            var skins = ReadXml(extra).Descendants("PetSkins").FirstOrDefault();
                            if (skins != null) { state.OwnedPetSkins = Items(skins.Value); TouchLocked(); }
                        }
                        else ApplySeasonInfoLocked(extra, endpoint == "season/bpInfo");
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is XmlException)
                { /* Optional metadata retains its previous value if the service is unavailable. */ }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is XmlException || ex is ArgumentException)
        {
            lock (Gate) if (generation == requestGeneration)
            { state.Status = ex is InvalidOperationException ? ex.Message : "The account response could not be read. Reconnect and retry."; state.Revision++; }
        }
        finally
        {
            bool retry; lock (Gate) retry = generation != requestGeneration && accessToken.Length > 0;
            RefreshGate.Release();
            // A new login may arrive while the previous account's refresh holds the gate.
            if (retry) _ = RefreshAsync();
        }
    }

    public static void ObservePacket(Client client, Packet packet, bool fromClient)
    {
        if (packet is HelloPacket hello && fromClient)
        {
            bool refresh = false;
            lock (Gate)
            {
                if (accessToken != hello.AccessToken)
                {
                    SavePendingLocked();
                    accessToken = hello.AccessToken ?? ""; generation++;
                    state = new AccountSnapshot { Revision = state.Revision + 1 };
                    refresh = accessToken.Length > 0;
                }
                activeClient = client; activeObjectId = -1; vaultBatch = false; pendingVault.Clear(); petObjects.Clear(); storagePages.Clear(); inPetYard = false;
                currentSeasonal = false;
                foreach (var storage in state.Storage) storage.ObjectId = -1;
            }
            if (refresh) _ = RefreshAsync();
            return;
        }
        lock (Gate)
        {
            if (activeClient != null && !ReferenceEquals(activeClient, client)) return;
            if (fromClient) return;
            if (packet is MapInfoPacket map)
            {
                vaultBatch = false; pendingVault.Clear(); petObjects.Clear(); storagePages.Clear();
                foreach (var storage in state.Storage) storage.ObjectId = -1;
                inPetYard = (map.MapName ?? "").IndexOf("Pet Yard", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            if (client?.Player != null)
            {
                activeObjectId = client.Player.ObjectId;
                if (!string.IsNullOrWhiteSpace(client.Player.AccountId)) SelectAccountLocked(HashAccountId(client.Player.AccountId));
            }
            if (packet is UpdatePacket update)
            {
                ScanPlayerIdentityLocked((update.NewObjects ?? Array.Empty<ObjectData>()).Select(o => o.Stats).FirstOrDefault(s => s != null && (s.ObjectId == activeObjectId || s.ObjectId == client?.ClientId)));
                foreach (var obj in update.NewObjects ?? Array.Empty<ObjectData>())
                    ApplyObjectLocked(client, obj.Stats, obj.ObjectType);
            }
            if (packet is NewTickPacket tick)
            {
                ScanPlayerIdentityLocked((tick.Statuses ?? new List<ObjectStatsData>()).FirstOrDefault(s => s.ObjectId == activeObjectId || s.ObjectId == client?.ClientId));
                foreach (var stats in tick.Statuses ?? new List<ObjectStatsData>()) ApplyObjectLocked(client, stats, 0);
            }
            if (packet is VaultContentPacket vault) ApplyVaultLocked(vault);
            if (packet is InvSwapPacket swap) ApplySwapLocked(swap);
            if (packet.Id == 114 || packet.Id == 120 || packet.Id == 78 || packet.Id == 46 || packet.Id == 183 || packet.Id == 153)
                ApplyRawLocked(packet.Id, packet.GetPayloadBytes());
        }
    }

    private static void ScanPlayerIdentityLocked(ObjectStatsData stats)
    {
        var identity = stats?.StatList?.FirstOrDefault(s => s.Type == 38);
        if (!string.IsNullOrWhiteSpace(identity?.StringValue)) SelectAccountLocked(HashAccountId(identity.StringValue));
        var seasonal = stats?.StatList?.FirstOrDefault(s => s.Type == 24);
        if (seasonal != null) currentSeasonal = seasonal.Value != 0;
    }

    private static void ApplyObjectLocked(Client client, ObjectStatsData stats, int objectType)
    {
        if (stats == null) return;
        if (stats.ObjectId != activeObjectId && stats.ObjectId != client?.ClientId)
        {
            ApplyStorageStatsLocked(stats);
            if (inPetYard) ApplyPetStatsLocked(stats, objectType);
            return;
        }
        var accountStat = stats.StatList?.FirstOrDefault(s => s.Type == 38);
        if (!string.IsNullOrEmpty(accountStat?.StringValue)) SelectAccountLocked(HashAccountId(accountStat.StringValue));
        int charId = client?.CharacterId ?? state.CurrentCharacterId;
        if (charId < 0) return;
        if (state.DeadCharacters.Any(c => c.Id == charId)) return;
        state.CurrentCharacterId = charId;
        var character = state.Characters.FirstOrDefault(c => c.Id == charId);
        if (character == null) { character = new AccountCharacter { Id = charId, Inventory = Enumerable.Repeat(-1, 28).ToArray() }; state.Characters.Add(character); }
        if (objectType > 0) character.ClassType = objectType;
        bool changed = false;
        var seasonalStat = stats.StatList?.FirstOrDefault(s => s.Type == 24);
        if (seasonalStat != null) { currentSeasonal = seasonalStat.Value != 0; character.Seasonal = currentSeasonal; }
        foreach (var stat in stats.StatList ?? new List<StatData>())
        {
            int id = stat.Type;
            if (id == 24) { character.Seasonal = stat.Value != 0; currentSeasonal = character.Seasonal; changed = true; }
            else if (id == 31) { character.Name = stat.StringValue ?? ""; state.Name = character.Name; changed = true; }
            else if (id == 7) { character.Level = stat.Value; changed = true; }
            else if (id == 57) { character.Fame = stat.Value; changed = true; }
            else if (id == 35) { state.Gold = stat.Value; changed = true; }
            else if (id == 39) { state.Fame = stat.Value; changed = true; }
            else if (id == 30) { state.Stars = stat.Value; changed = true; }
            else if (id == 120) { state.ForgeFire = stat.Value; if (currentSeasonal) state.SeasonalForgeFire = stat.Value; else state.RegularForgeFire = stat.Value; changed = true; }
            else if ((id == 71 || id == 72) && !string.IsNullOrEmpty(stat.StringValue))
            {
                int?[] amounts = id == 71 ? currentSeasonal ? state.SeasonalMaterials : state.RegularMaterials
                    : currentSeasonal ? state.SeasonalMaterialCaps : state.RegularMaterialCaps;
                foreach (var part in stat.StringValue.Split(','))
                {
                    var fields = part.Split(':'); if (fields.Length != 2) continue;
                    int tier = Number(fields[0]);
                    if (tier >= 1 && tier <= 4 && int.TryParse(fields[1], out int amount)) amounts[tier - 1] = Math.Max(0, amount);
                }
                changed = true;
            }
            else if ((id == 127 || id == 147) && !string.IsNullOrEmpty(stat.StringValue))
            {
                var amounts = currentSeasonal ? state.SeasonalDustAmounts : state.RegularDustAmounts;
                var caps = currentSeasonal ? state.SeasonalDustCaps : state.RegularDustCaps;
                foreach (var part in stat.StringValue.Split(','))
                {
                    var fields = part.Split(':'); if (fields.Length != 2) continue;
                    int dust = Number(fields[0]), index = dust == 1 ? 0 : dust == 4 ? 1 : dust == 5 ? 2 : -1;
                    if (index < 0 || !int.TryParse(fields[1], out int amount)) continue;
                    if (id == 127) amounts[index] = Math.Max(0, amount); else caps[index] = Math.Max(0, amount);
                }
                if (id == 127) { if (currentSeasonal) state.SeasonalDust = amounts.Sum(); else state.RegularDust = amounts.Sum(); }
                changed = true;
            }
            else if (id == 25) { character.Skin = stat.Value; changed = true; }
            else if (id == 128) { character.Crucible = !string.IsNullOrEmpty(stat.StringValue) && stat.StringValue != "0"; changed = true; }
            else if (id == 155) { character.BloodRitualId = stat.StringValue ?? ""; changed = true; }
            else if (id == 113) { character.ExaltDamage = stat.Value; changed = true; }
            else if (id == 66 || id == 67 || id == 68)
            {
                if (id == 66) { character.XpBoostSeconds = Math.Max(0, stat.Value); character.XpBoostObservedUtc = DateTime.UtcNow; }
                if (id == 67) { character.LootBoostSeconds = Math.Max(0, stat.Value); character.LootBoostObservedUtc = DateTime.UtcNow; }
                if (id == 68) { character.LootTierBoostSeconds = Math.Max(0, stat.Value); character.LootTierBoostObservedUtc = DateTime.UtcNow; }
                character.BoostObservedUtc = DateTime.UtcNow; changed = true;
            }
            else if (id == 29) { character.Effects = stat.Value; changed = true; }
            else if (id == 96) { character.Effects2 = stat.Value; changed = true; }
            int slot = InventorySlot(id);
            if (slot >= 0)
            {
                if (character.Inventory.Length <= slot) { var inventory = Enumerable.Repeat(-1, slot + 1).ToArray(); Array.Copy(character.Inventory, inventory, character.Inventory.Length); character.Inventory = inventory; }
                character.Enchants = Resize(character.Enchants, character.Inventory.Length);
                if (character.Inventory[slot] != stat.Value) character.Enchants[slot] = "";
                character.Inventory[slot] = stat.Value; changed = true;
            }
            if (id >= 116 && id <= 118)
            {
                slot = id - 116; if (character.QuickSlots[slot] != stat.Value) character.QuickSlotEnchants[slot] = "";
                character.QuickSlots[slot] = stat.Value;
                character.QuickSlotCounts[slot] = stat.Value > 0 ? Math.Max(1, (int)stat.SecondaryValue) : 0; changed = true;
            }
        }
        var enchantStat = stats.StatList?.FirstOrDefault(s => s.Type == 80);
        if (enchantStat != null) { character.Enchants = SplitEnchants(enchantStat.StringValue, character.Inventory.Length); changed = true; }
        // Use the applied entity's total-minus-equipment values to show unmaxed base stats.
        var p = client?.Player;
        if (p != null)
        {
            character.ClassType = p.ObjectType;
            character.ObservedStats = new[] { p.MaxHp, p.MaxMp, p.Attack, p.Defense, p.Speed, p.Dexterity, p.Vitality, p.Wisdom };
            character.ObservedUtc = DateTime.UtcNow;
            var baseStats = new[] { Math.Max(0, p.MaxHp - p.HealthBonus), Math.Max(0, p.MaxMp - p.ManaBonus),
                Math.Max(0, p.Attack - p.AttackBonus), Math.Max(0, p.Defense - p.DefenseBonus), Math.Max(0, p.Speed - p.SpeedBonus),
                Math.Max(0, p.Dexterity - p.DexterityBonus), Math.Max(0, p.Vitality - p.VitalityBonus), Math.Max(0, p.Wisdom - p.WisdomBonus) };
            for (int i = 0; i < baseStats.Length; i++) baseStats[i] = Math.Max(baseStats[i], character.Stats[i]);
            if (!character.Stats.SequenceEqual(baseStats)) { character.Stats = baseStats; changed = true; }
        }
        if (changed) { character.UpdatedUtc = DateTime.UtcNow; TouchLocked(); }
    }

    private static int InventorySlot(int stat) => stat >= 8 && stat <= 19 ? stat - 8 : stat >= 131 && stat <= 146 ? stat - 131 + 12 : -1;

    private static void ApplySeasonInfoLocked(string json, bool battlePass)
    {
        var data = Serializer().DeserializeObject(json) as Dictionary<string, object>;
        if (battlePass)
        {
            if (data != null && data.TryGetValue("battlePass", out object nested)) data = nested as Dictionary<string, object>;
            else data = null;
        }
        if (data == null)
        {
            if (battlePass) { state.BattlePassEndUtc = null; state.BattlePassName = ""; TouchLocked(); }
            return;
        }
        long start = data.TryGetValue("start", out object startValue) && long.TryParse(Convert.ToString(startValue, CultureInfo.InvariantCulture), out long startNumber) ? startNumber : 0;
        long end = data.TryGetValue("end", out object endValue) && long.TryParse(Convert.ToString(endValue, CultureInfo.InvariantCulture), out long endNumber) ? endNumber : 0;
        if (start <= 0 || end <= start || end > 253402300799L) return;
        DateTime expires = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(end);
        string name = data.TryGetValue(battlePass ? "title" : "name", out object title) ? Convert.ToString(title, CultureInfo.InvariantCulture)?.Trim() ?? "" : "";
        if (battlePass) { state.BattlePassEndUtc = expires; state.BattlePassName = name; }
        else { state.SeasonEndUtc = expires; state.SeasonName = name; }
        TouchLocked();
    }

    private static void ApplyPetStatsLocked(ObjectStatsData stats, int objectType)
    {
        var values = stats.StatList ?? new List<StatData>();
        var instance = values.FirstOrDefault(s => s.Type == 81);
        if (instance != null && instance.Value > 0) petObjects[stats.ObjectId] = instance.Value;
        if (!petObjects.TryGetValue(stats.ObjectId, out int petId)) return;
        var pet = state.Pets.FirstOrDefault(p => p.Id == petId && p.Seasonal == currentSeasonal);
        if (pet == null) { pet = new AccountPet { Id = petId, Seasonal = currentSeasonal, Skin = objectType,
            AbilityTypes = new int[3], AbilityLevels = new int[3] }; state.Pets.Add(pet); }
        if (!pet.IdentityFromApi && objectType > 0) pet.Skin = objectType;
        foreach (var stat in values)
        {
            int id = stat.Type;
            if (!pet.IdentityFromApi)
            {
                if (id == 82 && !string.IsNullOrWhiteSpace(stat.StringValue)) pet.Name = stat.StringValue;
                if (id == 83) pet.Type = stat.Value;
                if (id == 84) pet.Rarity = stat.Value;
                if (id == 85) pet.MaximumLevel = stat.Value;
            }
            if (id >= 90 && id <= 92) { if (pet.AbilityLevels.Length < 3) pet.AbilityLevels = new int[3]; pet.AbilityLevels[id - 90] = stat.Value; }
            if (id >= 93 && id <= 95 && !pet.IdentityFromApi) { if (pet.AbilityTypes.Length < 3) pet.AbilityTypes = new int[3]; pet.AbilityTypes[id - 93] = stat.Value; }
            int slot = InventorySlot(id);
            if (slot >= 0)
            {
                if (pet.Inventory.Length <= slot) pet.Inventory = pet.Inventory.Concat(Enumerable.Repeat(-1, slot + 1 - pet.Inventory.Length)).ToArray();
                pet.StackCounts = Resize(pet.StackCounts, pet.Inventory.Length);
                pet.Enchants = Resize(pet.Enchants, pet.Inventory.Length);
                if (pet.Inventory[slot] != stat.Value) pet.Enchants[slot] = "";
                pet.Inventory[slot] = stat.Value;
                pet.StackCounts[slot] = stat.Value > 0 ? Math.Max(1, (int)stat.SecondaryValue) : 0;
            }
        }
        var enchantStat = values.FirstOrDefault(s => s.Type == 80);
        if (enchantStat != null) pet.Enchants = SplitEnchants(enchantStat.StringValue, pet.Inventory.Length);
        pet.UpdatedUtc = DateTime.UtcNow; TouchLocked();
    }

    private static void ApplyStorageStatsLocked(ObjectStatsData stats)
    {
        var storage = state.Storage.FirstOrDefault(s => s.ObjectId == stats.ObjectId && s.Seasonal == currentSeasonal);
        if (storage == null) return;
        var changes = (stats.StatList ?? new List<StatData>()).Where(s => s.Type >= 8 && s.Type <= 15).ToArray();
        if (changes.Length == 0) return;
        int page = -1;
        for (int candidate = 0; candidate < (storage.Items.Length + 7) / 8; candidate++)
        {
            if (changes.All(s => { int index = StorageIndex(storage, candidate, (int)s.Type - 8); return index >= 0 && index < storage.Items.Length ? storage.Items[index] == s.Value : storage.Name != "Gifts" && s.Value == -1; }))
            { page = candidate; break; }
        }
        if (page >= 0) { storagePages[stats.ObjectId] = page; return; }
        // A single page has an unambiguous address; larger containers require an observed page.
        if (!storagePages.TryGetValue(stats.ObjectId, out page)) { if (storage.Items.Length <= 8) page = 0; else return; }
        bool changed = false; storage.Enchants = Resize(storage.Enchants, storage.Items.Length);
        foreach (var stat in changes)
        {
            int index = StorageIndex(storage, page, (int)stat.Type - 8);
            if (index < 0 || index >= storage.Items.Length) continue;
            if (storage.Items[index] != stat.Value) storage.Enchants[index] = "";
            storage.Items[index] = stat.Value; changed = true;
        }
        if (changed) { storage.UpdatedUtc = DateTime.UtcNow; TouchLocked(); }
    }

    private static int StorageIndex(AccountStorage storage, int page, int slot) => storage.Name == "Gifts"
        ? storage.Items.Length - 1 - (page * 8 + slot) : page * 8 + slot;

    private static void ApplyVaultLocked(VaultContentPacket packet)
    {
        if (!vaultBatch) { pendingVault.Clear(); storagePages.Clear(); vaultBatch = true; }
        AppendVault("Vault", packet.VaultChestObjectId, packet.VaultContents, packet.UnknownString1);
        AppendVault("Materials", packet.MaterialChestObjectId, packet.MaterialContents);
        AppendVault("Gifts", packet.GiftChestObjectId, packet.GiftContents, packet.UnknownString2, true);
        AppendVault("Potions", packet.PotionStorageObjectId, packet.PotionContents);
        if (!currentSeasonal) AppendVault("Seasonal spoils", packet.SeasonalSpoilChestObjectId, packet.SeasonalSpoilContents, packet.UnknownString3);
        if (!packet.IsLastVaultPacket) return;
        foreach (var storage in pendingVault)
        {
            state.Storage.RemoveAll(s => s.Name == storage.Name && s.Seasonal == storage.Seasonal);
            state.Storage.Add(storage.Clone());
        }
        pendingVault.Clear(); vaultBatch = false; TouchLocked();
    }

    private static void AppendVault(string name, int objectId, int[] items, string enchantData = null, bool reverse = false)
    {
        var storage = pendingVault.FirstOrDefault(s => s.Name == name);
        if (storage == null) { storage = new AccountStorage { Name = name, ObjectId = objectId, Seasonal = currentSeasonal }; pendingVault.Add(storage); }
        items = items ?? Array.Empty<int>(); var enchants = SplitEnchants(enchantData, items.Length);
        storage.Items = storage.Items.Concat(reverse ? Enumerable.Reverse(items) : items).Take(100000).ToArray();
        storage.Enchants = storage.Enchants.Concat(reverse ? Enumerable.Reverse(enchants) : enchants).Take(100000).ToArray();
        storage.UpdatedUtc = DateTime.UtcNow;
    }

    private static void ApplyRawLocked(byte id, byte[] payload)
    {
        if (payload == null) return;
        try
        {
            using (var reader = new PacketReader(new MemoryStream(payload)))
            {
                if (id == 114)
                {
                    int type = reader.ReadInt16(); var raw = new int[8];
                    for (int i = 0; i < raw.Length; i++) raw[i] = Math.Max(0, reader.ReadCompressedInt());
                    state.Exaltations[type.ToString(CultureInfo.InvariantCulture)] = new[] { raw[7], raw[6], raw[5], raw[4], raw[1], raw[0], raw[2], raw[3] };
                }
                else if (id == 120)
                {
                    bool seasonal = reader.ReadByte() != 0; int count = reader.ReadCompressedInt();
                    if (count < 0 || count > 100000 || count > reader.BaseStream.Length - reader.BaseStream.Position) return;
                    var items = new int[count]; for (int i = 0; i < count; i++) items[i] = reader.ReadCompressedInt();
                    if (seasonal) state.SeasonalBlueprints = items; else state.RegularBlueprints = items;
                }
                else if (id == 78)
                {
                    int yard = reader.ReadInt32(); if (yard < 1 || yard > 5) return;
                    if (currentSeasonal) state.SeasonalPetYard = yard; else state.RegularPetYard = yard;
                }
                else if (id == 46) { ApplyDeathLocked(reader); return; }
                else if (id == 183) { ApplyChallengeFeedLocked(reader); return; }
                else if (id == 153) { ApplyAcceleratorLocked(reader.ReadInt32()); return; }
                else return;
                TouchLocked();
            }
        }
        catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is OverflowException) { }
    }

    internal static XDocument ReadXml(string xml)
    {
        using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 })) return XDocument.Load(reader);
    }

    internal static AccountSnapshot ParseAccountXml(string xml)
    {
        var document = ReadXml(xml); var root = document.Root;
        if (root?.Name.LocalName != "Chars") throw new InvalidDataException("Unexpected account response.");
        var account = root.Element("Account"); var result = new AccountSnapshot { UpdatedUtc = DateTime.UtcNow };
        string id = (string)account?.Element("AccountId") ?? "";
        if (id.Length > 0) result.AccountKey = HashAccountId(id);
        result.Name = (string)account?.Element("Name") ?? "Account";
        result.Gold = NullableNumber(account?.Element("Credits"));
        result.Fame = NullableNumber(account?.Element("Stats")?.Element("Fame") ?? account?.Element("Fame"));
        result.CharacterSlots = Number(account?.Element("MaxNumChars")?.Value ?? (string)root.Attribute("maxNumChars"));
        result.OwnedSkins = Items((string)account?.Element("OwnedSkins"));
        result.OwnedEmotes = Items((string)account?.Element("OwnedEmotes"));
        result.RegularBlueprints = Items((string)account?.Element("RegularForgeFireBlueprints"));
        result.Accelerators = ParseAccelerators((string)root.Element("Accelerators"), (string)root.Element("Timestamp"));
        var bestFames = account?.Element("Stats")?.Elements("ClassStats").Select(e => Number((string)e.Element("BestBaseFame"))).ToArray();
        if (bestFames != null && bestFames.Length > 0) result.Stars = bestFames.Sum(f => new[] { 20, 150, 400, 800, 2000 }.Count(t => f >= t));
        foreach (var node in root.Elements("Char"))
        {
            var c = new AccountCharacter { Id = Number((string)node.Attribute("id")), ClassType = Number((string)node.Element("ObjectType")),
                Name = result.Name, Level = Number((string)node.Element("Level")), Fame = Number((string)node.Element("CurrentFame")),
                Seasonal = string.Equals((string)node.Element("Seasonal"), "True", StringComparison.OrdinalIgnoreCase),
                // The API retains CrucibleActive after a season ends; only live acceptance is authoritative.
                Crucible = false, Skin = Number((string)node.Element("Texture")),
                Created = (string)node.Element("CreationDate") ?? "", UpdatedUtc = DateTime.UtcNow,
                Inventory = Items((string)node.Element("Equipment")),
                Stats = new[] { "MaxHitPoints", "MaxMagicPoints", "Attack", "Defense", "Speed", "Dexterity", "HpRegen", "MpRegen" }.Select(n => Number((string)node.Element(n))).ToArray() };
            if (double.TryParse((string)node.Element("LDTimer"), NumberStyles.Float, CultureInfo.InvariantCulture, out double lootSeconds))
            { c.LootBoostSeconds = (int)Math.Max(0, Math.Min(int.MaxValue, lootSeconds)); c.BoostObservedUtc = c.LootBoostObservedUtc = DateTime.UtcNow; }
            string pcstats = (string)node.Element("PCStats");
            c.PcStats = AccountWorkspaceFame.Decode(pcstats);
            c.HasPcStats = !string.IsNullOrEmpty(pcstats) && c.PcStats != null;
            c.PcStats = c.PcStats ?? new Dictionary<string, int>();
            var enchantQueues = node.Element("UniqueItemInfo")?.Elements("ItemData")
                .GroupBy(e => Number((string)e.Attribute("type") ?? (string)e.Attribute("id")))
                .ToDictionary(g => g.Key, g => new Queue<string>(g.Select(e => e.Value))) ?? new Dictionary<int, Queue<string>>();
            string NextEnchant(int item) => enchantQueues.TryGetValue(item, out var queue) && queue.Count > 0 ? queue.Dequeue() : "";
            c.Enchants = c.Inventory.Select(NextEnchant).ToArray();
            var quick = ((string)node.Element("EquipQS") ?? "").Split(',');
            for (int i = 0; i < Math.Min(3, quick.Length); i++)
            {
                var fields = quick[i].Split('|'); if (fields.Length != 2) continue;
                c.QuickSlots[i] = Number(fields[0]); c.QuickSlotCounts[i] = c.QuickSlots[i] > 0 ? Math.Max(1, Math.Min(255, Number(fields[1]))) : 0;
                c.QuickSlotEnchants[i] = NextEnchant(c.QuickSlots[i]);
            }
            var petNode = node.Element("Pet");
            if (petNode != null)
            {
                var pet = ParsePet(petNode, c.Seasonal); c.PetId = pet.Id;
                if (!result.Pets.Any(p => p.Id == pet.Id && p.Seasonal == pet.Seasonal)) result.Pets.Add(pet);
            }
            result.Characters.Add(c);
        }
        var uniqueItems = new Dictionary<string, string>();
        foreach (var entry in account?.Elements().Where(e => e.Name.LocalName == "UniqueItemInfo" || e.Name.LocalName == "UniqueGiftItemInfo" || e.Name.LocalName == "UniqueTemporaryGiftItemInfo").SelectMany(e => e.Elements("ItemData")) ?? Enumerable.Empty<XElement>())
        { string uniqueId = (string)entry.Attribute("id"); if (!string.IsNullOrEmpty(uniqueId)) uniqueItems[uniqueId] = entry.Value; }
        AddApiStorage(result, "Vault", account?.Element("Vault")?.Elements("Chest").Select(c => c.Value), uniqueItems);
        AddApiStorage(result, "Materials", account?.Element("MaterialStorage")?.Elements("Chest").Select(c => c.Value), uniqueItems);
        AddApiStorage(result, "Gifts", account?.Element("Gifts") == null ? null : new[] { (string)account.Element("Gifts") }, uniqueItems);
        AddApiStorage(result, "Potions", account?.Element("Potions") == null ? null : new[] { (string)account.Element("Potions") }, uniqueItems);
        foreach (var element in root.Element("PowerUpStats")?.Elements("ClassStats") ?? Enumerable.Empty<XElement>())
        {
            var raw = Items(element.Value); if (raw.Length != 8) continue;
            int type = Number((string)element.Attribute("class"));
            result.Exaltations[type.ToString(CultureInfo.InvariantCulture)] = new[] { raw[7], raw[6], raw[5], raw[4], raw[1], raw[0], raw[2], raw[3] };
        }
        return result;
    }

    private static AccountPet ParsePet(XElement node, bool seasonal)
    {
        var parts = ((string)node.Attribute("inv") ?? "").Split(';');
        int count = parts.Length > 0 ? Number(parts[0].Split(',').LastOrDefault()) : 0;
        var items = parts.Length > 2 ? Items(parts[2]) : Array.Empty<int>();
        if (count > items.Length && count < 1000) items = items.Concat(Enumerable.Repeat(-1, count - items.Length)).ToArray();
        return new AccountPet { Id = Number((string)node.Attribute("instanceId")), Name = (string)node.Attribute("name") ?? "Pet",
            Type = Number((string)node.Attribute("type")), Skin = Number((string)node.Attribute("skin")), Rarity = Number((string)node.Attribute("rarity")),
            Seasonal = seasonal, Inventory = items, StackCounts = items.Select(i => i > 0 ? 1 : 0).ToArray(), Enchants = new string[items.Length],
            IdentityFromApi = true, UpdatedUtc = DateTime.UtcNow, MaximumLevel = Number((string)node.Attribute("maxAbilityPower")),
            AbilityTypes = node.Elements("Ability").Select(a => Number((string)a.Attribute("type"))).ToArray(),
            AbilityPowers = node.Elements("Ability").Select(a => Number((string)a.Attribute("power"))).ToArray() };
    }

    private static void AddApiStorage(AccountSnapshot target, string name, IEnumerable<string> chests, Dictionary<string, string> uniqueItems)
    {
        if (chests == null) return;
        var slots = chests.Where(c => !string.IsNullOrWhiteSpace(c)).SelectMany(c => c.Split(',')).Take(100000).ToArray();
        target.Storage.Add(new AccountStorage { Name = name, Items = slots.Select(s => Items(s).FirstOrDefault()).ToArray(),
            Enchants = slots.Select(s => { var fields = s.Split('#'); return fields.Length > 1 && uniqueItems.TryGetValue(fields[1], out string code) ? code : ""; }).ToArray(), UpdatedUtc = DateTime.UtcNow });
    }
    internal static int Number(string value) => value != null && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? int.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex) ? hex : 0
        : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;
    private static int? NullableNumber(XElement element) => element == null ? (int?)null : Number(element.Value);
    internal static int[] Items(string value) => string.IsNullOrWhiteSpace(value) ? Array.Empty<int>() : value.Split(',').Take(100000)
        .Select(s => int.TryParse(s.Trim().Split('#')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : -1).ToArray();
    internal static string HashAccountId(string id) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").ToLowerInvariant(); }

    private static void SelectAccountLocked(string key)
    {
        if (state.AccountKey == key || key.Length != 64) return;
        if (state.AccountKey.Length > 0) { SavePendingLocked(); generation++; }
        var cached = pendingSaves.TryGetValue(key, out var queued) ? queued.Clone() : Load(key);
        cached.Revision = Math.Max(cached.Revision, state.Revision) + 1;
        cached.Status = "Cached account data. Refresh to synchronize.";
        cached.CurrentCharacterId = -1;
        foreach (var storage in cached.Storage) storage.ObjectId = -1;
        storagePages.Clear(); petObjects.Clear(); pendingVault.Clear(); vaultBatch = false;
        state = cached; state.AccountKey = key; dirty = false;
    }

    private static void MergeApiLocked(AccountSnapshot parsed, DateTime requestStartedUtc, AccountSnapshot before)
    {
        // Live changes observed while the web request ran take precedence over its older snapshot.
        DateTime now = DateTime.UtcNow;
        if (parsed.AccountKey != state.AccountKey) throw new InvalidOperationException("Account identity changed. Reconnect before refreshing.");
        var previous = state.Characters.ToDictionary(c => c.Id);
        var incomingIds = new HashSet<int>(parsed.Characters.Select(c => c.Id));
        foreach (var missing in state.Characters.Where(c => !incomingIds.Contains(c.Id) && c.UpdatedUtc <= requestStartedUtc).ToArray())
            MarkDeadLocked(missing.Id, "Missing from latest account refresh (death or deletion)", 0, 0, null, null);
        var live = state.Characters.FirstOrDefault(c => c.Id == state.CurrentCharacterId);
        state.Name = parsed.Name;
        var deadIds = new HashSet<int>(state.DeadCharacters.Select(c => c.Id));
        state.Characters = parsed.Characters.Where(c => !deadIds.Contains(c.Id)).ToList();
        // Characters observed after the request began can be absent from its older response.
        state.Characters.AddRange(previous.Values.Where(c => !incomingIds.Contains(c.Id) && !deadIds.Contains(c.Id) && c.UpdatedUtc > requestStartedUtc));
        foreach (var c in state.Characters) if (previous.TryGetValue(c.Id, out var old))
        {
            c.Label = old.Label; c.Order = old.Order; c.Fame = Math.Max(c.Fame, old.Fame); c.Crucible = old.Crucible;
            c.BloodRitualId = old.BloodRitualId; c.ExaltDamage = old.ExaltDamage;
            if (old.ObservedUtc > requestStartedUtc) { c.ObservedStats = old.ObservedStats; c.ObservedUtc = old.ObservedUtc; c.Effects = old.Effects; c.Effects2 = old.Effects2; }
            c.XpBoostSeconds = old.XpBoostSeconds; c.LootTierBoostSeconds = old.LootTierBoostSeconds;
            c.XpBoostObservedUtc = old.XpBoostObservedUtc ?? old.BoostObservedUtc;
            c.LootTierBoostObservedUtc = old.LootTierBoostObservedUtc ?? old.BoostObservedUtc;
            if (!c.LootBoostSeconds.HasValue || old.LootBoostObservedUtc > requestStartedUtc)
            { c.LootBoostSeconds = old.LootBoostSeconds; c.LootBoostObservedUtc = old.LootBoostObservedUtc ?? old.BoostObservedUtc; c.BoostObservedUtc = old.BoostObservedUtc; }
        }
        if (live != null && live.UpdatedUtc > requestStartedUtc)
        {
            var fromApi = state.Characters.FirstOrDefault(c => c.Id == live.Id);
            if (fromApi != null) { live.PcStats = fromApi.PcStats; live.HasPcStats = fromApi.HasPcStats; live.Created = fromApi.Created; live.PetId = fromApi.PetId; }
            state.Characters.RemoveAll(c => c.Id == live.Id); state.Characters.Add(live);
        }
        foreach (var storage in parsed.Storage)
        {
            var current = state.Storage.FirstOrDefault(s => s.Name == storage.Name && !s.Seasonal);
            if (current != null && current.UpdatedUtc > requestStartedUtc) continue;
            if (current != null)
            {
                storage.ObjectId = current.ObjectId;
                if (!current.Items.SequenceEqual(storage.Items)) storagePages.Remove(current.ObjectId);
            }
            state.Storage.RemoveAll(s => s.Name == storage.Name && !s.Seasonal); state.Storage.Add(storage);
        }
        foreach (var pet in parsed.Pets)
        {
            var livePet = state.Pets.FirstOrDefault(p => p.Id == pet.Id && p.Seasonal == pet.Seasonal);
            if (livePet != null) pet.AbilityLevels = livePet.AbilityLevels;
            if (livePet != null && livePet.UpdatedUtc > requestStartedUtc)
            {
                pet.Inventory = livePet.Inventory; pet.StackCounts = livePet.StackCounts; pet.Enchants = livePet.Enchants;
                pet.UpdatedUtc = livePet.UpdatedUtc;
            }
            state.Pets.RemoveAll(p => p.Id == pet.Id && p.Seasonal == pet.Seasonal); state.Pets.Add(pet);
        }
        foreach (var entry in parsed.Exaltations)
        {
            if (state.Exaltations.TryGetValue(entry.Key, out var existing))
                state.Exaltations[entry.Key] = entry.Value.Select((v, i) => Math.Max(v, i < existing.Length ? existing[i] : 0)).ToArray();
            else state.Exaltations[entry.Key] = entry.Value;
        }
        if (before.AccountKey != state.AccountKey || state.Gold == before.Gold) state.Gold = parsed.Gold ?? state.Gold;
        if (before.AccountKey != state.AccountKey || state.Fame == before.Fame) state.Fame = parsed.Fame ?? state.Fame;
        if (before.AccountKey != state.AccountKey || state.Stars == before.Stars) state.Stars = parsed.Stars ?? state.Stars;
        state.CharacterSlots = parsed.CharacterSlots; state.OwnedSkins = parsed.OwnedSkins; state.OwnedEmotes = parsed.OwnedEmotes;
        state.RegularBlueprints = parsed.RegularBlueprints; state.UpdatedUtc = now;
        var recentAccelerators = state.Accelerators.Where(a => a.ObservedUtc > requestStartedUtc).ToArray();
        state.Accelerators = parsed.Accelerators.Where(a => !recentAccelerators.Any(r => r.ItemType == a.ItemType)).Concat(recentAccelerators).ToList();
    }

    private static void TouchLocked() { state.Revision++; dirty = true; }
    private static JavaScriptSerializer Serializer() => new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024, RecursionLimit = 40 };
    private static AccountSnapshot Load(string key)
    {
        try
        {
            string path = Path.Combine(DataDirectory, key + ".json");
            if (File.Exists(path) && new FileInfo(path).Length < 8 * 1024 * 1024)
            {
                var value = Serializer().Deserialize<AccountSnapshot>(File.ReadAllText(path));
                if (value?.AccountKey == key) { Normalize(value); return value; }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException) { }
        return new AccountSnapshot { AccountKey = key };
    }
    // Timer/shutdown entry point. Packet handlers only queue immutable snapshots;
    // serialization and file replacement happen after releasing the tracker lock.
    public static void SavePending()
    {
        lock (Gate) SavePendingLocked();
        lock (SaveGate)
        {
            AccountSnapshot[] pending; lock (Gate) pending = pendingSaves.Values.OrderBy(s => s.Revision).ToArray();
            foreach (var snapshot in pending)
            {
                if (writtenRevisions.TryGetValue(snapshot.AccountKey, out long written) && written >= snapshot.Revision)
                {
                    lock (Gate) if (pendingSaves.TryGetValue(snapshot.AccountKey, out var queued) && queued.Revision == snapshot.Revision) pendingSaves.Remove(snapshot.AccountKey);
                    continue;
                }
                try
                {
                    string json = Serializer().Serialize(snapshot);
                    Directory.CreateDirectory(DataDirectory);
                    string path = Path.Combine(DataDirectory, snapshot.AccountKey + ".json"), temporary = path + ".tmp";
                    File.WriteAllText(temporary, json, new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                    writtenRevisions[snapshot.AccountKey] = snapshot.Revision;
                    lock (Gate) if (pendingSaves.TryGetValue(snapshot.AccountKey, out var queued) && queued.Revision == snapshot.Revision) pendingSaves.Remove(snapshot.AccountKey);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
                {
                    lock (Gate) if (state.AccountKey == snapshot.AccountKey)
                    { state.Status = "Account data is available, but its local cache could not be saved."; state.Revision++; }
                }
            }
            string selected; lock (Gate) selected = state.AccountKey;
            if (selected.Length == 64 && selected != writtenAccountKey)
            {
                try
                {
                    Directory.CreateDirectory(DataDirectory);
                    File.WriteAllText(Path.Combine(DataDirectory, "last-account.txt"), selected, new UTF8Encoding(false));
                    writtenAccountKey = selected;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
        }
    }
    private static void SavePendingLocked()
    {
        if (!dirty || state.AccountKey.Length != 64) return;
        pendingSaves[state.AccountKey] = state.Clone(); dirty = false;
    }

    internal static int[] ClassMaximums(int type)
    {
        if (ClassStats.Map.TryGetValue((ushort)type, out var c)) return new[] { c.LifeMax, c.ManaMax, c.AttackMax, c.DefenseMax, c.SpeedMax, c.DexterityMax, c.VitalityMax, c.WisdomMax };
        return new int[8];
    }
    internal static string ClassName(int type)
    {
        if (type == 817) return "Summoner"; if (type == 818) return "Kensei"; if (type == 819) return "Druid";
        return Enum.IsDefined(typeof(PlayerClassType), (short)type) ? ((PlayerClassType)(short)type).ToString() : "Class " + type;
    }
    internal static int ExaltLevel(int count) => new[] { 5, 15, 30, 50, 75 }.Count(n => count >= n);
}
