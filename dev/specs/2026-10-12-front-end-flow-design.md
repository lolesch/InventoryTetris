# Front-end flow — main menu, hero select, pause menu and Cancel

> 2026-10-14: "Modal" below is now **Popup** (`CancelRank.Popup`), and `*Prompt` components are `*Popup`;
> see `2026-10-14-ui-naming-design.md`. The behaviour is unchanged.

Date: 2026-10-12
Status: Spec, ready for `/to-tickets` once #69 is merged. Settled by a grilling on 2026-10-12 (five
rounds, every recommendation accepted or amended by the owner as recorded below). This is spec 2 of 2
of hero persistence: `2026-10-05-hero-persistence-design.md` built the `HeroSaveService`, this spec
puts views and a flow over it. It also answers the open questions of `2026-10-07-settings-menu-design.md`
(where the panel opens from) and settles the Escape rule `2026-09-28-player-session-flow-research.md`
A8 left open. **It is written with #69 (`IInputService<GameAction>`, ADR-0019, branch
`feature/input-service`) as its base**: every ticket below that touches input is `Blocked by` the PR
that merges it.

## Problem Statement

The game has one scene that matters and no way in or out of it. A built player boots straight into
`Example` and continues the last hero, because `GameBoot` loads one before any scene; `MainMenu.unity`
is in the build settings but behind `Example`, so a player never sees it. There is no way back to it:
no pause menu, no exit-to-menu, no way to pick or create a hero, no settings screen to open. Escape does
one thing, cancel a drag, and nothing defines what it should do next to the open panels and a menu.
A first launch silently creates a hero called "Hero" before the player has seen a screen. And in a
browser, closing the tab mid-Run writes nothing, so the player has no way to know when leaving is safe.

## Solution

A full cycle: **launch → Main Menu (select a hero or create one) → Play → Town and Runs → Pause Menu → Save &
Exit → Main Menu**, with one rule for the back-out key, **Cancel**.

