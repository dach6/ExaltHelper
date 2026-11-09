# ExaltHUD desktop guide

Press **F10**, click **Expand F10** beside **Lock** in the compact HUD, or choose **ExaltHUD** in ExaltHelper to open the desktop dashboard. The shortcut label remains visible when the HUD is locked; press **F10** in that state. You can also right-click the unlocked compact overlay and choose **Expand to desktop**. The same tracking session supplies both views.

The Midnight theme uses layered navy surfaces, teal accents and an icon sidebar. The active page has a highlighted left edge; resource cards use separate HP, MP and currency tints. Choose **Settings → Appearance** to switch between Midnight, Slate and Light or select your own accent color.

Search fields, dropdowns, numeric inputs and checkboxes share the selected theme. Thin scrollbars support dragging and the mouse wheel; lists and dropdowns keep their keyboard controls. Vault slots have individual frames, small slot indices, stack-count badges and a highlighted border on hover. The original slot order is preserved.

See the [Vault preview](images/tactical-vault.png), [live-feed preview](images/tactical-live.png), and [Settings preview](images/tactical-settings.png), captured offline.

Loot history groups the recorded items from each bag into one row; collection
details also show item icons with tier and rarity labels.
Loot entries in the live feed and player/party equipment use individual icon
strips. Hover a specific icon to see its name, catalog stats, effects, observed
enchantments and available drop context. Focus an item cell and press **F1** to
open its details with the keyboard; **Escape** closes the tooltip. Search and
sorting still use the item names. An unidentified item keeps a readable name
instead of borrowing an icon from a different item.

Item tooltips use the selected HUD theme in tables, character equipment, Vault,
Treasury and pet inventories. Inventory hover details include the container,
slot, stack count and observation time. Ability entries use an icon when their recorded name identifies
one catalog item; ambiguous names retain text. See the [loot preview](images/tactical-loot.png)
and [item tooltip preview](images/tactical-item-tooltip.png).

## Window and navigation

Drag the title bar and resize any edge, or use **Monitor** to move to another display. Maximize for a large workspace. **Pin window** toggles always-on-top. The desktop remains usable without game focus. Its bounds, maximized state, topmost setting and selected page are saved separately from compact overlay position, height and lock state. A removed monitor causes the next launch to recover the window onto an available display.

**F8** hides or shows the active view. **F9** changes the compact overlay lock. **F10**, **Compact F10**, or the desktop close button returns to the compact view. Scroll the sidebar to reach all pages on smaller screens. Table search is case-insensitive; category and date filters are remembered. Double-click a row, or select it and press Enter, to open available details.

In a small window, scroll the live-feed toolbar horizontally to reach its actions and quick text buttons. Summary cards fold away to keep the records visible. Hover over a shortened warning to read all active reminders.

Account filters also scroll horizontally in short windows. The roster keeps at least one complete row; if there is too little height for character or pet details, a resize hint appears. Increasing the window height restores the selected detail tab.

The selected character's detail view stays in place during live account updates.
You can scroll **Equipment & stats** while stats, inventory and enchantments
continue to update without returning to the top.

Dashboard refreshes reuse unchanged rows and update the displayed history only
when its records change. Minimizing or hiding the dashboard suspends visual
refresh work while tracking, reminders and enabled clipboard expiry continue.

## Widgets and tasks

The widget bar shows observed character resources, account currencies, forge fire, materials, dust, stars, skin and character counts, playtime, season/battlepass timers, server and realm progress. Unknown values display a dash. Hover for mode-specific breakdowns and freshness information. Right-click to enable individual widgets and drag to reorder them. Settings supports one or two rows and current, regular or seasonal storage. Scroll horizontally when the widgets exceed the available width.

The taskbar remains available on every page. Pin missions or quests, or enable automatic tracking in their pages. Matching objectives merge into shared tasks. Progress respects prerequisites, eligibility, AND/OR requirements, repeatable cooldowns and claimed status. Hover over a task for remaining objectives and target locations; click to open its source mission or quest. Hidden missions and categories stay excluded until restored.

