using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ExaltHelper.Proxy;

namespace ExaltHelper;

internal class HotkeysSettingsForm : Form
{
	[CompilerGenerated]
	private sealed class HotkeyItemBinding
	{
		public string SettingName;

		public ChooseKeyForm KeyChooser;

		internal bool MatchesSetting(HotkeyHandler hotkey)
		{
			if (hotkey.Setting == SettingName)
			{
				return hotkey.Result <= -1;
			}
			return false;
		}

		internal bool MatchesKey(HotkeyHandler hotkey)
		{
			return hotkey.Key == KeyChooser.SelectedKey;
		}
	}

	[CompilerGenerated]
	private sealed class HotkeyHandlerBinding
	{
		public HotkeyHandler Handler;

		internal bool MatchesHandler(HotkeyHandler hotkey)
		{
			return hotkey.Key == Handler.Key;
		}
	}

	private readonly Dictionary<string, decimal> _numericValues = new Dictionary<string, decimal>();

	private readonly Dictionary<string, decimal> _defaultValues = new Dictionary<string, decimal>();

	private IContainer components;

	private Label lblHotkeys;

	private Panel pnlSettings;

	private ListBox lstSettings;

	private Button btnAdd;

	private Panel pnlHotkeys;

	private ListBox lstHotkeys;

	private Button btnReset;

	private Label lblInfo;

	public static Point GetControlScreenPosition(Control control2)
	{
		Form form = control2.FindForm();
		if (form == null)
		{
			return new Point(0, 0);
		}
		Control control = control2.Parent;
		Point location = control2.Location;
		while (control != null)
		{
			location.X += control.Left;
			location.Y += control.Top;
			control = control.Parent;
		}
		location.X -= form.Left;
		location.Y -= form.Top;
		return location;
	}

	public HotkeysSettingsForm(SettingsControl settings)
	{
		InitializeFormComponents();
		foreach (KeyValuePair<string, Control> item in from positionedControl in settings.SettingsControls.Select(delegate(KeyValuePair<string, Control> entry)
			{
				KeyValuePair<string, Control> keyValuePair = entry;
				return (pair: entry, GetControlScreenPosition(keyValuePair.Value));
			}).OrderBy(delegate((KeyValuePair<string, Control> pair, Point) entry)
			{
				(KeyValuePair<string, Control>, Point) tuple = entry;
				return GetControlScreenPosition(tuple.Item1.Value).Y;
			}).ThenBy(delegate((KeyValuePair<string, Control> pair, Point) entry)
			{
				(KeyValuePair<string, Control>, Point) tuple = entry;
				return GetControlScreenPosition(tuple.Item1.Value).X;
			})
			select positionedControl.pair)
		{
			if (item.Value is NumericUpDown numericUpDown)
			{
				_numericValues[item.Key] = numericUpDown.Minimum;
				_defaultValues[item.Key] = numericUpDown.Maximum;
			}
			lstSettings.Items.Add(item.Key);
		}
		lstHotkeys.Items.AddRange(Settings.Default.Hotkeys.Cast<object>().ToArray());
	}

	private void btnReset_Click(object sender, EventArgs e)
	{
		btnAdd.Enabled = lstSettings.SelectedIndex > -1;
	}

	private void lstHotkeys_SelectedIndexChanged(object sender, EventArgs e)
	{
		btnReset.Enabled = lstHotkeys.SelectedIndex > -1;
	}

