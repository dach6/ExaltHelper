using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal class MainForm : Form
{
	[CompilerGenerated]
	private sealed class MainFormDisplayBinding
	{
		public MainForm ParentForm;

		public bool IsRunning;

		public string StatusMessage;

		internal void UpdateStatusAction()
		{
			try
			{
				Program.LogInfo("core", "Initializing proxy...");
				GameData.LoadGameData();
				ServerManager.LoadServers();
				ParentForm.Proxy.StartProxy();
			}
			catch (Exception ex)
			{
				IsRunning = true;
				StatusMessage = ex.ToString();
			}
		}
	}

	[CompilerGenerated]
	private ProxyServer _proxyServer;

	private StartControl _startControl;

	private SettingsControl _settingsControl;

	private MultiLoginControl _multiLoginControl;

	private static bool _isUpdatePending;

	private int _retryCount;

	private static bool _updateFailed;

	private IContainer components;

	private ImageList imagesTabs;

	private Panel pnlContent;

	private Panel pnlStrip;

	private MenuStrip stripMain;

	private ToolStripMenuItem btnPlay;

	private ToolStripMenuItem btnSettings;

	private ToolStripMenuItem lblVersion;

	public ProxyServer Proxy
	{
		[CompilerGenerated]
		get
		{
			return _proxyServer;
		}
		[CompilerGenerated]
		private set
		{
			_proxyServer = value;
		}
	}

	public MainForm()
	{
		InitializeComponent();
		Text += " v" + Program.ToolVersion;
		ToolStripMenuItem toolStripMenuItem = lblVersion;
		toolStripMenuItem.Text = toolStripMenuItem.Text + " " + Program.GameVersion;
		Proxy = new ProxyServer();
	}

	public async Task<bool> CheckForUpdatesAsync()
	{
		await Task.CompletedTask;
		return true;
	}

	private (bool majorMinorChanged, bool patchChanged) CompareVersions(string currentVersion, string availableVersion)
	{
		string[] array = currentVersion.Split('.');
		string[] array2 = availableVersion.Split('.');
		if (array.Length != array2.Length)
		{
			return (majorMinorChanged: true, patchChanged: false);
		}
		if (array.Length != 3)
		{
			if (!(currentVersion != availableVersion))
			{
				return (majorMinorChanged: false, patchChanged: false);
			}
			return (majorMinorChanged: true, patchChanged: false);
		}
		bool flag = false;
		bool item = false;
		if (array[0] != array2[0] || array[1] != array2[1])
		{
			flag = true;
		}
		if (!flag && array[2] != array2[2])
		{
			item = true;
		}
		return (majorMinorChanged: flag, patchChanged: item);
	}

	private async void MainForm_Shown(object sender, EventArgs e)
	{
		try
		{
			await Task.Run((Func<Task<bool>>)CheckForUpdatesAsync);
		}
		catch (Exception ex)
		{
			Program.LogWarning("core", "Failed to check for update: " + ex.Message);
			if (!Program.ShowQuestion("Cannot connect to update server! Ignore and continue?\n\nUpdate checks will NOT work!", this))
			{
				GameLauncher.DeleteHook();
				Program.ShowError("Failed to check for updates!\n\nPlease try again in 5 minutes!\n\nMore details: " + ex.Message, this);
				Close();
				return;
			}
			_updateFailed = true;
		}
		bool IsRunning = false;
		string StatusMessage = string.Empty;
		await Task.Run(delegate
		{
			try
			{
				Program.LogInfo("core", "Initializing proxy...");
				GameData.LoadGameData();
				ServerManager.LoadServers();
				Proxy.StartProxy();
			}
			catch (Exception ex2)
			{
				IsRunning = true;
				StatusMessage = ex2.ToString();
			}
		});
		if (IsRunning)
		{
			Program.ShowError("Failed to initialize Proxy!\n\nPlease make sure:\n- Your AntiVirus is not blocking this program\n- There are no other Exalt tools are running\n- Loading the client normally works fine\n\nMore details: " + StatusMessage, this);
			Close();
			return;
		}
		DpsOverlayManager.Initialize();
		_startControl = new StartControl
		{
			Dock = DockStyle.Fill
		};
		pnlContent.Controls.Add(_startControl);
		_settingsControl = new SettingsControl
		{
			Dock = DockStyle.Fill
		};
		pnlContent.Controls.Add(_settingsControl);
		_multiLoginControl = new MultiLoginControl
		{
			Dock = DockStyle.Fill
		};
		pnlContent.Controls.Add(_multiLoginControl);
		pnlContent.BackgroundImage = null;
		_startControl.BringToFront();
		stripMain.Enabled = true;
		btnPlay.Select();
		base.Deactivate += MainForm_Deactivate;
	}

	private void MainForm_Deactivate(object sender, EventArgs e)
	{
		base.ActiveControl = null;
	}

	private void btnPlay_Click(object sender, EventArgs e)
	{
		_startControl.BringToFront();
	}

	private void btnSettings_Click(object sender, EventArgs e)
	{
		_settingsControl.BringToFront();
	}

	public void btnMultiLogin_Click(object sender, EventArgs e)
	{
		_multiLoginControl.BringToFront();
	}

	private void lblVersion_Click(object sender, EventArgs e)
	{
		Process.Start("https://github.com/");
	}

	private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (stripMain.Enabled)
		{
			e.Cancel = !Program.ShowQuestion("Are you sure you want to EXIT?", this);
		}
	}

	private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
	{
		try
		{
			DpsOverlayManager.Shutdown();
			GameLauncher.DeleteHook();
		}
		catch (Exception ex)
		{
			Console.WriteLine("Failed to delete hook on exit: " + ex);
		}
		Settings.Default.Save();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		components = new Container();
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(MainForm));
		imagesTabs = new ImageList(components);
		pnlContent = new Panel();
		pnlStrip = new Panel();
		stripMain = new MenuStrip();
		btnPlay = new ToolStripMenuItem();
		btnSettings = new ToolStripMenuItem();
		lblVersion = new ToolStripMenuItem();
		pnlStrip.SuspendLayout();
		stripMain.SuspendLayout();
		SuspendLayout();
		imagesTabs.ImageStream = (ImageListStreamer)componentResourceManager.GetObject("imagesTabs.ImageStream");
		imagesTabs.TransparentColor = Color.Transparent;
		imagesTabs.Images.SetKeyName(0, "");
		pnlContent.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		pnlContent.BackColor = Color.FromArgb(10, 14, 23);
		pnlContent.BackgroundImage = (Image)componentResourceManager.GetObject("pnlContent.BackgroundImage");
		pnlContent.BackgroundImageLayout = ImageLayout.Zoom;
		pnlContent.BorderStyle = BorderStyle.FixedSingle;
		pnlContent.Location = new Point(12, 57);
		pnlContent.Name = "pnlContent";
		pnlContent.Size = new Size(560, 301);
		pnlContent.TabIndex = 1;
		pnlStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		pnlStrip.BorderStyle = BorderStyle.FixedSingle;
		pnlStrip.Controls.Add(stripMain);
		pnlStrip.Location = new Point(12, 12);
		pnlStrip.Name = "pnlStrip";
		pnlStrip.Size = new Size(560, 39);
		pnlStrip.TabIndex = 2;
		stripMain.BackgroundImage = (Image)componentResourceManager.GetObject("stripMain.BackgroundImage");
		stripMain.Dock = DockStyle.Fill;
		stripMain.Enabled = false;
		stripMain.ImageScalingSize = new Size(24, 24);
		stripMain.Items.AddRange(new ToolStripItem[3] { btnPlay, btnSettings, lblVersion });
		stripMain.Location = new Point(0, 0);
		stripMain.Name = "stripMain";
		stripMain.Size = new Size(558, 37);
		stripMain.TabIndex = 2;
		stripMain.Text = "menuStrip1";
		btnPlay.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
		btnPlay.ForeColor = Color.Gold;
		btnPlay.Image = (Image)componentResourceManager.GetObject("btnAbout.Image");
		btnPlay.Name = "btnAbout";
		btnPlay.Size = new Size(74, 33);
		btnPlay.Text = "Play";
		btnPlay.TextAlign = ContentAlignment.MiddleRight;
		btnPlay.Click += btnPlay_Click;
		btnSettings.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
		btnSettings.ForeColor = Color.Gold;
		btnSettings.Image = (Image)componentResourceManager.GetObject("btnSettings.Image");
		btnSettings.Name = "btnSettings";
		btnSettings.Size = new Size(98, 33);
		btnSettings.Text = "Settings";
		btnSettings.Click += btnSettings_Click;
		lblVersion.Alignment = ToolStripItemAlignment.Right;
		lblVersion.ForeColor = Color.Gold;
		lblVersion.Image = (Image)componentResourceManager.GetObject("lblVersion.Image");
		lblVersion.Name = "lblVersion";
		lblVersion.Size = new Size(88, 33);
		lblVersion.Text = "For Exalt";
		lblVersion.Click += lblVersion_Click;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = Color.FromArgb(10, 14, 23);
		BackgroundImage = null;
		BackgroundImageLayout = ImageLayout.None;
		base.ClientSize = new Size(584, 370);
		base.Controls.Add(pnlStrip);
		base.Controls.Add(pnlContent);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.MaximizeBox = false;
		MinimumSize = new Size(600, 405);
		base.Name = "FrmExaltHelper";
		Text = "ExaltHelper";
		base.FormClosing += MainForm_FormClosing;
		base.FormClosed += MainForm_FormClosed;
		base.Shown += MainForm_Shown;
		pnlStrip.ResumeLayout(performLayout: false);
		pnlStrip.PerformLayout();
		stripMain.ResumeLayout(performLayout: false);
		stripMain.PerformLayout();
		ResumeLayout(performLayout: false);
	}
}
