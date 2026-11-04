using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExaltHelper;

// These controls retain the standard WinForms types and native interaction.
// Scrollbars are skinned in place; no extra children enter content collections.
internal static class HudControlPaint
{
    public static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath(); float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
    }
    public static Color Mix(Color from, Color to, double amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount), (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));
    public static void Frame(Graphics graphics, Rectangle bounds, Color parent, Color fill, Color border)
    {
        if (bounds.Width < 2 || bounds.Height < 2) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var outer = new SolidBrush(parent); graphics.FillRectangle(outer, bounds);
        using var path = Rounded(new RectangleF(bounds.X + .5f, bounds.Y + .5f, bounds.Width - 1, bounds.Height - 1), 7);
        using var background = new SolidBrush(fill); graphics.FillPath(background, path);
        using var line = new Pen(border); graphics.DrawPath(line, path);
    }
}

internal class HudTextBox : TextBox
{
    private bool searchIcon;
    private string placeholder = "";
    public bool SearchIcon { get => searchIcon; set { if (searchIcon == value) return; searchIcon = value; RefreshFrame(); } }
    public string Placeholder { get => placeholder; set { placeholder = value ?? ""; ApplyPlaceholder(); } }
    public HudTextBox()
    {
        BorderStyle = BorderStyle.None; AutoSize = false; Height = 34;
        BackColor = HudTheme.Input; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
    }
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; value.Style &= ~0x00800000; value.ExStyle &= ~0x00000200; return value; }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyPlaceholder(); RefreshFrame(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (!Multiline) Height = Math.Max(34, Font.Height + 16); RefreshFrame(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); PaintFrame(IntPtr.Zero); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); PaintFrame(IntPtr.Zero); }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); PaintFrame(IntPtr.Zero); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); PaintFrame(IntPtr.Zero); }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize); if (!Multiline) size.Height = Math.Max(34, Font.Height + 16); return size;
    }
    private void ApplyPlaceholder() { if (IsHandleCreated) HudNative.SendText(Handle, 0x1501, IntPtr.Zero, placeholder); }
    private void RefreshFrame()
    {
        if (!IsHandleCreated) return;
        HudNative.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020);
        Invalidate(); PaintFrame(IntPtr.Zero);
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg == 0x0083)
        {
            var rect = (HudNative.Rect)Marshal.PtrToStructure(message.LParam, typeof(HudNative.Rect));
            int vertical = Multiline ? 8 : Math.Max(4, (rect.Bottom - rect.Top - Font.Height) / 2);
            rect.Left += searchIcon ? 34 : 11; rect.Right = Math.Max(rect.Left, rect.Right - 11);
            rect.Top += vertical; rect.Bottom = Math.Max(rect.Top, rect.Bottom - vertical);
            Marshal.StructureToPtr(rect, message.LParam, false); message.Result = IntPtr.Zero;
        }
        else if (HudNative.PaintMessage(message.Msg)) PaintFrame(message.Msg == 0x0317 ? message.WParam : IntPtr.Zero);
    }
    private void PaintFrame(IntPtr target)
    {
        if (!IsHandleCreated || Width < 2 || Height < 2 || IsDisposed) return;
        IntPtr dc = target == IntPtr.Zero ? HudNative.GetWindowDC(Handle) : target;
        if (dc == IntPtr.Zero) return;
        try
        {
            using var graphics = Graphics.FromHdc(dc);
            graphics.ExcludeClip(HudNative.ClientInWindow(Handle));
            HudControlPaint.Frame(graphics, new Rectangle(0, 0, Width, Height), Parent?.BackColor ?? HudTheme.Background,
                BackColor, Focused ? HudTheme.Accent : HudTheme.InputBorder);
            if (searchIcon)
            {
                float centerY = Height / 2f - 1;
                using var pen = new Pen(Enabled ? HudTheme.Muted : HudTheme.Border, 1.35f);
                graphics.DrawEllipse(pen, 12, centerY - 5, 8, 8); graphics.DrawLine(pen, 19, centerY + 2, 23, centerY + 6);
            }
        }
        finally { if (target == IntPtr.Zero) HudNative.ReleaseDC(Handle, dc); }
        if (Multiline) HudNative.PaintScrollbars(Handle, BackColor, target);
    }
}

