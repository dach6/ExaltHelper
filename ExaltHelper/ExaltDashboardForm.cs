using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal partial class ExaltDashboardForm : Form
{
    private readonly Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 8) };
    private readonly Panel pageHost = new Panel { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel navigation = new HudFlowPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(10, 4, 10, 12) };
    private readonly FlowLayoutPanel widgetBar = new HudFlowPanel { Dock = DockStyle.Top, Height = 89, BackColor = HudTheme.Background, Padding = new Padding(24, 8, 12, 8), AutoScroll = true, WrapContents = false, AllowDrop = true };
    private readonly FlowLayoutPanel taskBar = new HudFlowPanel { Dock = DockStyle.Top, Height = 60, BackColor = HudTheme.Background, Padding = new Padding(24, 4, 12, 6), AutoScroll = true, WrapContents = false };
    private readonly Label title = new Label { Dock = DockStyle.Fill, Font = HudTheme.HeadingFont, ForeColor = HudTheme.Text, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Label footer = new Label { Dock = DockStyle.Bottom, Height = 30, ForeColor = HudTheme.Muted, Padding = new Padding(24, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolTip tooltips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 350, ReshowDelay = 150 };
    private readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
    private readonly Dictionary<string, Button> navButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Label> widgets = new Dictionary<string, Label>();
    private readonly Dictionary<string, bool> widgetVisibility = new Dictionary<string, bool>();
    private readonly Timer refreshTimer = new Timer { Interval = 500 };
    private readonly Timer saveTimer = new Timer { Interval = 500 };
    private DpsSnapshot snapshot = new DpsSnapshot();
    private MoonlightVillageSnapshot moonlight = new MoonlightVillageSnapshot();
    private string taskSignature;
    private long lastTaskMissionRevision = -1, lastTaskAccountRevision = -1;
    private string taskAccountKey;
    private DateTime nextTaskRefreshUtc;
    private bool snapshotDirty = true, moonlightDirty = true;
    private FormWindowState refreshWindowState;
    private bool ready;
    private bool shuttingDown;
    private int lastJournalRevision = -1;
    private DateTime lastNoticeCheck;
    private string noticeAccountKey;
    private string[] currentCycleWarnings = Array.Empty<string>();
    private readonly HashSet<string> notified = new HashSet<string>();
    private Label cycleWarnings;
    public string SelectedPage { get; private set; }
    public event EventHandler CompactRequested;

    private static readonly string[][] PageDefinitions = {
        new[] { "live", "Live feed", "PLAY" }, new[] { "performance", "Combat tracker", "" }, new[] { "combat", "Combat history", "" },
        new[] { "loot", "Loot history", "" }, new[] { "trophies", "Trophy hall", "" }, new[] { "missions", "Missions", "PROGRESS" },
        new[] { "quests", "Quests", "" }, new[] { "characters", "Characters", "ACCOUNT" }, new[] { "treasury", "Treasury", "" },
        new[] { "vault", "Vault", "" }, new[] { "exaltations", "Exaltations", "" }, new[] { "pets", "Pet yard", "" },
        new[] { "party", "Party", "TOOLS" }, new[] { "players", "Player inspection", "" }, new[] { "abilities", "Ability log", "" }, new[] { "chat", "Chat", "" }, new[] { "moonlight", "Moonlight Village", "" },
        new[] { "calculator", "DPS calculator", "" }, new[] { "settings", "Settings", "" }
    };

    public ExaltDashboardForm()
    {
        Text = "ExaltHelper · ExaltHUD"; BackColor = HudTheme.Background; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi; MinimumSize = new Size(900, 620); ClientSize = new Size(1240, 820);
        StartPosition = FormStartPosition.Manual;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }
        var left = new Panel { Dock = DockStyle.Left, Width = 218, BackColor = HudTheme.Surface };
        left.Paint += (_, e) => { using var line = new Pen(HudTheme.Border); e.Graphics.DrawLine(line, left.Width - 1, 0, left.Width - 1, left.Height); };
        var brand = new HudBrandPanel { Dock = DockStyle.Top, Height = 82, BackColor = HudTheme.Surface };
        left.Controls.Add(navigation); left.Controls.Add(brand);
        foreach (var definition in PageDefinitions)
        {
            if (definition[2].Length > 0) navigation.Controls.Add(new Label { Text = definition[2], Width = 176, Height = 24,
                Font = HudTheme.SmallFont, ForeColor = HudTheme.Muted, Padding = new Padding(14, 6, 0, 0), Margin = new Padding(0, 10, 0, 4) });
            string key = definition[0];
            var button = new HudNavButton { Text = definition[1], IconKey = key, Font = HudTheme.BodyFont, Cursor = Cursors.Hand, AccessibleName = definition[1] };
            button.Click += (_, __) => SelectPage(key);
            button.Name = "nav_" + key; button.AutoSize = false; button.Size = new Size(188, 36); button.TextAlign = ContentAlignment.MiddleLeft;
            button.Margin = new Padding(0, 0, 0, 3); button.BackColor = HudTheme.Surface; button.FlatAppearance.BorderSize = 0;
            tooltips.SetToolTip(button, HudPreferences.Current.TabTooltips ? definition[1] : "");
            navButtons[key] = button; navigation.Controls.Add(button);
        }
        var top = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(24, 12, 20, 10), BackColor = HudTheme.Background };
        top.Paint += (_, e) => { using var line = new Pen(HudTheme.Border); e.Graphics.DrawLine(line, 24, top.Height - 1, top.Width - 24, top.Height - 1); };
        var actions = new HudFlowPanel { Dock = DockStyle.Right, Width = 408, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        actions.Controls.Add(HudTheme.Button("Compact  F10", (_, __) => CompactRequested?.Invoke(this, EventArgs.Empty)));
        actions.Controls.Add(HudTheme.Button("Monitor", (_, __) => ShowMonitorMenu()));
        var pin = HudTheme.Button("Pin window", (_, __) => { TopMost = !TopMost; HudPreferences.Current.AlwaysOnTop = TopMost; HudPreferences.Current.Save(); });
        tooltips.SetToolTip(pin, "Keep this dashboard above other desktop windows."); actions.Controls.Add(pin);
        top.Controls.Add(title); top.Controls.Add(actions);
        content.Controls.Add(pageHost);
        var workspace = new Panel { Dock = DockStyle.Fill };
        workspace.Controls.Add(content); workspace.Controls.Add(footer); workspace.Controls.Add(taskBar); workspace.Controls.Add(widgetBar); workspace.Controls.Add(top);
        Controls.Add(workspace); Controls.Add(left);
        widgetBar.DragEnter += (_, e) => { if (e.Data.GetDataPresent(typeof(string)) && ((string)e.Data.GetData(typeof(string))).StartsWith("widget:")) e.Effect = DragDropEffects.Move; };
        widgetBar.DragDrop += (_, e) => MoveWidget((string)e.Data.GetData(typeof(string)), widgetBar.PointToClient(new Point(e.X, e.Y)));
        RestoreLayout();
        refreshTimer.Tick += (_, __) => RefreshDashboardTick(); refreshTimer.Start();
        saveTimer.Tick += (_, __) => { saveTimer.Stop(); SaveLayout(); };
        LocationChanged += (_, __) => ScheduleSave(); SizeChanged += (_, __) => ScheduleSave();
        ready = true;
        SelectPage(navButtons.ContainsKey(HudPreferences.Current.Page ?? "") ? HudPreferences.Current.Page : "live");
        RefreshDashboard();
    }

    public static Rectangle RestoreBoundsToScreens(Rectangle saved, IEnumerable<Rectangle> workAreas)
        => ClampBounds(saved, workAreas, new Size(900, 620));

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); HudWindowChrome.Apply(this); }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (ready && Visible && WindowState != FormWindowState.Minimized) RefreshDashboard();
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        bool restored = refreshWindowState == FormWindowState.Minimized && WindowState != FormWindowState.Minimized;
        refreshWindowState = WindowState;
        if (ready && Visible && restored) RefreshDashboard();
    }

    public static Rectangle ClampBounds(Rectangle saved, IEnumerable<Rectangle> workAreas, Size minimum)
    {
        var areas = workAreas.Where(x => x.Width > 0 && x.Height > 0).ToArray();
        if (areas.Length == 0) return new Rectangle(40, 40, Math.Max(400, saved.Width), Math.Max(300, saved.Height));
        Rectangle best = areas.OrderByDescending(x => { Rectangle overlap = Rectangle.Intersect(x, saved); return (long)overlap.Width * overlap.Height; }).First();
        int width = Math.Min(Math.Max(minimum.Width, saved.Width), best.Width), height = Math.Min(Math.Max(minimum.Height, saved.Height), best.Height);
        return new Rectangle(Math.Max(best.Left, Math.Min(saved.X, best.Right - width)), Math.Max(best.Top, Math.Min(saved.Y, best.Bottom - height)), width, height);
    }
    private void RestoreLayout()
    {
        var settings = HudPreferences.Current;
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        var saved = new Rectangle(settings.DesktopX == int.MinValue ? area.Left + 40 : settings.DesktopX,
            settings.DesktopX == int.MinValue ? area.Top + 40 : settings.DesktopY, settings.DesktopWidth, settings.DesktopHeight);
        var restored = RestoreBoundsToScreens(saved, Screen.AllScreens.Select(x => x.WorkingArea));
        MinimumSize = new Size(Math.Min(900, restored.Width), Math.Min(620, restored.Height));
        Bounds = restored;
        TopMost = settings.AlwaysOnTop;
        if (settings.Maximized) WindowState = FormWindowState.Maximized;
    }
    private void ScheduleSave() { if (ready && !shuttingDown) { saveTimer.Stop(); saveTimer.Start(); } }
    public void SaveLayout()
    {
        if (!ready || WindowState == FormWindowState.Minimized) return;
        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var s = HudPreferences.Current;
        s.DesktopX = bounds.X; s.DesktopY = bounds.Y; s.DesktopWidth = bounds.Width; s.DesktopHeight = bounds.Height;
        s.Maximized = WindowState == FormWindowState.Maximized; s.Page = SelectedPage; s.AlwaysOnTop = TopMost; s.Save();
    }
    private void ShowMonitorMenu()
    {
        var menu = new ContextMenuStrip { BackColor = HudTheme.Surface, ForeColor = HudTheme.Text };
        foreach (var screen in Screen.AllScreens)
        {
            var target = screen;
            menu.Items.Add((screen.Primary ? "Primary" : "Display") + " · " + screen.WorkingArea.Width + " × " + screen.WorkingArea.Height, null, (_, __) =>
            {
                WindowState = FormWindowState.Normal;
                MinimumSize = new Size(Math.Min(900, target.WorkingArea.Width), Math.Min(620, target.WorkingArea.Height));
                Bounds = RestoreBoundsToScreens(new Rectangle(target.WorkingArea.Location, Size), new[] { target.WorkingArea });
                SaveLayout();
            });
        }
        menu.Closed += (_, __) => menu.Dispose(); menu.Show(Cursor.Position);
    }
    public void SelectPage(string key)
    {
        if (!navButtons.ContainsKey(key)) return;
        if (!pages.TryGetValue(key, out var page)) { page = CreatePage(key); page.Dock = DockStyle.Fill; pages[key] = page; pageHost.Controls.Add(page); }
        foreach (var entry in pages) entry.Value.Visible = entry.Key == key;
        page.BringToFront(); SelectedPage = key; title.Text = navButtons[key].Text;
        foreach (var nav in navButtons) { ((HudNavButton)nav.Value).Active = nav.Key == key; nav.Value.BackColor = HudTheme.Surface; nav.Value.ForeColor = nav.Key == key ? HudTheme.Accent : HudTheme.Muted; }
        navigation.ScrollControlIntoView(navButtons[key]);
        lastJournalRevision = -1; RefreshDashboard(); ScheduleSave();
    }
    public void UpdateSnapshot(DpsSnapshot value) { if (value != null) { snapshot = value; snapshotDirty = true; } }
    public void UpdateMoonlightVillage(MoonlightVillageSnapshot value) { if (value != null) { moonlight = value; moonlightDirty = true; } }
    private void RefreshDashboardTick()
    {
        if (IsDisposed || shuttingDown) return;
        if (!Visible || WindowState == FormWindowState.Minimized)
        {
            // Clipboard expiry and reminders remain active while the view is hidden.
            CheckNotifications(); RefreshAutomaticCallout(); return;
        }
        RefreshDashboard();
    }
    private void RefreshDashboard()
    {
        if (IsDisposed) return;
        RefreshWidgets(); RefreshTasks();
        CheckNotifications();
        if (SelectedPage == "live") RefreshCycleWarnings();
        int revision = HudJournal.PageRevision(SelectedPage);
        if (lastJournalRevision != revision) { lastJournalRevision = revision; RefreshPageData(); }
        else if (snapshotDirty) RefreshLivePageData();
        if (tables.TryGetValue(SelectedPage, out var visibleTable)) visibleTable.RefreshDateFilter();
        snapshotDirty = false;
        if (SelectedPage == "moonlight" && moonlightView != null && moonlightDirty)
        { moonlightView.UpdateMoonlightVillage(moonlight); moonlightDirty = false; }
        if (SelectedPage == "live") RefreshCalloutPreview();
        RefreshAutomaticCallout();
        if (SelectedPage == "settings" && soundStatus != null) soundStatus.Text = HudSounds.LastError;
        string location = HudJournal.Map + (string.IsNullOrEmpty(HudJournal.Realm) ? "" : " · " + HudJournal.Realm);
        footer.Text = HudStorage.LastError ?? (location + "    ·    " + (HudJournal.LastPacketUtc == default ? "Launch the game through ExaltHelper to connect" :
            (DateTime.UtcNow - HudJournal.LastPacketUtc).TotalSeconds > 15 ? "Last activity " + HudJournal.LastPacketUtc.ToLocalTime().ToString("HH:mm:ss") : "Receiving game data  ·  " + HudJournal.Latency + " ms") + "    ·    F8 hide  /  F10 compact");
    }
    private void CheckNotifications()
    {
        string accountKey = AccountWorkspace.AccountKey;
        if (noticeAccountKey == accountKey && (DateTime.UtcNow - lastNoticeCheck).TotalSeconds < 30) return;
        noticeAccountKey = accountKey;
        lastNoticeCheck = DateTime.UtcNow;
        var account = AccountWorkspace.ReadHudSummary();
        var warnings = currentCycleWarnings = CycleWarningValues(DateTime.UtcNow, account.SeasonEndUtc, account.BattlePassEndUtc).ToArray();
        foreach (string warning in warnings)
            if (notified.Add(account.AccountKey + DateTime.UtcNow.ToString("yyyyMMdd") + warning.Split(':')[0])) { HudJournal.AppendFeed("Reminder", warning); HudSounds.Play("Reminder"); }
        foreach (var member in HudJournal.LiveSnapshot.PartyMembers.Where(x => x.IsInDungeon && (watchlist.Contains(x.Name) || banned.Contains(x.Name))))
        {
            string kind = banned.Contains(member.Name) ? "Banned-list player " : "Watchlist member ";
            string key = account.AccountKey + HudJournal.LiveSnapshot.DungeonStartTime.Ticks + kind + member.Name;
            if (notified.Add(key)) { HudJournal.AppendFeed("Party", kind + member.Name + " is in your dungeon."); HudSounds.Play("Party"); }
        }
        if (notified.Count > 1000) notified.Clear();
    }
    private void RefreshCycleWarnings()
    {
        if (cycleWarnings == null) return;
        string text = string.Join("\n", currentCycleWarnings);
        if (cycleWarnings.Text != text) { cycleWarnings.Text = text; tooltips.SetToolTip(cycleWarnings, text); }
        int height = Math.Max(28, currentCycleWarnings.Length * 23 + 8);
        if (cycleWarnings.Height != height) cycleWarnings.Height = height;
        cycleWarnings.Visible = currentCycleWarnings.Length > 0;
    }
    internal static IEnumerable<string> CycleWarnings(DateTime utcNow, AccountSnapshot account)
        => CycleWarningValues(utcNow, account.SeasonEndUtc, account.BattlePassEndUtc);
    private static IEnumerable<string> CycleWarningValues(DateTime utcNow, DateTime? seasonEndUtc, DateTime? battlePassEndUtc)
    {
        var settings = HudPreferences.Current;
        if (settings.CalendarWarnings && utcNow.Day == DateTime.DaysInMonth(utcNow.Year, utcNow.Month))
            yield return "Daily login calendar: last day of the month. Claim your rewards before the UTC reset.";
        foreach (var timer in new[] { Tuple.Create("Season", seasonEndUtc, settings.SeasonWarnings), Tuple.Create("Battlepass", battlePassEndUtc, settings.BattlePassWarnings) })
        {
            if (!timer.Item3 || !timer.Item2.HasValue) continue;
            var left = timer.Item2.Value.ToUniversalTime() - utcNow;
            if (left > TimeSpan.Zero && left.TotalDays <= 3) yield return timer.Item1 + ": ends in " + (left.Days > 0 ? left.Days + "d " + left.Hours + "h" : left.Hours + "h " + left.Minutes + "m") + ". Check your unclaimed rewards.";
        }
    }
    private void RefreshWidgets()
    {
        var s = HudPreferences.Current; widgetBar.Visible = s.WidgetBar;
        if (!s.WidgetBar) return;
        widgetBar.SuspendLayout();
        bool layoutChanged = false;
        var direction = s.WidgetRows > 1 ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        if (widgetBar.FlowDirection != direction) { widgetBar.FlowDirection = direction; layoutChanged = true; }
        if (widgetBar.WrapContents != (s.WidgetRows > 1)) { widgetBar.WrapContents = s.WidgetRows > 1; layoutChanged = true; }
        var account = AccountWorkspace.ReadHudSummary();
        var live = HudJournal.LiveSnapshot.LocalPlayer;
        bool seasonal = s.WidgetStorage == "Seasonal" || (s.WidgetStorage == "Current" && account.CurrentCharacterSeasonal);
        string Materials(IReadOnlyList<int?> amounts) => string.Join(" / ", amounts.Select(FormatCurrency));
        bool known = account.UpdatedUtc != default;
        var values = new Dictionary<string, string> {
            ["Character"] = live.MaxHp > 0 ? live.Name + " · " + live.ClassName : account.Name ?? "Connect to your account", ["HP"] = live.MaxHp > 0 ? live.CurrentHp + " / " + live.MaxHp : "—",
            ["MP"] = live.MaxMp > 0 ? live.CurrentMp + " / " + live.MaxMp : "—", ["Gold"] = FormatCurrency(account.Gold),
            ["Fame"] = FormatCurrency(account.Fame), ["Forge"] = FormatCurrency(seasonal ? account.SeasonalForgeFire : account.RegularForgeFire),
            ["Dust"] = FormatCurrency(seasonal ? account.SeasonalDust : account.RegularDust),
            ["Materials"] = Materials(seasonal ? account.SeasonalMaterials : account.RegularMaterials),
            ["Rank"] = FormatCurrency(account.Stars) + " stars", ["Skins"] = known ? account.SkinCount.ToString("N0") : "—",
            ["Playtime"] = account.HasPcStats ? (account.PlaytimeMinutes / 60).ToString("N0") + "h " + (account.PlaytimeMinutes % 60) + "m" : "—",
            ["Season"] = Countdown(account.SeasonEndUtc), ["Battlepass"] = Countdown(account.BattlePassEndUtc),
            ["Characters"] = known ? account.CharacterCount + " / " + account.CharacterSlots : "—",
            ["Server"] = string.IsNullOrEmpty(HudJournal.Server) ? "—" : HudJournal.Server,
            ["Realm"] = RealmWidgetValue(DateTime.UtcNow)
        };
        foreach (var entry in values.OrderBy(x => { int i = s.WidgetOrder.IndexOf(x.Key); return i < 0 ? 100 : i; }))
        {
            if (!widgets.TryGetValue(entry.Key, out var label))
            {
                string key = entry.Key;
                label = new HudStatCard { BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, Font = HudTheme.SmallFont,
                    Margin = new Padding(0, 0, 8, 4), Cursor = Cursors.Hand, Tag = key, AccessibleName = key };
                Point press = Point.Empty;
                label.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) ShowWidgetMenu(values.Keys); else press = e.Location; };
                label.MouseMove += (_, e) => { if (e.Button == MouseButtons.Left && (Math.Abs(e.X - press.X) > 6 || Math.Abs(e.Y - press.Y) > 6)) label.DoDragDrop("widget:" + key, DragDropEffects.Move); };
                label.DoubleClick += (_, __) => SelectPage(key == "Forge" || key == "Dust" ? "treasury" : "characters");
                widgets[key] = label; widgetBar.Controls.Add(label); layoutChanged = true;
            }
            float? progress = entry.Key == "HP" && live.MaxHp > 0 ? (float?)live.CurrentHp / live.MaxHp
                : entry.Key == "MP" && live.MaxMp > 0 ? (float?)live.CurrentMp / live.MaxMp : null;
            var card = (HudStatCard)label;
            if (card.Progress != progress) { card.Progress = progress; card.Invalidate(); }
            string text = entry.Key.ToUpperInvariant() + "   " + entry.Value;
            if (label.Text != text) { label.Text = text; layoutChanged = true; }
            bool enabled = !s.HiddenWidgets.Contains(entry.Key);
            if (!widgetVisibility.TryGetValue(entry.Key, out bool wasVisible) || wasVisible != enabled)
            { widgetVisibility[entry.Key] = enabled; label.Visible = enabled; layoutChanged = true; }
            string detail = entry.Key == "Character" && live.MaxHp > 0 ? "Level " + live.Level + " · " + live.MaxedStats + " maxed\nATT " + live.Attack + " · DEX " + live.Dexterity + " · DEF " + live.Defense + "\nSPD " + live.Speed + " · VIT " + live.Vitality + " · WIS " + live.Wisdom
                : entry.Key == "Dust" ? "Regular green / purple / red: " + (account.RegularDust.HasValue ? string.Join(" / ", account.RegularDustAmounts) : "Not observed") + "\nSeasonal green / purple / red: " + (account.SeasonalDust.HasValue ? string.Join(" / ", account.SeasonalDustAmounts) : "Not observed")
                : entry.Key == "Materials" ? "Common / rare / legendary / mythical\nRegular: " + Materials(account.RegularMaterials) + "\nSeasonal: " + Materials(account.SeasonalMaterials)
                : entry.Key == "Forge" ? "Regular: " + FormatCurrency(account.RegularForgeFire) + "\nSeasonal: " + FormatCurrency(account.SeasonalForgeFire)
                : entry.Key == "Season" ? account.SeasonName + "\n" + account.SeasonEndUtc?.ToLocalTime().ToString("f")
                : entry.Key == "Battlepass" ? account.BattlePassName + "\n" + account.BattlePassEndUtc?.ToLocalTime().ToString("f") : "";
            string tip = detail + "\nDrag to reorder. Right-click to choose widgets.\n" + account.Status;
            if (tooltips.GetToolTip(label) != tip) tooltips.SetToolTip(label, tip);
        }
        int visibleIndex = 0;
        foreach (Control label in widgetBar.Controls)
        {
            bool enabled = !s.HiddenWidgets.Contains((string)label.Tag);
            bool flowBreak = s.WidgetRows > 1 && enabled && ++visibleIndex % 2 == 0;
            if (widgetBar.GetFlowBreak(label) != flowBreak) { widgetBar.SetFlowBreak(label, flowBreak); layoutChanged = true; }
        }
        int rowHeight = widgets.Count == 0 ? 30 : widgets.Values.Max(x => x.Height);
        int height = (s.WidgetRows > 1 ? 2 * (rowHeight + 4) : rowHeight + 4) + widgetBar.Padding.Vertical + SystemInformation.HorizontalScrollBarHeight + 2;
        if (widgetBar.Height != height) { widgetBar.Height = height; layoutChanged = true; }
        widgetBar.ResumeLayout(layoutChanged);
    }
    private static string Countdown(DateTime? end)
    {
        if (!end.HasValue) return "—";
        var left = end.Value.ToUniversalTime() - DateTime.UtcNow;
        if (left <= TimeSpan.Zero) return "Ended";
        return left.Days > 0 ? left.Days + "d " + left.Hours + "h" : left.Hours + "h " + left.Minutes + "m";
    }
    internal static string RealmWidgetValue(DateTime utcNow)
    {
        if (HudJournal.RealmCastleUtc is DateTime castle)
        {
            var remaining = castle.ToUniversalTime() - utcNow.ToUniversalTime();
            return remaining <= TimeSpan.Zero ? "Closed" : "Castle " + TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)).ToString(@"mm\:ss");
        }
        return HudJournal.MaxRealmScore > 0 ? (100.0 * HudJournal.RealmScore / HudJournal.MaxRealmScore).ToString("0.0") + "%" : "—";
    }
    private static string FormatCurrency(int? value) => value.HasValue ? value.Value.ToString("N0") : "—";
    private void ShowWidgetMenu(IEnumerable<string> keys)
    {
        var menu = new ContextMenuStrip { BackColor = HudTheme.Surface, ForeColor = HudTheme.Text };
        foreach (string key in keys)
        {
            var item = new ToolStripMenuItem(key) { Checked = !HudPreferences.Current.HiddenWidgets.Contains(key), CheckOnClick = true };
            item.CheckedChanged += (_, __) => { if (item.Checked) HudPreferences.Current.HiddenWidgets.Remove(key); else HudPreferences.Current.HiddenWidgets.Add(key); HudPreferences.Current.Save(); RefreshWidgets(); };
            menu.Items.Add(item);
        }
        menu.Closed += (_, __) => menu.Dispose(); menu.Show(Cursor.Position);
    }
    private void MoveWidget(string token, Point location)
    {
        if (token == null || !token.StartsWith("widget:") || !widgets.TryGetValue(token.Substring(7), out var source)) return;
        var target = widgetBar.GetChildAtPoint(location);
        int index = target != null ? widgetBar.Controls.GetChildIndex(target) : widgetBar.Controls.Count - 1;
        widgetBar.Controls.SetChildIndex(source, index);
        HudPreferences.Current.WidgetOrder = widgetBar.Controls.Cast<Control>().Select(x => (string)x.Tag).ToList(); HudPreferences.Current.Save();
    }
    private void RefreshTasks()
    {
        taskBar.Visible = HudPreferences.Current.TaskBar;
        if (!HudPreferences.Current.TaskBar) return;
        long missionRevision = MissionWorkspace.Revision, accountRevision = AccountWorkspace.Revision;
        string accountKey = AccountWorkspace.AccountKey;
        DateTime now = DateTime.UtcNow;
        if (missionRevision == lastTaskMissionRevision && accountRevision == lastTaskAccountRevision
            && accountKey == taskAccountKey && now < nextTaskRefreshUtc) return;
        var tasks = MissionWorkspace.GetTrackedTasks();
        lastTaskMissionRevision = missionRevision; lastTaskAccountRevision = accountRevision; taskAccountKey = accountKey;
        nextTaskRefreshUtc = MissionWorkspace.NextTaskRefreshUtc(now);
        string signature = string.Join("|", tasks.Select(x => x.Id + x.Title + x.Detail + x.Progress + x.Status + x.Eligible));
        if (signature == taskSignature) return; taskSignature = signature;
        taskBar.SuspendLayout();
        while (taskBar.Controls.Count > 0) { var old = taskBar.Controls[0]; taskBar.Controls.Remove(old); old.Dispose(); }
        taskBar.Controls.Add(HudTheme.Label("TASKBAR", true));
        if (tasks.Count == 0) taskBar.Controls.Add(HudTheme.Button("Track missions and quests  +", (_, __) => SelectPage("missions")));
        foreach (var task in tasks)
        {
            var chip = HudTheme.Button(task.Title + "   " + task.Progress.ToString("P0") + "   " + task.Status, (_, __) => {
                string id = task.SourceIds.FirstOrDefault() ?? task.Id;
                string key = id.StartsWith("quest:", StringComparison.Ordinal) ? "quests" : "missions"; SelectPage(key);
                if (pages[key] is MissionPage page) page.SelectTask(id);
            });
            chip.Font = HudTheme.SmallFont; chip.ForeColor = task.Eligible ? HudTheme.Accent : HudTheme.Muted;
            tooltips.SetToolTip(chip, task.Detail); taskBar.Controls.Add(chip);
        }
        taskBar.ResumeLayout(true);
    }
    public void Shutdown() { shuttingDown = true; SaveLayout(); Close(); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveLayout();
        if (!shuttingDown && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; CompactRequested?.Invoke(this, EventArgs.Empty); Hide(); }
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { ClearAutomaticCallout(); refreshTimer.Dispose(); saveTimer.Dispose(); tooltips.Dispose(); }
        base.Dispose(disposing);
    }
}
