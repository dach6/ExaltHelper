using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ExaltHelper;

internal sealed partial class AccountWorkspacePage : UserControl
{
    private static Color Surface => HudTheme.Surface;
    private static Color Accent => HudTheme.Accent;
    private readonly string page;
    private readonly TextBox search = new HudTextBox { Width = 230, SearchIcon = true, Placeholder = "Search account", BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 0, 8, 6), AccessibleName = "Search account" };
    private readonly ComboBox side = Choice("All modes", "Seasonal", "Regular");
    private readonly ComboBox category = Choice("All categories", "Weapons", "Abilities", "Armor", "Rings", "Consumables", "Marks", "Keys", "Skins", "Blueprints", "Unlocked blueprints");
    private readonly ComboBox itemType = Choice("All item types", "UT", "ST", "Shiny");
    private readonly ComboBox enchantTier = Choice("All enchant tiers", "No enchant slots", "Uncommon", "Rare", "Legendary", "Divine");
    private readonly ComboBox dungeon = Choice("All dungeons");
    private readonly ComboBox container = Choice("All containers");
    private readonly ComboBox roster = Choice("Living", "Graveyard", "All characters");
    private readonly ComboBox treasurySort = Choice("Category order", "Name", "Count");
    private readonly TextBox characterLabel = new HudTextBox { Width = 150, MaxLength = 80, Placeholder = "Character label", BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 0, 8, 6) };
    private readonly CheckBox missing = new HudCheckBox { Text = "Include unowned drops", AutoSize = true, ForeColor = HudTheme.Muted, Margin = new Padding(0, 6, 12, 6) };
    private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 38, ForeColor = HudTheme.Muted, Font = HudTheme.SmallFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 4, 2, 0) };
    private readonly Label summary = new Label { Dock = DockStyle.Top, Height = 42, ForeColor = HudTheme.Muted, Font = HudTheme.SmallFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0) };
    private readonly DataGridView table = CreateTable();
    private readonly Panel details = new HudSurfacePanel { Dock = DockStyle.Bottom, Height = 258, BackColor = Surface, Padding = new Padding(12) };
    private readonly Panel filterCard = new HudSurfacePanel { Name = "account_filter_card", Dock = DockStyle.Top, Height = 60, BackColor = Surface, Padding = new Padding(12, 12, 4, 6) };
    private readonly FlowLayoutPanel filters = new HudFlowPanel { Name = "account_filter_toolbar", Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty, BackColor = Surface };
    private readonly AccountItemGrid items = new AccountItemGrid { Dock = DockStyle.Fill };
    private readonly AccountItemGrid treasuryItems = new AccountItemGrid { Dock = DockStyle.Fill };
    private readonly Timer timer = new Timer { Interval = 750 };
    private AccountSnapshot snapshot = new AccountSnapshot();
    private long revision = -1;
    private bool refreshing;
    private int selectedCharacter = -1;
    private int selectedPet = -1;
    private bool selectedPetSeasonal;
    private int detailTab;
    private string detailAccount;
    private AccountCharacter detailCharacter;
    private AccountDetailTabs characterTabs;
    private AccountItemGrid characterInventory;
    private Label characterStats, characterFame;
    private DataGridView characterFameGrid, characterStatsGrid;
    private Action refreshDerivedDetails;
    private bool layoutReady, arrangingPage, detailsCollapsed;

    internal AccountWorkspacePage(string key)
    {
        page = key.ToLowerInvariant().Replace(" ", "").Replace("-", "");
        BackColor = HudTheme.Background; ForeColor = HudTheme.Text;
        Font = HudTheme.BodyFont; Dock = DockStyle.Fill; Padding = new Padding(16);
        var refresh = HudTheme.Button("Refresh account");
        refresh.Click += async (_, __) => { refresh.Enabled = false; try { await AccountWorkspace.RefreshAsync(); } finally { if (!IsDisposed) refresh.Enabled = true; } };
        filters.Controls.Add(search); filters.Controls.Add(side);
        if (page == "treasury")
        {
            filters.Controls.Add(category); filters.Controls.Add(itemType); filters.Controls.Add(enchantTier); filters.Controls.Add(dungeon); filters.Controls.Add(missing);
            filters.Controls.Add(treasurySort); filters.Controls.Add(ActionButton("Arrange sections", ArrangeTreasury));
            foreach (string name in AccountWorkspaceDrops.Dungeons.Keys.OrderBy(s => s)) dungeon.Items.Add(name);
        }
        if (page == "vault") filters.Controls.Add(container);
        if (page == "characters")
        {
            filters.Controls.Add(roster); filters.Controls.Add(characterLabel);
            characterLabel.AccessibleName = "Character label";
            filters.Controls.Add(ActionButton("Save label", () => AccountWorkspace.SetCharacterLabel(selectedCharacter, characterLabel.Text)));
            filters.Controls.Add(ActionButton("Move up", () => AccountWorkspace.MoveCharacter(selectedCharacter, -1)));
            filters.Controls.Add(ActionButton("Move down", () => AccountWorkspace.MoveCharacter(selectedCharacter, 1)));
        }
        filters.Controls.Add(refresh);
        if (page == "treasury")
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Height = 520, SplitterDistance = 250, SplitterWidth = 8, BackColor = HudTheme.Background };
            split.Panel1.Controls.Add(table); split.Panel2.Controls.Add(treasuryItems); Controls.Add(split);
            treasuryItems.HeaderClicked += key => { AccountWorkspace.ToggleTreasurySection(key); Rebuild(true); };
        }
        else Controls.Add(page == "vault" ? (Control)items : table);
        if (page == "characters" || page == "petyard") Controls.Add(details);
        filterCard.Controls.Add(filters);
        filterCard.SizeChanged += (_, __) => ArrangePage(); filters.Layout += (_, __) => ArrangePage();
        Controls.Add(summary); Controls.Add(filterCard); Controls.Add(status);
        summary.AutoEllipsis = status.AutoEllipsis = true;
        layoutReady = true; ArrangePage();
        search.TextChanged += (_, __) => Rebuild(true);
        foreach (var choice in new[] { side, category, itemType, enchantTier, dungeon, container, roster, treasurySort }) choice.SelectedIndexChanged += (_, __) => { if (!refreshing) Rebuild(true); };
        missing.CheckedChanged += (_, __) => Rebuild(true);
        table.SelectionChanged += (_, __) => ShowSelected();
        timer.Tick += (_, __) => { if (Visible && FindForm()?.WindowState != FormWindowState.Minimized) Rebuild(false); };
        VisibleChanged += (_, __) => { if (Visible) Rebuild(false); };
        timer.Start(); Rebuild(true);
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); ArrangePage(); }
    private void ArrangePage()
    {
        if (!layoutReady || arrangingPage || IsDisposed) return;
        arrangingPage = true;
        try
        {
            bool shortPage = ClientSize.Height < 470;
            Padding = shortPage ? new Padding(8, 6, 8, 6) : new Padding(16);
            summary.Height = shortPage ? 22 : 42;
            status.Height = shortPage ? 20 : 38;
            status.Padding = shortPage ? new Padding(4, 0, 2, 0) : new Padding(4, 4, 2, 0);
            filterCard.Padding = shortPage ? new Padding(8, 6, 8, 4) : new Padding(12, 12, 4, 6);
            if (filters.WrapContents == shortPage)
            {
                filters.AutoScrollPosition = Point.Empty;
                filters.WrapContents = !shortPage;
            }
            filters.AutoScroll = shortPage;
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - filterCard.Padding.Horizontal);
            int toolbarHeight;
            if (shortPage)
            {
                // Reserve the native scrollbar's layout space; the shared control
                // paints its slim dark thumb without changing native interaction.
                int rowHeight = 0, rowWidth = 0;
                foreach (Control control in filters.Controls)
                {
                    Size size = control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;
                    rowHeight = Math.Max(rowHeight, size.Height + control.Margin.Vertical);
                    rowWidth += size.Width + control.Margin.Horizontal;
                }
                toolbarHeight = rowHeight + (rowWidth > width ? SystemInformation.HorizontalScrollBarHeight : 0);
            }
            else toolbarHeight = filters.GetPreferredSize(new Size(width, 0)).Height;
            filterCard.Height = toolbarHeight + filterCard.Padding.Vertical;
            if (page == "characters" || page == "petyard")
            {
                details.Padding = shortPage ? new Padding(6) : new Padding(12);
                int available = Math.Max(0, ClientSize.Height - Padding.Vertical - filterCard.Height - summary.Height - status.Height);
                int rosterMinimum = table.ColumnHeadersHeight + table.RowTemplate.Height + 8;
                int detailHeight = Math.Min(258, Math.Max(0, available - rosterMinimum));
                // At extremely short heights, preserve the roster instead of
                // covering it with a detail tab strip that has no usable body.
                detailsCollapsed = detailHeight < 64;
                details.Visible = !detailsCollapsed;
                details.Height = detailsCollapsed ? 0 : detailHeight;
            }
            UpdateStatus();
        }
        finally { arrangingPage = false; }
    }
    private void UpdateStatus()
    {
        status.Text = detailsCollapsed ? "Increase window height to view " + (page == "petyard" ? "pet" : "character") + " details."
            : snapshot.Status + (snapshot.UpdatedUtc == default ? "" : "   Last account refresh: " + snapshot.UpdatedUtc.ToLocalTime().ToString("g"));
    }

    private static ComboBox Choice(params string[] values)
    {
        var box = new HudComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 154, FlatStyle = FlatStyle.Flat,
            BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 0, 8, 6) };
        box.Items.AddRange(values); box.SelectedIndex = 0; return box;
    }
    internal static DataGridView CreateTable()
    {
        return HudTheme.Grid();
    }
    private bool Side(bool seasonal) => side.SelectedIndex == 0 || side.SelectedIndex == (seasonal ? 1 : 2);
    private bool Matches(string text) => string.IsNullOrWhiteSpace(search.Text) || (text ?? "").IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    private void Columns(params string[] names)
    {
        if (table.Columns.Count == names.Length && names.Select((n, i) => table.Columns[i].HeaderText == n).All(b => b)) return;
        table.Columns.Clear();
        foreach (string name in names) table.Columns.Add(name, name);
    }

    private void Rebuild(bool force)
    {
        if (refreshing || IsDisposed) return;
        var next = force ? AccountWorkspace.Snapshot() : AccountWorkspace.SnapshotIfChanged(revision, snapshot.AccountKey);
        if (next == null) return;
        refreshing = true;
        try
        {
            bool accountChanged = snapshot.AccountKey != next.AccountKey;
            if (accountChanged)
            {
                selectedCharacter = next.CurrentCharacterId; selectedPet = -1; detailTab = 0;
                characterLabel.Text = "";
            }
            snapshot = next; revision = next.Revision;
            UpdateStatus();
            int scroll = accountChanged ? 0 : table.FirstDisplayedScrollingRowIndex;
            table.Rows.Clear();
            if (page == "characters") Characters();
            else if (page == "exaltations") Exaltations();
            else if (page == "petyard") Pets();
            else if (page == "vault") Vault();
            else Treasury();
            if (scroll >= 0 && scroll < table.Rows.Count && table.IsHandleCreated && table.DisplayedRowCount(false) > 0)
                table.FirstDisplayedScrollingRowIndex = scroll;
        }
        finally { refreshing = false; }
        ShowSelected();
    }

    private void Characters()
    {
        Columns("Character", "Mode", "Level", "Base fame", "Maxed stats", "Inventory", "Last observed");
        if (selectedCharacter < 0) selectedCharacter = snapshot.CurrentCharacterId;
        var characters = roster.SelectedIndex == 0 ? snapshot.Characters : roster.SelectedIndex == 1 ? snapshot.DeadCharacters : snapshot.Characters.Concat(snapshot.DeadCharacters);
        foreach (var c in characters.Where(c => Side(c.Seasonal) && Matches(AccountWorkspace.ClassName(c.ClassType) + " " + c.Id + " " + c.Name + " " + c.Label + " " + c.KilledBy)).OrderBy(c => c.IsDead).ThenByDescending(c => c.Seasonal).ThenBy(c => c.Order).ThenBy(c => c.Id))
        {
            int[] max = AccountWorkspace.ClassMaximums(c.ClassType);
            int maxed = Enumerable.Range(0, 8).Count(i => max[i] > 0 && c.Stats[i] >= max[i]);
            int row = table.Rows.Add((string.IsNullOrEmpty(c.Label) ? "" : c.Label + " / ") + AccountWorkspace.ClassName(c.ClassType) + " #" + c.Id + (c.IsDead ? "  DECEASED" : c.Id == snapshot.CurrentCharacterId ? "  LIVE" : ""),
                (c.Seasonal ? "Seasonal" : "Regular") + (c.Crucible ? " / Crucible" : ""), c.Level, c.Fame, maxed + "/8",
                c.Inventory.Count(i => i > 0) + " / " + c.Inventory.Length, (c.DeathUtc ?? c.UpdatedUtc).ToLocalTime().ToString("g"));
            table.Rows[row].Tag = c;
            table.Rows[row].Cells[0].ToolTipText = c.IsDead ? c.KilledBy : c.Label;
            if (selectedCharacter == c.Id) table.CurrentCell = table.Rows[row].Cells[0];
        }
        summary.Text = snapshot.Characters.Count + " living characters / " + snapshot.CharacterSlots + " slots   |   " + snapshot.DeadCharacters.Count + " in graveyard   |   " + snapshot.Name;
    }

    private void Exaltations()
    {
        Columns("Class", "HP", "MP", "ATT", "DEF", "SPD", "DEX", "VIT", "WIS", "Total");
        int total = 0;
        int[] types = { 768, 775, 782, 784, 785, 796, 797, 798, 799, 800, 801, 802, 803, 804, 805, 806, 817, 818, 819 };
        foreach (int type in types.OrderBy(t => AccountWorkspace.ClassName(t)))
        {
            if (!Matches(AccountWorkspace.ClassName(type))) continue;
            bool observed = snapshot.Exaltations.TryGetValue(type.ToString(CultureInfo.InvariantCulture), out var counts);
            counts = counts ?? new int[8]; var values = new object[10]; values[0] = AccountWorkspace.ClassName(type);
            int levels = counts.Sum(AccountWorkspace.ExaltLevel); total += levels;
            for (int i = 0; i < 8; i++) values[i + 1] = observed ? AccountWorkspace.ExaltLevel(counts[i]) + "/5" : "—";
            values[9] = observed ? levels + "/40" : "Not synced";
            int row = table.Rows.Add(values);
            for (int i = 0; i < 8; i++)
            {
                int next = new[] { 5, 15, 30, 50, 75 }.FirstOrDefault(n => counts[i] < n);
                table.Rows[row].Cells[i + 1].ToolTipText = observed ? counts[i] + " dungeon completions. " + (next == 0 ? "Fully exalted." : (next - counts[i]) + " more for the next level.") : "Refresh account data to load this class.";
                if (observed && counts[i] >= 75) table.Rows[row].Cells[i + 1].Style.ForeColor = Accent;
            }
        }
        summary.Text = total + " exaltation levels displayed   |   Hover a stat for completion counts and the next milestone";
    }

    private void Vault()
    {
        var stores = snapshot.Storage.Where(s => Side(s.Seasonal)).OrderBy(s => s.Seasonal ? 0 : 1).ThenBy(s => s.Name).ToList();
        string selected = container.SelectedItem?.ToString();
        container.Items.Clear(); container.Items.Add("All containers");
        foreach (var s in stores) container.Items.Add((s.Seasonal ? "Seasonal " : "Regular ") + s.Name);
        container.SelectedItem = selected; if (container.SelectedIndex < 0) container.SelectedIndex = 0;
        var sections = stores.Where(s => container.SelectedIndex == 0 || (s.Seasonal ? "Seasonal " : "Regular ") + s.Name == container.Text)
            .Select(s => new AccountItemGrid.Section { Title = (s.Seasonal ? "SEASONAL  /  " : "REGULAR  /  ") + s.Name,
                Items = s.Items, Enchants = s.Enchants, Timestamp = s.UpdatedUtc }).ToList();
        items.SetSections(sections, search.Text);
        summary.Text = stores.Sum(s => s.Items.Count(id => id > 0)) + " items in " + stores.Sum(s => s.Items.Length) + " slots   |   Slots retain their in-game order";
    }

    private void Treasury()
    {
        Columns("Item", "Category", "Seasonal", "Regular", "Total", "Locations / collection");
        var all = new Dictionary<int, List<Tuple<string, bool, int>>>();
        void Add(int[] inventory, string location, bool seasonal, int[] counts = null, string[] enchants = null)
        {
            if (!Side(seasonal)) return;
            for (int slot = 0; slot < inventory.Length; slot++)
            {
                int id = inventory[slot]; if (id <= 0) continue;
                if (enchantTier.SelectedIndex > 0 && AccountEnchantmentInfo.Parse(enchants != null && slot < enchants.Length ? enchants[slot] : "").Total != enchantTier.SelectedIndex - 1) continue;
                int count = counts != null && slot < counts.Length ? Math.Max(1, counts[slot]) : 1;
                if (!all.TryGetValue(id, out var locations)) { locations = new List<Tuple<string, bool, int>>(); all[id] = locations; }
                locations.Add(Tuple.Create(location + " slot " + (slot + 1) + (count > 1 ? " (x" + count + ")" : ""), seasonal, count));
            }
        }
        foreach (var s in snapshot.Storage) Add(s.Items, (s.Seasonal ? "Seasonal " : "Regular ") + s.Name, s.Seasonal, enchants: s.Enchants);
        foreach (var c in snapshot.Characters)
        {
            Add(c.Inventory, AccountWorkspace.ClassName(c.ClassType) + " #" + c.Id, c.Seasonal, enchants: c.Enchants);
            Add(c.QuickSlots, AccountWorkspace.ClassName(c.ClassType) + " #" + c.Id + " quickslot", c.Seasonal, c.QuickSlotCounts, c.QuickSlotEnchants);
        }
        foreach (var p in snapshot.Pets) Add(p.Inventory, p.Name + " #" + p.Id, p.Seasonal, p.StackCounts, p.Enchants);
        if (category.Text == "Unlocked blueprints")
        {
            all.Clear(); if (Side(false)) Add(snapshot.RegularBlueprints, "Unlocked forge blueprint", false);
            if (Side(true)) Add(snapshot.SeasonalBlueprints, "Unlocked forge blueprint", true);
        }
        HashSet<int> dungeonIds = dungeon.SelectedIndex > 0 && AccountWorkspaceDrops.Dungeons.TryGetValue(dungeon.Text, out int[] ids) ? new HashSet<int>(ids) : null;
        if (missing.Checked && dungeonIds != null && enchantTier.SelectedIndex == 0) foreach (int id in dungeonIds) if (!all.ContainsKey(id)) all[id] = new List<Tuple<string, bool, int>>();
        int shown = 0, owned = 0;
        var entries = all.Select(p => new { Pair = p, Meta = ItemSpriteManager.GetItemMetadata(p.Key) }).AsEnumerable();
        string[] categoryOrder = AccountWorkspace.OrderedKeys(snapshot.TreasuryCategoryOrder, AccountWorkspace.TreasuryCategories);
        if (treasurySort.SelectedIndex == 0) entries = entries.OrderBy(p => Array.IndexOf(categoryOrder, ItemCategory(p.Pair.Key))).ThenByDescending(p => p.Pair.Value.Sum(v => v.Item3)).ThenBy(p => p.Meta.Name);
        else if (treasurySort.SelectedIndex == 2) entries = entries.OrderByDescending(p => p.Pair.Value.Sum(v => v.Item3)).ThenBy(p => p.Meta.Name);
        else entries = entries.OrderBy(p => p.Meta.Name);
        foreach (var entry in entries)
        {
            var meta = entry.Meta; int id = entry.Pair.Key; string group = ItemCategory(id);
            if (!Matches(meta.Name + " " + id) || (dungeonIds != null && !dungeonIds.Contains(id))) continue;
            if (category.SelectedIndex > 0 && category.Text != "Unlocked blueprints" && group != category.Text) continue;
            if (itemType.SelectedIndex > 0 && (itemType.Text == "Shiny" ? !meta.IsShiny : !AccountWorkspace.ItemHasTag(id, itemType.Text))) continue;
            var locations = entry.Pair.Value;
            int row = table.Rows.Add(meta.Name ?? "Item " + id, group, locations.Where(p => p.Item2).Sum(p => p.Item3), locations.Where(p => !p.Item2).Sum(p => p.Item3), locations.Sum(p => p.Item3),
                locations.Count == 0 ? "Not owned" : string.Join("; ", locations.Select(p => p.Item1).Distinct()));
            table.Rows[row].Cells[0].Style.ForeColor = locations.Count == 0 ? HudTheme.Muted : meta.RarityColor;
            table.Rows[row].Tag = id;
            table.Rows[row].Cells[5].ToolTipText = table.Rows[row].Cells[5].Value.ToString();
            shown++; if (locations.Count > 0) owned++;
        }
        summary.Text = shown + " item types   |   " + owned + " owned" + (missing.Checked && dungeonIds != null ? " / " + shown + " in this collection" : "") + "   |   Search covers characters, pets and every captured storage container";
        BuildTreasurySections(dungeonIds);
    }

    internal static string ItemCategory(int id)
    {
        var meta = ItemSpriteManager.GetItemMetadata(id); string name = meta.Name ?? "";
        if (name.StartsWith("Mark of", StringComparison.OrdinalIgnoreCase)) return "Marks";
        if (name.IndexOf("Blueprint", StringComparison.OrdinalIgnoreCase) >= 0) return "Blueprints";
        if (AccountWorkspace.ItemHasTag(id, "KEY")) return "Keys";
        if (name.IndexOf(" Skin", StringComparison.OrdinalIgnoreCase) >= 0) return "Skins";
        if (meta.IsWeapon) return "Weapons";
        if (new[] { 6, 7, 14 }.Contains(meta.SlotType)) return "Armor";
        if (meta.SlotType == 9) return "Rings";
        if (meta.SlotType > 0 && meta.SlotType != 10) return "Abilities";
        return "Consumables";
    }

    private void Pets()
    {
        Columns("Pet", "Mode", "Rarity", "Abilities", "Inventory", "Identity");
        foreach (var pet in snapshot.Pets.Where(p => Side(p.Seasonal) && Matches(p.Name + " " + p.Id)).OrderBy(p => p.Name))
        {
            bool conflict = snapshot.Pets.Any(p => p.Id == pet.Id && p.Seasonal != pet.Seasonal && (p.Skin != pet.Skin || p.Type != pet.Type));
            int row = table.Rows.Add(pet.Name + " #" + pet.Id, pet.Seasonal ? "Seasonal" : "Regular",
                new[] { "Common", "Uncommon", "Rare", "Legendary", "Divine" }[Math.Max(0, Math.Min(4, pet.Rarity))],
                string.Join(" / ", pet.AbilityTypes.Select((type, i) => AbilityName(type)
                    + (i < pet.AbilityLevels.Length && pet.AbilityLevels[i] > 0 ? " lv " + pet.AbilityLevels[i] : "")
                    + (i < pet.AbilityPowers.Length ? " pow " + pet.AbilityPowers[i] : ""))),
                pet.Inventory.Count(id => id > 0) + " / " + pet.Inventory.Length, conflict ? "Different cached skins: visit each Pet Yard" : "Skin " + pet.Skin);
            table.Rows[row].Tag = pet; if (selectedPet == pet.Id && selectedPetSeasonal == pet.Seasonal) table.CurrentCell = table.Rows[row].Cells[0];
            table.Rows[row].Cells[3].ToolTipText = Convert.ToString(table.Rows[row].Cells[3].Value);
            table.Rows[row].Cells[5].ToolTipText = (pet.IdentityFromApi ? "Identity verified by account refresh." : "Identity observed in Pet Yard.")
                + "\nEquipped by: " + string.Join(", ", snapshot.Characters.Where(c => c.PetId == pet.Id && c.Seasonal == pet.Seasonal).Select(c => AccountWorkspace.ClassName(c.ClassType) + " #" + c.Id));
        }
        string Yard(int? tier) => tier.HasValue && tier.Value >= 1 && tier.Value <= 5 ? new[] { "Common", "Uncommon", "Rare", "Legendary", "Divine" }[tier.Value - 1] : "Not observed";
        summary.Text = snapshot.Pets.Count + " pets observed   |   Regular yard: " + Yard(snapshot.RegularPetYard) + "   |   Seasonal yard: " + Yard(snapshot.SeasonalPetYard);
    }
    private static string AbilityName(int type)
    {
        switch (type) { case 402: return "Attack Close"; case 404: return "Attack Mid"; case 405: return "Attack Far";
            case 406: return "Electric"; case 407: return "Heal"; case 408: return "Magic Heal"; case 409: return "Savage"; case 410: return "Decoy"; case 411: return "Rising Fury"; default: return "Ability " + type; }
    }

    private void ShowSelected()
    {
        if (refreshing || (page != "characters" && page != "petyard")) return;
        if (table.SelectedRows.Count == 0)
        {
            ClearDetails();
            details.Controls.Add(new Label { Dock = DockStyle.Fill, Text = page == "characters" ? "Select a character to inspect equipment, stats and fame milestones." : "Select a pet to inspect its inventory.", ForeColor = HudTheme.Muted });
            return;
        }
        if (table.SelectedRows[0].Tag is AccountPet pet)
        {
            ClearDetails();
            selectedPet = pet.Id; selectedPetSeasonal = pet.Seasonal; var petItems = new AccountItemGrid { Dock = DockStyle.Fill };
            petItems.SetSections(new[] { new AccountItemGrid.Section { Title = pet.Name + (pet.MaximumLevel > 0 ? " / maximum ability power " + pet.MaximumLevel : ""),
                Items = pet.Inventory, Counts = pet.StackCounts, Enchants = pet.Enchants } }, ""); details.Controls.Add(petItems); return;
        }
        if (!(table.SelectedRows[0].Tag is AccountCharacter c)) return;
        bool sameCharacter = characterTabs != null && !characterTabs.IsDisposed && detailAccount == snapshot.AccountKey
            && detailCharacter != null && detailCharacter.Id == c.Id && detailCharacter.Seasonal == c.Seasonal && detailCharacter.IsDead == c.IsDead;
        if (!sameCharacter || !characterLabel.Focused) characterLabel.Text = c.Label;
        selectedCharacter = c.Id;
        if (!sameCharacter)
        {
            ClearDetails();
            detailAccount = snapshot.AccountKey; detailCharacter = c;
            CreateCharacterDetails();
        }
        else detailCharacter = c;
        UpdateCharacterDetails();
    }

    private void ClearDetails()
    {
        refreshDerivedDetails = null; detailCharacter = null; detailAccount = null;
        characterTabs = null; characterInventory = null; characterStats = characterFame = null;
        characterFameGrid = characterStatsGrid = null; detailTab = 0;
        foreach (Control control in details.Controls.Cast<Control>().ToArray()) { details.Controls.Remove(control); control.Dispose(); }
    }

    private void CreateCharacterDetails()
    {
        characterTabs = new AccountDetailTabs { Dock = DockStyle.Fill };
        var equipment = new Panel { BackColor = Surface, ForeColor = HudTheme.Text };
        characterStats = new Label { Dock = DockStyle.Top, Height = 66, Padding = new Padding(8), ForeColor = HudTheme.Text, AccessibleName = "Character base stats" };
        characterInventory = new AccountItemGrid { Dock = DockStyle.Fill, AccessibleName = "Character equipment and inventory" };
        equipment.Controls.Add(characterInventory); equipment.Controls.Add(characterStats); characterTabs.AddPage("Equipment & stats", equipment);
        var fame = new Panel { BackColor = Surface, ForeColor = HudTheme.Text };
        characterFameGrid = CreateTable(); characterFameGrid.Columns.Add("Bonus", "Bonus"); characterFameGrid.Columns.Add("Fame", "Added fame"); characterFameGrid.Columns.Add("Progress", "Progress to next milestone");
        characterFame = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), ForeColor = Accent };
        fame.Controls.Add(characterFameGrid); fame.Controls.Add(characterFame); characterTabs.AddPage("Fame & milestones", fame);
        var statistics = new Panel { BackColor = Surface };
        characterStatsGrid = CreateTable(); characterStatsGrid.Columns.Add("Stat", "Statistic"); characterStatsGrid.Columns.Add("Value", "Value");
        statistics.Controls.Add(characterStatsGrid); characterTabs.AddPage("Statistics", statistics);
        characterTabs.AddPage("Combat & boosts", CreateDerivedDetails(() => detailCharacter, out refreshDerivedDetails));
        characterTabs.SelectedIndex = detailTab;
        var tabs = characterTabs;
        tabs.SelectedIndexChanged += (_, __) => detailTab = tabs.SelectedIndex;
        details.Controls.Add(characterTabs);
    }

    private void UpdateCharacterDetails()
    {
        var c = detailCharacter;
        string[] names = { "HP", "MP", "ATT", "DEF", "SPD", "DEX", "VIT", "WIS" }; var maxima = AccountWorkspace.ClassMaximums(c.ClassType);
        characterStats.Height = c.IsDead ? 84 : 66;
        characterStats.Text = string.Join("    ", names.Select((n, i) => n + " " + c.Stats[i] + "/" + (maxima[i] == 0 ? "?" : maxima[i].ToString())))
            + "\nPotions to max: " + string.Join("    ", names.Select((n, i) => n + " " + (maxima[i] == 0 ? "?" : Math.Ceiling(Math.Max(0, maxima[i] - c.Stats[i]) / (i < 2 ? 5d : 1d)).ToString("0"))))
            + "\nBase stats exclude equipment bonuses.";
        if (c.IsDead) characterStats.Text += "\n" + c.KilledBy + (c.DeathFame > 0 ? " / earned " + c.DeathFame.ToString("N0") + " fame" : "");
        var sections = new List<AccountItemGrid.Section>();
        string[] groups = { "Equipment", "Inventory", "Backpack", "Backpack extender" }; int[] starts = { 0, 4, 12, 20 }, lengths = { 4, 8, 8, 8 };
        for (int i = 0; i < groups.Length; i++) if (c.Inventory.Length > starts[i]) sections.Add(new AccountItemGrid.Section { Title = groups[i], StartSlot = starts[i],
            Items = c.Inventory.Skip(starts[i]).Take(lengths[i]).ToArray(), Enchants = c.Enchants.Skip(starts[i]).Take(lengths[i]).ToArray() });
        sections.Add(new AccountItemGrid.Section { Title = "Quickslots", Items = c.QuickSlots, Counts = c.QuickSlotCounts, Enchants = c.QuickSlotEnchants });
        characterInventory.SetSections(sections, "");
        var breakdown = AccountWorkspaceFame.Breakdown(c);
        UpdateDetailRows(characterFameGrid, c.DeathBonuses.Count > 0
            ? c.DeathBonuses.Select(b => new object[] { b.Name, b.Fame, "Awarded tier " + b.Level })
            : breakdown.Select(line => new object[] { line.Name, line.Bonus(c.Fame), line.Progress }));
        characterFame.Text = "Estimated fame on death: " + (c.Fame + breakdown.Sum(l => l.Bonus(c.Fame))).ToString("N0")
            + "   |   Base: " + c.Fame.ToString("N0") + "\n" + (c.HasPcStats ? "Uses cached character statistics; excludes the account Ancestor bonus." : "Detailed statistics are unavailable. Refresh account data for a complete estimate.");
        if (c.IsDead && c.DeathFame > 0) characterFame.Text = "Fame earned on death: " + c.DeathFame.ToString("N0") + "\n" + c.KilledBy;
        UpdateDetailRows(characterStatsGrid, c.PcStats.OrderBy(p => p.Key).Select(stat => new object[] { AccountWorkspaceFame.DisplayName(stat.Key), stat.Value }));
        refreshDerivedDetails?.Invoke();
    }

    private static void UpdateDetailRows(DataGridView grid, IEnumerable<object[]> values)
    {
        var rows = values.ToList();
        string selected = Convert.ToString(grid.CurrentRow?.Cells[0].Value);
        int column = grid.CurrentCell?.ColumnIndex ?? 0, scroll = grid.FirstDisplayedScrollingRowIndex;
        string anchor = scroll < 0 ? null : Convert.ToString(grid.Rows[scroll].Cells[0].Value);
        int horizontal = grid.HorizontalScrollingOffset;
        var sortedColumn = grid.SortedColumn; var sortOrder = grid.SortOrder;
        grid.SuspendLayout();
        try
        {
            while (grid.Rows.Count > rows.Count) grid.Rows.RemoveAt(grid.Rows.Count - 1);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= grid.Rows.Count) grid.Rows.Add(rows[i]);
                else for (int j = 0; j < rows[i].Length; j++)
                    if (!Equals(grid.Rows[i].Cells[j].Value, rows[i][j])) grid.Rows[i].Cells[j].Value = rows[i][j];
                if (grid.Columns.Count > 2) grid.Rows[i].Cells[2].ToolTipText = Convert.ToString(rows[i][2]);
            }
            if (sortedColumn != null && sortOrder != SortOrder.None)
                grid.Sort(sortedColumn, sortOrder == SortOrder.Ascending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending);
            var selectedRow = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => Convert.ToString(r.Cells[0].Value) == selected);
            if (selectedRow != null && grid.CurrentCell != selectedRow.Cells[column]) grid.CurrentCell = selectedRow.Cells[column];
        }
        finally { grid.ResumeLayout(); }
        if (scroll >= 0 && grid.Rows.Count > 0 && grid.IsHandleCreated && grid.DisplayedRowCount(false) > 0)
        {
            var anchorRow = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => Convert.ToString(r.Cells[0].Value) == anchor);
            grid.FirstDisplayedScrollingRowIndex = anchorRow?.Index ?? Math.Min(scroll, grid.Rows.Count - 1);
        }
        if (grid.HorizontalScrollingOffset != horizontal) grid.HorizontalScrollingOffset = horizontal;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class AccountDetailTabs : UserControl
{
    private readonly FlowLayoutPanel navigation = new HudFlowPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
    private readonly Panel content = new Panel { Dock = DockStyle.Fill };
    private readonly List<Button> buttons = new List<Button>();
    private readonly List<Control> pages = new List<Control>();
    private int selectedIndex;
    public event EventHandler SelectedIndexChanged;
    public AccountDetailTabs() { BackColor = HudTheme.Surface; content.BackColor = BackColor; navigation.BackColor = BackColor; Controls.Add(content); Controls.Add(navigation); }
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            selectedIndex = Math.Max(0, Math.Min(pages.Count - 1, value));
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == selectedIndex;
                buttons[i].ForeColor = i == selectedIndex ? HudTheme.Accent : HudTheme.Muted;
                buttons[i].BackColor = i == selectedIndex ? HudTheme.Raised : BackColor;
                buttons[i].FlatAppearance.BorderColor = i == selectedIndex ? HudTheme.Accent : HudTheme.InputBorder;
            }
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public void AddPage(string title, Control page)
    {
        int index = pages.Count; page.Dock = DockStyle.Fill; pages.Add(page); content.Controls.Add(page);
        var button = HudTheme.Button(title, (_, __) => SelectedIndex = index);
        button.Margin = new Padding(0, 0, 6, 4); button.UseMnemonic = false;
        buttons.Add(button); navigation.Controls.Add(button); SelectedIndex = selectedIndex;
    }
}

