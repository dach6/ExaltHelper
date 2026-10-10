# ExaltHelper

**Version 1.0.0**

ExaltHelper is a Windows companion for Realm of the Mad God Exalt. It combines an integrated combat HUD, launcher and account tools, and configurable gameplay utilities in one application.

## Features

- Track DPS, total damage, and encounter history, with weapon, ability, and summon attribution.
- Inspect players and equipment, view ability logs, and follow party and loot activity.
- Track Moonlight Village encounters and phases.
- Move and resize the HUD, toggle click-through, and navigate lists with the mouse wheel or draggable scrollbars.
- Launch the official or Steam client and manage multiple accounts.
- Configure reconnect tools, automation, hotkeys, and effect filters.
- Display bundled monster names, equipment icons, forge rarities, and enchantment data.

## Getting started

Requirements: Windows, .NET Framework 4.8, and an installed Realm of the Mad God Exalt client.

1. Download `ExaltHelper-Release.zip` from the repository's Releases page.
2. Extract the entire ZIP to a writable folder.
3. Open `ExaltHelper.exe` and accept the administrator prompt.
4. Select your Exalt launcher in the application and locate it when prompted.
5. Launch the game through ExaltHelper and configure the features you want in Settings.

Keep `assets/`, `ExaltHelper_Data/`, the DLLs, and `ExaltHelper.exe.config` beside the executable. Run the application from the extracted folder.

## HUD controls

| Control | Action |
| --- | --- |
| **F8** | Show or hide the HUD |
| **F9** | Toggle lock/click-through mode |
| Drag grips | Move the unlocked HUD |
| Bottom edge | Resize the unlocked HUD vertically |
| Scrollbars | Click the track, drag the thumb, or use the mouse wheel |

Unlock the HUD to use its controls and scroll lists. Use windowed or borderless game mode so the overlay can appear above the client.

## Damage tracking

The tracker reconstructs damage from combat packets, equipped items, received stats, and available buff definitions. Matching server reports correct local estimates. Unconfirmed hits remain estimates, and totals can be affected by visibility, missing packets, unavailable event definitions, or game updates.

Crucible and Blood Ritual bonuses use definitions received from the game. If the HUD flags missing bonus definitions, open the relevant event panels while tracking is active.

Combat diagnostics are saved to `Diagnostics/dps-latest.csv`, with up to 30 traces retained in `Diagnostics/History/`. Each trace contains up to 20,000 recent combat events. These files contain numeric combat data and exclude account details, player names, chat, and raw packets. When reporting a damage issue, include the relevant trace and your equipment and active buffs.

## Build from source

Use a current .NET SDK on Windows and the .NET Framework 4.8 reference assemblies, available through the Developer Pack or an initial NuGet restore. The project builds with .NET SDK 10.0.401.

```powershell
dotnet build ExaltHelper.csproj -c Release
```

Run `bin/Release/net48/ExaltHelper.exe`. The build copies the required dependencies, game data, and assets to the output folder. Visual Studio with the .NET desktop development workload can also open `ExaltHelper.sln`.

`lib/`, `CompiledResources/`, and `ExaltHelper_Data/` contain required build inputs. The native hook, `ExaltHelper_Data/version.dll`, is supplied as a binary; its source is not included.

## Tests

See [tests/README.md](tests/README.md) for packet, damage, metadata, encounter, and UI checks. For example, after building:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-LethalStrikeReplay.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-PackagedAssets.ps1
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/Test-OverlayScrolling.ps1
```

Each script accepts `-AssemblyPath` to check a compiled release. Tests use offline fixtures and do not require a game account.

## Source layout

| Path | Contents |
| --- | --- |
| `ExaltHelper/` | Application UI, HUD, launcher, and settings |
| `ExaltHelper.Proxy/` | Proxy server and client sessions |
| `ExaltHelper.Proxy.Networking*` | Packet encoding and definitions |
| `ExaltHelper.Proxy.DataStructures/` | Game data and tracking models |
| `ExaltHelper.Proxy.Helpers/` | Resource loading, damage scaling, and utilities |
| `ExaltHelper.Proxy.Mods/` | Tracking, gameplay tools, and filters |
| `assets/`, `ExaltHelper_Data/` | Game metadata, sprites, and native hook |
| `CompiledResources/`, `lib/` | UI resources and dependency assemblies |
| `tests/` | Automated checks and combat/dialogue fixtures |

Repository setup instructions are in [docs/GITHUB-UPLOAD.md](docs/GITHUB-UPLOAD.md).

## References

- [RealmShark / Tomato](https://github.com/X-com/RealmShark): reference implementation for combat tracking and game packets.
- [RealmEye Wiki](https://www.realmeye.com/wiki/items): item and ability mechanics.
