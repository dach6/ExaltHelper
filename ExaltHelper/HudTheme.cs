using System;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ExaltHelper;

internal static class HudTheme
{
    public static bool IsLight => HudPreferences.Current.Theme == "Light";
    private static bool Light => IsLight;
    private static bool Slate => HudPreferences.Current.Theme == "Slate";
    public static Color Background => Light ? Color.FromArgb(242, 245, 249) : Slate ? Color.FromArgb(22, 28, 39) : Color.FromArgb(11, 14, 20);
    public static Color Surface => Light ? Color.White : Slate ? Color.FromArgb(29, 37, 50) : Color.FromArgb(18, 23, 33);
    public static Color Raised => Light ? Color.FromArgb(231, 237, 244) : Slate ? Color.FromArgb(39, 50, 66) : Color.FromArgb(25, 34, 51);
    public static Color Border => Light ? Color.FromArgb(205, 215, 228) : Color.FromArgb(30, 41, 59);
    public static Color Input => Light ? Color.FromArgb(248, 250, 253) : Color.FromArgb(22, 31, 48);
    public static Color InputBorder => Light ? Color.FromArgb(168, 185, 204) : Color.FromArgb(45, 59, 85);
    public static Color Slot => Light ? Color.FromArgb(235, 241, 247) : Color.FromArgb(19, 26, 41);
    public static Color Text => Light ? Color.FromArgb(27, 39, 53) : Color.FromArgb(226, 234, 245);
    public static Color Muted => Light ? Color.FromArgb(80, 101, 124) : Color.FromArgb(139, 156, 180);
    public static Color Accent
    {
        get { try { return ColorTranslator.FromHtml(HudPreferences.Current.AccentColor ?? "#00e5bf"); } catch (Exception ex) when (ex is ArgumentException || ex is FormatException) { return Color.FromArgb(0, 229, 191); } }
    }
    public static Color Gold => Light ? Color.FromArgb(153, 99, 10) : Color.FromArgb(246, 202, 119);
    public static Color Selection => Light ? Color.FromArgb(205, 235, 234) : Color.FromArgb(21, 51, 57);
    public static Color Alternate => Light ? Color.FromArgb(246, 248, 251) : Slate ? Color.FromArgb(26, 33, 46) : Color.FromArgb(16, 21, 31);
    public static Color Hover => Light ? Color.FromArgb(218, 232, 241) : Color.FromArgb(29, 44, 61);
    public static Color[] Palette => new[] { Background, Surface, Raised, Border, Text, Muted, Accent, Gold, Selection, Alternate, Hover, Input, InputBorder, Slot };
    public static readonly Font BodyFont = new Font("Segoe UI", 9.5f);
    public static readonly Font SmallFont = new Font("Segoe UI", 8.5f);
    public static readonly Font HeadingFont = new Font("Segoe UI Semibold", 17f);

