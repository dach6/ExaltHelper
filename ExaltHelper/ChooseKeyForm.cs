using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ExaltHelper;

internal class ChooseKeyForm : Form
{
	[CompilerGenerated]
	private Keys _selectedKey;

	private IContainer components;

	private Label lblPrompt;

	private Button btnSelect;

	private Button btnCancel;

	private TextBox txtKey;

	public Keys SelectedKey
	{
		[CompilerGenerated]
		get
		{
			return _selectedKey;
		}
		[CompilerGenerated]
		private set
		{
			_selectedKey = value;
		}
	}

	public ChooseKeyForm(string settingName)
	{
		InitializeFormComponents();
		Label label = lblPrompt;
		label.Text = label.Text + " " + settingName;
		txtKey.Select();
	}

	private void txtKey_KeyDown(object sender, KeyEventArgs e)
	{
		TextBox textBox = txtKey;
		Keys result = e.KeyCode;
		textBox.Text = result.ToString();
		btnSelect.Enabled = Enum.TryParse<Keys>(txtKey.Text, out result);
	}

	private void btnSelect_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
		SelectedKey = (Keys)Enum.Parse(typeof(Keys), txtKey.Text);
		Close();
	}

	private void btnCancel_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.Cancel;
		Close();
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
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(ChooseKeyForm));
		lblPrompt = new Label();
		btnSelect = new Button();
		btnCancel = new Button();
		txtKey = new TextBox();
		SuspendLayout();
		lblPrompt.AutoSize = true;
		lblPrompt.BackColor = Color.Transparent;
		lblPrompt.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblPrompt.ForeColor = Color.Gold;
		lblPrompt.Location = new Point(12, 9);
		lblPrompt.Name = "lblHotkey";
		lblPrompt.Size = new Size(133, 25);
		lblPrompt.TabIndex = 0;
		lblPrompt.Text = "Choose a key:";
		btnSelect.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		btnSelect.BackColor = Color.FromArgb(0, 140, 215);
		btnSelect.Enabled = false;
		btnSelect.FlatAppearance.BorderSize = 0;
		btnSelect.FlatStyle = FlatStyle.Flat;
		btnSelect.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnSelect.ForeColor = Color.Gold;
		btnSelect.Location = new Point(17, 65);
		btnSelect.Name = "btnDone";
		btnSelect.Size = new Size(328, 25);
		btnSelect.TabIndex = 11;
		btnSelect.Text = "Done";
		btnSelect.UseVisualStyleBackColor = false;
		btnSelect.Click += btnSelect_Click;
		btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		btnCancel.BackColor = Color.FromArgb(0, 140, 215);
		btnCancel.FlatAppearance.BorderSize = 0;
		btnCancel.FlatStyle = FlatStyle.Flat;
		btnCancel.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnCancel.ForeColor = Color.Gold;
		btnCancel.Location = new Point(351, 65);
		btnCancel.Name = "btnCancel";
		btnCancel.Size = new Size(116, 25);
		btnCancel.TabIndex = 12;
		btnCancel.Text = "Cancel";
		btnCancel.UseVisualStyleBackColor = false;
		btnCancel.Click += btnCancel_Click;
		txtKey.Location = new Point(17, 37);
		txtKey.Name = "tbxHotkey";
		txtKey.ReadOnly = true;
		txtKey.Size = new Size(450, 22);
		txtKey.TabIndex = 13;
		txtKey.Text = "<press a key>";
		txtKey.KeyUp += txtKey_KeyDown;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(484, 108);
		base.Controls.Add(txtKey);
		base.Controls.Add(btnCancel);
		base.Controls.Add(btnSelect);
		base.Controls.Add(lblPrompt);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.SizableToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmSettingsAddHotkey";
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
