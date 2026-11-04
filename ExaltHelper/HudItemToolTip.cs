using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExaltHelper;

// One renderer and one timer per owner. Sprites and theme fonts are borrowed.
internal sealed class HudItemToolTip : IDisposable
{
    private readonly Control owner;
    private readonly ToolTip tooltip = new ToolTip { OwnerDraw = true, ShowAlways = false, AutoPopDelay = 20000, InitialDelay = 350, ReshowDelay = 150, UseAnimation = false, UseFading = false };
    private readonly Timer timer = new Timer { Interval = 350 };
    private HudItemView item;
    private string text = "", body = "", priorDescription;
    private Point anchor;
    private bool disposed, pending, visible, ownsDescription;
    private int surfaceColor, textColor, borderColor;
    internal string Text => text;
    internal HudItemView Item => item;
    internal bool IsPending => pending;
    internal bool IsVisible => visible;

    internal HudItemToolTip(Control value)
    {
        owner = value ?? throw new ArgumentNullException(nameof(value));
        tooltip.Popup += Popup; tooltip.Draw += ToolTipDraw; timer.Tick += Tick;
        owner.MouseLeave += OwnerLeft; owner.MouseDown += OwnerLeft; owner.VisibleChanged += VisibilityChanged;
        owner.HandleDestroyed += OwnerLeft; owner.Disposed += OwnerDisposed; owner.KeyDown += KeyPressed;
        owner.Invalidated += OwnerInvalidated; owner.BackColorChanged += AppearanceChanged; owner.ForeColorChanged += AppearanceChanged;
    }

    internal void Show(HudItemView value, string content, Point location, bool immediate = false)
    {
        if (disposed) return;
        if (string.IsNullOrWhiteSpace(content) || owner.IsDisposed || !owner.Visible) { Hide(); return; }
        if (ReferenceEquals(item, value) && text == content)
        {
            if (immediate && !visible) { anchor = location; Display(); }
            return;
        }
        bool reshow = visible;
        Hide(); item = value; text = content; anchor = location; body = Body(value, content);
        priorDescription = owner.AccessibleDescription; owner.AccessibleDescription = text; ownsDescription = true;
        if (immediate) Display();
        else { pending = true; timer.Interval = reshow ? 150 : 350; timer.Start(); }
    }

    private static string Body(HudItemView value, string content)
    {
        // Inventory hints can put slot/context lines before the item description.
        // Remove the duplicate caption and grade only from the known item block.
        if (value == null || string.IsNullOrEmpty(value.Tooltip)) return content;
        int start = content.IndexOf(value.Tooltip, StringComparison.Ordinal);
        int first = value.Tooltip.IndexOf('\n');
        int second = first < 0 ? -1 : value.Tooltip.IndexOf('\n', first + 1);
        if (start < 0 || first < 0) return content;
        int headingLength = second < 0 ? value.Tooltip.Length : second + 1;
        return (content.Substring(0, start) + content.Substring(start + headingLength)).Trim();
    }

    internal void Hide()
    {
        if (disposed) return;
        timer.Stop(); pending = visible = false;
        if (!owner.IsDisposed && owner.IsHandleCreated) tooltip.Hide(owner);
        if (ownsDescription && !owner.IsDisposed && owner.AccessibleDescription == text) owner.AccessibleDescription = priorDescription;
        ownsDescription = false; priorDescription = null; item = null; text = body = "";
    }

