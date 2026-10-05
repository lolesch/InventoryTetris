# Hero persistence — save the Hero, continue it after a restart

Date: 2026-10-05
Status: Spec, ready for `/to-tickets`. Settled by a grilling on 2026-10-05 (three rounds, every
recommendation accepted). Builds on `2026-09-28-save-serialization-prior-art.md` (its §5 owner
decisions stand) and `2026-09-28-player-session-flow-research.md` (Phases C and D). This is spec 1
of 2; spec 2 (`hero-select-front-end`: MainMenu, hero list, create, delete, pause menu, #46) is out
of scope here and consumes the service this one builds.

## Problem Statement

The Hero is rebuilt from its `HeroData` template on every Play entry and every launch. Nothing is
written to disk, so a player who equips a hero, plays Runs, stops and starts again gets a fresh hero:
the gear, the bag, the Stash, the Wallet, the level, the Corpse and the Behaviour Profile tuning are
all gone. The foundational rework built the item-level half (an item instance round-trips to a
plain Dto in memory) and ADR-0015 built the seam (a hero load replaces the Hero and the World), but
no save format, no store, no file and no way to list, create or continue a hero exists. The
developer has the same problem: every Play entry starts from the scene and the debug spawners, so a
change to equipment or loot cannot be tested across a restart.

## Solution

The Hero, and only the Hero, saves. A save is one file per hero in a saves folder, wrapped in a
versioned envelope and written atomically with a backup. A small Account file records which hero
was last selected. On launch the game continues the last-selected hero, or creates one from the
default template when there is none. The save is written at the points where the Hero is final:
after a Recall or a Death has settled, on exit, on a hero switch and on quit. A quit in the middle
of a Run Recalls first, so what the hero already picked up is banked.

The mechanism (store, serializer, envelope, slot, migration chain, mapping contract) is generic and
lives in the `Utility` submodule. The format (the Dtos, the mappers, the restore) is this game's and
lives in a new assembly. A `HeroSaveService` exposes the operations a front end needs (list, create,
delete, load, last-selected) so spec 2 is only views over it. Loading a saved hero builds a new
Hero and World pair and swaps it in through the existing Session, exactly as ADR-0015 decided.

## User Stories

1. As a player, I want my hero's equipment to still be worn when I stop and start the game, so that my progress is not lost.
2. As a player, I want my Inventory and its coins to come back exactly as I left them, so that my Wallet is intact.
3. As a player, I want my Stash to come back with the hero, so that stored loot is not lost between sessions.
4. As a player, I want my level and my progress toward the next level to be kept, so that the XP I earned still counts.
5. As a player, I want my hero's current Health and Resource to be kept, so that quitting right after a near-Death does not heal me for free.
6. As a player, I want my six Behaviour Profile sliders to come back as I tuned them, so that I do not retune the hero every launch.
7. As a player, I want the Location I last chose to be selected again, so that Send goes where I expect.
8. As a player, I want my unclaimed Corpse to survive a restart, so that recovering my bag is still possible after I quit.
9. As a player, I want a quit in the middle of a Run to bank what my hero already picked up, so that closing the game does not throw away a good run.
10. As a player, I want the game to come back in Town after a launch, so that I never resume mid-fight.
11. As a player, I want a Death to be saved with its Corpse and its penalty, so that quitting afterwards cannot undo the Death.
12. As a player, I want a Recall to be saved, so that the loot I just brought home survives a crash a moment later.
13. As a player, I want an item I was dragging when a Recall fired to go back where I lifted it from, so that the save never loses an item that was on the cursor.
14. As a player, I want a first launch with no save to just start a hero, so that there is no setup before playing.
15. As a player, I want a new hero to start with a small authored kit, so that the first Run has something to work with.
16. As a player, I want a hero whose save file is damaged to be recovered from its backup, so that one bad write does not cost me the hero.
17. As a player, I want a hero I can no longer recover to start fresh rather than block the game, so that a bad file never stops me from playing.
18. As a player, I want a damaged file to be kept aside and not erased, so that it can still be recovered by hand.
19. As a player, I want a save written by a newer version of the game to be left untouched, so that opening an old build never destroys it.
20. As a player, I want an item whose definition was removed from the game to be set aside rather than crash the load, so that the rest of my hero still loads.
21. As a player, I want a saved Corpse at a Location that no longer exists to be set aside rather than crash the load, so that content changes cannot brick a hero.
22. As a player, I want an item that no longer fits because a container shrank to land in the first free Inventory cell, so that a size change does not delete it.
23. As a player, I want an item set aside by a load to be recorded in a file beside the save, so that a renamed definition can be recovered by hand.
24. As a player, I want a failed write (disk full, file locked) to leave my previous save intact, so that a bad moment never costs more than the last session.
25. As a player, I want the game to try again at the next save point after a failed write, so that a transient failure heals itself.
26. As a player, I want several heroes to be able to exist on disk, so that choosing among them is possible once there is a menu.
27. As a player, I want renaming a hero never to move or lose its file, so that identity and display name stay separate.
28. As a front-end developer, I want to list the saved heroes with id, name, level, last-saved time and load status, so that a selection screen is only a view.
29. As a front-end developer, I want to create a hero by name, delete one by id and load one by id through one service, so that the menu holds no persistence logic.
30. As a front-end developer, I want to read and set the last-selected hero, so that Continue is one call.
31. As a developer, I want Play in the Editor to continue the last hero by default, so that "run, stop, run" keeps my equipment.
32. As a developer, I want a Start Fresh Each Play toggle, so that I can test a new-hero path without deleting files.
33. As a developer, I want a menu item that wipes every hero save and one that opens the saves folder, so that clearing or inspecting a save is one click.
34. As a developer, I want the Start Fresh toggle stored per machine and not in an asset, so that it neither dirties the project nor follows me to another machine.
35. As a developer, I want the save to be written after the Death settlement, so that a Death never saves a hero with its Corpse missing and its bag still full.
36. As a developer, I want the save trigger to be a service event that survives a hero swap, so that no subscription has to be rebuilt per World.
37. As a developer, I want stopping Play Mode with a live Run to leave a saved hero in Town, so that the Editor Stop is covered like a player quit.
38. As a developer, I want stopping Play Mode to raise no teardown errors from the save path, so that the Stop-time error class fixed in ADR-0015 does not return.
39. As a developer, I want the level-to-modifier rule in one pure function used by both the level-up path and the loader, so that a load never replays the XP gain.
40. As a developer, I want the restore to place gear through the Equipment's own placement path, so that stats re-apply and a two-hander still occupies both slots.
41. As a developer, I want the current resource values applied after gear and level, so that a hero with gear-raised maximums is not clamped on load.
42. As a developer, I want the save layer to know nothing about UI, so that the engine-free assemblies stay testable without a scene.
43. As a developer, I want the generic save mechanism to live in the submodule with no game vocabulary, so that AutoBattler and the other consumers can adopt it on their own schedule.
44. As a developer, I want the serializer behind a seam, so that a move off `JsonUtility` is one adapter.
45. As a developer, I want a schema version on every file and an empty-but-wired migration chain, so that the first schema change does not break old saves.
46. As a developer, I want one round-trip test through the whole service over an in-memory store, so that the mappers, the restore order, the tolerance rules and the Corpse are covered at the highest seam.
47. As a developer, I want the file store tested against a temp directory, so that atomic replace and backup are proven without a scene.
48. As a developer, I want the Stash and the Wallet sections of the Dto kept separable from the rest of the hero, so that lifting the Stash to a shared tier later changes its owner and not the format (ADR-0014).

## Implementation Decisions

**What saves.** The Hero only (ADR-0015 consequence). Saved: id, name, level, the current values of
Health, Resource, Shield and Experience, the six Behaviour Profile values, the selected Location id,
the Corpse, and the Equipment, Inventory and Stash contents. Not saved: stat modifiers (derived),
the dev flags (`IsInvincible`, `IsBlocking`, `SpendResource`), regeneration timers, the Behaviour
Profile's cast latch, the Wallet (it is the Inventory's coin cells, so saving the Inventory saves it),
and everything the World holds (Supplies, the Sold container, the Run and its ground Drops, the
Inventory Context). Base stats are not saved: a loaded hero is built from the one default template,
and retuning that template changes every saved hero. No template reference is written now; adding
one later is a schema migration that defaults a missing value to the default template.

