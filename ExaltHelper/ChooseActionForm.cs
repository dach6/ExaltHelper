using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ExaltHelper;

internal class ChooseActionForm : Form
{
	[CompilerGenerated]
	private int _selectedAction;

	private IContainer components;

	private Label lblAction;

	private Button btnCancel;

	private Button btnSelect;

	private ComboBox cmbActions;

	public int SelectedAction
	{
		[CompilerGenerated]
		get
		{
			return _selectedAction;
		}
		[CompilerGenerated]
		private set
		{
			_selectedAction = value;
		}
	}

	public ChooseActionForm(string settingName)
	{
		InitializeFormComponents();
		Label label = lblAction;
		label.Text = label.Text + " " + settingName;
	}

	private void btnSelect_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
		if (cmbActions.Text == "Toggle")
		{
			SelectedAction = -1;
		}
		else if (cmbActions.Text == "Hold")
		{
			SelectedAction = -2;
		}
		Close();
	}

	private void btnCancel_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.Cancel;
		Close();
	}

	private void ChooseActionForm_Load(object sender, EventArgs e)
	{
		cmbActions.SelectedIndex = 0;
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
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(ChooseActionForm));
		lblAction = new Label();
		btnCancel = new Button();
		btnSelect = new Button();
		cmbActions = new ComboBox();
		SuspendLayout();
		lblAction.AutoSize = true;
		lblAction.BackColor = Color.Transparent;
		lblAction.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblAction.ForeColor = Color.Gold;
		lblAction.Location = new Point(12, 9);
		lblAction.Name = "lblHotkey";
		lblAction.Size = new Size(244, 25);
		lblAction.TabIndex = 0;
		lblAction.Text = "Choose a key press action:";
		btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		btnCancel.BackColor = Color.FromArgb(0, 140, 215);
		btnCancel.FlatAppearance.BorderSize = 0;
		btnCancel.FlatStyle = FlatStyle.Flat;
		btnCancel.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnCancel.ForeColor = Color.Gold;
		btnCancel.Location = new Point(17, 67);
		btnCancel.Name = "btnDone";
		btnCancel.Size = new Size(161, 25);
		btnCancel.TabIndex = 11;
		btnCancel.Text = "Done";
		btnCancel.UseVisualStyleBackColor = false;
		btnCancel.Click += btnSelect_Click;
		btnSelect.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		btnSelect.BackColor = Color.FromArgb(0, 140, 215);
		btnSelect.FlatAppearance.BorderSize = 0;
		btnSelect.FlatStyle = FlatStyle.Flat;
		btnSelect.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnSelect.ForeColor = Color.Gold;
		btnSelect.Location = new Point(184, 67);
		btnSelect.Name = "btnCancel";
		btnSelect.Size = new Size(116, 25);
		btnSelect.TabIndex = 12;
		btnSelect.Text = "Cancel";
		btnSelect.UseVisualStyleBackColor = false;
		btnSelect.Click += btnCancel_Click;
		cmbActions.DropDownStyle = ComboBoxStyle.DropDownList;
		cmbActions.FormattingEnabled = true;
		cmbActions.Items.AddRange(new object[2] { "Toggle", "Hold" });
		cmbActions.Location = new Point(17, 38);
		cmbActions.Name = "lstAction";
		cmbActions.Size = new Size(283, 21);
		cmbActions.TabIndex = 14;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(317, 110);
		base.Controls.Add(cmbActions);
		base.Controls.Add(btnSelect);
		base.Controls.Add(btnCancel);
		base.Controls.Add(lblAction);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.SizableToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmSettingAddHotkeyBool";
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		base.Load += ChooseActionForm_Load;
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
