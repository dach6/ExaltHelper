# Expanded HUD: feature and verification ledger

Reference: [pillarzz/RealmHound](https://github.com/pillarzz/RealmHound), commit
`a4c6f74ca16155d7facd97c293e784e8cd9ffd4e`, inspected 2026-11-02.
The feature tour (`docs/index.html`), panel implementations, protocol definitions,
and settings are the scope sources. A tab name or a successful build alone is not
evidence of feature completion.

The requested result is ExaltHelper's HUD with an independent, resizable desktop
mode suitable for a second monitor, improved presentation, and the RealmHound
features below. Its existing combat, equipment, party, ability, loot, Moonlight
Village, overlay lock, and scrolling behavior must remain functional.

## Game 7.2.0.1.0 compatibility update: 2026-11-05

ExaltHelper 1.1.1 updates the launcher version file and compiled fallback to
`7.2.0.1.0`. Assets were extracted from the official launcher's downloaded
Production client. The launcher installation contains separate launcher assets;
the game metadata identifies version `7.2.0.1.0` at byte offset 11034925.

The game catalog changes one object: Ocean Trench Key (3089) now has
`DungeonDifficulty` 5 and an `ExtraTooltipData` warning that keys require 10 or
more stars. No objects or grounds were added or removed. Sprite atlases, sprite
coordinates, generated object/tile lists, ground definitions, modifiers,
enchantments, and forge properties are byte-identical to the previous release.
The manifest records current source hashes and the exact key before/after values.
Explicit Git line-ending attributes preserve the extracted asset bytes in source
archives and fresh checkouts, keeping the manifest checksums reproducible.

The Release build completed with zero errors and 49 existing warnings. Fourteen
offline suites passed: game asset bundle (150 assertions with original sources),
packaged assets, ability packets, stat protocol, callouts, dashboard feed,
expanded calculator, DPS tracking, DPS modifiers, combat stats, player roster,
HUD item presentation, account workspace, and missions. The bundle test verifies
the new key fields, all packaged hashes, embedded catalogs, version fallback,
complete XML loading, and sprite artwork. Authenticated gameplay and the supplied
native hook's game-specific mappings remain unverified for this game version.

## Expanded combat tracker: 2026-11-09

The expanded dashboard now exposes a dedicated **Combat tracker** page. It keeps
all simultaneously observed encounters, gives every monster its own expandable
leaderboard, and preserves independent damage totals when names repeat. Header
rows show the observed monster portrait, HP, encounter duration, phase count and
current/defeated state. Player rows show rank, damage, encounter DPS, share,
available hit counts, optional taken/guarded damage, class portrait and last
observed equipment. Search, active/defeated filtering, keyboard expansion,
stable selection/scroll state and light/dark theme changes are covered.

The page and related history tables resolve bundled monster, class/player,
portal and equipment artwork from the declared game atlases. Missing historical
fields remain unknown. `Test-CombatTrackerView.ps1` passed 34 assertions,
including same-name simultaneous targets, tracker-to-journal retention, sprite
pixel/source checks, corrections to zero, account scope reset, narrow rendering
and the integrated dashboard preview. The Release build completed with zero
errors and 49 existing warnings. This fixture evidence is offline and does not
replace authenticated live-game validation.

## Character scrolling, grouped loot and tooltip update: 2026-11-03

Live account revisions previously disposed and rebuilt the selected character's
detail controls. The page now retains the same detail tree for the same account,
character, mode and living/dead state, updates its current values in place, and
keeps inventory scroll, detail tabs, focus and table selections. Identity changes
reset the view. Accepted inventory sections snapshot their content, so later
caller mutations still refresh stack/enchantment hints while unchanged live
updates retain the existing hover and scroll position.

Loot history now presents one row per bag with separate item icons and individual
rarity/enchantment details. New records carry a scoped bag identity and slot;
the original per-item IDs, deduplication and lifetime collection counts remain
intact. Old GUID-plus-slot records group only when their original context agrees.
Unrecognized historical IDs stay separate. Search matches all bag contents;
both the bag type and Shiny filters can match a mixed bag. Opening a bag shows
every recorded item, with access to its individual details.

Item tables and account inventories share an owner-drawn tooltip renderer. Item
tables suppress competing Windows cell tooltips for the binding lifetime and
retain themed explicit/truncated hints for non-item cells. Per-item context,
keyboard F1/Escape, theme changes, multiple item columns, and stable hover during
unrelated live stat updates are covered. The compact header now shows a clickable
**Expand F10** control beside **Lock**, including when collapsed. Locked mode
keeps the label visible and continues to use the F10 keyboard shortcut.

Validation: 15 suites passed on the final integrated build, including 41
character-refresh checks, 48 bag-grouping checks, 59 item-presentation checks,
146 history-refresh checks, 32 refresh-performance checks and 95 compact-overlay
scroll checks. Account, persistence, feed, retention, isolation, infrastructure,
repainting, expanded-dashboard and asset-bundle checks also passed. The old
binary reproduces the timer-driven scroll reset and two rows for one saved bag.
A separate UI smoke check opened the dashboard through the new header control,
checked its bounds and click-through behavior, and rendered both collapsed and
locked headers. Grouped loot was visually reviewed at normal and minimum sizes;
tooltips were reviewed in Midnight, Slate and Light. Build: 49 existing warnings,
zero errors. Tests use isolated local data and do not start a game session.

## Game 7.2.0.0.0 asset refresh: 2026-11-03

The launcher version and compiled fallback now target `7.2.0.0.0`. Game data,
sprite atlases, sprite coordinates, enchantments, forge properties and modifiers
were extracted together from the locally installed official Production client.
The game version is present in `global-metadata.dat` at byte offset 11199167.
`assets/game-assets.json` records source fingerprints, extraction provenance,
packaged hashes, source ID exceptions and representative release changes.

The bundle contains 36,857 distinct source objects, 12,843 items and 10,398
ground definitions. Compared with the previous XML bundle, it adds 227 objects
(including 65 items) and 13 grounds, changes 327 objects, and removes none.
Three inherited oversized source IDs are preserved; the existing protocol
conversion produces 36,855 runtime object IDs. Independent checks confirm
source ordering and the same alias winners. `ID3.list` remains the unchanged
fallback for four legacy debug records.

Generated catalogs use UTF-8, preserving accented names and curly apostrophes.
Item sprite metadata now reads slot types directly from XML for armor and
abilities without projectiles. This fixes missing slot types in the compact
catalog while retaining its embedded-name fallback. All 12,823 item definitions
with an explicit slot type agree with runtime metadata.

Twenty verification suites passed: the new game bundle check; packaged assets;
expanded calculator; DPS tracking and modifiers; combat stats; Lethal Strike;
player roster; callouts; ability packets; stat protocol; item presentation;
history refresh; account workspace; loot retention; missions; dashboard
infrastructure; repainting; expanded dashboard; and refresh performance.
The bundle check passed 143 assertions with the original game sources,
including exact source fingerprints/version bytes, all runtime object and ground
names, independently extracted sprite rectangles and rendered pixels, Unicode
names and new/changed items. Calculator coverage evaluated 1,106 modes across
448 abilities and 180 proc mappings. The expanded dashboard passed 212 checks
when rerun alone after a transient Windows clipboard restoration error during
the parallel run. The build retains 49 existing warnings and has no errors.

The native hook remains unchanged. Its required 16 IL2CPP exports are present
in the current game binary, but game-specific hook mappings and authenticated
gameplay have not been verified. Offline asset, packet and UI checks do not
establish live server compatibility or game FPS.

## Desktop and presentation requirements

| ID | Required behavior | Verification |
| --- | --- | --- |
| D01 | Switch between compact overlay and expanded desktop without losing tracking state. | Toggle both directions with populated snapshots and inspect the selected data. |
| D02 | Desktop moves to any monitor and resizes horizontally and vertically; maximization works. | Real Form bounds/resize test plus a rendered narrow and wide dashboard. |
| D03 | Desktop stays usable when the game is minimized, closed, or not focused. | Run timer/visibility paths with absent or minimized game; inspect desktop interactivity. |
| D04 | Save desktop bounds, maximized state, topmost preference and selected page separately from compact overlay bounds/lock. | Restart settings round-trip, including mode switching. |
| D05 | Recover saved windows when a monitor is removed, including negative-coordinate monitors. | Synthetic monitor geometries with offscreen, partially visible, oversized and negative bounds. |
| D06 | F8 toggles the active view; F9 locks only the compact overlay; F10 switches modes. | Exercise command routes and check desktop controls still accept input after overlay locking. |
| D07 | Expanded view exposes readable navigation, clear empty/loading/error states and functional search, scrolling and selection. | Per-page control interaction and visual review at multiple window sizes. |
| D08 | UI callbacks and snapshot updates cross threads safely; closed forms stop timers/subscriptions. | Background update/disposal regression. |

## RealmHound feature scope

| ID | Feature and required behavior | Authoritative reference |
| --- | --- | --- |
| W01 | Widget bar remains visible on every page. | `settings.rs`, `app.rs` widget rendering |
| W02 | Current character and attributes; dust; forge fire; materials; account fame; gold; rank/stars; character slots; owned skins; cumulative playtime; season and battlepass countdowns. | `settings.rs: WidgetKind` (12 kinds), `panels/derived_stats.rs` |
| W03 | Choose enabled widgets, persist their order, and support two rows. Show unknown data as unknown rather than zero. | `settings.rs: WidgetBarSettings` |
| T01 | Taskbar tracks remaining mission and daily quest objectives on every page. | `panels/taskbar.rs`, `panels/live_feed.rs` |
| T02 | Pin/unpin, automatic tracking preferences, hide exclusions, and seasonal/regular mark preference persist. | `settings.rs: TaskbarSettings` |
| T03 | Merge matching quest/mission objectives, recommend useful OR alternatives, and connect missions with overlapping objectives. | `panels/taskbar.rs: build_taskbar_items` and tests |
| T04 | Correct AND/OR progress, remaining counts, eligibility, cooldown/locked chains and completion lifecycle; explain targets/farming areas in tooltips. | `panels/taskbar.rs`, `panels/missions.rs` |
| L01 | Live feed records Oryx realm events, entered dungeons, current server/realm/score, incoming direct messages, alerts and notifications. | `panels/live_feed.rs`, `processing/realm_discovery.rs` |
| L02 | Clipboard callouts for events and dungeons, with server/realm/party options, short/custom names, join/origin markers, bonus/reward tags, upcoming hints, quick text and optional automatic dungeon copy. | `panels/event_call.rs`, `panels/dungeon_callout.rs`, `LiveFeedSettings` |
| L03 | Configurable feed categories and end-of-season/battlepass/calendar warnings. | `LiveFeedFilterSettings`, `SeasonWarningSettings` |
| Q01 | Receive Tinkerer daily/event quests, retain them away from the cave and group them by category. | `protocol/packets/quest_fetch_response.rs`, `panels/quest.rs` |
| Q02 | Calculate required mark counts across seasonal/regular inventory, distinguish stale daily data, and show claimable rewards. | `panels/quest.rs` |
| Q03 | Hide quests/categories, select Taskbar tracking, and persist preferences. | `settings.rs: QuestsSettings`, `panels/quest.rs` |
| M01 | Refresh all current-season mission definitions and account progress using the logged-in game session. | `api/missions.rs`, `processing/mission_tracker.rs` |
| M02 | Live mission state/progress, grouped by status, including claimable, locked, claimed, and repeatable cooldown. | `processing/mission_tracker.rs`, `panels/missions.rs` |
| M03 | Correct current-character eligibility, prerequisites, AND/OR objectives and target descriptions/tooltips. | `panels/missions.rs`, mission fixtures |
| M04 | Drag/reorder, hide and pin missions with durable preferences and season-specific identities. | `panels/missions.rs`, `MissionsSettings` |
| H01 | Durable loot history searchable by item, source monster, dungeon, character and date, with rarity/special-item filters. | `panels/loot.rs`, `loot/database.rs` |
| H02 | Durable encounter/dungeon history with searchable filters and per-player damage dealt/taken. | `panels/combat_history.rs`, `combat/database.rs` |
| H03 | My Performance details include observed damage sources, gear, stats, buffs and calculation context. | `panels/combat_history.rs` |
| H04 | Trophy Hall groups rare-item collections by dungeon and shows collected and still-undiscovered items and run summaries. | `panels/trophy_hall.rs` |
| A01 | Treasury searches account-wide items across all containers, aggregates counts and filters by dungeon/category, including blueprints. | `panels/treasury.rs` |
| A02 | Vault preserves slot layout and contents of vault, gifts, potions, materials, seasonal spoils and pet inventories, split by mode. | `panels/vault.rs`, `vault/live_vault.rs` |
| A03 | Characters shows all cached characters by seasonal/regular mode, gear, inventories, stats and maxing progress. | `panels/characters.rs`, `api/character/` |
| A04 | Character statistics include fame-on-death calculation, milestones and theoretical DPS with target-defense input and item/ability breakdown. | `panels/characters.rs`, `panels/derived_stats.rs`, `stats/fame_bonus.rs` |
| A05 | Exaltations reports account-wide progress for all eight stats by class. | `panels/exaltations.rs`, `protocol/packets/exaltation_update.rs` |
| A06 | Pet Yard diagnostics reports cached identities/skins, seasonal and regular inventories and equipped characters. | `panels/pet_yard.rs` (present in `ActiveTab`, beyond feature tour) |
| P01 | Party page exposes membership and useful player/equipment/status details without opening the game panel. | `panels/party.rs` |
| C01 | Durable searchable chat history, conversations/player search, and public/whisper/guild/party channel filters. | `panels/chat.rs`, `ChatSettings` |
| S01 | Appearance and per-feature preferences persist, including widget/taskbar/feed/loot/combat/notification settings. | `settings.rs`, settings UI |

## Integration invariants

- Reading HUD features must preserve forwarded packet bytes. Unsupported and
  malformed optional packets must not disconnect the game or break unrelated
  tracking.
- Refresh operations must not block the UI or packet processing, expose session
  tokens, persist credentials, or mix results from different accounts.
- Cached account data needs account separation, mode separation, and freshness
  information. Missing observations must not be presented as authoritative zeros.
- Repeated snapshots and packets must not duplicate history; later corrections
  must update the matching record. Navigating old combat history must not add it
  as a newly observed fight.
- Persistence must tolerate missing/corrupt/partial files and write atomically.
  Snapshot or filtering callbacks must not mutate source collections.
- A live-game limitation must remain visible in the completion audit. Synthetic
  tests establish parser and UI behavior; they do not establish all current
  server payloads or authenticated API responses.

## Baseline evidence

On 2026-11-02, before expanded-dashboard implementation, an isolated Release build
with SDK 10.0.401 completed with 0 errors and 51 existing warnings (including an
unavailable NuGet vulnerability endpoint). Output:
`.exalthelper-tools/expanded-dashboard-baseline/ExaltHelper.exe`.

All baseline regression processes exited successfully:

| Suite | Result |
| --- | --- |
| Ability packets | 77 assertions |
| DPS tracking | 38 assertions |
| DPS accounting | 37 assertions |
| DPS modifiers | 25 assertions |
| Combat stats | 19 assertions |
| Combat diagnostics | 43 assertions |
| Lethal Strike replay | 264 assertions |
| Packaged assets | 24 assertions |
| Player roster | All regression checks passed |
| Moonlight Village | 109 assertions, 83 recorded dialogue events, 4 sessions |
| Overlay scrolling | 95 assertions across six lists |

## Integrated verification evidence

The integrated Release output at `.exalthelper-tools/dashboard-release-candidate` builds
with SDK 10.0.401, 0 errors and 49 warnings on 2026-11-02. The warnings are not
treated as proof of feature correctness. Each harness runs in a fresh Windows
PowerShell process. New dashboard tests isolate persisted data from the user's
account, and the desktop test uses the executable directory as its application
base so packaged data is actually exercised.

| Suite | Integrated result | Main evidence |
| --- | --- | --- |
| Dashboard infrastructure | 67 assertions | Passive packet payloads, independent MapInfo maximum/current score pairs and incomplete suffixes, forwarding, corruption/concurrent saves, durable table filters/scroll/selection |
| Dashboard journal | 76 assertions | Durable account separation, chat channels, deduplication/corrections, failed-save retry, nonblocking cold loads, late account handoff, replay merging and detached snapshots |
| Dashboard combat integration | 20 assertions | Actual projectile handlers through snapshots into history; corrections, immutable source stats, pellets, summons, boss phases, final map-change capture and observed calculator conditions |
| Dashboard isolation | 23 assertions | Real offline HELLO transitions; unresolved backlog; same-account reconnect retention; stale-client chat, snapshots, source, party and ability hooks ignored; refresh gate prevents networking |
| Dashboard retention | 69 assertions | Actual loot capture/provenance and independent feed pipeline, 22 bag mappings, whole-bag/content filters, public bags, bounded history with lifetime totals and durable deduplication, cold-load replay/UTC ordering and dungeon-run aggregation |
| Dashboard feed | 41 assertions | Actual portal capture and forwarded use bytes, original realm score/deadline, distinct-instance modifier learning, independent loot, dust crossings, key tiers, trusted realm-closing countdown and recognized NPC events |
| Combat capture policy | 118 assertions | All twelve groups, object-ID/dungeon/anchor precedence, unknown enemies, solo/group transitions, capture-time filtering, exact difficulty boundaries and durable preferences |
| Dashboard calculator | 8 assertions | Fixed independent expected DPS values for roll bounds, truncation, defense, curse, armor piercing, buffs and projectile count |
| Expanded calculator | 79 assertions | Supported weapon/proc, ability modes, eight stats, relative/enchant bonuses, regeneration, pet interpolation, exaltations/proficiencies, profiles and UI; all 1,101 bundled ability modes evaluate |
| Expanded dashboard | 203 assertions | Nineteen pages, 104 catalog locations, widgets, durable callout settings/quick text, automatic clipboard ownership/account changes, UTC/local history date boundaries, readable minimum-size feed with eight quick texts/two widget rows/warnings, party lists, themes, bounds and shutdown |
| Callout engine | 89 assertions | Independent dungeon/event text, short/custom names, modifier catalog/bonuses/tags, upcoming hints, origin/join rules, unknown modifiers, roster and thank-you templates |
| Notification sounds | 100 assertions | Opt-in category routing, packet hooks, PCM format validation/volume scaling, custom files, queue/rate limits, durable UI controls and shutdown using silent playback substitution |
| Secondary-stat protocol | 125 assertions | Independent signed compressed vectors, range/alignment, Update/NewTick exact forwarding, large stack counts, BloodRitual string identifier and generated sentinels |
| Account workspace | 162 assertions | Synthetic API/live storage, characters/graveyard/pets, inventory/layout, fame/derived calculations, cached pages, account persistence and refresh contention |
| Missions and quests | 133 assertions | Real season 52 fixture, state/lifecycle, shared AND/OR objectives, exact normal/advanced/stage difficulties, automatic defaults/per-ID overrides, UI, nonblocking persistence, ordered retry and cold-load preference merges |

All 26 main regression scripts passed against the same candidate
`.exalthelper-tools/dashboard-release-candidate/ExaltHelper.exe` with SHA-256
`FB6FBF64A14C8BEE9DA7A7148EA3EDF2B1C520545622B5816D79ED28DBA20822`.
The suites report 2,044 numeric assertions plus the Player Roster checks.
Original-suite counts match the baseline table. Root-run logs are in
`.exalthelper-tools/hud-release-regressions`; the seven verification-suite logs
are in `.exalthelper-tools/hud-release-candidate-regressions/verification`.
Account/calculator/sound and mission/combat-policy/callout agents confirmed their
assigned suites on that exact binary. These results record the HUD baseline.

On 2026-11-02, the launcher game version was updated to `7.1.0.2.0` in
`ExaltHelper_Data/version.txt` and the compiled `Program.GameVersion` fallback.
The rebuilt executable has SHA-256
`CDF55AE44C1EA0DB2695DEE074F71BA72F1CF4F99754225340C29051FB0CFD4C`.
It builds with 0 errors and the same 49 warnings. Runtime checks confirmed both
the packaged version and the fallback without a version file; all 24 packaged
asset assertions passed. The 26-suite results above apply to the preceding HUD
baseline. Authenticated gameplay with version `7.1.0.2.0` remains unverified.

The tactical UI refresh was built on 2026-11-02 with 0 errors and 49 inherited
warnings. Its executable SHA-256 is
`BF1A112E11890DC7FC06EB8C8425DE9A407B3D9CE954E093AB01CB38ED8696FE`.
On this exact binary, Expanded Dashboard passed 212 checks, Account Workspace
162, Missions 133, Expanded Calculator 79 (1,101 ability modes), Notification
Sounds 100, and Dashboard Infrastructure 67: 753 checks across six repository
suites. A separate native-control probe passed 26 checks for text editing,
dropdown selection, numeric input, scrolling, theme changes and handle disposal.
The new dashboard assertions verify readable inherited cell styles for both
alternating rows and selections when switching between Light, Slate and Midnight.

All 19 populated pages were visually inspected. An independent build of the same
final production sources passed 65 focused contrast and compact-layout checks,
including complete roster and Vault rows with two widget rows enabled. The final
release binary also produced the two [guide previews](HUD-GUIDE.md), with 5 capture
checks. Evidence is under `.exalthelper-tools/account-responsive-review/20261103-022016-a5f13438`,
`.exalthelper-tools/ui-visual-review/20261103-022620-f66ebfc2`, and
`artifacts/expanded-dashboard/106d89732fc7404ca4a8c932f7b6c489`.
The subsequent overlapping-text fix has executable SHA-256
`CC86F035420B2B31F5E9764F55D23159A45754EC5BF5546205FD90F4680E788B`.
Buttons and checkboxes now clear their backgrounds before painting, and custom
text preserves the drawing origin and clipping region. Expanded Dashboard passed
212 checks, Missions 133, the custom-control probe 26, and the new
`Test-HudRepainting.ps1` suite 114: 485 checks on this exact binary.
The same repaint suite fails 91 checks on the original tactical binary and
reproduces the reported ghosting. The earlier whole-window capture corruption
was evidence of this product defect; clean `DrawToBitmap` images had missed it.
Native control and shared-window captures now agree after partial paints,
scrolling, resizing and painting into buffers containing old text.
Evidence and the Settings preview are under
`artifacts/hud-repainting/3cf1f545cb0b44a0bd2a5620ea3c54d2`.
Separate geometry checks found no sibling collisions or label-height clipping
at 100%, 125%, 150% and 200% simulated scaling; those checks emulate font and
control dimensions on a 96-DPI desktop, not physical monitor DPI changes.
Screen capture remains unavailable in this session; native window-message
rendering is tested, but physical desktop pixels and monitor behavior remain
unverified. Windows title-bar theming is applied where supported.

The item browsing and refresh update has executable SHA-256
`778E1A29BAE436CB32A32B62ED6C9130A775575A7305AFF9B37248760E3F0F36`.
It adds item icons, tier/rarity badges and per-item hover details to loot history,
live loot, equipment, collections and identified abilities. Feed entries retain
the observed item rarity, with compatible fallback for older cached entries.
The dashboard copies only the selected history, uses independent page revisions,
reuses native table rows, and avoids unchanged widget/task work. Account summaries
and indexed inventory counts reduce repeated cloning; hidden/minimized windows
continue notifications and clipboard expiry without visual refresh work.

On this exact binary, Expanded Dashboard passed 212 checks, Account Workspace
162, Missions 133, Dashboard Infrastructure 67, Journal 76, Retention 69, Feed 41,
HUD Repainting 114, Item Presentation 28, Refresh Performance 32, and History
Refresh 146: 1,080 checks across 11 suites. A separate visual review passed 15
checks and inspected native normal, minimum and Light layouts plus individual
item hover cards. Evidence includes
`artifacts/hud-history-refresh/be8a8d6aa5724e338b6770dbf35a0ba5` and
`.exalthelper-tools/hud-item-visual-review/0e3367d5c9144090b57646ceef92faae`.

With loaded item assets and synthetic histories (5,000 loot records, 500 feed
entries, 1,000 fights and 560 vault slots), median processing for a live-stat
update fell from 33.752 to 0.496 ms on Live, 12.869 to 1.126 ms on Loot, and
3.076 to 0.197 ms on Vault compared with the preceding `CC86F035…` build.
Corresponding allocations fell from 6.24/11.04/4.45 MB to about 26/21/21 KB.
These measurements cover recurring HUD updates, not game FPS or first-time
asset loading. Mission tasks still rebuild when the account revision changes.
Timing evidence is under
`.exalthelper-tools/expanded-performance-release/0a8d8f75299d48a195d46878b983a677`.

The tests exposed and verified fixes for guild/party chat classification,
clipped widget/toolbars, monitor/compact restore bounds, maximum-exclusive DPS
rolls and integer truncation, lost server-pellet source context, zero-damage
corrections, boss phase identity and damage-taken merging, final map-transition
history, pending-account persistence, cloned-record selection, and the catalog
parser's incorrect array casts. Further fixtures verified retention-independent
collection/run totals, reused loot entity IDs, public-bag provenance,
same-account reconnect retention and stale-client rejection. The protocol suite demonstrated the old parser
failing at secondary value 64 before correcting it. The three synthesized
secondary sentinels retain their original wire byte `0x41` after widening.
An independent raw MapInfo fixture also failed before correcting maximum/current
score order and preserving incomplete optional suffixes. Visual review exposed
clipped Live feed rows at minimum size; both ordinary and maximum-controls
regressions passed after the responsive layout correction.

Rendered desktop examples are in
`artifacts/expanded-dashboard/fe77b32f71c1418aa63aae4508cc1a8b/`. Trophy Hall
contains all 104 bundled dungeon/biome collections, including proper Unicode
names. Wide live, settings and trophy views and the minimum party view were
visually inspected. The final light-theme Live feed and minimum-size view with
all eight quick-text buttons, two widget rows and warnings were visually
reviewed. Narrow layouts scroll the toolbar, hide metric cards and summarize
warnings with their full text in a tooltip to preserve readable records.
Empty account and mission pages in these desktop images
are intentional fixtures; their populated states are covered by their domain
harnesses. The account agent's populated images, including graveyard/derived
details and arranged Treasury sections, are in
`.exalthelper-tools/account-screenshots`; both of those added views were
visually inspected.

## Implementation and fixture audit

The rows below audit the specific behavior required above. "Fixture" means a
synthetic packet, account, calculation or Windows Forms scenario exercised real
production handlers. It does not mean authenticated game validation. Suite
names refer to the integrated evidence table; the source paths are under
`ExaltHelper/` unless another directory is named.

| ID | Implemented behavior and source | Fixture evidence and practical limit |
| --- | --- | --- |
| D01 | `DpsOverlayManager` switches the two forms around one retained snapshot. | Expanded dashboard verifies both directions, selected page and populated snapshot identity. |
| D02 | `ExaltDashboardForm` is a normal sizable/maximizable taskbar window with a monitor menu. | Expanded dashboard checks actual window styles, normal/maximized restore bounds and narrow/wide rendering. Physical second-monitor movement remains untested. |
| D03 | Desktop visibility is independent of the compact overlay's game-window polling. | Expanded dashboard uses a minimized then closed dummy game form and keeps desktop controls active. |
| D04 | `HudPreferences`, dashboard and compact form persist separate geometry/lock/mode/page/topmost fields. | Infrastructure round-trips settings; Expanded dashboard exercises compact restore, desktop save and maximized normal bounds. |
| D05 | `ClampBounds` selects the greatest-overlap working area and clamps size/location. | Expanded dashboard covers negative monitors, removed monitors, partial overlap, oversized/invalid saved sizes and small screens. |
| D06 | Manager routes F8 visibility, F9 compact locking and F10 mode switching. | Expanded dashboard invokes the actual command routes; OS hotkey conflicts in a running game remain untested. |
| D07 | Nineteen pages have navigation, scrollable bars/tables, filtering and selection; account/mission pages show cache/refresh status. | Expanded dashboard navigates every page, requires a complete Live feed row at minimum size, and checks equivalent UTC/local records around local midnight in four history views. Infrastructure checks composed filters, empty state, retained selection and scroll. Domain suites exercise populated account/mission controls. |
| D08 | Manager marshals updates to its UI thread; dashboard/child controls dispose timers and shutdown flushes workspaces. | Expanded dashboard delivers from another thread, joins the terminated UI thread and rejects late updates; domain suites test persistence during background contention. |
| W01 | `ExaltDashboardForm` places widget and task bars above the page host. | Expanded dashboard checks both bars on all nineteen pages. |
| W02 | Widget values include live character/attributes, HP/MP, mode-specific dust/fire/materials, fame/gold/rank/slots/skins/playtime and season/battlepass timers. | Account fixtures validate parsed values; Expanded dashboard checks observed attributes, known zero vs unknown currency, unobserved dust tooltips, dead-only cumulative playtime and both storage modes. Seasonal storage requires a live visit to become known. |
| W03 | Right-click visibility, drag order, storage mode and one/two rows persist in `HudPreferences`. | Expanded dashboard invokes the real drop route, checks persisted order/visibility and recreated controls, and confirms exactly two rows at minimum width. |
| T01 | `MissionWorkspace.GetTrackedTasks` projects remaining mission/quest objectives into shared taskbar chips on every page. | Missions checks remaining counts and merged source IDs; Expanded dashboard checks bar presence across navigation. |
| T02 | Default-on mission/daily-chest tracking, stable-ID on/off overrides, master-switch override resets, hide/category exclusions and mark-mode preference persist through mission settings. | Missions covers new-task defaults, daily-only scope, explicit overrides, master resets, hidden categories, cooldown links, early edits during cold load, durable reload and UI synchronization. |
| T03 | Task groups retain independent AND objectives and choose OR alternatives using progress and overlap with other tracked work. | Missions tests shared quest/mission objectives, independent AND steps, progressed choices, show-all choices and cooldown links. |
| T04 | `MissionModel`, `MissionTargets` and workspace projections model states, eligibility, prerequisite history and farming hints. | Missions tests claim acknowledgements, repeatable cooldown reset, failed claims, restricted equipment, integer corrections and concrete mark farming locations. |
| L01 | `HudJournal` records travel, recognized realm/NPC events, whispers, whole-bag loot, dust-cap transitions and typed key openings, with server/realm/score, captured portal metadata and a trusted realm-closing countdown. | Journal and Feed fixtures exercise packets, distinct instances, historical context, portal deadlines, dust crossings, key tiers, recognized events and countdown expiry; Infrastructure verifies independent maximum/current score bytes and preserved partial suffixes. Full live announcement variation remains untested. |
| L02 | `HudCallouts` and `DashboardPages` support short/full/custom names, party/server/realm/join/origin formatting, packaged modifier bonuses and editable tags, upcoming-event hints, roster/thanks, quick text and opt-in automatic dungeon copy. | Callouts validates independent text/formula expectations; Expanded dashboard exercises real settings persistence, copy buttons, preview, unknown-modifier handling and clipboard ownership on expiry/location/account changes. Feed validates captured provenance and deadlines. Unknown modifiers suppress partial totals; stale joins cannot be copied. |
| L03 | Hidden feed categories, independent whole-bag display rules and season/battlepass/calendar warnings persist; season/battlepass warn within three days and calendar uses UTC month end. | Feed tests independent filters and persistence; Retention proves the actual handler feeds bags before history policy/recording; Expanded dashboard tests reminder toggles and date bounds. Countdown accuracy depends on observed account dates. |
| Q01 | Workspace parses quest-fetch payloads, caches results by account and retains daily/event categories away from the Tinkerer. | Missions tests successful/malformed lists, cached account return and cold-load vs newer packet ordering. |
| Q02 | Mark counts remain separate by seasonal/regular inventory; eligibility, reward text, expiry and daily freshness are displayed. | Missions tests whole-set availability without cross-mode mixing, preferred mode, claim responses, expiry parsing and stale data that cannot be renewed by a repeated old fetch. |
| Q03 | Quest/category hiding, task tracking and page/filter preferences use durable account-scoped settings. | Missions checks hidden/pinned interactions, UI controls, restored cache/preferences and account isolation. |
| M01 | Asynchronous refresh fetches mission definitions and account progress through the active session; stale-account results are rejected. | Missions parses the bundled real season 52 dataset (36 definitions) and tests account/refresh transitions. Authenticated successful requests have not been run. |
| M02 | Missions group by in-progress/claimable/locked/claimed/cooldown state, retaining prerequisite claim history through repeat resets. | Missions checks live deltas, claim success/failure, cooldown lifecycle, reset gates and overflow/clamping. |
| M03 | Current mode/equipment restrictions and grouped AND/OR objectives drive eligibility and readable target/reward hints. | Missions exercises restricted Mystic vs Spell equipment, shared target selection and real farming metadata. |
| M04 | Cards support drag ordering, pin/hide, collapsed groups and persistent status/category filters keyed by season and mission. | Missions checks reorder, pin/hide state, section collapse/filter persistence and taskbar navigation into a collapsed section. |
| H01 | `DpsTrackerMod` captures whole bags when a selected bag type or content rule matches; `HudJournal` retains slot provenance and independent deduplication. Tables search item/source/dungeon/character/date/rarity/enchantments. | Retention tests real map transitions, reused IDs, repeated slots, 22 mappings, mixed public bags, persisted bag/content options and real shiny/UT/potion/mark/tier fixtures plus enchant thresholds. Cold-load replay cannot inflate lifetime totals or displace newer local-time drops with cached UTC rows. |
| H02 | Durable encounters use stable phase identity, twelve configurable capture groups and solo filtering; dungeon groups sum retained encounter/player damage and use maximum observed run duration. Details include per-player dealt/taken damage. | Combat policy checks classifications, precedence, solo/group corrections and recording-time behavior; Combat integration tests projectile corrections, phases, taken damage and map finalization; Retention verifies grouped duration/DPS/guard/taken totals and detached equipment. A run view totals retained encounters only. |
| H03 | Fight detail stores source item/kind, estimated and confirmed damage, ATT/DEX/VIT/WIS, effect bits, event multiplier and gear. | Combat integration checks immutable fire-time context, pellets, summons, server-first/client-first reconciliation and zero corrections. Journal checks separate calculation contexts; unknown effects are labeled without guessing. |
| H03a | The expanded Combat tracker keeps simultaneous monster sections independent, ranks contributors per target, computes target-local DPS/share, preserves hits and appearance metadata, and renders monster/player/portal artwork. | `Test-CombatTrackerView.ps1` checks two same-name targets, independent rankings and duration, journal retention, corrections to zero, keyboard/search/filter/theme/narrow behavior, stable rows, declared atlas pixels and the integrated dashboard preview. Synthetic records do not establish live packet coverage. |
| H04 | Trophy Hall loads 104 catalog locations and shows discovered/missing items, counts, first/latest dates and recorded runs using lifetime aggregates separate from retained rows. | Expanded dashboard requires a populated bundled catalog; Retention proves trimming/reload cannot erase trophies or replay runs. Public brown bags do not award collection trophies. |
| A01 | Treasury aggregates all known character/quickslot/storage/pet items and blueprint unlocks with mode/category/dungeon/type/enchant filters, missing-item lookup and saved section/category layout. | Account suite checks quantities/filters, slot sources, blueprint mode, section order and header collapse. Unknown seasonal storage is not inferred from regular storage. |
| A02 | Vault renders slot grids for vault/gifts/potions/materials/spoils and pet inventories, preserves empty slots and applies known page updates/successful swaps. | Account fixtures check chunk reconciliation, gift order, unknown page rejection, stack/enchant transfer and seasonal separation. Availability depends on API/live observations. |
| A03 | Characters shows living/cached dead rosters, modes, labels/order, equipment/inventories/quickslots, stats/maxing and observation time. | Account fixtures cover current-character updates, roster ordering/labels, death packets and caches. Graveyard covers known cached characters and deaths, not unknown historical deaths. |
| A04 | Character details decode current/legacy PCStats, show fame/milestones, account/character boosts and theoretical weapon DPS with defense input; calculator provides item/ability/stat breakdowns and per-character overrides. | Account fixtures assert independent fame totals (including 46,188 maxed bonus, 4,619 First Steps and 1,640 biome fame). Calculator suites assert formulas/modes/profile isolation. Fame estimate explicitly excludes Ancestor; DPS assumptions are listed below. |
| A05 | Exaltations maps all eight stat counters to class rows and levels, displaying unobserved classes as unknown. | Account fixtures test API/live packet ordering and thresholds; calculator tests account exaltation/proficiency bonuses. |
| A06 | Pet Yard reports seasonal/regular identities, skin conflicts, ability levels, inventories/stacks and equipped characters. | Account fixtures verify identity sources and mode separation; cached identity conflicts prompt visits to both yards. |
| P01 | Party renders membership/presence/HP/maxed stats/equipment, opens player gear details, copies utility commands and maintains separate watch/banned lists with local arrival notices. | Expanded dashboard renders 25 members at minimum width and exercises both list buttons, simultaneous flags, independent removal, persistence/recreation and a banned-player notice. Isolation rejects stale-client updates; Player roster regressions pass. Utility commands are copied for use in game. |
| C01 | Account-scoped chat history classifies public/whisper/guild/party/game messages, filters/searches by sender/recipient/text and copies replies. | Journal validates exact text/channel classification, outgoing echo deduplication, recording preference and durable account separation; Infrastructure validates search/filter behavior. |
| S01 | Layout, themes, widgets/taskbar, independent feed/history filters, richer callouts, twelve combat groups/solo capture, and opt-in event sounds persist. Sound rules provide master/category volume, tones, imported PCM WAVs and chat phrases; playback uses a bounded background queue. | Infrastructure checks atomic/corrupt/concurrent saves; Expanded dashboard verifies callout controls and layout; Retention/Combat policy verify capture options; Notification sounds tests routing, PCM, files, queue limits, UI persistence and shutdown. Native audio-device playback remains unverified. |

## Scope differences from the reference

- The listed feature scope is implemented in a WinForms desktop/compact HUD;
  reference-specific presentation and styling are not identical.
- All 21 reference bag IDs plus the legacy cyan ID are recognized. The durable
  bag/content matrix uses OR matching and retains all slots in qualifying bags.
  Defaults preserve higher-bag capture and additionally permit any enchantment;
  users can select the reference's narrower valuable-enchantment categories.

## Calculation and observation limits

These are model/data limitations, including limitations shared with the
reference, rather than absent feature pages.

- Dungeon history summarizes retained observed encounters, not all damage from
  unseen or already-trimmed encounters. Lifetime collection/run totals are
  durable, while bounded deduplication ledgers retain a finite recent window.
- The calculator reports weapon DPS and per-use ability potential separately.
  Ability potential before defense follows the reference model; it is not an
  invented combined weapon/ability DPS. Conditional/set/enchant procs and
  Lethal Strike are explicitly conditional; summon uptime/rate, delivered DoT
  timing and hit probability are not simulated. Pet Electric intermediate and
  high-level rates are approximate. Unknown challenge modifiers require an
  explicit override; observed known live challenge IDs use shared definitions.
- Fame-on-death estimates exclude the account Ancestor bonus. Account dust from
  live activation is marked estimated until API refresh. Seasonal storage and
  pet inventories require observed visits when the API lacks them.

## Validation limits

This evidence establishes tested behavior for synthetic inputs and the bundled
reference dataset. It does not establish all current authenticated API
responses, live game-server packet variants, global keyboard hook behavior in
a running game, native audio-device audibility, or movement between physical
monitors. Audio tests use silent playback substitution; PCM data, routing,
queue behavior and shutdown are tested. Negative-coordinate and
disconnected-monitor recovery use synthetic screen geometries. Feature scope
above remains a review checklist; passing assertions are not a blanket claim
of complete RealmHound parity.
