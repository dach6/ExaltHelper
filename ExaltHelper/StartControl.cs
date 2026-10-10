using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ExaltHelper;

internal class StartControl : UserControl
{
	private IContainer components;

	private PictureBox picLogo;

	private PictureBox picBackground;

	private Panel pnlLaunch;

	private Label lblStart;

	private Panel pnlShop;

	private PictureBox picBanner;

	private Label lblLauncherStatus;

	private Button btnLaunch;

	private ComboBox lstLaunchers;

	private Label lblAccountStatus;

	private Label lblUpdateStatus;

	private Button btnShop;

	private ComboBox lstCategories;

	public StartControl()
	{
		InitializeControlComponents();
		lstLaunchers.SelectedIndex = Settings.Default.LastLauncherIndex;
	}

	private void btnLaunch_Click(object sender, EventArgs e)
	{
	}

	private async void btnShop_Click(object sender, EventArgs e)
	{
		await ((MainForm)base.Parent.Parent).CheckForUpdatesAsync();
		if (lstLaunchers.SelectedIndex == 0)
		{
			try
			{
				if (GameLauncher.Launch(useSteam: false) == null)
				{
					Program.ShowError("Launch Failed!", base.ParentForm);
				}
				return;
			}
			catch (Exception ex)
			{
				Program.ShowError("Launch Failed!\n" + ex, base.ParentForm);
				return;
			}
		}
		if (lstLaunchers.SelectedIndex == 1)
		{
			try
			{
				if (GameLauncher.Launch(useSteam: true) == null)
				{
					Program.ShowError("Launch Failed!", base.ParentForm);
				}
				return;
			}
			catch (Exception ex2)
			{
				Program.ShowError("Launch Failed!\n" + ex2, base.ParentForm);
				return;
			}
		}
		if (lstLaunchers.SelectedIndex == 2)
		{
			(base.ParentForm as MainForm).btnMultiLogin_Click(this, EventArgs.Empty);
		}
		else if (lstLaunchers.SelectedIndex == 3)
		{
			try
			{
				GameLauncher.DeleteHook();
			}
			catch (Exception ex3)
			{
				Program.LogWarning("CORE", "Failed to delete hook: " + ex3);
			}
			Settings.Default.ExaltLauncherPath = string.Empty;
			Settings.Default.ExaltLauncherSteamPath = string.Empty;
			Program.ShowInfo("Your paths have been reset!", base.ParentForm);
		}
	}

	private void lstCategories_SelectedIndexChanged(object sender, EventArgs e)
	{
	}

