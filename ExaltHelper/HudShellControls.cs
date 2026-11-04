using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExaltHelper;

internal static class HudWindowChrome
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public static void Apply(Form form)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            int dark = HudTheme.IsLight ? 0 : 1;
            if (DwmSetWindowAttribute(form.Handle, 20, ref dark, 4) != 0) DwmSetWindowAttribute(form.Handle, 19, ref dark, 4);
            void ColorAttribute(int attribute, Color color) { int value = color.R | color.G << 8 | color.B << 16; DwmSetWindowAttribute(form.Handle, attribute, ref value, 4); }
            ColorAttribute(34, HudTheme.Border); ColorAttribute(35, HudTheme.Background); ColorAttribute(36, HudTheme.Text);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
}

internal static class HudDrawing
{
    // GDI text must share the GDI+ clip and origin used by the rest of a control.
    // Buffered partial repaints and scrolled children can otherwise leave text
    // at stale coordinates or draw it over neighboring controls.
    private const TextFormatFlags PaintTextFlags = TextFormatFlags.PreserveGraphicsClipping
        | TextFormatFlags.PreserveGraphicsTranslateTransform;
    public static void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        => TextRenderer.DrawText(graphics, text, font, bounds, color, flags | PaintTextFlags);
    public static void DrawText(Graphics graphics, string text, Font font, Point location, Color color)
        => TextRenderer.DrawText(graphics, text, font, location, color, PaintTextFlags);

