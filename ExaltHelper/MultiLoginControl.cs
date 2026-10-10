using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExaltHelper.Proxy;
using Microsoft.VisualBasic;

namespace ExaltHelper;

internal class MultiLoginControl : UserControl
{
	[CompilerGenerated]
	private sealed class AccountItemBinding
	{
		public string AccountGuid;

		internal bool MatchesAccountGuid(Account account)
		{
			return account.Label == AccountGuid;
		}
	}

	[CompilerGenerated]
	private sealed class AccountSteamBinding
	{
		public string SteamAccountGuid;

		internal bool MatchesSteamAccountGuid(Account account)
		{
			return account.Label == SteamAccountGuid;
		}
	}

	public static readonly Dictionary<string, Process> ActiveProcesses = new Dictionary<string, Process>();

	private IContainer components;

	private Button btnAdd;

	private Button btnLogin;

	private Label lblStatus;

	private Panel pnlAccounts;

	private LinkLabel btnAddSteam;

	private ListView lstAccounts;

	private ImageList iconList;

	private ContextMenuStrip cmsAccount;

	private ToolStripMenuItem tsmiLaunch;

	private ToolStripSeparator tssSeparator1;

	private ToolStripMenuItem tsmiEdit;

	private ToolStripMenuItem tsmiDelete;

	private ToolStripMenuItem tsmiCopyToken;

	private ToolStripMenuItem tsmiLaunchClient;

	private ToolStripSeparator tssSeparator2;

	private ToolStripMenuItem tsmiRefresh;

	public MultiLoginControl()
	{
		InitializeControlComponents();
		RefreshAccountList();
	}

	private void RefreshAccountList()
	{
		lstAccounts.Clear();
		if (Settings.Default.SavedAccountsEx != null && Settings.Default.SavedAccountsEx.Length != 0)
		{
			lstAccounts.Items.AddRange(Settings.Default.SavedAccountsEx.Select((Account account) => new ListViewItem(account.Label, account.Icon)
			{
				Tag = account,
				BackColor = lstAccounts.BackColor
			}).ToArray());
		}
	}

	private void btnAdd_Click(object sender, EventArgs e)
	{
		btnLogin.Enabled = lstAccounts.FocusedItem != null;
	}

