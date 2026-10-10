using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal class DpsOverlayForm : Form
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private class DarkContextMenuColorTable : ProfessionalColorTable
	{
		public override Color MenuBorder => Color.FromArgb(50, 65, 85);

		public override Color MenuItemBorder => Color.FromArgb(0, 180, 220);

		public override Color MenuItemSelected => Color.FromArgb(32, 44, 62);

		public override Color MenuStripGradientBegin => Color.FromArgb(16, 20, 28);

		public override Color MenuStripGradientEnd => Color.FromArgb(16, 20, 28);

		public override Color ToolStripDropDownBackground => Color.FromArgb(16, 20, 28);

		public override Color ImageMarginGradientBegin => Color.FromArgb(20, 25, 35);

		public override Color ImageMarginGradientMiddle => Color.FromArgb(20, 25, 35);

		public override Color ImageMarginGradientEnd => Color.FromArgb(20, 25, 35);

		public override Color SeparatorDark => Color.FromArgb(45, 55, 75);

		public override Color SeparatorLight => Color.FromArgb(30, 40, 55);
	}

	private const int WS_EX_TOPMOST = 8;

	private const int WS_EX_TRANSPARENT = 32;

	private const int WS_EX_LAYERED = 524288;

	private const int WS_EX_NOACTIVATE = 134217728;

	private const int GWL_EXSTYLE = -20;

	private bool isClickThrough;

	private bool isCompact;

	private bool isUserHidden;

	private int activeTab;

	private bool wasF8Down;

	private bool wasF9Down;

	private bool isMonsterDropdownOpen;

	private readonly OverlayScrollBar dropdownScrollBar = new OverlayScrollBar();

	private Rectangle monsterDropdownBarRect;

	private Rectangle monsterDropdownPopupRect;

	private OverlayScrollBar draggingScrollBar;

	private int scrollThumbGrabOffset;

	private int wheelDeltaRemainder;

	private OverlayScrollBar wheelScrollBar;

	private readonly OverlayScrollBar dpsScrollBar = new OverlayScrollBar();

	private readonly OverlayScrollBar parseScrollBar = new OverlayScrollBar();

	private int parseSubTab;

	private int parseAbilityFilter;

	private readonly OverlayScrollBar abilityScrollBar = new OverlayScrollBar();

	private Rectangle subTabPlayersRect;

	private Rectangle subTabAbilitiesRect;

	private Rectangle subTabPartyRect;

	private Rectangle abilityClearBtnRect;

	private Rectangle partyClearBtnRect;

	private readonly OverlayScrollBar partyScrollBar = new OverlayScrollBar();

	private class PartyDisplayItem
	{
		public bool IsHeader;
		public string HeaderText = "";
		public bool IsWarning;
		public bool IsColumnHeader;
		public string Col1 = "";
		public string Col2 = "";
		public string Col3 = "";
		public string Col4 = "";
		public PartyMemberEntry Member;
		public bool IsMissing;
	}

	private Rectangle filterAllRect;

	private Rectangle filterDecoyRect;

	private Rectangle filterStasisRect;

	private Rectangle filterStunRect;

	private readonly OverlayScrollBar lootScrollBar = new OverlayScrollBar();

	private bool isResizingBottom;

	private int resizeDragStartY;

	private int resizeDragStartHeight;

	private string hoveredTooltipText;

	private Point hoveredTooltipPos;

	private Point dragStartPoint;

	private bool isDragging;

	private bool hasCustomPosition;

	private bool isLeftDragHandleHovered;

	private bool isRightDragHandleHovered;

	private int dungeonDropdownHitRight = 130;

	private readonly Timer updateTimer;

	private IntPtr cachedGameHwnd = IntPtr.Zero;

	private DpsSnapshot currentSnapshot = new DpsSnapshot();

	private MoonlightVillageSnapshot currentMvSnapshot = new MoonlightVillageSnapshot();

	private Rectangle mvBtnMinusRect;

	private Rectangle mvBtnPlusRect;

	private Rectangle mvBtnNextRect;

	private Rectangle mvBtnResetRect;

	private int hoveredMvBtn = -1;

	private static readonly Color BgColor = Color.FromArgb(238, 14, 15, 20);

	private static readonly Color HeaderBg = Color.FromArgb(250, 22, 24, 32);

	private static readonly Color StatsBarBg = Color.FromArgb(240, 26, 28, 38);

	private static readonly Color BorderColor = Color.FromArgb(255, 45, 48, 64);

	private static readonly Color AccentCyan = Color.FromArgb(255, 0, 210, 255);

	private static readonly Color GoldColor = Color.FromArgb(255, 255, 204, 0);

	private static readonly Color SilverColor = Color.FromArgb(255, 200, 205, 215);

	private static readonly Color BronzeColor = Color.FromArgb(255, 205, 130, 75);

	private static readonly Color TextWhite = Color.FromArgb(255, 240, 242, 248);

	private static readonly Color TextMuted = Color.FromArgb(255, 140, 145, 160);

	private static readonly Color HpGreen = Color.FromArgb(255, 46, 204, 113);

	private static readonly Color HpRed = Color.FromArgb(255, 231, 76, 60);

	private readonly Font fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold);

	private readonly Font fontTab = new Font("Segoe UI", 8.5f, FontStyle.Bold);

	private readonly Font fontHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold);

	private readonly Font fontRow = new Font("Segoe UI", 8f, FontStyle.Regular);

	private readonly Font fontSmall = new Font("Segoe UI", 7.5f, FontStyle.Regular);

	private readonly Font fontBadge = new Font("Segoe UI", 7f, FontStyle.Bold);

	private readonly Font fontTiny = new Font("Segoe UI", 6.5f, FontStyle.Bold);

	private readonly SolidBrush brushBg = new SolidBrush(BgColor);

	private readonly SolidBrush brushHeaderBg = new SolidBrush(HeaderBg);

	private readonly SolidBrush brushStatsBarBg = new SolidBrush(StatsBarBg);

	private readonly SolidBrush brushAccentCyan = new SolidBrush(AccentCyan);

	private readonly SolidBrush brushGold = new SolidBrush(GoldColor);

	private readonly SolidBrush brushSilver = new SolidBrush(SilverColor);

	private readonly SolidBrush brushBronze = new SolidBrush(BronzeColor);

	private readonly SolidBrush brushTextWhite = new SolidBrush(TextWhite);

	private readonly SolidBrush brushTextMuted = new SolidBrush(TextMuted);

	private readonly SolidBrush brushHpBg = new SolidBrush(Color.FromArgb(40, 18, 22));

	private readonly SolidBrush brushTabActive = new SolidBrush(Color.FromArgb(50, 56, 75));

	private readonly SolidBrush brushTabInactive = new SolidBrush(Color.FromArgb(26, 28, 36));

	private readonly SolidBrush brushTableHeader = new SolidBrush(Color.FromArgb(30, 32, 42));

	private readonly SolidBrush brushLocalPlayerRow = new SolidBrush(Color.FromArgb(40, 0, 160, 200));

	private readonly Pen penBorder = new Pen(BorderColor, 1.5f);

	private readonly Pen penBorderThin = new Pen(BorderColor, 1f);

	private readonly Pen penAccentTab = new Pen(AccentCyan, 2f);

	private readonly Pen penLocalHighlight = new Pen(AccentCyan, 1f);

	private readonly SolidBrush brushGuardGriefRow = new SolidBrush(Color.FromArgb(70, 220, 38, 38));

	private readonly SolidBrush brushGuardGriefText = new SolidBrush(Color.FromArgb(255, 252, 165, 165));

	private readonly Pen penGuardGriefBorder = new Pen(Color.FromArgb(220, 239, 68, 68), 1f);

	private readonly SolidBrush brushDeadText = new SolidBrush(Color.FromArgb(180, 200, 140, 140));

	private readonly SolidBrush brushNexusedText = new SolidBrush(Color.FromArgb(160, 160, 175, 190));

	private ContextMenuStrip dungeonHistoryMenu;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 524296;
			if (isClickThrough)
			{
				createParams.ExStyle |= 134217760;
			}
			return createParams;
		}
	}

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	public DpsOverlayForm()
	{
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		BackColor = Color.FromArgb(14, 15, 20);
		base.Size = new Size(380, 440);
		base.Opacity = 0.94;
		DoubleBuffered = true;
		try
		{
			base.Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 400, 45);
		}
		catch
		{
			base.Location = new Point(100, 100);
		}
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		updateTimer = new Timer
		{
			Interval = 50
		};
		updateTimer.Tick += OnTimerTick;
		updateTimer.Start();
	}

	public void ToggleClickThrough()
	{
		SetClickThrough(!isClickThrough);
	}

	public void SetClickThrough(bool enable)
	{
		if (enable) EndScrollDrag();
		isClickThrough = enable;
		if (base.IsHandleCreated)
		{
			int windowLong = GetWindowLong(base.Handle, -20);
			if (enable)
			{
				SetWindowLong(base.Handle, -20, windowLong | 0x20 | 0x8000000);
			}
			else
			{
				SetWindowLong(base.Handle, -20, windowLong & -134217761);
			}
		}
		Invalidate();
	}

	public void ToggleVisibility()
	{
		isUserHidden = !isUserHidden;
		base.Visible = !isUserHidden;
		Invalidate();
	}

	public void UpdateData(DpsSnapshot snapshot)
	{
		if (snapshot != null)
		{
			currentSnapshot = snapshot;
			Invalidate();
		}
	}

	public void UpdateMoonlightVillage(MoonlightVillageSnapshot snapshot)
	{
		if (snapshot != null)
		{
			currentMvSnapshot = snapshot;
			Invalidate();
		}
	}

	public void SelectTab(int tabIndex)
	{
		if (tabIndex >= 0 && tabIndex <= 3)
		{
			EndScrollDrag();
			activeTab = tabIndex;
			isMonsterDropdownOpen = false;
			Invalidate();
		}
	}

	private void OnTimerTick(object sender, EventArgs e)
	{
		bool flag = (GetAsyncKeyState(119) & 0x8000) != 0;
		bool flag2 = (GetAsyncKeyState(120) & 0x8000) != 0;
		if (flag && !wasF8Down)
		{
			ToggleVisibility();
		}
		if (flag2 && !wasF9Down)
		{
			ToggleClickThrough();
		}
		wasF8Down = flag;
		wasF9Down = flag2;
		IntPtr gameHwnd = GetGameHwnd();
		if (!(gameHwnd != IntPtr.Zero) || !IsWindow(gameHwnd))
		{
			return;
		}
		if (IsIconic(gameHwnd) || !IsWindowVisible(gameHwnd))
		{
			if (base.Visible)
			{
				base.Visible = false;
			}
			return;
		}
		if (!isUserHidden && !base.Visible)
		{
			base.Visible = true;
		}
		if (!hasCustomPosition && GetWindowRect(gameHwnd, out var lpRect))
		{
			int num = lpRect.Right - base.Width - 15;
			int num2 = lpRect.Top + 35;
			if (Math.Abs(base.Left - num) > 2 || Math.Abs(base.Top - num2) > 2)
			{
				base.Location = new Point(num, num2);
			}
		}
	}

	private IntPtr GetGameHwnd()
	{
		if (cachedGameHwnd != IntPtr.Zero && IsWindow(cachedGameHwnd))
		{
			return cachedGameHwnd;
		}
		try
		{
			Process[] processesByName = Process.GetProcessesByName("RotMG Exalt");
			if (processesByName.Length != 0 && !processesByName[0].HasExited)
			{
				cachedGameHwnd = processesByName[0].MainWindowHandle;
				return cachedGameHwnd;
			}
		}
		catch
		{
		}
		return IntPtr.Zero;
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (isClickThrough)
		{
			return;
		}
		if (e.Button != MouseButtons.Left) return;
		// Flush a pending tab, resize, or snapshot repaint before hit testing.
		Update();
		OverlayScrollBar scrollBar = GetActiveScrollBar();
		if (scrollBar != null && scrollBar.Track.Contains(e.Location))
		{
			hoveredTooltipText = null;
			if (scrollBar.Thumb.Contains(e.Location))
			{
				draggingScrollBar = scrollBar;
				scrollThumbGrabOffset = e.Y - scrollBar.Thumb.Top;
				Capture = true;
			}
			else
			{
				scrollBar.Scroll(e.Y < scrollBar.Thumb.Top ? -scrollBar.VisibleRows : scrollBar.VisibleRows);
			}
			Invalidate();
			return;
		}
		if (isMonsterDropdownOpen && activeTab == 0 && !isCompact)
		{
			if (monsterDropdownPopupRect.Contains(e.Location))
			{
				HandleMonsterDropdownClick(e.Location);
			}
			else
			{
				isMonsterDropdownOpen = false;
				Invalidate();
			}
			return;
		}

		if (!isCompact && e.Y >= base.Height - 8 && e.Button == MouseButtons.Left)
		{
			isResizingBottom = true;
			resizeDragStartY = Cursor.Position.Y;
			resizeDragStartHeight = base.Height;
			return;
		}
		if (e.Y <= 26)
		{
			if (e.X >= 4 && e.X <= 22)
			{
				if (e.Button == MouseButtons.Left)
				{
					isDragging = true;
					dragStartPoint = e.Location;
					hasCustomPosition = true;
				}
				return;
			}
			if (e.X >= 24 && e.X <= 42)
			{
				DpsTrackerMod.NavigatePreviousDungeon();
				return;
			}
			if (e.X >= 44 && e.X <= Math.Min(168, dungeonDropdownHitRight))
			{
				ShowDungeonHistoryMenu(new Point(44, 26));
				return;
			}
			if (e.X >= 170 && e.X <= 190)
			{
				DpsTrackerMod.NavigateNextDungeon();
				return;
			}
			if (e.X >= 192 && e.X <= 242)
			{
				DpsTrackerMod.NavigateLiveDungeon();
				return;
			}
			if (e.X >= 244 && e.X <= 302)
			{
				ToggleClickThrough();
				return;
			}
			if (e.X >= 304 && e.X <= 324)
			{
				if (e.Button == MouseButtons.Left)
				{
					isDragging = true;
					dragStartPoint = e.Location;
					hasCustomPosition = true;
				}
				return;
			}
			if (e.X >= 326 && e.X <= 348)
			{
				isCompact = !isCompact;
				base.Height = (isCompact ? 72 : 440);
				Invalidate();
				return;
			}
			if (e.X >= 350 && e.X <= 375)
			{
				ToggleVisibility();
				return;
			}
			if (e.Button == MouseButtons.Left)
			{
				isDragging = true;
				dragStartPoint = e.Location;
				hasCustomPosition = true;
				return;
			}
		}
		if (!isCompact && e.Y >= 26 && e.Y <= 50 && e.Button == MouseButtons.Left)
		{
			isDragging = true;
			dragStartPoint = e.Location;
			hasCustomPosition = true;
		}
		else
		{
			if (isCompact)
			{
				return;
			}
			if (e.Y >= 52 && e.Y <= 74)
			{
				if (e.X >= 10 && e.X <= 93)
				{
					activeTab = 0;
					isMonsterDropdownOpen = false;
					Invalidate();
					return;
				}
				if (e.X >= 94 && e.X <= 183)
				{
					activeTab = 1;
					isMonsterDropdownOpen = false;
					Invalidate();
					return;
				}
				if (e.X >= 184 && e.X <= 269)
				{
					activeTab = 2;
					isMonsterDropdownOpen = false;
					Invalidate();
					return;
				}
				if (e.X >= 270 && e.X <= 372)
				{
					activeTab = 3;
					isMonsterDropdownOpen = false;
					Invalidate();
					return;
				}
			}
			if (activeTab == 0 && monsterDropdownBarRect.Contains(e.Location))
			{
				isMonsterDropdownOpen = !isMonsterDropdownOpen;
				Invalidate();
			}
			if (activeTab == 1)
			{
				if (subTabPlayersRect.Contains(e.Location))
				{
					parseSubTab = 0;
					Invalidate();
					return;
				}
				if (subTabAbilitiesRect.Contains(e.Location))
				{
					parseSubTab = 1;
					Invalidate();
					return;
				}
				if (subTabPartyRect.Contains(e.Location))
				{
					parseSubTab = 2;
					Invalidate();
					return;
				}
				if (parseSubTab == 2)
				{
					if (partyClearBtnRect.Contains(e.Location))
					{
						DpsTrackerMod.ClearParty();
						partyScrollBar.Offset = 0;
						Invalidate();
						return;
					}
				}
				if (parseSubTab == 1)
				{
					if (abilityClearBtnRect.Contains(e.Location))
					{
						DpsTrackerMod.ClearAbilityLog();
						abilityScrollBar.Offset = 0;
						Invalidate();
						return;
					}
					if (filterAllRect.Contains(e.Location))
					{
						parseAbilityFilter = 0;
						abilityScrollBar.Offset = 0;
						Invalidate();
						return;
					}
					if (filterDecoyRect.Contains(e.Location))
					{
						parseAbilityFilter = 1;
						abilityScrollBar.Offset = 0;
						Invalidate();
						return;
					}
					if (filterStasisRect.Contains(e.Location))
					{
						parseAbilityFilter = 2;
						abilityScrollBar.Offset = 0;
						Invalidate();
						return;
					}
					if (filterStunRect.Contains(e.Location))
					{
						parseAbilityFilter = 3;
						abilityScrollBar.Offset = 0;
						Invalidate();
						return;
					}
				}
			}
			if (activeTab == 3)
			{
				if (mvBtnMinusRect.Contains(e.Location))
				{
					MoonlightVillageMod.ActiveInstance?.AddFlameToCurrentPhase(-1);
					Invalidate();
					return;
				}
				if (mvBtnPlusRect.Contains(e.Location))
				{
					MoonlightVillageMod.ActiveInstance?.AddFlameToCurrentPhase(1);
					Invalidate();
					return;
				}
				if (mvBtnNextRect.Contains(e.Location))
				{
					MoonlightVillageMod.ActiveInstance?.NextPhase();
					Invalidate();
					return;
				}
				if (mvBtnResetRect.Contains(e.Location))
				{
					MoonlightVillageMod.ActiveInstance?.ResetCounters();
					Invalidate();
					return;
				}
			}
		}
	}



	private void HandleMonsterDropdownClick(Point pt)
	{
		if (dropdownScrollBar.Track.Contains(pt))
		{
			return;
		}
		int num = pt.Y - monsterDropdownPopupRect.Y - 2;
		int num2 = 22;
		int num3 = num / num2 + dropdownScrollBar.Offset;
		switch (num3)
		{
		case 0:
			currentSnapshot.SelectedMonsterMode = -1;
			DpsTrackerMod.SetSelectedMonsterMode(-1);
			break;
		case 1:
			currentSnapshot.SelectedMonsterMode = -2;
			DpsTrackerMod.SetSelectedMonsterMode(-2);
			break;
		default:
		{
			int num4 = num3 - 2;
			if (num4 >= 0 && num4 < currentSnapshot.DefeatedEnemies.Count)
			{
				currentSnapshot.SelectedMonsterMode = num4;
				DpsTrackerMod.SetSelectedMonsterMode(num4);
			}
			break;
		}
		}
		isMonsterDropdownOpen = false;
		Invalidate();
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (draggingScrollBar != null)
		{
			Update();
			if (isClickThrough || draggingScrollBar != GetActiveScrollBar() || !draggingScrollBar.Visible)
			{
				EndScrollDrag();
				return;
			}
			draggingScrollBar.DragTo(e.Y, scrollThumbGrabOffset);
			Cursor = Cursors.Hand;
			Invalidate();
			return;
		}
		if (isDragging)
		{
			base.Left += e.X - dragStartPoint.X;
			base.Top += e.Y - dragStartPoint.Y;
			return;
		}
		if (isResizingBottom)
		{
			int num = Cursor.Position.Y - resizeDragStartY;
			base.Height = Math.Max(260, Math.Min(Screen.PrimaryScreen.WorkingArea.Height - 40, resizeDragStartHeight + num));
			Invalidate();
			return;
		}
		bool num2 = isLeftDragHandleHovered;
		bool flag = isRightDragHandleHovered;
		isLeftDragHandleHovered = !isClickThrough && e.Y <= 26 && e.X >= 4 && e.X <= 22;
		isRightDragHandleHovered = !isClickThrough && e.Y <= 26 && e.X >= 304 && e.X <= 324;
		if (num2 != isLeftDragHandleHovered || flag != isRightDragHandleHovered)
		{
			Invalidate(new Rectangle(2, 2, 24, 24));
			Invalidate(new Rectangle(302, 2, 24, 24));
		}
		if (!isClickThrough && e.Y <= 26)
		{
			if (isLeftDragHandleHovered || isRightDragHandleHovered)
			{
				Cursor = Cursors.SizeAll;
				if (hoveredTooltipText != "Drag to move HUD")
				{
					hoveredTooltipText = "Drag to move HUD";
					hoveredTooltipPos = new Point(Math.Max(10, Math.Min(base.Width - 140, e.X)), 30);
					Invalidate();
				}
			}
			else if (e.X >= 44 && e.X <= Math.Min(168, dungeonDropdownHitRight))
			{
				Cursor = Cursors.Hand;
				if (hoveredTooltipText == "Drag to move HUD")
				{
					hoveredTooltipText = null;
					Invalidate();
				}
			}
			else if ((e.X >= 24 && e.X <= 42) || (e.X >= 170 && e.X <= 190) || (e.X >= 192 && e.X <= 242) || (e.X >= 244 && e.X <= 302) || (e.X >= 326 && e.X <= 375))
			{
				Cursor = Cursors.Hand;
				if (hoveredTooltipText == "Drag to move HUD")
				{
					hoveredTooltipText = null;
					Invalidate();
				}
			}
			else
			{
				Cursor = Cursors.SizeAll;
				if (hoveredTooltipText == "Drag to move HUD")
				{
					hoveredTooltipText = null;
					Invalidate();
				}
			}
		}
		else if (!isClickThrough && !isCompact && e.Y >= 26 && e.Y <= 50)
		{
			Cursor = Cursors.SizeAll;
			if (hoveredTooltipText == "Drag to move HUD")
			{
				hoveredTooltipText = null;
				Invalidate();
			}
		}
		else if (!isClickThrough && !isCompact && e.Y >= base.Height - 8)
		{
			Cursor = Cursors.SizeNS;
			if (hoveredTooltipText == "Drag to move HUD")
			{
				hoveredTooltipText = null;
				Invalidate();
			}
		}
		else
		{
			Cursor = Cursors.Default;
			if (hoveredTooltipText == "Drag to move HUD")
			{
				hoveredTooltipText = null;
				Invalidate();
			}
		}
		OverlayScrollBar hoveredScrollBar = GetActiveScrollBar();
		if (!isClickThrough && hoveredScrollBar != null && hoveredScrollBar.Track.Contains(e.Location))
		{
			Cursor = Cursors.Hand;
			if (hoveredTooltipText != null)
			{
				hoveredTooltipText = null;
				Invalidate();
			}
		}

		else if (!isClickThrough && !isCompact)
		{
			if (activeTab == 0)
			{
				CheckDpsTableTooltip(e.Location);
			}
			else if (activeTab == 1 || activeTab == 2)
			{
				CheckEquipmentTooltip(e.Location);
			}
			else if (activeTab == 3)
			{
				CheckMoonlightVillageTooltip(e.Location);
			}
			else if (hoveredTooltipText != null)
			{
				hoveredTooltipText = null;
				Invalidate();
			}
		}
		else if (hoveredTooltipText != null)
		{
			hoveredTooltipText = null;
			Invalidate();
		}
	}

	private void CheckDpsTableTooltip(Point pt)
	{
		EnemyCombatSnapshot enemyCombatSnapshot = currentSnapshot?.GetCurrentDisplayEnemy();
		if (enemyCombatSnapshot == null)
		{
			if (hoveredTooltipText != null)
			{
				hoveredTooltipText = null;
				Invalidate();
			}
			return;
		}
		int num = 166;
		int num2 = 21;
		List<PlayerDamageEntry> damagers = enemyCombatSnapshot.Damagers;
		if (damagers == null || damagers.Count == 0)
		{
			if (hoveredTooltipText != null)
			{
				hoveredTooltipText = null;
				Invalidate();
			}
			return;
		}
		int num3 = base.Height - num - 12;
		int num4 = Math.Max(1, num3 / num2);
		for (int i = 0; i < num4; i++)
		{
			int num5 = i + dpsScrollBar.Offset;
			if (num5 >= damagers.Count)
			{
				break;
			}
			int num6 = num + i * num2;
			if (pt.Y >= num6 && pt.Y <= num6 + num2 && pt.X >= 10 && pt.X <= base.Width - 10)
			{
				PlayerDamageEntry playerDamageEntry = damagers[num5];
				string text;
				if (IsOryx3Combat(currentSnapshot, enemyCombatSnapshot) && playerDamageEntry.GuardHits > 0)
				{
					double num7 = ((playerDamageEntry.Damage > 0) ? ((double)playerDamageEntry.GuardDamage / (double)playerDamageEntry.Damage * 100.0) : 0.0);
					text = $"#{playerDamageEntry.Rank} {playerDamageEntry.Name} ({playerDamageEntry.ClassName})\n" + $"\ud83d\udca5 Total Dmg: {playerDamageEntry.Damage:N0} ({playerDamageEntry.Percentage:F1}%) | ⚡ DPS: {playerDamageEntry.Dps:N0}/s\n" + $"\ud83d\udee1\ufe0f Guard Hits: {playerDamageEntry.GuardHits:N0} | \ud83d\udca2 Guard Dmg: {playerDamageEntry.GuardDamage:N0} ({num7:F1}%)";
				}
				else
				{
					text = $"#{playerDamageEntry.Rank} {playerDamageEntry.Name} ({playerDamageEntry.ClassName})\n" + $"\ud83d\udca5 Total Dmg: {playerDamageEntry.Damage:N0} ({playerDamageEntry.Percentage:F1}%) | ⚡ DPS: {playerDamageEntry.Dps:N0}/s";
				}
				if (hoveredTooltipText != text)
				{
					hoveredTooltipText = text;
					hoveredTooltipPos = new Point(Math.Min(base.Width - 250, Math.Max(10, pt.X - 50)), Math.Max(5, pt.Y - 52));
					Invalidate();
				}
				return;
			}
		}
		if (hoveredTooltipText != null)
		{
			hoveredTooltipText = null;
			Invalidate();
		}
	}

	private void CheckEquipmentTooltip(Point pt)
	{
		if (activeTab == 0 && currentSnapshot != null)
		{
			EnemyCombatSnapshot currentDisplayEnemy = currentSnapshot.GetCurrentDisplayEnemy();
			if (currentDisplayEnemy != null && currentDisplayEnemy.Damagers != null && currentDisplayEnemy.Damagers.Count > 0)
			{
				int num = 14;
				int num2 = 55 + num + 4 + 18 + 20;
				int num3 = 21;
				List<PlayerDamageEntry> damagers = currentDisplayEnemy.Damagers;
				int num4 = base.Height - num2 - 12;
				int num5 = Math.Max(1, num4 / num3);
				if (pt.Y >= num2 && pt.Y <= num2 + num5 * num3 && pt.X >= 10 && pt.X <= base.Width - 10)
				{
					int num6 = (pt.Y - num2) / num3 + dpsScrollBar.Offset;
					if (num6 >= 0 && num6 < damagers.Count)
					{
						PlayerDamageEntry playerDamageEntry = damagers[num6];
						string text = ((playerDamageEntry.Status == PlayerCombatStatus.Dead) ? "\ud83d\udc80 DEAD" : ((playerDamageEntry.Status != PlayerCombatStatus.Nexused) ? "Alive" : ((playerDamageEntry.LastHpPercent > 0 && playerDamageEntry.LastHpPercent < 100) ? $"\ud83c\udfc3 NEXUSED (@ {playerDamageEntry.LastHpPercent}% HP)" : "\ud83c\udfc3 NEXUSED")));
						string text2 = $"{playerDamageEntry.Name} ({playerDamageEntry.ClassName})\nRank #{playerDamageEntry.Rank} | Dmg: {playerDamageEntry.Damage:N0} ({playerDamageEntry.Percentage:F1}%)\nDPS: {playerDamageEntry.Dps:N0}/s | Status: {text}";
						if (IsOryx3Combat(currentSnapshot, currentDisplayEnemy) && playerDamageEntry.GuardHits > 0)
						{
							double guardDamagePercent = playerDamageEntry.GuardDamagePercent;
							text2 += $"\n\ud83d\udee1\ufe0f Guard: {playerDamageEntry.GuardHits} hits ({playerDamageEntry.GuardDamage:N0} dmg = {guardDamagePercent:F1}%)";
							if (playerDamageEntry.IsHeavyGuardShooter)
							{
								text2 += " ⚠\ufe0f [>= 20% GUARD GRIEF]";
							}
						}
						if (hoveredTooltipText != text2)
						{
							hoveredTooltipText = text2;
							hoveredTooltipPos = new Point(Math.Min(base.Width - 240, Math.Max(10, pt.X - 30)), Math.Max(5, pt.Y - 35));
							Invalidate();
						}
						return;
					}
				}
			}
		}
		else if (activeTab == 1)
		{
			if (parseSubTab == 0)
			{
				int num7 = 116;
				int num8 = 28;
				int num9 = 28;
				int num10 = 224;
				List<PlayerParseEntry> players = currentSnapshot.Players;
				int num11 = base.Height - num7 - 14;
				int num12 = Math.Max(1, num11 / num8);
				for (int i = 0; i < num12; i++)
				{
					int num13 = i + parseScrollBar.Offset;
					if (num13 >= players.Count)
					{
						break;
					}
					int num14 = num7 + i * num8;
					if (pt.Y < num14 || pt.Y > num14 + num8)
					{
						continue;
					}
					PlayerParseEntry playerParseEntry = players[num13];
					for (int j = 0; j < 4; j++)
					{
						int num17 = num10 + j * (num9 + 4);
						if (pt.X >= num17 && pt.X <= num17 + num9)
						{
							int num18 = playerParseEntry.EquipmentIds[j];
							string text4 = playerParseEntry.EquipmentNames[j];
							string text5 = ((playerParseEntry.EquipmentRarities != null && playerParseEntry.EquipmentRarities.Length > j) ? playerParseEntry.EquipmentRarities[j] : "Common");
							List<string> list = ((playerParseEntry.EquipmentEnchants != null && playerParseEntry.EquipmentEnchants.Length > j) ? playerParseEntry.EquipmentEnchants[j] : null);
							string text7;
							if (num18 > 0)
							{
								string text6 = ((text5 != "Common" && !string.IsNullOrEmpty(text5)) ? (text4 + " [" + text5 + "]") : text4);
								text7 = ((list == null || list.Count <= 0) ? text6 : (text6 + "\n(" + string.Join(", ", list) + ")"));
							}
							else
							{
								text7 = "Empty Slot";
							}
							if (hoveredTooltipText != text7)
							{
								hoveredTooltipText = text7;
								int num19 = ((num14 < 120) ? (num14 + num8 + 2) : (num14 - 38));
								hoveredTooltipPos = new Point(Math.Max(10, pt.X - 20), num19);
								Invalidate();
							}
							return;
						}
					}
				}
			}
			else
			{
				int abRowStartY = 138;
				int abRowH = 22;
				List<AbilityLogEntry> filtered = GetFilteredAbilityList();
				List<AbilityLogEntry> displayList = filtered.AsEnumerable().Reverse().ToList();
				int availH = base.Height - abRowStartY - 14;
				int visCount = Math.Max(1, availH / abRowH);
				for (int k = 0; k < visCount; k++)
				{
					int entryIdx = k + abilityScrollBar.Offset;
					if (entryIdx >= displayList.Count)
					{
						break;
					}
					int rowY = abRowStartY + k * abRowH;
					if (pt.Y >= rowY && pt.Y <= rowY + abRowH)
					{
						AbilityLogEntry entry = displayList[entryIdx];
						string itemOrDet = !string.IsNullOrEmpty(entry.AbilityItemName) ? entry.AbilityItemName : entry.Details;
						string tip = $"⚡ {entry.AbilityType}: {entry.PlayerName} ({entry.ClassName})\nItem: {itemOrDet}\nDetails: {entry.Details}\nTime: {entry.TimeString}";
						if (hoveredTooltipText != tip)
						{
							hoveredTooltipText = tip;
							int tipY = ((rowY < 150) ? (rowY + abRowH + 2) : (rowY - 48));
							hoveredTooltipPos = new Point(Math.Min(base.Width - 220, Math.Max(10, pt.X - 20)), tipY);
							Invalidate();
						}
						return;
					}
				}
			}
		}
		else if (activeTab == 2)
		{
			LocalPlayerInfo localPlayer = currentSnapshot.LocalPlayer;
			int num20 = 36;
			int num21 = 8;
			int num22 = 185;
			if (pt.Y >= num22 && pt.Y <= num22 + num20)
			{
				for (int k = 0; k < 4; k++)
				{
					int num23 = 14 + k * (num20 + num21);
					if (pt.X >= num23 && pt.X <= num23 + num20)
					{
						int num24 = localPlayer.EquipmentIds[k];
						string text8 = localPlayer.EquipmentNames[k];
						string text9 = ((localPlayer.EquipmentRarities != null && localPlayer.EquipmentRarities.Length > k) ? localPlayer.EquipmentRarities[k] : "Common");
						List<string> list2 = ((localPlayer.EquipmentEnchants != null && localPlayer.EquipmentEnchants.Length > k) ? localPlayer.EquipmentEnchants[k] : null);
						string text11;
						if (num24 > 0)
						{
							string text10 = ((text9 != "Common" && !string.IsNullOrEmpty(text9)) ? (text8 + " [" + text9 + "]") : text8);
							text11 = ((list2 == null || list2.Count <= 0) ? text10 : (text10 + "\n(" + string.Join(", ", list2) + ")"));
							if (k == 0)
							{
								ItemSpriteManager.ItemMeta meta = ItemSpriteManager.GetItemMetadata(num24);
								if (meta.Projectiles != null && meta.Projectiles.Count > 1)
								{
									StringBuilder sb = new StringBuilder(text11);
									sb.Append("\nBullets: ");
									for (int bIdx = 0; bIdx < meta.Projectiles.Count; bIdx++)
									{
										var p = meta.Projectiles[bIdx];
										if (bIdx > 0) sb.Append(", ");
										sb.Append($"{p.NumProjectiles}x {p.MinDamage}-{p.MaxDamage}");
									}
									text11 = sb.ToString();
								}
							}
						}
						else
						{
							text11 = "Empty Slot";
						}
						if (hoveredTooltipText != text11)
						{
							hoveredTooltipText = text11;
							hoveredTooltipPos = new Point(Math.Max(10, pt.X - 20), Math.Max(5, num22 - 36));
							Invalidate();
						}
						return;
					}
				}
			}
			int num25 = num22 + num20 + 6 + 15;
			int num26 = 32;
			int num27 = base.Height - num25 - 14;
			int num28 = Math.Max(1, num27 / num26);
			List<LootBagDrop> lootBags = currentSnapshot.LootBags;
			for (int l = 0; l < num28; l++)
			{
				int num29 = l + lootScrollBar.Offset;
				if (num29 >= lootBags.Count)
				{
					break;
				}
				LootBagDrop lootBagDrop = lootBags[num29];
				int num30 = num25 + 2 + l * num26;
				if (pt.Y < num30 || pt.Y > num30 + num26)
				{
					continue;
				}
				int num31 = 145;
				int num32 = 26;
				for (int m = 0; m < lootBagDrop.ItemIds.Count; m++)
				{
					int num33 = num31 + m * (num32 + 4);
					if (pt.X >= num33 && pt.X <= num33 + num32)
					{
						int itemId = lootBagDrop.ItemIds[m];
						ItemSpriteManager.ItemMeta meta = ItemSpriteManager.GetItemMetadata(itemId);
						string rText = (lootBagDrop.ItemRarities != null && lootBagDrop.ItemRarities.Count > m) ? lootBagDrop.ItemRarities[m] : meta.Rarity;
						bool isShiny = ((lootBagDrop.ItemIsShiny != null && lootBagDrop.ItemIsShiny.Count > m) && lootBagDrop.ItemIsShiny[m]) || meta.IsShiny;
						List<string> enchants = (lootBagDrop.ItemEnchants != null && lootBagDrop.ItemEnchants.Count > m) ? lootBagDrop.ItemEnchants[m] : null;

						string tip = meta.Name;
						if (isShiny)
						{
							tip = "✨ " + tip + " [SHINY]";
						}
						else if (!string.IsNullOrEmpty(rText) && rText != "Common")
						{
							tip += $" [{rText}]";
						}
						if (enchants != null && enchants.Count > 0)
						{
							tip += "\n(" + string.Join(", ", enchants) + ")";
						}
						if (hoveredTooltipText != tip)
						{
							hoveredTooltipText = tip;
							hoveredTooltipPos = new Point(Math.Min(base.Width - 180, Math.Max(10, pt.X - 30)), Math.Max(5, pt.Y - 26));
							Invalidate();
						}
						return;
					}
				}
			}
		}
		if (hoveredTooltipText != null)
		{
			hoveredTooltipText = null;
			Invalidate();
		}
	}

	private OverlayScrollBar GetActiveScrollBar()
	{
		if (isCompact) return null;
		if (activeTab == 0) return isMonsterDropdownOpen ? dropdownScrollBar : dpsScrollBar;
		if (activeTab == 1)
		{
			if (parseSubTab == 0) return parseScrollBar;
			return parseSubTab == 1 ? abilityScrollBar : partyScrollBar;
		}
		return activeTab == 2 ? lootScrollBar : null;
	}

	private void DrawScrollBar(Graphics graphics, OverlayScrollBar scrollBar)
	{
		if (!scrollBar.Visible) return;
		using SolidBrush trackBrush = new SolidBrush(Color.FromArgb(25, 28, 38));
		using SolidBrush thumbBrush = new SolidBrush(draggingScrollBar == scrollBar ? AccentCyan : Color.FromArgb(90, 110, 140));
		graphics.FillRectangle(trackBrush, scrollBar.Track);
		graphics.FillRectangle(thumbBrush, scrollBar.Thumb);
	}

	private void EndScrollDrag()
	{
		if (draggingScrollBar == null) return;
		draggingScrollBar = null;
		Capture = false;
		Invalidate();
	}

	protected override void OnMouseCaptureChanged(EventArgs e)
	{
		base.OnMouseCaptureChanged(e);
		if (!Capture) EndScrollDrag();
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if (isClickThrough || isCompact || draggingScrollBar != null || e.Delta == 0) return;
		Update();
		OverlayScrollBar scrollBar = GetActiveScrollBar();
		if (scrollBar == null || !scrollBar.Visible) return;
		if (wheelScrollBar != scrollBar)
		{
			wheelScrollBar = scrollBar;
			wheelDeltaRemainder = 0;
		}
		wheelDeltaRemainder += e.Delta;
		int notches = wheelDeltaRemainder / SystemInformation.MouseWheelScrollDelta;
		wheelDeltaRemainder %= SystemInformation.MouseWheelScrollDelta;
		if (notches == 0) return;
		int rows = SystemInformation.MouseWheelScrollLines;
		if (rows < 0) rows = scrollBar.VisibleRows;
		scrollBar.Scroll(-notches * rows);
		hoveredTooltipText = null;
		Invalidate();
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		isDragging = false;
		EndScrollDrag();
		isResizingBottom = false;
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (isLeftDragHandleHovered || isRightDragHandleHovered || hoveredTooltipText == "Drag to move HUD")
		{
			isLeftDragHandleHovered = false;
			isRightDragHandleHovered = false;
			if (hoveredTooltipText == "Drag to move HUD")
			{
				hoveredTooltipText = null;
			}
			Invalidate();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		dpsScrollBar.ResetLayout();
		parseScrollBar.ResetLayout();
		abilityScrollBar.ResetLayout();
		partyScrollBar.ResetLayout();
		lootScrollBar.ResetLayout();
		dropdownScrollBar.ResetLayout();
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.FillRectangle(brushBg, 0, 0, base.Width, base.Height);
		graphics.DrawRectangle(penBorder, 1, 1, base.Width - 2, base.Height - 2);
		DrawHeader(graphics);
		if (isCompact)
		{
			DrawCompactBossBar(graphics, 10, 32, base.Width - 20, 30);
			return;
		}
		DrawStatsBar(graphics, 1, 26, base.Width - 2, 24);
		DrawTabs(graphics, 10, 52);
		if (activeTab == 0)
		{
			DrawDpsTab(graphics, 10, 78);
		}
		else if (activeTab == 1)
		{
			DrawParseTab(graphics, 10, 78);
		}
		else if (activeTab == 2)
		{
			DrawMyInfoTab(graphics, 10, 78);
		}
		else if (activeTab == 3)
		{
			DrawMoonlightVillageTab(graphics, 10, 78);
		}
		if (isMonsterDropdownOpen && activeTab == 0)
		{
			DrawMonsterDropdownPopup(graphics);
		}
		if (hoveredTooltipText != null)
		{
			DrawTooltip(graphics);
		}
		if (isCompact)
		{
			return;
		}
		using Pen pen = new Pen(Color.FromArgb(80, 95, 125), 2f);
		int num = base.Width / 2;
		graphics.DrawLine(pen, num - 18, base.Height - 3, num + 18, base.Height - 3);
	}

	private void DrawDragGrip(Graphics g, int startX, int startY, bool isHovered)
	{
		Color color = (isHovered ? AccentCyan : Color.FromArgb(145, 165, 190));
		if (isHovered)
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(50, 0, 200, 255)))
			{
				g.FillRectangle(brush, startX - 2, startY - 2, 18, 18);
			}
			using Pen pen = new Pen(Color.FromArgb(120, 0, 240, 255), 1f);
			g.DrawRectangle(pen, startX - 2, startY - 2, 18, 18);
		}
		using SolidBrush brush2 = new SolidBrush(color);
		for (int i = 0; i < 2; i++)
		{
			int num = startX + 3 + i * 5;
			for (int j = 0; j < 3; j++)
			{
				int num2 = startY + 2 + j * 4;
				g.FillEllipse(brush2, num, num2, 2.5f, 2.5f);
			}
		}
	}

	private void DrawHeader(Graphics g)
	{
		g.FillRectangle(brushHeaderBg, 1, 1, base.Width - 2, 25);
		DrawDragGrip(g, 5, 5, isLeftDragHandleHovered);
		bool flag = currentSnapshot.HistoryTotalCount > 0;
		Brush brush = (flag ? brushAccentCyan : brushTextMuted);
		g.DrawString("◀", fontHeader, brush, 28f, 4f);
		string s = TruncateString(currentSnapshot.DungeonName, 13);
		bool flag2 = currentSnapshot.HistoryIndex >= 0;
		using (SolidBrush brush2 = new SolidBrush(flag2 ? GoldColor : TextWhite))
		{
			g.DrawString(s, fontHeader, brush2, 46f, 4f);
		}
		int num = (int)(46f + g.MeasureString(s, fontHeader).Width);
		dungeonDropdownHitRight = num + 16;
		if (num < 165)
		{
			g.DrawString("▾", fontBadge, flag2 ? brushGold : brushAccentCyan, num, 6f);
		}
		Brush brush3 = ((flag2 | flag) ? brushAccentCyan : brushTextMuted);
		g.DrawString("▶", fontHeader, brush3, 174f, 4f);
		Color color = (flag2 ? GoldColor : HpGreen);
		string s2 = (flag2 ? $"[{currentSnapshot.HistoryIndex + 1}/{currentSnapshot.HistoryTotalCount}]" : "[LIVE]");
		g.DrawString(s2, fontBadge, new SolidBrush(color), 196f, 6f);
		string s3 = (isClickThrough ? "[\ud83d\udd12 Locked]" : "[\ud83d\udd13 Lock]");
		SolidBrush brush4 = (isClickThrough ? brushGold : brushTextMuted);
		g.DrawString(s3, fontBadge, brush4, 250f, 6f);
		DrawDragGrip(g, 306, 5, isRightDragHandleHovered);
		g.DrawString(isCompact ? "▾" : "▴", fontTitle, brushTextWhite, 332f, 3f);
		g.DrawString("✕", fontTitle, brushTextWhite, 358f, 3f);
	}

	private void DrawStatsBar(Graphics g, int x, int y, int width, int height)
	{
		g.FillRectangle(brushStatsBarBg, x, y, width, height);
		g.DrawLine(penBorderThin, x, y + height, x + width, y + height);
		int num = (int)(currentSnapshot.ElapsedDungeonSeconds / 60.0);
		int num2 = (int)(currentSnapshot.ElapsedDungeonSeconds % 60.0);
		string s = $"⏱ {num:D2}:{num2:D2}";
		string s2 = $"⚔ {currentSnapshot.KillsCount} Kills";
		string s3 = "\ud83d\udca5 " + FormatNumber(currentSnapshot.TotalDungeonDamage);
		string s4 = "⚡ " + FormatNumber(currentSnapshot.TotalGroupDps) + "/s";
		string s5 = $"\ud83d\udc65 {currentSnapshot.Players.Count}";
		g.DrawString(s, fontSmall, brushTextMuted, x + 8, y + 4);
		g.DrawString(s2, fontSmall, brushGold, x + 72, y + 4);
		g.DrawString(s3, fontSmall, brushAccentCyan, x + 155, y + 4);
		g.DrawString(s4, fontSmall, brushTextWhite, x + 235, y + 4);
		g.DrawString(s5, fontSmall, brushGold, x + 315, y + 4);
	}

	private void DrawTabs(Graphics g, int x, int y)
	{
		int tabH = 22;

		// Tab 0: DPS METER
		bool isTab0 = activeTab == 0;
		SolidBrush b0 = isTab0 ? brushTabActive : brushTabInactive;
		SolidBrush t0 = isTab0 ? brushTextWhite : brushTextMuted;
		g.FillRectangle(b0, x, y, 82, tabH);
		if (isTab0)
		{
			g.DrawLine(penAccentTab, x, y + 21, x + 82, y + 21);
		}
		g.DrawString("⚔ DPS", fontTab, t0, x + 16, y + 3);

		// Tab 1: PARSE
		bool isTab1 = activeTab == 1;
		SolidBrush b1 = isTab1 ? brushTabActive : brushTabInactive;
		SolidBrush t1 = isTab1 ? brushTextWhite : brushTextMuted;
		int x1 = x + 86;
		g.FillRectangle(b1, x1, y, 86, tabH);
		if (isTab1)
		{
			g.DrawLine(penAccentTab, x1, y + 21, x1 + 86, y + 21);
		}
		string parseLabel = $"\ud83d\udc65 PARSE ({currentSnapshot.Players.Count})";
		g.DrawString(parseLabel, fontTab, t1, x1 + 6, y + 3);

		// Tab 2: MY INFO
		bool isTab2 = activeTab == 2;
		SolidBrush b2 = isTab2 ? brushTabActive : brushTabInactive;
		SolidBrush t2 = isTab2 ? brushTextWhite : brushTextMuted;
		int x2 = x1 + 90;
		g.FillRectangle(b2, x2, y, 82, tabH);
		if (isTab2)
		{
			g.DrawLine(penAccentTab, x2, y + 21, x2 + 82, y + 21);
		}
		g.DrawString("\ud83d\udc64 MY INFO", fontTab, t2, x2 + 10, y + 3);

		// Tab 3: VILLAGE / UMI
		bool isTab3 = activeTab == 3;
		SolidBrush b3 = isTab3 ? brushTabActive : brushTabInactive;
		bool hasFlames = currentMvSnapshot != null && (currentMvSnapshot.IsInDungeon || currentMvSnapshot.TotalFlames > 0);
		SolidBrush t3 = isTab3 ? brushTextWhite : (hasFlames ? brushGold : brushTextMuted);
		int x3 = x2 + 86;
		int w3 = (base.Width - 10) - x3;
		g.FillRectangle(b3, x3, y, w3, tabH);

		bool isUmi = currentMvSnapshot != null && currentMvSnapshot.IsUmiMode;
		if (isTab3)
		{
			Color lineCol;
			if (isUmi)
			{
				lineCol = Color.FromArgb(232, 121, 249); // Fox Spirit Violet
			}
			else
			{
				lineCol = (currentMvSnapshot != null && currentMvSnapshot.CurrentTier > 0)
					? GetMvTierColor(currentMvSnapshot.CurrentTier, false)
					: AccentCyan;
			}
			using Pen tabPen = new Pen(lineCol, 2f);
			g.DrawLine(tabPen, x3, y + 21, x3 + w3, y + 21);
		}

		string mvLabel;
		if (isUmi)
		{
			mvLabel = (currentMvSnapshot != null && currentMvSnapshot.TotalFlames > 0)
				? $"🦊 UMI ({currentMvSnapshot.TotalFlames}/56)"
				: "🦊 UMI";
		}
		else
		{
			mvLabel = (currentMvSnapshot != null && currentMvSnapshot.TotalFlames > 0)
				? $"🌙 MV ({currentMvSnapshot.TotalFlames}/88)"
				: "🌙 VILLAGE";
		}
		g.DrawString(mvLabel, fontTab, t3, x3 + 8, y + 3);
	}

	private void DrawDpsTab(Graphics g, int x, int y)
	{
		int num = base.Width - 20;
		monsterDropdownBarRect = new Rectangle(x, y, num, 24);
		g.FillRectangle(brushTableHeader, monsterDropdownBarRect);
		g.DrawRectangle(penBorderThin, monsterDropdownBarRect);
		EnemyCombatSnapshot currentDisplayEnemy = currentSnapshot.GetCurrentDisplayEnemy();
		bool num2 = currentSnapshot.HistoryIndex >= 0;
		string s = ((!num2) ? ((currentSnapshot.SelectedMonsterMode == -1) ? "\ud83d\udd34 [LIVE] " : ((currentSnapshot.SelectedMonsterMode == -2) ? "\ud83c\udf1f [TOTAL] " : "\ud83d\udc80 [DEAD] ")) : ((currentSnapshot.SelectedMonsterMode == -2) ? "\ud83c\udf1f [TOTAL] " : "\ud83d\udcdc [BOSS] "));
		using (SolidBrush brush = new SolidBrush((!num2) ? ((currentSnapshot.SelectedMonsterMode == -1) ? HpGreen : ((currentSnapshot.SelectedMonsterMode == -2) ? AccentCyan : GoldColor)) : ((currentSnapshot.SelectedMonsterMode == -2) ? AccentCyan : GoldColor)))
		{
			g.DrawString(s, fontBadge, brush, x + 4, y + 5);
		}
		bool flag = IsOryx3Combat(currentSnapshot, currentDisplayEnemy);
		bool flag2 = flag && currentDisplayEnemy.IsGuarding;
		string s2 = TruncateString(currentDisplayEnemy.Name, flag2 ? 18 : 28);
		g.DrawString(s2, fontHeader, brushTextWhite, x + 62, y + 4);
		int num3 = x + num - 22;
		if (flag2)
		{
			int num4 = 90;
			int num5 = num3 - num4;
			num3 -= num4 + 4;
			Rectangle rect = new Rectangle(num5, y + 3, num4, 18);
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(220, 220, 38, 38)))
			{
				g.FillRectangle(brush2, rect);
			}
			using (Pen pen = new Pen(Color.FromArgb(255, 255, 215, 0), 1.2f))
			{
				g.DrawRectangle(pen, rect);
			}
			g.DrawString("\ud83d\udee1\ufe0f GUARDING", fontBadge, brushTextWhite, num5 + 4, y + 5);
		}
		if (!currentDisplayEnemy.IsDead)
		{
			List<(string, Color, Color)> list = new List<(string, Color, Color)>();
			if (currentDisplayEnemy.IsCursed)
			{
				list.Add(("CURSE", Color.FromArgb(210, 147, 51, 234), Color.FromArgb(216, 180, 254)));
			}
			if (currentDisplayEnemy.IsArmorBroken)
			{
				list.Add(("BREAK", Color.FromArgb(210, 220, 38, 38), Color.FromArgb(252, 165, 165)));
			}
			if (currentDisplayEnemy.IsStunned)
			{
				list.Add(("STUN", Color.FromArgb(210, 234, 179, 8), Color.FromArgb(254, 240, 138)));
			}
			if (currentDisplayEnemy.IsDazed)
			{
				list.Add(("DAZE", Color.FromArgb(210, 202, 138, 4), Color.FromArgb(254, 240, 138)));
			}
			if (currentDisplayEnemy.IsParalyzed)
			{
				list.Add(("PARA", Color.FromArgb(210, 13, 148, 136), Color.FromArgb(153, 246, 228)));
			}
			if (currentDisplayEnemy.IsExposed)
			{
				list.Add(("EXPOSED", Color.FromArgb(210, 219, 39, 119), Color.FromArgb(244, 114, 182)));
			}
			if (currentDisplayEnemy.IsInvulnerable)
			{
				list.Add(("INVULN", Color.FromArgb(210, 75, 85, 99), Color.FromArgb(209, 213, 219)));
			}
			foreach (var item in list)
			{
				int num6 = (int)g.MeasureString(item.Item1, fontBadge).Width + 8;
				int num7 = num3 - num6;
				if (num7 < x + 160)
				{
					break;
				}
				num3 -= num6 + 3;
				Rectangle rect2 = new Rectangle(num7, y + 3, num6, 18);
				using (SolidBrush brush3 = new SolidBrush(item.Item2))
				{
					g.FillRectangle(brush3, rect2);
				}
				using (Pen pen2 = new Pen(item.Item3, 1f))
				{
					g.DrawRectangle(pen2, rect2);
				}
				g.DrawString(item.Item1, fontBadge, brushTextWhite, num7 + 4, y + 5);
			}
		}
		string s3 = (isMonsterDropdownOpen ? "▴" : "▾");
		g.DrawString(s3, fontHeader, brushAccentCyan, x + num - 18, y + 3);
		int num8 = y + 28;
		int num9 = 18;
		int maxHp = currentDisplayEnemy.MaxHp;
		int currentHp = currentDisplayEnemy.CurrentHp;
		double num10 = ((maxHp > 0) ? Math.Max(0.0, Math.Min(100.0, (double)currentHp / (double)maxHp * 100.0)) : 0.0);
		g.FillRectangle(brushHpBg, x, num8, num, num9);
		if (!currentDisplayEnemy.IsDead && num10 > 0.0)
		{
			int num11 = (int)((double)num * (num10 / 100.0));
			if (num11 > 0)
			{
				using LinearGradientBrush brush4 = new LinearGradientBrush(new Rectangle(x, num8, num11, num9), HpRed, HpGreen, LinearGradientMode.Horizontal);
				g.FillRectangle(brush4, x, num8, num11, num9);
			}
		}
		if (flag2)
		{
			using Pen pen3 = new Pen(Color.FromArgb(255, 239, 68, 68), 2f);
			g.DrawRectangle(pen3, x, num8, num, num9);
		}
		else
		{
			g.DrawRectangle(penBorderThin, x, num8, num, num9);
		}
		string s4 = (currentDisplayEnemy.IsDead ? "SLAIN / DEFEATED" : ((maxHp <= 0) ? (flag2 ? "Target HP Unavailable  [\ud83d\udee1\ufe0f GUARD]" : "Target HP Unavailable") : (flag2 ? $"{currentHp:N0} / {maxHp:N0} ({num10:F1}%)  [\ud83d\udee1\ufe0f GUARD]" : $"{currentHp:N0} / {maxHp:N0} ({num10:F1}%)")));
		SizeF sizeF = g.MeasureString(s4, fontSmall);
		g.DrawString(s4, fontSmall, brushTextWhite, (float)x + ((float)num - sizeF.Width) / 2f, num8 + 2);
		int num12 = num8 + num9 + 4;
		int num13 = (int)(currentDisplayEnemy.FightDurationSeconds / 60.0);
		int num14 = (int)(currentDisplayEnemy.FightDurationSeconds % 60.0);
		string s5 = $"⏱ {num13:D2}:{num14:D2} | \ud83d\udca5 Dmg: {FormatNumber(currentDisplayEnemy.TotalDamage)} | ⚡ DPS: {FormatNumber(currentDisplayEnemy.Dps)}/s";
		g.DrawString(s5, fontSmall, brushTextMuted, x, num12);
		if (flag && currentDisplayEnemy.TotalGuardHits > 0)
		{
			string s6 = $"\ud83d\udee1\ufe0f Guards: {currentDisplayEnemy.TotalGuardHits} ({FormatNumber(currentDisplayEnemy.TotalGuardDamage)})";
			SizeF sizeF2 = g.MeasureString(s6, fontSmall);
			using SolidBrush brush5 = new SolidBrush(Color.FromArgb(255, 248, 113, 113));
			g.DrawString(s6, fontSmall, brush5, (float)(x + num) - sizeF2.Width, num12);
		}
		int num15 = num12 + 18;
		if (!string.IsNullOrEmpty(currentSnapshot.DamageEstimateNote))
		{
			g.DrawString(currentSnapshot.DamageEstimateNote, fontSmall, brushTextMuted, x, num15);
			num15 += 18;
		}
		DrawDpsTable(g, x, num15, num, currentDisplayEnemy);
	}

	private void DrawDpsTable(Graphics g, int x, int y, int width, EnemyCombatSnapshot enemy)
	{
		g.FillRectangle(brushTableHeader, x, y, width, 18);
		g.DrawString("#", fontSmall, brushTextMuted, x + 4, y + 2);
		g.DrawString("PLAYER", fontSmall, brushTextMuted, x + 24, y + 2);
		g.DrawString("CLASS", fontSmall, brushTextMuted, x + 136, y + 2);
		g.DrawString("DMG", fontSmall, brushTextMuted, x + 195, y + 2);
		g.DrawString("DPS", fontSmall, brushTextMuted, x + 252, y + 2);
		g.DrawString("%", fontSmall, brushTextMuted, x + 308, y + 2);
		int num = y + 20;
		int num2 = 21;
		List<PlayerDamageEntry> list = enemy.Damagers.ToList();
		if (list.Count == 0)
		{
			g.DrawString("No damage records for this target.", fontSmall, brushTextMuted, x + 10, num + 15);
			return;
		}
		long num3 = ((list.Count > 0) ? list[0].Damage : 1);
		if (num3 <= 0)
		{
			num3 = 1L;
		}
		int num4 = base.Height - num - 12;
		int num5 = Math.Max(1, num4 / num2);
		dpsScrollBar.Configure(new Rectangle(x + width - 8, num, 8, num5 * num2), list.Count, num5);

		bool flag = list.Count > num5;
		int num7 = (flag ? 8 : 0);
		int num8 = width - num7;
		for (int i = 0; i < num5; i++)
		{
			int num9 = i + dpsScrollBar.Offset;
			if (num9 >= list.Count)
			{
				break;
			}
			PlayerDamageEntry playerDamageEntry = list[num9];
			int num10 = num + i * num2;
			double num11 = Math.Min(1.0, (double)playerDamageEntry.Damage / (double)num3);
			int num12 = (int)((double)num8 * num11);
			bool flag2 = IsOryx3Combat(currentSnapshot, enemy);
			bool flag3 = flag2 && playerDamageEntry.IsHeavyGuardShooter;
			if (flag3)
			{
				g.FillRectangle(brushGuardGriefRow, x, num10, num8, num2 - 2);
				if (num12 > 0)
				{
					using SolidBrush brush = new SolidBrush(Color.FromArgb(120, 239, 68, 68));
					g.FillRectangle(brush, x, num10, num12, num2 - 2);
				}
				g.DrawRectangle(penGuardGriefBorder, x, num10, num8, num2 - 2);
				if (playerDamageEntry.IsLocalPlayer)
				{
					using Pen pen = new Pen(AccentCyan, 1f);
					g.DrawRectangle(pen, x + 1, num10 + 1, num8 - 2, num2 - 4);
				}
			}
			else
			{
				if (num12 > 0)
				{
					using SolidBrush brush2 = new SolidBrush(playerDamageEntry.IsLocalPlayer ? Color.FromArgb(60, 0, 180, 220) : Color.FromArgb(35, 80, 100, 140));
					g.FillRectangle(brush2, x, num10, num12, num2 - 2);
				}
				if (playerDamageEntry.IsLocalPlayer)
				{
					g.DrawRectangle(penLocalHighlight, x, num10, num8, num2 - 2);
				}
			}
			SolidBrush brush3 = brushTextMuted;
			if (playerDamageEntry.Rank == 1)
			{
				brush3 = brushGold;
			}
			else if (playerDamageEntry.Rank == 2)
			{
				brush3 = brushSilver;
			}
			else if (playerDamageEntry.Rank == 3)
			{
				brush3 = brushBronze;
			}
			g.DrawString($"#{playerDamageEntry.Rank}", fontRow, brush3, x + 4, num10 + 2);
			string text = (playerDamageEntry.IsLocalPlayer ? (playerDamageEntry.Name + " *") : playerDamageEntry.Name);
			SolidBrush brush4 = (flag3 ? brushGuardGriefText : ((playerDamageEntry.Status == PlayerCombatStatus.Dead) ? brushDeadText : ((playerDamageEntry.Status == PlayerCombatStatus.Nexused) ? brushNexusedText : ((!playerDamageEntry.IsLocalPlayer) ? brushTextWhite : brushAccentCyan))));
			string text2 = null;
			Color color = Color.Empty;
			if (playerDamageEntry.Status == PlayerCombatStatus.Dead)
			{
				text2 = "\ud83d\udc80 DEAD";
				color = Color.FromArgb(220, 153, 27, 27);
			}
			else if (playerDamageEntry.Status == PlayerCombatStatus.Nexused)
			{
				text2 = "\ud83c\udfc3 NEXUS";
				color = Color.FromArgb(200, 180, 83, 9);
			}
			string text3 = null;
			Color color2 = Color.Empty;
			if (flag2)
			{
				if (flag3)
				{
					double guardDamagePercent = playerDamageEntry.GuardDamagePercent;
					text3 = $"\ud83d\udee1\ufe0f {guardDamagePercent:F0}%";
					color2 = Color.FromArgb(235, 220, 38, 38);
				}
				else if (playerDamageEntry.GuardHits > 0)
				{
					text3 = $"\ud83d\udee1\ufe0f{playerDamageEntry.GuardHits}";
					color2 = Color.FromArgb(180, 185, 28, 28);
				}
			}
			int num13 = x + 132;
			if (text2 != null)
			{
				int num14 = (int)g.MeasureString(text2, fontTiny).Width + 4;
				int num15 = num13 - num14;
				Rectangle rect = new Rectangle(num15, num10 + 3, num14, 13);
				using (SolidBrush brush5 = new SolidBrush(color))
				{
					g.FillRectangle(brush5, rect);
				}
				g.DrawString(text2, fontTiny, brushTextWhite, num15 + 2, num10 + 3);
				num13 = num15 - 3;
			}
			if (text3 != null)
			{
				int num16 = (int)g.MeasureString(text3, fontTiny).Width + 4;
				int num17 = num13 - num16;
				Rectangle rect2 = new Rectangle(num17, num10 + 3, num16, 13);
				using (SolidBrush brush6 = new SolidBrush(color2))
				{
					g.FillRectangle(brush6, rect2);
				}
				g.DrawString(text3, fontTiny, brushTextWhite, num17 + 2, num10 + 3);
				num13 = num17 - 3;
			}
			int num18 = Math.Max(20, num13 - (x + 24));
			string text4 = text;
			while (text4.Length > 2 && g.MeasureString(text4, fontRow).Width > (float)num18)
			{
				text4 = text4.Substring(0, text4.Length - 1);
			}
			g.DrawString(text4, fontRow, brush4, x + 24, num10 + 2);
			g.DrawString(TruncateString(playerDamageEntry.ClassName, 7), fontRow, brushTextMuted, x + 136, num10 + 2);
			SolidBrush brush7 = (flag3 ? brushGuardGriefText : ((playerDamageEntry.Status == PlayerCombatStatus.Dead) ? brushDeadText : ((playerDamageEntry.Status == PlayerCombatStatus.Nexused) ? brushNexusedText : brushTextWhite)));
			g.DrawString(FormatNumber(playerDamageEntry.Damage), fontRow, brush7, x + 195, num10 + 2);
			g.DrawString(FormatNumber(playerDamageEntry.Dps), fontRow, brush7, x + 252, num10 + 2);
			g.DrawString($"{playerDamageEntry.Percentage:F1}%", fontRow, flag3 ? brushGuardGriefText : brushGold, x + 308, num10 + 2);
		}
		DrawScrollBar(g, dpsScrollBar);
	}

	private List<AbilityLogEntry> GetFilteredAbilityList()
	{
		List<AbilityLogEntry> raw = currentSnapshot?.AbilityLog ?? new List<AbilityLogEntry>();
		if (parseAbilityFilter == 1)
		{
			return raw.Where(e => e.AbilityType != null && e.AbilityType.IndexOf("DECOY", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
		if (parseAbilityFilter == 2)
		{
			return raw.Where(e => e.AbilityType != null && e.AbilityType.IndexOf("STASIS", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
		if (parseAbilityFilter == 3)
		{
			return raw.Where(e => e.AbilityType != null && (
				e.AbilityType.IndexOf("STUN", StringComparison.OrdinalIgnoreCase) >= 0 ||
				e.AbilityType.IndexOf("ARMOR", StringComparison.OrdinalIgnoreCase) >= 0 ||
				e.AbilityType.IndexOf("PARALYZE", StringComparison.OrdinalIgnoreCase) >= 0 ||
				e.AbilityType.IndexOf("TRAP", StringComparison.OrdinalIgnoreCase) >= 0
			)).ToList();
		}
		return raw;
	}

	private void DrawFilterPill(Graphics g, Rectangle rect, string text, bool isSelected)
	{
		using (SolidBrush bg = new SolidBrush(isSelected ? Color.FromArgb(45, 58, 80) : Color.FromArgb(25, 28, 36)))
		{
			g.FillRectangle(bg, rect);
		}
		using (Pen p = new Pen(isSelected ? AccentCyan : Color.FromArgb(50, 55, 70), 1f))
		{
			g.DrawRectangle(p, rect);
		}
		using (SolidBrush tb = new SolidBrush(isSelected ? TextWhite : TextMuted))
		{
			SizeF sz = g.MeasureString(text, fontTiny);
			g.DrawString(text, fontTiny, tb, rect.X + (rect.Width - sz.Width) / 2f, rect.Y + (rect.Height - sz.Height) / 2f);
		}
	}

	private void DrawAbilityBadge(Graphics g, int bx, int by, int bw, int bh, string abType)
	{
		string upper = abType?.ToUpperInvariant() ?? "ABILITY";
		Color bgColor;
		Color borderColor;
		Color textColor;

		if (upper.Contains("DECOY"))
		{
			bgColor = Color.FromArgb(60, 245, 158, 11);
			borderColor = Color.FromArgb(180, 245, 158, 11);
			textColor = Color.FromArgb(255, 215, 0);
		}
		else if (upper.Contains("STASIS"))
		{
			bgColor = Color.FromArgb(60, 168, 85, 247);
			borderColor = Color.FromArgb(180, 168, 85, 247);
			textColor = Color.FromArgb(216, 180, 254);
		}
		else if (upper.Contains("STUN"))
		{
			bgColor = Color.FromArgb(70, 239, 68, 68);
			borderColor = Color.FromArgb(200, 239, 68, 68);
			textColor = Color.FromArgb(254, 162, 162);
		}
		else if (upper.Contains("ARMOR") || upper.Contains("OGMUR"))
		{
			bgColor = Color.FromArgb(70, 234, 88, 12);
			borderColor = Color.FromArgb(200, 234, 88, 12);
			textColor = Color.FromArgb(253, 186, 116);
		}
		else if (upper.Contains("PARALYZE"))
		{
			bgColor = Color.FromArgb(60, 234, 179, 8);
			borderColor = Color.FromArgb(180, 234, 179, 8);
			textColor = Color.FromArgb(253, 224, 71);
		}
		else
		{
			bgColor = Color.FromArgb(40, 100, 120, 150);
			borderColor = Color.FromArgb(120, 100, 120, 150);
			textColor = Color.FromArgb(200, 210, 225);
		}

		using (SolidBrush bb = new SolidBrush(bgColor))
		{
			g.FillRectangle(bb, bx, by, bw, bh);
		}
		using (Pen bp = new Pen(borderColor, 1f))
		{
			g.DrawRectangle(bp, bx, by, bw, bh);
		}
		using (SolidBrush bt = new SolidBrush(textColor))
		{
			string label = upper.Length > 8 ? upper.Substring(0, 8) : upper;
			SizeF sz = g.MeasureString(label, fontTiny);
			g.DrawString(label, fontTiny, bt, bx + (bw - sz.Width) / 2f, by + (bh - sz.Height) / 2f);
		}
	}

	private void DrawParseTab(Graphics g, int x, int y)
	{
		int num = base.Width - 20;
		List<PlayerParseEntry> players = currentSnapshot.Players;

		subTabPlayersRect = new Rectangle(x, y, 78, 18);
		int abilityTotalCount = currentSnapshot.AbilityLog?.Count ?? 0;
		string abilitySubTabLabel = abilityTotalCount > 0 ? $"⚡ ABILITIES ({abilityTotalCount})" : "⚡ ABILITIES";
		int abTabW = Math.Max(95, (int)g.MeasureString(abilitySubTabLabel, fontTiny).Width + 14);
		subTabAbilitiesRect = new Rectangle(x + 82, y, abTabW, 18);

		int partyTotal = currentSnapshot.PartyMembers?.Count ?? 0;
		int inDungeonCount = currentSnapshot.PartyMembers?.Count(p => p.IsInDungeon) ?? 0;
		int missingCount = partyTotal - inDungeonCount;
		string partySubTabLabel = partyTotal > 0 ? $"🚩 PARTY ({inDungeonCount}/{partyTotal})" : "🚩 PARTY";
		int partyTabW = Math.Max(78, (int)g.MeasureString(partySubTabLabel, fontTiny).Width + 14);
		subTabPartyRect = new Rectangle(subTabAbilitiesRect.Right + 4, y, partyTabW, 18);

		bool isSubPlayers = (parseSubTab == 0);
		using (SolidBrush bSubP = new SolidBrush(isSubPlayers ? Color.FromArgb(45, 52, 70) : Color.FromArgb(24, 26, 34)))
		{
			g.FillRectangle(bSubP, subTabPlayersRect);
		}
		if (isSubPlayers)
		{
			g.DrawLine(penAccentTab, subTabPlayersRect.Left, subTabPlayersRect.Bottom - 1, subTabPlayersRect.Right, subTabPlayersRect.Bottom - 1);
		}
		g.DrawString("👥 PLAYERS", fontTiny, isSubPlayers ? brushTextWhite : brushTextMuted, subTabPlayersRect.X + 8, subTabPlayersRect.Y + 3);

		bool isSubAbilities = (parseSubTab == 1);
		using (SolidBrush bSubA = new SolidBrush(isSubAbilities ? Color.FromArgb(45, 52, 70) : Color.FromArgb(24, 26, 34)))
		{
			g.FillRectangle(bSubA, subTabAbilitiesRect);
		}
		if (isSubAbilities)
		{
			g.DrawLine(penAccentTab, subTabAbilitiesRect.Left, subTabAbilitiesRect.Bottom - 1, subTabAbilitiesRect.Right, subTabAbilitiesRect.Bottom - 1);
		}
		using (SolidBrush tSubA = new SolidBrush(isSubAbilities ? TextWhite : (abilityTotalCount > 0 ? GoldColor : TextMuted)))
		{
			g.DrawString(abilitySubTabLabel, fontTiny, tSubA, subTabAbilitiesRect.X + 8, subTabAbilitiesRect.Y + 3);
		}

		bool isSubParty = (parseSubTab == 2);
		using (SolidBrush bSubParty = new SolidBrush(isSubParty ? Color.FromArgb(45, 52, 70) : Color.FromArgb(24, 26, 34)))
		{
			g.FillRectangle(bSubParty, subTabPartyRect);
		}
		if (isSubParty)
		{
			g.DrawLine(penAccentTab, subTabPartyRect.Left, subTabPartyRect.Bottom - 1, subTabPartyRect.Right, subTabPartyRect.Bottom - 1);
		}
		Color partyColor = isSubParty ? TextWhite : (missingCount > 0 ? Color.FromArgb(255, 120, 120) : (partyTotal > 0 ? HpGreen : TextMuted));
		using (SolidBrush tSubParty = new SolidBrush(partyColor))
		{
			g.DrawString(partySubTabLabel, fontTiny, tSubParty, subTabPartyRect.X + 8, subTabPartyRect.Y + 3);
		}

		if (parseSubTab == 0)
		{
			int count = players.Count;
			int num2 = players.Count((PlayerParseEntry p) => p.MaxedCount == 8);
			string statStr = $"👥 {count} | 🌟 8/8s: {num2}";
			SizeF statSz = g.MeasureString(statStr, fontSmall);
			g.DrawString(statStr, fontSmall, brushGold, (float)(x + num) - statSz.Width - 2f, y + 2);

			int num4 = y + 18;
			g.FillRectangle(brushTableHeader, x, num4, num, 18);
			g.DrawString("NAME", fontSmall, brushTextMuted, x + 4, num4 + 2);
			g.DrawString("CLASS", fontSmall, brushTextMuted, x + 104, num4 + 2);
			g.DrawString("MAX", fontSmall, brushTextMuted, x + 174, num4 + 2);
			g.DrawString("EQUIPMENT", fontSmall, brushTextMuted, x + 224, num4 + 2);

			int num5 = num4 + 20;
			int num6 = 28;
			if (players.Count == 0)
			{
				g.DrawString("Scanning for players nearby...", fontSmall, brushTextMuted, x + 10, num5 + 15);
				return;
			}
			int num7 = base.Height - num5 - 14;
			int num8 = Math.Max(1, num7 / num6);
			parseScrollBar.Configure(new Rectangle(x + num - 8, num5, 8, num8 * num6), players.Count, num8);

			bool flag = players.Count > num8;
			int num10 = (flag ? 8 : 0);
			int num11 = num - num10;
			for (int num12 = 0; num12 < num8; num12++)
			{
				int num13 = num12 + parseScrollBar.Offset;
				if (num13 >= players.Count)
				{
					break;
				}
				PlayerParseEntry playerParseEntry = players[num13];
				int num14 = num5 + num12 * num6;
				if (playerParseEntry.IsLocalPlayer)
				{
					g.FillRectangle(brushLocalPlayerRow, x, num14, num11, num6 - 2);
				}
				SolidBrush brush = (playerParseEntry.IsLocalPlayer ? brushAccentCyan : brushTextWhite);
				g.DrawString(TruncateString(playerParseEntry.Name, 13), fontRow, brush, x + 4, num14 + 5);
				g.DrawString(TruncateString(playerParseEntry.ClassName, 10), fontRow, brushTextMuted, x + 104, num14 + 5);
				SolidBrush solidBrush = ((playerParseEntry.MaxedCount == 8) ? brushGold : ((playerParseEntry.MaxedCount >= 6) ? new SolidBrush(HpGreen) : brushTextMuted));
				g.DrawString(playerParseEntry.MaxedStats, fontRow, solidBrush, x + 174, num14 + 5);
				if (playerParseEntry.MaxedCount >= 6 && playerParseEntry.MaxedCount < 8)
				{
					solidBrush.Dispose();
				}
				int num15 = x + 224;
				int num16 = 28;
				int num17 = 24;
				for (int num18 = 0; num18 < 4; num18++)
				{
					int num19 = num15 + num18 * (num16 + 4);
					int itemId = playerParseEntry.EquipmentIds[num18];
					string rarityOverride = ((playerParseEntry.EquipmentRarities != null && playerParseEntry.EquipmentRarities.Length > num18) ? playerParseEntry.EquipmentRarities[num18] : null);
					int enchantCount = ((playerParseEntry.EquipmentEnchantCounts != null && playerParseEntry.EquipmentEnchantCounts.Length > num18) ? playerParseEntry.EquipmentEnchantCounts[num18] : (-1));
					ItemSpriteManager.DrawEquipmentSlot(g, num19, num14 + 2, num16, num17, itemId, num18, rarityOverride, enchantCount);
				}
			}
			DrawScrollBar(g, parseScrollBar);
		}

		else if (parseSubTab == 1)
		{
			// ABILITIES SUB-TAB
			abilityClearBtnRect = new Rectangle(x + num - 54, y, 54, 18);
			using (SolidBrush bClear = new SolidBrush(Color.FromArgb(50, 24, 28)))
			{
				g.FillRectangle(bClear, abilityClearBtnRect);
			}
			using (Pen pClear = new Pen(Color.FromArgb(150, 60, 65), 1f))
			{
				g.DrawRectangle(pClear, abilityClearBtnRect);
			}
			g.DrawString("🗑 CLEAR", fontTiny, brushTextWhite, abilityClearBtnRect.X + 5, abilityClearBtnRect.Y + 3);

			int filterY = y + 20;
			filterAllRect = new Rectangle(x, filterY, 40, 17);
			filterDecoyRect = new Rectangle(x + 44, filterY, 62, 17);
			filterStasisRect = new Rectangle(x + 110, filterY, 62, 17);
			filterStunRect = new Rectangle(x + 176, filterY, 56, 17);

			DrawFilterPill(g, filterAllRect, "ALL", parseAbilityFilter == 0);
			DrawFilterPill(g, filterDecoyRect, "🎭 DECOY", parseAbilityFilter == 1);
			DrawFilterPill(g, filterStasisRect, "🔮 STASIS", parseAbilityFilter == 2);
			DrawFilterPill(g, filterStunRect, "🛡️ STUN", parseAbilityFilter == 3);

			List<AbilityLogEntry> filteredList = GetFilteredAbilityList();
			List<AbilityLogEntry> displayList = filteredList.AsEnumerable().Reverse().ToList();

			string countInfo = $"{displayList.Count} logged";
			SizeF countSz = g.MeasureString(countInfo, fontTiny);
			g.DrawString(countInfo, fontTiny, brushTextMuted, (float)(x + num) - countSz.Width - 2f, filterY + 3);

			int headerY = y + 40;
			g.FillRectangle(brushTableHeader, x, headerY, num, 18);
			g.DrawString("TIME", fontSmall, brushTextMuted, x + 4, headerY + 2);
			g.DrawString("PLAYER", fontSmall, brushTextMuted, x + 50, headerY + 2);
			g.DrawString("CLASS", fontSmall, brushTextMuted, x + 138, headerY + 2);
			g.DrawString("ABILITY", fontSmall, brushTextMuted, x + 196, headerY + 2);
			g.DrawString("ITEM / DETAILS", fontSmall, brushTextMuted, x + 262, headerY + 2);

			int rowStartY = headerY + 20;
			int rowH = 22;

			if (displayList.Count == 0)
			{
				g.DrawString("No ability uses detected yet.", fontSmall, brushTextMuted, x + 10, rowStartY + 15);
				return;
			}

			int availH = base.Height - rowStartY - 14;
			int visibleCount = Math.Max(1, availH / rowH);
			abilityScrollBar.Configure(new Rectangle(x + num - 8, rowStartY, 8, visibleCount * rowH), displayList.Count, visibleCount);

			bool isScrollable = displayList.Count > visibleCount;
			int scrollBarW = isScrollable ? 8 : 0;
			int rowW = num - scrollBarW;

			for (int i = 0; i < visibleCount; i++)
			{
				int entryIdx = i + abilityScrollBar.Offset;
				if (entryIdx >= displayList.Count)
				{
					break;
				}

				AbilityLogEntry entry = displayList[entryIdx];
				int rowY = rowStartY + i * rowH;

				if (entry.IsLocalPlayer)
				{
					g.FillRectangle(brushLocalPlayerRow, x, rowY, rowW, rowH - 2);
				}
				else if (i % 2 == 1)
				{
					using SolidBrush altBrush = new SolidBrush(Color.FromArgb(18, 20, 27));
					g.FillRectangle(altBrush, x, rowY, rowW, rowH - 2);
				}

				// Time
				g.DrawString(entry.TimeString, fontTiny, brushTextMuted, x + 4, rowY + 4);

				// Player Name
				SolidBrush pBrush = entry.IsLocalPlayer ? brushAccentCyan : brushTextWhite;
				g.DrawString(TruncateString(entry.PlayerName, 11), fontRow, pBrush, x + 50, rowY + 3);

				// Class Name
				g.DrawString(TruncateString(entry.ClassName, 7), fontSmall, brushTextMuted, x + 138, rowY + 4);

				// Ability Badge
				DrawAbilityBadge(g, x + 196, rowY + 2, 60, 16, entry.AbilityType);

				// Item / Details
				string detailText = !string.IsNullOrEmpty(entry.AbilityItemName) ? entry.AbilityItemName : entry.Details;
				g.DrawString(TruncateString(detailText, 14), fontSmall, brushSilver, x + 262, rowY + 4);
			}

			DrawScrollBar(g, abilityScrollBar);
		}

		else if (parseSubTab == 2)
		{
			DrawPartySubTab(g, x, y);
		}
	}

	private void DrawPartySubTab(Graphics g, int x, int y)
	{
		int num = base.Width - 20;
		List<PartyMemberEntry> partyMembers = currentSnapshot.PartyMembers ?? new List<PartyMemberEntry>();
		int partyTotal = partyMembers.Count;
		int inDungeonCount = partyMembers.Count(p => p.IsInDungeon);
		int missingCount = partyTotal - inDungeonCount;

		// Summary stats at top
		string statStr = partyTotal > 0
			? (missingCount > 0 ? $"👥 {partyTotal} | ✔ {inDungeonCount} | ❌ {missingCount} MISSING" : $"👥 {partyTotal} | ✔ ALL IN DUNGEON")
			: "👥 0 in Party";
		SizeF statSz = g.MeasureString(statStr, fontSmall);
		using (SolidBrush bStat = new SolidBrush(missingCount > 0 ? Color.FromArgb(255, 120, 120) : (partyTotal > 0 ? HpGreen : TextMuted)))
		{
			g.DrawString(statStr, fontSmall, bStat, (float)(x + num) - statSz.Width - 60f, y + 2);
		}

		// [Clear] button at the top right
		partyClearBtnRect = new Rectangle(x + num - 52, y, 52, 18);
		bool isClearHover = partyClearBtnRect.Contains(PointToClient(Cursor.Position));
		using (SolidBrush bClear = new SolidBrush(isClearHover ? Color.FromArgb(55, 30, 30) : Color.FromArgb(30, 24, 26)))
		{
			g.FillRectangle(bClear, partyClearBtnRect);
		}
		using (Pen pClear = new Pen(isClearHover ? Color.FromArgb(180, 60, 60) : Color.FromArgb(70, 45, 50), 1f))
		{
			g.DrawRectangle(pClear, partyClearBtnRect);
		}
		g.DrawString("Clear", fontTiny, isClearHover ? brushTextWhite : brushTextMuted, partyClearBtnRect.X + 13, partyClearBtnRect.Y + 3);

		int contentStartY = y + 22;

		if (partyTotal == 0)
		{
			// Empty state
			using (SolidBrush emptyBg = new SolidBrush(Color.FromArgb(20, 22, 30)))
			{
				g.FillRectangle(emptyBg, x, contentStartY, num, 110);
			}
			using (Pen emptyBorder = new Pen(Color.FromArgb(40, 45, 60), 1f))
			{
				g.DrawRectangle(emptyBorder, x, contentStartY, num, 110);
			}

			g.DrawString("🚩 No party members detected", fontHeader, brushGold, x + 14, contentStartY + 14);
			g.DrawString("Party members are automatically tracked when you form or join a party in Realm.", fontSmall, brushTextWhite, x + 14, contentStartY + 38);
			g.DrawString("This tab compares players in your Party vs players inside the current Dungeon,", fontSmall, brushTextMuted, x + 14, contentStartY + 58);
			g.DrawString("instantly identifying who got left behind, nexused, or disconnected.", fontSmall, brushTextMuted, x + 14, contentStartY + 76);
			return;
		}

		// Build rows to render:
		var missingList = partyMembers.Where(p => !p.IsInDungeon).OrderBy(p => p.Name).ToList();
		var inDungeonList = partyMembers.Where(p => p.IsInDungeon).OrderByDescending(p => p.IsLocalPlayer).ThenBy(p => p.Name).ToList();

		List<PartyDisplayItem> displayRows = new List<PartyDisplayItem>();

		if (missingList.Count > 0)
		{
			displayRows.Add(new PartyDisplayItem { IsHeader = true, HeaderText = $"❌ NOT IN DUNGEON ({missingList.Count})", IsWarning = true });
			displayRows.Add(new PartyDisplayItem { IsColumnHeader = true, Col1 = "NAME", Col2 = "CLASS", Col3 = "STATUS", Col4 = "LAST KNOWN HP" });
			foreach (var m in missingList)
			{
				displayRows.Add(new PartyDisplayItem { Member = m, IsMissing = true });
			}
		}

		if (inDungeonList.Count > 0)
		{
			displayRows.Add(new PartyDisplayItem { IsHeader = true, HeaderText = $"✔ IN DUNGEON ({inDungeonList.Count})", IsWarning = false });
			displayRows.Add(new PartyDisplayItem { IsColumnHeader = true, Col1 = "NAME", Col2 = "CLASS", Col3 = "MAX", Col4 = "HP / STATUS" });
			foreach (var d in inDungeonList)
			{
				displayRows.Add(new PartyDisplayItem { Member = d, IsMissing = false });
			}
		}

		int rowH = 26;
		int availH = base.Height - contentStartY - 14;
		int visibleCount = Math.Max(1, availH / rowH);
		partyScrollBar.Configure(new Rectangle(x + num - 8, contentStartY, 8, visibleCount * rowH), displayRows.Count, visibleCount);

		bool isScrollable = displayRows.Count > visibleCount;
		int scrollBarW = isScrollable ? 8 : 0;
		int rowW = num - scrollBarW;

		for (int i = 0; i < visibleCount; i++)
		{
			int rowIdx = i + partyScrollBar.Offset;
			if (rowIdx >= displayRows.Count) break;

			PartyDisplayItem item = displayRows[rowIdx];
			int rowY = contentStartY + i * rowH;

			if (item.IsHeader)
			{
				Color bgH = item.IsWarning ? Color.FromArgb(50, 24, 24) : Color.FromArgb(20, 42, 28);
				Color borderH = item.IsWarning ? Color.FromArgb(140, 45, 45) : Color.FromArgb(40, 120, 60);
				using (SolidBrush bH = new SolidBrush(bgH))
				{
					g.FillRectangle(bH, x, rowY, rowW, rowH - 2);
				}
				using (Pen pH = new Pen(borderH, 1f))
				{
					g.DrawRectangle(pH, x, rowY, rowW - 1, rowH - 3);
				}
				using (SolidBrush textH = new SolidBrush(item.IsWarning ? Color.FromArgb(255, 140, 140) : Color.FromArgb(100, 240, 140)))
				{
					g.DrawString(item.HeaderText, fontHeader, textH, x + 8, rowY + 5);
				}
			}
			else if (item.IsColumnHeader)
			{
				using (SolidBrush bSubHeader = new SolidBrush(Color.FromArgb(26, 28, 38)))
				{
					g.FillRectangle(bSubHeader, x, rowY, rowW, rowH - 2);
				}
				g.DrawString(item.Col1, fontTiny, brushTextMuted, x + 14, rowY + 6);
				g.DrawString(item.Col2, fontTiny, brushTextMuted, x + 115, rowY + 6);
				g.DrawString(item.Col3, fontTiny, brushTextMuted, x + 195, rowY + 6);
				g.DrawString(item.Col4, fontTiny, brushTextMuted, x + 250, rowY + 6);
			}
			else
			{
				PartyMemberEntry m = item.Member;
				if (item.IsMissing)
				{
					Color rowBg = (i % 2 == 1) ? Color.FromArgb(32, 20, 22) : Color.FromArgb(24, 16, 18);
					using (SolidBrush bRow = new SolidBrush(rowBg))
					{
						g.FillRectangle(bRow, x, rowY, rowW, rowH - 2);
					}
					using (SolidBrush bStripe = new SolidBrush(Color.FromArgb(220, 60, 60)))
					{
						g.FillRectangle(bStripe, x, rowY, 3, rowH - 2);
					}

					using (SolidBrush bName = new SolidBrush(Color.FromArgb(255, 150, 150)))
					{
						g.DrawString(TruncateString(m.Name, 14), fontHeader, bName, x + 14, rowY + 5);
					}

					g.DrawString(m.ClassName ?? "Unknown", fontSmall, brushSilver, x + 115, rowY + 6);

					string statusText = m.Status;
					Color badgeBg = Color.FromArgb(50, 20, 20);
					Color badgeBorder = Color.FromArgb(150, 40, 40);
					Color badgeText = Color.FromArgb(255, 140, 140);

					if (statusText == "DIED")
					{
						badgeBg = Color.FromArgb(60, 15, 15);
						badgeBorder = Color.FromArgb(200, 30, 30);
						badgeText = Color.FromArgb(255, 100, 100);
					}
					else if (statusText.StartsWith("NEXUSED"))
					{
						badgeBg = Color.FromArgb(50, 35, 15);
						badgeBorder = Color.FromArgb(180, 120, 30);
						badgeText = Color.FromArgb(255, 190, 80);
					}

					int badgeW = Math.Max(50, (int)g.MeasureString(statusText, fontTiny).Width + 10);
					Rectangle badgeRect = new Rectangle(x + 195, rowY + 4, badgeW, 16);
					using (SolidBrush bBadge = new SolidBrush(badgeBg))
					{
						g.FillRectangle(bBadge, badgeRect);
					}
					using (Pen pBadge = new Pen(badgeBorder, 1f))
					{
						g.DrawRectangle(pBadge, badgeRect);
					}
					using (SolidBrush bText = new SolidBrush(badgeText))
					{
						g.DrawString(statusText, fontTiny, bText, badgeRect.X + 5, badgeRect.Y + 2);
					}

					if (m.MaxHp > 0)
					{
						string hpStr = $"{m.CurrentHp:N0}/{m.MaxHp:N0} ({m.HpPercentage:F0}%)";
						g.DrawString(hpStr, fontSmall, brushTextMuted, x + 250 + badgeW - 40, rowY + 6);
					}
					else
					{
						g.DrawString("Missed Portal / Not Entered", fontTiny, brushTextMuted, x + 250 + badgeW - 40, rowY + 6);
					}
				}
				else
				{
					if (m.IsLocalPlayer)
					{
						g.FillRectangle(brushLocalPlayerRow, x, rowY, rowW, rowH - 2);
						using (SolidBrush bStripe = new SolidBrush(AccentCyan))
						{
							g.FillRectangle(bStripe, x, rowY, 3, rowH - 2);
						}
					}
					else
					{
						Color rowBg = (i % 2 == 1) ? Color.FromArgb(22, 26, 34) : Color.FromArgb(16, 19, 26);
						using (SolidBrush bRow = new SolidBrush(rowBg))
						{
							g.FillRectangle(bRow, x, rowY, rowW, rowH - 2);
						}
						using (SolidBrush bStripe = new SolidBrush(Color.FromArgb(46, 204, 113)))
						{
							g.FillRectangle(bStripe, x, rowY, 3, rowH - 2);
						}
					}

					SolidBrush bName = m.IsLocalPlayer ? brushAccentCyan : brushTextWhite;
					g.DrawString(TruncateString(m.Name, 14), fontHeader, bName, x + 14, rowY + 5);

					g.DrawString(m.ClassName ?? "Unknown", fontSmall, brushSilver, x + 115, rowY + 6);

					if (m.MaxedCount == 8)
					{
						g.DrawString("8/8", fontSmall, brushGold, x + 195, rowY + 6);
					}
					else if (m.MaxedCount >= 0)
					{
						g.DrawString($"{m.MaxedCount}/8", fontSmall, brushTextMuted, x + 195, rowY + 6);
					}
					else
					{
						g.DrawString("-", fontSmall, brushTextMuted, x + 195, rowY + 6);
					}

					int barX = x + 245;
					int barW = 75;
					int barH = 12;
					int barY = rowY + 6;
					using (SolidBrush bBarBg = new SolidBrush(Color.FromArgb(35, 38, 48)))
					{
						g.FillRectangle(bBarBg, barX, barY, barW, barH);
					}
					float hpPct = (float)Math.Max(0.0, Math.Min(100.0, m.HpPercentage));
					int fillW = (int)((hpPct / 100f) * barW);
					Color barColor = (hpPct > 50f) ? HpGreen : ((hpPct > 25f) ? GoldColor : HpRed);
					using (SolidBrush bBarFill = new SolidBrush(barColor))
					{
						g.FillRectangle(bBarFill, barX, barY, fillW, barH);
					}
					string hpText = $"{m.CurrentHp}/{m.MaxHp} ({hpPct:F0}%)";
					g.DrawString(hpText, fontTiny, brushTextWhite, barX + 3, barY);

					string badgeText = m.IsLocalPlayer ? "YOU" : "READY";
					using (SolidBrush bStatus = new SolidBrush(m.IsLocalPlayer ? AccentCyan : HpGreen))
					{
						g.DrawString(badgeText, fontTiny, bStatus, x + 335, rowY + 6);
					}
				}
			}
		}

		DrawScrollBar(g, partyScrollBar);
	}

	private void DrawMyInfoTab(Graphics g, int x, int y)
	{
		int num = base.Width - 20;
		LocalPlayerInfo localPlayer = currentSnapshot.LocalPlayer;
		string s = $"★ {localPlayer.Stars}";
		g.DrawString(s, fontHeader, brushGold, x + 2, y);
		string s2 = $"{localPlayer.Name}  (Lvl {localPlayer.Level} {localPlayer.ClassName})";
		g.DrawString(s2, fontHeader, brushAccentCyan, x + 42, y);
		string s3 = ((localPlayer.TotalExalts <= 0) ? ((localPlayer.MaxedCount == 8) ? "[\ud83c\udf1f 8/8 MAXED]" : ("[" + localPlayer.MaxedStats + " MAXED]")) : ((localPlayer.MaxedCount == 8) ? $"[\ud83c\udf1f 8/8 • {localPlayer.TotalExalts}/40 EXALT]" : $"[{localPlayer.MaxedStats} • {localPlayer.TotalExalts}/40 EXALT]"));
		using (SolidBrush brush = new SolidBrush((localPlayer.MaxedCount == 8) ? GoldColor : ((localPlayer.MaxedCount >= 6) ? HpGreen : TextMuted)))
		{
			SizeF sizeF = g.MeasureString(s3, fontBadge);
			g.DrawString(s3, fontBadge, brush, (float)(x + num) - sizeF.Width - 2f, y + 1);
		}
		int num2 = y + 18;
		int num3 = 13;
		int num4 = Math.Max(1, localPlayer.MaxHp);
		int num5 = Math.Max(0, localPlayer.CurrentHp);
		double num6 = Math.Min(1.0, (double)num5 / (double)num4);
		g.FillRectangle(brushHpBg, x + 2, num2, num - 4, num3);
		int num7 = (int)((double)(num - 4) * num6);
		if (num7 > 0)
		{
			using LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(x + 2, num2, num7, num3), HpRed, HpGreen, LinearGradientMode.Horizontal);
			g.FillRectangle(brush2, x + 2, num2, num7, num3);
		}
		g.DrawRectangle(penBorderThin, x + 2, num2, num - 4, num3);
		string s4 = $"HP: {num5:N0} / {num4:N0}";
		SizeF sizeF2 = g.MeasureString(s4, fontTiny);
		g.DrawString(s4, fontTiny, brushTextWhite, (float)(x + 2) + ((float)(num - 4) - sizeF2.Width) / 2f, num2);
		int num8 = num2 + num3 + 2;
		int num9 = 12;
		int num10 = Math.Max(1, localPlayer.MaxMp);
		int num11 = Math.Max(0, localPlayer.CurrentMp);
		double num12 = Math.Min(1.0, (double)num11 / (double)num10);
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(15, 20, 35)))
		{
			g.FillRectangle(brush3, x + 2, num8, num - 4, num9);
		}
		int num13 = (int)((double)(num - 4) * num12);
		if (num13 > 0)
		{
			using LinearGradientBrush brush4 = new LinearGradientBrush(new Rectangle(x + 2, num8, num13, num9), Color.FromArgb(0, 100, 200), AccentCyan, LinearGradientMode.Horizontal);
			g.FillRectangle(brush4, x + 2, num8, num13, num9);
		}
		g.DrawRectangle(penBorderThin, x + 2, num8, num - 4, num9);
		string s5 = $"MP: {num11:N0} / {num10:N0}";
		SizeF sizeF3 = g.MeasureString(s5, fontTiny);
		g.DrawString(s5, fontTiny, brushTextWhite, (float)(x + 2) + ((float)(num - 4) - sizeF3.Width) / 2f, num8);
		int num14 = num8 + num9 + 4;
		int num15 = (num - 10) / 4;
		int num16 = 17;
		(string, int, int, int, int)[] array = new(string, int, int, int, int)[8]
		{
			("ATT", localPlayer.Attack, localPlayer.BaseAttack, localPlayer.MaxAttack, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length != 0) ? localPlayer.Exaltations[0] : 0),
			("DEF", localPlayer.Defense, localPlayer.BaseDefense, localPlayer.MaxDefense, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 1) ? localPlayer.Exaltations[1] : 0),
			("SPD", localPlayer.Speed, localPlayer.BaseSpeed, localPlayer.MaxSpeed, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 2) ? localPlayer.Exaltations[2] : 0),
			("DEX", localPlayer.Dexterity, localPlayer.BaseDexterity, localPlayer.MaxDexterity, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 5) ? localPlayer.Exaltations[5] : 0),
			("VIT", localPlayer.Vitality, localPlayer.BaseVitality, localPlayer.MaxVitality, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 3) ? localPlayer.Exaltations[3] : 0),
			("WIS", localPlayer.Wisdom, localPlayer.BaseWisdom, localPlayer.MaxWisdom, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 4) ? localPlayer.Exaltations[4] : 0),
			("LIFE", localPlayer.MaxHp, localPlayer.BaseLife, localPlayer.MaxLife, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 6) ? localPlayer.Exaltations[6] : 0),
			("MANA", localPlayer.MaxMp, localPlayer.BaseMana, localPlayer.MaxMana, (localPlayer.Exaltations != null && localPlayer.Exaltations.Length > 7) ? localPlayer.Exaltations[7] : 0)
		};
		for (int i = 0; i < 8; i++)
		{
			int num17 = i % 4;
			int num18 = i / 4;
			int num19 = x + 2 + num17 * (num15 + 2);
			int num20 = num14 + num18 * (num16 + 2);
			(string, int, int, int, int) tuple = array[i];
			bool flag = tuple.Item4 > 0 && tuple.Item3 >= tuple.Item4;
			using (SolidBrush brush5 = new SolidBrush(flag ? Color.FromArgb(30, 255, 204, 0) : Color.FromArgb(24, 26, 35)))
			{
				g.FillRectangle(brush5, num19, num20, num15, num16);
			}
			using (Pen pen = new Pen(flag ? GoldColor : Color.FromArgb(45, 48, 64), 1f))
			{
				g.DrawRectangle(pen, num19, num20, num15, num16);
			}
			using SolidBrush brush6 = new SolidBrush(flag ? GoldColor : TextWhite);
			string s6 = ((tuple.Item5 > 0) ? $"{tuple.Item1}: {tuple.Item2} (+{tuple.Item5})" : $"{tuple.Item1}: {tuple.Item2}");
			g.DrawString(s6, fontBadge, brush6, num19 + 3, num20 + 2);
		}
		int num21 = num14 + 2 * (num16 + 2) + 4;
		g.DrawString("⚔ EQUIPPED SET & CALCULATED DPS", fontBadge, brushAccentCyan, x + 2, num21);
		int num22 = 36;
		int num23 = 8;
		int num24 = num21 + 15;
		for (int j = 0; j < 4; j++)
		{
			int num25 = x + 4 + j * (num22 + num23);
			string rarityOverride = ((localPlayer.EquipmentRarities != null && localPlayer.EquipmentRarities.Length > j) ? localPlayer.EquipmentRarities[j] : null);
			int enchantCount = ((localPlayer.EquipmentEnchantCounts != null && localPlayer.EquipmentEnchantCounts.Length > j) ? localPlayer.EquipmentEnchantCounts[j] : (-1));
			ItemSpriteManager.DrawEquipmentSlot(g, num25, num24, num22, num22, localPlayer.EquipmentIds[j], j, rarityOverride, enchantCount);
		}
		int num26 = x + 4 + 4 * (num22 + num23) + 4;
		string text = TruncateString(localPlayer.WeaponBulletInfo, 36);
		g.DrawString("• " + text, fontTiny, brushTextMuted, num26, num24);
		g.DrawString($"• Weapon: {localPlayer.WeaponDps:N0} dmg/s", fontTiny, brushTextWhite, num26, num24 + 12);
		g.DrawString($"⚡ True Set DPS: {localPlayer.TrueSetDps:N0} /s", fontBadge, brushGold, num26, num24 + 24);
		int num27 = num24 + num22 + 6;
		int count = currentSnapshot.LootBags.Count;
		g.DrawString($"\ud83c\udf81 LOOT BAGS (Blue+ Bags: {count})", fontBadge, brushGold, x + 2, num27);
		int num28 = num27 + 15;
		int num29 = Math.Max(0, base.Height - num28 - 14);
		if (num29 < 32) return;
		Rectangle rect = new Rectangle(x, num28, num, num29);
		using (SolidBrush brush7 = new SolidBrush(Color.FromArgb(20, 22, 30)))
		{
			g.FillRectangle(brush7, rect);
		}
		g.DrawRectangle(penBorderThin, rect);
		if (count == 0)
		{
			g.DrawString("No qualifying loot bags dropped yet.", fontSmall, brushTextMuted, x + 10, num28 + 15);
			g.DrawString("Monitoring drops from Blue, Cyan, White, Orange, & Red bags.", fontTiny, brushTextMuted, x + 10, num28 + 32);
			return;
		}
		int num30 = 32;
		int num31 = Math.Max(1, num29 / num30);
		lootScrollBar.Configure(new Rectangle(x + num - 8, num28 + 2, 8, num31 * num30), count, num31);
		int lootContentRight = x + num - (lootScrollBar.Visible ? 12 : 4);

		for (int k = 0; k < num31; k++)
		{
			int num33 = k + lootScrollBar.Offset;
			if (num33 >= count)
			{
				break;
			}
			LootBagDrop lootBagDrop = currentSnapshot.LootBags[num33];
			int num34 = num28 + 2 + k * num30;
			using (SolidBrush brush8 = new SolidBrush(lootBagDrop.BagColor))
			{
				string text2 = lootBagDrop.BagType.Replace(" Bag", "").ToUpper();
				if (lootBagDrop.HasShinyItem)
				{
					text2 = "✨ " + text2;
				}
				g.DrawString("[" + text2 + "]", fontBadge, brush8, x + 4, num34 + 4);
			}
			string s7 = $"{TruncateString(lootBagDrop.SourceMonster, 16)} • {lootBagDrop.DropTime:HH:mm:ss}";
			g.DrawString(s7, fontTiny, brushTextMuted, x + 4, num34 + 16);
			int num35 = x + 145;
			int num36 = 26;
			for (int l = 0; l < lootBagDrop.ItemIds.Count; l++)
			{
				int num37 = num35 + l * (num36 + 4);
				if (num37 + num36 > lootContentRight)
				{
					break;
				}
				int itemId = lootBagDrop.ItemIds[l];
				string rOverride = (lootBagDrop.ItemRarities != null && lootBagDrop.ItemRarities.Count > l) ? lootBagDrop.ItemRarities[l] : null;
				int eCount = (lootBagDrop.ItemEnchantCounts != null && lootBagDrop.ItemEnchantCounts.Count > l) ? lootBagDrop.ItemEnchantCounts[l] : -1;
				bool shiny = (lootBagDrop.ItemIsShiny != null && lootBagDrop.ItemIsShiny.Count > l) && lootBagDrop.ItemIsShiny[l];
				ItemSpriteManager.DrawEquipmentSlot(g, num37, num34 + 2, num36, num36, itemId, 0, rOverride, eCount, shiny);
			}
		}
		DrawScrollBar(g, lootScrollBar);
		if (count > num31)
		{
			string s8 = $"▲▼ ({lootScrollBar.Offset + 1}-{Math.Min(count, lootScrollBar.Offset + num31)} of {count})";
			g.DrawString(s8, fontTiny, brushAccentCyan, x + num - 95, num27);
		}
	}

	private void DrawMonsterDropdownPopup(Graphics g)
	{
		int num = monsterDropdownBarRect.Width;
		int num2 = 7;
		int num3 = 22;
		List<(string, Color)> dropdownItems = GetDropdownItems();
		int count = dropdownItems.Count;
		int num4 = Math.Min(num2, count);
		int num5 = num4 * num3 + 4;
		monsterDropdownPopupRect = new Rectangle(monsterDropdownBarRect.X, monsterDropdownBarRect.Bottom + 1, num, num5);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(250, 18, 20, 28)))
		{
			g.FillRectangle(brush, monsterDropdownPopupRect);
		}
		g.DrawRectangle(penBorderThin, monsterDropdownPopupRect);
		bool flag = count > num2;
		int num6 = (flag ? 10 : 0);
		int num7 = num - num6 - 4;
		dropdownScrollBar.Configure(new Rectangle(monsterDropdownPopupRect.Right - num6 - 2,
			monsterDropdownPopupRect.Y + 2, num6, num5 - 4), count, num2);

		for (int i = 0; i < num4; i++)
		{
			int num9 = i + dropdownScrollBar.Offset;
			if (num9 >= dropdownItems.Count)
			{
				break;
			}
			int num10 = monsterDropdownPopupRect.Y + 2 + i * num3;
			(string, Color) tuple = dropdownItems[num9];
			if ((num9 == 0 && currentSnapshot.SelectedMonsterMode == -1) || (num9 == 1 && currentSnapshot.SelectedMonsterMode == -2) || (num9 >= 2 && currentSnapshot.SelectedMonsterMode == num9 - 2))
			{
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb(50, 0, 180, 220));
				g.FillRectangle(brush2, monsterDropdownPopupRect.X + 2, num10, num7, num3 - 2);
			}
			g.DrawString(tuple.Item1, fontRow, new SolidBrush(tuple.Item2), monsterDropdownPopupRect.X + 6, num10 + 2);
		}
		DrawScrollBar(g, dropdownScrollBar);
	}

	private List<(string text, Color color)> GetDropdownItems()
	{
		List<(string, Color)> list = new List<(string, Color)>();
		bool flag = currentSnapshot.HistoryIndex >= 0;
		string item = (flag ? ("\ud83d\udcdc [MAIN TARGET] " + TruncateString(currentSnapshot.ActiveEnemy?.Name ?? "Main Target", 20)) : ("\ud83d\udd34 [LIVE] " + TruncateString(currentSnapshot.ActiveEnemy?.Name ?? "Live Target", 22)));
		list.Add((item, flag ? GoldColor : HpGreen));
		list.Add(("\ud83c\udf1f [ALL MOBS COMBINED]", AccentCyan));
		foreach (EnemyCombatSnapshot defeatedEnemy in currentSnapshot.DefeatedEnemies)
		{
			int num = (int)defeatedEnemy.FightDurationSeconds;
			string text = $"{num / 60:D2}:{num % 60:D2}";
			string item2 = "\ud83d\udc80 " + TruncateString(defeatedEnemy.Name, 17) + " (" + FormatNumber(defeatedEnemy.TotalDamage) + " dmg, " + text + ")";
			list.Add((item2, GoldColor));
		}
		return list;
	}

	private void DrawTooltip(Graphics g)
	{
		if (string.IsNullOrEmpty(hoveredTooltipText))
		{
			return;
		}
		int num = Math.Max(120, base.Width - 16);
		int num2 = num - 14;
		SizeF sizeF = g.MeasureString(hoveredTooltipText, fontSmall, num2);
		int num3 = Math.Min(num, (int)Math.Ceiling(sizeF.Width) + 14);
		int num4 = (int)Math.Ceiling(sizeF.Height) + 8;
		int num5 = hoveredTooltipPos.X;
		if (num5 + num3 > base.Width - 8)
		{
			num5 = base.Width - 8 - num3;
		}
		if (num5 < 8)
		{
			num5 = 8;
		}
		int num6 = hoveredTooltipPos.Y;
		if (num6 + num4 > base.Height - 8)
		{
			num6 = Math.Max(8, base.Height - 8 - num4);
		}
		if (num6 < 8)
		{
			num6 = 8;
		}
		Rectangle rect = new Rectangle(num5, num6, num3, num4);
		Rectangle rectangle = new Rectangle(num5 + 6, num6 + 4, num3 - 12, num4 - 8);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(248, 12, 15, 20)))
		{
			g.FillRectangle(brush, rect);
		}
		using (Pen pen = new Pen(AccentCyan, 1f))
		{
			g.DrawRectangle(pen, rect);
		}
		using StringFormat stringFormat = new StringFormat();
		stringFormat.Alignment = StringAlignment.Near;
		stringFormat.LineAlignment = StringAlignment.Near;
		stringFormat.Trimming = StringTrimming.EllipsisWord;
		stringFormat.FormatFlags = StringFormatFlags.NoClip;
		g.DrawString(hoveredTooltipText, fontSmall, brushTextWhite, rectangle, stringFormat);
	}

	private void DrawCompactBossBar(Graphics g, int x, int y, int width, int height)
	{
		EnemyCombatSnapshot currentDisplayEnemy = currentSnapshot.GetCurrentDisplayEnemy();
		int maxHp = currentDisplayEnemy.MaxHp;
		int currentHp = currentDisplayEnemy.CurrentHp;
		double num = ((maxHp > 0) ? Math.Max(0.0, Math.Min(100.0, (double)currentHp / (double)maxHp * 100.0)) : 0.0);
		if (!currentDisplayEnemy.IsDead && num > 0.0)
		{
			int num2 = (int)((double)width * (num / 100.0));
			if (num2 > 0)
			{
				using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(x, y, num2, height), HpRed, HpGreen, LinearGradientMode.Horizontal);
				g.FillRectangle(brush, x, y, num2, height);
			}
		}
		bool flag = IsOryx3Combat(currentSnapshot, currentDisplayEnemy) && currentDisplayEnemy.IsGuarding;
		if (flag)
		{
			using Pen pen = new Pen(Color.FromArgb(255, 239, 68, 68), 2f);
			g.DrawRectangle(pen, x, y, width, height);
		}
		else
		{
			g.DrawRectangle(penBorderThin, x, y, width, height);
		}
		string text = ((!string.IsNullOrEmpty(currentDisplayEnemy.Name)) ? currentDisplayEnemy.Name : "No Target Active");
		string text2 = "";
		if (currentDisplayEnemy.IsCursed)
		{
			text2 += " [CURSE]";
		}
		if (currentDisplayEnemy.IsArmorBroken)
		{
			text2 += " [BREAK]";
		}
		if (currentDisplayEnemy.IsStunned)
		{
			text2 += " [STUN]";
		}
		if (currentDisplayEnemy.IsDazed)
		{
			text2 += " [DAZE]";
		}
		string s = (currentDisplayEnemy.IsDead ? (text + " (SLAIN)") : ((maxHp <= 0) ? (flag ? (text + " [\ud83d\udee1\ufe0f GUARD]" + text2) : (text + text2)) : (flag ? $"{text}: {currentHp:N0}/{maxHp:N0} ({num:F1}%)  [\ud83d\udee1\ufe0f GUARD]{text2}" : $"{text}: {currentHp:N0}/{maxHp:N0} ({num:F1}%){text2}")));
		SizeF sizeF = g.MeasureString(s, fontHeader);
		g.DrawString(s, fontHeader, brushTextWhite, (float)x + ((float)width - sizeF.Width) / 2f, (float)y + ((float)height - sizeF.Height) / 2f);
	}

	private static string FormatNumber(double num)
	{
		if (num >= 1000000.0)
		{
			return (num / 1000000.0).ToString("F1") + "M";
		}
		if (num >= 1000.0)
		{
			return (num / 1000.0).ToString("F1") + "k";
		}
		return ((int)num).ToString();
	}

	private static string TruncateString(string val, int maxLen)
	{
		if (string.IsNullOrEmpty(val))
		{
			return "";
		}
		if (val.Length <= maxLen)
		{
			return val;
		}
		return val.Substring(0, maxLen - 1) + ".";
	}

	private bool IsOryx3Combat(DpsSnapshot snapshot, EnemyCombatSnapshot enemy)
	{
		if (snapshot != null && !string.IsNullOrEmpty(snapshot.DungeonName) && snapshot.DungeonName.IndexOf("Sanctuary", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			if (enemy != null && !string.IsNullOrEmpty(enemy.Name))
			{
				if (enemy.Name.IndexOf("Oryx", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
				if (enemy.Name.IndexOf("All Monsters", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}
		if (enemy != null && !string.IsNullOrEmpty(enemy.Name) && (enemy.Name.IndexOf("Oryx the Mad God 3", StringComparison.OrdinalIgnoreCase) >= 0 || enemy.Name.IndexOf("Oryx 3", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return true;
		}
		return false;
	}

	private void CheckMoonlightVillageTooltip(Point pt)
	{
		int prevHovered = hoveredMvBtn;
		string text = null;
		Point pos = Point.Empty;

		if (mvBtnMinusRect.Contains(pt))
		{
			hoveredMvBtn = 0;
			Cursor = Cursors.Hand;
			text = "Remove 1 Flame (-1) from current phase";
			pos = new Point(Math.Max(10, pt.X - 50), Math.Max(5, pt.Y - 28));
		}
		else if (mvBtnPlusRect.Contains(pt))
		{
			hoveredMvBtn = 1;
			Cursor = Cursors.Hand;
			text = "Add 1 Flame (+1) to current phase";
			pos = new Point(Math.Max(10, pt.X - 50), Math.Max(5, pt.Y - 28));
		}
		else if (mvBtnNextRect.Contains(pt))
		{
			hoveredMvBtn = 2;
			Cursor = Cursors.Hand;
			text = "Advance to next phase (locks current flames)";
			pos = new Point(Math.Max(10, pt.X - 80), Math.Max(5, pt.Y - 28));
		}
		else if (mvBtnResetRect.Contains(pt))
		{
			hoveredMvBtn = 3;
			Cursor = Cursors.Hand;
			text = (currentMvSnapshot != null && currentMvSnapshot.IsUmiMode)
				? "Reset Kitsune Umi counters"
				: "Reset all Moonlight Village counters";
			pos = new Point(Math.Max(10, pt.X - 60), Math.Max(5, pt.Y - 28));
		}
		else
		{
			hoveredMvBtn = -1;
		}

		if (hoveredMvBtn != prevHovered || hoveredTooltipText != text)
		{
			hoveredTooltipText = text;
			hoveredTooltipPos = pos;
			Invalidate();
		}
	}

	private void DrawMoonlightVillageTab(Graphics g, int x, int y)
	{
		int width = base.Width - 20;
		int curY = y;
		bool isUmiMode = currentMvSnapshot != null && currentMvSnapshot.IsUmiMode;

		// 1. Active Dancer / Encounter Banner
		string dancer = string.IsNullOrEmpty(currentMvSnapshot.ActiveDancer) || currentMvSnapshot.ActiveDancer == "None"
			? "Waiting for Dancer"
			: currentMvSnapshot.ActiveDancer;
		Color dancerColor = isUmiMode ? Color.FromArgb(232, 121, 249) : GetMvDancerColor(dancer);
		Rectangle dancerRect = new Rectangle(x, curY, width, 44);

		using (GraphicsPath path = CreateRoundedRectangle(dancerRect, 6))
		{
			using (SolidBrush cardBg = new SolidBrush(Color.FromArgb(22, 26, 36)))
			{
				g.FillPath(cardBg, path);
			}
			using (Pen borderPen = new Pen(dancerColor, 1.2f))
			{
				g.DrawPath(borderPen, path);
			}
		}

		string dancerIcon;
		string bossSubtext;
		if (isUmiMode)
		{
			dancerIcon = "🦊 Kitsune Umi (Secret Boss)";
			bossSubtext = $"Fox Spirit Encounter  •  Phase {currentMvSnapshot.CurrentPhaseNumber}/7";
		}
		else
		{
			dancerIcon = dancer switch
			{
				"Miko" => "🌸 Dancer Miko (Pink)",
				"Genji" => "💧 Sage Genji (Blue)",
				"Kaguya" => "⚡ Drummer Kaguya (Yellow)",
				"Umi" => "🦊 Kitsune Umi",
				_ => "🏮 Village Street / Waiting"
			};
			bossSubtext = $"Boss {currentMvSnapshot.CurrentBossIndex}/3  •  Dancers Slain: {currentMvSnapshot.DancersCompleted}/3";
		}

		using (SolidBrush dBrush = new SolidBrush(dancerColor))
		{
			g.DrawString(dancerIcon, fontHeader, dBrush, x + 10, curY + 6);
		}
		g.DrawString(bossSubtext, fontSmall, brushTextMuted, x + 10, curY + 24);

		// Status pill on the right
		string statusPill = isUmiMode
			? (currentMvSnapshot.IsInDungeon ? "● ACTIVE UMI" : "UMI STANDBY")
			: (currentMvSnapshot.IsInDungeon ? "● ACTIVE MV" : "STANDBY");
		Color pillColor = currentMvSnapshot.IsInDungeon
			? (isUmiMode ? Color.FromArgb(232, 121, 249) : HpGreen)
			: TextMuted;
		Rectangle pillRect = new Rectangle(x + width - 96, curY + 12, 86, 20);
		using (GraphicsPath pillPath = CreateRoundedRectangle(pillRect, 4))
		{
			using (SolidBrush pillBg = new SolidBrush(Color.FromArgb(30, pillColor)))
			{
				g.FillPath(pillBg, pillPath);
			}
			using (Pen pillBorder = new Pen(pillColor, 1f))
			{
				g.DrawPath(pillBorder, pillPath);
			}
			using (SolidBrush pillText = new SolidBrush(pillColor))
			{
				StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
				g.DrawString(statusPill, fontBadge, pillText, pillRect, sf);
			}
		}

		curY += 50;

		// 2. Current Phase Flames Card
		bool isHolding = currentMvSnapshot.IsHoldingPhaseResult && DateTime.UtcNow < currentMvSnapshot.HoldUntilUtc;
		int displayPhaseNum = isHolding ? currentMvSnapshot.DisplayPhaseNumber : currentMvSnapshot.CurrentPhaseNumber;
		string displayPhaseName = isHolding ? currentMvSnapshot.DisplayPhaseName : currentMvSnapshot.CurrentPhaseName;
		int displayFlames = Math.Max(0, Math.Min(8, isHolding ? currentMvSnapshot.DisplayPhaseFlames : currentMvSnapshot.CurrentPhaseFlames));
		int maxPhases = isUmiMode ? 7 : 11;

		Rectangle phaseCardRect = new Rectangle(x, curY, width, 72);
		using (GraphicsPath path = CreateRoundedRectangle(phaseCardRect, 6))
		{
			using (SolidBrush cardBg = new SolidBrush(Color.FromArgb(20, 24, 34)))
			{
				g.FillPath(cardBg, path);
			}
			using (Pen cardBorder = new Pen(isHolding ? Color.FromArgb(74, 222, 128) : Color.FromArgb(45, 55, 75), isHolding ? 1.4f : 1f))
			{
				g.DrawPath(cardBorder, path);
			}
		}

		string phaseHeader = $"Phase {displayPhaseNum}/{maxPhases}: {displayPhaseName}";
		if (isHolding)
		{
			phaseHeader += "  ✓";
		}
		if (phaseHeader.Length > 28)
		{
			phaseHeader = phaseHeader.Substring(0, 26) + "..";
		}
		g.DrawString(phaseHeader, fontHeader, isHolding ? brushAccentCyan : brushTextWhite, x + 10, curY + 7);

		string flameCountStr = $"🔥 {displayFlames} / 8 Flames";
		SizeF flameSize = g.MeasureString(flameCountStr, fontHeader);
		using (SolidBrush fBrush = new SolidBrush(isHolding ? Color.FromArgb(74, 222, 128) : Color.FromArgb(255, 200, 60)))
		{
			g.DrawString(flameCountStr, fontHeader, fBrush, x + width - flameSize.Width - 10, curY + 7);
		}

		// Phase Progress Bar (0 to 8)
		Rectangle phaseBarRect = new Rectangle(x + 10, curY + 30, width - 20, 14);
		Color startCol = isHolding ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 140, 20);
		Color endCol = isHolding ? Color.FromArgb(74, 222, 128) : Color.FromArgb(255, 220, 50);
		DrawMvProgressBar(g, phaseBarRect, displayFlames, 8, startCol, endCol);

		// Tick marks for 8 slots
		int barSlotW = (width - 20) / 8;
		for (int i = 1; i < 8; i++)
		{
			int tickX = x + 10 + i * barSlotW;
			using Pen tickPen = new Pen(Color.FromArgb(70, 15, 20, 30), 1f);
			g.DrawLine(tickPen, tickX, curY + 30, tickX, curY + 44);
		}

		string phaseFooter;
		if (isHolding)
		{
			int secLeft = (int)Math.Max(1, Math.Ceiling((currentMvSnapshot.HoldUntilUtc - DateTime.UtcNow).TotalSeconds));
			phaseFooter = $"✓ Phase Complete (+{displayFlames} Flames) • Next: Phase {currentMvSnapshot.CurrentPhaseNumber} in {secLeft}s";
		}
		else if (isUmiMode)
		{
			if (displayPhaseNum == 7)
				phaseFooter = "Last Word Danmaku • Survive the Princess and the Fox finale!";
			else if (currentMvSnapshot.IsLanternPhase)
				phaseFooter = "Danmaku Phase • Complete lantern ring to spawn wisps";
			else
				phaseFooter = "DPS Phase • Damage Kitsune Umi to trigger wisp drop";
		}
		else
		{
			phaseFooter = currentMvSnapshot.IsLanternPhase
				? "Lantern Phase • Complete lantern ring to spawn wisps"
				: "DPS Phase • Physical reward wisps spawn at center arena upon phase end";
		}
		g.DrawString(phaseFooter, fontTiny, isHolding ? brushGold : brushTextMuted, x + 10, curY + 50);

		curY += 78;

		// 3. Total Run Flames & Tier Card
		int total = currentMvSnapshot.TotalFlames;
		int maxFlames = isUmiMode ? 56 : 88;
		Color tierColor = GetMvTierColor(currentMvSnapshot.CurrentTier, isUmiMode);
		Rectangle totalCardRect = new Rectangle(x, curY, width, 84);
		using (GraphicsPath path = CreateRoundedRectangle(totalCardRect, 6))
		{
			using (SolidBrush cardBg = new SolidBrush(Color.FromArgb(20, 24, 34)))
			{
				g.FillPath(cardBg, path);
			}
			using (Pen tierBorder = new Pen(tierColor, 1.2f))
			{
				g.DrawPath(tierBorder, path);
			}
		}

		string totalLabel = isUmiMode ? "TOTAL KITSUNE UMI FLAMES" : "TOTAL RUN FLAMES";
		g.DrawString(totalLabel, fontSmall, brushTextMuted, x + 10, curY + 6);

		string tierText = (currentMvSnapshot.CurrentTier > 0)
			? $"{total} / {maxFlames}  •  {currentMvSnapshot.TierName}"
			: $"{total} / {maxFlames}  (Need 2+ for T1)";
		SizeF tierSize = g.MeasureString(tierText, fontHeader);
		using (SolidBrush tBrush = new SolidBrush(tierColor))
		{
			g.DrawString(tierText, fontHeader, tBrush, x + width - tierSize.Width - 10, curY + 6);
		}

		// Total Progress Bar (0 to maxFlames)
		Rectangle totalBarRect = new Rectangle(x + 10, curY + 27, width - 20, 16);
		DrawMvProgressBar(g, totalBarRect, total, maxFlames, tierColor, Color.FromArgb(255, 255, 255));

		// Threshold markers:
		int barW = width - 20;
		if (isUmiMode)
		{
			// Umi Tiers: 24 (T2), 36 (T3), 48 (T4)
			int xT2 = x + 10 + (int)(barW * (24.0 / 56.0));
			int xT3 = x + 10 + (int)(barW * (36.0 / 56.0));
			int xT4 = x + 10 + (int)(barW * (48.0 / 56.0));
			using (Pen markerPen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f))
			{
				g.DrawLine(markerPen, xT2, curY + 27, xT2, curY + 43);
				g.DrawLine(markerPen, xT3, curY + 27, xT3, curY + 43);
				g.DrawLine(markerPen, xT4, curY + 27, xT4, curY + 43);
			}
		}
		else
		{
			// Village Tiers: 40 (T2), 58 (T3), 78 (T4)
			int xT2 = x + 10 + (int)(barW * (40.0 / 88.0));
			int xT3 = x + 10 + (int)(barW * (58.0 / 88.0));
			int xT4 = x + 10 + (int)(barW * (78.0 / 88.0));
			using (Pen markerPen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f))
			{
				g.DrawLine(markerPen, xT2, curY + 27, xT2, curY + 43);
				g.DrawLine(markerPen, xT3, curY + 27, xT3, curY + 43);
				g.DrawLine(markerPen, xT4, curY + 27, xT4, curY + 43);
			}
		}

		// Status Route and Buffer info
		string routeText = currentMvSnapshot.RouteStatus ?? (isUmiMode ? "Tier 4 Reachable" : "Tier 4 Reachable");
		using (SolidBrush rBrush = new SolidBrush(tierColor))
		{
			g.DrawString(routeText, fontBadge, rBrush, x + 10, curY + 48);
		}

		string umiBufferText;
		Color umiBufferColor;
		int t4Target = isUmiMode ? 48 : 78;
		if (currentMvSnapshot.MaxPossibleFlames >= t4Target)
		{
			umiBufferText = isUmiMode
				? $"Top Loot Safe (Buffer: {currentMvSnapshot.Tier4Margin} lost)"
				: $"100% Umi Safe (Buffer: {currentMvSnapshot.Tier4Margin} lost)";
			umiBufferColor = currentMvSnapshot.Tier4Margin >= 4 ? HpGreen : GoldColor;
		}
		else
		{
			umiBufferText = $"T4 Lost • Max: {currentMvSnapshot.MaxPossibleFlames}/{maxFlames}";
			umiBufferColor = HpRed;
		}

		SizeF umiSize = g.MeasureString(umiBufferText, fontSmall);
		using (SolidBrush uBrush = new SolidBrush(umiBufferColor))
		{
			g.DrawString(umiBufferText, fontSmall, uBrush, x + width - umiSize.Width - 10, curY + 48);
		}

		string legend = isUmiMode
			? "T1: 2-22   |   T2: 24-34   |   T3: 36-46   |   T4: 48-56 (Top Loot)"
			: "T1: 2-38   |   T2: 40-56   |   T3: 58-76   |   T4: 78-88 (100% Umi)";
		g.DrawString(legend, fontTiny, brushTextMuted, x + 10, curY + 66);

		curY += 90;

		// 4. Boss / Act Breakdown
		int colGap = 6;
		int colW = (width - (colGap * 2)) / 3;

		if (isUmiMode)
		{
			Color umiCardColor = Color.FromArgb(232, 121, 249);
			DrawBossMiniCard(g, x, curY, colW, 46,
				"Act 1 (P1-2)",
				currentMvSnapshot.UmiAct1Flames, 16,
				umiCardColor);

			DrawBossMiniCard(g, x + colW + colGap, curY, colW, 46,
				"Act 2 (P3-4)",
				currentMvSnapshot.UmiAct2Flames, 16,
				umiCardColor);

			DrawBossMiniCard(g, x + (colW + colGap) * 2, curY, colW, 46,
				"Finale (P5-7)",
				currentMvSnapshot.UmiAct3Flames, 24,
				umiCardColor);
		}
		else
		{
			DrawBossMiniCard(g, x, curY, colW, 46,
				$"B1: {currentMvSnapshot.Boss1Name}",
				currentMvSnapshot.Boss1Flames, 16,
				GetMvDancerColor(currentMvSnapshot.Boss1Name));

			DrawBossMiniCard(g, x + colW + colGap, curY, colW, 46,
				$"B2: {currentMvSnapshot.Boss2Name}",
				currentMvSnapshot.Boss2Flames, 32,
				GetMvDancerColor(currentMvSnapshot.Boss2Name));

			DrawBossMiniCard(g, x + (colW + colGap) * 2, curY, colW, 46,
				$"B3: {currentMvSnapshot.Boss3Name}",
				currentMvSnapshot.Boss3Flames, 40,
				GetMvDancerColor(currentMvSnapshot.Boss3Name));
		}

		curY += 52;

		// 5. Interactive Control Buttons
		mvBtnMinusRect = new Rectangle(x, curY, 70, 26);
		mvBtnPlusRect = new Rectangle(x + 74, curY, 70, 26);
		mvBtnNextRect = new Rectangle(x + 148, curY, 114, 26);
		mvBtnResetRect = new Rectangle(x + 266, curY, 94, 26);

		DrawMvButton(g, mvBtnMinusRect, "-1 Flame", Color.FromArgb(200, 160, 60), hoveredMvBtn == 0);
		DrawMvButton(g, mvBtnPlusRect, "+1 Flame", Color.FromArgb(74, 222, 128), hoveredMvBtn == 1);
		DrawMvButton(g, mvBtnNextRect, "Next Phase ⏭", Color.FromArgb(0, 210, 255), hoveredMvBtn == 2);
		DrawMvButton(g, mvBtnResetRect, "Reset ↺", Color.FromArgb(239, 68, 68), hoveredMvBtn == 3);

		curY += 30;
		string helpNote = isUmiMode
			? "Kitsune Umi Active. 7 Phases (0/56 flames). Toggle with /umi or /mvumi."
			: "Tip: Kitsune Umi tracking starts after the three dancers, when her fight begins.";
		g.DrawString(helpNote, fontTiny, brushTextMuted, x + 4, curY);
	}

	private void DrawBossMiniCard(Graphics g, int bx, int by, int bw, int bh, string title, int flames, int maxFlames, Color accent)
	{
		Rectangle rect = new Rectangle(bx, by, bw, bh);
		using (GraphicsPath path = CreateRoundedRectangle(rect, 4))
		{
			using (SolidBrush bg = new SolidBrush(Color.FromArgb(22, 26, 36)))
			{
				g.FillPath(bg, path);
			}
			using (Pen p = new Pen(Color.FromArgb(45, 55, 75), 1f))
			{
				g.DrawPath(p, path);
			}
		}

		if (title.Length > 14)
		{
			title = title.Substring(0, 12) + "..";
		}
		using (SolidBrush tBrush = new SolidBrush(accent))
		{
			g.DrawString(title, fontTiny, tBrush, bx + 6, by + 4);
		}

		string fStr = $"{flames}/{maxFlames}";
		SizeF fSize = g.MeasureString(fStr, fontBadge);
		g.DrawString(fStr, fontBadge, brushTextWhite, bx + bw - fSize.Width - 6, by + 4);

		Rectangle bRect = new Rectangle(bx + 6, by + 22, bw - 12, 8);
		DrawMvProgressBar(g, bRect, flames, maxFlames, accent, Color.FromArgb(255, 255, 255));
	}

	private void DrawMvButton(Graphics g, Rectangle rect, string text, Color accentColor, bool isHovered)
	{
		using (GraphicsPath path = CreateRoundedRectangle(rect, 4))
		{
			Color bgColor = isHovered ? Color.FromArgb(50, accentColor) : Color.FromArgb(25, 30, 42);
			using (SolidBrush bg = new SolidBrush(bgColor))
			{
				g.FillPath(bg, path);
			}
			using (Pen borderPen = new Pen(isHovered ? accentColor : Color.FromArgb(60, 75, 100), 1f))
			{
				g.DrawPath(borderPen, path);
			}
			using (SolidBrush textBrush = new SolidBrush(isHovered ? TextWhite : Color.FromArgb(210, 220, 235)))
			{
				StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
				g.DrawString(text, fontBadge, textBrush, rect, sf);
			}
		}
	}

	private static void DrawMvProgressBar(Graphics g, Rectangle rect, int value, int max, Color fillStart, Color fillEnd)
	{
		using (GraphicsPath bgPath = CreateRoundedRectangle(rect, 3))
		{
			using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(25, 32, 45)))
			{
				g.FillPath(bgBrush, bgPath);
			}
		}

		if (max <= 0 || value <= 0)
		{
			return;
		}
		float pct = Math.Min(1.0f, (float)value / max);
		int fillW = Math.Max(4, (int)(rect.Width * pct));
		Rectangle fillRect = new Rectangle(rect.X, rect.Y, fillW, rect.Height);

		using (GraphicsPath fillPath = CreateRoundedRectangle(fillRect, 3))
		{
			using (LinearGradientBrush lgb = new LinearGradientBrush(fillRect, fillStart, fillEnd, LinearGradientMode.Horizontal))
			{
				g.FillPath(lgb, fillPath);
			}
		}
	}

	private static GraphicsPath CreateRoundedRectangle(Rectangle r, int radius)
	{
		GraphicsPath path = new GraphicsPath();
		int d = radius * 2;
		path.AddArc(r.X, r.Y, d, d, 180, 90);
		path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
		path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
		path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
		path.CloseFigure();
		return path;
	}

	private static Color GetMvTierColor(int tier, bool isUmi = false)
	{
		if (isUmi)
		{
			return tier switch
			{
				4 => Color.FromArgb(232, 121, 249), // Tier 4 Fox Spirit Violet
				3 => Color.FromArgb(192, 132, 252), // Tier 3 Purple
				2 => Color.FromArgb(56, 189, 248),  // Tier 2 Sky Blue
				1 => Color.FromArgb(74, 222, 128),  // Tier 1 Emerald Green
				_ => Color.FromArgb(148, 163, 184)  // Tier 0 Slate Gray
			};
		}
		return tier switch
		{
			4 => Color.FromArgb(255, 215, 0),   // Tier 4 Gold (100% Umi)
			3 => Color.FromArgb(192, 132, 252), // Tier 3 Purple
			2 => Color.FromArgb(56, 189, 248),  // Tier 2 Sky Blue
			1 => Color.FromArgb(74, 222, 128),  // Tier 1 Emerald Green
			_ => Color.FromArgb(148, 163, 184)  // Tier 0 Slate Gray
		};
	}

	private static Color GetMvDancerColor(string dancer)
	{
		return dancer switch
		{
			"Miko" => Color.FromArgb(255, 130, 180),   // Cherry Blossom Pink (Sicken)
			"Genji" => Color.FromArgb(96, 165, 250),   // Water Blue (Silence)
			"Kaguya" => Color.FromArgb(245, 190, 40),  // Thunder Yellow (Pet Silence)
			"Umi" => Color.FromArgb(232, 121, 249),    // Fox Spirit Violet
			_ => Color.FromArgb(160, 175, 195)
		};
	}

	private void ShowDungeonHistoryMenu(Point location)
	{
		List<DpsTrackerMod.DungeonHistoryItem> dungeonHistoryList = DpsTrackerMod.GetDungeonHistoryList();
		if (dungeonHistoryList == null || dungeonHistoryList.Count == 0)
		{
			return;
		}
		if (dungeonHistoryMenu != null)
		{
			dungeonHistoryMenu.Dispose();
			dungeonHistoryMenu = null;
		}
		dungeonHistoryMenu = new ContextMenuStrip();
		dungeonHistoryMenu.Renderer = new ToolStripProfessionalRenderer(new DarkContextMenuColorTable());
		dungeonHistoryMenu.ShowImageMargin = false;
		dungeonHistoryMenu.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
		dungeonHistoryMenu.BackColor = Color.FromArgb(16, 20, 28);
		dungeonHistoryMenu.ForeColor = Color.FromArgb(220, 225, 235);
		dungeonHistoryMenu.MaximumSize = new Size(360, 420);
		ToolStripMenuItem value = new ToolStripMenuItem("── SELECT DUNGEON INSTANCE ──")
		{
			Enabled = false,
			ForeColor = Color.FromArgb(120, 140, 170),
			Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
		};
		dungeonHistoryMenu.Items.Add(value);
		foreach (DpsTrackerMod.DungeonHistoryItem item in dungeonHistoryList)
		{
			string text = (item.IsCurrent ? "● " : "   ");
			string text2;
			if (item.Index == -1)
			{
				text2 = $"{text}[LIVE] {item.DungeonName} ({item.TimeStr} | {item.Kills} kills | {item.PlayerCount}p)";
			}
			else
			{
				string text3 = FormatNumber(item.Damage);
				text2 = $"{text}{item.DungeonName} ({item.TimeStr} | {item.Kills} kills | {text3} dmg | {item.PlayerCount}p)";
			}
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text2);
			int capturedIndex = item.Index;
			if (item.IsCurrent)
			{
				toolStripMenuItem.ForeColor = Color.FromArgb(255, 215, 0);
				toolStripMenuItem.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
			}
			else if (item.Index == -1)
			{
				toolStripMenuItem.ForeColor = Color.FromArgb(74, 222, 128);
			}
			else
			{
				toolStripMenuItem.ForeColor = Color.FromArgb(220, 225, 235);
			}
			toolStripMenuItem.Click += delegate
			{
				DpsTrackerMod.SelectDungeonByIndex(capturedIndex);
				Invalidate();
			};
			dungeonHistoryMenu.Items.Add(toolStripMenuItem);
		}
		dungeonHistoryMenu.Show(this, location);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			updateTimer?.Stop();
			updateTimer?.Dispose();
			dungeonHistoryMenu?.Dispose();
			fontTitle?.Dispose();
			fontTab?.Dispose();
			fontHeader?.Dispose();
			fontRow?.Dispose();
			fontSmall?.Dispose();
			fontBadge?.Dispose();
			fontTiny?.Dispose();
			brushBg?.Dispose();
			brushHeaderBg?.Dispose();
			brushStatsBarBg?.Dispose();
			brushAccentCyan?.Dispose();
			brushGold?.Dispose();
			brushSilver?.Dispose();
			brushBronze?.Dispose();
			brushTextWhite?.Dispose();
			brushTextMuted?.Dispose();
			brushHpBg?.Dispose();
			brushTabActive?.Dispose();
			brushTabInactive?.Dispose();
			brushTableHeader?.Dispose();
			brushLocalPlayerRow?.Dispose();
			penBorder?.Dispose();
			penBorderThin?.Dispose();
			penAccentTab?.Dispose();
			penLocalHighlight?.Dispose();
			brushGuardGriefRow?.Dispose();
			brushGuardGriefText?.Dispose();
			penGuardGriefBorder?.Dispose();
			brushDeadText?.Dispose();
			brushNexusedText?.Dispose();
		}
		base.Dispose(disposing);
	}
}
