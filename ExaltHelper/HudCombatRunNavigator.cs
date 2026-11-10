using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ExaltHelper;

internal sealed class HudCombatRun
{
    public long StartTicks;
    public string Dungeon;
    public override string ToString() => StartTicks == HudCombatRunNavigator.AllRuns ? "All recorded runs"
        : StartTicks == HudCombatRunNavigator.LiveRun ? "Live · " + Dungeon
        : new DateTime(StartTicks, DateTimeKind.Local).ToString("yyyy-MM-dd HH:mm:ss") + " · " + Dungeon;
}

internal sealed class HudCombatRunNavigator : TableLayoutPanel
{
    internal const long AllRuns = -1, LiveRun = 0;
    internal readonly Button Previous = HudTheme.Button("<"), Next = HudTheme.Button(">"), Live = HudTheme.Button("Live");
    internal readonly HudComboBox Runs = new HudComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
        DropDownWidth = 640, AccessibleName = "Dungeon visit" };
    private readonly bool allowAll;
    private bool updating;
    internal long SelectedStartTicks => (Runs.SelectedItem as HudCombatRun)?.StartTicks ?? DefaultSelection;
    private long DefaultSelection => allowAll ? AllRuns : LiveRun;
    internal event Action SelectionChanged;

    internal HudCombatRunNavigator(bool allowAll)
    {
        this.allowAll = allowAll;
        Dock = DockStyle.Top; Height = 44; ColumnCount = 4; RowCount = 1;
        BackColor = HudTheme.Background; Font = HudTheme.BodyFont;
        AccessibleName = "Dungeon history navigation";
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        Previous.AccessibleName = "Previous dungeon run"; Next.AccessibleName = "Next dungeon run";
        Live.AccessibleName = "Return to live dungeon";
        foreach (Control control in new Control[] { Previous, Next, Runs, Live })
        {
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 3, 6, 8);
            if (control is Button button) { button.AutoSize = false; button.MinimumSize = Size.Empty; button.Padding = Padding.Empty; }
            Controls.Add(control);
        }
        Previous.Click += (_, __) => MoveRun(1); Next.Click += (_, __) => MoveRun(-1);
        Live.Click += (_, __) => SelectRun(LiveRun, true);
        Runs.SelectedIndexChanged += (_, __) => { UpdateButtons(); if (!updating) SelectionChanged?.Invoke(); };
    }

    internal void SetRuns(IEnumerable<HudCombatRun> history, string liveDungeon, bool reset)
    {
        long selected = reset ? DefaultSelection : SelectedStartTicks;
        var choices = new List<HudCombatRun>();
        if (allowAll) choices.Add(new HudCombatRun { StartTicks = AllRuns });
        choices.Add(new HudCombatRun { StartTicks = LiveRun, Dungeon = liveDungeon ?? "Waiting for dungeon" });
        choices.AddRange(history.OrderByDescending(run => run.StartTicks));
        // Stable identities keep repeat visits separate and prevent incoming live
        // ticks from resetting the native dropdown while the user is browsing.
        if (!Runs.Items.Cast<HudCombatRun>().Select(run => run.StartTicks + ":" + run.Dungeon).SequenceEqual(choices.Select(run => run.StartTicks + ":" + run.Dungeon)))
        {
            updating = true; Runs.BeginUpdate();
            try { Runs.Items.Clear(); Runs.Items.AddRange(choices.Cast<object>().ToArray()); SelectRun(selected, false); }
            finally { Runs.EndUpdate(); updating = false; }
        }
        else if (reset) SelectRun(selected, false);
        UpdateButtons();
    }

    internal void SelectRun(long ticks, bool notify)
    {
        bool wasUpdating = updating; updating = true;
        var item = Runs.Items.Cast<HudCombatRun>().FirstOrDefault(run => run.StartTicks == ticks)
            ?? Runs.Items.Cast<HudCombatRun>().FirstOrDefault(run => run.StartTicks == DefaultSelection);
        try { Runs.SelectedItem = item; }
        finally { updating = wasUpdating; }
        UpdateButtons();
        if (notify) SelectionChanged?.Invoke();
    }

    private void MoveRun(int direction)
    {
        var visits = Runs.Items.Cast<HudCombatRun>().Where(run => run.StartTicks != AllRuns).ToArray();
        int index = Array.FindIndex(visits, run => run.StartTicks == SelectedStartTicks);
        int desired = index < 0 ? 1 : index + direction;
        if (desired >= 0 && desired < visits.Length) SelectRun(visits[desired].StartTicks, true);
    }

    private void UpdateButtons()
    {
        var visits = Runs.Items.Cast<HudCombatRun>().Where(run => run.StartTicks != AllRuns).ToArray();
        int index = Array.FindIndex(visits, run => run.StartTicks == SelectedStartTicks);
        Previous.Enabled = visits.Length > 1 && index < visits.Length - 1;
        Next.Enabled = index > 0;
        Live.Enabled = SelectedStartTicks != LiveRun;
    }
}
