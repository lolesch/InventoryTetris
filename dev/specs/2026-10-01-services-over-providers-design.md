# Services Over Providers

Date: 2026-10-01
Status: Draft — settled with `/rederive` and two `/drift-review` passes; not yet sliced. Slice with
`/to-tickets` (the amended slice is in Further Notes), then `/drift-review` it once more before
`/implement`.
Base: `main` at `7aff289`.
Supersedes the open question in `2026-09-05-provider-seam-design.md` (*Is the singleton still the
right call?*) and the scene-scoped-providers premise of `2026-09-28-player-session-flow-research.md`
(A7, section 3, point 3).

## Problem Statement

Every game-wide thing in the project is reached through `Provider.Instance`, and every provider is a
scene object found by scanning the scene or created empty on first ask. That one mechanism fuses three
jobs: it holds authored data (the catalog, the distributions), it holds mutable runtime state and the
logic over it (the containers, the Wallet, the Run), and it holds scene references (the hero, the
loading screen's panel).

The seams show where it hurts. A provider created fresh mid-run has no Inspector-authored fields, so
`InventoryProvider` already works around it with register-by-role seams for its panels. The Play-mode
quitting flag leaked across Play sessions with domain reload disabled, and the reset guard silently
stopped working when a base class was split. A provider that is not authored as a root object errors at
edit time, and one authored under a Canvas once dragged the toggles out of it (#46). The player, a
`MonoBehaviour` only because the stats happen to be serialized on it, owns a UI display pool, so the
model knows about its own view. Nothing can be built in a test without a scene: `LocalPlayer.PickUpItem`
cannot be unit-tested because it reaches three containers through `InventoryProvider.Instance`.

The honest need is small: global access to authored data, and a way to manipulate the state built from
it, that is the same in every scene, available before first use, and replaceable when a different hero
is loaded.

## Solution

Retire the scene-scanned provider for everything except the three scene-scoped UI singletons. Replace
it with three kinds of thing, each of which can be a plain object:

- **Authored data**: one immutable `GameConfig` asset set, loaded before the first scene, identical in
  every scene. Nothing writes it at runtime.
- **Services**: plain classes with the functions over that data. A config service (item rolls, currency,
  catalog queries) is built once from `GameConfig`. A state service (inventory, simulation) operates on
  the Hero State.
- **Hero State** (working name; ticket 1 settles it against the glossary): one plain, serializable state
  model per hero: the hero itself, the four containers, the Wallet, the Sell Basket, the Inventory
  Context, the behaviour sliders, the selected Location, the Corpse and the Run. Replaced as a unit when a
  hero is loaded.

`.Instance` stays as a static **locator**, but it only returns a service and is called only from the
Unity edge: views and `MonoBehaviour`s. The core takes its dependencies by constructor and never calls
`.Instance`. The only scene object left is one hidden runner `MonoBehaviour` that hosts the simulation
tick and coroutines.

The invariant, as settled: **one service type maps to exactly one plain instance, built from an
immutable authored asset set that is identical in every scene, operating on one serializable state
model; the core takes it by constructor, and only the Unity edge reaches it through the static
locator.**

## User Stories

1. As a developer, I want every game-wide service built from one authored asset set before the first scene loads, so that no service can exist unconfigured.
2. As a developer, I want a missing `GameConfig` to fail loudly at boot, so that a mis-authored project is caught on the first Play, not when the first loot roll silently returns nothing.
3. As a developer, I want two instances of a service to be unrepresentable, so that a scene-scan-and-disable guard is never needed.
4. As a developer, I want a service's config to be identical in every scene, so that behaviour never depends on what one scene happened to author.
5. As a developer, I want `.Instance` to never touch the scene, so that reading it in the Editor, in an EditMode test or in an `[InitializeOnLoad]` hook cannot create, reparent or dirty anything.
6. As a developer, I want the locator reset in `SubsystemRegistration`, so that with domain reload disabled a second Play entry starts from a clean boot instead of inheriting a stale or quitting service.
7. As a developer, I want the quitting flag and its reflection-based reset guard gone from this project, so that a refactor of a base class can no longer silently disable it.
8. As a developer, I want the core to take its dependencies by constructor, so that a roll, a pickup or a settlement is unit-testable with a fake and no scene.
9. As a developer, I want only views and `MonoBehaviour`s to call the static locator, so that the one place a hidden global is reached is the one place Unity forces it.
10. As a developer, I want the construction order of the Hero State to be explicit (hero first, then the containers that take it as their stat receiver), so that it no longer depends on which `.Instance` call fires first.
11. As a developer, I want the hero to be a plain class with a data template, so that its stats, resources, regeneration, XP and modifier comparison are unit-testable without a `GameObject`.
12. As a developer, I want the hero's stat display out of the hero, so that the model does not own its own view.
13. As a developer, I want a `CharacterStatPanel` view bound to the hero's change events, so that the stat list updates without the model reaching into a UI pool.
14. As a developer, I want the hero to heal itself when it levels up, so that `LocalPlayer` stops reaching through `CharacterProvider.Instance` to do it.
15. As a developer, I want the Healer to ask the state to heal the hero, so that it holds no reference to a hero object.
16. As a developer, I want `DummyTarget` deleted, so that a superseded combat path stops pulling the loot and XP code in a second direction.
17. As a developer, I want the loot roll to take the hero's magic find and item-quantity bonus as parameters, so that the item service no longer reaches the character service and the two stop being each other's dependency.
18. As a developer, I want the item catalog read through one service, so that the duplicate `ItemView.Catalog` static goes away.
19. As a developer, I want the item acquisition entry point to live on the Hero State's inventory service, so that pickup placement (equip, else bag, debug stash overflow) is unit-testable with real containers.
20. As a developer, I want the simulation loot path to take an item receiver by constructor, so that `LootFlow` and the Corpse recovery bag both honour auto-equip through one entry point (#103).
21. As a developer, I want the simulation service to live on the Hero State, so that the Run, the loot flow and the Corpse go with the hero that owns them.
22. As a developer, I want the simulation tick hosted by a runner that boot creates, so that the zero-setup bare scene needs no `AddComponent` or smoke-gate spawn.
23. As a developer, I want the tuning defaults in `GameConfig` and the live slider values on the Hero State, so that an Inspector `OnValidate` no longer applies defaults to a scene object.
24. As a player, I want loading a different hero to give me exactly that hero's containers, Wallet, Corpse and Run, so that nothing from the previous hero leaks into the next one.
25. As a developer, I want the Session to be replaced as one pointer swap rather than cleared field by field, so that a missed field cannot leak state between heroes.
26. As a developer, I want a `SessionChanged` event, so that every view that bound to the old Hero State can rebind.
27. As a developer, I want the container-by-role seam, the field-face-panel registration and the context subscription to re-run on `SessionChanged`, so that a panel enabled once does not stay bound to a discarded state.
28. As a developer, I want the Field face panel's registration to live outside the Hero State, so that replacing the state does not discard a reference that is only ever registered once, on enable.
29. As a developer, I want the registration seams defined once on the new Hero State API, with the old provider forwarding, so that the migrate batches rewire callers without rewriting the seams again.
30. As a developer, I want the sim's 7 GUI callers (including the enemy HP bar pool) migrated explicitly, so that no `SimulationProvider.Instance` reach survives by being nobody's ticket.
31. As a developer, I want the debug spawn, kill and amount-slider controls in one `DebugPanel` view that calls services, so that the dev tools no longer force the state class to be a scene component.
32. As a developer, I want the Amount Slider prefab's wiring moved with the debug controls, so that the slider just built for the provider is not stranded by the swap.
33. As a developer, I want the Utility submodule's `AbstractSceneSingleton` and `AbstractProvider` left in place, so that AutoBattler, which shares the submodule, is not broken by this project's refactor.
34. As a developer, I want `DragProvider`, `PreviewProvider` and `SceneProvider` unchanged, so that the swap's blast radius excludes canvas-nested UI singletons and a shared-submodule class.
35. As a developer, I want ground items display (#63) to call the acquisition entry point instead of `LocalPlayer`, so that it is built once against the surviving API.
36. As a maintainer, I want the swap sequenced expand, migrate, contract, so that every ticket lands green and the old provider is deleted only when nothing calls it.
37. As a maintainer, I want greps for the old names to come back clean in the last ticket, so that the retirement half of the swap provably happened.
38. As a future contributor, I want #46 closed with the swap and credited with the reparent guard that already shipped, so that the tracker tells the real story.

## Implementation Decisions

- **Three roles, split.** Authored data (immutable asset set), services (plain classes with functions) and state (plain, serializable model). The old provider fused all three; none of the new ones needs a scene object.
- **The singleton is a plain object.** "Found in the scene" is not its business. The static locator is armed by a boot hook before the first scene and reset in `SubsystemRegistration`. The locator returns a service, never state directly.
- **Config.** One root `GameConfig` asset loaded from a fixed key. It references the item catalog, the category and rarity distributions, the currency distribution and drop table, the stat-icon data, the currency icons, the container sizes, the sim tuning defaults and the location configs. The asset is one root, so it cannot differ per scene. `Resources` is the default loader; Addressables is not adopted for this alone because its async load would force an awaited bootstrap.
- **Nothing writes an authored asset at runtime.** With domain reload disabled a runtime write to a `ScriptableObject` survives Stop.
- **Config services.** Built once from `GameConfig`. The item service owns loot rolls, coin rolls, currency minting, catalog queries and icon lookup. It takes the roll bonuses as parameters through the existing roll context; it holds no reference to the character.
- **Hero State.** The unit of replacement. Contents: the hero, Equipment, Inventory, Stash, the Supply, the Sell Basket, the Wallet, the Inventory Context, the behaviour sliders, the selected Location, the Corpse, the Run and the loot flow. Built in explicit order. The never-saved parts (Run, Basket, ground drops, cursor-adjacent state) live in it but are excluded from any future save DTO.
- **Hero.** A plain class with a `HeroData` template (an SO for a new hero; a DTO for a loaded one). It implements the stat-receiver and item-receiver interfaces the container core already takes. Level-up heals the hero itself. The stat display pool and every `MonoBehaviour` lifecycle hook go; the initial refill is plain construction.
- **Replace, not hydrate.** Loading a hero builds a new Hero State and swaps the pointer. Views do not cache a state object across a swap; they bind at enable and rebind on `SessionChanged`.
- **Rebinding seams.** The container-by-role registration, the field-face-panel registration and the context subscription are defined on the Hero State API. The field-face-panel registration lives outside the Hero State, because the panel registers once from its own enable and would otherwise be discarded with the state.
- **Locator at the edge only.** Views and `MonoBehaviour`s call the locator. Anything in the engine-free assemblies takes its collaborators by constructor. New code goes in assemblies that a test assembly can reach (ADR-0007), not the predefined assembly.
- **Runner.** One hidden `MonoBehaviour`, created by boot and persistent, hosts the simulation tick. Coroutines for scene loading stay with `SceneProvider`, which is out of scope.
- **Debug tooling.** The debug spawn, kill, heal and amount-slider controls move to one `DebugPanel` view that calls the services. Scenes that wired the old methods by `UnityEvent` are rewired.
- **Naming.** The new services do not carry the "Provider" suffix. Exact names, and the Hero State's final name, are settled in ticket 1 and the glossary; **Session** in `CONTEXT.md` already means the span of play and must not be reused for the replaceable unit.
- **Out-of-this-project constraint.** `AbstractSceneSingleton`, `AbstractProvider` and the quitting reset guard remain in the shared `Utility` submodule. This project stops using `AbstractProvider` except through `SceneProvider`, and keeps `AbstractSceneSingleton` for the drag and preview singletons.
- **Existing issues.** #103's Corpse half takes the item receiver by constructor instead of reaching the character locator. #63's criteria name the acquisition entry point, not `LocalPlayer`, and it is blocked on the migrate batch that moves that entry point. #46 closes with the contract ticket and the close comment credits the reparent guard that already shipped. #68 settles the shared-Stash question before the Hero State's contents are fixed.

## Testing Decisions

- **One seam.** Construct the Hero State from a `GameConfig`-shaped test config and drive the public service functions. This is the highest seam, reaches the whole core, and needs no scene. Existing seams are reused below it, not replaced.
- **What a good test is.** It asserts observable behaviour through the service's public functions: where a package lands, what a roll returns, what a hero's stat total becomes, what is in the Corpse after a Death. It never asserts that a service was built a certain way.
- **Prior art.** The container fixtures with fake stat receiver, cursor sink and currency minter; the deterministic roll source behind the item generator; the engine-free simulation and loot-flow tests; the settlement tests against fake ports.
- **New coverage.** The hero's stats, regeneration, XP and level-up become unit-testable for the first time (today in the predefined assembly with no test coverage). The acquisition entry point (equip, else bag, debug stash overflow) gets the test the 2026-09-05 spec wanted. Replacement on load gets a test: after a swap, the old state's containers, Corpse and Run are unreachable through the locator.
- **Boot.** An EditMode test that building from a missing config throws, and that the locator is reset between simulated Play entries.
- **Not tested.** The runner, the scene wiring and the rebind-on-`SessionChanged` of real views are verified by hand in Play Mode, the existing gate.
- **Compile gate.** Verified through the Unity bridge or batch mode, never `dotnet build`; run a negative control.

## Out of Scope

- The hero selection screen, the heroes list and persistence of any state (the session-flow research's Phase C and later). This spec only defines the replace-on-load entry point.
- `DragProvider`, `PreviewProvider` and `SceneProvider`: canvas-nested UI and a shared-submodule class. They stay as they are.
- Any change to the `Utility` submodule or to AutoBattler.
- The read-only state inspection window (a later ticket; an Inspector mirror is rejected because editing state through it bypasses the change events).
- Changing the pickup placement rules, the death penalty, loot rules or any gameplay behaviour. This is structural.
- Addressables.
- A shared Stash across heroes. #68 decides it first; the spec assumes the Stash is inside the Hero State until it says otherwise.

## Further Notes

### Replay, as settled

The derived mechanism was replayed against every case the old one handled. The cases and their outcomes are in the `/rederive` record: lazy create (impossible: boot builds everything), duplicates (no scene object to duplicate), survive scene load (static lifetime), non-root authoring and #46 (moot), quitting and Play re-entry (reset in `SubsystemRegistration`), construction order (explicit), hero switch (replace), zero-setup scene (boot hook plus a default `HeroData`), debug buttons (`DebugPanel`), dummy (deleted).

### Amended ticket slice (after two drift reviews)

This is the input for `/to-tickets`, with the seven drift findings applied. The chain `6 → 11 → 10 → 7 → 8, 9 → 12 → 13` is deliberate: hero callers migrate before the swap so no wrapper has to be re-pointed, and the DebugPanel is written once against the relocated entry point.

| # | Ticket | Blocked by |
|---|---|---|
| 1 | Name the per-hero unit and record the service model (glossary entry plus ADR) | #68 |
| 2 | Boot: `GameConfig`, locator, runner, test seam; scene-authored values carry over | 1 |
| 3 | The item service replaces `ItemProvider` | 2 |
| 4 | `Hero` as a plain class (expand); includes the stat-receiver add/remove; default `HeroData` reproduces the scene-authored stats | 2 |
| 5 | The hero stat panel binds to `Hero` | 4 |
| 6 | The Hero State holder, built in explicit order; old providers become facades | 3, 4 |
| 11 | Simulation service on the Hero State, runner-hosted tick; **names** the 7 GUI sim callers (including #94's HP bar pool) and hero regeneration in both phases | 3, 6 |
| 7 | Replace on load, `SessionChanged`; seams defined on the Hero State API, facades forward | 6, 11, 10 |
| 8 | Migrate inventory callers: GUI | 7 |
| 9 | Migrate inventory callers: runtime and settlement; **owns relocating** the acquisition entry point (the sim wiring is 11's) | 7, #103 |
| 10 | Migrate hero callers, delete `DummyTarget`, Healer via the state | 5, 6 |
| 12 | `DebugPanel`, including the Amount Slider and its prefab wiring | 6, 9 |
| 13 | Contract: delete the old providers; greps clean; closes #46 and credits the reparent guard | 8, 9, 10, 11, 12 |

Amend alongside: #103 (Corpse half by constructor), #63 (criteria reworded, blocked on 9).

### Why the Hero State is not called Session

The glossary's **Session** is the span of play between app start and quit and the unit that persists. The session-flow research already says that word is overloaded (play span, persistence unit, owner of the hero) and that the glossary needs the split before any DTO is named. The replaceable unit is the per-hero piece of that. Ticket 1 names it and records the split.

### Risks

- **Scene rewiring.** Every scene or prefab that references a provider method through a `UnityEvent` breaks. The debug buttons are the known case; ticket 12 owns them.
- **The pointer swap meets a live Run.** Replacing the Hero State mid-Run discards the Run and its Corpse; the entry point should refuse or end the Run first. Ticket 7 names this behaviour.
- **Parallel work in the shared checkout.** The `Utility` submodule and the primary working directory are shared across sessions; this epic runs in a worktree.
