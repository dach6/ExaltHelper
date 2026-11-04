using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ExaltHelper;

internal sealed class MissionPage : UserControl
{
    private readonly bool quests;
    private readonly TextBox search = new HudTextBox { Width = 220, SearchIcon = true, Placeholder = "Search objectives...", BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 4, 10, 0), AccessibleName = "Search missions or quests" };
    private readonly ComboBox filter = new HudComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = HudTheme.Input, ForeColor = HudTheme.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 4, 10, 0), AccessibleName = "Status filter" };
    private readonly ComboBox category = new HudComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = HudTheme.Input, ForeColor = HudTheme.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 4, 10, 0), AccessibleName = "Category filter" };
    private readonly ComboBox storage = new HudComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = HudTheme.Input, ForeColor = HudTheme.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 4, 10, 0), AccessibleName = "Quest mark storage side" };
    private readonly CheckBox showHidden = new HudCheckBox { Text = "Show hidden", AutoSize = true, ForeColor = HudTheme.Muted, Margin = new Padding(0, 8, 10, 0) };
    private readonly CheckBox taskOptions = new HudCheckBox { AutoSize = true, ForeColor = HudTheme.Muted, Margin = new Padding(0, 8, 10, 6) };
    private readonly CheckBox autoTrack = new HudCheckBox { AutoSize = true, ForeColor = HudTheme.Muted, Margin = new Padding(0, 8, 10, 6) };
    private readonly ToolTip optionsTip = new ToolTip { AutoPopDelay = 15000 };
    private readonly FlowLayoutPanel toolbar = new HudFlowPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 8) };
    private readonly Panel cards = new HudScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, AllowDrop = true };
    private readonly Label info = new Label { Dock = DockStyle.Top, Height = 38, ForeColor = HudTheme.Muted, AutoEllipsis = true };
    private readonly Timer timer = new Timer { Interval = 1000 };
    private readonly Button refresh;
    private readonly Button hideCategory;
    private List<MissionTaskView> rows = new List<MissionTaskView>();
    private string signature;
    private bool rebuilding;
    private bool applyingPreferences;
    private string currentAccount;
    private long preferencesRevision = -1;
    private MissionPagePreferences view = new MissionPagePreferences();

    public MissionPage(bool quests)
    {
        this.quests = quests;
        Dock = DockStyle.Fill; BackColor = HudTheme.Background; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        filter.Items.AddRange(new object[] { "All statuses", "In progress", "Ready to claim", "Locked", "Cooldown", "Claimed", "Refresh at Tinkerer", "Expired" });
        filter.SelectedIndex = 0;
        category.Items.Add("All categories"); category.SelectedIndex = 0;
        storage.Items.AddRange(new object[] { "Both", "Seasonal", "Regular" }); storage.SelectedItem = MissionWorkspace.QuestStorage;
        refresh = HudTheme.Button(quests ? "Refresh marks" : "Refresh", async (_, __) =>
        {
            refresh.Enabled = false;
            try { if (quests) await AccountWorkspace.RefreshAsync(); else await MissionWorkspace.RefreshAsync(); }
            finally { if (!IsDisposed) { refresh.Enabled = true; RefreshRows(true); } }
        });
        hideCategory = HudTheme.Button("Hide / show category", (_, __) => { if (category.SelectedIndex > 0) { MissionWorkspace.ToggleCategory(category.SelectedItem.ToString()); RefreshRows(true); } });
        hideCategory.Enabled = false;
        toolbar.Controls.Add(refresh); toolbar.Controls.Add(search); toolbar.Controls.Add(filter); toolbar.Controls.Add(category);
        if (quests) toolbar.Controls.Add(storage);
        toolbar.Controls.Add(showHidden); toolbar.Controls.Add(hideCategory);
        taskOptions.Text = quests ? "Taskbar prefers seasonal marks" : "Show all choice options in Taskbar";
        toolbar.Controls.Add(taskOptions);
        autoTrack.Text = quests ? "Automatically track daily quest chests" : "Automatically track missions";
        optionsTip.SetToolTip(autoTrack, "Changing this resets the Track / Tracked choices for " + (quests ? "quests" : "missions") + ". Hidden cards stay hidden.");
        toolbar.Controls.Add(autoTrack);
        Controls.Add(cards); Controls.Add(toolbar); Controls.Add(info);
        search.TextChanged += (_, __) => { if (!applyingPreferences) Rebuild(); };
        filter.SelectedIndexChanged += (_, __) => SaveViewAndRebuild();
        category.SelectedIndexChanged += (_, __) => { hideCategory.Enabled = category.SelectedIndex > 0; SaveViewAndRebuild(); };
        showHidden.CheckedChanged += (_, __) => SaveViewAndRebuild();
        storage.SelectedIndexChanged += (_, __) => { if (!applyingPreferences) { MissionWorkspace.SetQuestStorage(storage.SelectedItem.ToString()); RefreshRows(true); } };
        taskOptions.CheckedChanged += (_, __) => { if (!applyingPreferences) { if (quests) MissionWorkspace.SetPreferSeasonalMarks(taskOptions.Checked); else MissionWorkspace.SetShowAllChoices(taskOptions.Checked); RefreshRows(true); } };
        autoTrack.CheckedChanged += (_, __) => { if (!applyingPreferences) { MissionWorkspace.SetAutoTracking(quests, autoTrack.Checked); RefreshRows(true); } };
        cards.Resize += (_, __) => Rebuild();
        timer.Tick += (_, __) => { if (Visible) RefreshRows(false); };
        VisibleChanged += (_, __) => { if (Visible) RefreshRows(true); };
        timer.Start();
        RefreshRows(true);
    }

    public void SelectTask(string id)
    {
        var row = (quests ? MissionWorkspace.GetQuests() : MissionWorkspace.GetMissions()).FirstOrDefault(t => t.Id == id);
        if (row == null) return;
        filter.SelectedIndex = 0; category.SelectedIndex = 0; showHidden.Checked = row.Hidden;
        view.Collapsed.Remove(quests ? row.Category : StatusGroup(row.Status));
        MissionWorkspace.SetPagePreferences(quests, view);
        search.Text = row.Title;
        RefreshRows(true);
    }

    private void RefreshRows(bool force)
    {
        if (IsDisposed) return;
        string active = MissionWorkspace.ActiveAccountKey;
        long nextRevision = MissionWorkspace.Revision;
        if (active != currentAccount || preferencesRevision != nextRevision)
        {
            bool changedAccount = active != currentAccount;
            currentAccount = active; view = MissionWorkspace.GetPagePreferences(quests);
            preferencesRevision = nextRevision;
            applyingPreferences = true;
            filter.SelectedItem = filter.Items.Contains(view.Status) ? view.Status : "All statuses";
            showHidden.Checked = view.ShowHidden;
            storage.SelectedItem = MissionWorkspace.QuestStorage;
            taskOptions.Checked = quests ? MissionWorkspace.PreferSeasonalMarks : MissionWorkspace.ShowAllChoices;
            autoTrack.Checked = MissionWorkspace.GetAutoTracking(quests);
            if (changedAccount) search.Clear();
            applyingPreferences = false; force = true;
        }
        rows = quests ? MissionWorkspace.GetQuests() : MissionWorkspace.GetMissions();
        info.Text = (quests ? MissionWorkspace.QuestSummary : MissionWorkspace.Status) + " · " + rows.Count + " " + (quests ? "quests" : "missions");
        refresh.Enabled = quests || !MissionWorkspace.IsRefreshing;
        string next = string.Join("|", rows.Select(r => r.Id + r.Detail + r.Status + r.Pinned + r.Hidden)) + MissionWorkspace.QuestStorage;
        if (force || next != signature)
        {
            signature = next;
            string selected = view.Category;
            var categories = new[] { "All categories" }.Concat(rows.Select(r => r.Category).Distinct().OrderBy(c => c)).ToArray();
            if (!category.Items.Cast<string>().SequenceEqual(categories))
            {
                rebuilding = true; category.Items.Clear(); category.Items.AddRange(categories); category.SelectedItem = categories.Contains(selected) ? selected : categories[0]; rebuilding = false;
            }
            else if ((category.SelectedItem as string) != (categories.Contains(selected) ? selected : categories[0]))
            {
                rebuilding = true; category.SelectedItem = categories.Contains(selected) ? selected : categories[0]; rebuilding = false;
            }
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (rebuilding || applyingPreferences || cards.ClientSize.Width < 100) return;
        rebuilding = true;
        try
        {
            int scroll = -cards.AutoScrollPosition.Y;
            cards.SuspendLayout();
            cards.AutoScrollPosition = Point.Empty;
            foreach (Control child in cards.Controls.Cast<Control>().ToArray()) child.Dispose();
            cards.Controls.Clear();
            string term = search.Text.Trim(); string state = filter.SelectedItem as string; string cat = category.SelectedItem as string;
            var visible = rows.Where(r => (showHidden.Checked || !r.Hidden) && (filter.SelectedIndex <= 0 || r.Status.StartsWith(state ?? "", StringComparison.Ordinal)) && (category.SelectedIndex <= 0 || r.Category == cat) && (term.Length == 0 || (r.Title + " " + r.Detail).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (visible.Count == 0)
            {
                var empty = new Label { Text = rows.Count == 0 ? (quests ? "Your quests will appear after you visit the Tinkerer.\nRefresh marks after visiting your vault or changing characters." : "Your season, your next objective.\nConnect a character, then click Refresh to load missions.") : "No matches. Change the search or filters to see more.", ForeColor = HudTheme.Muted, Font = HudTheme.BodyFont, Location = new Point(24, 35), Size = new Size(Math.Max(100, cards.ClientSize.Width - 48), 100) };
                cards.Controls.Add(empty);
            }
            int available = cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            int columns = available >= 1050 ? 2 : 1;
            int width = (available - (columns - 1) * 12) / columns;
            int y = 0;
            foreach (var group in visible.GroupBy(r => quests ? r.Category : StatusGroup(r.Status)).OrderBy(g => quests ? 0 : StatusRank(g.Key)))
            {
                bool collapsed = view.Collapsed.Contains(group.Key);
                var heading = new Button { Text = (collapsed ? "+  " : "−  ") + group.Key.ToUpperInvariant() + "  ·  " + group.Count(), ForeColor = HudTheme.Muted, Font = HudTheme.SmallFont, Location = new Point(0, y), Size = new Size(available, 30), TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, BackColor = HudTheme.Background, AccessibleName = (collapsed ? "Expand " : "Collapse ") + group.Key };
                heading.FlatAppearance.BorderSize = 0;
                heading.Click += (_, __) => { if (!view.Collapsed.Remove(group.Key)) view.Collapsed.Add(group.Key); MissionWorkspace.SetPagePreferences(quests, view); Rebuild(); };
                cards.Controls.Add(heading);
                y += 34;
                if (collapsed) { y += 8; continue; }
                int column = 0;
                foreach (var row in group)
                {
                    var card = new MissionCard(row) { Location = new Point(column * (width + 12), y), Size = new Size(width, 238), AllowDrop = true };
                    card.PreferenceChanged += () => RefreshRows(true);
                    card.DragEnter += (_, e) => e.Effect = e.Data.GetDataPresent(typeof(string)) ? DragDropEffects.Move : DragDropEffects.None;
                    card.DragDrop += (_, e) => { var from = e.Data.GetData(typeof(string)) as string; if (from != null && rows.Any(r => r.Id == from)) { MissionWorkspace.MoveBefore(from, row.Id); RefreshRows(true); } };
                    card.MoveRequested += delta =>
                    {
                        var siblings = group.ToList(); int index = siblings.FindIndex(r => r.Id == row.Id);
                        if (delta < 0 && index > 0) MissionWorkspace.MoveBefore(row.Id, siblings[index - 1].Id);
                        else if (delta > 0 && index >= 0 && index < siblings.Count - 1) MissionWorkspace.MoveBefore(siblings[index + 1].Id, row.Id);
                        RefreshRows(true);
                    };
                    cards.Controls.Add(card);
                    if (++column == columns) { column = 0; y += 250; }
                }
                if (column != 0) y += 250;
                y += 10;
            }
            cards.AutoScrollMinSize = new Size(0, y);
            cards.AutoScrollPosition = new Point(0, scroll);
            cards.ResumeLayout();
        }
        finally { rebuilding = false; }
    }
    private void SaveViewAndRebuild()
    {
        if (applyingPreferences || rebuilding) return;
        view.Status = filter.SelectedItem as string ?? "All statuses";
        view.Category = category.SelectedItem as string ?? "All categories";
        view.ShowHidden = showHidden.Checked;
        MissionWorkspace.SetPagePreferences(quests, view);
        Rebuild();
    }
    private static string StatusGroup(string s) => s.StartsWith("Cooldown", StringComparison.Ordinal) ? "Cooldown" : s;
    private static int StatusRank(string s) => s == "Ready to claim" ? 0 : s == "In progress" ? 1 : s == "Cooldown" ? 2 : s == "Locked" ? 3 : 4;
    protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); optionsTip.Dispose(); } base.Dispose(disposing); }
}

