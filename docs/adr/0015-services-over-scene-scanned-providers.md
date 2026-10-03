---
status: accepted
---

# Services over scene-scanned providers; a hero load replaces the Hero State

Every game-wide thing is reached through `Provider.Instance`, and every provider is a scene
object found by scanning the scene or created empty on first ask. That one mechanism fuses
authored data, mutable runtime state and scene references, and it is the source of the
quitting flag that leaked across Play sessions, the reset guard that silently stopped
working when a base class was split, and #46. It also makes the core untestable: nothing
that reaches `.Instance` can be built without a scene.

Four decisions replace it (`dev/specs/2026-10-01-services-over-providers-design.md`).

1. **Services over scene-scanned providers.** Authored data is one immutable `GameConfig`
   asset set, loaded before the first scene and identical in every scene. Services are plain
   classes with the functions over that data. State is one plain, serializable **Hero
   State**. One service type maps to exactly one plain instance; nothing finds it in the
   scene, so a duplicate is unrepresentable and no scan-and-disable guard exists.
2. **The locator is called only from the Unity edge.** `.Instance` stays, as a static
   locator armed by a boot hook before the first scene and reset in
   `SubsystemRegistration`. It returns a service and never touches the scene. Only views and
   `MonoBehaviour`s call it.
3. **The core takes dependencies by constructor.** Anything in an engine-free assembly
   receives its collaborators as constructor arguments and never calls `.Instance`, so a
   roll, a pickup or a settlement is unit-testable with a fake. New code goes in an
   assembly a test assembly can reach (ADR-0007), not the predefined one.
4. **A hero load replaces the Hero State; it does not hydrate it.** Loading builds a new
   Hero State and swaps the pointer. A view never caches a Hero State across a swap: it
   binds on enable and rebinds on `HeroStateChanged`.

## Names

The Hero State keeps its working name; the glossary entry (`CONTEXT.md`) holds its *Avoid*
list. **Session** stays the span of play and must not name the replaceable unit: the
Session holds one Hero State at a time. None of the new names carries a `Provider` suffix.

| Name | Is | Replaces |
|---|---|---|
| `GameConfig` | the immutable authored asset set, one root asset | the Inspector fields on every provider |
| `ItemService` | config service: loot and coin rolls, currency minting, catalog and icon queries; takes the hero's magic find and item-quantity bonus as roll parameters | `ItemProvider`, the static `ItemView.Catalog` |
| `InventoryService` | state service owned by the Hero State: the containers by role, acquisition (equip, else bag, debug Stash overflow), Restock | `InventoryProvider` |
| `SimulationService` | state service owned by the Hero State: the Run, loot flow, selected Location, Corpse, settlement, live behaviour values | `SimulationProvider` |
| `HeroState` | the per-hero unit, built hero-first | the hero, containers, Wallet and Run scattered over four providers |
| `Hero`, `HeroData` | the hero as a plain class, and its template (an SO for a new hero, a DTO for a loaded one) | `LocalPlayer` and `CharacterProvider` |
| `Session` | holds the current Hero State; replaces it on load and raises `HeroStateChanged` | nothing: the hero used to be reached through the character provider |
| `GameRunner` | the one hidden persistent `MonoBehaviour` that boot creates, hosting the simulation tick | the added driver component and the smoke-gate spawn |
| `DebugPanel` | one view that calls the services for the debug spawn, kill, heal and amount-slider controls | the `UnityEvent` debug buttons wired to provider methods |
| `CharacterStatPanel` | a view bound to the hero's change events | the stat display pool the hero owned |

The spec and tickets #108 to #119 name the event `SessionChanged`; it is
`HeroStateChanged`, because the Session does not change when a hero loads. `DragProvider`,
`PreviewProvider` and `SceneProvider` keep their names: canvas-nested UI singletons and a
shared-submodule class, out of scope.

## Considered options

**Name the unit Session.** The spec's user stories say "the Session is replaced", but the
glossary's **Session** is the span of play, a persistence unit and the owner of the hero at
once, and the session-flow research already flagged that the word is overloaded. Reusing it
for the replaceable part would make "load a hero" read as "end the Session". Rejected; the
Session survives the swap.

**Hydrate the existing state on load.** Clearing the containers, Wallet, Corpse and Run
field by field means every new field is a place the previous hero can leak into the next.
A pointer swap cannot miss one. Rejected.

**Addressables for `GameConfig`.** Its async load would force an awaited bootstrap before the
first scene. `Resources` loads synchronously from a fixed key, and one root asset cannot
differ per scene. Not adopted for this alone.

## Consequences

- The Hero State holds the Hero, Equipment, Inventory, Stash, Wallet, Inventory Context,
  behaviour sliders and selected Location; and the parts that are never saved: each Town
  Stop's Supply, the **Sold container**, the Run with its ground Drops, and the Corpse. Only
  the Corpse is ever saved. The Sold container takes the place the spec's contents list
  gives the Sell Basket (epic #124 replaces the staged sale), so the staged basket never
  enters the Hero State.
- The Stash lives in the Hero State for the MVP, as ADR-0014 decided; moving it to a tier
  above the Session later changes its owner, not this model.
- Only the Hero State's saved parts belong in a future save DTO. A save never sees the
  Supply, the Sold container or the Run.
- Swapping the Hero State during a live Run discards the Run and its Corpse, so the load
  entry point must end the Run first or refuse. #114 picks one and documents it.
- Nothing writes an authored asset at runtime: with domain reload disabled a runtime write
  to a `ScriptableObject` survives Stop.
- `AbstractSceneSingleton`, `AbstractProvider` and the quitting reset guard stay in the
  shared `Utility` submodule, which AutoBattler also uses. This project stops using
  `AbstractProvider` except through `SceneProvider`.
- Open for #112 and #114: whether the state services are reached as properties of the
  Session's current Hero State or as locator-resolved facades over it. Either keeps the
  rule in decision 4.