    public static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Max(0, Math.Min(1, amount));
        return Color.FromArgb((int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));
    }

    public static GraphicsPath Round(RectangleF bounds, float radius = 4)
    {
        var path = new GraphicsPath();
        float size = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (size <= 0) return path;
        path.AddArc(bounds.Left, bounds.Top, size, size, 180, 90);
        path.AddArc(bounds.Right - size, bounds.Top, size, size, 270, 90);
        path.AddArc(bounds.Right - size, bounds.Bottom - size, size, size, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - size, size, size, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void Icon(Graphics graphics, string key, Rectangle bounds, Color color)
    {
        var state = graphics.Save();
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 20f, bounds.Height / 20f);
        using var pen = new Pen(color, 1.45f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        switch (key)
        {
            case "live":
                graphics.DrawLines(pen, new[] { new PointF(1, 11), new PointF(5, 11), new PointF(8, 4), new PointF(12, 16), new PointF(15, 9), new PointF(19, 9) }); break;
            case "combat":
                graphics.DrawLines(pen, new[] { new PointF(4, 16), new PointF(16, 4), new PointF(16, 8) });
                graphics.DrawLine(pen, 12, 4, 16, 4); graphics.DrawLine(pen, 3, 12, 8, 17);
                graphics.DrawLines(pen, new[] { new PointF(4, 4), new PointF(4, 8), new PointF(16, 16) }); graphics.DrawLine(pen, 12, 17, 17, 12); break;
            case "performance":
                graphics.DrawLine(pen, 3, 16, 18, 16); graphics.DrawLine(pen, 5, 13, 5, 9); graphics.DrawLine(pen, 10, 13, 10, 6); graphics.DrawLine(pen, 15, 13, 15, 3); break;
            case "loot": case "treasury":
                if (key == "treasury") { graphics.DrawEllipse(pen, 3, 3, 12, 12); graphics.DrawArc(pen, 6, 6, 12, 12, 0, 95); graphics.DrawLine(pen, 9, 6, 9, 12); }
                else graphics.DrawPolygon(pen, new[] { new PointF(10, 2), new PointF(18, 8), new PointF(10, 18), new PointF(2, 8) }); break;
            case "trophies":
                graphics.DrawArc(pen, 5, 1, 10, 12, 0, 180); graphics.DrawLine(pen, 5, 7, 5, 3); graphics.DrawLine(pen, 15, 7, 15, 3);
                graphics.DrawLine(pen, 5, 3, 15, 3); graphics.DrawArc(pen, 1, 4, 6, 7, 80, 185); graphics.DrawArc(pen, 13, 4, 6, 7, -85, 185);
                graphics.DrawLine(pen, 10, 13, 10, 17); graphics.DrawLine(pen, 6, 18, 14, 18); break;
            case "missions":
                graphics.DrawEllipse(pen, 4, 4, 12, 12); graphics.DrawEllipse(pen, 8, 8, 4, 4);
                graphics.DrawLine(pen, 10, 1, 10, 4); graphics.DrawLine(pen, 10, 16, 10, 19); graphics.DrawLine(pen, 1, 10, 4, 10); graphics.DrawLine(pen, 16, 10, 19, 10); break;
            case "quests":
                graphics.DrawRectangle(pen, 4, 3, 12, 15); graphics.DrawLine(pen, 7, 1, 13, 1);
                graphics.DrawLines(pen, new[] { new PointF(7, 10), new PointF(9, 12), new PointF(14, 7) }); break;
            case "characters": case "party": case "players":
                graphics.DrawEllipse(pen, 7, 2, 6, 6); graphics.DrawArc(pen, 4, 11, 12, 11, 180, 180);
                if (key == "party") { graphics.DrawArc(pen, 0, 4, 6, 6, 90, 230); graphics.DrawArc(pen, 14, 4, 6, 6, -135, 230); }
                if (key == "players") { graphics.DrawEllipse(pen, 12, 11, 5, 5); graphics.DrawLine(pen, 16, 16, 19, 19); } break;
            case "vault": case "calculator":
                if (key == "calculator") { graphics.DrawRectangle(pen, 3, 2, 14, 16); graphics.DrawLine(pen, 6, 6, 14, 6); }
                for (int x = 0; x < 3; x++) for (int y = 0; y < (key == "vault" ? 3 : 2); y++)
                    graphics.FillRectangle(brush, 4 + x * 5, (key == "vault" ? 3 : 10) + y * 5, 2, 2); break;
            case "exaltations":
                var star = new PointF[10]; for (int i = 0; i < star.Length; i++) { double angle = i * Math.PI / 5 - Math.PI / 2; float radius = i % 2 == 0 ? 8 : 3.8f; star[i] = new PointF(10 + radius * (float)Math.Cos(angle), 10 + radius * (float)Math.Sin(angle)); }
                graphics.DrawPolygon(pen, star); break;
            case "pets":
                graphics.DrawEllipse(pen, 6, 10, 8, 7); graphics.FillEllipse(brush, 2, 6, 3, 4); graphics.FillEllipse(brush, 6, 2, 3, 4); graphics.FillEllipse(brush, 11, 2, 3, 4); graphics.FillEllipse(brush, 15, 6, 3, 4); break;
            case "abilities":
                graphics.DrawPolygon(pen, new[] { new PointF(12, 1), new PointF(4, 11), new PointF(9, 11), new PointF(8, 19), new PointF(17, 8), new PointF(11, 8) }); break;
            case "chat":
                graphics.DrawPolygon(pen, new[] { new PointF(2, 3), new PointF(18, 3), new PointF(18, 14), new PointF(9, 14), new PointF(5, 18), new PointF(5, 14), new PointF(2, 14) });
                graphics.DrawLine(pen, 6, 7, 14, 7); graphics.DrawLine(pen, 6, 10, 11, 10); break;
            case "moonlight":
                graphics.DrawArc(pen, 2, 2, 16, 16, 65, 270); graphics.DrawArc(pen, 8, -1, 12, 15, 90, 160); break;
            default:
                graphics.DrawEllipse(pen, 5, 5, 10, 10); graphics.DrawEllipse(pen, 8, 8, 4, 4);
                for (int i = 0; i < 8; i++) { double angle = i * Math.PI / 4; graphics.DrawLine(pen, 10 + 5 * (float)Math.Cos(angle), 10 + 5 * (float)Math.Sin(angle), 10 + 8 * (float)Math.Cos(angle), 10 + 8 * (float)Math.Sin(angle)); } break;
        }
        graphics.Restore(state);
    }
}

internal class HudButton : Button
{
    private readonly Timer hoverTimer = new Timer { Interval = 15 };
    private bool hovered, pressed;
    protected float HoverAmount { get; private set; }
    public HudButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // ButtonBase assumes OnPaint fills every pixel. Our rounded painting
        // needs OnPaintBackground to clear text left in a reused paint buffer.
        SetStyle(ControlStyles.Opaque, false);
        FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor = false;
        hoverTimer.Tick += (_, __) => {
            float target = hovered && Enabled ? 1 : 0;
            HoverAmount = Math.Max(0, Math.Min(1, HoverAmount + (target > HoverAmount ? .15f : -.15f)));
            if (Math.Abs(target - HoverAmount) < .01f) hoverTimer.Stop();
            Invalidate();
        };
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; hoverTimer.Start(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; hoverTimer.Start(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? HudTheme.Background); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        Color fill = HudDrawing.Blend(BackColor, HudTheme.Hover, pressed ? 1 : HoverAmount);
        using var shape = HudDrawing.Round(bounds);
        using var brush = new LinearGradientBrush(bounds, HudDrawing.Blend(fill, HudTheme.Text, .025f), fill, 90);
        g.FillPath(brush, shape);
        using var border = new Pen(HudDrawing.Blend(HudTheme.InputBorder, HudTheme.Accent, Focused ? .8f : HoverAmount * .45f));
        g.DrawPath(border, shape);
        var textBounds = Rectangle.Inflate(bounds, -Math.Max(5, Padding.Left), 0);
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        flags |= TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        HudDrawing.DrawText(g, Text, Font, textBounds, Enabled ? ForeColor : HudTheme.Muted, flags);
    }
    protected override void Dispose(bool disposing) { if (disposing) hoverTimer.Dispose(); base.Dispose(disposing); }
}

internal sealed class HudNavButton : HudButton
{
    private bool active;
    public string IconKey { get; set; }
    public bool Active { get => active; set { if (active != value) { active = value; Invalidate(); } } }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 1, Width - 2, Height - 3);
        using var shape = HudDrawing.Round(bounds, 5);
        Color fill = HudDrawing.Blend(HudTheme.Surface, HudTheme.Hover, HoverAmount * .7f);
        if (Active)
        {
            using var gradient = new LinearGradientBrush(bounds, HudDrawing.Blend(HudTheme.Surface, HudTheme.Accent, .14f), HudTheme.Surface, 0f);
            g.FillPath(gradient, shape);
            using var outline = new Pen(Color.FromArgb(HudTheme.IsLight ? 50 : 32, HudTheme.Accent)); g.DrawPath(outline, shape);
            using var glow = new Pen(Color.FromArgb(24, HudTheme.Accent), 7); g.DrawLine(glow, 2, 10, 2, Height - 10);
            using var accent = new Pen(HudTheme.Accent, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(accent, 2, 9, 2, Height - 9);
        }
        else { using var brush = new SolidBrush(fill); g.FillPath(brush, shape); }
        Color foreground = Active ? HudTheme.Accent : HudDrawing.Blend(HudTheme.Muted, HudTheme.Text, HoverAmount);
        HudDrawing.Icon(g, IconKey, new Rectangle(14, (Height - 18) / 2, 18, 18), foreground);
        HudDrawing.DrawText(g, Text, Font, new Rectangle(44, 0, Math.Max(1, Width - 50), Height), foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) { using var focus = new Pen(HudTheme.Accent); g.DrawPath(focus, shape); }
    }
}