	private void lstSettings_SelectedIndexChanged(object sender, EventArgs e)
	{
		string SettingName = lstSettings.SelectedItem.ToString();
		ChooseKeyForm KeyChooser = new ChooseKeyForm(SettingName);
		if (KeyChooser.ShowDialog() != DialogResult.OK || KeyChooser.SelectedKey == Keys.None || Settings.Default.Hotkeys.Any((HotkeyHandler hotkeyHandler2) => hotkeyHandler2.Setting == SettingName && hotkeyHandler2.Result <= -1) || Settings.Default.Hotkeys.Any((HotkeyHandler hotkeyHandler2) => hotkeyHandler2.Key == KeyChooser.SelectedKey))
		{
			return;
		}
		int result = -1;
		Type fieldType = typeof(Settings).GetField(SettingName).FieldType;
		if (fieldType == typeof(int) || fieldType == typeof(double))
		{
			ChooseValueForm chooseValueForm = new ChooseValueForm(SettingName, (int)_numericValues[SettingName], (int)_defaultValues[SettingName]);
			if (chooseValueForm.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			result = chooseValueForm.SelectedValue;
		}
		else if (fieldType == typeof(bool))
		{
			ChooseActionForm chooseActionForm = new ChooseActionForm(SettingName);
			if (chooseActionForm.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			result = chooseActionForm.SelectedAction;
		}
		HotkeyHandler hotkeyHandler = new HotkeyHandler
		{
			Key = KeyChooser.SelectedKey,
			Setting = SettingName,
			Result = result
		};
		lstHotkeys.Items.Add(hotkeyHandler);
		Settings.Default.Hotkeys = Settings.Default.Hotkeys.Concat(new HotkeyHandler[1] { hotkeyHandler }).ToArray();
		Settings.Default.Change();
		if (SettingName == "EnableSlowWalk")
		{
			RegistryHelper.UpdateSlowWalk(hotkeyHandler);
		}
	}

	private void HotkeysSettingsForm_Load(object sender, EventArgs e)
	{
		HotkeyHandler Handler = lstHotkeys.SelectedItem as HotkeyHandler;
		if (Handler != null)
		{
			lstHotkeys.Items.Remove(Handler);
			Settings.Default.Hotkeys = Settings.Default.Hotkeys.Except(Settings.Default.Hotkeys.Where((HotkeyHandler hotkeyHandler) => hotkeyHandler.Key == Handler.Key)).ToArray();
			Settings.Default.Change();
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
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(HotkeysSettingsForm));
		lblHotkeys = new Label();
		pnlSettings = new Panel();
		lstSettings = new ListBox();
		btnAdd = new Button();
		pnlHotkeys = new Panel();
		lstHotkeys = new ListBox();
		btnReset = new Button();
		lblInfo = new Label();
		pnlSettings.SuspendLayout();
		pnlHotkeys.SuspendLayout();
		SuspendLayout();
		lblHotkeys.AutoSize = true;
		lblHotkeys.BackColor = Color.Transparent;
		lblHotkeys.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblHotkeys.ForeColor = Color.Gold;
		lblHotkeys.Location = new Point(12, 9);
		lblHotkeys.Name = "lblHotkeys";
		lblHotkeys.Size = new Size(169, 25);
		lblHotkeys.TabIndex = 0;
		lblHotkeys.Text = "Available Settings";
		pnlSettings.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		pnlSettings.Controls.Add(lstSettings);
		pnlSettings.Location = new Point(17, 37);
		pnlSettings.Name = "pnlEnemies";
		pnlSettings.Size = new Size(195, 300);
		pnlSettings.TabIndex = 14;
		lstSettings.BackColor = Color.FromArgb(10, 14, 23);
		lstSettings.BorderStyle = BorderStyle.FixedSingle;
		lstSettings.Dock = DockStyle.Fill;
		lstSettings.ForeColor = Color.Gainsboro;
		lstSettings.FormattingEnabled = true;
		lstSettings.IntegralHeight = false;
		lstSettings.Location = new Point(0, 0);
		lstSettings.Name = "lstSettings";
		lstSettings.Size = new Size(195, 300);
		lstSettings.TabIndex = 8;
		lstSettings.SelectedIndexChanged += btnReset_Click;
		btnAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		btnAdd.BackColor = Color.FromArgb(0, 140, 215);
		btnAdd.Enabled = false;
		btnAdd.FlatAppearance.BorderSize = 0;
		btnAdd.FlatStyle = FlatStyle.Flat;
		btnAdd.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnAdd.ForeColor = Color.Gold;
		btnAdd.Location = new Point(17, 343);
		btnAdd.Name = "btnAdd";
		btnAdd.Size = new Size(195, 25);
		btnAdd.TabIndex = 11;
		btnAdd.Text = "Add Hotkey for Selected";
		btnAdd.UseVisualStyleBackColor = false;
		btnAdd.Click += lstSettings_SelectedIndexChanged;
		pnlHotkeys.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
		pnlHotkeys.Controls.Add(lstHotkeys);
		pnlHotkeys.Location = new Point(218, 37);
		pnlHotkeys.Name = "pnlIgnored";
		pnlHotkeys.Size = new Size(249, 300);
		pnlHotkeys.TabIndex = 15;
		lstHotkeys.BackColor = Color.FromArgb(10, 14, 23);
		lstHotkeys.BorderStyle = BorderStyle.FixedSingle;
		lstHotkeys.Dock = DockStyle.Fill;
		lstHotkeys.ForeColor = Color.Gainsboro;
		lstHotkeys.FormattingEnabled = true;
		lstHotkeys.IntegralHeight = false;
		lstHotkeys.Location = new Point(0, 0);
		lstHotkeys.Name = "lstHotkeys";
		lstHotkeys.Size = new Size(249, 300);
		lstHotkeys.TabIndex = 10;
		lstHotkeys.SelectedIndexChanged += lstHotkeys_SelectedIndexChanged;
		btnReset.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		btnReset.BackColor = Color.FromArgb(0, 140, 215);
		btnReset.Enabled = false;
		btnReset.FlatAppearance.BorderSize = 0;
		btnReset.FlatStyle = FlatStyle.Flat;
		btnReset.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnReset.ForeColor = Color.Gold;
		btnReset.Location = new Point(218, 343);
		btnReset.Name = "btnRemove";
		btnReset.Size = new Size(249, 25);
		btnReset.TabIndex = 12;
		btnReset.Text = "Remove Selected";
		btnReset.UseVisualStyleBackColor = false;
		btnReset.Click += HotkeysSettingsForm_Load;
		lblInfo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		lblInfo.AutoSize = true;
		lblInfo.BackColor = Color.Transparent;
		lblInfo.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblInfo.ForeColor = Color.Gold;
		lblInfo.Location = new Point(213, 9);
		lblInfo.Name = "lblIgnored";
		lblInfo.Size = new Size(149, 25);
		lblInfo.TabIndex = 9;
		lblInfo.Text = "Hotkeys Added";
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(484, 386);
		base.Controls.Add(pnlHotkeys);
		base.Controls.Add(pnlSettings);
		base.Controls.Add(btnReset);
		base.Controls.Add(btnAdd);
		base.Controls.Add(lblInfo);
		base.Controls.Add(lblHotkeys);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.SizableToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmSettingsHotkeys";
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		pnlSettings.ResumeLayout(performLayout: false);
		pnlHotkeys.ResumeLayout(performLayout: false);
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