The build starts on `MainMenu`, whose **hero slots** (already built, `HeroSlotToggle`) list the saves and
whose showcase (the hero's gear, info and model) shows the selected one. Selecting a slot loads that
hero into the Session without entering anything; the create slot asks for a name; a Delete removes a
hero after a confirmation; **Play** enters the game with the selected hero, and on arrival that is the
last one played, so returning is one click. In the game, Cancel
peels one layer per press (a held Package, a menu screen, the Inventory Context) and, with nothing
left to peel, opens the **Pause Menu**: Resume, Settings, Save & Exit, Quit Game. While it is open the
Run holds still and the player's own Pause is given back as found. **Save & Exit** Recalls first when a
Run is in the Field, writes the hero, loads the Main Menu, and tells the player their progress is safe.
Settings opens from both menus as one panel. Every failure on the way names what failed.

## User Stories

**Launching**

1. As a player, I want the game to open on a menu, so that I choose when to enter it.
2. As a player, I want my last hero already selected and shown when the menu opens, so that returning is one click on Play and I see whose game it is.
3. As a new player with no hero, I want the create slot to ask me for a name, so that I never get a hero I did not name, and Play to wait until I have one.
4. As a player, I want to see the build version on the main menu, so that I can name my build in a bug report (the Changelog epic #224 places it).
5. As a player in a browser, I want no Exit button that does nothing, so that I am not offered a button that lies.
6. As a player in a browser who came back to the menu, I want a line saying my hero was saved and when, so that I feel safe to close the tab.

**Heroes**

7. As a player, I want a slot per hero showing its icon, and the selected hero's name, level and class, so that I can tell them apart.
8. As a player, I want selecting a hero to show it on the menu: its gear, its last Location, whether a Corpse is waiting and where, its total monsters killed and the average item level of what it wears, so that I know where each stands before I play.
9. As a player, I want a Play button, and a click on a slot to enter nothing, so that browsing my heroes never starts a game.
10. As a player, I want to create a hero by typing a name, so that I can start another without a class screen that does not exist yet.
11. As a player, I want a name the file system cannot hold to be refused with the offending characters named, so that I know what to change.
12. As a player, I want to delete a hero after a confirmation naming it, so that I cannot lose one by a stray click.
13. As a player, I want a save that cannot be read to appear as a slot I can select, that says why and offers only Delete, so that a bad file does not hide or break the slots.

**In the game**

14. As a player, I want Cancel to put a held item back first, so that an accidental press never closes a panel with an item stuck on my cursor.
15. As a player, I want one Cancel to close every open panel, so that I do not press it four times to reach the menu.
16. As a player, I want a Cancel with nothing left to close to open the Pause Menu, so that the menu key is the same key as every other back-out.
17. As a player, I want the Run to hold still while the Pause Menu is open, so that I cannot be hurt while reading Settings.
18. As a player who paused with Space, I want closing the Pause Menu to leave me paused, and one who was not to resume, so that the menu never changes my own Pause.
19. As a player, I want the panel hotkeys and Space ignored while the menu is open, so that I cannot play behind it.
20. As a player, I want Settings in the Pause Menu, so that I can tune the game without leaving it.

**Leaving**

21. As a player in the field, I want to be asked before Save & Exit ends my Run, and told it Recalls with everything kept and no penalty, so that I am never surprised.
22. As a player in Town, I want Save & Exit to go straight through, so that nothing asks me what it does not need to.
23. As a player, I want Quit Game (native builds) to follow the same rule, so that closing the application is never a way to lose a Run.
24. As a player, I want a failed save to stay in the game and tell me why, so that a Run's banked loot is not thrown away silently.

**Failing well**

25. As a player, I want a hero that fails to load to leave me on the menu with the reason, so that nothing half-enters the game.
26. As a player, I want to be told once when my latest save was damaged and an older one was loaded, so that I know progress may be missing.
27. As a player, I want to be told how many items could not be restored, and to see which on request, so that I know what the save could not give back.

## Implementation Decisions

**Scenes and boot**

- The build order puts `MainMenu` first and `Example` second. A built player's `GameBoot` still
  continues the last-selected hero, because the menu's showcase needs a hero in the Session, but it
  **never writes the fallback "Hero"**: with no saves the Session keeps the default-template hero, which
  is not from a file and so is never saved (`Save()` already refuses it), and the menu shows only the
  create slot. The Editor keeps `LoadLastOrCreate`, so Play from `Example` stays a developer shortcut. If
  `Example` starts in a built player with a Session hero that did not come from a save, it loads
  `MainMenu`. This is what makes the name entry reachable: a first launch finds no saves and creates none.
- **Play always loads the selected hero from disk again** before changing scene, even though selecting
  it already loaded it. Load replaces the Hero and the World together, so nothing of the previous trip
  leaks, the Supply is restocked as the research (§6.4) requires, and every Save & Exit then Play
  round-trips the save. The scene change goes through `SceneProvider` with its progress display.
- The services outlive scene loads; only the views are rebuilt. The `HeroSaveService` result of a load
  (its `LoadStatus` and `RestoreReport`) is held until the game scene has shown its entry notice, then
  dropped.

**Main Menu**

- Buttons: **Play**, **Settings**, **Changelog**, **Credits**, **Our Discord**, **Report a Bug**,
  **Exit**. The existing Start Run button becomes Play: a Run starts in Town, not here. There is no
  separate Heroes screen: the hero slots and the showcase are on the menu itself, as built. The version
  label and Changelog button are #226 and #228; lay out Settings beside the Changelog button as that
  spec's Further Notes asks.
- On arrival the last-selected hero is the one selected and shown (else the newest save that reads);
  Play enters whichever slot is selected, and is disabled with no readable hero selected.
- **Exit and Quit Game are hidden in WebGL**, where `Application.Quit` does nothing. In their place the
  main menu shows a status line for the last-selected hero: "Saved: Aria, Level 12, 14:32" (local time,
  from the save's saved-at). Native builds keep Exit.
- Cancel on the Main Menu steps a sub-screen back to the root and does nothing at the root, so it cannot
  quit the application by accident.

**Hero slots and the showcase** (the existing `HeroSlotToggle` row and the preview-only hero displays,
#244; this spec adds to them rather than replacing them)

- A slot shows the template's icon; the selected hero's name, level and the template's class name show
  in the showcase beside its gear. Slots keep `List()`'s order, newest save first, and the
  last-selected hero is the one selected. No saved time, no created date, no mode.
- **Selecting a slot loads that hero into the Session** (as today) and enters nothing: the menu is not
  a Run, and a load only swaps the Hero and the World. The showcase is therefore the hero itself, and
  needs no separate read of the file. Besides what it already shows, the menu adds a text area for:
  - the **Location** the hero is set to Send to; blank before the first Send. Town is never a Location.
    "Highest unlocked" is not shown: Location unlocks do not exist, and when they land it becomes a
    second line;
  - whether a **Corpse** is waiting, and the Location it lies at;
  - **total monsters killed**, a new lifetime figure (below);
  - the **average item level of the worn Equipment**: the mean `ItemLevel` over the worn items only,
    coins excluded, empty slots not counted, blank with nothing worn.
- The create slot (today a click that creates and loads a hero) first asks for a name: trimmed, one to
  twenty characters, duplicates allowed (the generated id is the identity, see **Hero save**),
  characters a file name cannot hold refused with those characters named. It then creates, loads and
  selects the new hero; Cancel in the name entry returns to the slots and creates nothing.
- **Delete** acts on the selected hero, behind a modal naming it. An unreadable save keeps its slot but
  is now selectable (today it is not interactable): selecting it shows the reason and leaves only
  Delete available, which removes the file and its backup. Deleting the last-selected or active hero
  clears both, as `HeroSaveService.Delete` already does, so a later quit cannot recreate it; the menu
  then selects the newest readable hero, or none.
- Rename has no screen. The service supports it for later.

**Total monsters killed** is a lifetime figure on the **Hero** and in the **Hero save** (a `totalKills`
field, default 0, so older saves need no migration). It rises by one per **Kill**, counting Kills in a
Run that ends in Death. A Run in the Field is never saved, so the Run's count is folded into the hero
when the Run settles, with the other settle-time banking. The average item level is derived on read and
has no field.

**Cancel**

- Built on #69: `CancelDrag` (value 10, bound to Escape) becomes the general **`Cancel`**; the enum
  member keeps its number, the asset action is renamed with it. Two actions on one key would both fire.
  `Cancel` is **not offered by the rebinding screen**: ADR-0019 already has Escape cancel an interactive
  rebind.
- One ranked chain in the game project. A layer registers a handler under a rank; Cancel goes to the
  first rank whose handler consumes it and stops there. Ranks, top first:
  1. **Drag**: a held Package returns to its origin (`DragProvider` stops subscribing itself and
     becomes this layer).
  2. **Modal**: the open menu screen closes to the one beneath it: Settings back to the Pause Menu or
     Main Menu, a delete confirmation dismissed without deleting, name entry back to the hero slots.
  3. **Context**: any Inventory Context other than `None` returns to `None`, closing the Hero Panel,
     the Town Stop panel and the ground tab together.
  4. **Pause Menu**: with nothing above, Cancel opens it; with it open, Cancel closes it.
- A layer that registers is not told about the others, and the ranks are an enum in the game project.
  When #69's settings screen exists, nothing about the chain changes.

**Pause Menu**

- Items: **Resume**, **Settings**, **Save & Exit to Main Menu**, **Quit Game** (native builds only).
  No manual Save: saves happen at settle points and never while a Run is in the Field, so a Save
  button would lie or sit disabled half the time. Available in Town and in the Field.
- It is a **modal**: a scrim dims the screen and blocks clicks; the panel hotkeys and Space are ignored
  while it is open; only Cancel and Resume close it.
- **The menu holds the sim on its own.** It does not toggle `IsPaused`, which is the player's Pause
  and is refused in Town. A separate hold on the simulation stops the tick for as long as the menu is
  open and releases it on close, so a Space-paused Run stays paused and a running one resumes. In Town
  there is nothing to hold. Sim speed never goes through `Time.timeScale` (ADR-0008).

**Save & Exit**

- In Town: write the hero, load the Main Menu.
- In the Field: first a confirmation: leaving ends the Run, the hero Recalls with everything it holds
  and no penalty. On confirm, the implicit Recall (the existing quit path, `GameExit`), then the write,
  then the Main Menu. **Quit Game** (native) follows the identical rule, then quits.
- A Package on the cursor is normalised back to its origin before the write by the existing
  before-save normaliser.
- **A failed write stays in the game.** The screen says what failed and offers **Retry** and a
  secondary **Exit without saving** behind its own confirmation. Nothing leaves silently with the
  Recall's banked loot unwritten.

**Failures name what failed**

- `Save()` returns a result carrying a short player-facing reason (storage blocked, no room, a
  before-save step failed) instead of a bool and a log line. The technical text stays in the log.
- Hero slots, unreadable save: "File damaged and no backup could be read" (`Corrupt`) or "Saved by a
  newer version of the game" (`NewerVersion`).
- Selecting or playing a hero whose file reads in the list but fails to load: stay on the menu, mark the slot
  unreadable, show the same reason.
- Entry notices, shown once on entering the game and only when something went wrong: for
  `RestoredFromBackup`, "Your latest save was damaged. Loaded the previous one, so recent progress may
  be missing"; for a non-clean `RestoreReport`, "N items couldn't be restored", with the names behind a
  "Show" action. A clean load shows nothing.

**Settings**

- One panel (#193's), opened from the Main Menu and from the Pause Menu. There is no side-panel toggle
  for it: Side Panels belong to the Inventory Context, and a second kind would give Cancel two close
  meanings. Cancel inside it closes it back to whichever menu opened it.
- Settings live in `PlayerPrefs` (per machine; ADR-0019 puts the key bindings there too), so they
  work on the Main Menu before any hero is loaded and `SaveLocation.WipeAll` does not clear them.
  This corrects the "separate file" wording in `2026-09-28-player-session-flow-research.md` A1 and the
  hero-persistence spec's Out of Scope; the settings spec's `PlayerPrefs` stands.

**Service additions** (named for behaviour, not signatures)

- The menu's text area reads the hero in the Session: the Hero gains its lifetime Kill count and a pure
  average-worn-item-level function, and the Location and Corpse come from what it already holds. No
  second read of the file. `Save()` gains the result with a reason.
- The mapper and simulation gain the lifetime Kill count and its settle-time fold.
- The simulation gains the menu hold beside `IsPaused`.
- `GameBoot` gains the player/Editor split above.

## Testing Decisions

- **EditMode, engine-free:** the Cancel chain (rank order, consume-and-stop, a handler registering and
  leaving, `Cancel` closing the Pause Menu); the menu hold against Pause (Space-paused stays paused,
  running resumes, Town is a no-op, a hold never touches `IsPaused`); the Kill fold at settle for
  Recall and Death and a pre-stat save reading 0; the average worn item level (coins, empty slots,
  nothing worn); the hero name rules; the `Save` reason for
  each failure; the entry-notice selection from `LoadStatus` and `RestoreReport`. Prior art:
  `SimulationServiceTests` for the pause tests, `HeroSaveServiceTests` and `HeroSaveFailureTests` for
  the save paths, `GameInputTests` and `FakeInput` (from #69) for pressing Cancel.
- **PlayMode:** MainMenu → Play → `Example` → Save & Exit → MainMenu with the hero on disk and the
  banked Run's loot present after Play; a first launch with an empty save store offering only the create
  slot and writing no hero; a built-player boot writing no fallback hero; `Example` with an unsaved
  Session hero loading the menu. Prior art:
  `BootPlayModeTests` and `HotkeyPlayModeTests`.
- **By hand:** the scene layout and prefab authoring, the scrim, the WebGL status line, and the
  Editor-only shortcut. Run the Play-exit check (`PlayExitCheck`) after touching the quit wiring.

## Out of Scope

- **Hardcore mode.** It breaks "Death is not a game-over" (ADR-0009) and needs its own grilling; it goes
  to the Icebox. The hero save carries no mode field.
- Hero classes or archetypes; the name stays the only thing a player chooses.
- Auto-opening the Pause Menu when the tab loses focus; browser fullscreen swallowing Escape.
- Gating the debug panel and dev spawners out of the player path.
- Rename on the hero slots; a "highest unlocked Location" line (needs unlocks).
- Audio anywhere in the menus.
- Saving a Run in the Field, or any best-effort save when a browser tab closes. A tab closed mid-Run
  loses that Run's loot; this is a **Known Issue** for the Changelog (#230), not a fix here.
- The settings panel's contents and the rebinding screen: #193 and its spec, and the screen #69 left
  unbuilt.

## Further Notes

**Suggested slicing for `/to-tickets`** (`dev/frontier.py` derives the order from each issue's
*Blocked by*):

1. The lifetime Kill count: field, mapper, the settle-time fold, tests.
2. The hero's menu figures: the average worn item level as a pure function, tested engine-free.
3. `Save()` result with a reason, and the load result held for the entry notice.
4. The simulation's menu hold beside `IsPaused`, with tests.
5. `Cancel` replaces `CancelDrag`, the ranked chain, the Drag and Context layers. *Blocked by #69.*
6. Boot split (no hero load in a built player), build order, the `Example` guard.
7. Main Menu buttons: Play replaces Start Run, the last hero selected on arrival, the status line, WebGL button rules. *Blocked by 6; lay out with #226 and #228.*
8. Hero slots: the text area (Location, Corpse, Kills, average gear level), name entry on the create slot, Delete with confirm, selectable unreadable slots, Play. *Blocked by 1, 2, 6.*
9. Entry notices on entering the game. *Blocked by 3, 8.*
10. Pause Menu, the Modal and Pause Menu ranks, Save & Exit and Quit Game with the failure path. *Blocked by 3, 4, 5.*
11. Settings entry buttons on both menus. *Blocked by #193's panel.*
12. Doc corrections: the stale statements the grilling found (`MainMenu.unity` "not in the build
    settings", the settings-storage wording above).

**Amended 2026-10-12, after #69 merged.** The grilling designed a list-plus-Play Heroes screen without
having read `MainMenu.unity`, which already carries hero slots (`HeroSlotToggle`: selecting loads the
hero, the create slot creates and loads one) and a preview-only showcase of the loaded hero (#244).
The owner chose to keep the slots and showcase, so the peek read, the boot that loads no hero, the
Continue button and the separate Heroes screen were dropped for the sections above.

**Stale statements found.** The hero-persistence spec says `MainMenu.unity` is not in the build
settings; it is, at index 1. The settings-storage wording above. The Icebox "Pause Menu" says it stops
"ticker time propagation"; the mechanism is the sim hold, not `Time.timeScale`. The player-session
research §6 pre-dates `HeroSaveService`.

**No ADR.** Cancel as a ranked chain, the Pause Menu's separate hold and the lifetime Kill count are each
cheap to reverse and the obvious shape once #69 exists. The glossary already carries the new terms:
**Main Menu, Play, Hero slots, Pause, Pause Menu, Cancel, Save & Exit**, and the Kill count on
**Hero** and **Hero save**.
