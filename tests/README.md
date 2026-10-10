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
| `Test-PlayerRoster.ps1` | Player roster, shiny labels, and forge rarities |
| `Test-MvDetection.ps1` | Moonlight Village encounters and the bundled NPC dialogue fixture; accepts `-SniffLog` |

Run `Test-PackagedAssets.ps1` against a clean extracted release to check its bundled files. `-NamesOnly` checks embedded names using an executable without external assets, and `-ScreenshotPath <png>` saves an equipment image.

`Test-OverlayScrolling.ps1 -ScreenshotDirectory <path>` saves rendered examples. Use a directory under `artifacts/` to keep them out of version control. The test creates an off-screen HUD without starting a game session.

The fixtures contain numeric combat inputs or NPC dialogue and session markers. They exclude personal chat, account details, and raw packet captures. The Lethal Strike replay includes an independent Tomato formula comparison; its reference totals do not establish server-confirmed damage for every hit.
