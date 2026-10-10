using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ExaltHelper.Proxy;

namespace ExaltHelper;

internal class IgnoreEffectsForm : Form
{
	[CompilerGenerated]
	private sealed class EffectFilterBinding
	{
		public string EffectFilter;

		internal bool MatchesEffectName(string effectName)
		{
			return effectName != EffectFilter;
		}
	}

	private IContainer components;

	private Panel pnlEffects;

	private Label lblEffectsTitle;

	public IgnoreEffectsForm()
	{
		InitializeFormComponents();
		int num = 5;
		Type typeFromHandle = typeof(EffectType);
		string[] names = Enum.GetNames(typeFromHandle);
		foreach (string text in names)
		{
			FieldInfo field = typeFromHandle.GetField(text);
			CheckBox checkBox = new CheckBox();
			pnlEffects.Controls.Add(checkBox);
			checkBox.AutoSize = true;
			checkBox.Font = pnlEffects.Font;
			checkBox.Text = "Ignore '" + text + "'";
			checkBox.Tag = text;
			checkBox.Checked = Settings.Default.AntiLagIgnoredEffects.Contains(text);
			checkBox.Location = new Point(5, num);
			num += checkBox.Height + 5;
			if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute descriptionAttribute && !string.IsNullOrWhiteSpace(descriptionAttribute.Description))
			{
				checkBox.Text = checkBox.Text + " | " + descriptionAttribute.Description;
			}
			checkBox.CheckedChanged += IgnoreEffectsForm_Load;
		}
	}

	private void IgnoreEffectsForm_Load(object sender, EventArgs e)
	{
		if (!(sender is CheckBox checkBox))
		{
			return;
		}
		string EffectFilter = checkBox.Tag as string;
		if (checkBox.Checked)
		{
			Settings.Default.AntiLagIgnoredEffects = Settings.Default.AntiLagIgnoredEffects.Concat(new string[1] { EffectFilter }).ToArray();
		}
		else
		{
			Settings.Default.AntiLagIgnoredEffects = Settings.Default.AntiLagIgnoredEffects.Where((string text) => text != EffectFilter).ToArray();
		}
	}

	private void IgnoreEffectsForm_FormClosed(object sender, FormClosedEventArgs e)
	{
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

	private void InitializeFormComponents()
	{
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(IgnoreEffectsForm));
		pnlEffects = new Panel();
		lblEffectsTitle = new Label();
		SuspendLayout();
		pnlEffects.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		pnlEffects.AutoScroll = true;
		pnlEffects.BackColor = Color.FromArgb(34, 21, 42);
		pnlEffects.BorderStyle = BorderStyle.FixedSingle;
		pnlEffects.Font = new Font("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point, 0);
		pnlEffects.Location = new Point(17, 37);
		pnlEffects.Name = "pnlEffects";
		pnlEffects.Size = new Size(450, 330);
		pnlEffects.TabIndex = 16;
		lblEffectsTitle.AutoSize = true;
		lblEffectsTitle.BackColor = Color.Transparent;
		lblEffectsTitle.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblEffectsTitle.ForeColor = Color.Gold;
		lblEffectsTitle.Location = new Point(12, 9);
		lblEffectsTitle.Name = "lblEffects";
		lblEffectsTitle.Size = new Size(203, 25);
		lblEffectsTitle.TabIndex = 15;
		lblEffectsTitle.Text = "Ignored Game Effects";
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(484, 386);
		base.Controls.Add(pnlEffects);
		base.Controls.Add(lblEffectsTitle);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.SizableToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmSettingsIgnoredEffects";
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		base.FormClosed += IgnoreEffectsForm_FormClosed;
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
