param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotDirectory = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
public static class OverlayScrollRegression
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly assembly;
    static Form form;
    static object snapshot;
    static int checks;
    static Type T(string name) { return assembly.GetType(name, true); }
    static object New(string name) { return Activator.CreateInstance(T("ExaltHelper.Proxy.DataStructures." + name), true); }
    static object Get(object o, string name)
    {
        var field = o.GetType().GetField(name, F);
        return field != null ? field.GetValue(o) : o.GetType().GetProperty(name, F).GetValue(o, null);
    }
    static void Set(object o, string name, object value)
    {
        var field = o.GetType().GetField(name, F);
        if (field != null) field.SetValue(o, value);
        else o.GetType().GetProperty(name, F).SetValue(o, value, null);
    }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, F).Invoke(o, args); }
    static void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
    static void Paint(string screenshot)
    {
        using (var bitmap = new Bitmap(form.Width, form.Height))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var args = new PaintEventArgs(graphics, form.ClientRectangle))
        {
            Call(form, "OnPaint", args);
            if (!string.IsNullOrEmpty(screenshot)) bitmap.Save(screenshot, ImageFormat.Png);
        }
    }
    static void Mouse(string method, MouseButtons button, int x, int y, int delta)
    {
        Call(form, method, new MouseEventArgs(button, 1, x, y, delta));
    }
    static int Offset(object bar) { return (int)Get(bar, "Offset"); }
    static Rectangle Rect(object bar, string property) { return (Rectangle)Get(bar, property); }
    static void Select(int tab, int subTab, bool dropdown)
    {
        Call(form, "SelectTab", tab); Set(form, "parseSubTab", subTab);
        Set(form, "isMonsterDropdownOpen", dropdown); Paint(null);
    }
    static void Populate()
    {
        snapshot = New("DpsSnapshot");
        Set(snapshot, "DamageEstimateNote", "Estimated damage includes ability projectiles");
        object enemy = New("EnemyCombatSnapshot"); Set(enemy, "Name", "Test Target");
        Set(snapshot, "ActiveEnemy", enemy);
        for (int i = 0; i < 60; i++)
        {
            object player = New("PlayerParseEntry"); Set(player, "Name", "Player " + i); Set(player, "ClassName", "Wizard");
            ((IList)Get(snapshot, "Players")).Add(player);
            object damage = New("PlayerDamageEntry"); Set(damage, "Name", "Player " + i); Set(damage, "ClassName", "Wizard");
            Set(damage, "Damage", (long)(6000 - i)); ((IList)Get(enemy, "Damagers")).Add(damage);
            object ability = New("AbilityLogEntry"); Set(ability, "PlayerName", "Player " + i);
            Set(ability, "AbilityType", i % 2 == 0 ? "DECOY" : "STUN"); ((IList)Get(snapshot, "AbilityLog")).Add(ability);
            object party = New("PartyMemberEntry"); Set(party, "Name", "Player " + i); Set(party, "IsInDungeon", i % 2 == 0);
            ((IList)Get(snapshot, "PartyMembers")).Add(party);
            ((IList)Get(snapshot, "LootBags")).Add(New("LootBagDrop"));
            ((IList)Get(snapshot, "DefeatedEnemies")).Add(New("EnemyCombatSnapshot"));
        }
        Call(form, "UpdateData", snapshot);
    }
    static void Exercise(string field, int tab, int subTab, bool dropdown, string screenshots)
    {
        Select(tab, subTab, dropdown);
        object bar = Get(form, field);
        Set(bar, "Offset", 0); Paint(null);
        Rectangle track = Rect(bar, "Track");
        Check(!track.IsEmpty && track.Bottom <= form.Height - 8, field + ": track is missing or overlaps resize grip");
        int x = track.X + track.Width / 2;
        int maximum = (int)Get(bar, "MaximumOffset");
        int page = (int)Get(bar, "VisibleRows");
        Mouse("OnMouseDown", MouseButtons.Left, x, track.Bottom - 2, 0);
        Mouse("OnMouseUp", MouseButtons.Left, x, track.Bottom - 2, 0);
        Check(Offset(bar) == Math.Min(page, maximum), field + ": track click did not page down");
        Paint(null);
        Mouse("OnMouseDown", MouseButtons.Left, x, track.Top + 1, 0);
        Mouse("OnMouseUp", MouseButtons.Left, x, track.Top + 1, 0);
        Check(Offset(bar) == 0, field + ": track click did not page up");
        Paint(null);
        Rectangle thumb = Rect(bar, "Thumb");
        int grab = thumb.Height / 2;
        Mouse("OnMouseDown", MouseButtons.Left, x, thumb.Top + grab, 0);
        Check(form.Capture && Get(form, "draggingScrollBar") == bar, field + ": thumb did not capture mouse");
        Mouse("OnMouseMove", MouseButtons.Left, x, track.Bottom - thumb.Height + grab, 0);
        Check(Offset(bar) == maximum, field + ": dragging could not reach final row");
        Paint(null);
        Mouse("OnMouseMove", MouseButtons.Left, x, track.Bottom + 200, 0);
        Check(Offset(bar) == maximum, field + ": drag beyond bottom exceeded list");
        Mouse("OnMouseMove", MouseButtons.Left, x, track.Top - 200, 0);
        Check(Offset(bar) == 0, field + ": drag beyond top exceeded list");
        Mouse("OnMouseUp", MouseButtons.Left, x, track.Top - 200, 0);
        Check(!form.Capture && Get(form, "draggingScrollBar") == null, field + ": mouse release left dragging active");
        Paint(null);
        int rows = SystemInformation.MouseWheelScrollLines;
        if (rows < 0) rows = page;
        Set(form, "wheelDeltaRemainder", 0);
        Mouse("OnMouseWheel", MouseButtons.None, x - 40, track.Top + 2, -60);
        Check(Offset(bar) == 0, field + ": partial wheel delta jumped early");
        Mouse("OnMouseWheel", MouseButtons.None, x - 40, track.Top + 2, -60);
        Check(Offset(bar) == Math.Min(rows, maximum), field + ": high resolution wheel deltas were lost");
        Mouse("OnMouseWheel", MouseButtons.None, x - 40, track.Top + 2, -240);
        Check(Offset(bar) == Math.Min(rows * 3, maximum), field + ": multiple wheel notches were lost");
        if (rows > 0)
        {
            for (int i = 0; i < 100; i++) Mouse("OnMouseWheel", MouseButtons.None, x - 40, track.Top + 2, -120);
            Check(Offset(bar) == maximum, field + ": wheel cannot reach final row");
        }
        Paint(null);
        thumb = Rect(bar, "Thumb");
        Mouse("OnMouseDown", MouseButtons.Left, x, thumb.Top + 2, 0);
        form.Capture = false;
        Check(Get(form, "draggingScrollBar") == null, field + ": losing capture left a stuck drag");
        Call(form, "SetClickThrough", true);
        int lockedOffset = Offset(bar);
        Mouse("OnMouseWheel", MouseButtons.None, x, track.Top, 120);
        Mouse("OnMouseDown", MouseButtons.Left, x, track.Top, 0);
        Check(Offset(bar) == lockedOffset && Get(form, "draggingScrollBar") == null, field + ": locked overlay consumed input");
        Call(form, "SetClickThrough", false);
        if (!string.IsNullOrEmpty(screenshots)) Paint(Path.Combine(screenshots, field + ".png"));
        form.Height = 600; Paint(null);
        Check(Offset(bar) <= (int)Get(bar, "MaximumOffset"), field + ": resizing did not clamp offset");
        form.Height = 440;
    }
    public static void Run(string path, string screenshots)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        form = (Form)Activator.CreateInstance(T("ExaltHelper.DpsOverlayForm"), true);
        try
        {
            ((Timer)Get(form, "updateTimer")).Stop();
            form.Location = new Point(-2000, -2000);
            IntPtr handle = form.Handle;
            Populate(); Select(1, 0, false);
            // Exercise Parse scrollbar input through the public mouse handlers.
            if (form.GetType().GetField("parseScrollOffset", F) != null)
            {
                Mouse("OnMouseDown", MouseButtons.Left, form.Width - 14, 420, 0);
                Check((int)Get(form, "parseScrollOffset") > 0, "Parse scrollbar ignores clicks");
                throw new Exception("Old overlay fields are still present");
            }
            Exercise("parseScrollBar", 1, 0, false, screenshots);
            Exercise("abilityScrollBar", 1, 1, false, screenshots);
            Exercise("partyScrollBar", 1, 2, false, screenshots);
            Exercise("dpsScrollBar", 0, 0, false, screenshots);
            Exercise("lootScrollBar", 2, 0, false, screenshots);
            Exercise("dropdownScrollBar", 0, 0, true, screenshots);
            // One party group has two header rows, while mixed groups have four.
            foreach (object member in (IList)Get(snapshot, "PartyMembers")) Set(member, "IsInDungeon", true);
            Select(1, 2, false);
            object partyBar = Get(form, "partyScrollBar");
            Check((int)Get(partyBar, "MaximumOffset") + (int)Get(partyBar, "VisibleRows") == 62,
                "Party wheel counts phantom group headers");
            Select(1, 1, false); Set(form, "parseAbilityFilter", 1); Paint(null);
            object abilityBar = Get(form, "abilityScrollBar");
            Check((int)Get(abilityBar, "MaximumOffset") + (int)Get(abilityBar, "VisibleRows") == 30,
                "Ability wheel ignores filtered count");
            Set(form, "isCompact", true); Paint(null);
            int before = Offset(abilityBar);
            Mouse("OnMouseWheel", MouseButtons.None, 100, 150, -120);
            Check(Offset(abilityBar) == before, "Compact overlay scrolled hidden rows");
            Set(form, "isCompact", false); Set(form, "parseAbilityFilter", 0);
            Select(1, 0, false);
            object playerBar = Get(form, "parseScrollBar"); Set(playerBar, "Offset", 50);
            IList players = (IList)Get(snapshot, "Players");
            while (players.Count > 2) players.RemoveAt(players.Count - 1);
            Paint(null);
            Check(Offset(playerBar) == 0 && Rect(playerBar, "Track").IsEmpty, "Shortened list retained stale scrolling");
            players.Clear(); Paint(null);
            Check(Rect(playerBar, "Track").IsEmpty, "Empty list retained scrollbar hit targets");
            Console.WriteLine("PASS: " + checks + " overlay scrolling assertions across six lists.");
        }
        finally { form.Dispose(); }
    }
}
'@
if ($ScreenshotDirectory) { New-Item -ItemType Directory -Path $ScreenshotDirectory -Force | Out-Null }
try { [OverlayScrollRegression]::Run($AssemblyPath, $ScreenshotDirectory) }
catch { Write-Host $_.Exception.ToString(); exit 1 }
