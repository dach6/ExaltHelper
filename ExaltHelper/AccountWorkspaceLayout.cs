using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ExaltHelper;

internal sealed partial class AccountWorkspacePage
{
    private int derivedTargetDefense;
    private Button ActionButton(string title, Action action)
    {
        return HudTheme.Button(title, (_, __) => { action(); Rebuild(true); });
    }

    private bool TreasuryMatches(int item, string enchant, HashSet<int> dungeonItems)
    {
        if (item <= 0) return false;
        var meta = ItemSpriteManager.GetItemMetadata(item);
        if (!Matches(meta.Name + " " + item) || dungeonItems != null && !dungeonItems.Contains(item)) return false;
        if (category.SelectedIndex > 0 && category.Text != "Unlocked blueprints" && ItemCategory(item) != category.Text) return false;
        if (itemType.SelectedIndex > 0 && (itemType.Text == "Shiny" ? !meta.IsShiny : !AccountWorkspace.ItemHasTag(item, itemType.Text))) return false;
        return enchantTier.SelectedIndex == 0 || AccountEnchantmentInfo.Parse(enchant).Total == enchantTier.SelectedIndex - 1;
    }

    private void BuildTreasurySections(HashSet<int> dungeonItems)
    {
        var groups = AccountWorkspace.TreasurySections.ToDictionary(k => k, _ => new List<AccountItemGrid.Section>());
        void Add(string key, string title, int[] ids, string[] enchants, int[] counts = null)
        {
            if (!groups.ContainsKey(key) || !Side(key.StartsWith("seasonal."))) return;
            bool[] matches = ids.Select((id, i) => TreasuryMatches(id, i < enchants.Length ? enchants[i] : "", dungeonItems)).ToArray();
            if (!matches.Any(m => m)) return;
            groups[key].Add(new AccountItemGrid.Section { Title = title, Items = ids, Enchants = enchants, Counts = counts ?? Array.Empty<int>(), Matches = matches });
        }
        foreach (var c in snapshot.Characters.OrderBy(c => c.Order).ThenBy(c => c.Id))
        {
            string key = (c.Seasonal ? "seasonal" : "regular") + ".characters", title = (c.Label.Length > 0 ? c.Label + " / " : "") + AccountWorkspace.ClassName(c.ClassType) + " #" + c.Id;
            Add(key, title, c.Inventory, c.Enchants); Add(key, title + " quickslots", c.QuickSlots, c.QuickSlotEnchants, c.QuickSlotCounts);
        }
        foreach (var s in snapshot.Storage)
        {
            string type = s.Name == "Seasonal spoils" ? "spoils" : s.Name.ToLowerInvariant();
            Add((s.Seasonal ? "seasonal" : "regular") + "." + type, s.Name, s.Items, s.Enchants);
        }
        foreach (var p in snapshot.Pets) Add((p.Seasonal ? "seasonal" : "regular") + ".pets", p.Name + " #" + p.Id, p.Inventory, p.Enchants, p.StackCounts);
        var sections = new List<AccountItemGrid.Section>();
        if (category.Text == "Unlocked blueprints")
        {
            foreach (bool seasonal in new[] { true, false }) if (Side(seasonal))
            {
                var ids = (seasonal ? snapshot.SeasonalBlueprints : snapshot.RegularBlueprints).Where(id => TreasuryMatches(id, "", dungeonItems)).ToArray();
                sections.Add(new AccountItemGrid.Section { Title = (seasonal ? "Seasonal" : "Regular") + " unlocked forge recipes", Items = ids });
            }
        }
        else foreach (string key in AccountWorkspace.OrderedKeys(snapshot.TreasurySectionOrder, AccountWorkspace.TreasurySections))
        {
            if (!Side(key.StartsWith("seasonal."))) continue;
            var children = groups[key]; bool collapsed = snapshot.CollapsedTreasurySections.Contains(key);
            int count = children.Sum(s => s.Items.Select((id, i) => s.Matches[i] ? Math.Max(1, i < s.Counts.Length ? s.Counts[i] : 1) : 0).Sum());
            sections.Add(new AccountItemGrid.Section { Key = key, Title = AccountWorkspace.TreasurySectionName(key), HeaderOnly = true, HeaderCount = count, Collapsed = collapsed });
            if (!collapsed) sections.AddRange(children);
        }
        treasuryItems.SetSections(sections, search.Text);
    }

