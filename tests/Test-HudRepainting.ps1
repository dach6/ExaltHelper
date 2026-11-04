param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotDirectory = "$PSScriptRoot/../artifacts/hud-repainting"
)
$ErrorActionPreference = 'Stop'
$artifactDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($ScreenshotDirectory)))
if ($artifactDrive.AvailableFreeSpace -le 5GB) { throw 'Disk guard stopped work: artifact drive has at most 5 GiB free.' }
$testRoot = Join-Path ([IO.Path]::GetFullPath($ScreenshotDirectory)) ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$priorData = $env:EXALTHELPER_HUD_DATA
$env:EXALTHELPER_HUD_DATA = Join-Path $testRoot 'data'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$harness = Join-Path $testRoot 'HudRepaintingTest.dll'
Add-Type -OutputAssembly $harness -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public sealed class HudRepaintingRegression : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly app;
    static readonly Color Sentinel = Color.FromArgb(255, 0, 255);
    static readonly List<string> results = new List<string>();
    static int checks, failures;
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Bounds { get { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } } }
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetUpdateRect(IntPtr window, out NativeRect bounds, bool erase);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr first, IntPtr second);
    static Type T(string name) { return app.GetType("ExaltHelper." + name, true); }
    static object Get(object instance, string name)
    {
        Type type = instance as Type ?? instance.GetType(); object target = instance is Type ? null : instance;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target); }
        return type.GetProperty(name, F).GetValue(target, null);
    }
    static void Set(object instance, string name, object value)
    {
        Type type = instance as Type ?? instance.GetType(); object target = instance is Type ? null : instance;
        for (Type current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) { field.SetValue(target, value); return; } }
        type.GetProperty(name, F).SetValue(target, value, null);
    }
    static void Check(bool pass, string message)
    { checks++; if (!pass) failures++; string line = (pass ? "PASS " : "FAIL ") + message; results.Add(line); Console.WriteLine(line); }
    static void Paint(Control control, string name, PaintEventArgs args)
    { control.GetType().GetMethod(name, F).Invoke(control, new object[] { args }); }
    static void PaintBoth(Control control, Graphics graphics, Rectangle clip)
    {
        using (var args = new PaintEventArgs(graphics, clip))
        { Paint(control, "OnPaintBackground", args); Paint(control, "OnPaint", args); }
    }
    static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) PaintBoth(control, graphics, control.ClientRectangle);
        return bitmap;
    }
    static Bitmap Native(Control control, bool dirty = false)
    {
        var bitmap = new Bitmap(control.ClientSize.Width, control.ClientSize.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(dirty ? Sentinel : control.Parent == null ? control.BackColor : control.Parent.BackColor);
            if (dirty)
                using (var ink = new SolidBrush(Color.White))
                    for (int y = 0; y < bitmap.Height; y += 13) graphics.DrawString("Previous unrelated control WWWWW", control.Font, ink, 0, y);
            IntPtr dc = graphics.GetHdc(); bool printed;
            try
            {
                printed = PrintWindow(control.Handle, dc, 1);
                if (!printed) printed = PrintWindow(control.Handle, dc, 0);
                if (!printed)
                {
                    SendMessage(control.Handle, 0x0318, dc, new IntPtr(0x0C));
                    results.Add("CAPTURE " + control.GetType().Name + " native WM_PRINTCLIENT fallback (PrintWindow unavailable for this HWND)");
                    printed = true;
                }
            }
            finally { graphics.ReleaseHdc(dc); }
            if (!printed) { bitmap.Dispose(); throw new InvalidOperationException("PrintWindow failed for " + control.GetType().Name); }
        }
        return bitmap;
    }
    static bool Similar(Color a, Color b)
    { return Math.Abs(a.R - b.R) <= 3 && Math.Abs(a.G - b.G) <= 3 && Math.Abs(a.B - b.B) <= 3; }
    static int Differences(Bitmap first, Bitmap second, Rectangle area, Point secondOffset)
    {
        int different = 0;
        for (int y = area.Top; y < area.Bottom; y++) for (int x = area.Left; x < area.Right; x++)
            if (!Similar(first.GetPixel(x, y), second.GetPixel(x + secondOffset.X, y + secondOffset.Y))) different++;
        return different;
    }
    static int OutsideChanges(Bitmap bitmap, Rectangle expected)
    {
        int changed = 0;
        for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
            if (!expected.Contains(x, y) && !Similar(bitmap.GetPixel(x, y), Sentinel)) changed++;
        return changed;
    }
    static Rectangle[] Clips(Control control)
    {
        int middleX = control.Width / 2, middleY = control.Height / 2;
        return new[] { new Rectangle(0, 0, middleX, middleY), new Rectangle(middleX, 0, control.Width - middleX, middleY),
            new Rectangle(0, middleY, middleX, control.Height - middleY), new Rectangle(middleX, middleY, control.Width - middleX, control.Height - middleY) };
    }
    sealed class PaintObserver : NativeWindow, IDisposable
    {
        public readonly List<Rectangle> Dirty = new List<Rectangle>();
        public PaintObserver(Control control) { AssignHandle(control.Handle); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 15) { NativeRect rectangle; if (GetUpdateRect(Handle, out rectangle, false)) Dirty.Add(rectangle.Bounds); }
            base.WndProc(ref message);
        }
        public void Dispose() { ReleaseHandle(); }
    }
    static void Contract(Control control, string root)
    {
        string name = control.GetType().Name;
        using (var baseline = Render(control))
        {
            baseline.Save(Path.Combine(root, name + "-clean.png"));
            var offset = new Point(37, 29); var expected = new Rectangle(offset, control.Size);
            using (var translated = new Bitmap(control.Width + 91, control.Height + 73))
            {
                using (var graphics = Graphics.FromImage(translated))
                {
                    graphics.Clear(Sentinel); graphics.TranslateTransform(offset.X, offset.Y); graphics.SetClip(control.ClientRectangle);
                    PaintBoth(control, graphics, control.ClientRectangle);
                }
                translated.Save(Path.Combine(root, name + "-translated.png"));
                int different = Differences(baseline, translated, control.ClientRectangle, offset);
                Check(different == 0, name + " translated paint matches clean local paint [different pixels " + different + "]");
                int leaked = OutsideChanges(translated, expected);
                Check(leaked == 0, name + " translated paint stays inside its control bounds [outside pixels " + leaked + "]");
            }
            using (var assembled = new Bitmap(control.Width, control.Height))
            {
                using (var target = Graphics.FromImage(assembled))
                {
                    target.Clear(Sentinel);
                    foreach (Rectangle clip in Clips(control))
                    {
                        using (var piece = new Bitmap(clip.Width, clip.Height))
                        {
                            using (var graphics = Graphics.FromImage(piece))
                            { graphics.Clear(Sentinel); graphics.TranslateTransform(-clip.X, -clip.Y); graphics.SetClip(clip); PaintBoth(control, graphics, clip); }
                            target.DrawImageUnscaled(piece, clip.Location);
                        }
                    }
                }
                assembled.Save(Path.Combine(root, name + "-partial.png"));
                int different = Differences(baseline, assembled, control.ClientRectangle, Point.Empty);
                Check(different == 0, name + " four partial buffered paints reconstruct a clean repaint [different pixels " + different + "]");
            }
        }
    }
    static void NativeRepaint(Control control, string root)
    {
        string name = control.GetType().Name;
        using (var observer = new PaintObserver(control))
        {
            foreach (Rectangle clip in Clips(control)) { control.Invalidate(clip); control.Update(); }
            Check(observer.Dirty.Count >= 4 && observer.Dirty.Any(r => r.X > 0 && r.Y > 0), name + " received actual WM_PAINT updates with a nonzero dirty origin");
            results.Add("DIRTY " + name + " " + string.Join("; ", observer.Dirty));
        }
        using (var before = Native(control))
        {
            before.Save(Path.Combine(root, name + "-native-after-partial.png"));
            using (var dirty = Native(control, true))
            {
                dirty.Save(Path.Combine(root, name + "-native-dirty-buffer.png"));
                int different = Differences(before, dirty, control.ClientRectangle, Point.Empty);
                Check(different == 0, name + " native paint completely replaces unrelated pixels in a reused buffer [different pixels " + different + "]");
            }
            control.Invalidate(); control.Update();
            using (var after = Native(control))
            {
                after.Save(Path.Combine(root, name + "-native-after-full.png"));
                int different = Differences(before, after, control.ClientRectangle, Point.Empty);
                Check(different == 0, name + " per-control native capture is stable after partial/full live repaint [different pixels " + different + "]");
            }
        }
    }
    static void Composition(Form form, Control[] controls, string root)
    {
        using (var composite = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        using (var graphics = Graphics.FromImage(composite))
        {
            graphics.Clear(form.BackColor);
            foreach (Control control in controls) using (var child = Native(control)) graphics.DrawImageUnscaled(child, control.Location);
            composite.Save(Path.Combine(root, "native-independent-controls.png"));
            using (var whole = Native(form))
            {
                whole.Save(Path.Combine(root, "native-shared-window.png"));
                int different = Differences(composite, whole, form.ClientRectangle, Point.Empty);
                Check(different == 0, "Native shared-window paint matches independently captured controls [different pixels " + different + "]");
            }
        }
    }
    static IEnumerable<Control> Children(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; } }
    static void Call(object target, string name, params object[] args)
    { target.GetType().GetMethods(F).First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args); }
    static bool FullyVisible(Control control)
    {
        if (!control.Visible || control.Width <= 0 || control.Height <= 0) return false;
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control parent = control.Parent; parent != null; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds)) return false;
        return true;
    }
    static void SettingsFrame(Form form, string stage, string root)
    {
        Application.DoEvents(); form.Refresh(); Application.DoEvents();
        var page = (ScrollableControl)((System.Collections.IDictionary)Get(form, "pages"))["settings"];
        Check(stage.Contains("scrolled") ? page.AutoScrollPosition.Y < 0 : page.AutoScrollPosition.Y == 0,
            "Settings " + stage + " uses the intended live scroll position [" + page.AutoScrollPosition.Y + "]");
        Control[] controls = Children(form).Where(FullyVisible).Where(c => c.GetType().Name == "HudCheckBox" || c.GetType().Name == "HudButton" || c.GetType().Name == "HudNavButton")
            .GroupBy(c => c.GetType().Name).SelectMany(g => g.Take(3)).ToArray();
        Check(controls.Any(c => c.GetType().Name == "HudCheckBox"), "Settings " + stage + " exposes checkbox repaint fixtures");
        foreach (Control control in controls) foreach (Rectangle clip in Clips(control)) { control.Invalidate(clip); control.Update(); }
        using (var shared = Native(form))
        {
            shared.Save(Path.Combine(root, "settings-" + stage + "-native.png"));
            foreach (Control control in controls)
            {
                using (var fresh = Native(control))
                using (var dirty = Native(control, true))
                {
                    int stale = Differences(fresh, dirty, control.ClientRectangle, Point.Empty);
                    Check(stale == 0, "Settings " + stage + " " + control.GetType().Name + " replaces dirty pixels: " + control.Text + " [" + stale + "]");
                    Point origin = form.PointToClient(control.PointToScreen(Point.Empty));
                    int sharedDifference = Differences(fresh, shared, control.ClientRectangle, origin);
                    Check(sharedDifference == 0, "Settings " + stage + " shared native paint matches independent control: " + control.Text + " [" + sharedDifference + "]");
                }
            }
        }
    }
    static void Settings(string root)
    {
        object preferences = Get(T("HudPreferences"), "Current");
        Set(preferences, "DesktopWidth", 1280); Set(preferences, "DesktopHeight", 900); Set(preferences, "Page", "settings"); Set(preferences, "SoundAlerts", false);
        Set(T("AccountWorkspace"), "accessToken", ""); Set(T("AccountWorkspace"), "activeClient", null);
        var gate = (SemaphoreSlim)Get(T("AccountWorkspace"), "RefreshGate");
        if (!gate.Wait(0)) throw new InvalidOperationException("Could not suppress authenticated account refresh.");
        ((System.Threading.Timer)Get(T("AccountWorkspace"), "saveTimer")).Change(Timeout.Infinite, Timeout.Infinite);
        try
        {
            Check(!(bool)Get(T("AccountWorkspace"), "IsAuthenticated"), "Settings repaint fixture has no authenticated session");
            T("Proxy.DataStructures.GameData").GetMethod("LoadGameData", F).Invoke(null, null);
            using (var form = (Form)Activator.CreateInstance(T("ExaltDashboardForm"), true))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(100, 90); form.Show();
                ((System.Windows.Forms.Timer)Get(form, "refreshTimer")).Stop(); Call(form, "SelectPage", "settings"); Call(form, "RefreshDashboard");
                var page = (ScrollableControl)((System.Collections.IDictionary)Get(form, "pages"))["settings"];
                SettingsFrame(form, "initial", root);
                form.ActiveControl = null; page.AutoScrollPosition = new Point(0, 430); SettingsFrame(form, "scrolled", root);
                form.Size = form.MinimumSize; Application.DoEvents(); page.AutoScrollPosition = new Point(0, 760); SettingsFrame(form, "minimum-scrolled", root);
                form.Size = new Size(1280, 900); Application.DoEvents(); page.AutoScrollPosition = Point.Empty; SettingsFrame(form, "restored", root);
                Call(form, "Shutdown"); Check(form.IsDisposed, "Settings repaint closes its dashboard");
            }
            Check(!(bool)Get(T("AccountWorkspace"), "IsAuthenticated"), "Settings repaint completes without an authenticated session");
        }
        finally { Set(T("AccountWorkspace"), "dirty", false); gate.Release(); }
    }
    public void Run(string path, string root)
    {
        app = Assembly.LoadFrom(path); Environment.CurrentDirectory = Path.GetDirectoryName(path);
        var preferences = Get(T("HudPreferences"), "Current"); Set(preferences, "Theme", "Midnight");
        Color background = (Color)Get(T("HudTheme"), "Background"), surface = (Color)Get(T("HudTheme"), "Surface"), foreground = (Color)Get(T("HudTheme"), "Text");
        try
        {
            using (var form = new Form { ClientSize = new Size(580, 505), BackColor = background, StartPosition = FormStartPosition.Manual, Location = new Point(120, 110), Text = "HUD repaint regression", ShowInTaskbar = false })
            {
                string[] names = { "HudButton", "HudNavButton", "HudCheckBox", "HudStatCard", "HudBrandPanel" };
                var controls = names.Select(n => (Control)Activator.CreateInstance(T(n), true)).ToArray();
                for (int i = 0; i < controls.Length; i++)
                {
                    Control control = controls[i]; control.Location = new Point(57 + i * 7, 38 + i * 84); control.Size = new Size(330, i == 4 ? 82 : 38);
                    control.BackColor = surface; control.ForeColor = foreground; control.Font = (Font)Get(T("HudTheme"), "BodyFont"); control.TabStop = false;
                    form.Controls.Add(control);
                }
                controls[0].Text = "Choose accent color"; controls[0].Padding = new Padding(12, 5, 12, 5);
                controls[1].Text = "Player inspection"; Set(controls[1], "IconKey", "players");
                controls[2].Text = "Use compact table rows and buttons"; ((CheckBox)controls[2]).Checked = true; controls[2].Height = 24;
                controls[3].Tag = "HP"; controls[3].Text = "HP   692 / 770"; Set(controls[3], "Progress", (float?).9f);
                form.Show(); Application.DoEvents(); form.Refresh(); Application.DoEvents();
                foreach (Control control in controls)
                {
                    foreach (ControlStyles style in new[] { ControlStyles.UserPaint, ControlStyles.Opaque, ControlStyles.AllPaintingInWmPaint, ControlStyles.OptimizedDoubleBuffer, ControlStyles.SupportsTransparentBackColor })
                        results.Add("STYLE " + control.GetType().Name + " " + style + "=" + typeof(Control).GetMethod("GetStyle", F).Invoke(control, new object[] { style }));
                    Contract(control, root); NativeRepaint(control, root);
                }
                Composition(form, controls, root);
                form.Close();
            }
            Settings(root);
        }
        finally { File.WriteAllLines(Path.Combine(root, "review.txt"), results); }
        Console.WriteLine("HUD repaint regression: " + checks + " checks, " + failures + " failures. Images: " + root);
        if (failures > 0) throw new InvalidOperationException(failures + " repaint assertions failed. See review.txt.");
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($harness))
$setup = New-Object AppDomainSetup
$setup.ApplicationBase = Split-Path $assembly -Parent
$domain = [AppDomain]::CreateDomain('HUD native repaint regression', $null, $setup)
try {
    $runner = $domain.CreateInstanceFromAndUnwrap($harness, 'HudRepaintingRegression')
    $runner.Run($assembly, $testRoot)
} finally {
    [AppDomain]::Unload($domain)
    $env:EXALTHELPER_HUD_DATA = $priorData
    @{ Assembly=$assembly; SHA256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash; CreatedUtc=[DateTime]::UtcNow.ToString('o'); Images=$testRoot; DesktopCapture=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'manifest.json') -Encoding UTF8
}