internal class HudComboBox : ComboBox
{
    private readonly HudComboPopup popup = new HudComboPopup();
    private bool painting;
    public HudComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat;
        BackColor = HudTheme.Input; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        ItemHeight = Math.Max(26, Font.Height + 10); IntegralHeight = false; DropDownHeight = 260;
    }
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; value.Style &= ~0x00800000; value.ExStyle &= ~0x00000200; return value; }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ConfigureEdit(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ItemHeight = Math.Max(26, Font.Height + 10); ConfigureEdit(); }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e); ConfigureEdit();
        if (Width > 1 && Height > 1)
        {
            using var outline = HudControlPaint.Rounded(new RectangleF(0, 0, Width, Height), 7);
            var previous = Region; Region = new Region(outline); previous?.Dispose();
        }
        Invalidate();
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnDropDown(EventArgs e)
    {
        if (HudNative.ComboInfo(Handle, out var info)) popup.Attach(info.List, this);
        base.OnDropDown(e); Invalidate();
    }
    protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var fill = new SolidBrush(selected ? HudTheme.Hover : BackColor); e.Graphics.FillRectangle(fill, e.Bounds);
        string text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
        var bounds = Rectangle.Inflate(e.Bounds, -11, 0);
        HudDrawing.DrawText(e.Graphics, text, Font, bounds, Enabled ? ForeColor : HudTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (selected)
        {
            using var mark = new SolidBrush(HudTheme.Accent); e.Graphics.FillRectangle(mark, e.Bounds.Left + 2, e.Bounds.Top + 6, 2, Math.Max(1, e.Bounds.Height - 12));
        }
    }
    private void ConfigureEdit()
    {
        if (!IsHandleCreated || DropDownStyle == ComboBoxStyle.DropDownList) return;
        if (HudNative.ComboInfo(Handle, out var info) && info.Item != IntPtr.Zero)
            HudNative.SetWindowPos(info.Item, IntPtr.Zero, 11, Math.Max(2, (Height - Font.Height) / 2), Math.Max(1, Width - 44), Font.Height + 2, 0x0004 | 0x0010);
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (HudNative.PaintMessage(message.Msg) || message.Msg == 0x0007 || message.Msg == 0x0008)
            PaintFace(message.Msg == 0x0317 || message.Msg == 0x0318 ? message.WParam : IntPtr.Zero);
    }
    private void PaintFace(IntPtr target)
    {
        if (painting || !IsHandleCreated || IsDisposed || Width < 2 || Height < 2) return;
        painting = true; IntPtr dc = target == IntPtr.Zero ? HudNative.GetWindowDC(Handle) : target;
        try
        {
            if (dc == IntPtr.Zero) return;
            using var graphics = Graphics.FromHdc(dc);
            if (DropDownStyle != ComboBoxStyle.DropDownList && HudNative.ComboInfo(Handle, out var info))
                graphics.ExcludeClip(HudNative.WindowInWindow(info.Item, Handle));
            HudControlPaint.Frame(graphics, new Rectangle(0, 0, Width, Height), Parent?.BackColor ?? HudTheme.Background, BackColor,
                ContainsFocus || DroppedDown ? HudTheme.Accent : HudTheme.InputBorder);
            if (DropDownStyle == ComboBoxStyle.DropDownList)
                HudDrawing.DrawText(graphics, Text, Font, new Rectangle(11, 1, Math.Max(1, Width - 42), Height - 2), Enabled ? ForeColor : HudTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using var chevron = new Pen(Enabled ? HudTheme.Muted : HudTheme.Border, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float x = Width - 18, y = Height / 2f;
            graphics.DrawLines(chevron, new[] { new PointF(x - 3, y - 1.5f), new PointF(x, y + 1.5f), new PointF(x + 3, y - 1.5f) });
        }
        finally { if (target == IntPtr.Zero && dc != IntPtr.Zero) HudNative.ReleaseDC(Handle, dc); painting = false; }
    }
    protected override void Dispose(bool disposing) { if (disposing) popup.Dispose(); base.Dispose(disposing); }
}

internal sealed class HudComboPopup : NativeWindow, IDisposable
{
    private ComboBox owner;
    private bool painting;
    public void Attach(IntPtr handle, ComboBox value)
    {
        owner = value; if (handle == IntPtr.Zero) return;
        if (Handle != handle) { ReleaseHandle(); AssignHandle(handle); }
        HudNative.SetWindowTheme(handle, "", "");
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (!painting && owner != null && HudNative.PaintMessage(message.Msg))
        {
            painting = true;
            try
            {
                HudNative.PaintScrollbars(Handle, owner.BackColor, message.Msg == 0x0317 ? message.WParam : IntPtr.Zero);
                IntPtr dc = HudNative.GetWindowDC(Handle);
                try
                {
                    if (dc != IntPtr.Zero && HudNative.GetWindowRect(Handle, out var bounds))
                    {
                        using var graphics = Graphics.FromHdc(dc); using var border = new Pen(HudTheme.InputBorder);
                        graphics.DrawRectangle(border, 0, 0, Math.Max(0, bounds.Right - bounds.Left - 1), Math.Max(0, bounds.Bottom - bounds.Top - 1));
                    }
                }
                finally { if (dc != IntPtr.Zero) HudNative.ReleaseDC(Handle, dc); }
            }
            finally { painting = false; }
        }
    }
    public void Dispose() { ReleaseHandle(); owner = null; }
}

internal class HudNumericUpDown : NumericUpDown
{
    private HudSpinButtonsSkin buttonsSkin;
    private HudNumericEditSkin editSkin;
    private bool arranging, painting;
    public HudNumericUpDown()
    {
        BorderStyle = BorderStyle.None; AutoSize = false;
        BackColor = HudTheme.Input; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        ArrangeChildren();
    }
    protected override void OnLayout(LayoutEventArgs e) { if (arranging) return; base.OnLayout(e); ArrangeChildren(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ArrangeChildren(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); ArrangeChildren(); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ArrangeChildren(); }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); ArrangeChildren(); Invalidate(); }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); ArrangeChildren(); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    private void ArrangeChildren()
    {
        if (arranging || IsDisposed) return; arranging = true;
        try
        {
            foreach (Control child in Controls)
            {
                child.BackColor = BackColor; child.ForeColor = ForeColor;
                if (child is TextBox edit)
                {
                    edit.BorderStyle = BorderStyle.None;
                    if (editSkin == null) editSkin = new HudNumericEditSkin(edit, this);
                }
                else
                {
                    if (buttonsSkin == null) buttonsSkin = new HudSpinButtonsSkin(child, this);
                }
            }
        }
        finally { arranging = false; }
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (HudNative.PaintMessage(message.Msg)) PaintOutline(message.Msg == 0x0317 || message.Msg == 0x0318 ? message.WParam : IntPtr.Zero);
    }
    internal void PaintOutline(IntPtr target)
    {
        if (painting || !IsHandleCreated || IsDisposed || Width < 3 || Height < 3) return;
        painting = true;
        IntPtr dc = target == IntPtr.Zero ? HudNative.GetWindowDC(Handle) : target;
        try
        {
            if (dc == IntPtr.Zero) return;
            using var graphics = Graphics.FromHdc(dc);
            using var interior = HudControlPaint.Rounded(new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), 5.5f);
            using var region = new Region(interior); graphics.ExcludeClip(region);
            HudControlPaint.Frame(graphics, new Rectangle(0, 0, Width, Height), Parent?.BackColor ?? HudTheme.Background, BackColor,
                ContainsFocus ? HudTheme.Accent : HudTheme.InputBorder);
        }
        finally { if (target == IntPtr.Zero && dc != IntPtr.Zero) HudNative.ReleaseDC(Handle, dc); painting = false; }
    }
    protected override void Dispose(bool disposing) { if (disposing) { buttonsSkin?.Dispose(); editSkin?.Dispose(); } base.Dispose(disposing); }
}