    private Size Measure()
    {
        Rectangle work = Screen.FromControl(owner).WorkingArea;
        int width = Math.Max(80, Math.Min(420, work.Width - 16));
        int top = item == null ? 12 : 66;
        int height = TextRenderer.MeasureText(body, HudTheme.BodyFont, new Size(Math.Max(1, width - 24), 10000),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;
        return new Size(width, Math.Max(1, Math.Min(Math.Min(480, work.Height - 16), Math.Max(72, top + height + 12))));
    }

    private void Display()
    {
        timer.Stop(); pending = false;
        if (disposed || text.Length == 0 || owner.IsDisposed || !owner.IsHandleCreated || !owner.Visible) { Hide(); return; }
        Size size = Measure(); Point screen = owner.PointToScreen(anchor); Rectangle work = Screen.FromPoint(screen).WorkingArea;
        screen.X = Math.Max(work.Left + 4, Math.Min(screen.X, work.Right - size.Width - 4));
        screen.Y = Math.Max(work.Top + 4, Math.Min(screen.Y, work.Bottom - size.Height - 4));
        Point client = owner.PointToClient(screen);
        RememberPalette(); visible = true;
        tooltip.Show(text, owner, client.X, client.Y, 20000);
        timer.Interval = 20000; timer.Start();
    }

    private void Tick(object sender, EventArgs e) { if (pending) Display(); else Hide(); }
    private void Popup(object sender, PopupEventArgs e) { e.ToolTipSize = Measure(); }
    private void ToolTipDraw(object sender, DrawToolTipEventArgs e) { Draw(e.Graphics, e.Bounds); }
    internal void Draw(Graphics graphics, Rectangle bounds)
    {
        graphics.Clear(HudTheme.Surface);
        using (var border = new Pen(HudTheme.InputBorder)) graphics.DrawRectangle(border, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
        int top = bounds.Top + 12;
        if (item != null)
        {
            HudItemPresentation.DrawIcon(graphics, new Rectangle(bounds.Left + 12, bounds.Top + 12, 40, 40), item);
            HudDrawing.DrawText(graphics, item.Caption, HudTheme.BodyFont, new Rectangle(bounds.Left + 62, bounds.Top + 10, bounds.Width - 76, 23), HudTheme.Text,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            HudDrawing.DrawText(graphics, item.Badge, HudTheme.SmallFont, new Rectangle(bounds.Left + 62, bounds.Top + 34, bounds.Width - 76, 20), HudItemPresentation.RarityColor(item),
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            top = bounds.Top + 66;
        }
        HudDrawing.DrawText(graphics, body, HudTheme.BodyFont, new Rectangle(bounds.Left + 12, top, Math.Max(1, bounds.Width - 24), Math.Max(1, bounds.Bottom - top - 12)), HudTheme.Text,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void RememberPalette() { surfaceColor = HudTheme.Surface.ToArgb(); textColor = HudTheme.Text.ToArgb(); borderColor = HudTheme.InputBorder.ToArgb(); }
    private void RefreshPalette()
    {
        if (visible && !disposed && (surfaceColor != HudTheme.Surface.ToArgb() || textColor != HudTheme.Text.ToArgb() || borderColor != HudTheme.InputBorder.ToArgb()))
        { tooltip.Hide(owner); Display(); }
    }
    private void OwnerInvalidated(object sender, InvalidateEventArgs e) { RefreshPalette(); }
    private void AppearanceChanged(object sender, EventArgs e) { RefreshPalette(); }
    private void OwnerLeft(object sender, EventArgs e) { Hide(); }
    private void VisibilityChanged(object sender, EventArgs e) { if (!owner.Visible) Hide(); }
    private void KeyPressed(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Hide(); }
    private void OwnerDisposed(object sender, EventArgs e) { Dispose(); }
    public void Dispose()
    {
        if (disposed) return;
        Hide(); disposed = true; timer.Dispose(); tooltip.Dispose();
        owner.MouseLeave -= OwnerLeft; owner.MouseDown -= OwnerLeft; owner.VisibleChanged -= VisibilityChanged;
        owner.HandleDestroyed -= OwnerLeft; owner.Disposed -= OwnerDisposed; owner.KeyDown -= KeyPressed;
        owner.Invalidated -= OwnerInvalidated; owner.BackColorChanged -= AppearanceChanged; owner.ForeColorChanged -= AppearanceChanged;
    }
}
