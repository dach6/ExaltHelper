using System;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal static class DpsOverlayManager
{
	private static readonly object initLock = new object();

	private static volatile bool isInitialized = false;

	private static Thread overlayThread;

	private static DpsOverlayForm overlayForm;

	public static void Initialize()
	{
		lock (initLock)
		{
			if (!isInitialized)
			{
				isInitialized = true;
				overlayThread = new Thread(OverlayThreadProc)
				{
					Name = "ExaltHudOverlayThread",
					IsBackground = true
				};
				overlayThread.SetApartmentState(ApartmentState.STA);
				overlayThread.Start();
			}
		}
	}

	private static void OverlayThreadProc()
	{
		try
		{
			overlayForm = new DpsOverlayForm();
			Application.Run(overlayForm);
		}
		catch (Exception ex)
		{
			Program.LogError("overlay", "Overlay thread encountered an error: " + ex.Message);
		}
	}

	public static void UpdateSnapshot(DpsSnapshot snapshot)
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.BeginInvoke((Action)delegate
			{
				if (!overlayForm.IsDisposed)
				{
					overlayForm.UpdateData(snapshot);
				}
			});
		}
		catch
		{
		}
	}

	public static void UpdateMoonlightVillage(MoonlightVillageSnapshot mvSnapshot)
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.BeginInvoke((Action)delegate
			{
				if (!overlayForm.IsDisposed)
				{
					overlayForm.UpdateMoonlightVillage(mvSnapshot);
				}
			});
		}
		catch
		{
		}
	}

	public static void SwitchToMoonlightVillageTab()
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.BeginInvoke((Action)delegate
			{
				if (!overlayForm.IsDisposed)
				{
					overlayForm.SelectTab(3);
				}
			});
		}
		catch
		{
		}
	}

	public static void RequestRedraw()
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.BeginInvoke((Action)delegate
			{
				if (!overlayForm.IsDisposed)
				{
					overlayForm.Invalidate();
				}
			});
		}
		catch
		{
		}
	}

	public static void ToggleVisibility()
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.BeginInvoke((Action)delegate
			{
				if (!overlayForm.IsDisposed)
				{
					overlayForm.ToggleVisibility();
				}
			});
		}
		catch
		{
		}
	}

	public static void ToggleClickThrough()
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.BeginInvoke((Action)delegate
			{
				if (!overlayForm.IsDisposed)
				{
					overlayForm.ToggleClickThrough();
				}
			});
		}
		catch
		{
		}
	}

	public static void Shutdown()
	{
		if (overlayForm == null || !overlayForm.IsHandleCreated || overlayForm.IsDisposed)
		{
			return;
		}
		try
		{
			overlayForm.Invoke((Action)delegate
			{
				overlayForm.Close();
				overlayForm.Dispose();
			});
		}
		catch
		{
		}
	}
}
