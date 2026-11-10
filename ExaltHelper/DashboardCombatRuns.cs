using System;
using System.Collections.Generic;
using System.Linq;
using ExaltHelper.Proxy.DataStructures;
using ExaltHelper.Proxy.Mods;

namespace ExaltHelper;

internal partial class ExaltDashboardForm
{
    private HudCombatRunNavigator combatTrackerRuns, combatHistoryRuns;
    private string combatRunScope;
    private int combatCatalogRevision = -1, compactSelectionRevision = -1;
    private List<HudCombatRun> recordedCombatRuns = new List<HudCombatRun>();
    private HudFightRecord[] combatHistoryRecords = Array.Empty<HudFightRecord>();

    private HudCombatRunNavigator AddCombatRunNavigation(HudCombatTrackerPage page, bool allowAll)
    {
        compactSelectionRevision = -1;
        var navigator = new HudCombatRunNavigator(allowAll);
        navigator.SelectionChanged += () => {
            long ticks = navigator.SelectedStartTicks;
            if (ticks != HudCombatRunNavigator.AllRuns)
            {
                // Match the compact HUD by stable start time, since its archive
                // indices change when older session snapshots are evicted.
                combatTrackerRuns?.SelectRun(ticks, false);
                combatHistoryRuns?.SelectRun(ticks, false);
                if (ticks == HudCombatRunNavigator.LiveRun) DpsTrackerMod.NavigateLiveDungeon();
                else DpsTrackerMod.SelectDungeonByStartTime(new DateTime(ticks));
                compactSelectionRevision = DpsTrackerMod.DungeonSelectionRevision;
            }
            RefreshLivePageData();
        };
        page.Controls.Add(navigator);
        return navigator;
    }

    private void RefreshCombatRunNavigation()
    {
        string scope = HudJournal.ScopeId;
        bool reset = combatRunScope != scope;
        if (reset)
        {
            if (combatRunScope != null) snapshot = HudJournal.LiveSnapshot;
            combatRunScope = scope; combatCatalogRevision = compactSelectionRevision = -1;
            recordedCombatRuns.Clear(); combatHistoryRecords = Array.Empty<HudFightRecord>();
        }
        int revision = HudJournal.PageRevision("combat");
        if (combatCatalogRevision != revision)
        {
            recordedCombatRuns = HudJournal.CombatRuns(); combatCatalogRevision = revision;
        }
        var session = DpsTrackerMod.GetDungeonHistoryList();
        var live = CurrentLiveSnapshot;
        var runs = recordedCombatRuns.Concat(session.Where(run => run.Index >= 0)
                .Select(run => new HudCombatRun { StartTicks = run.StartTime.Ticks, Dungeon = run.DungeonName }))
            .Where(run => run.StartTicks > 0 && run.StartTicks != live.DungeonStartTime.Ticks)
            .GroupBy(run => run.StartTicks).Select(group => group.First()).ToArray();
        combatTrackerRuns?.SetRuns(runs, live.DungeonName, reset);
        combatHistoryRuns?.SetRuns(runs, live.DungeonName, reset);

        int selectionRevision = DpsTrackerMod.DungeonSelectionRevision;
        long selected = session.Where(run => run.IsCurrent).Select(run => run.Index < 0 ? 0 : run.StartTime.Ticks).DefaultIfEmpty(0).First();
        // Only explicit compact navigation should replace a selected visit.
        // A map transition resets compact to Live automatically; browsing an
        // older expanded run must survive that transition and live updates.
        bool followCompact = compactSelectionRevision < 0 ? selected > 0 : compactSelectionRevision != selectionRevision;
        if (followCompact && (selected == 0 || runs.Any(run => run.StartTicks == selected)))
        {
            combatTrackerRuns?.SelectRun(selected, false);
            combatHistoryRuns?.SelectRun(selected, false);
        }
        compactSelectionRevision = selectionRevision;
    }

    private void RefreshCombatRunPage()
    {
        RefreshCombatRunNavigation();
        bool historyPage = SelectedPage == "combat";
        var page = historyPage ? combatHistoryTracker : combatTracker;
        var navigator = historyPage ? combatHistoryRuns : combatTrackerRuns;
        if (page == null || navigator == null) return;
        long ticks = navigator.SelectedStartTicks;
        if (historyPage && combatGrouping != null) combatGrouping.Enabled = ticks == HudCombatRunNavigator.AllRuns;
        if (ticks == HudCombatRunNavigator.AllRuns)
        {
            page.SetHistory(combatHistoryRecords, "All recorded runs");
            return;
        }
        if (ticks == HudCombatRunNavigator.LiveRun)
        {
            var live = CurrentLiveSnapshot;
            page.SetLive(live, HudJournal.ScopeId, HudJournal.SnapshotForRun(live.DungeonStartTime));
            return;
        }
        var start = new DateTime(ticks);
        var saved = HudJournal.SnapshotForRun(start);
        var archived = DpsTrackerMod.ArchivedDungeonSnapshot(start);
        if (archived != null) page.SetArchived(archived, HudJournal.ScopeId, saved);
        else page.SetHistory(saved, recordedCombatRuns.FirstOrDefault(run => run.StartTicks == ticks)?.Dungeon ?? "Recorded dungeon");
    }
}