    public static Button Button(string text, EventHandler click = null)
    {
        var button = new HudButton { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat, BackColor = Raised, ForeColor = Text, Font = BodyFont,
            Padding = HudPreferences.Current.CompactControls ? new Padding(8, 2, 8, 2) : new Padding(12, 5, 12, 5), Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Hover;
        if (click != null) button.Click += click;
        return button;
    }

    public static Label Label(string text, bool muted = false) => new Label { Text = text, AutoSize = true,
        ForeColor = muted ? Muted : Text, Font = BodyFont, Margin = new Padding(0, 4, 8, 4) };

    public static DataGridView Grid(params string[] columns)
    {
        var grid = new HudDataGridView { Dock = DockStyle.Fill, BackgroundColor = Surface, BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Border, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 40, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            Font = BodyFont };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Text,
            SelectionBackColor = Selection, SelectionForeColor = Text, Padding = new Padding(8, 2, 8, 2), NullValue = "—" };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Alternate;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Raised, ForeColor = Muted, Font = SmallFont,
            Padding = new Padding(8, 0, 8, 0), SelectionBackColor = Raised };
        grid.RowTemplate.Height = HudPreferences.Current.CompactControls ? 30 : 38;
        foreach (string name in columns)
        {
            int index = grid.Columns.Add(name, name);
            grid.Columns[index].HeaderText = name.ToUpperInvariant();
            grid.Columns[index].MinimumWidth = 55;
            grid.Columns[index].FillWeight = name == "Message" || name == "Event" || name == "Equipment" ? 240 : 100;
        }
        grid.SortCompare += (_, e) => {
            string a = Convert.ToString(e.CellValue1), b = Convert.ToString(e.CellValue2);
            if (decimal.TryParse(a.TrimEnd('%'), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal left) && decimal.TryParse(b.TrimEnd('%'), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal right))
            { e.SortResult = left.CompareTo(right); e.Handled = true; }
            else if ((e.Column.Name == "When" || e.Column.Name == "Time") && DateTime.TryParse(a, out DateTime first) && DateTime.TryParse(b, out DateTime second))
            { e.SortResult = first.CompareTo(second); e.Handled = true; }
        };
        return grid;
    }
    public static void Apply(Control root, Color[] previous)
    {
        Color[] next = Palette;
        Color Map(Color value) { int index = Array.FindIndex(previous, x => x.ToArgb() == value.ToArgb()); return index < 0 ? value : next[index]; }
        root.BackColor = Map(root.BackColor); root.ForeColor = Map(root.ForeColor);
        if (root is Form form) HudWindowChrome.Apply(form);
        if (root is Button button) { button.FlatAppearance.BorderColor = Border; button.FlatAppearance.MouseOverBackColor = Hover; button.Padding = HudPreferences.Current.CompactControls ? new Padding(8, 2, 8, 2) : new Padding(12, 5, 12, 5); }
        if (root is DataGridView grid)
        {
            grid.BackgroundColor = Surface; grid.GridColor = Border;
            // Some framework versions return a detached default style. Assign the
            // updated styles back so existing rows and future rows use the palette.
            var body = grid.DefaultCellStyle.Clone();
            body.BackColor = Surface; body.ForeColor = Text;
            body.SelectionBackColor = Selection; body.SelectionForeColor = Text;
            grid.DefaultCellStyle = body;
            var alternating = grid.AlternatingRowsDefaultCellStyle.Clone();
            alternating.BackColor = Alternate; alternating.ForeColor = Text;
            alternating.SelectionBackColor = Selection; alternating.SelectionForeColor = Text;
            grid.AlternatingRowsDefaultCellStyle = alternating;
            var header = grid.ColumnHeadersDefaultCellStyle.Clone();
            header.BackColor = Raised; header.ForeColor = Muted;
            header.SelectionBackColor = Raised; header.SelectionForeColor = Muted;
            grid.ColumnHeadersDefaultCellStyle = header;
            grid.RowTemplate.Height = HudPreferences.Current.CompactControls ? 30 : 38;
            foreach (DataGridViewRow row in grid.Rows) row.Height = grid.RowTemplate.Height;
        }
        foreach (Control child in root.Controls) Apply(child, previous);
        if (root is HudCombatTrackerPage combat) combat.RefreshAppearance();
        root.Invalidate();
    }
}

internal sealed class HudTableRow
{
    public object Key;
    public object StableKey;
    public object[] Cells;
    public string Search;
    public string Category;
    public string[] CategoryAliases = Array.Empty<string>();
    public DateTime Time;
    public string Tooltip;
    public HudTableRow(object key, params object[] cells) { Key = key; Cells = cells; Search = string.Join(" ", cells); }
}

