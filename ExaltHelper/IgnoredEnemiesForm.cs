using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal class IgnoredEnemiesForm : Form
{
	[CompilerGenerated]
	private sealed class EnemyFilterBinding
	{
		public string EnemyFilter;

		internal bool MatchesEnemyName(string enemyName)
		{
			if (!(EnemyFilter == ""))
			{
				return enemyName.ToLower().Contains(EnemyFilter);
			}
			return true;
		}
	}

	private IContainer components;

	private Label lblAllEnemies;

	private Panel pnlAllEnemies;

	private ListBox lstAllEnemies;

	private Button btnAdd;

	private Label lblIgnored;

	private Panel pnlIgnored;

	private ListBox lstIgnored;

	private TextBox tbxFilter;

	private Button btnRemoveAll;

	private Button btnRemove;

	public IgnoredEnemiesForm()
	{
		InitializeFormComponents();
		ListBox.ObjectCollection items = lstIgnored.Items;
		object[] array = (from objectType in Settings.Default.FameIngoredEnemies
			select GameData.Objects.GetById((ushort)objectType) into objectDefinition
			where objectDefinition.Enemy
			select objectDefinition.Name).ToArray();
		object[] items2 = array;
		items.AddRange(items2);
		FilterEnemiesList();
		lstAllEnemies.SelectedIndex = 0;
		lstIgnored.SelectedIndex = -1;
	}

	private void btnAdd_Click(object enemyName, EventArgs e)
	{
		tbxFilter.Clear();
	}

	private void btnRemove_Click(object enemyName, EventArgs e)
	{
		FilterEnemiesList(tbxFilter.Text);
	}

	private void FilterEnemiesList(string enemyName = "")
	{
		enemyName = enemyName.ToLower();
		lstAllEnemies.Items.Clear();
		ListBox.ObjectCollection items = lstAllEnemies.Items;
		object[] array = (from objectStructure in GameData.Objects.Map.Values
			where objectStructure.Enemy
			select objectStructure.Name into text
			where enemyName == "" || text.ToLower().Contains(enemyName)
			select text).ToArray();
		object[] items2 = array;
		items.AddRange(items2);
	}

	private void btnRemoveAll_Click(object enemyName, EventArgs e)
	{
		btnAdd.Enabled = lstAllEnemies.SelectedIndex > -1;
	}

	private void tbxFilter_TextChanged(object enemyName, EventArgs e)
	{
		btnRemove.Enabled = lstIgnored.SelectedIndex > -1;
	}

	private void lstAllEnemies_SelectedIndexChanged(object enemyName, EventArgs e)
	{
		if (!lstIgnored.Items.Contains(lstAllEnemies.SelectedItem))
		{
			lstIgnored.Items.Add(lstAllEnemies.SelectedItem);
			PopulateEnemyLists();
		}
	}

	private void lstIgnored_SelectedIndexChanged(object enemyName, EventArgs e)
	{
		lstIgnored.Items.Remove(lstIgnored.SelectedItem);
		PopulateEnemyLists();
	}

	private void PopulateEnemyLists()
	{
		Settings.Default.FameIngoredEnemies = lstIgnored.Items.Cast<string>().Select((Func<string, int>)((string enemyName) => GameData.Objects.GetByName(enemyName).ID)).ToArray();
		Settings.Default.Save();
	}

	private void IgnoredEnemiesForm_Load(object enemyName, EventArgs e)
	{
		lstIgnored.Items.Clear();
		PopulateEnemyLists();
	}

	protected override void Dispose(bool enemyName)
	{
		if (enemyName && components != null)
		{
			components.Dispose();
		}
		base.Dispose(enemyName);
	}

	private void InitializeFormComponents()
	{
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(IgnoredEnemiesForm));
		lblAllEnemies = new Label();
		pnlAllEnemies = new Panel();
		lstAllEnemies = new ListBox();
		btnAdd = new Button();
		lblIgnored = new Label();
		pnlIgnored = new Panel();
		lstIgnored = new ListBox();
		tbxFilter = new TextBox();
		btnRemoveAll = new Button();
		btnRemove = new Button();
		pnlAllEnemies.SuspendLayout();
		pnlIgnored.SuspendLayout();
		SuspendLayout();
		lblAllEnemies.AutoSize = true;
		lblAllEnemies.BackColor = Color.Transparent;
		lblAllEnemies.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblAllEnemies.ForeColor = Color.Gold;
		lblAllEnemies.Location = new Point(12, 9);
		lblAllEnemies.Name = "lblEnemies";
		lblAllEnemies.Size = new Size(112, 25);
		lblAllEnemies.TabIndex = 0;
		lblAllEnemies.Text = "All Enemies";
		pnlAllEnemies.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		pnlAllEnemies.Controls.Add(lstAllEnemies);
		pnlAllEnemies.Location = new Point(17, 37);
		pnlAllEnemies.Name = "pnlEnemies";
		pnlAllEnemies.Size = new Size(244, 272);
		pnlAllEnemies.TabIndex = 14;
		lstAllEnemies.BackColor = Color.FromArgb(10, 14, 23);
		lstAllEnemies.BorderStyle = BorderStyle.FixedSingle;
		lstAllEnemies.Dock = DockStyle.Fill;
		lstAllEnemies.ForeColor = Color.Gainsboro;
		lstAllEnemies.FormattingEnabled = true;
		lstAllEnemies.IntegralHeight = false;
		lstAllEnemies.Location = new Point(0, 0);
		lstAllEnemies.Name = "lstEnemies";
		lstAllEnemies.Size = new Size(244, 272);
		lstAllEnemies.TabIndex = 8;
		lstAllEnemies.SelectedIndexChanged += btnRemoveAll_Click;
		btnAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		btnAdd.BackColor = Color.FromArgb(0, 140, 215);
		btnAdd.Enabled = false;
		btnAdd.FlatAppearance.BorderSize = 0;
		btnAdd.FlatStyle = FlatStyle.Flat;
		btnAdd.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnAdd.ForeColor = Color.Gold;
		btnAdd.Location = new Point(17, 343);
		btnAdd.Name = "btnAdd";
		btnAdd.Size = new Size(244, 25);
		btnAdd.TabIndex = 11;
		btnAdd.Text = "Add Selected";
		btnAdd.UseVisualStyleBackColor = false;
		btnAdd.Click += lstAllEnemies_SelectedIndexChanged;
		lblIgnored.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		lblIgnored.AutoSize = true;
		lblIgnored.BackColor = Color.Transparent;
		lblIgnored.Font = new Font("Segoe UI", 14.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		lblIgnored.ForeColor = Color.Gold;
		lblIgnored.Location = new Point(262, 9);
		lblIgnored.Name = "lblIgnored";
		lblIgnored.Size = new Size(161, 25);
		lblIgnored.TabIndex = 9;
		lblIgnored.Text = "Ignored Enemies";
		pnlIgnored.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
		pnlIgnored.Controls.Add(lstIgnored);
		pnlIgnored.Location = new Point(267, 37);
		pnlIgnored.Name = "pnlIgnored";
		pnlIgnored.Size = new Size(200, 272);
		pnlIgnored.TabIndex = 15;
		lstIgnored.BackColor = Color.FromArgb(10, 14, 23);
		lstIgnored.BorderStyle = BorderStyle.FixedSingle;
		lstIgnored.Dock = DockStyle.Fill;
		lstIgnored.ForeColor = Color.Gainsboro;
		lstIgnored.FormattingEnabled = true;
		lstIgnored.IntegralHeight = false;
		lstIgnored.Location = new Point(0, 0);
		lstIgnored.Name = "lstIgnored";
		lstIgnored.Size = new Size(200, 272);
		lstIgnored.TabIndex = 10;
		lstIgnored.SelectedIndexChanged += tbxFilter_TextChanged;
		tbxFilter.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		tbxFilter.BackColor = Color.FromArgb(10, 14, 23);
		tbxFilter.BorderStyle = BorderStyle.FixedSingle;
		tbxFilter.ForeColor = Color.Gold;
		tbxFilter.Location = new Point(17, 315);
		tbxFilter.Name = "tbxFilter";
		tbxFilter.Size = new Size(244, 22);
		tbxFilter.TabIndex = 13;
		tbxFilter.Text = "Search...";
		tbxFilter.Click += btnAdd_Click;
		tbxFilter.TextChanged += btnRemove_Click;
		btnRemoveAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		btnRemoveAll.BackColor = Color.FromArgb(10, 14, 23);
		btnRemoveAll.FlatAppearance.BorderColor = Color.Gray;
		btnRemoveAll.FlatStyle = FlatStyle.Flat;
		btnRemoveAll.Font = new Font("Segoe UI", 8.25f);
		btnRemoveAll.ForeColor = Color.Gold;
		btnRemoveAll.Location = new Point(267, 315);
		btnRemoveAll.Name = "btnRemoveAll";
		btnRemoveAll.Size = new Size(200, 22);
		btnRemoveAll.TabIndex = 16;
		btnRemoveAll.Text = "Remove All";
		btnRemoveAll.UseVisualStyleBackColor = false;
		btnRemoveAll.Click += IgnoredEnemiesForm_Load;
		btnRemove.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		btnRemove.BackColor = Color.FromArgb(0, 140, 215);
		btnRemove.FlatAppearance.BorderSize = 0;
		btnRemove.FlatStyle = FlatStyle.Flat;
		btnRemove.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0);
		btnRemove.ForeColor = Color.Gold;
		btnRemove.Location = new Point(267, 343);
		btnRemove.Name = "btnRemove";
		btnRemove.Size = new Size(200, 25);
		btnRemove.TabIndex = 12;
		btnRemove.Text = "Remove Selected";
		btnRemove.UseVisualStyleBackColor = false;
		btnRemove.Click += lstIgnored_SelectedIndexChanged;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = SystemColors.ActiveCaptionText;
		BackgroundImage = (Image)componentResourceManager.GetObject("$this.BackgroundImage");
		BackgroundImageLayout = ImageLayout.Stretch;
		base.ClientSize = new Size(484, 386);
		base.Controls.Add(btnRemoveAll);
		base.Controls.Add(pnlIgnored);
		base.Controls.Add(pnlAllEnemies);
		base.Controls.Add(tbxFilter);
		base.Controls.Add(btnRemove);
		base.Controls.Add(btnAdd);
		base.Controls.Add(lblIgnored);
		base.Controls.Add(lblAllEnemies);
		DoubleBuffered = true;
		Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 0);
		ForeColor = Color.Gainsboro;
		base.FormBorderStyle = FormBorderStyle.SizableToolWindow;
		base.Icon = (Icon)componentResourceManager.GetObject("$this.Icon");
		base.Name = "FrmSettingsIgnoredEnemies";
		base.StartPosition = FormStartPosition.CenterParent;
		Text = "ExaltHelper";
		pnlAllEnemies.ResumeLayout(performLayout: false);
		pnlIgnored.ResumeLayout(performLayout: false);
		ResumeLayout(performLayout: false);
		PerformLayout();
	}
}