internal sealed class HudCheckBox : CheckBox
{
    private bool hovered;
    public HudCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // Clear the complete background before painting the glyph and label.
        SetStyle(ControlStyles.Opaque, false);
        UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
    }
    private TextFormatFlags TextFlags => TextFormatFlags.WordBreak | TextFormatFlags.NoPadding
        | (!UseMnemonic ? TextFormatFlags.NoPrefix : ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix);
    public override Size GetPreferredSize(Size proposedSize)
    {
        int available = proposedSize.Width > 0 ? Math.Max(1, proposedSize.Width - Padding.Horizontal - 26) : int.MaxValue;
        if (MaximumSize.Width > 0) available = Math.Min(available, Math.Max(1, MaximumSize.Width - Padding.Horizontal - 26));
        var text = TextRenderer.MeasureText(Text, Font, new Size(available, 0), TextFlags);
        return new Size(text.Width + 26 + Padding.Horizontal, Math.Max(20, text.Height + 2) + Padding.Vertical);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? HudTheme.Background); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        int top = Math.Max(1, (Height - 14) / 2);
        using var box = HudDrawing.Round(new Rectangle(1, top, 14, 14), 3);
        using var fill = new SolidBrush(Checked ? HudTheme.Accent : HudTheme.Input); g.FillPath(fill, box);
        using var outline = new Pen(hovered || Focused ? HudTheme.Accent : HudTheme.InputBorder); g.DrawPath(outline, box);
        if (CheckState == CheckState.Indeterminate) { using var dash = new Pen(HudTheme.Background, 2); g.DrawLine(dash, 4, top + 7, 12, top + 7); }
        else if (Checked) { using var tick = new Pen(HudTheme.Background, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLines(tick, new[] { new Point(4, top + 7), new Point(7, top + 10), new Point(12, top + 4) }); }
        HudDrawing.DrawText(g, Text, Font, new Rectangle(Padding.Left + 22, Padding.Top, Math.Max(1, Width - Padding.Horizontal - 22), Height - Padding.Vertical), Enabled ? ForeColor : HudTheme.Muted,
            TextFormatFlags.VerticalCenter | TextFlags);
    }
}