internal sealed class MissionCard : UserControl
{
    private readonly MissionTaskView row;
    private readonly Font titleFont = new Font("Segoe UI Semibold", 11);
    private readonly ToolTip tip = new ToolTip { AutoPopDelay = 30000, InitialDelay = 400, ReshowDelay = 100 };
    private Point dragStart;
    public event Action PreferenceChanged;
    public event Action<int> MoveRequested;
    public MissionCard(MissionTaskView row)
    {
        this.row = row;
        DoubleBuffered = true; BackColor = HudTheme.Surface; ForeColor = HudTheme.Text; TabStop = true;
        AccessibleName = row.Title + ". " + row.Status + ". " + row.Detail;
        AccessibleRole = AccessibleRole.Grouping;
        tip.SetToolTip(this, row.Detail);
        var actions = new HudFlowPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 0, 0, 0), WrapContents = false };
        actions.Controls.Add(ActionButton(row.Pinned ? "Tracked" : "Track", (_, __) => { MissionWorkspace.TogglePin(row.Id); PreferenceChanged?.Invoke(); }));
        actions.Controls.Add(ActionButton(row.Hidden ? "Show" : "Hide", (_, __) => { MissionWorkspace.ToggleHidden(row.Id); PreferenceChanged?.Invoke(); }));
        actions.Controls.Add(ActionButton("Details", (_, __) => ShowDetails()));
        var up = ActionButton("↑", (_, __) => MoveRequested?.Invoke(-1)); up.AccessibleName = "Move " + row.Title + " up"; actions.Controls.Add(up);
        var down = ActionButton("↓", (_, __) => MoveRequested?.Invoke(1)); down.AccessibleName = "Move " + row.Title + " down"; actions.Controls.Add(down);
        Controls.Add(actions);
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { dragStart = e.Location; Focus(); } };
        MouseMove += (_, e) => { if (e.Button == MouseButtons.Left && Math.Abs(e.X - dragStart.X) + Math.Abs(e.Y - dragStart.Y) > 12) DoDragDrop(row.Id, DragDropEffects.Move); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) ShowDetails(); if (e.Control && e.KeyCode == Keys.Up) MoveRequested?.Invoke(-1); if (e.Control && e.KeyCode == Keys.Down) MoveRequested?.Invoke(1); };
    }
    private static Button ActionButton(string text, EventHandler click)
    {
        var b = HudTheme.Button(text, click); b.Padding = new Padding(6, 1, 6, 1); b.Font = HudTheme.SmallFont; b.Margin = new Padding(0, 0, 6, 0); return b;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? HudTheme.Background);
        Color accent = row.Status == "Ready to claim" ? HudTheme.Gold : row.Eligible && !row.Hidden ? HudTheme.Accent : HudTheme.Muted;
        using (var shape = HudDrawing.Round(new Rectangle(0, 0, Width - 1, Height - 1), 5))
        {
            using var fill = new LinearGradientBrush(ClientRectangle, HudDrawing.Blend(HudTheme.Surface, accent, .035f), HudTheme.Surface, 90f);
            g.FillPath(fill, shape);
            using var pen = new Pen(Focused ? accent : row.Pinned ? HudDrawing.Blend(HudTheme.Border, accent, .5f) : HudTheme.Border); g.DrawPath(pen, shape);
        }
        using (var pen = new Pen(accent, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, 1, 12, 1, Height - 12);
        HudDrawing.DrawText(g, row.Title, titleFont, new Rectangle(14, 12, Width - 28, 25), row.Hidden ? HudTheme.Muted : HudTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        HudDrawing.DrawText(g, row.Status + "   ·   " + (row.Eligible ? "Eligible" : "Not eligible / refresh needed"), HudTheme.SmallFont, new Rectangle(14, 42, Width - 28, 20), accent, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        using (var bg = new Pen(HudTheme.Raised, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(bg, 16, 71, Width - 16, 71);
        float progress = (float)Math.Max(0, Math.Min(1, row.Progress));
        if (progress > 0) using (var fill = new Pen(accent, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(fill, 16, 71, 16 + (Width - 32) * progress, 71);
        var mainLines = row.Detail.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith("Rewards:", StringComparison.Ordinal) && !s.StartsWith("Find in ", StringComparison.Ordinal)).Take(5);
        HudDrawing.DrawText(g, string.Join(Environment.NewLine, mainLines), HudTheme.BodyFont, new Rectangle(14, 86, Width - 28, 92), HudTheme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        HudDrawing.DrawText(g, row.Rewards ?? "", HudTheme.SmallFont, new Rectangle(14, 179, Width - 28, 19), HudTheme.Gold, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
    private void ShowDetails()
    {
        using var form = new Form { Text = row.Title, StartPosition = FormStartPosition.CenterParent, Size = new Size(720, 540), MinimumSize = new Size(480, 360), BackColor = HudTheme.Background, ForeColor = HudTheme.Text, ShowInTaskbar = false };
        var text = new HudTextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = HudTheme.Surface, ForeColor = HudTheme.Text, Font = HudTheme.BodyFont, Text = row.Title + Environment.NewLine + row.Status + Environment.NewLine + Environment.NewLine + row.Detail, BorderStyle = BorderStyle.None };
        form.Padding = new Padding(20); form.Controls.Add(text); form.ShowDialog(FindForm());
    }
    protected override void Dispose(bool disposing) { if (disposing) { titleFont.Dispose(); tip.Dispose(); } base.Dispose(disposing); }
}
