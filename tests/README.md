# Tests

Build `ExaltHelper.csproj -c Release` first. Each script defaults to `../bin/Release/net48/ExaltHelper.exe` relative to its own location and accepts `-AssemblyPath` for a different build. Run scripts in separate Windows PowerShell processes so each reflection harness loads the intended assembly.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-LethalStrikeReplay.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-PackagedAssets.ps1
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/Test-OverlayScrolling.ps1
```

| Script | Coverage |
| --- | --- |
| `Test-AbilityPackets.ps1` | Byte-preserving projectile forwarding, optional fields, shot counts, and trailing data |
| `Test-OverlayScrolling.ps1` | Mouse wheel, track clicks, thumb dragging, capture, lock mode, resizing, and filtering across six lists; requires `-STA` |
| `Test-DpsTracking.ps1` | Projectile attribution, scaling, duplicate reports, summons, and event modifiers |
| `Test-DpsAccounting.ps1` | Server corrections, zero damage, pellets, summons, guard accounting, blocked hits, and RNG seeds |
| `Test-DpsModifiers.ps1` | Enchantments, party/event bonuses, late monster metadata, completed fight timing, and background trace saving |
| `Test-CombatStats.ps1` | Numeric stat packets, temporary vitality boosts, skin independence, stat displays, and server reconciliation |
| `Test-CombatDiagnostics.ps1` | Combat snapshots, boost payloads, trace retention, and bounded diagnostic output |
| `Test-LethalStrikeReplay.ps1` | Darkened Sun combat fixture, target defense, vitality snapshots, duplicate suppression, and server corrections |
| `Test-PackagedAssets.ps1` | Monster names, Fractal projectile definitions, forge data, equipment icons, and concurrent metadata initialization |
| `Test-GameAssetBundle.ps1` | Release provenance, asset checksums, embedded and external catalog agreement, launcher version and fallback, complete XML loading, release-specific item metadata, and sprite bounds/artwork |
| `Test-PlayerRoster.ps1` | Player roster, shiny labels, and forge rarities |
| `Test-MvDetection.ps1` | Moonlight Village encounters and the bundled NPC dialogue fixture; accepts `-SniffLog` |
| `Test-StatDataProtocol.ps1` | Independent compressed secondary-stat vectors, following-stat alignment, full Update/NewTick forwarding, large stack counts, and unchanged generated sentinel bytes |
| `Test-DashboardInfrastructure.ps1` | Detached passive packet payloads, independent MapInfo maximum/current score vectors and incomplete suffix preservation, malformed-packet forwarding, atomic/corrupt/concurrent persistence, search/date/category filters and table selection |
| `Test-DashboardJournal.ps1` | Chat classification, account-scoped durable histories, repeated snapshot deduplication, corrections, pending-account observations, and observation during blocked persistence |
| `Test-DashboardCombatIntegration.ps1` | Actual tracker-to-journal projectile estimates/corrections, pellets, summons, immutable stat context, zero rejection, boss phases, damage taken and map transitions |
| `Test-CombatTrackerView.ps1` | Per-monster simultaneous-target attribution, independent rankings/DPS/share/hits, journal retention, game sprites, expansion/search/filter/keyboard behavior, stable refresh state, themes and narrow dashboard rendering; requires `-STA` and desktop access |
| `Test-CombatPolicy.ps1` | All twelve combat capture groups, object-ID/anchor/dungeon classification precedence, solo/group changes, capture-time filtering, difficulty boundaries and preference persistence |
| `Test-DashboardIsolation.ps1` | Offline HELLO/account transitions, pending identity, stale-client chat/snapshot/source rejection, durable separation and unchanged forwarded bytes; API refresh is explicitly suppressed |
| `Test-DashboardFeed.ps1` | Actual portal observations and byte-preserving portal-use routing, captured origin and join deadlines, distinct dungeon modifier learning, independent whole-bag feed, dust-cap crossings and key-opening attribution |
| `Test-DashboardRetention.ps1` | Real loot-handler map/account provenance, independent feed before history filters/recording, reused entity IDs, all recognized bag IDs, public-bag rules, bounded history with durable lifetime collections/deduplication, and dungeon-run aggregation |
| `Test-DashboardCalculator.ps1` | Independent theoretical DPS arithmetic for exclusive maximum rolls, integer truncation, defense floor, curse, armor piercing and buffs |
| `Test-ExpandedCalculator.ps1` | Independent weapon/proc/ability/stat/regen/pet/exaltation calculations, item and ability mode coverage, live inputs, per-character overrides, and calculator UI |
| `Test-Callouts.ps1` | Dungeon/event abbreviations and overrides, packaged modifier definitions, reward tags and thresholds, event chains, join/origin rules, unknown modifiers, roster and thank-you text |
| `Test-NotificationSounds.ps1` | Opt-in category routing, real packet hooks, PCM validation/volume scaling, custom WAV files, bounded asynchronous queue, rate limits, durable settings UI and shutdown; uses silent playback substitution and requires `-STA` |
| `Test-ExpandedDashboard.ps1` | All expanded pages, packaged Trophy Hall data, cloned-record selection, themes/widgets, durable callout controls and quick text, automatic clipboard ownership/expiry/account transitions, equivalent UTC/local history dates, narrow layout, monitor geometry, background delivery and clean shutdown; requires `-STA` and saves screenshots |
| `Test-HudRepainting.ps1` | Custom HUD text clipping and translated drawing, partial repaint reconstruction, and native control background clearing; requires `-STA` and saves comparison images |
| `Test-HudItemPresentation.ps1` | Item sprites, rarity, stats/effect details, individual hover targets, unknown-item fallback, sorting, compact/light styling, bounded caches and shared-image ownership; requires `-STA` |
| `Test-CharacterDetailRefresh.ps1` | Character equipment scrolling and control identity during live refreshes, updated stats/inventory, selection boundaries and detail state; requires `-STA` |
| `Test-LootBagGrouping.ps1` | Bag identity, trustworthy legacy records, distinct slots/enchantments, filters, details, journal deduplication and collection counts; requires `-STA` |
| `Test-HudHistoryRefresh.ps1` | Per-page journal revisions, detached page snapshots, stable native rows, filters/selection/sorting/tooltips, midnight date-filter expiry and recorded versus legacy feed rarity; requires `-STA` |
| `Test-HudRefreshPerformance.ps1` | Hidden-dashboard behavior, account summary/count caches, inventory and account changes, task deadlines and repeated inventory hover; also emits synthetic timing/allocation measurements and requires `-STA` |
| `Test-AccountWorkspace.ps1` | Account XML, live storage/character/pet observations, mode isolation, inventory aggregation, fame and DPS calculations, cached UI and persistence; includes `Test-AccountWorkspaceParity.ps1` |
| `Test-AccountWorkspaceParity.ps1` | Supplemental character labels/order/graveyard, account boosts, derived combat, Treasury section/category order and collapse assertions; invoked by the account workspace suite |
| `Test-Missions.ps1` | Mission/quest packet observations, real season fixture, AND/OR progress, prerequisites, eligibility, daily freshness, merged taskbar objectives, preferences, UI, and nonblocking persistence/retry/startup merges |

Run `Test-PackagedAssets.ps1` against a clean extracted release to check its bundled files. `-NamesOnly` checks embedded names using an executable without external assets, and `-ScreenshotPath <png>` saves an equipment image.

`Test-GameAssetBundle.ps1` defaults to the manifest at `assets/game-assets.json`
beside the executable. Supply `-SourceDirectory` pointing to the installed game's
`RotMG Exalt_Data` folder to also verify the original resource fingerprints and
the game-version bytes recorded in the manifest. Without that argument, the test
verifies the packaged bundle and records the source-provenance verification limit.

`Test-ExpandedDashboard.ps1` also checks that existing even and odd table rows
keep readable text and selection contrast after switching between Light, Slate
and Midnight. This catches framework versions that return detached cell styles
and require assigning the updated style back to the grid.

`Test-HudRepainting.ps1` exercises drawing into reused buffers and translated,
clipped graphics contexts. It checks native painting as well as individual
controls, catching stale overlapping labels that clean full-frame screenshots
can miss. These checks do not capture physical monitor output.

The refresh benchmarks use isolated synthetic histories and account inventories.
Their timings measure HUD processing and allocation, not game FPS. Run timing
comparisons after item assets have loaded and without concurrent test suites.

`Test-OverlayScrolling.ps1 -ScreenshotDirectory <path>` saves rendered examples. Use a directory under `artifacts/` to keep them out of version control. The test creates an off-screen HUD without starting a game session.

The dashboard suites use isolated HUD data folders. `Test-ExpandedDashboard.ps1`
runs in an application domain rooted at the executable's output directory so
packaged catalog and asset paths match normal application startup. When running
older regression scripts against the expanded application, set
`EXALTHELPER_HUD_DATA` to a disposable test directory before launching PowerShell.
Synthetic packet, account and screen fixtures do not exercise an authenticated
live game session or physically moving a window between real monitors. Sound
tests substitute playback to verify generated samples and routing without
making noise; they do not establish native audio-device audibility.

The fixtures contain numeric combat inputs or NPC dialogue and session markers. They exclude personal chat, account details, and raw packet captures. The Lethal Strike replay includes an independent Tomato formula comparison; its reference totals do not establish server-confirmed damage for every hit.
