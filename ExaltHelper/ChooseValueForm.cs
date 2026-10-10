using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ExaltHelper;

internal class ChooseValueForm : Form
{
	[CompilerGenerated]
	private int _selectedValue;

	private IContainer components;

	private Label lblPrompt;

	private Button btnCancel;

	private Button btnSelect;

	private NumericUpDown nudValue;

	public int SelectedValue
	{
		[CompilerGenerated]
		get
		{
			return _selectedValue;
		}
		[CompilerGenerated]
		private set
		{
			_selectedValue = value;
		}
	}

	public ChooseValueForm(string settingName, int minimum, int maximum)
	{
		InitializeFormComponents();
		Label label = lblPrompt;
		label.Text = label.Text + " " + settingName;
		nudValue.Minimum = minimum;
		nudValue.Maximum = maximum;
		nudValue.Value = minimum;
	}

	private void btnSelect_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
		SelectedValue = (int)nudValue.Value;
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
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(ChooseValueForm));
		lblPrompt = new Label();
		btnCancel = new Button();
		btnSelect = new Button();
		nudValue = new NumericUpDown();
		((ISupportInitialize)nudValue).BeginInit();
		SuspendLayout();
		lblPrompt.AutoSize = true;
		lblPrompt.BackColor = Color.Transparent;
		lblPrompt.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblPrompt.ForeColor = Color.Gold;
		lblPrompt.Location = new Point(12, 9);
		lblPrompt.Name = "lblHotkey";
		lblPrompt.Size = new Size(182, 25);
		lblPrompt.TabIndex = 0;
		lblPrompt.Text = "Choose a value for:";
		btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		btnCancel.BackColor = Color.FromArgb(0, 140, 215);
		btnCancel.FlatAppearance.BorderSize = 0;
		btnCancel.FlatStyle = FlatStyle.Flat;
		btnCancel.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnCancel.ForeColor = Color.Gold;
		btnCancel.Location = new Point(17, 67);
		btnCancel.Name = "btnDone";
		btnCancel.Size = new Size(328, 25);
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
		btnSelect.Location = new Point(351, 67);
		btnSelect.Name = "btnCancel";
		btnSelect.Size = new Size(116, 25);
		btnSelect.TabIndex = 12;
		btnSelect.Text = "Cancel";
		btnSelect.UseVisualStyleBackColor = false;
		btnSelect.Click += btnCancel_Click;
		nudValue.Location = new Point(17, 37);
		nudValue.Name = "numValue";
		nudValue.Size = new Size(450, 22);
		nudValue.TabIndex = 13;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(484, 110);
		base.Controls.Add(nudValue);
		base.Controls.Add(btnSelect);
		base.Controls.Add(btnCancel);
		base.Controls.Add(lblPrompt);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.SizableToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmSettingAddHotkeyInt";
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		((ISupportInitialize)nudValue).EndInit();
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