internal sealed class HudTablePage : UserControl
{
    public readonly DataGridView Grid;
    public readonly TextBox SearchBox = new HudTextBox { Width = 260, SearchIcon = true, Placeholder = "Search records...", BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Font = HudTheme.BodyFont, Margin = new Padding(0, 3, 10, 0) };
    public readonly ComboBox Category = new HudComboBox { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 3, 10, 0) };
    public readonly ComboBox Period = new HudComboBox { Width = 135, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 3, 10, 0) };
    public readonly FlowLayoutPanel Toolbar = new HudFlowPanel { Dock = DockStyle.Top, Height = 64, WrapContents = false, AutoScroll = true };
    private readonly Label count = new Label { Dock = DockStyle.Bottom, Height = 28, ForeColor = HudTheme.Muted, TextAlign = ContentAlignment.MiddleLeft, Text = "No matching records. Observed game activity will appear here." };
    private List<HudTableRow> rows = new List<HudTableRow>();
    private string filterKey;
    private string preferredCategory;
    private bool settingFilters;
    private DateTime filterDate = DateTime.Today;
    public event Action<object> RowOpened;
    public HudTablePage(string hint, params string[] columns)
    {
        Dock = DockStyle.Fill; BackColor = HudTheme.Background; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        Grid = HudTheme.Grid(columns);
        var tip = new Label { Text = hint, Dock = DockStyle.Top, Height = 38, ForeColor = HudTheme.Muted, Padding = new Padding(0, 0, 0, 8) };
        SearchBox.AccessibleName = "Search records"; Category.AccessibleName = "Category filter"; Period.AccessibleName = "Date filter";
        Toolbar.Controls.Add(SearchBox); Toolbar.Controls.Add(Category); Toolbar.Controls.Add(Period);
        Category.Items.Add("All categories"); Category.SelectedIndex = 0;
        Period.Items.AddRange(new object[] { "All dates", "Today", "Last 7 days", "Last 30 days" }); Period.SelectedIndex = 0;
        Controls.Add(Grid); Controls.Add(count); Controls.Add(Toolbar); Controls.Add(tip);
        SearchBox.TextChanged += (_, __) => ApplyFilter(); Category.SelectedIndexChanged += (_, __) => FiltersChanged(); Period.SelectedIndexChanged += (_, __) => FiltersChanged();
        Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) RowOpened?.Invoke(Grid.Rows[e.RowIndex].Tag); };
        Grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter && Grid.CurrentRow != null) { RowOpened?.Invoke(Grid.CurrentRow.Tag); e.Handled = true; } };
    }
    public void BindPreferences(string key)
    {
        filterKey = key; settingFilters = true;
        if (HudPreferences.Current.TableCategories.TryGetValue(key, out string category)) preferredCategory = category;
        if (HudPreferences.Current.TablePeriods.TryGetValue(key, out string period) && Period.Items.Contains(period)) Period.SelectedItem = period;
        settingFilters = false;
    }
    private void FiltersChanged()
    {
        if (settingFilters) return;
        preferredCategory = Category.SelectedItem as string;
        if (filterKey != null)
        {
            HudPreferences.Current.TableCategories[filterKey] = preferredCategory ?? "All categories";
            HudPreferences.Current.TablePeriods[filterKey] = Period.SelectedItem as string ?? "All dates";
            HudPreferences.Current.Save();
        }
        ApplyFilter();
    }
    public void SetRows(IEnumerable<HudTableRow> values)
    {
        var nextRows = values.ToList();
        if (rows.Count == nextRows.Count && rows.Zip(nextRows, (a, b) => Equals(a.StableKey ?? a.Key, b.StableKey ?? b.Key)
            && a.Cells.SequenceEqual(b.Cells) && a.Search == b.Search && a.Category == b.Category
            && (a.CategoryAliases ?? Array.Empty<string>()).SequenceEqual(b.CategoryAliases ?? Array.Empty<string>()) && a.Time == b.Time && a.Tooltip == b.Tooltip).All(x => x))
        {
            rows = nextRows;
            var current = rows.Where(x => (x.StableKey ?? x.Key) != null).GroupBy(x => x.StableKey ?? x.Key).ToDictionary(x => x.Key, x => x.Last().Key);
            foreach (DataGridViewRow row in Grid.Rows) if (row.HeaderCell.Tag != null && current.TryGetValue(row.HeaderCell.Tag, out var value)) row.Tag = value;
            HudItemPresentation.Refresh(Grid);
            return;
        }
        rows = nextRows;
        string selected = preferredCategory ?? Category.SelectedItem as string;
        string[] categories = new[] { "All categories" }.Concat(rows.SelectMany(x => new[] { x.Category }.Concat(x.CategoryAliases ?? Array.Empty<string>())).Concat(new[] { selected }).Where(x => !string.IsNullOrEmpty(x) && x != "All categories").Distinct().OrderBy(x => x)).ToArray();
        if (!Category.Items.Cast<string>().SequenceEqual(categories))
        {
            settingFilters = true;
            Category.BeginUpdate(); Category.Items.Clear(); Category.Items.AddRange(categories);
            Category.SelectedItem = categories.Contains(selected) ? selected : categories[0]; Category.EndUpdate();
            settingFilters = false;
        }
        ApplyFilter();
    }
    public void RefreshDateFilter()
    {
        if (Period.SelectedIndex > 0 && filterDate != DateTime.Today) ApplyFilter();
    }
    public void ApplyFilter()
    {
        filterDate = DateTime.Today;
        object selected = Grid.CurrentRow?.HeaderCell.Tag;
        var sortColumn = Grid.SortedColumn;
        var sortDirection = Grid.SortOrder == SortOrder.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        int scroll = Grid.FirstDisplayedScrollingRowIndex;
        string query = SearchBox.Text.Trim();
        DateTime after = Period.SelectedIndex == 1 ? DateTime.Today : Period.SelectedIndex == 2 ? DateTime.Now.AddDays(-7) : Period.SelectedIndex == 3 ? DateTime.Now.AddDays(-30) : DateTime.MinValue;
        var filtered = rows.Where(x => (query.Length == 0 || (x.Search ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            && (Category.SelectedIndex <= 0 || x.Category == (string)Category.SelectedItem || (x.CategoryAliases ?? Array.Empty<string>()).Contains((string)Category.SelectedItem))
            && (x.Time == default || x.Time >= after)).ToList();
        // Keep native rows, selection and hover targets when live values change.
        // Recreating every row on each HP/DPS update caused repeated layout and
        // painting, even when only one cell needed new text.
        bool reuse = Grid.RowCount == filtered.Count;
        var targets = new List<DataGridViewRow>(filtered.Count);
        if (reuse && sortColumn == null)
        {
            for (int i = 0; i < filtered.Count; i++)
            {
                if (!Equals(Grid.Rows[i].HeaderCell.Tag, filtered[i].StableKey ?? filtered[i].Key)) { reuse = false; break; }
                targets.Add(Grid.Rows[i]);
            }
        }
        else if (reuse)
        {
            var existing = Grid.Rows.Cast<DataGridViewRow>().Where(x => x.HeaderCell.Tag != null)
                .GroupBy(x => x.HeaderCell.Tag).ToDictionary(x => x.Key, x => new Queue<DataGridViewRow>(x));
            foreach (var row in filtered)
            {
                object key = row.StableKey ?? row.Key;
                if (key == null || !existing.TryGetValue(key, out var matches) || matches.Count == 0) { reuse = false; break; }
                targets.Add(matches.Dequeue());
            }
        }
        Grid.SuspendLayout();
        try
        {
            bool sortChanged = !reuse;
            if (!reuse)
            {
                Grid.Rows.Clear(); targets.Clear();
                foreach (var row in filtered)
                {
                    var native = new DataGridViewRow(); native.CreateCells(Grid, row.Cells);
                    native.Height = Grid.RowTemplate.Height; targets.Add(native);
                }
            }
            for (int i = 0; i < filtered.Count; i++)
            {
                var row = filtered[i]; var native = targets[i];
                native.Tag = row.Key; native.HeaderCell.Tag = row.StableKey ?? row.Key;
                for (int cell = 0; cell < native.Cells.Count; cell++)
                {
                    object value = cell < row.Cells.Length ? row.Cells[cell] : null;
                    if (!Equals(native.Cells[cell].Value, value))
                    { native.Cells[cell].Value = value; if (sortColumn?.Index == cell) sortChanged = true; }
                    string tooltip = row.Tooltip ?? row.Search;
                    if (native.Cells[cell].ToolTipText != tooltip) native.Cells[cell].ToolTipText = tooltip;
                }
            }
            if (!reuse && targets.Count > 0) Grid.Rows.AddRange(targets.ToArray());
            if (sortColumn != null && sortChanged) Grid.Sort(sortColumn, sortDirection);
            if (!reuse && selected != null)
            {
                var selection = targets.FirstOrDefault(x => Equals(x.HeaderCell.Tag, selected));
                if (selection != null) Grid.CurrentCell = selection.Cells[0];
            }
            if (scroll >= 0 && scroll < Grid.RowCount && Grid.FirstDisplayedScrollingRowIndex != scroll) Grid.FirstDisplayedScrollingRowIndex = scroll;
            count.Text = Grid.RowCount == 0 ? "No matching records. Observed game activity will appear here." : Grid.RowCount.ToString("N0") + " records  ·  Double-click a row for details";
        }
        finally { Grid.ResumeLayout(); }
        HudItemPresentation.Refresh(Grid);
    }
}