	private void StartControl_Load(object sender, EventArgs e)
	{
		if (lstLaunchers.SelectedIndex <= 2)
		{
			Settings.Default.LastLauncherIndex = lstLaunchers.SelectedIndex;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		if (pnlLaunch != null && picLogo != null)
		{
			pnlLaunch.Left = (base.Width - pnlLaunch.Width) / 2;
			int num = base.Height - picLogo.Bottom;
			if (num > pnlLaunch.Height)
			{
				pnlLaunch.Top = picLogo.Bottom + (num - pnlLaunch.Height) / 2;
			}
		}
	}

	private void InitializeControlComponents()
	{
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(StartControl));
		picLogo = new PictureBox();
		picBackground = new PictureBox();
		pnlLaunch = new Panel();
		lblAccountStatus = new Label();
		btnLaunch = new Button();
		lstLaunchers = new ComboBox();
		lblStart = new Label();
		pnlShop = new Panel();
		lblUpdateStatus = new Label();
		btnShop = new Button();
		lstCategories = new ComboBox();
		picBanner = new PictureBox();
		lblLauncherStatus = new Label();
		((ISupportInitialize)picLogo).BeginInit();
		((ISupportInitialize)picBackground).BeginInit();
		pnlLaunch.SuspendLayout();
		pnlShop.SuspendLayout();
		((ISupportInitialize)picBanner).BeginInit();
		SuspendLayout();
		picLogo.BackColor = Color.Transparent;
		picLogo.Cursor = Cursors.Default;
		picLogo.Image = (Image)componentResourceManager.GetObject("pbxLogo.Image");
		picLogo.Location = new Point(0, 18);
		picLogo.Name = "pbxLogo";
		picLogo.Size = new Size(560, 120);
		picLogo.SizeMode = PictureBoxSizeMode.Zoom;
		picLogo.TabIndex = 1;
		picLogo.TabStop = false;
		picBackground.Image = (Image)componentResourceManager.GetObject("pbxClientsConnectedIcon.Image");
		picBackground.Location = new Point(8, 10);
		picBackground.Name = "pbxClientsConnectedIcon";
		picBackground.Size = new Size(64, 64);
		picBackground.SizeMode = PictureBoxSizeMode.Zoom;
		picBackground.TabIndex = 7;
		picBackground.TabStop = false;
		pnlLaunch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		pnlLaunch.BackColor = Color.FromArgb(18, 26, 40);
		pnlLaunch.BorderStyle = BorderStyle.FixedSingle;
		pnlLaunch.Controls.Add(lblAccountStatus);
		pnlLaunch.Controls.Add(btnLaunch);
		pnlLaunch.Controls.Add(lstLaunchers);
		pnlLaunch.Controls.Add(picBackground);
		pnlLaunch.Controls.Add(lblStart);
		pnlLaunch.Font = new Font("Segoe UI", 8.25f);
		pnlLaunch.Location = new Point(8, 168);
		pnlLaunch.Name = "pnlLaunch";
		pnlLaunch.Size = new Size(544, 84);
		pnlLaunch.TabIndex = 17;
		lblAccountStatus.AutoSize = true;
		lblAccountStatus.BackColor = Color.Transparent;
		lblAccountStatus.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
		lblAccountStatus.ForeColor = Color.Gold;
		lblAccountStatus.Location = new Point(80, 62);
		lblAccountStatus.Name = "label3";
		lblAccountStatus.RightToLeft = RightToLeft.No;
		lblAccountStatus.Size = new Size(219, 13);
		lblAccountStatus.TabIndex = 20;
		lblAccountStatus.Text = "Start by launching the game via this menu";
		btnLaunch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnLaunch.BackColor = Color.FromArgb(0, 140, 215);
		btnLaunch.Cursor = Cursors.Hand;
		btnLaunch.FlatStyle = FlatStyle.Flat;
		btnLaunch.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
		btnLaunch.ForeColor = Color.White;
		btnLaunch.Location = new Point(430, 29);
		btnLaunch.Name = "btnLaunch";
		btnLaunch.Size = new Size(104, 28);
		btnLaunch.TabIndex = 19;
		btnLaunch.Text = "Launch";
		btnLaunch.TextAlign = ContentAlignment.MiddleCenter;
		btnLaunch.UseVisualStyleBackColor = false;
		btnLaunch.Click += btnShop_Click;
		lstLaunchers.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		lstLaunchers.DropDownStyle = ComboBoxStyle.DropDownList;
		lstLaunchers.Font = new Font("Segoe UI", 10f);
		lstLaunchers.FormattingEnabled = true;
		lstLaunchers.Items.AddRange(new object[4] { "Official Exalt Launcher", "Steam Exalt Launcher", "Multi Login Launcher", "Reset Launcher Locations" });
		lstLaunchers.Location = new Point(80, 30);
		lstLaunchers.Name = "lstLaunchers";
		lstLaunchers.Size = new Size(342, 26);
		lstLaunchers.TabIndex = 18;
		lblStart.AutoSize = true;
		lblStart.BackColor = Color.Transparent;
		lblStart.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblStart.ForeColor = Color.Gold;
		lblStart.Location = new Point(78, 6);
		lblStart.Name = "lblStart";
		lblStart.RightToLeft = RightToLeft.No;
		lblStart.Size = new Size(118, 21);
		lblStart.TabIndex = 15;
		lblStart.Text = "Start ExaltHelper";
		pnlShop.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		pnlShop.BackColor = Color.FromArgb(18, 26, 40);
		pnlShop.BorderStyle = BorderStyle.FixedSingle;
		pnlShop.Controls.Add(lblUpdateStatus);
		pnlShop.Controls.Add(btnShop);
		pnlShop.Controls.Add(lstCategories);
		pnlShop.Controls.Add(picBanner);
		pnlShop.Controls.Add(lblLauncherStatus);
		pnlShop.Font = new Font("Segoe UI", 8.25f);
		pnlShop.Location = new Point(3, 204);
		pnlShop.Name = "panel1";
		pnlShop.Size = new Size(554, 74);
		pnlShop.TabIndex = 20;
		lblUpdateStatus.AutoSize = true;
		lblUpdateStatus.BackColor = Color.Transparent;
		lblUpdateStatus.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
		lblUpdateStatus.ForeColor = Color.Gold;
		lblUpdateStatus.Location = new Point(71, 54);
		lblUpdateStatus.Name = "label5";
		lblUpdateStatus.RightToLeft = RightToLeft.No;
		lblUpdateStatus.Size = new Size(145, 13);
		lblUpdateStatus.TabIndex = 23;
		lblUpdateStatus.Text = "Choose a product category";
		btnShop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnShop.BackColor = Color.FromArgb(0, 140, 215);
		btnShop.FlatStyle = FlatStyle.Flat;
		btnShop.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
		btnShop.ForeColor = Color.FromArgb(212, 212, 212);
		btnShop.Location = new Point(452, 26);
		btnShop.Name = "btnShop";
		btnShop.Size = new Size(95, 25);
		btnShop.TabIndex = 22;
		btnShop.Text = "Shop";
		btnShop.TextAlign = ContentAlignment.TopCenter;
		btnShop.UseVisualStyleBackColor = false;
		btnShop.Click += lstCategories_SelectedIndexChanged;
		lstCategories.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		lstCategories.DropDownStyle = ComboBoxStyle.DropDownList;
		lstCategories.Font = new Font("Segoe UI", 10f);
		lstCategories.FormattingEnabled = true;
		lstCategories.Location = new Point(74, 26);
		lstCategories.Name = "lstCollections";
		lstCategories.Size = new Size(372, 25);
		lstCategories.TabIndex = 21;
		picBanner.Image = (Image)componentResourceManager.GetObject("pictureBox1.Image");
		picBanner.Location = new Point(4, 4);
		picBanner.Name = "pictureBox1";
		picBanner.Size = new Size(64, 64);
		picBanner.SizeMode = PictureBoxSizeMode.Zoom;
		picBanner.TabIndex = 7;
		picBanner.TabStop = false;
		lblLauncherStatus.AutoSize = true;
		lblLauncherStatus.BackColor = Color.Transparent;
		lblLauncherStatus.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblLauncherStatus.ForeColor = Color.Gold;
		lblLauncherStatus.Location = new Point(70, 2);
		lblLauncherStatus.Name = "label4";
		lblLauncherStatus.RightToLeft = RightToLeft.No;
		lblLauncherStatus.Size = new Size(106, 21);
		lblLauncherStatus.TabIndex = 15;
		lblLauncherStatus.Text = "RotMG Store";
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.Controls.Add(pnlLaunch);
		base.Controls.Add(picLogo);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		base.Name = "StartControl";
		base.Size = new Size(560, 301);
		((ISupportInitialize)picLogo).EndInit();
		((ISupportInitialize)picBackground).EndInit();
		pnlLaunch.ResumeLayout(performLayout: false);
		pnlLaunch.PerformLayout();
		pnlShop.ResumeLayout(performLayout: false);
		pnlShop.PerformLayout();
		((ISupportInitialize)picBanner).EndInit();
		ResumeLayout(performLayout: false);
	}
}