**Layout.** One file per hero in a saves folder under the platform persistent data path, named by the
hero's generated GUID. The name is display-only, so a rename never moves a file. A separate Account
file holds the last-selected hero id and nothing else for now; it is the home ADR-0014 reserves for
a shared Stash tab. The hero list is derived by scanning the folder, so there is no index file to
fall out of step. The Stash and Wallet sections of the hero Dto stay separable (ADR-0014).

**The submodule (`Utility.Persistence`, a new assembly).** Everything game-agnostic, per the owner's
2026-09-28 decisions: a key/value save store interface with a file store and an in-memory store; a
serializer interface with a `JsonUtility` adapter; a versioned envelope (schema version, saved-at
UTC, payload); a save slot that stores, loads and reports a status of Missing, Loaded,
RestoredFromBackup, Corrupt or NewerVersion; the migration chain; and the mapping contract
(`IDtoMapper<TDomain, TDto>`, a separate object so it can take a catalog and the domain type can
stay immutable). The file store writes a temp file and replaces atomically, keeping one backup. The
directory is injected, so the persistent data path lives only in the game's composition root.
Synchronous: local saves are small, and an async sibling is a later addition if cloud saves land.
The submodule is committed first and the pointer bumped second (the two-step flow in
`docs/agents/codebase-notes.md`).

