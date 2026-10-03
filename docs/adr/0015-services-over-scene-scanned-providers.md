---
status: accepted
---

# Services over scene-scanned providers; a hero load replaces the Hero and the World

Every game-wide thing is reached through `Provider.Instance`, and every provider is a scene
object found by scanning the scene or created empty on first ask. That one mechanism fuses
authored data, mutable runtime state and scene references, and it is the source of the
quitting flag that leaked across Play sessions, the reset guard that silently stopped
working when a base class was split, and #46. It also makes the core untestable: nothing
that reaches `.Instance` can be built without a scene.

Four decisions replace it (`dev/specs/2026-10-01-services-over-providers-design.md`).

1. **Services over scene-scanned providers.** Authored data is one immutable `GameConfig`
   asset set, loaded before the first scene and identical in every scene. Services are plain
   classes with the functions over that data. State is two plain things: the **Hero**, which
   saves, and the **World**, which never does. One service type maps to exactly one plain
   instance; nothing finds it in the scene, so a duplicate is unrepresentable and no
   scan-and-disable guard exists.
2. **The locator is called only from the Unity edge.** `.Instance` stays, as a static
   locator armed by a boot hook before the first scene and reset in
   `SubsystemRegistration`. It returns a service and never touches the scene. Only views and
   `MonoBehaviour`s call it.
3. **The core takes dependencies by constructor.** Anything in an engine-free assembly
   receives its collaborators as constructor arguments and never calls `.Instance`, so a
   roll, a pickup or a settlement is unit-testable with a fake. New code goes in an
   assembly a test assembly can reach (ADR-0007), not the predefined one.
4. **A hero load replaces the Hero and the World; it does not hydrate them.** Loading builds
   a new Hero and a new World and swaps both. A view never caches either across a swap: it
   binds on enable and rebinds on `HeroLoaded`.

## The split behind decision 1

The spec called the replaceable unit the **Hero State** (a working name) and put the hero's
belongings and the game's transient parts in one bag. That bag is defined by lifecycle, not
by meaning: a Supply is never "the hero's", it is some Town Stop's, and neither it, the Sold
container nor the Run is saved. So the unit splits along what the glossary already says.

- **Hero** owns everything that is its own and saves: stats, level and XP, Equipment,
  Inventory, Stash, Wallet, **Behaviour Profile**, the selected Location and the Corpse.
- **World** holds what a Session carries around the hero and never saves: each Town Stop's
  Supply, the Sold container, the Run with its ground Drops, and the Inventory Context.

Both are replaced together on a hero load, so the no-leak guarantee of decision 4 holds.
The **Session** stays the span of play and holds one Hero and its World at a time. `CONTEXT.md`
carries the definitions, with an *Avoid* list on each.

## Names

None of the new names carries a `Provider` suffix.

| Name | Is | Replaces |
|---|---|---|
| `GameConfig` | the immutable authored asset set, one root asset | the Inspector fields on every provider |
| `ItemService` | config service: loot and coin rolls, currency minting, catalog and icon queries; takes the hero's magic find and item-quantity bonus as roll parameters | `ItemProvider`, the static `ItemView.Catalog` |
| `InventoryService` | state service over the Hero's containers and the World's Supplies: containers by role, acquisition (equip, else bag, debug Stash overflow), Restock | `InventoryProvider` |
| `SimulationService` | state service: the Run, loot flow, selected Location, Corpse, settlement, live Behaviour Profile values | `SimulationProvider` |
| `Hero`, `HeroData` | the domain Hero as a plain class built from its template (an SO for a new hero, a DTO for a loaded one) | `LocalPlayer` and `CharacterProvider` |
| `World` | the never-saved unit, built after the Hero | the Supply, Sold container, Run and context scattered over four providers |
| `Session` | holds the current Hero and World; replaces both on load and raises `HeroLoaded` | nothing: the hero used to be reached through the character provider |
| `GameRunner` | the one hidden persistent `MonoBehaviour` that boot creates, hosting the simulation tick | the added driver component and the smoke-gate spawn |
| `DebugPanel` | one view that calls the services for the debug spawn, kill, heal and amount-slider controls | the `UnityEvent` debug buttons wired to provider methods |
| `CharacterStatPanel` | a view bound to the hero's change events | the stat display pool the hero owned |

The spec and tickets #106 to #119 say **Hero State**, and call the load event
`SessionChanged`. Read them as the Hero and the World, and as `HeroLoaded`: the Session does
not change when a hero loads. `DragProvider`, `PreviewProvider` and `SceneProvider` keep their
names: canvas-nested UI singletons and a shared-submodule class, out of scope.

## Considered options

**One unit, named Hero State.** The spec's choice. It lumps the hero's own things with the
Town Stops' stock and the Run, so "per-hero" is only half true, and "state" is an engineering
word every other glossary term avoids. Rejected for the split above.

**Name the replaceable unit Session.** The glossary's **Session** is the span of play, a
persistence unit and the owner of the hero at once, and the session-flow research already
flagged that the word is overloaded. Reusing it for the replaceable part would make "load a
hero" read as "end the Session". Rejected; the Session survives the swap.

**Hydrate the existing state on load.** Clearing the containers, Wallet, Corpse and Run
field by field means every new field is a place the previous hero can leak into the next.
A swap cannot miss one. Rejected.

**Addressables for `GameConfig`.** Its async load would force an awaited bootstrap before the
first scene. `Resources` loads synchronously from a fixed key, and one root asset cannot
differ per scene. Not adopted for this alone.

## Consequences

- Only the Hero saves, and only its saved parts belong in a future save DTO. A save never
  sees the Supply, the Sold container, the Run or the Inventory Context. The Corpse is the
  Hero's, so it saves; the Run's settlement writes into it.
- The Sold container takes the place the spec gives the Sell Basket (epic #124 replaces the
  staged sale), so the staged basket never enters the World.
- The Stash belongs to the Hero for the MVP, as ADR-0014 decided; moving it to a tier above
  the Session later changes its owner, not this model.
- Swapping during a live Run discards the Run, which lives in the World, and the old Hero
  goes with it. The load entry point must end the Run first or refuse. #114 picks one and
  documents it.
- Nothing writes an authored asset at runtime: with domain reload disabled a runtime write
  to a `ScriptableObject` survives Stop.
- `AbstractSceneSingleton`, `AbstractProvider` and the quitting reset guard stay in the
  shared `Utility` submodule, which AutoBattler also uses. This project stops using
  `AbstractProvider` except through `SceneProvider`.
- #110, #112 and #114 need rewording: the Hero State holder becomes building a Hero and then
  a World. Open for them: how the Hero's stat class and its containers are grouped in code,
  and whether the state services are properties of the current Hero and World or
  locator-resolved facades over them. Either keeps decision 4.
