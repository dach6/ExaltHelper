using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ExaltHelper;

internal class LocateLauncherForm : Form
{
	[CompilerGenerated]
	private bool _isSteam;

	[CompilerGenerated]
	private bool _isCustom;

	private IContainer components;

	private Label lblPath;

	private TextBox txtPath;

	private Button btnBrowse;

	private Label lblStatus;

	public bool IsSteam
	{
		[CompilerGenerated]
		get
		{
			return _isSteam;
		}
		[CompilerGenerated]
		private set
		{
			_isSteam = value;
		}
	}

	public bool IsCustom
	{
		[CompilerGenerated]
		get
		{
			return _isCustom;
		}
		[CompilerGenerated]
		private set
		{
			_isCustom = value;
		}
	}

	public LocateLauncherForm(bool useSteam)
	{
		IsCustom = false;
		IsSteam = useSteam;
		InitializeFormComponents();
		if (IsSteam && !File.Exists(Settings.Default.ExaltLauncherSteamPath))
		{
			Settings.Default.ExaltLauncherSteamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam\\steamapps\\common\\Realm of the Mad God\\RotMG Exalt Launcher.exe");
		}
		else if (!IsSteam && !File.Exists(Settings.Default.ExaltLauncherPath))
		{
			Settings.Default.ExaltLauncherPath = "C:\\Program Files\\RotMG Exalt Launcher\\RotMG Exalt Launcher.exe";
		}
	}

	private void btnBrowse_Click(object sender, EventArgs e)
	{
		string text = "RotMG Exalt Launcher";
		Program.ShowInfo(IsSteam ? ("Please locate the STEAM " + text + " file where you installed it so that we can start RotMG.\n\nDefault location is similar to: " + Settings.Default.ExaltLauncherSteamPath) : ("Please locate the " + text + " file where you installed it so that we can start RotMG.\n\nDefault location is similar to: " + Settings.Default.ExaltLauncherPath), this);
		using OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Locate " + text + " file...",
			Filter = text + " (*.exe)|*.exe",
			InitialDirectory = Path.GetDirectoryName(IsSteam ? Settings.Default.ExaltLauncherSteamPath : Settings.Default.ExaltLauncherPath)
		};
		if (openFileDialog.ShowDialog() != DialogResult.OK)
		{
			return;
		}
		if (openFileDialog.FileName.EndsWith(text + ".exe"))
		{
			if (IsSteam)
			{
				Settings.Default.ExaltLauncherSteamPath = openFileDialog.FileName;
			}
			else
			{
				Settings.Default.ExaltLauncherPath = openFileDialog.FileName;
			}
			txtPath.Text = openFileDialog.FileName;
			Settings.Default.Save();
			IsCustom = true;
			Close();
		}
		else
		{
			Program.ShowWarning("Incorrect file!\nThe correct launcher file is named: " + text + ".exe", this);
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

	private void InitializeFormComponents()
	{
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(LocateLauncherForm));
		lblPath = new Label();
		txtPath = new TextBox();
		btnBrowse = new Button();
		lblStatus = new Label();
		SuspendLayout();
		lblPath.AutoSize = true;
		lblPath.BackColor = Color.Transparent;
		lblPath.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblPath.ForeColor = Color.Gold;
		lblPath.Location = new Point(12, 9);
		lblPath.Name = "lblTitle";
		lblPath.Size = new Size(205, 25);
		lblPath.TabIndex = 0;
		lblPath.Text = "Locate Exalt Launcher";
		txtPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		txtPath.BackColor = Color.FromArgb(10, 14, 23);
		txtPath.BorderStyle = BorderStyle.FixedSingle;
		txtPath.ForeColor = Color.Gold;
		txtPath.Location = new Point(17, 37);
		txtPath.Name = "tbxPath";
		txtPath.ReadOnly = true;
		txtPath.Size = new Size(316, 22);
		txtPath.TabIndex = 13;
		txtPath.Text = "...";
		btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnBrowse.BackColor = Color.FromArgb(10, 14, 23);
		btnBrowse.FlatAppearance.BorderColor = Color.Gray;
		btnBrowse.FlatStyle = FlatStyle.Flat;
		btnBrowse.Font = new Font("Segoe UI", 8.25f);
		btnBrowse.ForeColor = Color.Gold;
		btnBrowse.Location = new Point(339, 37);
		btnBrowse.Name = "btnBrowse";
		btnBrowse.Size = new Size(128, 22);
		btnBrowse.TabIndex = 16;
		btnBrowse.Text = "Browse...";
		btnBrowse.UseVisualStyleBackColor = false;
		btnBrowse.Click += btnBrowse_Click;
		lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		lblStatus.AutoSize = true;
		lblStatus.BackColor = Color.Transparent;
		lblStatus.Font = new Font("Segoe UI", 8.25f);
		lblStatus.ForeColor = Color.Gold;
		lblStatus.Location = new Point(14, 71);
		lblStatus.Name = "lblInfo";
		lblStatus.RightToLeft = RightToLeft.No;
		lblStatus.Size = new Size(264, 13);
		lblStatus.TabIndex = 17;
		lblStatus.Text = "Exalt Launcher installs to Program Files by default.";
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(484, 105);
		base.Controls.Add(lblStatus);
		base.Controls.Add(btnBrowse);
		base.Controls.Add(txtPath);
		base.Controls.Add(lblPath);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.FixedToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmLocateLauncher";
		base.ShowIcon = false;
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		base.TopMost = true;
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
