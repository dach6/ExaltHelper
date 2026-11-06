ExaltHelper 1.1.1 updates the launcher and bundled game data for **RotMG Exalt 7.2.0.1.0**.

Download **ExaltHelper-1.1.1-Release.zip**, extract the entire ZIP to a writable folder, and run **ExaltHelper.exe**. Restart ExaltHelper after updating. Requires Windows, .NET Framework 4.8, and an installed RotMG Exalt client. Keep the assets, data folders, configuration, and DLLs beside the executable.

- Preserves extracted asset line endings in source archives and Git checkouts so packaged checksums remain valid.
- Updates the launcher handshake and compiled fallback to game version 7.2.0.1.0.
- Refreshes the Ocean Trench Key definition with dungeon difficulty 5 and the game's warning that keys require 10 or more stars.
- Refreshes game-source fingerprints and packaged checksums. The extracted sprite atlases, object/tile lists, ground definitions, enchantments, modifiers, and forge data match the previous release.

**Source:** ExaltHelper-1.1.1-Source.zip contains this release's source, required build inputs, documentation, and tests. SHA256SUMS.txt provides checksums for both ZIP downloads.

**Validation:** Release build succeeded with zero errors and 49 existing warnings. All 14 selected offline regression suites passed, including 150 asset assertions against the installed 7.2.0.1.0 game sources. Packet forwarding, callouts, feed/key observations, calculators, DPS, combat stats, player metadata, item presentation, accounts, and missions passed. Authenticated gameplay and the supplied native hook's game-specific mappings remain unverified.

See the [HUD guide](https://github.com/dach6/ExaltHelper/blob/v1.1.1/docs/HUD-GUIDE.md) and [source changes](https://github.com/dach6/ExaltHelper/compare/v1.1.0...v1.1.1).
