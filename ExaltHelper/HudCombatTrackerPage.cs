using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal sealed class HudCombatRow
{
    public string Key;
    // The history table previously tagged rows with HudFightRecord directly;
    // keep an Id projection so integrations can retain stable selection keys.
    public string Id => Fight?.Id;
    public HudFightRecord Fight;
    public PlayerDamageEntry Player;
    public int Rank;
    public bool Header, Total;
    public int[] Equipment = Array.Empty<int>();
}

internal sealed class HudCombatTrackerPage : UserControl
{
    internal readonly DataGridView Grid = HudTheme.Grid("Rank", "Player", "Damage", "DPS", "Share", "Hits", "Taken", "Guarded", "Gear", "Status");
    internal readonly HudFlowPanel Toolbar = new HudFlowPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, AutoScroll = true };
    internal readonly TextBox Search = new HudTextBox { Width = 238, SearchIcon = true, Placeholder = "Find monster or player…", AccessibleName = "Find monster or player" };
    internal readonly ComboBox StateFilter = new HudComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Encounter status" };
    internal readonly HashSet<string> ExpandedIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HudCombatBanner banner = new HudCombatBanner { Dock = DockStyle.Top, Height = 94 };
    private readonly Label footer = new Label { Dock = DockStyle.Bottom, Height = 29, ForeColor = HudTheme.Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Dictionary<string, int> encounterOrder = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> lastEquipment = new Dictionary<string, int[]>(StringComparer.Ordinal);
    private readonly Dictionary<string, HudCombatRow> rowModels = new Dictionary<string, HudCombatRow>(StringComparer.Ordinal);
    private List<HudFightRecord> encounters = new List<HudFightRecord>();
    private string scope;
    private bool seedExpansion = true, history;
    private readonly bool showHistoryButton;
    private readonly bool expandFirstEncounter;
    private string focusId;
    public event Action HistoryRequested;
    public event Action<string> DetailRequested;

    public HudCombatTrackerPage() : this(true, true) { }

    internal HudCombatTrackerPage(bool showHistoryButton, bool expandFirstEncounter)
    {
        this.showHistoryButton = showHistoryButton;
        this.expandFirstEncounter = expandFirstEncounter;
        Dock = DockStyle.Fill; BackColor = HudTheme.Background; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        HudGameSprites.Initialize();
        var tools = Toolbar;
        Search.Margin = new Padding(0, 3, 10, 0); StateFilter.Margin = new Padding(0, 3, 10, 0);
        StateFilter.Items.AddRange(new object[] { "All encounters", "Active", "Defeated" }); StateFilter.SelectedIndex = 0;
        tools.Controls.Add(Search); tools.Controls.Add(StateFilter);
        tools.Controls.Add(HudTheme.Button("Expand all", (_, __) => { foreach (var fight in Filtered()) ExpandedIds.Add(fight.Id); RefreshRows(); }));
        tools.Controls.Add(HudTheme.Button("Collapse all", (_, __) => { ExpandedIds.Clear(); seedExpansion = false; RefreshRows(); }));
        if (showHistoryButton) tools.Controls.Add(HudTheme.Button("History", (_, __) => HistoryRequested?.Invoke()));
        var columns = new HudFlowPanel { Dock = DockStyle.Top, Height = 39, WrapContents = false, AutoScroll = true };
        columns.Controls.Add(HudTheme.Label("SHOW", true));
        foreach (var option in new[] { Tuple.Create("DPS", "DPS", true), Tuple.Create("Hits", "Hits", true), Tuple.Create("Damage taken", "Taken", false), Tuple.Create("Guarded", "Guarded", false) })
        {
            string key = option.Item2;
            var check = new HudCheckBox { Text = option.Item1, Checked = option.Item3, AutoSize = true, Margin = new Padding(0, 5, 14, 0), ForeColor = HudTheme.Text, BackColor = HudTheme.Background };
            Grid.Columns[key].Visible = option.Item3;
            check.CheckedChanged += (_, __) => { Grid.Columns[key].Visible = check.Checked; Grid.Invalidate(); };
            columns.Controls.Add(check);
        }
        Grid.Columns["Gear"].Visible = true;
        columns.Controls.Add(HudTheme.Button("Encounter details", (_, __) => OpenSelected()));
        Grid.AccessibleName = "Monster encounters and individual damage rankings";
        Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        Grid.ColumnHeadersHeight = 34;
        int[] widths = { 46, 186, 116, 86, 76, 70, 100, 100, 190, 105 };
        for (int i = 0; i < widths.Length; i++)
        {
            var column = Grid.Columns[i]; column.MinimumWidth = Math.Min(widths[i], 55); column.Width = widths[i];
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.DefaultCellStyle.Alignment = i > 1 && i < 8 ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft;
        }
        Grid.Columns["Rank"].HeaderText = "#"; Grid.Columns["Share"].HeaderText = "% DMG";
        Grid.Columns["Taken"].HeaderText = "TAKEN"; Grid.Columns["Guarded"].HeaderText = "GUARDED";
        Grid.Columns["Share"].ToolTipText = "Share of damage tracked against this specific monster; not a percentage of its maximum HP.";
        Grid.Columns["Guarded"].ToolTipText = "Tracked damage dealt while the target was guarding.";
        Grid.Columns["Gear"].HeaderText = "GEAR (4)";
        Grid.Columns["Gear"].ToolTipText = "The complete four-slot equipment set last observed for this player. Hover an item for details.";
        Grid.CellClick += (_, e) => { if (e.RowIndex >= 0 && Grid.Rows[e.RowIndex].Tag is HudCombatRow row && row.Header) ToggleEncounter(row.Fight.Id); };
        // Player rows are already visible in the encounter expansion. A double click
        // must not be required to inspect contributors; the explicit details button
        // remains available for the recorded source breakdown.
        Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && Grid.Rows[e.RowIndex].Tag is HudCombatRow row && row.Header) ToggleEncounter(row.Fight.Id); };
        Grid.KeyDown += (_, e) => {
            if (!(Grid.CurrentRow?.Tag is HudCombatRow row)) return;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter && row.Header)
            {
                bool desired = e.KeyCode == Keys.Right || e.KeyCode != Keys.Left && !ExpandedIds.Contains(row.Fight.Id);
                if (desired) ExpandedIds.Add(row.Fight.Id); else ExpandedIds.Remove(row.Fight.Id);
                seedExpansion = false; RefreshRows(); e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = e.SuppressKeyPress = true; }
        };
        Grid.RowPrePaint += PaintEncounter;
        Grid.CellPainting += PaintPlayer;
        HudItemPresentation.Attach(Grid, "Gear", value => value is HudCombatRow row && row.Player != null
            ? CombatEquipment(row)
            : Enumerable.Empty<HudItemView>());
        Search.TextChanged += (_, __) => RefreshRows(); StateFilter.SelectedIndexChanged += (_, __) => RefreshRows();
        Controls.Add(Grid); Controls.Add(footer); Controls.Add(columns); Controls.Add(tools); Controls.Add(banner);
        RefreshRows();
    }

    private void NewScope(string value)
    {
        if (scope == value) return;
        scope = value; ExpandedIds.Clear(); encounterOrder.Clear(); lastEquipment.Clear(); rowModels.Clear();
        seedExpansion = expandFirstEncounter; Grid.Rows.Clear();
    }
    public void SetLive(DpsSnapshot snapshot, string accountScope, IEnumerable<HudFightRecord> saved)
    {
        if (snapshot == null) return;
        NewScope(accountScope + ":" + snapshot.DungeonStartTime.Ticks); history = false;
        var recorded = (saved ?? Enumerable.Empty<HudFightRecord>()).GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var all = (snapshot.Encounters ?? new List<EnemyCombatSnapshot>()).Concat(snapshot.DefeatedEnemies ?? new List<EnemyCombatSnapshot>());
        if (snapshot.ActiveEnemy != null) all = all.Concat(new[] { snapshot.ActiveEnemy });
        encounters = all.Where(e => e != null && (e.TotalDamage != 0 || e.Damagers.Count > 0 || e.MaxHp > 0))
            .GroupBy(e => e.EncounterId >= 0 ? e.EncounterId : e.EntityId).Select(g => {
                var enemy = g.First(); string id = HudJournal.FightKey(snapshot.DungeonStartTime, g.Key); recorded.TryGetValue(id, out var record);
                return new HudFightRecord { Id = id, Enemy = enemy.Name, ObjectType = enemy.ObjectType, MaxHp = enemy.MaxHp > 0 ? (int?)enemy.MaxHp : null,
                    CurrentHp = enemy.MaxHp > 0 ? (int?)enemy.CurrentHp : null, Defeated = enemy.IsDead, IsBoss = enemy.IsBoss,
                    EncounterEntityIds = enemy.EncounterEntityIds ?? Array.Empty<int>(), Seconds = enemy.FightDurationSeconds, DungeonSeconds = snapshot.ElapsedDungeonSeconds,
                    Damage = enemy.TotalDamage, Players = HydratePlayers(enemy.Damagers, snapshot.Players), Dungeon = snapshot.DungeonName, Time = snapshot.Timestamp,
                    DamageTaken = record?.DamageTaken ?? new Dictionary<string, long>(), Sources = record?.Sources ?? new List<HudDamageSource>(), EstimateNote = snapshot.DamageEstimateNote };
            }).ToList();
        focusId = snapshot.ActiveEnemy == null ? null : HudJournal.FightKey(snapshot.DungeonStartTime, snapshot.ActiveEnemy.EncounterId >= 0 ? snapshot.ActiveEnemy.EncounterId : snapshot.ActiveEnemy.EntityId);
        banner.Set(snapshot.DungeonName, snapshot.ElapsedDungeonSeconds, encounters, false);
        RefreshRows();
    }
    public void SetHistory(IEnumerable<HudFightRecord> fights, string dungeon)
    {
        var records = (fights ?? Enumerable.Empty<HudFightRecord>()).Where(f => f != null).ToList();
        string historyScope = "history:" + string.Join("|", records.Select(f => f.Id));
        // Journal snapshots are cloned on every refresh. Rebuild the lightweight
        // row models for history so callers receive the new record objects while
        // native grid rows, selection, and scroll position remain stable.
        if (scope == historyScope) rowModels.Clear();
        NewScope(historyScope); history = true; focusId = null;
        encounters = records; banner.Set(dungeon, records.Select(f => f.DungeonSeconds).DefaultIfEmpty().Max(), records, true); RefreshRows();
    }
    internal void ToggleEncounter(string id)
    {
        if (!ExpandedIds.Remove(id)) ExpandedIds.Add(id);
        seedExpansion = false; RefreshRows();
    }
    internal void RefreshAppearance() { RefreshRows(); banner.Invalidate(); }
    private void OpenSelected()
    {
        if (Grid.CurrentRow?.Tag is HudCombatRow row) DetailRequested?.Invoke(row.Fight.Id);
    }
    private IEnumerable<HudFightRecord> Filtered()
    {
        string query = Search.Text.Trim();
        return encounters.Where(f => (StateFilter.SelectedIndex != 1 || !f.Defeated) && (StateFilter.SelectedIndex != 2 || f.Defeated)
            && (query.Length == 0 || (f.Enemy ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || f.Players.Any(p => (p.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)))
            .OrderByDescending(f => f.IsBoss)
            .ThenBy(f => encounterOrder.TryGetValue(f.Id, out int order) ? order : int.MaxValue);
    }
    private void RefreshRows()
    {
        if (IsDisposed) return;
        foreach (var fight in encounters) if (!encounterOrder.ContainsKey(fight.Id)) encounterOrder[fight.Id] = encounterOrder.Count;
        if (seedExpansion && encounters.Count > 0)
        {
            ExpandedIds.Add(encounters.FirstOrDefault(f => f.Id == focusId)?.Id ?? encounters[0].Id); seedExpansion = false;
        }
        var visible = Filtered().ToArray(); var desired = new List<HudCombatRow>();
        HudCombatRow Row(string key, HudFightRecord fight, PlayerDamageEntry player = null, int rank = 0, bool header = false, bool total = false)
        {
            if (!rowModels.TryGetValue(key, out var row)) rowModels[key] = row = new HudCombatRow { Key = key };
            row.Fight = fight; row.Player = player; row.Rank = rank; row.Header = header; row.Total = total;
            if (player != null)
            {
                var incoming = NormalizeEquipment(player.EquipmentIds);
                if (!lastEquipment.TryGetValue(key, out var previous)) previous = Array.Empty<int>();
                if (incoming.Any(id => id > 0) || previous.Length == 0)
                    lastEquipment[key] = MergeEquipment(previous, incoming);
                row.Equipment = lastEquipment.TryGetValue(key, out var equipment) ? equipment : NormalizeEquipment(null);
            }
            return row;
        }
        foreach (var fight in visible)
        {
            desired.Add(Row(fight.Id + ":header", fight, header: true));
            if (!ExpandedIds.Contains(fight.Id)) continue;
            int rank = 0;
            foreach (var player in fight.Players.OrderByDescending(p => p.Damage).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                desired.Add(Row(fight.Id + ":player:" + (player.Name ?? "Unknown").ToUpperInvariant(), fight, player, ++rank));
            desired.Add(Row(fight.Id + ":total", fight, total: true));
        }
        var desiredKeys = new HashSet<string>(desired.Select(r => r.Key), StringComparer.Ordinal);
        foreach (string key in rowModels.Keys.Where(k => !desiredKeys.Contains(k)).ToArray()) rowModels.Remove(key);
        string selected = (Grid.CurrentRow?.Tag as HudCombatRow)?.Key;
        int oldFirst = Grid.FirstDisplayedScrollingRowIndex;
        string first = oldFirst >= 0 ? (Grid.Rows[oldFirst].Tag as HudCombatRow)?.Key : null;
        int horizontal = Grid.HorizontalScrollingOffset;
        Grid.SuspendLayout();
        try
        {
            var native = Grid.Rows.Cast<DataGridViewRow>().Where(r => r.Tag is HudCombatRow).ToDictionary(r => ((HudCombatRow)r.Tag).Key, StringComparer.Ordinal);
            for (int i = Grid.Rows.Count - 1; i >= 0; i--) if (!(Grid.Rows[i].Tag is HudCombatRow row) || !desiredKeys.Contains(row.Key)) Grid.Rows.RemoveAt(i);
            for (int i = 0; i < desired.Count; i++)
            {
                var model = desired[i];
                if (!native.TryGetValue(model.Key, out var row))
                { row = new DataGridViewRow(); row.CreateCells(Grid); Grid.Rows.Insert(i, row); }
                else if (row.Index != i) { Grid.Rows.Remove(row); Grid.Rows.Insert(i, row); }
                row = Grid.Rows[i]; // Access the unshared native row after insertion.
                row.Tag = model; row.Height = model.Header ? 70 : HudPreferences.Current.CompactControls ? 32 : 38;
                var player = model.Player; var fight = model.Fight;
                object[] cells;
                if (model.Header) cells = new object[] { ExpandedIds.Contains(fight.Id) ? "▼" : "▶", fight.Enemy, "", "", "", "", "", "", "", "" };
                else if (model.Total) cells = new object[] { "", "Tracked total", fight.Damage.ToString("N0"), Rate(fight.Damage, fight.Seconds), fight.Damage > 0 ? "100%" : "—",
                    fight.Players.Count > 0 && fight.Players.All(p => p.Hits.HasValue) ? fight.Players.Sum(p => p.Hits.Value).ToString("N0") : "—",
                    fight.DamageTaken.Count > 0 ? fight.DamageTaken.Values.Sum().ToString("N0") : "—", fight.Players.Sum(p => p.GuardDamage).ToString("N0"), "", "" };
                else cells = new object[] { model.Rank, player.Name, player.Damage.ToString("N0"), Rate(player.Damage, fight.Seconds), fight.Damage > 0 ? (100.0 * player.Damage / fight.Damage).ToString("0.0") + "%" : "—",
                    player.Hits?.ToString("N0") ?? "—", fight.DamageTaken.TryGetValue(player.Name ?? "", out long taken) ? taken.ToString("N0") : "—",
                    player.GuardDamage.ToString("N0"), string.Join(" · ", model.Equipment.Select((id, slot) => EquipmentName(id, slot))), player.IsLocalPlayer ? "You · " + player.Status : player.Status.ToString() };
                for (int c = 0; c < cells.Length; c++) if (!Equals(row.Cells[c].Value, cells[c])) row.Cells[c].Value = cells[c];
                row.Cells["Player"].ToolTipText = model.Header ? fight.Enemy + " · click or press Enter to expand/collapse" : player == null ? "Total damage recorded for this monster." : player.Name + " · " + player.ClassName;
                row.DefaultCellStyle.BackColor = player?.IsLocalPlayer == true ? HudTheme.Selection : model.Total ? HudTheme.Raised : i % 2 == 0 ? HudTheme.Surface : HudTheme.Alternate;
                row.DefaultCellStyle.ForeColor = player != null && player.Status != PlayerCombatStatus.Alive ? HudTheme.Muted : HudTheme.Text;
            }
            if (selected != null)
            {
                var row = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => ((HudCombatRow)r.Tag).Key == selected);
                if (row != null && Grid.CurrentRow != row) Grid.CurrentCell = row.Cells["Player"];
            }
            if (first != null)
            {
                var row = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => ((HudCombatRow)r.Tag).Key == first);
                if (row != null && Grid.FirstDisplayedScrollingRowIndex != row.Index) Grid.FirstDisplayedScrollingRowIndex = row.Index;
            }
            if (Grid.HorizontalScrollingOffset != horizontal) Grid.HorizontalScrollingOffset = horizontal;
        }
        finally { Grid.ResumeLayout(); }
        footer.Text = encounters.Count == 0 ? history ? "This saved run has no retained encounters." : "Damage to observed monsters will appear here when you enter combat."
            : visible.Length + " / " + encounters.Count + " encounters  ·  Click a monster to expand  ·  GEAR (4) shows the full set  ·  % DMG = share of tracked damage";
        HudItemPresentation.Refresh(Grid); Grid.Invalidate();
    }
    private static string Rate(long damage, double seconds) => seconds > 0 ? (damage / Math.Max(.5, seconds)).ToString("N0") : "—";
    private static string Duration(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    private static readonly string[] EquipmentLabels = { "Weapon", "Ability", "Armor", "Ring" };
    private static int[] NormalizeEquipment(int[] ids)
        => Enumerable.Range(0, 4).Select(i => ids != null && i < ids.Length ? ids[i] : -1).ToArray();
    private static int[] MergeEquipment(int[] previous, int[] incoming)
        => Enumerable.Range(0, 4).Select(i => incoming[i] > 0 ? incoming[i] : previous.Length == 4 ? previous[i] : -1).ToArray();
    private static string EquipmentName(int id, int slot)
        => id > 0 ? ItemSpriteManager.GetItemMetadata(id).Name : "Empty " + EquipmentLabels[slot].ToLowerInvariant();
    private static IEnumerable<HudItemView> CombatEquipment(HudCombatRow row)
        => row.Equipment.Length == 4
            ? row.Equipment.Select((id, slot) => HudItemPresentation.Create(id, id > 0 ? ItemSpriteManager.GetItemMetadata(id).Name : "Empty " + EquipmentLabels[slot].ToLowerInvariant(),
                id > 0 ? ItemSpriteManager.GetItemMetadata(id).Rarity : "Empty",
                details: EquipmentLabels[slot] + " · " + row.Player.Name + "\nLast observed in " + row.Fight.Enemy))
            : Enumerable.Range(0, 4).Select(slot => HudItemPresentation.Create(-1, "Empty " + EquipmentLabels[slot].ToLowerInvariant(), "Empty",
                details: EquipmentLabels[slot] + " · " + row.Player.Name + "\nNo equipment observation was received."));
    private static List<PlayerDamageEntry> HydratePlayers(IEnumerable<PlayerDamageEntry> source, IEnumerable<PlayerParseEntry> roster)
    {
        var byName = (roster ?? Enumerable.Empty<PlayerParseEntry>()).Where(p => !string.IsNullOrWhiteSpace(p?.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        return (source ?? Enumerable.Empty<PlayerDamageEntry>()).Where(p => p != null).Select(p => {
            var equipment = NormalizeEquipment(p.EquipmentIds);
            if (byName.TryGetValue(p.Name ?? "", out var observed) && observed.EquipmentIds != null && observed.EquipmentIds.Any(id => id > 0))
                equipment = MergeEquipment(equipment, NormalizeEquipment(observed.EquipmentIds));
            return new PlayerDamageEntry { Rank = p.Rank, Name = p.Name, ClassName = p.ClassName, ClassType = p.ClassType, SkinType = p.SkinType,
                EquipmentIds = equipment, Hits = p.Hits, Damage = p.Damage, Dps = p.Dps, Percentage = p.Percentage, IsLocalPlayer = p.IsLocalPlayer,
                GuardHits = p.GuardHits, GuardDamage = p.GuardDamage, Status = p.Status, LastHpPercent = p.LastHpPercent };
        }).ToList();
    }
    private void PaintEncounter(object sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (!(Grid.Rows[e.RowIndex].Tag is HudCombatRow row) || !row.Header) return;
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(e.ClipBounds, CombineMode.Intersect);
            var area = new Rectangle(0, e.RowBounds.Top, Grid.ClientSize.Width, e.RowBounds.Height);
            var fight = row.Fight;
            using (var fill = new SolidBrush(HudTheme.Raised)) e.Graphics.FillRectangle(fill, area);
            using (var line = new Pen(HudTheme.Border)) e.Graphics.DrawLine(line, area.Left, area.Bottom - 1, area.Right, area.Bottom - 1);
            HudDrawing.DrawText(e.Graphics, ExpandedIds.Contains(fight.Id) ? "▼" : "▶", HudTheme.BodyFont, new Rectangle(10, area.Top + 20, 22, 26), HudTheme.Accent, TextFormatFlags.VerticalCenter);
            HudGameSprites.Draw(e.Graphics, HudGameSprites.Object(fight.ObjectType) ?? HudGameSprites.Monster(fight.Enemy), new Rectangle(36, area.Top + 11, 42, 42));
            int hpWidth = Math.Min(220, Math.Max(140, area.Width / 4)); int textWidth = Math.Max(80, area.Width - hpWidth - 112);
            HudDrawing.DrawText(e.Graphics, fight.Enemy ?? "Unknown monster", HudTheme.BodyFont, new Rectangle(90, area.Top + 9, textWidth, 24), HudTheme.Text,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            int phases = fight.EncounterEntityIds?.Distinct().Count() ?? 0;
            string summary = (fight.IsBoss ? "BOSS · " : "") + (fight.Defeated ? "Defeated" : history ? "Observed" : "Active") + " · " + Duration(fight.Seconds) + " · " + fight.Damage.ToString("N0") + " damage · " + fight.Players.Count + " players";
            if (phases > 1) summary += " · " + phases + " phases";
            HudDrawing.DrawText(e.Graphics, summary, HudTheme.SmallFont, new Rectangle(90, area.Top + 36, textWidth, 22), HudTheme.Muted,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            var bar = new Rectangle(area.Right - hpWidth - 16, area.Top + 18, hpWidth, 10);
            using (var fill = new SolidBrush(HudTheme.Background)) e.Graphics.FillRectangle(fill, bar);
            double fraction = fight.MaxHp > 0 && fight.CurrentHp.HasValue ? Math.Max(0, Math.Min(1, (double)fight.CurrentHp.Value / fight.MaxHp.Value)) : 0;
            using (var fill = new SolidBrush(fight.Defeated ? HudTheme.Muted : HudTheme.Accent)) e.Graphics.FillRectangle(fill, bar.X, bar.Y, (int)(bar.Width * fraction), bar.Height);
            string hp = fight.MaxHp > 0 && fight.CurrentHp.HasValue ? Math.Max(0, fight.CurrentHp.Value).ToString("N0") + " / " + fight.MaxHp.Value.ToString("N0") + " HP" : "HP not observed";
            HudDrawing.DrawText(e.Graphics, hp, HudTheme.SmallFont, new Rectangle(bar.X, area.Top + 36, bar.Width, 22), HudTheme.Muted, TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (Grid.CurrentRow == Grid.Rows[e.RowIndex] && Grid.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(area, -2, -2), HudTheme.Accent, HudTheme.Raised);
        }
        finally { e.Graphics.Restore(state); }
        e.Handled = true;
    }
    private void PaintPlayer(object sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(Grid.Rows[e.RowIndex].Tag is HudCombatRow row) || row.Player == null) return;
        string column = Grid.Columns[e.ColumnIndex].Name;
        if (column != "Player" && column != "Damage") return;
        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(e.ClipBounds, CombineMode.Intersect);
            var text = Rectangle.Inflate(e.CellBounds, -8, 0);
            if (column == "Player")
            {
                HudGameSprites.Draw(e.Graphics, HudGameSprites.Player(row.Player), new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - 26) / 2, 26, 26), "players");
                text.X += 31; text.Width -= 31;
            }
            else
            {
                double share = row.Fight.Damage > 0 ? Math.Max(0, Math.Min(1, (double)row.Player.Damage / row.Fight.Damage)) : 0;
                using var fill = new SolidBrush(Color.FromArgb(HudTheme.IsLight ? 35 : 28, HudTheme.Accent));
                e.Graphics.FillRectangle(fill, text.Left, text.Bottom - 6, (int)(text.Width * share), 3);
            }
            HudDrawing.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), e.CellStyle.Font ?? Grid.Font, text, e.CellStyle.ForeColor,
                (column == "Player" ? TextFormatFlags.Left : TextFormatFlags.Right) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        finally { e.Graphics.Restore(state); }
        e.Handled = true;
    }
}

internal sealed class HudCombatBanner : Control
{
    private string dungeon = "Combat tracker", duration = "", totals = "", detail = "Enter combat to track each monster separately.";
    public HudCombatBanner() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    public void Set(string name, double seconds, IList<HudFightRecord> fights, bool history)
    {
        dungeon = string.IsNullOrWhiteSpace(name) ? "Combat tracker" : name;
        duration = TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss") + " dungeon time";
        totals = fights.Sum(f => f.Damage).ToString("N0") + " damage";
        detail = (history ? "RECORDED RUN" : "CURRENT DUNGEON") + " · " + fights.Count + " encounters · " + fights.SelectMany(f => f.Players).Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() + " players";
        AccessibleName = dungeon + ". " + totals + ". " + detail; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        using (var background = new SolidBrush(HudTheme.Background)) e.Graphics.FillRectangle(background, ClientRectangle);
        HudGameSprites.Draw(e.Graphics, HudGameSprites.Portal(dungeon), new Rectangle(4, 13, 50, 50), "live");
        int rightWidth = Math.Min(238, Width / 3);
        HudDrawing.DrawText(e.Graphics, dungeon, HudTheme.HeadingFont, new Rectangle(68, 5, Math.Max(60, Width - rightWidth - 80), 36), HudTheme.Text,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        HudDrawing.DrawText(e.Graphics, detail, HudTheme.SmallFont, new Rectangle(68, 45, Math.Max(60, Width - rightWidth - 80), 24), HudTheme.Muted,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        HudDrawing.DrawText(e.Graphics, totals, HudTheme.BodyFont, new Rectangle(Width - rightWidth, 14, rightWidth, 28), HudTheme.Accent, TextFormatFlags.Right | TextFormatFlags.SingleLine);
        HudDrawing.DrawText(e.Graphics, duration, HudTheme.SmallFont, new Rectangle(Width - rightWidth, 45, rightWidth, 24), HudTheme.Muted, TextFormatFlags.Right | TextFormatFlags.SingleLine);
    }
}