internal sealed class HudNumericEditSkin : NativeWindow, IDisposable
{
    private readonly TextBox edit;
    private readonly HudNumericUpDown owner;
    public HudNumericEditSkin(TextBox value, HudNumericUpDown parent)
    {
        edit = value; owner = parent; edit.HandleCreated += HandleCreated; edit.HandleDestroyed += HandleDestroyed;
        if (edit.IsHandleCreated) HandleCreated(edit, EventArgs.Empty);
    }
    private void HandleCreated(object sender, EventArgs e)
    {
        AssignHandle(edit.Handle); HudNative.SendValue(edit.Handle, 0x00D3, new IntPtr(3), new IntPtr((3 << 16) | 9));
    }
    private void HandleDestroyed(object sender, EventArgs e) { ReleaseHandle(); }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (HudNative.PaintMessage(message.Msg) || message.Msg == 7 || message.Msg == 8) owner.PaintOutline(IntPtr.Zero);
    }
    public void Dispose() { edit.HandleCreated -= HandleCreated; edit.HandleDestroyed -= HandleDestroyed; ReleaseHandle(); }
}

internal sealed class HudSpinButtonsSkin : NativeWindow, IDisposable
{
    private readonly Control buttons;
    private readonly HudNumericUpDown owner;
    private bool painting;
    public HudSpinButtonsSkin(Control value, HudNumericUpDown parent)
    {
        buttons = value; owner = parent; buttons.HandleCreated += HandleCreated; buttons.HandleDestroyed += HandleDestroyed;
        if (buttons.IsHandleCreated) HandleCreated(buttons, EventArgs.Empty);
    }
    private void HandleCreated(object sender, EventArgs e) { AssignHandle(buttons.Handle); }
    private void HandleDestroyed(object sender, EventArgs e) { ReleaseHandle(); }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (painting || !HudNative.PaintMessage(message.Msg)) return; painting = true;
        IntPtr target = message.Msg == 0x0317 || message.Msg == 0x0318 ? message.WParam : IntPtr.Zero;
        IntPtr dc = target == IntPtr.Zero ? HudNative.GetDC(Handle) : target;
        try
        {
            if (dc == IntPtr.Zero) return;
            using var graphics = Graphics.FromHdc(dc); graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(owner.BackColor); graphics.FillRectangle(fill, buttons.ClientRectangle);
            using var pen = new Pen(owner.Enabled ? HudTheme.Muted : HudTheme.Border, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float x = buttons.Width / 2f, upper = buttons.Height * .25f, lower = buttons.Height * .75f;
            graphics.DrawLines(pen, new[] { new PointF(x - 3, upper + 1), new PointF(x, upper - 2), new PointF(x + 3, upper + 1) });
            graphics.DrawLines(pen, new[] { new PointF(x - 3, lower - 1), new PointF(x, lower + 2), new PointF(x + 3, lower - 1) });
        }
        finally { if (target == IntPtr.Zero && dc != IntPtr.Zero) HudNative.ReleaseDC(Handle, dc); painting = false; }
        owner.PaintOutline(IntPtr.Zero);
    }
    public void Dispose() { buttons.HandleCreated -= HandleCreated; buttons.HandleDestroyed -= HandleDestroyed; ReleaseHandle(); }
}