**The game side (`InventorySystem.Persistence`, a new assembly below `Services`).** The Dtos
(hero, container, Corpse, Account), the mappers and the restore. Dto rules: `[Serializable]`
classes, public fields, arrays not dictionaries, enums by name, content by stable string id. A
container is an array of `{x, y, instance, amount}`. The Corpse is a Location id plus item Dtos.
Item Dtos go through the existing item instance round trip, restored with the catalog overload so a
saved coin re-stamps onto the current denomination ladder.

**Restore order.** A new Hero and World are built the normal way, then: containers and gear are
placed (Equipment through its own placement path so stats re-apply and a two-hander takes both
slots), then the level and its Experience-threshold modifiers, then the current resource values.
Current values go last so a maximum raised by gear or level is in place before the current is set.

**Level and XP.** Today a level-up adds one modifier to the Experience resource's maximum and
heals; nothing else grows with level. A save stores the level and the current Experience, never the
modifiers. One pure function yields the modifiers for a level; the level-up path and the loader both
call it, so a load never replays the XP gain.

**Load tolerance.** An unknown item definition id, an unknown Location id and a package that no
longer fits are quarantined, logged and skipped, and the rest of the hero loads. A package that no
longer fits at its saved cell goes to the first free Inventory cell first and is quarantined only
if there is none. An enum name that does not parse still throws, as the item round trip does today.
Quarantined items are appended to a sidecar file beside the hero file with the item Dto, the reason
and the container. The sidecar is never read back by the game and is not part of the hero Dto, so
a normal save never rewrites or grows it.

**Failure handling.** RestoredFromBackup loads the backup and logs a warning. Corrupt with no usable
backup renames the file aside (never deletes it), logs an error and starts a fresh hero. NewerVersion
is never read as a hero and never overwritten: the loader starts a fresh hero under a new id. A write
that throws logs an error, leaves the previous file untouched (the write is atomic) and is retried
at the next write point.

**Write points.** After a Recall or a Death has settled, on exit to menu, on a hero switch, and on
quit. No timer. A Town-side change (equip, buy, Stash move) is saved at the next write point; a crash
in between loses it, and that window is accepted for v1. A one-line extension (also save when a Town
Stop panel closes) is the known cheap fix if playtests find it hurts.

**The Death ordering (a real hazard).** The Run raises its ended event inside the Death handling,
before the simulation service buries the bag in the Corpse. A save hooked to the Run's own ended
event would write a post-Death hero with no Corpse and a full bag. So the simulation service gains
one service-level event, raised after the Death settlement and after the Recall's own end-of-run
work. The save service subscribes to it once. It survives a World swap because the simulation
service is one instance, so nothing is re-subscribed per World.

**Quit.** A quit, and an Editor Stop (which raises the same signal), Recalls a live Run first, which
banks what the hero holds, then writes. It is wired where the boot arms the services and is released
on the Editor's `EnteredEditMode`, not on `ExitingPlayMode`, following the teardown rule in
`docs/agents/codebase-notes.md`, so it cannot reintroduce the Stop-time error class ADR-0015 fixed.

**The cursor.** A Recall can fire while the player is dragging from the Hero Panel. The cursor lives
in a canvas-nested provider the engine-free save code cannot see. The save service exposes a "before
save" list of normalisers; the drag provider registers one that does Return to Origin, and the
snapshot runs them first. Return to Origin never destroys a Package.

**`HeroSaveService` (in `Services`).** Over a save slot per hero and the Account slot: list (id,
name, level, saved-at, load status), create by name, delete by id, load by id, and read and set the
last-selected hero. It is registered at boot like the other services. The composition root picks the
persistent data path; tests inject an in-memory store. A first launch with no Account file and no
hero files creates a hero named "Hero" from the default template, with a fresh GUID, and writes it
at once, so quitting in Town without ever pressing Send still leaves a save.

**Loading into the game.** `ISession` gains a load that takes a hero Dto, beside the existing load
that takes a template. The session already holds the one function that builds a Hero and World pair;
that function (in the builder) gains a path that restores a Dto onto the hero it just built, so a
loaded hero is never a different shape from a new one and the Session learns nothing about save
formats. The #114 rule is unchanged: a load refuses while the Run is in the Field. A load swaps the
pair whole and raises `HeroLoaded`; nothing is hydrated field by field.

**Play entry.** Pressing Play, or launching, continues the last-selected hero when a save exists,
and creates one otherwise. A Start Fresh Each Play switch bypasses the read.

