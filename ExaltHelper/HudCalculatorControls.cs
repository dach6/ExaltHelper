using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal sealed class HudCalculatorSetup
{
    public double[] Base { get; set; }
    public double[] Bonus { get; set; }
    public double[] SnapshotAdjustment { get; set; }
    public int[] Equipment { get; set; }
    public string[] Enchants { get; set; }
    public int Mode { get; set; }
    public double[] Inputs { get; set; }
    public bool[] Conditions { get; set; }
    public int PetId { get; set; }
    public bool PetSeasonal { get; set; }
    public int ClassType { get; set; }
}

internal sealed partial class HudCalculatorPage : UserControl
{
    private sealed class CharacterChoice
    {
        public AccountCharacter Character;
        public override string ToString() => AccountWorkspace.ClassName(Character.ClassType) + " #" + Character.Id + (Character.Seasonal ? " · seasonal" : " · regular");
    }
    private sealed class PetChoice
    {
        public AccountPet Pet;
        public override string ToString() => Pet == null ? "No linked pet" : Pet.Name + " #" + Pet.Id + (Pet.Seasonal ? " · seasonal" : " · regular");
    }
    private readonly FlowLayoutPanel body = new HudFlowPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 8, 12) };
    private readonly ComboBox characters = Choice(290), mode = Choice(250), pets = Choice(290);
    private readonly ComboBox[] equipment = { Choice(280, true), Choice(280, true), Choice(280, true), Choice(280, true) };
    private readonly NumericUpDown[] baseStats = new[] { 700, 252, 75, 25, 50, 75, 40, 50 }.Select(x => Number(x, 100000)).ToArray();
    private readonly NumericUpDown[] statBonus = Enumerable.Range(0, 8).Select(_ => Number(0, 100000, -100000)).ToArray();
    private readonly NumericUpDown defense = Number(0, 5000), exalt = Number(0, 100, 0, 2), mastery = Number(0, 100, 0, 2), challenge = Number(0, 1000, -99, 2), hex = Number(0, 100);
    private readonly NumericUpDown casts = Number(1, 20, 0, 2), extraMp = Number(0, 1000, 0, 2), petHeal = Number(0, 100), petMagic = Number(0, 100);
    private readonly CheckBox damaging = Check("Damaging"), berserk = Check("Berserk"), curse = Check("Cursed target"), armorBroken = Check("Armor broken"),
        weak = Check("Weak"), dazed = Check("Dazed"), armored = Check("Armored target"), exposed = Check("Exposed target"), combat = Check("In combat");
    private readonly Label source = TextLabel("Choose gear and enter base stats, or import a character."), summary = TextLabel("Choose a weapon", false), abilitySummary = TextLabel("Choose an ability"), notes = TextLabel(""), regen = TextLabel("");
    private readonly DataGridView statGrid = Grid(260, "Stat", "Base", "Gear", "Bonus", "Live", "Total"),
        defenseGrid = Grid(235, "Target DEF", "Weapon DPS", "Proc DPS", "Total weapon DPS"),
        abilityGrid = Grid(260, "Ability effect", "One target / use", "All targets / use", "Breakdown"), procGrid = Grid(150, "Native weapon proc", "DPS", "Breakdown");
    private readonly string[] enchantCodes = new string[4];
    private double[] snapshotAdjustment = new double[8];
    private LocalPlayerInfo live;
    private bool loaded, updating;
    private string accountKey = "", setupKey = "manual";
    private long characterRevision = -1;
    private int importedClassType;
    private string challengeNotice = "";
    private List<AccountPet> accountPets = new List<AccountPet>();

    public HudCalculatorPage()
    {
        Dock = DockStyle.Fill; BackColor = HudTheme.Background; ForeColor = HudTheme.Text; Font = HudTheme.BodyFont;
        Controls.Add(body);
        body.Controls.Add(Row(HudTheme.Button("Use current gear", (_, __) => UseCurrent()), characters, HudTheme.Button("Load character", (_, __) => UseCharacter()),
            HudTheme.Button("Save setup", (_, __) => SaveSetup()), HudTheme.Button("Restore setup", (_, __) => RestoreSetup())));
        body.Controls.Add(source);
        var gear = Row(); string[] slots = { "Weapon", "Ability", "Armor", "Ring" };
        for (int i = 0; i < 4; i++)
        {
            int slot = i; gear.Controls.Add(Field(slots[i], equipment[i])); equipment[i].AccessibleName = slots[i];
            equipment[i].SelectedIndexChanged += (_, __) => { if (updating) return; enchantCodes[slot] = ""; if (slot == 1) LoadModes(); Calculate(); };
        }
        gear.Controls.Add(Field("Ability mode", mode)); mode.SelectedIndexChanged += (_, __) => Calculate(); body.Controls.Add(gear);
        body.Controls.Add(TextLabel("Base stats and exaltation / manual bonuses", false));
        var baseRow = Row(); var bonusRow = Row();
        for (int i = 0; i < 8; i++)
        {
            baseStats[i].AccessibleName = "Base " + HudCalculatorEngine.StatNames[i]; statBonus[i].AccessibleName = "Bonus " + HudCalculatorEngine.StatNames[i];
            baseRow.Controls.Add(Field(HudCalculatorEngine.StatNames[i], baseStats[i])); bonusRow.Controls.Add(Field("+ " + HudCalculatorEngine.StatNames[i], statBonus[i]));
            baseStats[i].ValueChanged += (_, __) => Calculate(); statBonus[i].ValueChanged += (_, __) => Calculate();
        }
        bonusRow.Controls.Add(HudTheme.Button("Clear live adjustments", (_, __) => { snapshotAdjustment = new double[8]; Calculate(); }));
        body.Controls.Add(baseRow); body.Controls.Add(bonusRow); body.Controls.Add(statGrid);
        body.Controls.Add(TextLabel("Damage and resources", false));
        body.Controls.Add(Row(Field("Target DEF", defense), Field("Exalt damage %", exalt), Field("Mastery %", mastery), Field("Challenge %", challenge), Field("Hex stacks", hex),
            Field("Requested casts / s", casts), Field("Other MP / s", extraMp), Field("Pet Heal level", petHeal), Field("Pet Magic Heal level", petMagic)));
        body.Controls.Add(Row(Field("Pet", pets)));
        pets.Items.Add(new PetChoice()); pets.SelectedIndex = 0;
        pets.SelectedIndexChanged += (_, __) => { if (!updating) { SetPetLevels((pets.SelectedItem as PetChoice)?.Pet); Calculate(); } };
        var conditions = Row(); foreach (var box in Conditions()) { conditions.Controls.Add(box); box.CheckedChanged += (_, __) => Calculate(); }
        body.Controls.Add(conditions);
        foreach (var input in Inputs()) input.ValueChanged += (_, __) => Calculate();
        summary.Font = new Font("Segoe UI Semibold", 17); summary.ForeColor = HudTheme.Accent;
        body.Controls.Add(summary); body.Controls.Add(regen); body.Controls.Add(defenseGrid); body.Controls.Add(procGrid);
        body.Controls.Add(abilitySummary); body.Controls.Add(abilityGrid); body.Controls.Add(notes);
        SizeChanged += (_, __) => ResizeContent();
        VisibleChanged += (_, __) => { if (Visible) { LoadItems(); RefreshCharacters(); ResizeContent(); } };
        ResizeContent();
    }

    private static ComboBox Choice(int width, bool editable = false) => new HudComboBox { Width = width, DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
        AutoCompleteMode = editable ? AutoCompleteMode.SuggestAppend : AutoCompleteMode.None, AutoCompleteSource = editable ? AutoCompleteSource.ListItems : AutoCompleteSource.None,
        BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Font = HudTheme.BodyFont, Margin = new Padding(0, 2, 8, 3) };
    private static CheckBox Check(string text) => new HudCheckBox { Text = text, AutoSize = true, ForeColor = HudTheme.Text, Margin = new Padding(0, 5, 16, 5) };
    private static NumericUpDown Number(int value, int max, int min = 0, int decimals = 0) => new HudNumericUpDown { Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimals,
        Increment = decimals > 0 ? .1m : 1, Width = 96, BackColor = HudTheme.Input, ForeColor = HudTheme.Text, Margin = new Padding(0, 1, 8, 3), ThousandsSeparator = true };
    private static Label TextLabel(string text, bool muted = true) => new Label { Text = text, AutoSize = true, ForeColor = muted ? HudTheme.Muted : HudTheme.Text,
        Font = HudTheme.BodyFont, Margin = new Padding(0, 8, 0, 8) };
    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new HudFlowPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 4, 0, 4) };
        row.Controls.AddRange(controls); return row;
    }
    private static Control Field(string label, Control control)
    {
        var box = new HudFlowPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 8, 4) };
        box.Controls.Add(HudTheme.Label(label, true)); box.Controls.Add(control); return box;
    }
    private static DataGridView Grid(int height, params string[] headers)
    {
        var grid = HudTheme.Grid(headers); grid.Dock = DockStyle.None; grid.Height = height; grid.Margin = new Padding(0, 4, 0, 8);
        var cellStyle = grid.DefaultCellStyle.Clone(); cellStyle.WrapMode = DataGridViewTriState.True; grid.DefaultCellStyle = cellStyle;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        foreach (DataGridViewColumn col in grid.Columns) col.SortMode = DataGridViewColumnSortMode.NotSortable;
        if (headers.Last() == "Breakdown") grid.Columns[grid.Columns.Count - 1].FillWeight = 300;
        return grid;
    }
    private NumericUpDown[] Inputs() => new[] { defense, exalt, mastery, challenge, hex, casts, extraMp, petHeal, petMagic };
    private CheckBox[] Conditions() => new[] { damaging, berserk, curse, armorBroken, weak, dazed, armored, exposed, combat };
    private int[] GearIds() => equipment.Select(x => (x.SelectedItem as HudCalculatorItem)?.Id ?? -1).ToArray();
    private double[] BaseValues() => baseStats.Select(x => (double)x.Value).ToArray();
    private double[] BonusValues() => statBonus.Select(x => (double)x.Value).ToArray();
    private static void Set(NumericUpDown field, double value) => field.Value = Math.Max(field.Minimum, Math.Min(field.Maximum, (decimal)value));
    private void ResizeContent()
    {
        int width = Math.Max(320, body.ClientSize.Width - 28);
        body.SuspendLayout(); foreach (Control c in body.Controls) { c.Width = width; if (c is FlowLayoutPanel || c is Label) c.MaximumSize = new Size(width, 0); }
        body.ResumeLayout(true);
    }

    private void LoadItems()
    {
        if (loaded) return; loaded = true; updating = true;
        foreach (var combo in equipment) { combo.BeginUpdate(); combo.Items.Add(new HudCalculatorItem { Id = -1, Name = "(None)" }); }
        foreach (var item in HudCalculatorData.Items.Values.Where(x => x.Equipment).OrderBy(x => x.Name))
        {
            int slot = item.IsWeapon ? 0 : item.IsAbility ? 1 : item.IsArmor ? 2 : item.Slot == 9 ? 3 : -1;
            if (slot >= 0) equipment[slot].Items.Add(item);
        }
        foreach (var combo in equipment) { combo.SelectedIndex = 0; combo.EndUpdate(); }
        updating = false; LoadModes(); Calculate();
    }

    public void SetLive(LocalPlayerInfo value)
    {
        live = value;
        if (!Visible) return;
        LoadItems(); RefreshCharacters();
    }
    private void RefreshCharacters()
    {
        var snapshot = AccountWorkspace.Snapshot();
        if (snapshot.Revision == characterRevision && accountKey == snapshot.AccountKey) return;
        int selected = (characters.SelectedItem as CharacterChoice)?.Character.Id ?? -1;
        bool accountChanged = accountKey != snapshot.AccountKey;
        if (accountChanged)
        {
            if (!string.IsNullOrEmpty(accountKey))
            {
                bool old = updating; updating = true;
                Array.Clear(enchantCodes, 0, enchantCodes.Length); snapshotAdjustment = new double[8]; setupKey = "manual"; importedClassType = 0; challengeNotice = "";
                for (int i = 0; i < 4; i++) if (equipment[i].Items.Count > 0) equipment[i].SelectedIndex = 0;
                int[] defaults = { 700, 252, 75, 25, 50, 75, 40, 50 };
                for (int i = 0; i < 8; i++) { Set(baseStats[i], defaults[i]); statBonus[i].Value = 0; }
                foreach (var condition in Conditions()) condition.Checked = false;
                foreach (var input in Inputs()) Set(input, 0); casts.Value = 1;
                LoadModes(); updating = old;
                source.Text = "Account changed. Load a character to import its setup.";
            }
            accountKey = snapshot.AccountKey ?? ""; selected = snapshot.CurrentCharacterId;
        }
        characterRevision = snapshot.Revision; accountPets = snapshot.Pets;
        var previousPet = accountChanged ? null : (pets.SelectedItem as PetChoice)?.Pet;
        bool wasUpdating = updating; updating = true; pets.BeginUpdate(); pets.Items.Clear(); pets.Items.Add(new PetChoice());
        foreach (var pet in accountPets.OrderBy(x => x.Name)) pets.Items.Add(new PetChoice { Pet = pet });
        pets.SelectedItem = pets.Items.Cast<PetChoice>().FirstOrDefault(x => x.Pet != null && x.Pet.Id == previousPet?.Id && x.Pet.Seasonal == previousPet.Seasonal) ?? pets.Items[0];
        pets.EndUpdate(); updating = wasUpdating;
        characters.BeginUpdate(); characters.Items.Clear();
        foreach (var c in snapshot.Characters.OrderBy(x => x.Id)) characters.Items.Add(new CharacterChoice { Character = c });
        characters.SelectedItem = characters.Items.Cast<CharacterChoice>().FirstOrDefault(x => x.Character.Id == selected) ?? characters.Items.Cast<CharacterChoice>().FirstOrDefault();
        characters.EndUpdate();
        if (accountChanged) Calculate();
    }
    private void SelectGear(int[] ids, string[] codes)
    {
        for (int i = 0; i < 4; i++)
        {
            int id = ids != null && i < ids.Length ? ids[i] : -1;
            var found = equipment[i].Items.Cast<HudCalculatorItem>().FirstOrDefault(x => x.Id == id);
            if (found == null && id > 0) { found = HudCalculatorData.Item(id) ?? new HudCalculatorItem { Id = id, Name = "Unknown item " + id }; equipment[i].Items.Add(found); }
            equipment[i].SelectedItem = found ?? equipment[i].Items[0]; enchantCodes[i] = codes != null && i < codes.Length ? codes[i] : "";
        }
        LoadModes();
    }
    private void LoadModes()
    {
        bool old = updating; updating = true; mode.Items.Clear();
        if (equipment[1].SelectedItem is HudCalculatorItem item) foreach (var m in item.Modes) mode.Items.Add(m);
        if (mode.Items.Count > 0) mode.SelectedIndex = 0; updating = old;
    }
    private void UseCurrent()
    {
        LoadItems(); RefreshCharacters();
        if (live == null) { source.Text = "Connect a character to import current gear and effective stats."; return; }
        updating = true;
        var character = AccountWorkspace.CurrentCharacter;
        bool matching = character != null && character.Inventory.Take(4).SequenceEqual(live.EquipmentIds.Take(4));
        SelectGear(live.EquipmentIds, matching ? character.Enchants : null);
        double[] values = { live.BaseLife, live.BaseMana, live.BaseAttack, live.BaseDefense, live.BaseSpeed, live.BaseDexterity, live.BaseVitality, live.BaseWisdom };
        int[] effective = { live.MaxHp, live.MaxMp, live.Attack, live.Defense, live.Speed, live.Dexterity, live.Vitality, live.Wisdom };
        importedClassType = character?.ClassType ?? 0;
        var proficiency = HudCalculatorEngine.Proficiency(AccountWorkspace.Snapshot(), importedClassType);
        for (int i = 0; i < 8; i++) { Set(baseStats[i], values[i]); Set(statBonus[i], proficiency.StatBonuses[i]); }
        var calculated = HudCalculatorEngine.Stats(values, GearIds(), enchantCodes, BonusValues());
        snapshotAdjustment = Enumerable.Range(0, 8).Select(i => (double)effective[i] - calculated.Effective[i]).ToArray();
        Set(exalt, (live.ExaltationBonusDamage / 1000d - 1) * 100);
        Set(mastery, live.HasMasteryDamageBonus ? live.MasteryDamagePercent : proficiency.MasteryPercent);
        Set(challenge, live.HasChallengeDamageBonus ? (live.ChallengeDamageMultiplier - 1) * 100 : 0);
        damaging.Checked = live.Damaging; berserk.Checked = live.Berserk; weak.Checked = live.Weak; dazed.Checked = live.Dazed;
        challengeNotice = !live.HasChallengeDamageBonus && (!string.IsNullOrEmpty(live.CrucibleId) || !string.IsNullOrEmpty(live.BloodRitualId)) ? "An active challenge has no observed damage definition; enter its damage bonus explicitly." : "";
        setupKey = character == null ? "live" : "character-" + character.Id;
        LoadPet(character?.PetId ?? 0, character?.Seasonal ?? false); updating = false;
        source.Text = "Imported " + live.Name + " · live effective stats and observed damage conditions preserved. Live adjustments contain observed stat bonuses beyond modeled gear and exaltations.";
        Calculate();
    }
    private void UseCharacter()
    {
        LoadItems(); RefreshCharacters();
        if (!(characters.SelectedItem is CharacterChoice choice)) { source.Text = "Refresh account data to load saved characters."; return; }
        var c = choice.Character; updating = true; SelectGear(c.Inventory, c.Enchants);
        var snapshot = AccountWorkspace.Snapshot();
        importedClassType = c.ClassType; var proficiency = HudCalculatorEngine.Proficiency(snapshot, c.ClassType);
        for (int i = 0; i < 8; i++) { Set(baseStats[i], c.Stats.Length > i ? c.Stats[i] : 0); Set(statBonus[i], proficiency.StatBonuses[i]); }
        Set(mastery, proficiency.MasteryPercent); exalt.Value = challenge.Value = 0;
        foreach (var condition in Conditions()) condition.Checked = false;
        challengeNotice = c.Crucible ? "This character has Crucible enabled. Its cached API record does not identify a damage definition; enter the active bonus explicitly." : "";
        snapshotAdjustment = new double[8]; setupKey = "character-" + c.Id; LoadPet(c.PetId, c.Seasonal); updating = false;
        source.Text = "Imported character " + c.Id + " · API base stats, gear, class exaltations and mastery. Seasonal modifiers remain explicit in the bonus fields.";
        Calculate();
    }
    private void LoadPet(int petId, bool seasonal)
    {
        var choice = pets.Items.Cast<PetChoice>().FirstOrDefault(x => x.Pet != null && x.Pet.Id == petId && x.Pet.Seasonal == seasonal);
        pets.SelectedItem = choice ?? pets.Items[0]; SetPetLevels(choice?.Pet);
    }
    private void SetPetLevels(AccountPet pet)
    {
        petHeal.Value = petMagic.Value = 0;
        if (pet == null) return;
        int unlocked = pet.Rarity == 0 ? 1 : pet.Rarity <= 2 ? 2 : 3;
        for (int i = 0; i < Math.Min(unlocked, Math.Min(pet.AbilityTypes.Length, pet.AbilityLevels.Length)); i++)
        { if (pet.AbilityTypes[i] == 407) Set(petHeal, pet.AbilityLevels[i]); if (pet.AbilityTypes[i] == 408) Set(petMagic, pet.AbilityLevels[i]); }
    }
    private string SetupFile => "calculator-" + (string.IsNullOrEmpty(accountKey) ? "local" : accountKey) + ".json";
    private void SaveSetup()
    {
        RefreshCharacters();
        var setups = HudStorage.Load(SetupFile, new Dictionary<string, HudCalculatorSetup>()) ?? new Dictionary<string, HudCalculatorSetup>();
        var pet = (pets.SelectedItem as PetChoice)?.Pet;
        setups[setupKey] = new HudCalculatorSetup { Base = BaseValues(), Bonus = BonusValues(), SnapshotAdjustment = snapshotAdjustment,
            Equipment = GearIds(), Enchants = (string[])enchantCodes.Clone(), Mode = mode.SelectedIndex, Inputs = Inputs().Select(x => (double)x.Value).ToArray(), Conditions = Conditions().Select(x => x.Checked).ToArray(), PetId = pet?.Id ?? 0, PetSeasonal = pet?.Seasonal ?? false, ClassType = importedClassType };
        HudStorage.Save(SetupFile, setups); source.Text = HudStorage.LastError ?? "Saved calculator setup for " + setupKey + ".";
    }
    private void RestoreSetup()
    {
        LoadItems(); var setups = HudStorage.Load(SetupFile, new Dictionary<string, HudCalculatorSetup>()) ?? new Dictionary<string, HudCalculatorSetup>();
        if (!setups.TryGetValue(setupKey, out var s) || s == null) { source.Text = "No saved setup for " + setupKey + "."; return; }
        updating = true;
        SelectGear(s.Equipment, s.Enchants);
        importedClassType = s.ClassType; challengeNotice = "";
        LoadPet(s.PetId, s.PetSeasonal);
        for (int i = 0; i < 8; i++) { Set(baseStats[i], s.Base != null && i < s.Base.Length ? s.Base[i] : 0); Set(statBonus[i], s.Bonus != null && i < s.Bonus.Length ? s.Bonus[i] : 0); }
        snapshotAdjustment = s.SnapshotAdjustment?.Length == 8 ? s.SnapshotAdjustment : new double[8];
        var inputs = Inputs(); for (int i = 0; i < Math.Min(inputs.Length, s.Inputs?.Length ?? 0); i++) Set(inputs[i], s.Inputs[i]);
        var conditions = Conditions(); for (int i = 0; i < conditions.Length; i++) conditions[i].Checked = s.Conditions != null && i < s.Conditions.Length && s.Conditions[i];
        if (s.Mode >= 0 && s.Mode < mode.Items.Count) mode.SelectedIndex = s.Mode;
        updating = false; source.Text = "Restored calculator setup for " + setupKey + "."; Calculate();
    }

    private void Calculate()
    {
        if (updating || !loaded) return;
        var stats = HudCalculatorEngine.Stats(BaseValues(), GearIds(), enchantCodes, BonusValues(), snapshotAdjustment);
        statGrid.Rows.Clear(); for (int i = 0; i < 8; i++) statGrid.Rows.Add(HudCalculatorEngine.StatNames[i], baseStats[i].Value, stats.Gear[i].ToString("+0.##;-0.##;0"), statBonus[i].Value, snapshotAdjustment[i].ToString("+0.##;-0.##;0"), stats.Effective[i]);
        var weaponItem = equipment[0].SelectedItem as HudCalculatorItem;
        double rangeMultiplier = (1 + (double)mastery.Value / 100) * (1 + (double)challenge.Value / 100);
        var weapon = weaponItem?.Id > 0 ? HudCalculatorEngine.Weapon(weaponItem, enchantCodes[0], rangeMultiplier, stats.RateMultiplier) : null;
        double dex = dazed.Checked ? 0 : stats.Effective[5];
        double shots = weapon == null ? 0 : (1.5 + 6.5 * dex / 75) * (berserk.Checked && !dazed.Checked ? 1.25 : 1) * weapon.RateOfFire;
        var procs = HudCalculatorEngine.WeaponProcs(weaponItem, stats.Effective, shots);
        double procDps = procs.Where(x => !x.Conditional).Sum(x => x.Damage);
        double Dps(double def)
        {
            if (weapon == null) return 0;
            return HudCalculatorEngine.WeaponDpsWithConditions(weapon, stats.Effective[2], stats.Effective[5], def, (double)exalt.Value,
                damaging.Checked, berserk.Checked, curse.Checked, armorBroken.Checked, weak.Checked, dazed.Checked, armored.Checked, exposed.Checked);
        }
        summary.Text = (Dps((double)defense.Value) + procDps).ToString("N0") + " theoretical weapon DPS  ·  " + shots.ToString("0.##") + " volleys/s";
        defenseGrid.Rows.Clear(); foreach (int def in new[] { 0, 20, 40, 60, 80, 100, 150, 200, (int)defense.Value }.Distinct().OrderBy(x => x))
            defenseGrid.Rows.Add(def, Dps(def).ToString("N1"), procDps.ToString("N1"), (Dps(def) + procDps).ToString("N1"));
        procGrid.Rows.Clear(); foreach (var p in procs) procGrid.Rows.Add(p.Name, p.Conditional ? "Conditional" : p.Damage.ToString("N1"), p.Detail);
        procGrid.Visible = procs.Count > 0;
        double combatFactor = combat.Checked ? .5 : 1;
        double hpRegen = (stats.HpStatRegen + HudCalculatorEngine.PetThroughput(407, (int)petHeal.Value)) * combatFactor + stats.HpGearRegen;
        double mpRegen = (stats.MpStatRegen + HudCalculatorEngine.PetThroughput(408, (int)petMagic.Value)) * combatFactor + stats.MpGearRegen + (double)extraMp.Value;
        var selectedPet = (pets.SelectedItem as PetChoice)?.Pet; double petDps = 0;
        if (selectedPet != null)
        {
            int unlocked = selectedPet.Rarity == 0 ? 1 : selectedPet.Rarity <= 2 ? 2 : 3;
            for (int i = 0; i < Math.Min(unlocked, Math.Min(selectedPet.AbilityTypes.Length, selectedPet.AbilityLevels.Length)); i++)
                if (new[] { 402, 404, 405, 406 }.Contains(selectedPet.AbilityTypes[i])) petDps += HudCalculatorEngine.PetThroughput(selectedPet.AbilityTypes[i], selectedPet.AbilityLevels[i]);
        }
        regen.Text = hpRegen.ToString("0.##") + " HP/s  ·  " + mpRegen.ToString("0.##") + " MP/s  ·  combat trigger " + HudCalculatorEngine.CombatTrigger(stats.Effective[3]) + " damage  ·  " + (combat.Checked ? "in-combat regen" : "out-of-combat regen")
            + "\nGear bonuses: loot " + stats.LootBonus.ToString("0.##") + "% · XP " + stats.XpBonus.ToString("0.##") + "% · dust " + stats.DustBonus.ToString("0.##") + "%. Linked pet: " + petDps.ToString("N0") + " estimated damage/s before DEF; level interpolation and Electric at high levels are approximate.";
        if (importedClassType > 0)
        {
            var proficiency = HudCalculatorEngine.Proficiency(AccountWorkspace.Snapshot(), importedClassType);
            regen.Text += "\nClass proficiency: +" + proficiency.XpPercent.ToString("0.#") + "% XP · +" + proficiency.LootPercent.ToString("0.#") + "% loot · −" + proficiency.ArmorCombatReduction.ToString("0.#") + "s in combat.";
        }
        var abilityItem = equipment[1].SelectedItem as HudCalculatorItem; var selectedMode = mode.SelectedItem as HudCalculatorMode;
        var ability = HudCalculatorEngine.Ability(abilityItem, selectedMode, stats.Effective, (int)hex.Value, enchantCodes[1], (double)defense.Value);
        double cost = Math.Max(0, (selectedMode?.MpCost ?? 0) * stats.MpMultiplier), cooldown = Math.Max(0, selectedMode?.Cooldown ?? 0);
        double castRate = selectedMode == null ? 0 : HudCalculatorEngine.CastRate((double)casts.Value, cooldown, cost, stats.Effective[1], mpRegen);
        abilitySummary.Text = selectedMode == null ? "Choose an ability." : ability.DamagePerUse.ToString("N0") + " ability potential / use against one target  ·  " + ability.MultiTargetDamage.ToString("N0") + " across all targets"
            + "\n" + cost.ToString("0.##") + " MP/use · " + cooldown.ToString("0.##") + "s declared cooldown · " + castRate.ToString("0.###") + " sustained casts/s · " + (ability.DamagePerUse * castRate).ToString("N0") + " potential damage/s before target defense and conditions.";
        abilityGrid.Rows.Clear(); foreach (var line in ability.Lines) abilityGrid.Rows.Add(line.Name, line.Conditional ? "Conditional" : line.Damage > 0 ? line.Damage.ToString("N0") : "—",
            line.MultiTargetDamage > 0 ? line.MultiTargetDamage.ToString("N0") : "—", line.Detail);
        var limitations = new List<string> { "Weapon DPS assumes every projectile hits. Ability totals are potential before target defense and conditions, with every component landing; they are shown separately from weapon DPS. Cast rate is limited by declared cooldown, maximum MP and sustained regeneration; existing mana reserves only affect bursts. Buff uptime, set synergies and conditional procs are not assumed. Native poison/bleeding proc estimates use neutral target conditions." };
        if (!string.IsNullOrEmpty(HudCalculatorData.Status)) limitations.Add(HudCalculatorData.Status);
        if (!string.IsNullOrEmpty(challengeNotice)) limitations.Add(challengeNotice);
        limitations.AddRange(stats.Notes); limitations.AddRange(ability.Notes);
        notes.Text = string.Join("\n", limitations.Distinct()); ResizeContent();
    }
}
