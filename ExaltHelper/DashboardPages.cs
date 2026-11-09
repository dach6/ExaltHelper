using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal partial class ExaltDashboardForm
{
    private readonly Dictionary<string, HudTablePage> tables = new Dictionary<string, HudTablePage>();
    private readonly List<Label> liveMetrics = new List<Label>();
    private DpsOverlayForm moonlightView;
    private HudCalculatorPage calculator;
    private HudCombatTrackerPage combatTracker;
    private Label calloutPreview;
    private Label soundStatus;
    private Button copyRoster, copyThanks;
    private readonly List<Button> quickTextButtons = new List<Button>();
    private string quickTextSignature;
    private DateTime autoCalloutArmedUtc = DateTime.UtcNow, autoCalloutExpiresUtc;
    private string autoCalloutAccount, autoCalloutLocation, autoCalloutText;
    private bool autoCalloutEnabled;
    private readonly HashSet<string> copiedCalloutIds = new HashSet<string>();
    private DpsSnapshot CurrentLiveSnapshot => snapshot.HistoryIndex >= 0 ? HudJournal.LiveSnapshot : snapshot;
    private readonly HashSet<string> watchlist = new HashSet<string>(HudStorage.Load("party-watchlist.json", new List<string>()) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> banned = new HashSet<string>(HudStorage.Load("party-banned.json", new List<string>()) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

    private Control CreatePage(string key)
    {
        switch (key)
        {
            case "missions": return MissionWorkspace.CreateMissionsPage();
            case "quests": return MissionWorkspace.CreateQuestsPage();
            case "characters": case "treasury": case "vault": case "exaltations": return AccountWorkspace.CreatePage(key);
            case "pets": return AccountWorkspace.CreatePage("petyard");
            case "live":
                var panel = new Panel();
                var feed = MakeTable(key, "Your realm events, travel, direct messages, and alerts in one place.", "Time", "Type", "From", "Event", "Location");
                ItemColumn(feed, "Event", FeedItems);
                HudGameSprites.Attach(feed.Grid, "Location", value => HudGameSprites.Portal((value as HudLogEntry)?.Location), "live");
                feed.Toolbar.WrapContents = true; feed.Toolbar.AutoScroll = false; feed.Toolbar.AutoSize = true;
                feed.Toolbar.Controls.Add(HudTheme.Button("Copy callout", (_, __) => CopyCallout()));
                feed.Toolbar.Controls.Add(HudTheme.Button("Callout settings", (_, __) => OpenCalloutSettings()));
                copyRoster = HudTheme.Button("Copy roster", (_, __) => {
                    var row = feed.Grid.CurrentRow?.Tag as HudLogEntry; var names = HudCallouts.ParseRoster(row?.Text);
                    if (names != null && names.Length > 0) CopyText(string.Join(", ", names));
                }); feed.Toolbar.Controls.Add(copyRoster);
                copyThanks = HudTheme.Button("Copy thanks", (_, __) => CopyText(ThanksFor(feed.Grid.CurrentRow?.Tag as HudLogEntry))); feed.Toolbar.Controls.Add(copyThanks);
                feed.Grid.SelectionChanged += (_, __) => RefreshCalloutPreview();
                var metrics = new TableLayoutPanel { Dock = DockStyle.Top, Height = 104, ColumnCount = 4, Padding = new Padding(0, 0, 0, 18) };
                foreach (string label in new[] { "CURRENT TARGET", "GROUP DPS", "DUNGEON DAMAGE", "PLAYERS / KILLS" })
                {
                    metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                    var card = new HudSurfacePanel { Dock = DockStyle.Fill, BackColor = HudTheme.Surface, Padding = new Padding(14, 8, 8, 6), Margin = new Padding(0, 0, 10, 0) };
                    card.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 23, Font = HudTheme.SmallFont, ForeColor = HudTheme.Muted, BackColor = Color.Transparent });
                    var value = new Label { Dock = DockStyle.Bottom, Height = 30, Font = new Font("Segoe UI Semibold", 15), ForeColor = HudTheme.Accent, BackColor = Color.Transparent, AutoEllipsis = true };
                    card.Controls.Add(value); liveMetrics.Add(value); metrics.Controls.Add(card);
                }
                cycleWarnings = new Label { Dock = DockStyle.Top, ForeColor = HudTheme.Gold, Height = 30, Visible = false, Padding = new Padding(0, 0, 0, 8) };
                calloutPreview = new Label { Dock = DockStyle.Top, ForeColor = HudTheme.Muted, Height = 38, AutoEllipsis = true, Padding = new Padding(0, 4, 0, 8) };
                panel.Controls.Add(feed); panel.Controls.Add(calloutPreview); panel.Controls.Add(metrics); panel.Controls.Add(cycleWarnings);
                void FitLiveControls()
                {
                    bool compact = panel.ClientSize.Height < 400;
                    feed.Toolbar.AutoSize = !compact; feed.Toolbar.WrapContents = !compact; feed.Toolbar.AutoScroll = compact;
                    if (compact) feed.Toolbar.Height = 64;
                    metrics.Height = compact ? 80 : 104; metrics.Padding = new Padding(0, 0, 0, compact ? 10 : 18);
                    calloutPreview.Height = compact ? 28 : 38;
                    cycleWarnings.MaximumSize = new Size(0, compact ? 28 : 0);
                    var hint = feed.Controls.OfType<Label>().FirstOrDefault(x => x.Dock == DockStyle.Top);
                    if (hint != null) hint.Height = compact ? 28 : 38;
                    int remaining = panel.ClientSize.Height - feed.Toolbar.Height - calloutPreview.Height - (cycleWarnings.Visible ? cycleWarnings.Height : 0);
                    int recordHeight = feed.Grid.ColumnHeadersHeight + feed.Grid.RowTemplate.Height
                        + feed.Controls.OfType<Label>().Where(x => x.Dock == DockStyle.Bottom).Sum(x => x.Height) + 8;
                    bool showHint = remaining >= recordHeight + (hint?.Height ?? 0);
                    if (hint != null) hint.Visible = showHint;
                    if (remaining < recordHeight + (hint?.Height ?? 0)) calloutPreview.Height = 24;
                    // Preserve readable records when optional quick texts, warnings, or widget rows consume the space.
                    metrics.Visible = !compact && panel.ClientSize.Height - feed.Toolbar.Height - calloutPreview.Height
                        - (cycleWarnings.Visible ? cycleWarnings.Height : 0) >= metrics.Height + recordHeight + (showHint ? hint?.Height ?? 0 : 0);
                }
                panel.SizeChanged += (_, __) => FitLiveControls(); feed.Toolbar.SizeChanged += (_, __) => FitLiveControls();
                cycleWarnings.VisibleChanged += (_, __) => FitLiveControls();
                lastNoticeCheck = default; return panel;
            case "combat":
                var combat = MakeTable(key, "Observed encounters are retained between sessions. Open a fight for team damage and your performance.", "When", "Encounter", "Dungeon", "Duration", "Damage", "DPS", "Result");
                HudGameSprites.Attach(combat.Grid, "Encounter", value => value is HudFightRecord fight ? HudGameSprites.Object(fight.ObjectType) ?? HudGameSprites.Monster(fight.Enemy) : null, "combat");
                HudGameSprites.Attach(combat.Grid, "Dungeon", value => HudGameSprites.Portal((value as HudFightRecord)?.Dungeon), "live");
                var combatGrouping = new HudComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 155, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
                combatGrouping.Items.AddRange(new object[] { "Encounters", "Dungeon runs" }); combatGrouping.SelectedIndex = HudPreferences.Current.CombatByDungeon ? 1 : 0;
                combatGrouping.SelectedIndexChanged += (_, __) => { HudPreferences.Current.CombatByDungeon = combatGrouping.SelectedIndex == 1; HudPreferences.Current.Save(); lastJournalRevision = -1; RefreshPageData(); };
                combat.Toolbar.Controls.Add(combatGrouping);
                combat.RowOpened += value => ShowFight((HudFightRecord)value); return combat;
            case "performance":
                combatTracker = new HudCombatTrackerPage();
                combatTracker.HistoryRequested += () => SelectPage("combat");
                combatTracker.DetailRequested += id => {
                    var fight = HudJournal.SnapshotForRun(CurrentLiveSnapshot.DungeonStartTime).FirstOrDefault(x => x.Id == id);
                    if (fight != null) ShowFight(fight);
                    else footer.Text = "This encounter has no saved detail record. Enable combat recording in Settings to retain details.";
                };
                return combatTracker;
            case "loot":
                var loot = MakeTable(key, "Each row shows one recorded bag. Hover an item for its own rarity, stats and enchantments; open the row for every item in the bag.", "When", "Item", "Rarity", "Source", "Dungeon", "Character", "Bag");
                ItemColumn(loot, "Item", LootBagItems);
                loot.Grid.Columns["Item"].HeaderText = "ITEMS / RARITY";
                loot.Grid.Columns["Item"].MinimumWidth = 240;
                loot.Grid.Columns["Item"].FillWeight = 320;
                loot.Grid.Columns["Rarity"].Visible = false;
                loot.RowOpened += value => ShowLootBag((HudLootBagHistory)value); return loot;
            case "trophies":
                var trophies = MakeTable(key, "Discoveries from your recorded drops, grouped by dungeon. Open a collection to see obtained and missing items.", "Dungeon / biome", "Discovered", "Catalog", "Collection", "Recorded runs");
                HudGameSprites.Attach(trophies.Grid, "Dungeon / biome", value => HudGameSprites.Portal(value as string), "live");
                trophies.RowOpened += value => ShowCollection((string)value); return trophies;
            case "chat":
                var chat = MakeTable(key, "Search conversations by player or message. Messages are stored locally while chat recording is enabled.", "When", "Channel", "From", "To", "Message", "Location");
                chat.Toolbar.Controls.Add(HudTheme.Button("Copy reply", (_, __) => {
                    if (chat.Grid.CurrentRow?.Tag is HudLogEntry entry && !string.IsNullOrWhiteSpace(entry.Sender)) CopyText("/tell " + entry.Sender.TrimStart('#') + " ");
                }));
                chat.RowOpened += value => { var entry = (HudLogEntry)value; chat.SearchBox.Text = entry.Sender ?? ""; }; return chat;
            case "players":
                var players = MakeTable(key, "Inspect observed players and their equipped items, enchantments, guild, and maxed stats.", "Player", "Class", "HP", "Maxed", "Stars", "Guild", "Equipment");
                ItemColumn(players, "Equipment", PlayerItems);
                HudGameSprites.Attach(players.Grid, "Player", PlayerPortrait, "players");
                players.RowOpened += value => ShowPlayer((string)value); return players;
            case "abilities":
                var abilities = MakeTable(key, "Observed abilities and crowd control. Hover identified items for details; search by item or player name.", "When", "Player", "Ability", "Item", "Details");
                ItemColumn(abilities, "Item", AbilityItems, singleItem: true); return abilities;
            case "party":
                var party = MakeTable(key, "Track your party's presence, health, and gear. Utility buttons copy commands for you to use in the game.", "Player", "Class", "Health", "Maxed", "Presence", "Watchlist", "Equipment");
                ItemColumn(party, "Equipment", PlayerItems);
                HudGameSprites.Attach(party.Grid, "Player", value => PlayerPortrait(value) ?? HudGameSprites.Class(CurrentLiveSnapshot.PartyMembers.FirstOrDefault(x => x.Name == value as string)?.ClassName), "party");
                foreach (string command in new[] { "/tell ", "/pkick ", "/ptransfer " })
                {
                    string prefix = command;
                    party.Toolbar.Controls.Add(HudTheme.Button(prefix.Trim(), (_, __) => { if (party.Grid.CurrentRow?.Tag is string name) CopyText(prefix + name + " "); }));
                }
                party.Toolbar.Controls.Add(HudTheme.Button("Watch / unwatch", (_, __) => {
                    if (party.Grid.CurrentRow?.Tag is string name) { if (!watchlist.Remove(name)) watchlist.Add(name); HudStorage.Save("party-watchlist.json", watchlist.ToList()); lastJournalRevision = -1; RefreshPageData(); }
                }));
                party.Toolbar.Controls.Add(HudTheme.Button("Ban / unban", (_, __) => {
                    if (party.Grid.CurrentRow?.Tag is string name) { if (!banned.Remove(name)) banned.Add(name); HudStorage.Save("party-banned.json", banned.ToList()); lastJournalRevision = -1; lastNoticeCheck = default; RefreshPageData(); }
                }));
                party.RowOpened += value => ShowPlayer((string)value); return party;
            case "moonlight":
                var mv = new Panel { BackColor = HudTheme.Background };
                moonlightView = new DpsOverlayForm(true) { Dock = DockStyle.Left, Width = 380 };
                moonlightView.SelectTab(3); moonlightView.UpdateMoonlightVillage(moonlight); moonlightView.UpdateData(snapshot);
                var guide = new Label { Dock = DockStyle.Fill, ForeColor = HudTheme.Muted, Padding = new Padding(30),
                    Text = "MOONLIGHT VILLAGE\n\nFollow the active dancer, phase, lanterns, and flame totals.\n\nThe tracker follows village dialogue and Umi transitions. Use the phase and flame controls when a manual correction is needed.\n\nYour tracker remains visible here when the game is minimized." };
                mv.Controls.Add(guide); mv.Controls.Add(moonlightView); moonlightView.Show(); return mv;
            case "calculator": calculator = new HudCalculatorPage(); return calculator;
            case "settings": return CreateSettingsPage();
            default: throw new ArgumentOutOfRangeException(nameof(key));
        }
    }
    private HudTablePage MakeTable(string key, string hint, params string[] columns)
    {
        var page = new HudTablePage(hint, columns); page.BindPreferences(key); tables[key] = page; return page;
    }
    private void RefreshPageData()
    {
        var data = HudJournal.SnapshotForPage(SelectedPage);
        if (SelectedPage == "live" && tables.TryGetValue("live", out var feed))
        {
            feed.SetRows(data.Feed.OrderByDescending(x => x.Time.ToUniversalTime()).Where(x => !HudPreferences.Current.HiddenFeedCategories.Contains(x.Category)
                && (x.Category != "Key pop" || !HudPreferences.Current.HiddenKeyPopTiers.Contains(x.KeyPopTier))
                && (x.Category != "Loot" || HudLootPolicy.ShowInFeed(x))).Select(x => new HudTableRow(x, x.Time.ToLocalTime().ToString("HH:mm:ss"), x.Category, x.Sender, x.Text, x.Location) { StableKey = x.Id, Category = x.Category, Time = x.Time.ToLocalTime() }));
        }
        if (SelectedPage == "combat" && tables.TryGetValue("combat", out var combat))
            combat.SetRows((HudPreferences.Current.CombatByDungeon ? DungeonHistory(data.Fights) : data.Fights).OrderByDescending(x => x.Time.ToUniversalTime()).Select(x => new HudTableRow(x, x.Time.ToLocalTime().ToString("g"), x.Enemy, x.Dungeon, TimeSpan.FromSeconds(x.Seconds).ToString(@"hh\:mm\:ss"), x.Damage.ToString("N0"), (x.Damage / Math.Max(.5, x.Seconds)).ToString("N0"), x.Defeated ? "Defeated" : "Observed") { StableKey = x.Id, Time = x.Time.ToLocalTime(), Category = HudPreferences.Current.CombatByDungeon ? x.Dungeon : x.IsBoss ? "Bosses" : "Other enemies", Search = x.Enemy + " " + x.Dungeon + " " + x.Character + " " + x.Time.ToLocalTime().ToString("yyyy-MM-dd"), Tooltip = x.EstimateNote }));
        if (SelectedPage == "loot" && tables.TryGetValue("loot", out var loot))
            loot.SetRows(LootHistoryRows(data.Loot));
        if (SelectedPage == "chat" && tables.TryGetValue("chat", out var chat))
            chat.SetRows(data.Chat.OrderByDescending(x => x.Time.ToUniversalTime()).Select(x => new HudTableRow(x, x.Time.ToLocalTime().ToString("g"), x.Category, x.Sender, x.Recipient, x.Text, x.Location) { StableKey = x.Id, Time = x.Time.ToLocalTime(), Category = x.Category }));
        if (SelectedPage == "trophies" && tables.TryGetValue("trophies", out var trophies))
        {
            var owned = new HashSet<int>(data.Trophies.Select(x => x.ItemId));
            trophies.SetRows(HudCatalog.Dungeons.OrderBy(x => x.Key).Select(x => {
                int obtained = x.Value.Count(owned.Contains);
                data.DungeonRuns.TryGetValue(x.Key, out long runs);
                return new HudTableRow(x.Key, x.Key, obtained, x.Value.Count, x.Value.Count == 0 ? "—" : (100.0 * obtained / x.Value.Count).ToString("0") + "%", runs) { Category = obtained == 0 ? "Undiscovered" : obtained == x.Value.Count ? "Complete" : "In progress" };
            }));
        }
        RefreshLivePageData();
    }
    private void RefreshLivePageData()
    {
        if (SelectedPage == "live" && liveMetrics.Count == 4)
        {
            var live = CurrentLiveSnapshot;
            liveMetrics[0].Text = live.ActiveEnemy?.Name ?? "Waiting for combat";
            liveMetrics[1].Text = live.TotalGroupDps.ToString("N0") + " /s";
            liveMetrics[2].Text = live.TotalDungeonDamage.ToString("N0");
            liveMetrics[3].Text = live.Players.Count + "  /  " + live.KillsCount;
        }
        if (SelectedPage == "performance" && combatTracker != null)
            combatTracker.SetLive(CurrentLiveSnapshot, HudJournal.ScopeId, HudJournal.SnapshotForRun(CurrentLiveSnapshot.DungeonStartTime));
        if (SelectedPage == "players" && tables.TryGetValue("players", out var players))
            players.SetRows(CurrentLiveSnapshot.Players.Select(x => new HudTableRow(x.Name, x.Name, x.ClassName, x.CurrentHp + " / " + x.MaxHp, x.MaxedStats, x.Stars, x.GuildName, string.Join(", ", x.EquipmentNames)) { Category = x.ClassName, Search = x.Name + " " + x.ClassName + " " + x.GuildName + " " + string.Join(" ", x.EquipmentNames), Tooltip = EquipmentTooltip(x) }));
        if (SelectedPage == "abilities" && tables.TryGetValue("abilities", out var abilities))
            abilities.SetRows(CurrentLiveSnapshot.AbilityLog.Select(x => new HudTableRow(x, x.TimeString, x.PlayerName, x.AbilityType, x.AbilityItemName, x.Details) { StableKey = x.Timestamp.Ticks + ":" + x.PlayerName + x.AbilityType, Time = x.Timestamp.ToLocalTime(), Category = x.AbilityType }));
        if (SelectedPage == "party" && tables.TryGetValue("party", out var party))
            party.SetRows(CurrentLiveSnapshot.PartyMembers.Select(x => {
                var player = CurrentLiveSnapshot.Players.FirstOrDefault(p => p.Name == x.Name);
                return new HudTableRow(x.Name, x.Name, x.ClassName, x.MaxHp > 0 ? x.CurrentHp + " / " + x.MaxHp : "—", x.MaxedCount < 0 ? "—" : x.MaxedCount + "/8", x.IsInDungeon ? "In dungeon" : x.Status, banned.Contains(x.Name) ? watchlist.Contains(x.Name) ? "Watched / Banned" : "Banned" : watchlist.Contains(x.Name) ? "Watched" : "", string.Join(", ", player?.EquipmentNames ?? Array.Empty<string>())) { Category = x.IsInDungeon ? "Present" : "Missing", Tooltip = EquipmentTooltip(player) };
            }));
        if (SelectedPage == "moonlight" && moonlightView != null) moonlightView.UpdateData(snapshot);
        if (SelectedPage == "calculator") calculator?.SetLive(CurrentLiveSnapshot.LocalPlayer);
    }
    private void CopyCallout()
    {
        var entry = tables["live"].Grid.CurrentRow?.Tag as HudLogEntry;
        var result = BuildCallout(entry ?? HudJournal.CurrentLocation(), entry != null);
        if (result.CanCopy) CopyText(result.Text);
        else footer.Text = "Callout unavailable: " + result.Details;
        RefreshCalloutPreview();
    }
    private void OpenCalloutSettings()
    {
        SelectPage("settings");
        if (pages["settings"] is FlowLayoutPanel panel)
        {
            var target = panel.Controls.Find("callout_dungeon_name", true).FirstOrDefault();
            if (target != null) panel.ScrollControlIntoView(target);
        }
    }
    private static HudCalloutResult BuildCallout(HudLogEntry entry, bool includeMessage)
    {
        var settings = HudPreferences.Current;
        string kind = string.IsNullOrEmpty(entry.CalloutKind) ? "Message" : entry.CalloutKind;
        if (kind == "KeyPop") kind = "Message";
        var context = new HudCalloutContext { Kind = kind, Name = kind == "Message" ? includeMessage ? entry.Text : "" : entry.CalloutName ?? entry.Location,
            Location = entry.Location, Server = entry.Server, Realm = entry.Realm, DungeonModifiers = entry.DungeonModifiers,
            OriginRealmPercent = entry.OriginRealmPercent, OpenedInNexus = entry.OpenedInNexus, LearnedEventModifiers = entry.LearnedEventModifiers,
            Available = !entry.AvailableUntilUtc.HasValue || DateTime.UtcNow < entry.AvailableUntilUtc.Value.ToUniversalTime() };
        return HudCallouts.Build(context, settings.Callouts, settings.PartyCallouts, settings.CalloutServer, settings.CalloutRealm);
    }
    private void RefreshCalloutPreview()
    {
        if (calloutPreview == null || !tables.TryGetValue("live", out var table)) return;
        var selected = table.Grid.CurrentRow?.Tag as HudLogEntry;
        var entry = selected ?? HudJournal.CurrentLocation(); var result = BuildCallout(entry, selected != null);
        bool unknown = entry.CalloutKind == "Dungeon" && !result.LootBonus.HasValue;
        calloutPreview.Text = result.CanCopy ? "Callout: " + result.Text + (unknown ? " · bonuses unknown" : "") : "Callout unavailable: " + result.Details;
        calloutPreview.ForeColor = unknown ? HudTheme.Gold : HudTheme.Muted;
        tooltips.SetToolTip(calloutPreview, result.Text + Environment.NewLine + result.Details);
        copyRoster.Enabled = HudCallouts.ParseRoster(selected?.Text)?.Length > 0;
        copyThanks.Enabled = !string.IsNullOrEmpty(ThanksFor(selected));
        RefreshQuickTextButtons(table);
    }
    private string ThanksFor(HudLogEntry entry)
    {
        if (entry == null) return "";
        var settings = HudPreferences.Current.Callouts; string local = CurrentLiveSnapshot.LocalPlayer.Name;
        if (entry.CalloutKind == "KeyPop") return HudCallouts.KeyThanks(entry.CalloutName, local, settings);
        var unlock = HudCallouts.ParseAreaUnlock(entry.Text);
        return unlock == null ? "" : HudCallouts.AreaThanks(new[] { unlock }, local, settings);
    }
    private void RefreshQuickTextButtons(HudTablePage table)
    {
        var values = HudPreferences.Current.Callouts.QuickTexts.Where(q => !string.IsNullOrWhiteSpace(q.Text)).Take(8).ToArray();
        string signature = string.Join("\n", values.Select(q => q.Name + "\t" + q.Text));
        if (signature == quickTextSignature) return;
        foreach (var button in quickTextButtons) { table.Toolbar.Controls.Remove(button); button.Dispose(); }
        quickTextButtons.Clear(); quickTextSignature = signature;
        foreach (var value in values)
        {
            string text = value.Text;
            var button = HudTheme.Button(string.IsNullOrWhiteSpace(value.Name) ? "Quick text" : value.Name, (_, __) => CopyText(text));
            button.Name = "callout_quick_" + quickTextButtons.Count; tooltips.SetToolTip(button, text);
            quickTextButtons.Add(button); table.Toolbar.Controls.Add(button);
        }
    }
    private void RefreshAutomaticCallout()
    {
        var settings = HudPreferences.Current.Callouts;
        string account = AccountWorkspace.AccountKey, location = HudJournal.CurrentLocationId;
        DateTime now = DateTime.UtcNow;
        if (autoCalloutText != null && (now >= autoCalloutExpiresUtc || autoCalloutLocation != location || autoCalloutAccount != account || !settings.AutoCopyDungeons))
            ClearAutomaticCallout();
        if (!settings.AutoCopyDungeons) { autoCalloutEnabled = false; return; }
        if (!autoCalloutEnabled || autoCalloutAccount != account)
        {
            autoCalloutArmedUtc = now;
            autoCalloutEnabled = true; autoCalloutAccount = account; copiedCalloutIds.Clear(); return;
        }
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(location)) return;
        if (!HudCalloutModifiers.LoadDefaultAsync().IsCompleted) return;
        foreach (var entry in HudJournal.SnapshotForPage("live").Feed.Where(e => e.CalloutKind == "Dungeon" && e.LocationId == location
            && e.Time.ToUniversalTime() >= autoCalloutArmedUtc && !copiedCalloutIds.Contains(e.Id)).OrderBy(e => e.Time.ToUniversalTime()))
        {
            DateTime expiry = entry.Time.ToUniversalTime().AddSeconds(settings.ExpirySeconds);
            if (entry.AvailableUntilUtc.HasValue && entry.AvailableUntilUtc.Value.ToUniversalTime() < expiry) expiry = entry.AvailableUntilUtc.Value.ToUniversalTime();
            var result = BuildCallout(entry, true);
            if (expiry <= now || !result.CanCopy) { copiedCalloutIds.Add(entry.Id); continue; }
            try
            {
                Clipboard.SetText(result.Text); copiedCalloutIds.Add(entry.Id);
                autoCalloutText = result.Text; autoCalloutExpiresUtc = expiry; autoCalloutLocation = location;
            }
            catch (System.Runtime.InteropServices.ExternalException) { break; }
        }
    }
    private void ClearAutomaticCallout()
    {
        if (autoCalloutText == null) return;
        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText() == autoCalloutText) Clipboard.Clear();
            autoCalloutText = null;
        }
        catch (System.Runtime.InteropServices.ExternalException) { }
    }
    private void CopyText(string text)
    {
        try { if (!string.IsNullOrEmpty(text)) { Clipboard.SetText(text); autoCalloutText = null; footer.Text = "Copied to clipboard"; } }
        catch (System.Runtime.InteropServices.ExternalException) { footer.Text = "Clipboard is busy. Try again."; }
    }
    private Form DetailWindow(string caption, Control body, Size? size = null)
    {
        var form = new Form { Text = caption, BackColor = HudTheme.Background, ForeColor = HudTheme.Text, Font = HudTheme.BodyFont,
            StartPosition = FormStartPosition.CenterParent, Size = size ?? new Size(920, 650), MinimumSize = new Size(650, 420), Padding = new Padding(18) };
        form.HandleCreated += (_, __) => HudWindowChrome.Apply(form);
        body.Dock = DockStyle.Fill; form.Controls.Add(body); form.Show(this); return form;
    }
    private void ShowFight(HudFightRecord fight)
    {
        var team = new HudCombatTrackerPage();
        var records = fight.Id != null && fight.Id.StartsWith("run:", StringComparison.Ordinal)
            ? HudJournal.SnapshotForPage("combat").Fights.Where(x => x.Id.StartsWith(fight.Id.Substring(4) + ":", StringComparison.Ordinal)).ToArray()
            : new[] { fight };
        team.SetHistory(records, fight.Dungeon);
        team.HistoryRequested += () => SelectPage("combat");
        team.DetailRequested += id => { var entry = records.FirstOrDefault(x => x.Id == id); if (entry != null) ShowFightSources(entry); };
        ShowFightTabs(fight, team);
    }
    private void ShowFightSources(HudFightRecord fight) => ShowFightTabs(fight, null);
    private void ShowFightTabs(HudFightRecord fight, HudCombatTrackerPage team)
    {
        var tabs = new TabControl();
        var sources = new HudTablePage("Recorded projectile contributions and their observed stats. Unconfirmed amounts remain estimates. " + fight.EstimateNote,
            "Source", "Kind", "Damage", "Confirmed", "ATT", "DEX", "VIT", "WIS", "Bonus");
        sources.SetRows(fight.Sources.Select(x => new HudTableRow(x, ItemSpriteManager.GetItemMetadata(x.ItemId).Name, x.Kind, x.Damage.ToString("N0"), x.ConfirmedDamage.ToString("N0"), x.Attack, x.Dexterity, x.Vitality, x.Wisdom, x.EventMultiplier.ToString("0.###") + "×") { Tooltip = "Observed effects: " + EffectNames(x.Effects, x.Effects2) }));
        ItemColumn(sources, "Source", value => value is HudDamageSource source ? new[] { HudItemPresentation.Create(source.ItemId,
            details: source.Kind + "\nObserved effects: " + EffectNames(source.Effects, source.Effects2)) } : Array.Empty<HudItemView>(), singleItem: true);
        var equipment = new HudTablePage(fight.Character + " · " + fight.Dungeon + " · " + fight.Time.ToLocalTime().ToString("g"), "Slot", "Equipment", "Rarity");
        equipment.SetRows((fight.Equipment ?? Array.Empty<int>()).Select((x, i) => new HudTableRow(x, new[] { "Weapon", "Ability", "Armor", "Ring" }[Math.Min(i, 3)], ItemSpriteManager.GetItemMetadata(x).Name, ItemSpriteManager.GetItemMetadata(x).Rarity)));
        ItemColumn(equipment, "Equipment", value => new[] { HudItemPresentation.Create((int)value, details: "Recorded equipment · " + fight.Character) }, showNames: true);
        foreach (var item in new[] { Tuple.Create("Team overview", (Control)team), Tuple.Create("My performance", (Control)sources), Tuple.Create("Equipment", (Control)equipment) }.Where(x => x.Item2 != null))
        { var tab = new TabPage(item.Item1) { BackColor = HudTheme.Background, Padding = new Padding(12) }; tab.Controls.Add(item.Item2); tabs.TabPages.Add(tab); }
        DetailWindow(fight.Enemy + " · " + fight.Damage.ToString("N0") + " damage", tabs, new Size(1160, 760));
    }
    internal static IEnumerable<HudFightRecord> DungeonHistory(IEnumerable<HudFightRecord> fights)
    {
        foreach (var run in fights.GroupBy(x => x.Id.Split(':')[0]))
        {
            var ordered = run.OrderBy(x => x.Time.ToUniversalTime()).ToArray(); var first = ordered[0];
            double seconds = run.Max(x => x.DungeonSeconds);
            if (seconds <= 0) seconds = (run.Max(x => x.Time.ToUniversalTime().AddSeconds(x.Seconds)) - first.Time.ToUniversalTime()).TotalSeconds;
            long damage = run.Sum(x => x.Damage);
            var players = ordered.SelectMany(x => x.Players).GroupBy(x => x.Name ?? "Unknown").Select(group => new PlayerDamageEntry {
                Name = group.Key, ClassName = group.Last().ClassName, Damage = group.Sum(x => x.Damage),
                Hits = group.All(x => x.Hits.HasValue) ? (int?)group.Sum(x => x.Hits.Value) : null,
                ClassType = group.Last().ClassType, SkinType = group.Last().SkinType, EquipmentIds = (int[])(group.Last().EquipmentIds ?? Array.Empty<int>()).Clone(),
                Dps = group.Sum(x => x.Damage) / Math.Max(.5, seconds), Percentage = 100.0 * group.Sum(x => x.Damage) / Math.Max(1, damage),
                IsLocalPlayer = group.Any(x => x.IsLocalPlayer), GuardHits = group.Sum(x => x.GuardHits), GuardDamage = group.Sum(x => x.GuardDamage),
                Status = group.Last().Status, LastHpPercent = group.Last().LastHpPercent }).OrderByDescending(x => x.Damage).ToList();
            yield return new HudFightRecord { Id = "run:" + run.Key, Time = first.Time, Dungeon = first.Dungeon, Character = first.Character,
                Enemy = run.Count() + " tracked encounters", Seconds = seconds, DungeonSeconds = seconds, Damage = damage, Players = players,
                Equipment = (int[])(first.Equipment ?? Array.Empty<int>()).Clone(), Sources = run.SelectMany(x => x.Sources).ToList(),
                DamageTaken = run.SelectMany(x => x.DamageTaken).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(y => y.Value)),
                EstimateNote = "Totals include retained encounters for this dungeon session. DPS uses the observed dungeon duration, including time between fights. Gear is from the first retained encounter; source rows preserve each fight's recorded stats." };
        }
    }
    private static string EffectNames(int primary, int secondary)
    {
        string[] first = { "Dead", "Quiet", "Weak", "Slowed", "Sick", "Dazed", "Stunned", "Blind", "Hallucinating", "Drunk", "Confused", "Stun immune", "Invisible", "Paralyzed", "Speedy", "Bleeding", "Armor break immune", "Healing", "Damaging", "Berserk", "In combat", "Stasis", "Stasis immune", "Invincible", "Invulnerable", "Armored", "Armor broken", "Hexed", "Ninja speedy", "Unstable", "Darkness" };
        var names = first.Where((x, i) => (primary & (1 << i)) != 0).ToList();
        // These secondary flags are corroborated by the combat protocol. Other
        // flags are retained in the journal without guessing a display name.
        if ((secondary & 0x40) != 0) names.Add("Cursed");
        if ((secondary & 0x20000) != 0) names.Add("Exposed");
        if ((secondary & ~0x20040) != 0 || (primary & int.MinValue) != 0) names.Add("Additional effects recorded");
        return names.Count == 0 ? "None recorded" : string.Join(", ", names);
    }
    private static IEnumerable<HudItemView> LootBagItems(object value)
        => value is HudLootBagHistory bag ? bag.Items.Select(LootItem) : Array.Empty<HudItemView>();

    private static IEnumerable<HudTableRow> LootHistoryRows(IEnumerable<HudLootRecord> records)
        => HudLootBagHistory.Group(records).Select(bag => new HudTableRow(bag, bag.Time.ToLocalTime().ToString("g"),
            string.Join(", ", bag.Items.Select(item => (item.Shiny ? "Shiny " : "") + item.Item)),
            string.Join(", ", bag.Items.Select(item => item.Rarity).Distinct()), bag.Enemy, bag.Dungeon, bag.Character, bag.Bag)
        {
            StableKey = bag.Id, Time = bag.Time.ToLocalTime(), Category = bag.Bag,
            CategoryAliases = bag.Shiny ? new[] { "Shiny" } : Array.Empty<string>(),
            Search = string.Join(" ", bag.Items.Select(item => string.Join(" ", item.Item, item.Rarity, item.Shiny ? "Shiny" : "",
                item.Bag, item.Enemy, item.Dungeon, item.Character, item.Enchantments, item.Time.ToLocalTime().ToString("yyyy-MM-dd")))),
            Tooltip = string.Join("\n", bag.Items.Select(item => item.Item + " · " + item.Rarity
                + (string.IsNullOrWhiteSpace(item.Enchantments) ? "" : " · " + item.Enchantments)))
        });

    private static HudTablePage CreateLootBagDetails(HudLootBagHistory bag)
    {
        var table = new HudTablePage(bag.Time.ToLocalTime().ToString("F") + " · " + bag.Bag + " · " + bag.Dungeon + " · " + bag.Character,
            "Item", "Rarity", "Enchantments", "Known drop locations");
        table.SetRows(bag.Items.Select(item => new HudTableRow(item, item.Item, item.Rarity, item.Enchantments, HudCatalog.Locations(item.ItemId))
        {
            StableKey = item.Id, Time = item.Time.ToLocalTime(), Category = item.Rarity,
            CategoryAliases = item.Shiny ? new[] { "Shiny" } : Array.Empty<string>(),
            Search = string.Join(" ", item.Item, item.Rarity, item.Shiny ? "Shiny" : "", item.Enchantments, item.Enemy, item.Dungeon, item.Character),
            Tooltip = item.Enchantments
        }));
        ItemColumn(table, "Item", value => value is HudLootRecord item ? new[] { LootItem(item) } : Array.Empty<HudItemView>(), showNames: true);
        return table;
    }

    private void ShowLootBag(HudLootBagHistory bag)
    {
        var table = CreateLootBagDetails(bag);
        table.RowOpened += value => ShowLoot((HudLootRecord)value);
        DetailWindow(bag.Bag + " · " + bag.Items.Length + (bag.Items.Length == 1 ? " item" : " items"), table, new Size(920, 500));
    }

    private void ShowLoot(HudLootRecord item)
    {
        var table = new HudTablePage("Recorded drop details", "Property", "Value");
        table.SetRows(new[] {
            new HudTableRow("item", "Item", item.Item), new HudTableRow("when", "When", item.Time.ToLocalTime().ToString("F")), new HudTableRow("source", "Source", item.Enemy),
            new HudTableRow("dungeon", "Dungeon", item.Dungeon), new HudTableRow("character", "Character", item.Character),
            new HudTableRow("enchant", "Enchantments", item.Enchantments), new HudTableRow("locations", "Known drop locations", HudCatalog.Locations(item.ItemId)) });
        ItemColumn(table, "Value", value => Equals(value, "item") ? new[] { LootItem(item) } : Array.Empty<HudItemView>(), showNames: true);
        DetailWindow(item.Item, table, new Size(760, 500));
    }
    private void ShowCollection(string dungeon)
    {
        var data = HudJournal.SnapshotForPage("trophies");
        var page = new HudTablePage(dungeon + " · Recorded discoveries and future drops from the RealmEye catalog.", "Item", "Rarity", "Drops", "First discovery", "Latest discovery", "Status");
        page.SetRows(HudCatalog.Dungeons[dungeon].Select(id => {
            var trophy = data.Trophies.FirstOrDefault(x => x.ItemId == id); var meta = ItemSpriteManager.GetItemMetadata(id);
            return new HudTableRow(id, meta.Name, meta.Rarity, trophy?.Count ?? 0, trophy?.First.ToLocalTime().ToString("g") ?? "—", trophy?.Last.ToLocalTime().ToString("g") ?? "—", trophy != null ? "Discovered" : "Undiscovered") { Category = trophy != null ? "Discovered" : "Undiscovered", Tooltip = HudCatalog.Locations(id) };
        }));
        ItemColumn(page, "Item", value => new[] { HudItemPresentation.Create((int)value, source: HudCatalog.Locations((int)value),
            details: data.Trophies.Any(x => x.ItemId == (int)value) ? "Discovered in your recorded loot" : "Not discovered yet") }, singleItem: true);
        DetailWindow(dungeon + " · Trophy hall", page);
    }
    private void ShowPlayer(string name)
    {
        var player = CurrentLiveSnapshot.Players.FirstOrDefault(x => x.Name == name); if (player == null) return;
        var page = new HudTablePage(name + " · " + player.ClassName + " · " + player.MaxedStats + " · " + player.GuildName, "Slot", "Item", "Rarity", "Enchantments");
        page.SetRows(Enumerable.Range(0, 4).Select(i => new HudTableRow(i, new[] { "Weapon", "Ability", "Armor", "Ring" }[i], player.EquipmentNames[i], player.EquipmentRarities[i], string.Join(", ", player.EquipmentEnchants[i]))));
        ItemColumn(page, "Item", value => new[] { EquipmentItem(player, (int)value) }, showNames: true);
        DetailWindow(name, page, new Size(800, 440));
    }
    private Control CreateSettingsPage()
    {
        var settings = HudPreferences.Current;
        var panel = new HudFlowPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        void Heading(string text) => panel.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = HudTheme.Accent, Font = new Font("Segoe UI Semibold", 12), Margin = new Padding(0, 16, 0, 10) });
        CheckBox Toggle(string text, bool value, Action<bool> action)
        {
            var box = new HudCheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
            box.CheckedChanged += (_, __) => { action(box.Checked); settings.Save(); lastNoticeCheck = default; RefreshDashboard(); }; panel.Controls.Add(box); return box;
        }
        Heading("Appearance");
        var themes = new HudComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        themes.Items.AddRange(new object[] { "Midnight", "Slate", "Light" }); themes.SelectedItem = settings.Theme ?? "Midnight";
        void ApplyAppearance(Color[] previous)
        {
            settings.Save(); HudTheme.Apply(this, previous); foreach (Form detail in OwnedForms) HudTheme.Apply(detail, previous);
            foreach (var nav in navButtons) tooltips.SetToolTip(nav.Value, settings.TabTooltips ? nav.Value.Text : "");
            lastJournalRevision = -1; RefreshDashboard();
        }
        themes.SelectedIndexChanged += (_, __) => {
            var previous = HudTheme.Palette; settings.Theme = (string)themes.SelectedItem;
            if (settings.Theme == "Light" && (settings.AccentColor == "#00e5bf" || settings.AccentColor == "#58dec1")) settings.AccentColor = "#007765";
            else if (settings.Theme != "Light" && settings.AccentColor == "#007765") settings.AccentColor = "#00e5bf";
            ApplyAppearance(previous);
        };
        panel.Controls.Add(HudTheme.Label("Color theme", true)); panel.Controls.Add(themes);
        panel.Controls.Add(HudTheme.Button("Choose accent color", (_, __) => {
            using var picker = new ColorDialog { Color = HudTheme.Accent, FullOpen = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            var previous = HudTheme.Palette; settings.AccentColor = "#" + picker.Color.R.ToString("x2") + picker.Color.G.ToString("x2") + picker.Color.B.ToString("x2"); ApplyAppearance(previous);
        }));
        Toggle("Use compact table rows and buttons", settings.CompactControls, x => { var previous = HudTheme.Palette; settings.CompactControls = x; ApplyAppearance(previous); });
        Toggle("Show navigation tooltips", settings.TabTooltips, x => { var previous = HudTheme.Palette; settings.TabTooltips = x; ApplyAppearance(previous); });
        Heading("Workspace");
        Toggle("Show widget bar on every page", settings.WidgetBar, x => settings.WidgetBar = x);
        Toggle("Use two rows for widgets", settings.WidgetRows > 1, x => settings.WidgetRows = x ? 2 : 1);
        var mode = new HudComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        mode.Items.AddRange(new object[] { "Current", "Regular", "Seasonal" }); mode.SelectedItem = settings.WidgetStorage;
        mode.SelectedIndexChanged += (_, __) => { settings.WidgetStorage = (string)mode.SelectedItem; settings.Save(); RefreshWidgets(); };
        panel.Controls.Add(HudTheme.Label("Storage shown in currency widgets", true)); panel.Controls.Add(mode);
        Toggle("Show tracked objectives in the taskbar", settings.TaskBar, x => settings.TaskBar = x);
        Toggle("Keep the desktop dashboard on top", settings.AlwaysOnTop, x => TopMost = settings.AlwaysOnTop = x);
        panel.Controls.Add(HudTheme.Button("Move to another monitor", (_, __) => ShowMonitorMenu()));
        Heading("History and notifications");
        Toggle("Remember combat encounters", settings.RecordCombat, x => settings.RecordCombat = x);
        Heading("Combat recording groups");
        Toggle("Record solo encounters", settings.Combat.TrackSolo, x => settings.Combat.TrackSolo = x);
        foreach (var group in HudCombatPolicy.Groups)
        {
            string key = group.Key;
            Toggle(group.Value, settings.Combat.Tracks(key), x => settings.Combat.Groups[key] = x);
        }
        panel.Controls.Add(HudTheme.Label("Unclassified encounters remain recorded. Group settings affect new encounters.", true));
        Toggle("Remember loot discoveries", settings.RecordLoot, x => settings.RecordLoot = x);
        Toggle("Remember chat conversations locally", settings.RecordChat, x => settings.RecordChat = x);
        Heading("Loot recording filters");
        panel.Controls.Add(HudTheme.Label("Record selected bag types OR bags containing any enabled item type.", true));
        var bags = new CheckedListBox { CheckOnClick = true, Height = 160, Width = 280, BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, BorderStyle = BorderStyle.None };
        foreach (string bag in HudLootPolicy.BagNames) bags.Items.Add(bag, settings.Loot.AlwaysTrackBags.Contains(bag));
        bags.ItemCheck += (_, e) => {
            string bag = (string)bags.Items[e.Index];
            if (e.NewValue == CheckState.Checked) { if (!settings.Loot.AlwaysTrackBags.Contains(bag)) settings.Loot.AlwaysTrackBags.Add(bag); }
            else settings.Loot.AlwaysTrackBags.Remove(bag); settings.Save();
        }; panel.Controls.Add(bags);
        Toggle("Shiny items", settings.Loot.Shiny, x => settings.Loot.Shiny = x);
        Toggle("Any enchantment", settings.Loot.AnyEnchantment, x => settings.Loot.AnyEnchantment = x);
        Toggle("Loot Bonus III / IV enchants", settings.Loot.LootEnchantments, x => settings.Loot.LootEnchantments = x);
        Toggle("Unique enchants", settings.Loot.UniqueEnchantments, x => settings.Loot.UniqueEnchantments = x);
        Toggle("Awakened enchants", settings.Loot.AwakenedEnchantments, x => settings.Loot.AwakenedEnchantments = x);
        Toggle("Legendary / Divine enchanted items", settings.Loot.LegendaryPlus, x => settings.Loot.LegendaryPlus = x);
        Toggle("Untiered equipment", settings.Loot.Untiered, x => settings.Loot.Untiered = x);
        Toggle("Set-tier equipment (ST)", settings.Loot.SetTier, x => settings.Loot.SetTier = x);
        Toggle("Stat potions", settings.Loot.Potions, x => settings.Loot.Potions = x);
        Toggle("Dungeon marks", settings.Loot.Marks, x => settings.Loot.Marks = x);
        Toggle("Tiered equipment above the threshold", settings.Loot.Tiered, x => settings.Loot.Tiered = x);
        panel.Controls.Add(HudTheme.Label("Minimum equipment tier", true));
        var tier = new HudNumericUpDown { Minimum = 0, Maximum = 15, Value = settings.Loot.MinimumTier, Width = 160, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        tier.ValueChanged += (_, __) => { settings.Loot.MinimumTier = (int)tier.Value; settings.Save(); }; panel.Controls.Add(tier);
        AddFeedLootSettings(panel, settings);
        Heading("Notifications and callouts");
        var directMessages = Toggle("Play a sound for direct messages", settings.SoundAlerts, x => settings.SoundAlerts = x);
        directMessages.Name = "sound_direct_messages";
        AddSoundSettings(panel, settings, directMessages);
        Toggle("Show season expiry reminders", settings.SeasonWarnings, x => settings.SeasonWarnings = x);
        Toggle("Show battlepass expiry reminders", settings.BattlePassWarnings, x => settings.BattlePassWarnings = x);
        Toggle("Show the monthly login-calendar reminder", settings.CalendarWarnings, x => settings.CalendarWarnings = x);
        Toggle("Include server in copied callouts", settings.CalloutServer, x => settings.CalloutServer = x);
        Toggle("Include realm in copied callouts", settings.CalloutRealm, x => settings.CalloutRealm = x);
        Toggle("Prefix copied callouts with /p", settings.PartyCallouts, x => settings.PartyCallouts = x);
        AddCalloutSettings(panel, settings);
        panel.Controls.Add(HudTheme.Label("Live feed categories", true));
        var categories = new CheckedListBox { CheckOnClick = true, Height = 120, Width = 280, BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, BorderStyle = BorderStyle.None };
        foreach (string category in new[] { "Travel", "Game", "Event", "Whisper", "Notice", "Alert", "Reminder", "Party", "Loot", "Dust", "Key pop" }) categories.Items.Add(category, !settings.HiddenFeedCategories.Contains(category));
        categories.ItemCheck += (_, e) => {
            string category = (string)categories.Items[e.Index];
            if (e.NewValue == CheckState.Checked) settings.HiddenFeedCategories.Remove(category); else if (!settings.HiddenFeedCategories.Contains(category)) settings.HiddenFeedCategories.Add(category);
            settings.Save(); lastJournalRevision = -1;
        }; panel.Controls.Add(categories);
        panel.Controls.Add(HudTheme.Label("Public key openings shown in the feed", true));
        foreach (string name in new[] { "Rookie", "Adept", "Expert", "Exaltation" })
        {
            string tierName = name;
            Toggle(tierName + " key openings", !settings.HiddenKeyPopTiers.Contains(tierName), enabled => {
                if (enabled) settings.HiddenKeyPopTiers.Remove(tierName); else if (!settings.HiddenKeyPopTiers.Contains(tierName)) settings.HiddenKeyPopTiers.Add(tierName);
                lastJournalRevision = -1;
            });
        }
        panel.Controls.Add(HudTheme.Label("Unknown key difficulty remains visible.", true));
        Heading("Party watchlist");
        var names = new HudComboBox { Width = 280, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        names.Items.AddRange(watchlist.OrderBy(x => x).ToArray()); panel.Controls.Add(names);
        panel.Controls.Add(HudTheme.Button("Add / remove name", (_, __) => {
            string name = names.Text.Trim(); if (name.Length == 0 || name.Length > 32) return;
            if (!watchlist.Remove(name)) watchlist.Add(name); HudStorage.Save("party-watchlist.json", watchlist.ToList());
            names.Items.Clear(); names.Items.AddRange(watchlist.OrderBy(x => x).ToArray()); lastNoticeCheck = default;
        }));
        Heading("Party banned list");
        panel.Controls.Add(HudTheme.Label("Keep a local list of players to flag in your party. Moderation commands are copied for you to use.", true));
        var bannedNames = new HudComboBox { Width = 280, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        bannedNames.Items.AddRange(banned.OrderBy(x => x).ToArray()); panel.Controls.Add(bannedNames);
        panel.Controls.Add(HudTheme.Button("Add / remove banned name", (_, __) => {
            string name = bannedNames.Text.Trim(); if (name.Length == 0 || name.Length > 32) return;
            if (!banned.Remove(name)) banned.Add(name); HudStorage.Save("party-banned.json", banned.ToList());
            bannedNames.Items.Clear(); bannedNames.Items.AddRange(banned.OrderBy(x => x).ToArray()); lastNoticeCheck = default; lastJournalRevision = -1;
        }));
        panel.Controls.Add(HudTheme.Label("History records to keep per category", true));
        var limit = new HudNumericUpDown { Minimum = 100, Maximum = 50000, Increment = 1000, Value = Math.Max(100, Math.Min(50000, settings.HistoryLimit)), Width = 160, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        limit.ValueChanged += (_, __) => { settings.HistoryLimit = (int)limit.Value; settings.Save(); }; panel.Controls.Add(limit);
        Heading("Account data");
        var refresh = HudTheme.Button("Refresh account and missions");
        refresh.Click += async (_, __) => { refresh.Enabled = false; try { await AccountWorkspace.RefreshAsync(); await MissionWorkspace.RefreshAsync(); } finally { if (!refresh.IsDisposed) refresh.Enabled = true; } };
        panel.Controls.Add(refresh);
        panel.Controls.Add(HudTheme.Label("Connect in game, then refresh to sync characters, storage, and missions.\nQuest data refreshes when you visit the Tinkerer.", true));
        Heading("Controls");
        panel.Controls.Add(HudTheme.Label("F10  Switch desktop / compact HUD\nF8    Show / hide the active view\nF9    Lock / unlock the compact overlay\nDrag a widget to reorder it; right-click to choose visible widgets.", true));
        return panel;
    }

    private void AddSoundSettings(FlowLayoutPanel panel, HudPreferences preferences, CheckBox directMessages)
    {
        var settings = preferences.Sounds;
        void Label(string text) => panel.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = HudTheme.Muted, Margin = new Padding(0, 10, 0, 3) });
        Label("Notification sounds");
        Label("New sound categories are off until enabled. Choose a category to customize its volume, tone, or WAV file.");
        Label("Master volume (%)");
        var master = new HudNumericUpDown { Name = "sound_master_volume", AccessibleName = "Master volume", Minimum = 0, Maximum = 100, Value = settings.MasterVolume,
            Width = 160, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        master.ValueChanged += (_, __) => { settings.MasterVolume = (int)master.Value; preferences.Save(); }; panel.Controls.Add(master);
        Label("Sound category");
        string[] keys = HudSounds.Categories.Keys.ToArray();
        var category = new HudComboBox { Name = "sound_category", AccessibleName = "Sound category", DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 400, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        category.Items.AddRange(keys.Select(x => HudSounds.Categories[x]).ToArray()); panel.Controls.Add(category);
        var enabled = new HudCheckBox { Name = "sound_enabled", Text = "Enable this sound", AutoSize = true, Margin = new Padding(0, 6, 0, 6) }; panel.Controls.Add(enabled);
        Label("Category volume (%)");
        var volume = new HudNumericUpDown { Name = "sound_volume", AccessibleName = "Category volume", Minimum = 0, Maximum = 100,
            Width = 160, BackColor = HudTheme.Input, ForeColor = HudTheme.Text }; panel.Controls.Add(volume);
        Label("Built-in tone");
        var tone = new HudComboBox { Name = "sound_tone", AccessibleName = "Built-in tone", DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 230, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        tone.Items.AddRange(HudSounds.Tones); panel.Controls.Add(tone);
        Label("Custom WAV file (optional)");
        var file = new HudTextBox { Name = "sound_file", AccessibleName = "Custom WAV file", ReadOnly = true, Width = 550,
            BackColor = HudTheme.Input, ForeColor = HudTheme.Text }; panel.Controls.Add(file);
        bool loading = false;
        string Key() => category.SelectedIndex >= 0 ? keys[category.SelectedIndex] : "Whisper";
        HudSoundRule Rule() => settings.Rules[Key()];
        void LoadRule()
        {
            loading = true; var rule = Rule(); enabled.Checked = Key() == "Whisper" ? preferences.SoundAlerts : rule.Enabled;
            volume.Value = rule.Volume; tone.SelectedItem = rule.Tone; file.Text = rule.File; tone.Enabled = string.IsNullOrWhiteSpace(rule.File); loading = false;
        }
        category.SelectedIndexChanged += (_, __) => LoadRule();
        enabled.CheckedChanged += (_, __) => {
            if (loading) return;
            if (Key() == "Whisper") directMessages.Checked = preferences.SoundAlerts = enabled.Checked; else Rule().Enabled = enabled.Checked;
            preferences.Save();
        };
        directMessages.CheckedChanged += (_, __) => { if (Key() == "Whisper") LoadRule(); };
        volume.ValueChanged += (_, __) => { if (!loading) { Rule().Volume = (int)volume.Value; preferences.Save(); } };
        tone.SelectedIndexChanged += (_, __) => { if (!loading) { Rule().Tone = tone.Text; preferences.Save(); } };
        var buttons = new HudFlowPanel { AutoSize = true, Width = 550, Margin = Padding.Empty, WrapContents = true };
        var browse = HudTheme.Button("Choose WAV file", (_, __) => {
            using var dialog = new OpenFileDialog { Title = "Choose a notification sound", Filter = "WAV audio (*.wav)|*.wav", CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog(this) == DialogResult.OK) { Rule().File = dialog.FileName; preferences.Save(); LoadRule(); }
        }); browse.Name = "sound_browse"; buttons.Controls.Add(browse);
        var reset = HudTheme.Button("Use built-in tone", (_, __) => { Rule().File = ""; preferences.Save(); LoadRule(); }); reset.Name = "sound_reset"; buttons.Controls.Add(reset);
        var preview = HudTheme.Button("Preview sound", (_, __) => HudSounds.Preview(Key())); preview.Name = "sound_preview"; buttons.Controls.Add(preview); panel.Controls.Add(buttons);
        Label("Custom audio: uncompressed PCM WAV, mono or stereo, 8/16 bit, 8–96 kHz, at most 5 seconds and 2 MB. Volume zero mutes previews too.");
        soundStatus = new Label { Name = "sound_status", AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = HudTheme.Gold }; panel.Controls.Add(soundStatus);
        Label("Custom chat phrases (separate phrases with commas)");
        var phrases = new HudTextBox { Name = "sound_chat_phrases", AccessibleName = "Custom chat phrases", Text = settings.CustomChatWords, Width = 550, MaxLength = 1000,
            BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        phrases.Leave += (_, __) => { settings.CustomChatWords = phrases.Text.Trim(); preferences.Save(); }; panel.Controls.Add(phrases);
        category.SelectedIndex = 0;
    }

    private void AddFeedLootSettings(FlowLayoutPanel panel, HudPreferences preferences)
    {
        var settings = preferences.FeedLoot;
        panel.Controls.Add(new Label { Text = "Live feed loot filters", AutoSize = true, ForeColor = HudTheme.Accent,
            Font = new Font("Segoe UI Semibold", 12), Margin = new Padding(0, 16, 0, 10) });
        panel.Controls.Add(new Label { Text = "These filters control bag notifications in the Live feed. Saved loot history uses the recording filters above.",
            AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = HudTheme.Muted });
        void Save() { preferences.Save(); lastJournalRevision = -1; }
        void Toggle(string key, string title, bool value, Action<bool> update)
        {
            var box = new HudCheckBox { Name = "feed_loot_" + key, Text = title, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
            box.CheckedChanged += (_, __) => { update(box.Checked); Save(); }; panel.Controls.Add(box);
        }
        var bags = new CheckedListBox { Name = "feed_loot_bags", CheckOnClick = true, Height = 160, Width = 280,
            BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, BorderStyle = BorderStyle.None };
        foreach (string bag in HudLootPolicy.BagNames) bags.Items.Add(bag, settings.AlwaysTrackBags.Contains(bag));
        bags.ItemCheck += (_, e) => {
            string bag = (string)bags.Items[e.Index];
            if (e.NewValue == CheckState.Checked) { if (!settings.AlwaysTrackBags.Contains(bag)) settings.AlwaysTrackBags.Add(bag); }
            else settings.AlwaysTrackBags.Remove(bag); Save();
        }; panel.Controls.Add(bags);
        Toggle("shiny", "Shiny items", settings.Shiny, x => settings.Shiny = x);
        Toggle("enchanted", "Any enchantment", settings.AnyEnchantment, x => settings.AnyEnchantment = x);
        Toggle("loot_enchants", "Loot Bonus III / IV enchants", settings.LootEnchantments, x => settings.LootEnchantments = x);
        Toggle("unique", "Unique enchants", settings.UniqueEnchantments, x => settings.UniqueEnchantments = x);
        Toggle("awakened", "Awakened enchants", settings.AwakenedEnchantments, x => settings.AwakenedEnchantments = x);
        Toggle("legendary", "Legendary / Divine enchanted items", settings.LegendaryPlus, x => settings.LegendaryPlus = x);
        Toggle("ut", "Untiered equipment", settings.Untiered, x => settings.Untiered = x);
        Toggle("st", "Set-tier equipment (ST)", settings.SetTier, x => settings.SetTier = x);
        Toggle("potions", "Stat potions", settings.Potions, x => settings.Potions = x);
        Toggle("marks", "Dungeon marks", settings.Marks, x => settings.Marks = x);
        Toggle("tiered", "Tiered equipment above the threshold", settings.Tiered, x => settings.Tiered = x);
        panel.Controls.Add(HudTheme.Label("Minimum feed equipment tier", true));
        var tier = new HudNumericUpDown { Name = "feed_loot_minimum_tier", Minimum = 0, Maximum = 15, Value = settings.MinimumTier, Width = 160, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        tier.ValueChanged += (_, __) => { settings.MinimumTier = (int)tier.Value; Save(); }; panel.Controls.Add(tier);
    }

    private void AddCalloutSettings(FlowLayoutPanel panel, HudPreferences preferences)
    {
        var settings = preferences.Callouts;
        void Save() { settings.Normalize(); preferences.Save(); RefreshCalloutPreview(); }
        void Label(string text) => panel.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = HudTheme.Muted, Margin = new Padding(0, 10, 0, 3) });
        void Heading(string text) => panel.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = HudTheme.Accent, Font = new Font("Segoe UI Semibold", 11), Margin = new Padding(0, 18, 0, 8) });
        void Choice(string key, string title, string value, string[] choices, Action<string> update)
        {
            Label(title);
            var box = new HudComboBox { Name = "callout_" + key, AccessibleName = title, DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
            box.Items.AddRange(choices); box.SelectedItem = value;
            box.SelectedIndexChanged += (_, __) => { update(box.Text); Save(); }; panel.Controls.Add(box);
        }
        void Toggle(string key, string title, bool value, Action<bool> update)
        {
            var box = new HudCheckBox { Name = "callout_" + key, Text = title, Checked = value, AutoSize = true, MaximumSize = new Size(550, 0), Margin = new Padding(0, 6, 0, 6) };
            box.CheckedChanged += (_, __) => { update(box.Checked); Save(); }; panel.Controls.Add(box);
        }
        void Number(string key, string title, int value, int minimum, int maximum, Action<int> update)
        {
            Label(title);
            var box = new HudNumericUpDown { Name = "callout_" + key, AccessibleName = title, Minimum = minimum, Maximum = maximum,
                Value = Math.Max(minimum, Math.Min(maximum, value)), Width = 160, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
            box.ValueChanged += (_, __) => { update((int)box.Value); Save(); }; panel.Controls.Add(box);
        }
        void TextOption(string key, string title, string value, Action<string> update)
        {
            Label(title);
            var box = new HudTextBox { Name = "callout_" + key, AccessibleName = title, Text = value ?? "", Width = 360, MaxLength = 120, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
            box.Leave += (_, __) => { update(box.Text.Trim()); Save(); }; panel.Controls.Add(box);
        }
        void Overrides(string key, string title, Dictionary<string, string> values, IEnumerable<string> known)
        {
            Label(title + " custom short names");
            var row = new HudFlowPanel { AutoSize = true, Width = 550, WrapContents = true, Margin = Padding.Empty };
            var name = new HudComboBox { Name = "callout_" + key + "_override_name", AccessibleName = title + " full name", Width = 240, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
            name.Items.AddRange(known.Concat(values.Keys).Distinct().OrderBy(x => x).ToArray());
            var replacement = new HudTextBox { Name = "callout_" + key + "_replacement", AccessibleName = title + " custom short name", Width = 145, MaxLength = 80, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
            name.SelectedIndexChanged += (_, __) => replacement.Text = values.TryGetValue(name.Text, out string text) ? text : "";
            row.Controls.Add(name); row.Controls.Add(replacement);
            row.Controls.Add(HudTheme.Button("Save", (_, __) => {
                string full = name.Text.Trim(), custom = replacement.Text.Trim(); if (full.Length == 0) return;
                if (custom.Length == 0) values.Remove(full); else values[full] = custom;
                if (!name.Items.Contains(full)) name.Items.Add(full); Save();
            }));
            panel.Controls.Add(row); Label("Choose a full name or type one. An empty custom name restores its default abbreviation.");
        }
        Heading("Dungeon and event callout format");
        Choice("dungeon_name", "Dungeon name (Short uses your custom names)", settings.DungeonNameStyle, new[] { "Short", "Full", "None" }, x => settings.DungeonNameStyle = x);
        Choice("event_name", "Event name (Short uses your custom names)", settings.EventNameStyle, new[] { "Short", "Full", "None" }, x => settings.EventNameStyle = x);
        Overrides("dungeon", "Dungeon", settings.DungeonNameOverrides, HudCalloutNames.Dungeons.Keys);
        Overrides("event", "Event", settings.EventNameOverrides, HudCalloutNames.Events.Keys);
        Choice("dungeon_join", "Dungeon join marker (j)", settings.DungeonJoin, new[] { "Beginning", "End", "None" }, x => settings.DungeonJoin = x);
        Choice("event_join", "Event join marker (j)", settings.EventJoin, new[] { "Beginning", "End", "None" }, x => settings.EventJoin = x);
        Choice("realm_status", "Captured origin realm status", settings.RealmStatus, new[] { "None", "Closing", "Score" }, x => settings.RealmStatus = x);
        Number("realm_threshold", "Minimum realm score percent to include", settings.RealmThreshold, 0, 100, x => settings.RealmThreshold = x);
        Choice("nexus_marker", "Nexus origin marker", settings.NexusMarker, new[] { "None", "Nex", "InNexus" }, x => settings.NexusMarker = x);
        Heading("Dungeon bonuses and reward tags");
        Choice("loot_label", "Loot bonus label", settings.LootLabel, new[] { "lb", "loot", "None" }, x => settings.LootLabel = x);
        Number("loot_threshold", "Minimum loot bonus percent", settings.LootThreshold, 0, 10000, x => settings.LootThreshold = x);
        Choice("dust_label", "Dust bonus label", settings.DustLabel, new[] { "db", "dust", "None" }, x => settings.DustLabel = x);
        Number("dust_threshold", "Minimum dust bonus percent", settings.DustThreshold, 0, 10000, x => settings.DustThreshold = x);
        Choice("xp_label", "XP bonus label", settings.XpLabel, new[] { "xp", "None" }, x => settings.XpLabel = x);
        Number("xp_threshold", "Minimum XP bonus percent", settings.XpThreshold, 0, 10000, x => settings.XpThreshold = x);
        Toggle("percent", "Include the percent sign in bonus tags", settings.Percent, x => settings.Percent = x);
        Toggle("event_modifiers", "Include modifiers supplied by dungeon events", settings.CallEventModifiers, x => settings.CallEventModifiers = x);
        Toggle("learn_event_modifiers", "Learn recurring event modifiers from observed runs", settings.LearnEventModifiers, x => settings.LearnEventModifiers = x);
        Number("learn_event_runs", "Observed runs required for a recurring modifier", settings.LearnEventRuns, 2, 10, x => settings.LearnEventRuns = x);
        Label("Reward modifiers: enable a tag and edit the text copied for it.");
        var tags = new HudDataGridView { Name = "callout_reward_tags", Width = 550, Height = 250, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, BackgroundColor = HudTheme.Surface, BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = HudTheme.Raised, ForeColor = HudTheme.Text },
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, SelectionBackColor = HudTheme.Raised, SelectionForeColor = HudTheme.Accent } };
        tags.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 14 });
        tags.Columns.Add(new DataGridViewTextBoxColumn { Name = "Modifier", HeaderText = "Modifier", ReadOnly = true, FillWeight = 56 });
        tags.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tag", HeaderText = "Clipboard tag", FillWeight = 30, MaxInputLength = 60 });
        foreach (var tag in settings.RewardTags) { int row = tags.Rows.Add(tag.Enabled, tag.Name, tag.Short); tags.Rows[row].Tag = tag; }
        tags.CurrentCellDirtyStateChanged += (_, __) => { if (tags.IsCurrentCellDirty) tags.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        tags.CellValueChanged += (_, e) => {
            if (e.RowIndex < 0 || !(tags.Rows[e.RowIndex].Tag is HudCalloutTag tag)) return;
            tag.Enabled = Convert.ToBoolean(tags.Rows[e.RowIndex].Cells[0].Value); tag.Short = Convert.ToString(tags.Rows[e.RowIndex].Cells[2].Value)?.Trim() ?? ""; Save();
        }; panel.Controls.Add(tags);
        Label("If modifier definitions are unavailable or unknown, the callout preview says so; bonuses are not guessed.");
        Heading("Event hints");
        Toggle("event_upcoming", "Include the next event in a known chain", settings.EventAddUpcoming, x => settings.EventAddUpcoming = x);
        TextOption("alien_adept", "Alien adept-wave prefix", settings.AlienAdeptPrefix, x => settings.AlienAdeptPrefix = x);
        TextOption("alien_veteran", "Alien veteran-wave prefix", settings.AlienVeteranPrefix, x => settings.AlienVeteranPrefix = x);
        Toggle("alien_wave4", "Include the boss hint on alien wave four", settings.AlienWave4Boss, x => settings.AlienWave4Boss = x);
        Heading("Automatic dungeon clipboard");
        Toggle("auto_copy", "Automatically copy newly observed dungeon callouts", settings.AutoCopyDungeons, x => { settings.AutoCopyDungeons = x; autoCalloutEnabled = false; autoCalloutArmedUtc = DateTime.UtcNow; });
        Number("expiry", "Clipboard lifetime in seconds (never extends a known join deadline)", settings.ExpirySeconds, 1, 30, x => settings.ExpirySeconds = x);
        Label("Only new observations in your current location are copied. On expiry or a location change, the clipboard is cleared only if it still contains that automatic callout.");
        Heading("Quick clipboard text");
        Label("Add up to eight buttons to the Live feed. Each button copies its text for you to paste in game.");
        var quick = new HudDataGridView { Name = "callout_quick_texts", Width = 550, Height = 220, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, BackgroundColor = HudTheme.Surface, BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = HudTheme.Raised, ForeColor = HudTheme.Text },
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, SelectionBackColor = HudTheme.Raised, SelectionForeColor = HudTheme.Accent } };
        quick.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Button name", FillWeight = 30, MaxInputLength = 32 });
        quick.Columns.Add(new DataGridViewTextBoxColumn { Name = "Text", HeaderText = "Text to copy", FillWeight = 70, MaxInputLength = 250 });
        for (int i = 0; i < 8; i++) quick.Rows.Add(settings.QuickTexts.ElementAtOrDefault(i)?.Name ?? "", settings.QuickTexts.ElementAtOrDefault(i)?.Text ?? "");
        panel.Controls.Add(quick);
        panel.Controls.Add(HudTheme.Button("Save quick text buttons", (_, __) => {
            quick.EndEdit(); settings.QuickTexts = quick.Rows.Cast<DataGridViewRow>().Select(r => new HudQuickText { Name = Convert.ToString(r.Cells[0].Value)?.Trim() ?? "", Text = Convert.ToString(r.Cells[1].Value) ?? "" })
                .Where(q => !string.IsNullOrWhiteSpace(q.Text)).ToList(); Save();
        }));
        Heading("Thank-you clipboard text");
        TextOption("key_thanks", "Key opener template ({player} is the opener)", settings.KeyPopThanks, x => settings.KeyPopThanks = x);
        TextOption("area_thanks", "Area unlock template ({player} and {item})", settings.AreaUnlockThanks, x => settings.AreaUnlockThanks = x);
        Label("Select a recognized key opening or area unlock in the Live feed, then choose Copy thanks. A /who response enables Copy roster.");
    }
}
