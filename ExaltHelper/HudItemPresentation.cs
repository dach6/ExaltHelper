using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ExaltHelper;

// Icons are borrowed from ItemSpriteManager. Neither views nor grid bindings
// own these bitmaps, and disposing a page must never dispose a shared sprite.
internal sealed class HudItemView
{
    public int ItemId { get; }
    public string Name { get; }
    public string Rarity { get; }
    public string TierLabel { get; }
    public bool Shiny { get; }
    public string Tooltip { get; }
    public Bitmap Icon { get; }
    public string Caption => CaptionFor(Name, Shiny);
    internal static string CaptionFor(string name, bool shiny)
        => (shiny && name.IndexOf("shiny", StringComparison.OrdinalIgnoreCase) < 0 ? "Shiny " : "") + name;
    public string Badge => string.Join(" · ", new[] { TierLabel, Rarity, Shiny ? "Shiny" : "" }.Where(x => !string.IsNullOrWhiteSpace(x)));
    internal HudItemView(int id, string name, string rarity, string tier, bool shiny, Bitmap icon, string tooltip)
    { ItemId = id; Name = name; Rarity = rarity; TierLabel = tier; Shiny = shiny; Icon = icon; Tooltip = tooltip; }
    public override string ToString() => Caption;
}

internal static class HudItemPresentation
{
    private sealed class BaseItem
    {
        public string Name, Rarity, Tier, Details;
        public bool Known, Shiny;
        public Bitmap Icon;
    }
    private static readonly object CacheGate = new object();
    private static readonly Dictionary<int, BaseItem> cache = new Dictionary<int, BaseItem>();
    private static readonly Queue<int> cacheOrder = new Queue<int>();
    private static readonly Lazy<Task> loading = new Lazy<Task>(() => Task.Run(() => {
        ItemSpriteManager.InitializeAssets(); HudCalculatorData.EnsureLoaded(); LoadNameIndex();
    }));
    private static IReadOnlyDictionary<string, int> itemNames = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
    private static readonly ConditionalWeakTable<DataGridView, List<HudItemGridBinding>> bindings = new ConditionalWeakTable<DataGridView, List<HudItemGridBinding>>();
    private static readonly ConditionalWeakTable<DataGridView, HudItemGridToolTips> tooltips = new ConditionalWeakTable<DataGridView, HudItemGridToolTips>();
    private static readonly Font badgeFont = new Font("Segoe UI Semibold", 6.5f);
    internal const int MetadataLimit = 1024;
    public static Task WarmUpAsync() => loading.Value;
    public static int ResolveItemId(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || WarmUpAsync().Status != TaskStatus.RanToCompletion) return -1;
        return Volatile.Read(ref itemNames).TryGetValue(name.Trim(), out int id) ? id : -1;
    }
    private static void LoadNameIndex()
    {
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in HudCalculatorData.Items.Values.Where(x => x.Equipment).OrderBy(x => x.Id).Take(50000))
            foreach (string name in new[] { item.Name, item.ObjectName })
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 200) continue;
                string key = name.Trim();
                if (names.TryGetValue(key, out int prior)) { if (prior != item.Id) names[key] = -1; }
                else if (names.Count < 65536) names[key] = item.Id;
            }
        Volatile.Write(ref itemNames, new ReadOnlyDictionary<string, int>(names));
    }

    public static HudItemView Create(int itemId, string name = null, string rarity = null, bool shiny = false,
        string enchantments = null, string source = null, string details = null, string enchantCode = null)
    {
        Task ready = WarmUpAsync();
        bool loaded = ready.Status == TaskStatus.RanToCompletion;
        BaseItem item = loaded ? Metadata(itemId) : null;
        string display = Clean(name, 200);
        if (display.Length == 0) display = item?.Name ?? (itemId > 0 ? "Item #" + itemId : "Unknown item");
        bool isShiny = shiny || item?.Shiny == true || string.Equals(rarity, "Shiny", StringComparison.OrdinalIgnoreCase);
        string grade = Clean(rarity, 40);
        if (grade.Length == 0 || grade.Equals("Shiny", StringComparison.OrdinalIgnoreCase)) grade = item?.Rarity ?? "Unknown rarity";
        string tier = item?.Tier ?? "";
        var text = new StringBuilder(HudItemView.CaptionFor(display, isShiny));
        text.AppendLine().Append(string.Join(" · ", new[] { tier, grade }.Where(x => x.Length > 0)));
        if (!string.IsNullOrEmpty(item?.Details)) text.AppendLine().AppendLine().Append(item.Details);
        string enchants = Clean(enchantments, 2500);
        if (loaded && !string.IsNullOrWhiteSpace(enchantCode))
        {
            var parsed = AccountEnchantmentInfo.Parse(enchantCode);
            if (!string.IsNullOrWhiteSpace(parsed.Description)) enchants = string.Join("\n", new[] { enchants, parsed.Description }.Where(x => x.Length > 0).Distinct());
        }
        if (enchants.Length > 0) text.AppendLine().AppendLine().Append("Enchantments: ").Append(enchants);
        string origin = Clean(source, 1000), extra = Clean(details, 1500);
        if (origin.Length > 0) text.AppendLine().AppendLine().Append("Source: ").Append(origin);
        if (extra.Length > 0) text.AppendLine().Append(extra);
        if (!loaded) text.AppendLine().AppendLine().Append(ready.IsFaulted ? "Item details are unavailable." : "Loading item details…");
        else if (item?.Known != true) text.AppendLine().AppendLine().Append("Item stats have not been identified.");
        return new HudItemView(itemId, display, grade, tier, isShiny, item?.Icon, text.ToString());
    }

    public static HudItemGridBinding Attach(DataGridView grid, string columnName, Func<object, IEnumerable<HudItemView>> rowItems, bool showNames = true)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (rowItems == null) throw new ArgumentNullException(nameof(rowItems));
        if (!grid.Columns.Contains(columnName)) throw new ArgumentException("The item column does not exist.", nameof(columnName));
        var list = bindings.GetOrCreateValue(grid);
        foreach (var old in list.Where(x => x.ColumnName == columnName).ToArray()) old.Dispose();
        var binding = new HudItemGridBinding(grid, columnName, rowItems, showNames); list.Add(binding); return binding;
    }
    public static void Refresh(DataGridView grid)
    {
        if (grid != null && bindings.TryGetValue(grid, out var list)) foreach (var binding in list.ToArray()) binding.Refresh();
    }
    internal static void Detach(DataGridView grid, HudItemGridBinding binding)
    {
        if (!bindings.TryGetValue(grid, out var list)) return;
        list.Remove(binding);
        if (list.Count == 0 && tooltips.TryGetValue(grid, out var tips)) { tips.Dispose(); tooltips.Remove(grid); }
    }
    internal static HudItemGridToolTips ToolTips(DataGridView grid) => tooltips.GetValue(grid, value => new HudItemGridToolTips(value));
    internal static bool HasItemHint(DataGridView grid, int row, int column)
    {
        if (row < 0 || column < 0 || column >= grid.Columns.Count || !bindings.TryGetValue(grid, out var list)) return false;
        string name = grid.Columns[column].Name;
        foreach (var binding in list) if (binding.ColumnName == name) return binding.HasItemHint(grid.Rows[row], column);
        return false;
    }

    private static BaseItem Metadata(int id)
    {
        lock (CacheGate)
        {
            if (cache.TryGetValue(id, out var existing)) return existing;
            var meta = ItemSpriteManager.GetItemMetadata(id);
            var definition = id > 0 ? HudCalculatorData.Item(id) : null;
            string[] labels = meta.Labels ?? Array.Empty<string>();
            string tier = labels.Contains("ST") ? "ST" : labels.Contains("UT") ? "UT"
                : labels.FirstOrDefault(x => Regex.IsMatch(x ?? "", @"^T\d{1,2}$")) ?? "";
            bool known = id > 0 && (!string.IsNullOrWhiteSpace(meta.Name) && !meta.Name.StartsWith("Item #", StringComparison.Ordinal) || definition != null);
            var result = new BaseItem { Name = meta.Name, Rarity = known ? meta.Rarity : "Unknown rarity", Tier = tier,
                Shiny = meta.IsShiny, Known = known, Icon = id > 0 ? ItemSpriteManager.GetItemSprite(id) : null,
                Details = Describe(meta, definition) };
            cache[id] = result; cacheOrder.Enqueue(id);
            while (cache.Count > MetadataLimit) cache.Remove(cacheOrder.Dequeue());
            return result;
        }
    }
    private static string Describe(ItemSpriteManager.ItemMeta meta, HudCalculatorItem item)
    {
        var lines = new List<string>();
        if (meta.IsWeapon && meta.MaxDamage > 0)
        {
            lines.Add("Damage per projectile: " + meta.MinDamage + "–" + meta.MaxDamage);
            if (meta.NumProjectiles > 1) lines.Add("Projectiles: " + meta.NumProjectiles);
            if (Math.Abs(meta.RateOfFire - 1) > .001) lines.Add("Rate of fire: " + (meta.RateOfFire * 100).ToString("0.#", CultureInfo.CurrentCulture) + "%");
            if (meta.ArmorPiercing) lines.Add("Armor piercing");
        }
        if (item == null) return string.Join("\n", lines);
        var stats = new List<string>();
        foreach (var effect in item.EquipEffects)
        {
            string kind = effect.Value.Trim(); int index = HudCalculatorData.StatIndex((string)effect.Attribute("stat"));
            if (kind == "IncrementStat" && index >= 0) stats.Add(Signed(HudCalculatorData.Attr(effect, "amount")) + " " + HudCalculatorEngine.StatNames[index]);
            else lines.Add("Equipped: " + Effect(effect));
        }
        if (stats.Count > 0) lines.Insert(0, string.Join("   ", stats));
        if (item.XpBonus != 0) lines.Add("XP bonus: " + Signed(item.XpBonus) + "%");
        if (item.IsWeapon && item.Projectiles.TryGetValue(0, out var projectile))
        {
            double speed = HudCalculatorData.Value(projectile.Element("Speed")), lifetime = HudCalculatorData.Value(projectile.Element("LifetimeMS"));
            if (speed > 0 && lifetime > 0) lines.Add("Projectile range: " + (speed * lifetime / 10000).ToString("0.##", CultureInfo.CurrentCulture) + " tiles");
        }
        if (item.IsAbility || !item.Equipment)
            foreach (var mode in item.Modes.Take(4))
            {
                var costs = new List<string>();
                if (mode.MpCost > 0) costs.Add(mode.MpCost.ToString("0.#", CultureInfo.CurrentCulture) + " MP");
                if (mode.Cooldown > 0) costs.Add(mode.Cooldown.ToString("0.##", CultureInfo.CurrentCulture) + "s cooldown");
                if (costs.Count > 0) lines.Add((mode.Name == "Primary" ? "Ability" : Clean(mode.Name, 100)) + ": " + string.Join(" · ", costs));
                foreach (var effect in mode.Effects.Take(5)) lines.Add("On use: " + Effect(effect));
            }
        foreach (var proc in item.Procs.Take(3)) lines.Add((proc.Name.LocalName == "OnEnemyHitActivate" ? "On hit: " : "On shooting: ") + Effect(proc));
        foreach (var effect in item.ConditionalEffects.Take(3)) lines.Add("Conditional: " + Effect(effect));
        return Clean(string.Join("\n", lines.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(18)), 2500);
    }
    private static string Effect(XElement effect)
    {
        string kind = effect.Value.Trim();
        if (kind == "AbilityUseDiscount") return "MP cost ×" + HudCalculatorData.Attr(effect, "multiplier", 1).ToString("0.##", CultureInfo.CurrentCulture);
        if (kind == "IncrementStat")
        {
            int index = HudCalculatorData.StatIndex((string)effect.Attribute("stat"));
            return Signed(HudCalculatorData.Attr(effect, "amount")) + " " + (index >= 0 ? HudCalculatorEngine.StatNames[index] : "stat");
        }
        string title = Regex.Replace(Clean(kind, 100), "([a-z])([A-Z])", "$1 $2");
        var values = new List<string>();
        foreach (string key in new[] { "effect", "amount", "damage", "totalDamage", "radius", "duration", "range", "healAmount", "maxTargets" })
        {
            var value = effect.Attribute(key); if (value == null) continue;
            string label = key == "effect" ? "" : key == "totalDamage" ? "base damage " : key == "healAmount" ? "healing " : key == "maxTargets" ? "targets " : key + " ";
            values.Add(label + Clean(value.Value, 80) + (key == "duration" ? "s" : ""));
        }
        if (effect.Attribute("scalingStat") != null) values.Add("scales with " + Clean((string)effect.Attribute("scalingStat"), 30));
        return title + (values.Count == 0 ? "" : " — " + string.Join(", ", values.Take(6)));
    }
    private static string Signed(double number) => number.ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture);
    private static string Clean(string text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return text.Length <= maximum ? text : text.Substring(0, maximum - 1) + "…";
    }
    public static Color RarityColor(HudItemView item)
    {
        bool light = HudTheme.IsLight;
        switch ((item?.Rarity ?? "").ToLowerInvariant())
        {
            case "divine": return light ? Color.FromArgb(146, 104, 9) : Color.FromArgb(246, 202, 119);
            case "legendary": return light ? Color.FromArgb(120, 61, 166) : Color.FromArgb(197, 147, 250);
            case "rare": return light ? Color.FromArgb(23, 106, 168) : Color.FromArgb(96, 190, 245);
            case "uncommon": return light ? Color.FromArgb(27, 115, 63) : Color.FromArgb(109, 214, 147);
            default: return item?.TierLabel == "ST" ? light ? Color.FromArgb(157, 86, 21) : Color.FromArgb(243, 180, 99) : HudTheme.Muted;
        }
    }
    internal static int IconSize(Rectangle bounds) => Math.Max(12, Math.Min(32, bounds.Height - 6));
    internal static int VisibleIcons(Rectangle bounds, int count) => Math.Max(1, Math.Min(count, Math.Max(1, (bounds.Width - 10) / (IconSize(bounds) + 6))));
    internal static Rectangle IconBounds(Rectangle cell, int index)
    {
        int size = IconSize(cell); return new Rectangle(cell.Left + 6 + index * (size + 6), cell.Top + (cell.Height - size) / 2, size, size);
    }
    internal static void DrawCell(Graphics graphics, Rectangle bounds, HudItemView[] items, bool showNames, Font font, Color foreground)
    {
        if (items.Length == 0 || bounds.Width < 12 || bounds.Height < 12) return;
        var state = graphics.Save(); graphics.SetClip(bounds, CombineMode.Intersect);
        try
        {
            if (items.Length == 1)
            {
                var item = items[0]; var icon = IconBounds(bounds, 0); DrawIcon(graphics, icon, item);
                var text = new Rectangle(icon.Right + 8, bounds.Top + 2, Math.Max(0, bounds.Right - icon.Right - 14), bounds.Height - 4);
                bool nameVisible = showNames || item.Icon == null;
                if (text.Width < 10) return;
                if (nameVisible && bounds.Height >= 36)
                {
                    HudDrawing.DrawText(graphics, item.Caption, font, new Rectangle(text.X, text.Y, text.Width, text.Height / 2 + 2), foreground,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    HudDrawing.DrawText(graphics, item.Badge, HudTheme.SmallFont, new Rectangle(text.X, text.Y + text.Height / 2, text.Width, text.Height / 2), RarityColor(item),
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else HudDrawing.DrawText(graphics, nameVisible ? item.Caption : item.Badge, font, text, nameVisible ? foreground : RarityColor(item),
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }
            int visible = VisibleIcons(bounds, items.Length), actual = visible < items.Length ? Math.Max(0, visible - 1) : visible;
            for (int i = 0; i < actual; i++) DrawIcon(graphics, IconBounds(bounds, i), items[i]);
            if (actual < items.Length)
            {
                var rectangle = IconBounds(bounds, actual);
                using var fill = new SolidBrush(HudTheme.Raised); graphics.FillRectangle(fill, rectangle);
                HudDrawing.DrawText(graphics, "+" + (items.Length - actual), HudTheme.SmallFont, rectangle, foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
        finally { graphics.Restore(state); }
    }
    public static void DrawIcon(Graphics graphics, Rectangle bounds, HudItemView item)
    {
        if (item == null || bounds.Width < 4 || bounds.Height < 4) return;
        var state = graphics.Save();
        try
        {
            graphics.SetClip(bounds, CombineMode.Intersect); graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = RarityColor(item);
            using var shape = HudControlPaint.Rounded(new RectangleF(bounds.X + .5f, bounds.Y + .5f, bounds.Width - 1, bounds.Height - 1), 5);
            using (var fill = new SolidBrush(HudControlPaint.Mix(HudTheme.Slot, color, .06))) graphics.FillPath(fill, shape);
            using (var border = new Pen(color)) graphics.DrawPath(border, shape);
            var imageBounds = Rectangle.Inflate(bounds, -3, -3);
            if (item.Icon != null)
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
                double scale = Math.Min(imageBounds.Width / (double)item.Icon.Width, imageBounds.Height / (double)item.Icon.Height);
                var target = new Rectangle(imageBounds.X + (imageBounds.Width - (int)(item.Icon.Width * scale)) / 2,
                    imageBounds.Y + (imageBounds.Height - (int)(item.Icon.Height * scale)) / 2,
                    Math.Max(1, (int)(item.Icon.Width * scale)), Math.Max(1, (int)(item.Icon.Height * scale)));
                graphics.DrawImage(item.Icon, target);
            }
            else
            {
                string initials = string.Concat(item.Name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0])).ToUpperInvariant();
                HudDrawing.DrawText(graphics, initials.Length == 0 ? "?" : initials, HudTheme.SmallFont, imageBounds, HudTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            if (item.TierLabel.Length > 0 && bounds.Height >= 22)
            {
                var badge = new Rectangle(bounds.Right - 20, bounds.Bottom - 10, 19, 9);
                using var fill = new SolidBrush(HudTheme.Surface); graphics.FillRectangle(fill, badge);
                HudDrawing.DrawText(graphics, item.TierLabel, badgeFont, badge, color,
                    TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            if (item.Shiny)
            {
                float x = bounds.Right - 5, y = bounds.Top + 5;
                using var shine = new Pen(HudTheme.Gold, 1.4f);
                graphics.DrawLine(shine, x - 3, y, x + 3, y); graphics.DrawLine(shine, x, y - 3, x, y + 3);
            }
        }
        finally { graphics.Restore(state); }
    }
}

// Disabling the native grid tooltip is essential: setting ToolTipText to empty
// still permits Windows to show its own tooltip for clipped cell content.
// One coordinator preserves non-item hints and avoids competing item bindings.
internal sealed class HudItemGridToolTips : IDisposable
{
    private readonly DataGridView grid;
    private readonly bool nativeTips;
    internal readonly HudItemToolTip Tip;
    private object active;
    private DataGridViewCell fallbackCell;
    private bool disposed;
    internal HudItemGridToolTips(DataGridView value)
    {
        grid = value; nativeTips = grid.ShowCellToolTips; grid.ShowCellToolTips = false;
        Tip = new HudItemToolTip(grid);
        grid.MouseMove += MouseMoved; grid.MouseLeave += Changed; grid.Scroll += Scrolled;
        grid.ColumnWidthChanged += ColumnChanged; grid.CellValueChanged += CellChanged;
        grid.RowsAdded += RowsAdded; grid.RowsRemoved += RowsRemoved; grid.Sorted += Changed;
        grid.SizeChanged += Changed; grid.Disposed += GridDisposed;
    }
    internal void Show(object source, HudItemView item, string text, Point anchor, bool immediate = false)
    {
        if (disposed) return;
        if (!ReferenceEquals(active, source)) Tip.Hide();
        active = source; Tip.Show(item, text, anchor, immediate);
    }
    internal void Hide(object source)
    { if (!disposed && ReferenceEquals(active, source)) { Tip.Hide(); active = null; } }
    internal bool IsActive(object source) => !disposed && ReferenceEquals(active, source);
    private void MouseMoved(object sender, MouseEventArgs e)
    {
        if (disposed || !nativeTips) return;
        var hit = grid.HitTest(e.X, e.Y);
        if (hit.ColumnIndex < 0 || HudItemPresentation.HasItemHint(grid, hit.RowIndex, hit.ColumnIndex)) { Hide(this); fallbackCell = null; return; }
        DataGridViewCell cell = hit.RowIndex < 0 ? grid.Columns[hit.ColumnIndex].HeaderCell : grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
        if (ReferenceEquals(fallbackCell, cell)) return;
        fallbackCell = cell; string text = cell.ToolTipText;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = Convert.ToString(cell.FormattedValue) ?? "";
            var bounds = grid.GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, false);
            var style = cell.InheritedStyle; var padding = style.Padding;
            var available = new Size(Math.Max(1, bounds.Width - padding.Horizontal - 6), Math.Max(1, bounds.Height - padding.Vertical - 4));
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            if (style.WrapMode == DataGridViewTriState.True) flags |= TextFormatFlags.WordBreak;
            else flags |= TextFormatFlags.SingleLine;
            Size measured = TextRenderer.MeasureText(text, style.Font ?? grid.Font, new Size(available.Width, 10000), flags);
            if (measured.Width <= available.Width && measured.Height <= available.Height) text = "";
        }
        if (text.Length == 0) Hide(this);
        else Show(this, null, text, new Point(e.X + 14, e.Y + 20));
    }
    private void Reset()
    {
        if (active is HudItemGridBinding binding) binding.HideTip();
        fallbackCell = null; active = null; Tip.Hide();
    }
    private void Changed(object sender, EventArgs e) { Reset(); }
    private void Scrolled(object sender, ScrollEventArgs e) { Reset(); }
    private void ColumnChanged(object sender, DataGridViewColumnEventArgs e) { Reset(); }
    private void CellChanged(object sender, DataGridViewCellEventArgs e)
    { if (fallbackCell != null && e.ColumnIndex == fallbackCell.ColumnIndex && e.RowIndex == fallbackCell.RowIndex) Reset(); }
    private void RowsAdded(object sender, DataGridViewRowsAddedEventArgs e) { Reset(); }
    private void RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e) { Reset(); }
    private void GridDisposed(object sender, EventArgs e) { Dispose(); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; Tip.Dispose(); active = null; fallbackCell = null;
        grid.MouseMove -= MouseMoved; grid.MouseLeave -= Changed; grid.Scroll -= Scrolled;
        grid.ColumnWidthChanged -= ColumnChanged; grid.CellValueChanged -= CellChanged;
        grid.RowsAdded -= RowsAdded; grid.RowsRemoved -= RowsRemoved; grid.Sorted -= Changed;
        grid.SizeChanged -= Changed; grid.Disposed -= GridDisposed;
        if (!grid.IsDisposed) grid.ShowCellToolTips = nativeTips;
    }
}

// A binding holds only a bounded set of visible/prepared rows. The selector is
// invoked from BeginInvoke, never inside CellPainting or tooltip drawing.
internal sealed class HudItemGridBinding : IDisposable
{
    private sealed class Prepared
    {
        public object Tag;
        public string Value;
        public HudItemView[] Items;
        public long Used;
    }
    private readonly DataGridView grid;
    private readonly Func<object, IEnumerable<HudItemView>> rowItems;
    private readonly bool showNames;
    private readonly Dictionary<DataGridViewRow, Prepared> prepared = new Dictionary<DataGridViewRow, Prepared>();
    private readonly HudItemGridToolTips tooltip;
    private bool disposed;
    private int refreshQueued;
    private long clock;
    private DataGridViewRow hoverRow;
    private int hoverIndex = -1;
    private Point hoverPoint;
    private HudItemView hoverItem;
    private string hoverText = "";
    public string ColumnName { get; }
    internal int PreparedCount => prepared.Count;
    internal const int PreparedLimit = 256;
    internal HudItemGridBinding(DataGridView value, string column, Func<object, IEnumerable<HudItemView>> selector, bool names)
    {
        grid = value; ColumnName = column; rowItems = selector; showNames = names; tooltip = HudItemPresentation.ToolTips(grid);
        grid.CellPainting += Paint; grid.Scroll += Scrolled; grid.RowsAdded += RowsAdded; grid.RowsRemoved += RowsRemoved;
        grid.CellValueChanged += CellChanged; grid.Sorted += Changed; grid.VisibleChanged += Changed; grid.HandleCreated += Changed;
        grid.ColumnWidthChanged += ColumnChanged; grid.MouseMove += MouseMoved; grid.MouseLeave += MouseLeft;
        grid.KeyDown += KeyPressed; grid.Disposed += GridDisposed;
        HudItemPresentation.WarmUpAsync().ContinueWith(_ => Refresh(), TaskScheduler.Default); Refresh();
    }
    public void Refresh()
    {
        if (disposed || grid.IsDisposed || !grid.IsHandleCreated || Interlocked.Exchange(ref refreshQueued, 1) != 0) return;
        try { grid.BeginInvoke(new Action(() => { Interlocked.Exchange(ref refreshQueued, 0); if (!disposed && !grid.IsDisposed) PrepareVisible(); })); }
        catch (InvalidOperationException) { Interlocked.Exchange(ref refreshQueued, 0); }
    }
    private int Column => grid.Columns.Contains(ColumnName) ? grid.Columns[ColumnName].Index : -1;
    private void PrepareVisible()
    {
        int column = Column;
        if (!grid.Visible || column < 0) return;
        if (grid.Rows.Count == 0) { prepared.Clear(); HideTip(); return; }
        int first = Math.Max(0, grid.FirstDisplayedScrollingRowIndex);
        int count = Math.Min(128, Math.Max(1, grid.DisplayedRowCount(false)) + 4);
        var targets = Enumerable.Range(first, Math.Min(count, grid.Rows.Count - first)).Select(i => grid.Rows[i]).ToList();
        if (grid.CurrentCell?.OwningRow != null && !targets.Contains(grid.CurrentCell.OwningRow)) targets.Add(grid.CurrentCell.OwningRow);
        foreach (var row in targets)
        {
            if (row.IsNewRow || row.Tag == null) continue;
            var items = (rowItems(row.Tag) ?? Enumerable.Empty<HudItemView>()).Where(x => x != null).Take(64).ToArray();
            string value = Convert.ToString(row.Cells[column].Value) ?? "";
            bool changed = !prepared.TryGetValue(row, out var old) || !ReferenceEquals(old.Tag, row.Tag) || old.Value != value
                || old.Items.Length != items.Length || !old.Items.Zip(items, (a, b) => a.ItemId == b.ItemId && a.Tooltip == b.Tooltip && ReferenceEquals(a.Icon, b.Icon)).All(x => x);
            prepared[row] = new Prepared { Tag = row.Tag, Value = value, Items = items, Used = ++clock };
            if (changed) { if (ReferenceEquals(hoverRow, row)) HideTip(); grid.InvalidateCell(column, row.Index); }
        }
        foreach (var row in prepared.Keys.Where(x => x.DataGridView != grid || x.Index < 0).ToArray()) prepared.Remove(row);
        while (prepared.Count > PreparedLimit) prepared.Remove(prepared.OrderBy(x => x.Value.Used).First().Key);
    }
    private Prepared Ready(DataGridViewRow row, int column)
    {
        if (prepared.TryGetValue(row, out var value) && ReferenceEquals(value.Tag, row.Tag) && value.Value == (Convert.ToString(row.Cells[column].Value) ?? ""))
        { value.Used = ++clock; return value; }
        Refresh(); return null;
    }
    internal bool HasItemHint(DataGridViewRow row, int column)
    {
        if (row.Tag == null) return false;
        var entry = Ready(row, column);
        return entry == null || entry.Items.Length > 0;
    }
    private void Paint(object sender, DataGridViewCellPaintingEventArgs e)
    {
        int column = Column; if (disposed || e.RowIndex < 0 || e.ColumnIndex != column || column < 0) return;
        var entry = Ready(grid.Rows[e.RowIndex], column); if (entry == null || entry.Items.Length == 0) return;
        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(e.ClipBounds, CombineMode.Intersect);
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            HudItemPresentation.DrawCell(e.Graphics, e.CellBounds, entry.Items, showNames, e.CellStyle.Font ?? grid.Font,
                selected ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor);
        }
        finally { e.Graphics.Restore(state); }
        e.Handled = true;
    }
    private void MouseMoved(object sender, MouseEventArgs e)
    {
        var hit = grid.HitTest(e.X, e.Y); int column = Column;
        if (hit.RowIndex < 0 || hit.ColumnIndex != column || column < 0) { HideTip(); return; }
        var row = grid.Rows[hit.RowIndex]; var entry = Ready(row, column);
        if (entry == null || entry.Items.Length == 0) { HideTip(); return; }
        var bounds = grid.GetCellDisplayRectangle(column, hit.RowIndex, false);
        int index = entry.Items.Length == 1 ? 0 : (e.X - bounds.Left - 6) / (HudItemPresentation.IconSize(bounds) + 6);
        int visible = HudItemPresentation.VisibleIcons(bounds, entry.Items.Length);
        if (index < 0 || index >= visible || !bounds.Contains(e.Location)
            || (entry.Items.Length > 1 && !HudItemPresentation.IconBounds(bounds, index).Contains(e.Location))) { HideTip(); return; }
        if (ReferenceEquals(hoverRow, row) && hoverIndex == index && tooltip.IsActive(this)) return;
        HideTip(); hoverRow = row; hoverIndex = index; hoverPoint = new Point(e.X + 14, e.Y + 20);
        if (visible < entry.Items.Length && index == visible - 1)
        {
            hoverItem = null; hoverText = "Additional items\n\n" + string.Join("\n\n", entry.Items.Skip(index).Select(x => x.Tooltip));
        }
        else { hoverItem = entry.Items[index]; hoverText = hoverItem.Tooltip; }
        tooltip.Show(this, hoverItem, hoverText, hoverPoint);
    }
    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { HideTip(); return; }
        if (e.KeyCode != Keys.F1 || grid.CurrentCell == null || grid.CurrentCell.ColumnIndex != Column) return;
        int column = Column; var row = grid.CurrentCell.OwningRow; var entry = Ready(row, column);
        if (entry == null || entry.Items.Length == 0) return;
        HideTip(); hoverRow = row; hoverIndex = 0; hoverItem = entry.Items.Length == 1 ? entry.Items[0] : null;
        hoverText = string.Join("\n\n", entry.Items.Select(x => x.Tooltip));
        var bounds = grid.GetCellDisplayRectangle(column, row.Index, false); hoverPoint = new Point(bounds.Left + 14, bounds.Bottom);
        tooltip.Show(this, hoverItem, hoverText, hoverPoint, true); e.Handled = true; e.SuppressKeyPress = true;
    }
    internal void HideTip() { if (disposed) return; tooltip.Hide(this); hoverRow = null; hoverIndex = -1; hoverItem = null; hoverText = ""; }
    private void Scrolled(object sender, ScrollEventArgs e) { HideTip(); Refresh(); }
    private void RowsAdded(object sender, DataGridViewRowsAddedEventArgs e) => Refresh();
    private void RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e) { HideTip(); Refresh(); }
    private void CellChanged(object sender, DataGridViewCellEventArgs e) { if (e.ColumnIndex == Column) Refresh(); }
    private void ColumnChanged(object sender, DataGridViewColumnEventArgs e) { HideTip(); Refresh(); }
    private void Changed(object sender, EventArgs e) { HideTip(); Refresh(); }
    private void MouseLeft(object sender, EventArgs e) => HideTip();
    private void GridDisposed(object sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (disposed) return; HideTip(); disposed = true;
        prepared.Clear();
        grid.CellPainting -= Paint; grid.Scroll -= Scrolled; grid.RowsAdded -= RowsAdded; grid.RowsRemoved -= RowsRemoved;
        grid.CellValueChanged -= CellChanged; grid.Sorted -= Changed; grid.VisibleChanged -= Changed; grid.HandleCreated -= Changed;
        grid.ColumnWidthChanged -= ColumnChanged; grid.MouseMove -= MouseMoved; grid.MouseLeave -= MouseLeft;
        grid.KeyDown -= KeyPressed; grid.Disposed -= GridDisposed;
        HudItemPresentation.Detach(grid, this);
    }
}