internal sealed class HudStatCard : Label
{
    private static readonly Font ValueFont = new Font("Segoe UI Semibold", 11.5f);
    private static readonly Font CaptionFont = new Font("Segoe UI Semibold", 7.5f);
    private bool hovered;
    public float? Progress { get; set; }
    public HudStatCard()
    {
        AutoSize = false; Height = 54;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    private string Caption => (Tag as string ?? "").ToUpperInvariant();
    private string Value => Text.StartsWith(Caption + "   ", StringComparison.Ordinal) ? Text.Substring(Caption.Length + 3) : Text;
    private Color Tint => (Tag as string) == "HP" ? Color.FromArgb(251, 113, 133) : (Tag as string) == "MP" ? Color.FromArgb(96, 165, 250)
        : (Tag as string) == "Gold" || (Tag as string) == "Fame" ? HudTheme.Gold : HudTheme.Accent;
    public override Size GetPreferredSize(Size proposedSize) => new Size(Math.Max(96, Math.Min(226, TextRenderer.MeasureText(Value, ValueFont).Width + 28)), 54);
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Size = GetPreferredSize(Size.Empty); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? HudTheme.Background); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var shape = HudDrawing.Round(bounds);
        using var surface = new LinearGradientBrush(bounds, HudDrawing.Blend(HudTheme.Surface, Tint, HudTheme.IsLight ? .055f : .065f), HudTheme.Surface, 90);
        g.FillPath(surface, shape);
        using var border = new Pen(hovered ? HudDrawing.Blend(HudTheme.InputBorder, Tint, .45f) : HudTheme.Border); g.DrawPath(border, shape);
        using var dot = new SolidBrush(Tint); g.FillEllipse(dot, 11, 10, 4, 4);
        HudDrawing.DrawText(g, Caption, CaptionFont, new Rectangle(21, 6, Width - 27, 15), HudTheme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        HudDrawing.DrawText(g, Value, ValueFont, new Rectangle(10, 23, Width - 19, 24), HudTheme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (Progress.HasValue)
        {
            using var track = new Pen(HudDrawing.Blend(HudTheme.Surface, Tint, .16f), 2); g.DrawLine(track, 11, Height - 5, Width - 12, Height - 5);
            float fraction = Math.Max(0, Math.Min(1, Progress.Value));
            if (fraction > 0) { using var fill = new Pen(Tint, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(fill, 11, Height - 5, 11 + (Width - 23) * fraction, Height - 5); }
        }
    }
}

internal class HudSurfacePanel : Panel
{
    public HudSurfacePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = HudTheme.Surface;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? HudTheme.Background);
        if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var shape = HudDrawing.Round(bounds, 5);
        using var fill = new LinearGradientBrush(bounds, HudDrawing.Blend(BackColor, HudTheme.Text, .018f), BackColor, 90);
        e.Graphics.FillPath(fill, shape);
        using var border = new Pen(HudTheme.Border); e.Graphics.DrawPath(border, shape);
        using var highlight = new Pen(Color.FromArgb(HudTheme.IsLight ? 80 : 12, Color.White)); e.Graphics.DrawLine(highlight, 6, 1, Width - 7, 1);
    }
}

internal sealed class HudBrandPanel : Panel
{
    private static readonly Font BrandFont = new Font("Segoe UI Semibold", 15.5f);
    private static readonly Font CaptionFont = new Font("Segoe UI", 7.5f);
    public HudBrandPanel() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var glow = new SolidBrush(Color.FromArgb(16, HudTheme.Accent)); g.FillEllipse(glow, 14, 16, 36, 36);
        using var outline = new Pen(HudTheme.Accent, 1.6f);
        g.DrawPolygon(outline, new[] { new Point(32, 20), new Point(44, 33), new Point(32, 46), new Point(20, 33) });
        g.DrawLine(outline, 28, 37, 36, 29); g.DrawLine(outline, 28, 29, 36, 37);
        HudDrawing.DrawText(g, "EXALT / HUD", BrandFont, new Point(56, 17), HudTheme.Text);
        HudDrawing.DrawText(g, "REALM COMPANION", CaptionFont, new Point(58, 44), HudTheme.Muted);
        using var line = new Pen(HudTheme.Border); g.DrawLine(line, 20, Height - 1, Width - 20, Height - 1);
    }
}