internal class HudFlowPanel : FlowLayoutPanel
{
    private bool paintingScrollbars;
    public HudFlowPanel() { DoubleBuffered = true; }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (!paintingScrollbars && IsHandleCreated && HudNative.PaintMessage(message.Msg))
        {
            paintingScrollbars = true;
            try { HudNative.PaintScrollbars(Handle, BackColor, message.Msg == 0x0317 ? message.WParam : IntPtr.Zero); }
            finally { paintingScrollbars = false; }
        }
    }
}

internal class HudScrollPanel : Panel
{
    private bool paintingScrollbars;
    public HudScrollPanel() { DoubleBuffered = true; }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (!paintingScrollbars && IsHandleCreated && HudNative.PaintMessage(message.Msg))
        {
            paintingScrollbars = true;
            try { HudNative.PaintScrollbars(Handle, BackColor, message.Msg == 0x0317 ? message.WParam : IntPtr.Zero); }
            finally { paintingScrollbars = false; }
        }
    }
}

internal class HudDataGridView : DataGridView
{
    private readonly Dictionary<ScrollBar, HudScrollBarSkin> skins = new Dictionary<ScrollBar, HudScrollBarSkin>();
    private bool paintingCorner;
    public HudDataGridView()
    {
        DoubleBuffered = true;
        foreach (Control control in Controls) Attach(control);
    }
    protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); if (skins != null) Attach(e.Control); }
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (skins != null && e.Control is ScrollBar bar && skins.TryGetValue(bar, out var skin)) { skin.Dispose(); skins.Remove(bar); }
        base.OnControlRemoved(e);
    }
    private void Attach(Control control)
    {
        if (control is ScrollBar bar && !skins.ContainsKey(bar)) skins.Add(bar, new HudScrollBarSkin(bar, () => BackgroundColor));
    }
    protected override void OnBackgroundColorChanged(EventArgs e)
    {
        base.OnBackgroundColorChanged(e); if (skins != null) foreach (var skin in skins.Values) skin.Invalidate();
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (paintingCorner || skins == null || !HudNative.PaintMessage(message.Msg) || !IsHandleCreated) return;
        Rectangle vertical = Rectangle.Empty, horizontal = Rectangle.Empty;
        foreach (var bar in skins.Keys)
        {
            if (!bar.Visible || !bar.IsHandleCreated) continue;
            if (bar is VScrollBar) vertical = HudNative.WindowInWindow(bar.Handle, Handle);
            else horizontal = HudNative.WindowInWindow(bar.Handle, Handle);
        }
        if (vertical.IsEmpty || horizontal.IsEmpty) return;
        paintingCorner = true;
        IntPtr target = message.Msg == 0x0317 || message.Msg == 0x0318 ? message.WParam : IntPtr.Zero;
        IntPtr dc = target == IntPtr.Zero ? HudNative.GetWindowDC(Handle) : target;
        try
        {
            if (dc != IntPtr.Zero)
            {
                using var graphics = Graphics.FromHdc(dc); using var fill = new SolidBrush(BackgroundColor);
                graphics.FillRectangle(fill, vertical.Left, horizontal.Top, vertical.Width, horizontal.Height);
            }
        }
        finally { if (target == IntPtr.Zero && dc != IntPtr.Zero) HudNative.ReleaseDC(Handle, dc); paintingCorner = false; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && skins != null) { foreach (var skin in skins.Values) skin.Dispose(); skins.Clear(); }
        base.Dispose(disposing);
    }
}

