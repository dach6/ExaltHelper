ExaltHelper 1.1.3 adds dungeon visit navigation to the expanded combat HUD for **RotMG Exalt 7.2.0.1.0**.

Download **ExaltHelper-1.1.3-Release.zip**, extract the entire ZIP to a writable folder, and run **ExaltHelper.exe**. Restart ExaltHelper after updating. Requires Windows, .NET Framework 4.8, and an installed RotMG Exalt client. Keep the assets, data folders, configuration, and DLLs beside the executable.

- Browse previous dungeon visits with the **< / >** arrows, select a specific visit from the dated dropdown, or return to **Live** on either expanded combat page.
- Keep repeat visits to the same dungeon separate, with each monster's own inline player damage ranking and full four-slot gear display. Bosses remain first.
- Open **Combat history** on **All recorded runs**, including saved visits from previous sessions. Recording filters and history retention settings continue to apply.
- Keep the selected historical visit visible during Live combat updates and new dungeon arrivals. Explicit compact HUD navigation also updates the expanded selection.
- Preserve the current game data, launcher handshake, monster portraits, portal artwork and equipment icons.

**Source:** ExaltHelper-1.1.3-Source.zip includes the release's source, required build inputs, documentation and tests. SHA256SUMS.txt provides checksums for both ZIP downloads.

**Validation:** The Release build succeeded. All 45 new dungeon-navigation assertions passed, along with the combat tracker, expanded dashboard, history refresh, journal, combat integration and account isolation suites. The extracted application package was checked against the bundled game assets. Live game sessions were not used for these checks.

See the [HUD guide](https://github.com/dach6/ExaltHelper/blob/v1.1.3/docs/HUD-GUIDE.md) and [source changes](https://github.com/dach6/ExaltHelper/compare/v1.1.2...v1.1.3).