**Dev tooling (Editor only).** Menu items under Tools: Wipe All hero saves, Open the saves folder,
and a Start Fresh Each Play toggle stored in `EditorPrefs`. Per machine, so it neither dirties an
asset nor follows the developer to another machine, and `GameConfig` stays immutable at runtime
(ADR-0015). A player build has no switch. The `DebugPanel` (#118) may add a button later without
changing the service.

**Domain docs.** ADR-0017: the save mechanism lives in the `Utility` submodule and the save format
does not (the ADR-0011 shape). `GLOSSARY.md` gains **Account** (the tier above the Hero that owns
the hero list and the last-selected hero; Avoid: profile, save file, user) and a **Hero save**
entry, and the **Session** entry's "the save is the Session's shadow" line is updated to name the
Hero as what saves. ADR-0014's open questions stay open; this spec does not move the Stash.

## Testing Decisions

A good test here exercises behaviour through the public surface: build a session, change it, save,
build another session from the file, and compare what a player could observe. It does not assert on
Dto field names, file bytes (outside the file store), or the order of internal calls.

- **The one seam: `HeroSaveService` over an in-memory store.** Covers the mappers, the restore order,
  the level function, the Corpse, tolerance (an unknown definition id, an unknown Location id, a
  package that no longer fits), the Dto round trip for each container including a two-hander and
  stacked coins, create/list/delete/last-selected, and the first-launch path. Prior art: the container
  fixtures with fake stat receiver and currency minter, `ItemInstanceDtoTests`, `SessionLoadTests` and
  `SessionBuilderTests`.
- **Death ordering.** A test that drives a Death through the simulation service and asserts the
  saved hero has its Corpse and an empty bag, and that the service event fires after settlement.
  Prior art: `SimulationServiceTests`.
- **Cursor normaliser.** A test that registers a normaliser and asserts it runs before the snapshot.
- **The file store, in the submodule.** Tests against a temp directory for atomic replace, the
  backup, a corrupt file, a missing file and a newer-version envelope, plus the save slot's status
  results and the migration chain. Prior art: `ServiceRegistryTests`, `PlayerLoopHookTests` in
  `Submodules.Utility.Tests`.
- **Quit and Editor Stop.** A PlayMode test that stopping with a live Run leaves a saved hero in Town
  with the Run's loot banked (PlayMode, because EditMode `Run All` cannot reach the boot hooks), and
  the Play-exit check (`PlayExitCheck`) after touching the quit wiring.
- **Not tested by assertion:** the Editor menu items (verified by hand).

## Out of Scope

- Hero selection UI, MainMenu to Game flow, create/delete screens, name rules and delete confirmation,
  the pause menu (spec 2), and `#46` scene-scoped providers.
- Hero classes or archetypes, and a template reference in the Dto.
- A shared Stash tab and any state owned by the Account beyond the last-selected id (ADR-0014, Stash tabs).
- Saving the World: Supplies, the Sold container, the Run and its ground Drops, the Inventory Context.
- Settings and key bindings (`#69`), which are a separate file.
- Gating the dev spawners out of the player path (`#118`).
- A timer or per-change autosave, and async or cloud saves.
- Newtonsoft; `JsonUtility` stands, and the serializer seam makes a change one adapter.
- Migrating the other `Utility` consumers; they adopt the submodule on their own schedule.

## Further Notes

**Suggested slicing for `/to-tickets`** (each a vertical, testable slice; `dev/frontier.py` derives
the order from each issue's *Blocked by*):

1. `Utility.Persistence`: store, in-memory store, serializer, envelope, save slot, migration chain, mapping contract, with tests (submodule commit, then pointer bump).
2. The level-to-modifier pure function, with the level-up path moved onto it.
3. Container Dtos and mappers (Inventory, Stash, Equipment) and the restore, through the Equipment's placement path.
4. A Location lookup by id, and the Corpse Dto and restore.
5. The hero Dto, `ISession` load of a Dto, the builder's restore path, and load tolerance with the quarantine sidecar.
6. `HeroSaveService`: list, create, delete, load, last-selected, the Account slot, failure handling and the first-launch path.
7. The simulation service's post-settlement event, the save triggers (Recall, Death), and the cursor normaliser.
8. Quit and Editor Stop wiring, and Play entry continuing the last hero.
9. The Editor menu items, ADR-0017 and the glossary entries.

**Facts worth keeping next to the decisions.** `RunState` raises its ended event before the Corpse is
buried (the Death ordering above). A coin saved before the denomination ladder existed re-stamps on
load only through the catalog overload of the item round trip. `Corpse` matches a Location by profile
reference, so the loader must reuse the one profile per Location the simulation service memoizes.
The Wallet needs no save of its own. `MainMenu.unity` exists but is not in the build settings; that
is spec 2's concern.