internal sealed class HudScrollBarSkin : NativeWindow, IDisposable
{
    private readonly ScrollBar bar;
    private readonly Func<Color> background;
    private bool painting;
    public HudScrollBarSkin(ScrollBar value, Func<Color> color)
    {
        bar = value; background = color; bar.HandleCreated += HandleCreated; bar.HandleDestroyed += HandleDestroyed;
        if (bar.IsHandleCreated) HandleCreated(bar, EventArgs.Empty);
    }
    private void HandleCreated(object sender, EventArgs e) { AssignHandle(bar.Handle); HudNative.SetWindowTheme(bar.Handle, "", ""); }
    private void HandleDestroyed(object sender, EventArgs e) { ReleaseHandle(); }
    public void Invalidate() { if (!bar.IsDisposed) bar.Invalidate(); }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (!painting && HudNative.PaintMessage(message.Msg))
        {
            painting = true;
            try { HudNative.PaintScrollbarControl(Handle, bar is VScrollBar, background(), message.Msg == 0x0317 || message.Msg == 0x0318 ? message.WParam : IntPtr.Zero); }
            finally { painting = false; }
        }
    }
    public void Dispose() { bar.HandleCreated -= HandleCreated; bar.HandleDestroyed -= HandleDestroyed; ReleaseHandle(); }
}