## Missions, quests and account data

Launch the game through ExaltHelper and connect a character. Account refresh uses the current session in memory. Use **Refresh account and missions** in Settings to retry a sync. Quest definitions and claim changes are observed when you interact with the Tinkerer, and remain cached after leaving. Daily quest data can become stale after reset; revisit the Tinkerer to refresh it.

Characters, Treasury, Vault, Exaltations and Pet Yard combine account responses with observed game updates. Regular and seasonal storage stay separate. Treasury aggregates item stacks, inventories and blueprints and supports item, dungeon, rarity and enchantment filtering. Vault preserves empty slots and container layout. Character details include equipment, stats, maxing, fame calculations and milestone progress. Pet pages show cached identities, abilities, inventories and equipped characters. Cached data can be incomplete until the corresponding API response or in-game container is observed; freshness/status text explains the current state.

You can label and reorder characters, review locally observed deaths in the graveyard, and open derived statistics for combat, regeneration, exaltations and observed boosts. Treasury sections and categories can be ordered and collapsed. These preferences are saved per account. The graveyard starts with deaths observed by this installation; it does not reconstruct unobserved past deaths.

## Live feed and history

The live feed records travel, realm events, messages, loot bags, key openings and alerts. **Copy callout** copies text for you to paste into the game. Settings controls server, realm, party prefix, full/short/custom names, join-marker position, upcoming-dungeon and alien-wave hints, and reward/modifier tags. Reward percentages use the bundled game modifier definitions; unknown modifiers leave the total unknown. Selecting an older event uses that event's recorded location and modifiers. Dungeon calls expire when their observed join window ends or you leave the dungeon.

Automatic dungeon copying is optional. It copies newly observed, joinable dungeons and clears an expired call only if the clipboard still contains that exact text. Observed portal spawns anchor a 30-second window; joins without a visible portal use a 25-second estimate. Your configured clipboard lifetime can shorten that window. Quick text buttons, roster copies and editable key/rune thank-you templates also write to the clipboard. Paste a message into the game to send it.

Loot display filters in the feed are independent of loot-history recording filters. Each visible bag keeps all of its items together. Dust notices report a newly reached cap separately for regular and seasonal characters; the first observation seeds the state quietly. Feed categories and season, battlepass and monthly calendar reminders can be enabled separately. Party utilities likewise copy commands. Separate local watch and banned lists flag players and can notify you when they appear in your dungeon; moderation commands remain available to copy and use yourself.

Combat history stores observed encounters, including team damage and available enemy-attributed damage taken. Open a fight for equipment and contribution details. Server reports can correct earlier estimates, including to zero. Source tooltips show the conditions recorded when the projectile was fired. Reviewing old combat history does not replace live widgets, party/player inspection or live-feed metrics.

The expanded dashboard's **Combat tracker** page breaks the current dungeon into
one expandable section per observed monster. Each section keeps its own damage
total, HP bar, fight duration, phase count and contributor ranking, so two
monsters with the same name still have separate leaderboards. Expand a row with
the mouse, **Enter**, **Space**, or the arrow keys. **% DMG** is each player's
share of that monster's tracked damage; DPS uses that monster's encounter time.
Optional columns show hits, damage taken, guarded damage and the last observed
gear. Search can match a monster or contributor while retaining the complete
leaderboard for matching monsters. The page also shows bundled monster,
character/class, dungeon/portal and equipment artwork. Older records without a
field such as hit count or HP keep it as unknown instead of displaying a zero.

Settings can restrict newly recorded fights by dungeon difficulty, realm boss group, seasonal encounter or treasure crate, and can exclude solo fights. Already recorded fights continue receiving damage corrections. Unknown groups remain eligible. The same dungeon ratings distinguish normal and advanced variants in mission recommendations and key-opening filters.

