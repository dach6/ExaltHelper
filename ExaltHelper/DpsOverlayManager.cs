using System;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal static class DpsOverlayManager
{
    private static readonly object initLock = new object();
    private static bool isInitialized;
    private static Thread overlayThread;
    private static DpsOverlayForm overlayForm;
    private static ExaltDashboardForm dashboardForm;
    private static DpsSnapshot lastSnapshot = new DpsSnapshot();
    private static MoonlightVillageSnapshot lastMoonlight = new MoonlightVillageSnapshot();
    public static bool IsExpanded { get; private set; }

    public static void Initialize()
    {
        lock (initLock)
        {
            if (isInitialized) return;
            isInitialized = true;
            overlayThread = new Thread(() => {
                try
                {
                    overlayForm = new DpsOverlayForm();
                    overlayForm.Shown += (_, __) => SetExpandedCore(HudPreferences.Current.Expanded);
                    Application.Run(overlayForm);
                }
                catch (Exception ex) { Program.LogError("overlay", "HUD stopped: " + ex.GetType().Name); }
                finally { lock (initLock) isInitialized = false; }
            }) { IsBackground = true, Name = "ExaltHudOverlayThread" };
            overlayThread.SetApartmentState(ApartmentState.STA); overlayThread.Start();
        }
    }
    private static void Apply(Action action)
    {
        var view = overlayForm;
        if (view == null || view.IsDisposed || !view.IsHandleCreated) return;
        try
        {
            if (view.InvokeRequired) view.BeginInvoke((Action)(() => { if (!view.IsDisposed) action(); }));
            else action();
        }
        catch (InvalidOperationException) { }
    }
    public static void SetExpanded(bool expanded)
    {
        HudPreferences.Current.Expanded = expanded;
        Initialize(); Apply(() => SetExpandedCore(expanded));
    }
    public static void ToggleExpanded() => SetExpanded(!IsExpanded);
    private static void SetExpandedCore(bool expanded)
    {
        IsExpanded = expanded; HudPreferences.Current.Expanded = expanded;
        if (expanded)
        {
            overlayForm.SaveOverlayLayout(); overlayForm.SetDesktopMode(true);
            if (dashboardForm == null || dashboardForm.IsDisposed)
            {
                dashboardForm = new ExaltDashboardForm();
                dashboardForm.CompactRequested += (_, __) => SetExpandedCore(false);
            }
            dashboardForm.UpdateSnapshot(lastSnapshot); dashboardForm.UpdateMoonlightVillage(lastMoonlight);
            dashboardForm.Show();
            if (dashboardForm.WindowState == FormWindowState.Minimized) dashboardForm.WindowState = FormWindowState.Normal;
            dashboardForm.Activate();
        }
        else
        {
            dashboardForm?.SaveLayout(); dashboardForm?.Hide(); overlayForm.SetDesktopMode(false);
            if (!overlayForm.Visible) overlayForm.ToggleVisibility();
        }
        HudPreferences.Current.Save();
    }
    public static void UpdateSnapshot(DpsSnapshot snapshot)
    {
        if (snapshot == null) return; lastSnapshot = snapshot;
        Apply(() => { overlayForm.UpdateData(snapshot); dashboardForm?.UpdateSnapshot(snapshot); });
    }
    public static void UpdateMoonlightVillage(MoonlightVillageSnapshot value)
    {
        if (value == null) return; lastMoonlight = value;
        Apply(() => { overlayForm.UpdateMoonlightVillage(value); dashboardForm?.UpdateMoonlightVillage(value); });
    }
    public static void SwitchToMoonlightVillageTab() => Apply(() => { overlayForm.SelectTab(3); dashboardForm?.SelectPage("moonlight"); });
    public static void RequestRedraw() => Apply(() => { overlayForm.Invalidate(); dashboardForm?.Invalidate(true); });
    public static void ToggleVisibility() => Apply(() => {
        if (IsExpanded && dashboardForm != null)
        {
            if (dashboardForm.Visible) dashboardForm.Hide();
            else { dashboardForm.Show(); if (dashboardForm.WindowState == FormWindowState.Minimized) dashboardForm.WindowState = FormWindowState.Normal; }
        }
        else overlayForm.ToggleVisibility();
    });
    public static void ToggleClickThrough() => Apply(() => { if (!IsExpanded) overlayForm.ToggleClickThrough(); });
    public static void Shutdown()
    {
        var view = overlayForm;
        void Flush() { HudSounds.Shutdown(); HudJournal.Flush(); AccountWorkspace.SavePending(); MissionWorkspace.Flush(); }
        if (view == null || view.IsDisposed || !view.IsHandleCreated) { Flush(); return; }
        Action close = () => {
            view.SaveOverlayLayout(); dashboardForm?.Shutdown(); dashboardForm?.Dispose();
            Flush(); view.Close(); view.Dispose();
        };
        try { if (view.InvokeRequired) view.Invoke(close); else close(); }
        catch (InvalidOperationException) { Flush(); }
    }
}
