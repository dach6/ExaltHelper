ExaltHelper 1.1.2 refines the expanded combat tracker for **RotMG Exalt 7.2.0.1.0**.

Download **ExaltHelper-1.1.2-Release.zip**, extract the entire ZIP to a writable folder, and run **ExaltHelper.exe**. Restart ExaltHelper after updating. Requires Windows, .NET Framework 4.8, and an installed RotMG Exalt client. Keep the assets, data folders, configuration, and DLLs beside the executable.

- Click a combat-history encounter to expand its per-monster player damage ranking inline; contributor details no longer require a separate window.
- Keep all four equipment slots visible for every tracked player, with explicit empty-slot markers and bundled item artwork.
- Put boss encounters first in the live and historical combat tracker, preserve same-named monsters as separate encounters, and keep the newest defeated monster available as the default DPS target.
- Retain the launcher handshake and bundled game data for game version 7.2.0.1.0 from ExaltHelper 1.1.1.

**Source:** ExaltHelper-1.1.2-Source.zip contains this release's source, required build inputs, documentation, and tests. SHA256SUMS.txt provides checksums for both ZIP downloads.

**Validation:** The Release build completed with zero errors and 49 existing warnings. The combat tracker, expanded dashboard, overlay scrolling, item presentation, history refresh, DPS tracking/accounting, dashboard combat integration, and player roster suites passed. Offline fixtures do not replace authenticated live-game validation or verification of game-specific native hook mappings.

See the [HUD guide](https://github.com/dach6/ExaltHelper/blob/v1.1.2/docs/HUD-GUIDE.md) and [source changes](https://github.com/dach6/ExaltHelper/compare/v1.1.1...v1.1.2).