Sound settings provide per-category switches, master and individual volume, built-in tones and custom WAV files. Categories include chat, named realm events, dungeon modifiers, bag types and key-opening difficulty. New sound categories start disabled. Custom files must contain 8- or 16-bit PCM mono/stereo audio, last at most five seconds and be no larger than 2 MB. Use the preview button to check the selected sound and volume.

Choose **Dungeon runs** in Combat history to combine retained encounters from the same dungeon visit. Its DPS uses the observed dungeon duration, including time between fights. The detail view explains when totals cover only encounters still retained by your history limit.

Loot history records observed drops with their original dungeon, character, source and enchantments. Source attribution depends on visible encounters. Trophy Hall combines lifetime discoveries with the bundled RealmEye drop catalog; it shows missing items as well as discoveries. Trophy counts and recorded-run summaries survive normal history trimming. Public bags do not establish a rare-item discovery. A recorded run means a distinct tracked dungeon session containing an observed defeated encounter, not proof of every boss or dungeon completion.

Each loot-history row contains one bag's item icons. Hover each icon for its own
rarity and enchantments, or open the bag to inspect every recorded item. Search
matches any item in the bag, and the **Shiny** filter includes bags containing a
shiny item. Existing records with the original bag identifier are grouped too;
older records without a reliable bag identifier remain separate. Lifetime
collection counts continue to count individual item drops.

Loot recording filters select bag types to always keep, plus content matches for shiny items, enchantments, UT/ST equipment, stat potions, marks and a minimum equipment tier. A bag is kept when its type or any enabled content filter matches; all its slots stay together. Disable **Any enchantment** to restrict enchant matches to the selected loot, unique, awakened or Legendary/Divine options. Blue/cyan/egg and higher bags remain enabled by default for compatibility with the compact HUD.

Chat history supports player/message search and realm, whisper, guild, party and game channels. Settings can disable each kind of history recording and change the retained record limit. All histories are local to this computer and separated by account.

## Calculator

Use live imports or choose equipment and enter stats, target defense, conditions, pet levels and additional bonuses. The calculator reports theoretical output under the displayed assumptions. Weapon calculations include projectile rolls, rate of fire, defense and applicable modifiers. Ability readouts use the reference's per-use potential model; supported effects, resource limits, cooldowns, procs and omitted mechanics are described alongside the result. Per-use ability potential is not a promise of actual sustained DPS. Positioning, hit rate, immunity, target count and mechanics outside the modeled effects can change real combat results.

Ability totals assume their listed projectiles/components hit and are generally before target defense. Lethal Strike is a separate conditional bonus calculated for the selected defense. Summons show projectile damage without inventing firing rate or uptime. Conditional, set and enchantment procs and buff uptime are not simulated as sustained output. Pet interpolation is approximate; cast-rate estimates enforce declared cooldown, MP capacity and regeneration without simulating channel drains or refunds. Native poison procs model expiry/cadence; ability poison totals assume the full damage-over-time effect. The result notes identify these cases.

## Appearance and local files

Settings provides Midnight, Slate and Light themes, a custom accent color, compact controls and navigation tooltips. Preferences and account caches are under `%LOCALAPPDATA%\ExaltHelper\HUD`. History filenames use a hashed account identity. Session credentials are not written to HUD files. Recorded player names, chat messages and item history are personal local data; do not include them when sharing a release or source package.

Close ExaltHelper before backing up or moving HUD data. Writes use temporary files and atomic replacement; HUD journals also retain a backup. Tests override the location with `EXALTHELPER_HUD_DATA` so fixtures do not alter your account history. Offline fixtures exercise parsing, calculations, persistence and rendered UI. Live authenticated API compatibility, current server variations and physical monitor/global-key behavior still require in-game validation; see [the verification ledger](REALMHOUND-PARITY.md).