	private void lstAccounts_MouseDown(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right && lstAccounts.FocusedItem != null && lstAccounts.FocusedItem.Bounds.Contains(e.Location))
		{
			cmsAccount.Show(Cursor.Position);
		}
	}

	private async void btnLogin_Click(object sender, EventArgs e)
	{
		var (text, password, AccountGuid) = await PromptAccountCredentials();
		if (!string.IsNullOrWhiteSpace(text) && (!string.IsNullOrEmpty(AccountGuid) || PromptAccountField("Label", "", out AccountGuid)))
		{
			if (Settings.Default.SavedAccountsEx.Any((Account account) => account.Label == AccountGuid))
			{
				Program.ShowWarning("An account with the same label has already been added!", base.ParentForm);
				return;
			}
			Settings.Default.SavedAccountsEx = Settings.Default.SavedAccountsEx.Concat(new Account[1]
			{
				new Account
				{
					Label = AccountGuid,
					Email = text,
					Password = password,
					Icon = 0
				}
			}).ToArray();
			Settings.Default.Save();
			RefreshAccountList();
			Program.ShowInfo("Successfully added account!", base.ParentForm);
		}
	}

	private void btnAddSteam_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		var (text, text2) = ParseAccountCredentials();
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && PromptAccountField("Label", "", out var SteamAccountGuid))
		{
			if (Settings.Default.SavedAccountsEx.Any((Account account) => account.Label == SteamAccountGuid))
			{
				Program.ShowWarning("An account with the same label has already been added!", base.ParentForm);
				return;
			}
			Settings.Default.SavedAccountsEx = Settings.Default.SavedAccountsEx.Concat(new Account[1]
			{
				new Account
				{
					Label = SteamAccountGuid,
					Email = text,
					Password = text2,
					Icon = 0
				}
			}).ToArray();
			Settings.Default.Save();
			RefreshAccountList();
			Program.ShowInfo("Successfully added account!", base.ParentForm);
		}
	}

	private void tsmiLaunch_Click(object sender, EventArgs e)
	{
		if (lstAccounts.FocusedItem != null)
		{
			Account account = lstAccounts.FocusedItem.Tag as Account;
			lstAccounts.Items.Remove(lstAccounts.FocusedItem);
			Settings.Default.SavedAccountsEx = Settings.Default.SavedAccountsEx.Except(new Account[1] { account }).ToArray();
			Settings.Default.Save();
		}
	}

	private async void tsmiEdit_Click(object sender, EventArgs e)
	{
		foreach (ListViewItem selectedItem in lstAccounts.SelectedItems)
		{
			Account account = selectedItem.Tag as Account;
			if (ActiveProcesses.TryGetValue(account.Email, out var value))
			{
				if (!value.HasExited)
				{
					Program.ShowError("Account already running: " + account.Label, base.ParentForm);
					break;
				}
				ActiveProcesses.Remove(account.Email);
			}
			string[] array = TokenHelper.CreateAccountToken(account.Email, account.Password);
			if (array.Length != 0 && array[0] == "<Error>CaptchaLock</Error>")
			{
				Program.ShowInfo("Account needs captcha to enter game, please complete the captcha, then close the launcher and ExaltHelper will launch the game.\n\n PRESS OK TO START LAUNCHER <<", base.ParentForm);
				RegistryHelper.ChangeLauncherUserPass(account.Email, account.Password);
				value = GameLauncher.Launch(array.Contains("steamworks") || array.Contains("kongregate"));
				if (value == null)
				{
					break;
				}
				value.WaitForExit();
				array = TokenHelper.CreateAccountToken(account.Email, account.Password);
			}
			if (array.Length != 4)
			{
				Program.ShowError("Error loading account: " + array[0], base.ParentForm);
				break;
			}
			Process process = ((!array[0].StartsWith("token|")) ? GameLauncher.LaunchDirect(account.Email, account.Password, array[0], array[1], array[2], array[3]) : GameLauncher.LaunchDirect(account.Label, "1", array[0], array[1], array[2], array[3]));
			if (process != null)
			{
				ActiveProcesses.Add(account.Email, process);
				await ((MainForm)base.Parent.Parent).CheckForUpdatesAsync();
				continue;
			}
			break;
		}
	}

	private async Task<(string, string, string)> PromptAccountCredentials(string defaultEmail = "", string defaultPassword = "")
	{
		string text = Interaction.InputBox("Please enter the Email for the RotMG Account:\n(Normal WEB accounts only)", "ExaltHelper", defaultEmail);
		if (string.IsNullOrEmpty(text))
		{
			return (string.Empty, string.Empty, string.Empty);
		}
		string[] array = text.Split(':');
		string text2;
		if (array.Length == 2)
		{
			text = array[0];
			text2 = array[1];
		}
		else
		{
			if (array.Length == 3 && int.TryParse(array[0], out var _))
			{
				string item = "token|" + array[1];
				string text3 = array[2];
				string verifyTokenUrl = "https://www.realmofthemadgod.com/account/verifyAccessTokenClient?game_net=Unity&play_platform=Unity&game_net_user_id=&clientToken=" + TokenHelper.Encode(array[1]) + "&accessToken=" + TokenHelper.Encode(text3);
				string text4 = await DownloadAccountResponseAsync(verifyTokenUrl);
				if (string.IsNullOrEmpty(text4) || !text4.Contains("<Success"))
				{
					Program.ShowError("RotMG Account invalid or expired!", base.ParentForm);
					return (string.Empty, string.Empty, string.Empty);
				}
				return (item, text3, array[0]);
			}
			text2 = Interaction.InputBox("Please enter the Password for the RotMG Account:", "ExaltHelper", defaultPassword);
			if (string.IsNullOrEmpty(text2))
			{
				return (string.Empty, string.Empty, string.Empty);
			}
		}
		string text5 = TokenHelper.Encode(text);
		string text6 = TokenHelper.Encode(text2);
		string verifyAccountUrl = $"https://www.realmofthemadgod.com/account/verify?guid={text5}&password={text6}&clientToken={TokenHelper.DeviceUniqueIdentifier}&ignore={Environment.TickCount}";
		string text7 = await DownloadAccountResponseAsync(verifyAccountUrl);
		if (string.IsNullOrEmpty(text7))
		{
			Program.ShowError("Unable to verify your RotMG Account!", base.ParentForm);
			return (string.Empty, string.Empty, string.Empty);
		}
		if (text7.Contains("<Account>") || text7.Contains("CaptchaLock"))
		{
			return (text, text2, string.Empty);
		}
		if (text7.Contains("<Error>LOGIN ATTEMPT LIMIT REACHED, please wait 5 minutes before re-trying!</Error>"))
		{
			Program.ShowError("You are IP rate limited for 5 minutes\nTry closing every RotMG instance, then wait for 5 minutes, and finally try adding the account again", base.ParentForm);
			return (string.Empty, string.Empty, string.Empty);
		}
		Program.ShowError("Account Credentials Invalid or account is in use!\n\nPlease try again with correct details while logged off in-game!", base.ParentForm);
		return (string.Empty, string.Empty, string.Empty);
	}

	private async Task<string> DownloadAccountResponseAsync(string url)
	{
		using WebClient webClient = new WebClient();
		try
		{
			return await webClient.DownloadStringTaskAsync(url);
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private (string, string) ParseAccountCredentials(string defaultGuid = "", string defaultSecret = "")
	{
		if (!Program.ShowQuestion("WARNING: This is for advanced users only.\nAccounts using this method are not verified - If you enter incorrect details, you will just be stock loading forever when you launch Exalt.\nAlso, you will not be able to purchase gold when in game, due to the client needing to be linked to Steam.\n\nContinue?", base.ParentForm))
		{
			return (string.Empty, string.Empty);
		}
		string text = Interaction.InputBox("Please enter the 'GUID' token for the RotMG Steam Account:\n", "ExaltHelper", defaultGuid);
		if (string.IsNullOrEmpty(text))
		{
			return (string.Empty, string.Empty);
		}
		string text2 = Interaction.InputBox("Please enter the 'SECRET' token for the RotMG Steam Account:", "ExaltHelper", defaultSecret);
		if (string.IsNullOrEmpty(text2))
		{
			return (string.Empty, string.Empty);
		}
		return (text, text2);
	}

	private bool PromptAccountField(string fieldName, string defaultValue, out string value)
	{
		value = Interaction.InputBox("Please enter the " + fieldName + " for the account:\n", "ExaltHelper", defaultValue);
		return !string.IsNullOrWhiteSpace(value);
	}

	private void tsmiDelete_Click(object sender, EventArgs e)
	{
		if (lstAccounts.FocusedItem != null)
		{
			Account account = lstAccounts.FocusedItem.Tag as Account;
			if (PromptAccountField("Label", account.Label, out var accountId))
			{
				account.Label = accountId;
				lstAccounts.FocusedItem.Text = accountId;
				Settings.Default.Save();
			}
		}
	}

	private void tsmiCopyToken_Click(object sender, EventArgs e)
	{
		if (lstAccounts.FocusedItem != null)
		{
			Account account = lstAccounts.FocusedItem.Tag as Account;
			if (PromptAccountField("Email", account.Email, out var accountId))
			{
				account.Email = accountId;
				Settings.Default.Save();
			}
		}
	}

	private void tsmiLaunchClient_Click(object sender, EventArgs e)
	{
		if (lstAccounts.FocusedItem != null)
		{
			Account account = lstAccounts.FocusedItem.Tag as Account;
			if (PromptAccountField("Password", account.Password, out var accountId))
			{
				account.Password = accountId;
				Settings.Default.Save();
			}
		}
	}

	private void tsmiRefresh_Click(object sender, EventArgs e)
	{
		throw new NotImplementedException();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeControlComponents()
	{
		components = new Container();
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(MultiLoginControl));
		btnAdd = new Button();
		btnLogin = new Button();
		lblStatus = new Label();
		pnlAccounts = new Panel();
		lstAccounts = new ListView();
		iconList = new ImageList(components);
		btnAddSteam = new LinkLabel();
		cmsAccount = new ContextMenuStrip(components);
		tsmiLaunch = new ToolStripMenuItem();
		tssSeparator1 = new ToolStripSeparator();
		tsmiEdit = new ToolStripMenuItem();
		tsmiDelete = new ToolStripMenuItem();
		tsmiCopyToken = new ToolStripMenuItem();
		tsmiLaunchClient = new ToolStripMenuItem();
		tssSeparator2 = new ToolStripSeparator();
		tsmiRefresh = new ToolStripMenuItem();
		pnlAccounts.SuspendLayout();
		cmsAccount.SuspendLayout();
		SuspendLayout();
		btnAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		btnAdd.BackColor = Color.FromArgb(0, 140, 215);
		btnAdd.FlatAppearance.BorderSize = 0;
		btnAdd.FlatStyle = FlatStyle.Flat;
		btnAdd.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnAdd.ForeColor = Color.White;
		btnAdd.Location = new Point(440, 264);
		btnAdd.Name = "btnAdd";
		btnAdd.Size = new Size(50, 25);
		btnAdd.TabIndex = 8;
		btnAdd.Text = "Add";
		btnAdd.UseVisualStyleBackColor = false;
		btnAdd.Click += btnLogin_Click;
		btnLogin.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		btnLogin.BackColor = Color.FromArgb(0, 140, 215);
		btnLogin.FlatAppearance.BorderSize = 0;
		btnLogin.FlatStyle = FlatStyle.Flat;
		btnLogin.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
		btnLogin.ForeColor = Color.White;
		btnLogin.Location = new Point(11, 264);
		btnLogin.Name = "btnLogin";
		btnLogin.Size = new Size(100, 25);
		btnLogin.TabIndex = 6;
		btnLogin.Text = "Login";
		btnLogin.UseVisualStyleBackColor = false;
		btnLogin.Click += tsmiEdit_Click;
		lblStatus.AutoSize = true;
		lblStatus.BackColor = Color.Transparent;
		lblStatus.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold);
		lblStatus.ForeColor = Color.Gold;
		lblStatus.Location = new Point(6, 7);
		lblStatus.Name = "lblStatus";
		lblStatus.Size = new Size(163, 25);
		lblStatus.TabIndex = 10;
		lblStatus.Text = "Exalt Multi Login";
		pnlAccounts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		pnlAccounts.Controls.Add(lstAccounts);
		pnlAccounts.Location = new Point(11, 35);
		pnlAccounts.Name = "pnlAccounts";
		pnlAccounts.Size = new Size(478, 224);
		pnlAccounts.TabIndex = 13;
		lstAccounts.BackColor = Color.FromArgb(10, 14, 23);
		lstAccounts.BorderStyle = BorderStyle.FixedSingle;
		lstAccounts.Dock = DockStyle.Fill;
		lstAccounts.ForeColor = Color.Gold;
		lstAccounts.GridLines = true;
		lstAccounts.HideSelection = false;
		lstAccounts.LargeImageList = iconList;
		lstAccounts.Location = new Point(0, 0);
		lstAccounts.MultiSelect = false;
		lstAccounts.Name = "lstAccounts";
		lstAccounts.Size = new Size(478, 224);
		lstAccounts.SmallImageList = iconList;
		lstAccounts.TabIndex = 0;
		lstAccounts.UseCompatibleStateImageBehavior = false;
		lstAccounts.SelectedIndexChanged += btnAdd_Click;
		lstAccounts.MouseClick += lstAccounts_MouseDown;
		iconList.ImageStream = (ImageListStreamer)componentResourceManager.GetObject("iconList.ImageStream");
		iconList.TransparentColor = Color.Transparent;
		iconList.Images.SetKeyName(0, "user_s_bordered.png");
		btnAddSteam.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnAddSteam.AutoSize = true;
		btnAddSteam.BackColor = Color.Transparent;
		btnAddSteam.LinkColor = Color.Gold;
		btnAddSteam.Location = new Point(327, 12);
		btnAddSteam.Name = "btnAddSteam";
		btnAddSteam.Size = new Size(162, 13);
		btnAddSteam.TabIndex = 12;
		btnAddSteam.TabStop = true;
		btnAddSteam.Text = "Advanced: Add Steam account";
		btnAddSteam.LinkClicked += btnAddSteam_LinkClicked;
		cmsAccount.Items.AddRange(new ToolStripItem[8] { tsmiLaunch, tssSeparator1, tsmiEdit, tsmiDelete, tsmiCopyToken, tsmiLaunchClient, tssSeparator2, tsmiRefresh });
		cmsAccount.Name = "cmsAccount";
		cmsAccount.Size = new Size(169, 148);
		tsmiLaunch.ForeColor = Color.DarkGreen;
		tsmiLaunch.Name = "btnCtxLogin";
		tsmiLaunch.Size = new Size(168, 22);
		tsmiLaunch.Text = "Login";
		tsmiLaunch.Click += tsmiEdit_Click;
		tssSeparator1.Name = "toolStripSeparator1";
		tssSeparator1.Size = new Size(165, 6);
		tsmiEdit.Name = "btnCtxChangeLabel";
		tsmiEdit.Size = new Size(168, 22);
		tsmiEdit.Text = "Change Label";
		tsmiEdit.Click += tsmiDelete_Click;
		tsmiDelete.Name = "btnCtxChangeEmail";
		tsmiDelete.Size = new Size(168, 22);
		tsmiDelete.Text = "Change Email";
		tsmiDelete.Click += tsmiCopyToken_Click;
		tsmiCopyToken.Name = "btnCtxChangePassword";
		tsmiCopyToken.Size = new Size(168, 22);
		tsmiCopyToken.Text = "Change Password";
		tsmiCopyToken.Click += tsmiLaunchClient_Click;
		tsmiLaunchClient.Name = "btnCtxChangeIcon";
		tsmiLaunchClient.Size = new Size(168, 22);
		tsmiLaunchClient.Text = "Change Icon";
		tsmiLaunchClient.Visible = false;
		tsmiLaunchClient.Click += tsmiRefresh_Click;
		tssSeparator2.Name = "toolStripSeparator2";
		tssSeparator2.Size = new Size(165, 6);
		tsmiRefresh.ForeColor = Color.DarkRed;
		tsmiRefresh.Name = "btnCtxDelete";
		tsmiRefresh.Size = new Size(168, 22);
		tsmiRefresh.Text = "Delete";
		tsmiRefresh.Click += tsmiLaunch_Click;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = Color.FromArgb(10, 14, 23);
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.Controls.Add(btnAddSteam);
		base.Controls.Add(pnlAccounts);
		base.Controls.Add(lblStatus);
		base.Controls.Add(btnAdd);
		base.Controls.Add(btnLogin);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		base.Name = "MultiLoginControl";
		base.Size = new Size(500, 300);
		pnlAccounts.ResumeLayout(performLayout: false);
		cmsAccount.ResumeLayout(performLayout: false);
		ResumeLayout(performLayout: false);
		PerformLayout();
	}

	[CompilerGenerated]
	private ListViewItem CreateAccountListViewItem(Account account)
	{
		return new ListViewItem(account.Label, account.Icon)
		{
			Tag = account,
			BackColor = lstAccounts.BackColor
		};
	}
}