    private sealed class OrderEntry
    {
        public string Key, Title;
        public override string ToString() => Title;
    }
    private void ArrangeTreasury()
    {
        using (var dialog = new Form { Text = "Treasury order", ClientSize = new Size(650, 430), StartPosition = FormStartPosition.CenterParent,
            BackColor = Surface, ForeColor = HudTheme.Text, Font = Font, MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog })
        {
            var lists = new ListBox[2];
            var labels = new[] { "Storage sections", "Item categories" };
            var values = new[] { AccountWorkspace.OrderedKeys(snapshot.TreasurySectionOrder, AccountWorkspace.TreasurySections), AccountWorkspace.OrderedKeys(snapshot.TreasuryCategoryOrder, AccountWorkspace.TreasuryCategories) };
            for (int column = 0; column < 2; column++)
            {
                int index = column;
                dialog.Controls.Add(new Label { Text = labels[column], Location = new Point(16 + column * 320, 16), AutoSize = true });
                var list = new ListBox { Location = new Point(16 + column * 320, 42), Size = new Size(300, 290), BackColor = HudTheme.Input, ForeColor = HudTheme.Text, BorderStyle = BorderStyle.FixedSingle };
                foreach (string key in values[column]) list.Items.Add(new OrderEntry { Key = key, Title = column == 0 ? AccountWorkspace.TreasurySectionName(key) : key });
                if (list.Items.Count > 0) list.SelectedIndex = 0; lists[column] = list; dialog.Controls.Add(list);
                void Move(int direction)
                {
                    int from = list.SelectedIndex, to = from + direction;
                    if (from < 0 || to < 0 || to >= list.Items.Count) return;
                    object item = list.Items[from]; list.Items.RemoveAt(from); list.Items.Insert(to, item); list.SelectedIndex = to;
                }
                var up = HudTheme.Button("Move up"); up.AutoSize = false; up.Location = new Point(16 + column * 320, 340); up.Size = new Size(145, 34);
                var down = HudTheme.Button("Move down"); down.AutoSize = false; down.Location = new Point(171 + column * 320, 340); down.Size = new Size(145, 34);
                up.Click += (_, __) => Move(-1); down.Click += (_, __) => Move(1); dialog.Controls.Add(up); dialog.Controls.Add(down);
            }
            var save = HudTheme.Button("Save order"); save.AutoSize = false; save.Location = new Point(420, 386); save.Size = new Size(112, 34); save.DialogResult = DialogResult.OK;
            var cancel = HudTheme.Button("Cancel"); cancel.AutoSize = false; cancel.Location = new Point(540, 386); cancel.Size = new Size(92, 34); cancel.DialogResult = DialogResult.Cancel;
            dialog.Controls.Add(save); dialog.Controls.Add(cancel); dialog.AcceptButton = save; dialog.CancelButton = cancel;
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) AccountWorkspace.SaveTreasuryLayout(lists[0].Items.Cast<OrderEntry>().Select(e => e.Key).ToArray(), lists[1].Items.Cast<OrderEntry>().Select(e => e.Key).ToArray());
        }
    }

    private Control CreateDerivedDetails(Func<AccountCharacter> character, out Action refresh)
    {
        var panel = new Panel { BackColor = Surface }; var grid = CreateTable();
        grid.Columns.Add("Attribute", "Attribute"); grid.Columns.Add("Value", "Value"); grid.Columns.Add("Source", "Sources / assumptions");
        var defense = new NumericUpDown { Minimum = 0, Maximum = 5000, Value = derivedTargetDefense, Width = 80, BackColor = HudTheme.Input, ForeColor = HudTheme.Text };
        var top = new HudFlowPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 3, 0, 3) };
        top.Controls.Add(new Label { Text = "Target DEF", AutoSize = true, ForeColor = HudTheme.Muted, Padding = new Padding(0, 5, 0, 0) }); top.Controls.Add(defense);
        void Render()
        {
            var current = character();
            if (current != null) UpdateDetailRows(grid, AccountWorkspaceDerived.Build(current, snapshot, (int)defense.Value)
                .Select(line => new object[] { line.Name, line.Value, line.Detail }));
        }
        refresh = Render;
        defense.ValueChanged += (_, __) => { derivedTargetDefense = (int)defense.Value; Render(); };
        var boostTimer = new Timer { Interval = 1000 };
        boostTimer.Tick += (_, __) => { if (panel.Visible) Render(); };
        panel.Disposed += (_, __) => boostTimer.Dispose(); boostTimer.Start();
        Render(); panel.Controls.Add(grid); panel.Controls.Add(top); return panel;
    }
}