internal static class HudNative
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct ComboBoxInfo { public int Size; public Rect ItemRect, ButtonRect; public int ButtonState; public IntPtr Combo, Item, List; }
    [StructLayout(LayoutKind.Sequential)] private struct ScrollBarInfo
    {
        public int Size; public Rect Bounds; public int ArrowSize, ThumbTop, ThumbBottom, Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] States;
    }
    [DllImport("user32.dll")] internal static extern IntPtr GetWindowDC(IntPtr handle);
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr handle);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr handle, IntPtr dc);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr handle, ref Point point);
    [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr handle, ref ComboBoxInfo info);
    [DllImport("user32.dll")] private static extern bool GetScrollBarInfo(IntPtr handle, int objectId, ref ScrollBarInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] internal static extern IntPtr SendText(IntPtr handle, int message, IntPtr wParam, string text);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] internal static extern IntPtr SendValue(IntPtr handle, int message, IntPtr wParam, IntPtr value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] internal static extern int SetWindowTheme(IntPtr handle, string subApp, string subId);
    internal static bool ComboInfo(IntPtr handle, out ComboBoxInfo info)
    {
        info = new ComboBoxInfo { Size = Marshal.SizeOf(typeof(ComboBoxInfo)) }; return GetComboBoxInfo(handle, ref info);
    }
    internal static Rectangle ClientInWindow(IntPtr handle)
    {
        if (!GetWindowRect(handle, out var window) || !GetClientRect(handle, out var client)) return Rectangle.Empty;
        var point = new Point(); ClientToScreen(handle, ref point);
        return new Rectangle(point.X - window.Left, point.Y - window.Top, client.Right - client.Left, client.Bottom - client.Top);
    }
    internal static Rectangle WindowInWindow(IntPtr child, IntPtr parent)
    {
        if (child == IntPtr.Zero || !GetWindowRect(child, out var item) || !GetWindowRect(parent, out var window)) return Rectangle.Empty;
        return new Rectangle(item.Left - window.Left, item.Top - window.Top, item.Right - item.Left, item.Bottom - item.Top);
    }
    internal static bool PaintMessage(int message) => message == 0x000F || message == 0x0085 || message == 0x0317 || message == 0x0318
        || message == 0x0005 || message == 0x0047 || message == 0x007D || message == 0x0114 || message == 0x0115
        || message == 0x020A || message == 0x020E || message == 0x00A0 || message == 0x00A1 || message == 0x00A2
        || message == 0x0200 || message == 0x0201 || message == 0x0202 || message == 0x0113 || message == 0x001F;
    internal static void PaintScrollbars(IntPtr handle, Color background, IntPtr target)
    {
        if (!GetWindowRect(handle, out var window)) return;
        IntPtr dc = target == IntPtr.Zero ? GetWindowDC(handle) : target;
        if (dc == IntPtr.Zero) return;
        try
        {
            using var graphics = Graphics.FromHdc(dc);
            var vertical = Info(handle, -5); var horizontal = Info(handle, -6);
            if (vertical.HasValue) DrawBar(graphics, vertical.Value, window, true, background);
            if (horizontal.HasValue) DrawBar(graphics, horizontal.Value, window, false, background);
            if (vertical.HasValue && horizontal.HasValue)
            {
                using var fill = new SolidBrush(background);
                var v = vertical.Value.Bounds; var h = horizontal.Value.Bounds;
                graphics.FillRectangle(fill, v.Left - window.Left, h.Top - window.Top, v.Right - v.Left, h.Bottom - h.Top);
            }
        }
        finally { if (target == IntPtr.Zero) ReleaseDC(handle, dc); }
    }
    internal static void PaintScrollbarControl(IntPtr handle, bool vertical, Color background, IntPtr target)
    {
        var info = Info(handle, -4); if (!info.HasValue || !GetWindowRect(handle, out var window)) return;
        IntPtr dc = target == IntPtr.Zero ? GetDC(handle) : target; if (dc == IntPtr.Zero) return;
        try { using var graphics = Graphics.FromHdc(dc); DrawBar(graphics, info.Value, window, vertical, background); }
        finally { if (target == IntPtr.Zero) ReleaseDC(handle, dc); }
    }
    private static ScrollBarInfo? Info(IntPtr handle, int id)
    {
        var info = new ScrollBarInfo { Size = Marshal.SizeOf(typeof(ScrollBarInfo)), States = new int[6] };
        return GetScrollBarInfo(handle, id, ref info) && (info.States[0] & 0x8000) == 0 && info.Bounds.Right > info.Bounds.Left && info.Bounds.Bottom > info.Bounds.Top ? info : (ScrollBarInfo?)null;
    }
    private static void DrawBar(Graphics graphics, ScrollBarInfo info, Rect window, bool vertical, Color background)
    {
        var bounds = new Rectangle(info.Bounds.Left - window.Left, info.Bounds.Top - window.Top, info.Bounds.Right - info.Bounds.Left, info.Bounds.Bottom - info.Bounds.Top);
        using var fill = new SolidBrush(background); graphics.FillRectangle(fill, bounds);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int trackStart = info.ArrowSize, trackLength = (vertical ? bounds.Height : bounds.Width) - 2 * info.ArrowSize;
        var track = vertical ? new RectangleF(bounds.Left + (bounds.Width - 6) / 2f, bounds.Top + trackStart, 6, Math.Max(0, trackLength))
            : new RectangleF(bounds.Left + trackStart, bounds.Top + (bounds.Height - 6) / 2f, Math.Max(0, trackLength), 6);
        if (track.Width > 0 && track.Height > 0)
        {
            using var path = HudControlPaint.Rounded(track, 3); using var brush = new SolidBrush(HudControlPaint.Mix(background, HudTheme.Muted, .09)); graphics.FillPath(brush, path);
        }
        if ((info.States[0] & 1) != 0 || info.ThumbBottom <= info.ThumbTop) return;
        var thumb = vertical ? new RectangleF(bounds.Left + (bounds.Width - 6) / 2f, bounds.Top + info.ThumbTop, 6, info.ThumbBottom - info.ThumbTop)
            : new RectangleF(bounds.Left + info.ThumbTop, bounds.Top + (bounds.Height - 6) / 2f, info.ThumbBottom - info.ThumbTop, 6);
        using var thumbPath = HudControlPaint.Rounded(thumb, 3);
        using var thumbFill = new SolidBrush((info.States[3] & 8) != 0 ? HudTheme.Accent : HudControlPaint.Mix(background, HudTheme.Muted, .55));
        graphics.FillPath(thumbFill, thumbPath);
    }
}