internal sealed class AccountItemGrid : HudScrollPanel
{
    internal sealed class Section { public string Title; public string Key; public bool HeaderOnly, Collapsed; public int HeaderCount; public bool[] Matches = Array.Empty<bool>(); public int[] Items = Array.Empty<int>(); public int[] Counts = Array.Empty<int>(); public string[] Enchants = Array.Empty<string>(); public int StartSlot; public DateTime Timestamp; }
    internal event Action<string> HeaderClicked;
    private readonly HudItemToolTip tip;
    private List<Section> sections = new List<Section>();
    private string filter = "";
    private string currentTip;
    private HudItemView currentTipItem;
    private Point currentTipAnchor;
    private Section hoveredSection;
    private int hoveredIndex = -1;
    private string hoveredHeader;
    private bool hoveredDetailsComplete;
    private readonly Font indexFont = new Font("Segoe UI", 6.5f);
    private readonly Font headerFont = new Font("Segoe UI Semibold", 9f);
    private readonly Font badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold);
    private const int Tile = 54;
    private const int Columns = 8;
    public AccountItemGrid()
    {
        tip = new HudItemToolTip(this);
        DoubleBuffered = true; AutoScroll = true; BackColor = HudTheme.Background; TabStop = true;
        AccessibleName = "Account inventory slots";
        Font = HudTheme.SmallFont; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }
    internal void SetSections(IEnumerable<Section> data, string search)
    {
        var next = data.ToList(); string nextFilter = search?.Trim() ?? "";
        if (filter == nextFilter && sections.Count == next.Count && sections.Zip(next, SameSection).All(equal => equal)) return;
        var scroll = new Point(-AutoScrollPosition.X, -AutoScrollPosition.Y);
        sections = next.Select(SnapshotSection).ToList(); filter = nextFilter;
        ClearHover();
        var contentSize = new Size(Columns * Tile + 20, sections.Sum(s => 42 + ((s.Items.Length + Columns - 1) / Columns) * Tile) + 12);
        if (AutoScrollMinSize != contentSize)
        {
            AutoScrollMinSize = contentSize;
            AutoScrollPosition = scroll;
        }
        Invalidate();
    }
    private static bool SameSection(Section first, Section second) => first.Title == second.Title && first.Key == second.Key
        && first.HeaderOnly == second.HeaderOnly && first.Collapsed == second.Collapsed && first.HeaderCount == second.HeaderCount
        && first.StartSlot == second.StartSlot && first.Timestamp == second.Timestamp && first.Items.SequenceEqual(second.Items)
        && first.Counts.SequenceEqual(second.Counts) && Enumerable.SequenceEqual(first.Enchants, second.Enchants, StringComparer.Ordinal)
        && first.Matches.SequenceEqual(second.Matches);
    private static Section SnapshotSection(Section section) => new Section { Title = section.Title, Key = section.Key,
        HeaderOnly = section.HeaderOnly, Collapsed = section.Collapsed, HeaderCount = section.HeaderCount,
        StartSlot = section.StartSlot, Timestamp = section.Timestamp, Items = (int[])section.Items.Clone(),
        Counts = (int[])section.Counts.Clone(), Enchants = (string[])section.Enchants.Clone(), Matches = (bool[])section.Matches.Clone() };
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; var state = g.Save();
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y); g.SmoothingMode = SmoothingMode.AntiAlias;
        int y = 12, viewportTop = -AutoScrollPosition.Y, viewportBottom = viewportTop + ClientSize.Height;
        int cardWidth = Math.Max(Columns * Tile + 8, ClientSize.Width - 12);
        using (var muted = new SolidBrush(HudTheme.Muted))
        {
            if (sections.Count == 0) g.DrawString("No storage observed yet. Connect a character and refresh account data.", Font, muted, new RectangleF(16, 18, Math.Max(180, ClientSize.Width - 40), 60));
            foreach (var section in sections)
            {
                int rows = (section.Items.Length + Columns - 1) / Columns, sectionHeight = 42 + rows * Tile;
                if (y + sectionHeight < viewportTop || y > viewportBottom) { y += sectionHeight; continue; }
                using (var card = HudControlPaint.Rounded(new RectangleF(6.5f, y - 4.5f, cardWidth - 1, sectionHeight - 9), 8))
                using (var fill = new SolidBrush(HudTheme.Surface))
                using (var border = new Pen(HudTheme.Border))
                { g.FillPath(fill, card); g.DrawPath(border, card); }
                DrawHeader(g, section, y, cardWidth);
                int slotTop = y + 30;
                int firstRow = Math.Max(0, (viewportTop - slotTop) / Tile);
                int lastRow = Math.Min(rows, (viewportBottom - slotTop) / Tile + 1);
                for (int i = firstRow * Columns; i < Math.Min(section.Items.Length, lastRow * Columns); i++)
                {
                    DrawSlot(g, section, i, new Rectangle(12 + i % Columns * Tile, slotTop + i / Columns * Tile, Tile - 4, Tile - 4));
                }
                y += sectionHeight;
            }
        }
        g.Restore(state);
        if (Focused && ClientSize.Width > 4 && ClientSize.Height > 4)
            using (var outline = HudControlPaint.Rounded(new RectangleF(1, 1, ClientSize.Width - 3, ClientSize.Height - 3), 8))
            using (var focus = new Pen(Color.FromArgb(180, HudTheme.Accent))) g.DrawPath(focus, outline);
    }
    private void DrawHeader(Graphics g, Section section, int y, int cardWidth)
    {
        string count = section.HeaderOnly ? section.HeaderCount.ToString("N0") + " matching items" : section.Items.Count(i => i > 0).ToString("N0") + " / " + section.Items.Length.ToString("N0") + " slots";
        int badgeWidth = (int)Math.Ceiling(g.MeasureString(count, indexFont).Width) + 14;
        var badge = new RectangleF(cardWidth - badgeWidth - 2, y + 3, badgeWidth, 18);
        using (var path = HudControlPaint.Rounded(badge, 5))
        using (var fill = new SolidBrush(HudTheme.Raised)) g.FillPath(fill, path);
        using (var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
        using (var ink = new SolidBrush(HudTheme.Muted)) g.DrawString(count, indexFont, ink, new RectangleF(badge.X + 5, badge.Y, badge.Width - 10, badge.Height), format);
        int left = section.HeaderOnly ? 30 : 17;
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
        using (var ink = new SolidBrush(section.HeaderOnly && hoveredHeader == section.Key ? HudTheme.Accent : HudTheme.Text))
            g.DrawString(section.Title ?? "", headerFont, ink, new RectangleF(left, y, Math.Max(10, badge.X - left - 10), 25), format);
        if (section.HeaderOnly)
        {
            using var line = new Pen(hoveredHeader == section.Key ? HudTheme.Accent : HudTheme.Muted, 1.4f);
            if (section.Collapsed) g.DrawLines(line, new[] { new Point(17, y + 9), new Point(21, y + 13), new Point(17, y + 17) });
            else g.DrawLines(line, new[] { new Point(15, y + 11), new Point(19, y + 15), new Point(23, y + 11) });
        }
    }
    private void DrawSlot(Graphics g, Section section, int index, Rectangle rect)
    {
        int id = section.Items[index]; var meta = id > 0 ? ItemSpriteManager.GetItemMetadata(id) : null;
        bool match = (filter.Length == 0 || (meta?.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || id.ToString() == filter)
            && (index >= section.Matches.Length || section.Matches[index]);
        bool hovered = ReferenceEquals(hoveredSection, section) && hoveredIndex == index;
        using var shape = HudControlPaint.Rounded(new RectangleF(rect.X + .5f, rect.Y + .5f, rect.Width - 1, rect.Height - 1), 7);
        using (var fill = new SolidBrush(hovered ? HudControlPaint.Mix(HudTheme.Slot, HudTheme.Accent, .055) : HudTheme.Slot)) g.FillPath(fill, shape);
        using (var inset = HudControlPaint.Rounded(new RectangleF(rect.X + 2.5f, rect.Y + 2.5f, rect.Width - 5, rect.Height - 5), 5))
        using (var frame = new Pen(HudControlPaint.Mix(HudTheme.Slot, HudTheme.Border, .48))) g.DrawPath(frame, inset);
        if (id > 0)
        {
            var image = ItemSpriteManager.GetItemSprite(id);
            if (image != null)
            {
                var interpolation = g.InterpolationMode; var pixels = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(image, new Rectangle(rect.X + 9, rect.Y + 8, 32, 32));
                g.InterpolationMode = interpolation; g.PixelOffsetMode = pixels;
            }
            else using (var ink = new SolidBrush(HudTheme.Muted))
                using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(id.ToString(), Font, ink, new RectangleF(rect.X + 4, rect.Y + 13, rect.Width - 8, 22), center);
            var enchant = AccountEnchantmentInfo.Parse(index < section.Enchants.Length ? section.Enchants[index] : "");
            if (enchant.Total > 0) using (var rarity = new SolidBrush(EnchantmentParser.GetRarityColor(enchant.Rarity)))
                g.FillEllipse(rarity, rect.Right - 8, rect.Y + 5, 3, 3);
            if (index < section.Counts.Length && section.Counts[index] > 1)
            {
                string count = section.Counts[index].ToString("N0"); int width = Math.Min(rect.Width - 8, (int)Math.Ceiling(g.MeasureString(count, badgeFont).Width) + 6);
                var badge = new RectangleF(rect.Right - width - 3, rect.Bottom - 17, width, 14);
                using (var badgeShape = HudControlPaint.Rounded(badge, 4))
                using (var fill = new SolidBrush(HudTheme.Raised))
                using (var border = new Pen(HudTheme.Border)) { g.FillPath(fill, badgeShape); g.DrawPath(border, badgeShape); }
                using (var ink = new SolidBrush(HudTheme.Text))
                using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(count, badgeFont, ink, badge, center);
            }
            if (!match) using (var dim = new SolidBrush(Color.FromArgb(185, HudTheme.Slot))) g.FillPath(dim, shape);
        }
        using (var ink = new SolidBrush(HudControlPaint.Mix(HudTheme.Slot, HudTheme.Muted, .78)))
            g.DrawString((section.StartSlot + index + 1).ToString(), indexFont, ink, rect.X + 4, rect.Y + 3);
        using (var border = new Pen(hovered ? HudTheme.Accent : match && filter.Length > 0 ? Color.FromArgb(165, HudTheme.Accent) : HudTheme.Border, hovered ? 1.4f : 1f))
            g.DrawPath(border, shape);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); string header = HeaderAt(e.Y); Cursor = header == null ? Cursors.Default : Cursors.Hand;
        int x = e.X - AutoScrollPosition.X - 12, y = e.Y - AutoScrollPosition.Y - 12;
        Section hover = null; int hovered = -1;
        foreach (var section in sections)
        {
            y -= 30; int rows = (section.Items.Length + Columns - 1) / Columns;
            if (y >= 0 && y < rows * Tile && x >= 0 && x < Columns * Tile)
            {
                int index = y / Tile * Columns + x / Tile;
                if (index < section.Items.Length && x % Tile < Tile - 4 && y % Tile < Tile - 4)
                { hover = section; hovered = index; }
                break;
            }
            y -= rows * Tile + 12;
        }
        bool detailsComplete = hover != null && hover.Items[hovered] > 0 && HudItemPresentation.WarmUpAsync().IsCompleted;
        bool changed = !ReferenceEquals(hover, hoveredSection) || hovered != hoveredIndex || header != hoveredHeader;
        if (!changed && detailsComplete == hoveredDetailsComplete) return;
        string text = ""; HudItemView itemTip = null;
        if (hover != null)
        {
            int id = hover.Items[hovered];
            text = hover.Title + " / slot " + (hover.StartSlot + hovered + 1);
            if (hovered < hover.Counts.Length && hover.Counts[hovered] > 1) text += "\nStack: " + hover.Counts[hovered];
            if (hover.Timestamp != default) text += "\nObserved " + hover.Timestamp.ToLocalTime().ToString("g");
            if (id <= 0) text += "\nEmpty";
            else
            {
                var meta = ItemSpriteManager.GetItemMetadata(id);
                var enchant = AccountEnchantmentInfo.Parse(hovered < hover.Enchants.Length ? hover.Enchants[hovered] : "");
                itemTip = HudItemPresentation.Create(id, meta.Name, meta.Rarity, meta.IsShiny,
                    enchantments: enchant.Total > 0 ? enchant.Description : null, details: AccountWorkspacePage.ItemCategory(id));
                text += "\n" + itemTip.Tooltip;
            }
        }
        hoveredDetailsComplete = detailsComplete;
        if (changed)
        { hoveredSection = hover; hoveredIndex = hovered; hoveredHeader = header; Invalidate(); }
        if (currentTip != text)
        {
            currentTip = text; currentTipItem = itemTip; currentTipAnchor = new Point(e.X + 16, e.Y + 20);
            if (text.Length == 0) tip.Hide(); else tip.Show(itemTip, text, currentTipAnchor);
        }
    }
    private string HeaderAt(int mouseY)
    {
        int y = 12 + AutoScrollPosition.Y;
        foreach (var section in sections)
        {
            if (section.HeaderOnly && mouseY >= y && mouseY < y + 30) return section.Key;
            y += 42 + ((section.Items.Length + Columns - 1) / Columns) * Tile;
        }
        return null;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); string key = HeaderAt(e.Y);
        if (e.Button == MouseButtons.Left && key != null) HeaderClicked?.Invoke(key);
    }
    private void ClearHover() { hoveredSection = null; hoveredIndex = -1; hoveredHeader = null; hoveredDetailsComplete = false; currentTip = ""; currentTipItem = null; tip.Hide(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); ClearHover(); Cursor = Cursors.Default; Invalidate(); }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); ClearHover(); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override bool IsInputKey(Keys keyData) => new[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End, Keys.F1 }.Contains(keyData & Keys.KeyCode) || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); int x = -AutoScrollPosition.X, y = -AutoScrollPosition.Y;
        switch (e.KeyCode)
        {
            case Keys.F1:
                if (!string.IsNullOrEmpty(currentTip)) tip.Show(currentTipItem, currentTip, currentTipAnchor, true);
                e.Handled = e.SuppressKeyPress = true; return;
            case Keys.Up: y -= Tile; break; case Keys.Down: y += Tile; break;
            case Keys.Left: x -= Tile; break; case Keys.Right: x += Tile; break;
            case Keys.PageUp: y -= ClientSize.Height; break; case Keys.PageDown: y += ClientSize.Height; break;
            case Keys.Home: y = 0; break; case Keys.End: y = AutoScrollMinSize.Height; break;
            default: return;
        }
        AutoScrollPosition = new Point(Math.Max(0, Math.Min(Math.Max(0, AutoScrollMinSize.Width - ClientSize.Width), x)),
            Math.Max(0, Math.Min(Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height), y)));
        e.Handled = e.SuppressKeyPress = true; ClearHover(); Invalidate();
    }
    protected override void Dispose(bool disposing) { if (disposing) { tip.Dispose(); indexFont.Dispose(); headerFont.Dispose(); badgeFont.Dispose(); } base.Dispose(disposing); }
}
