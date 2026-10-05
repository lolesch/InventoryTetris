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
| `GameLoop` | one player-loop system that boot installs, hosting the simulation tick; no `MonoBehaviour`, no GameObject | the added driver component and the smoke-gate spawn |
| `IService`, `ServiceRegistry`, `ServiceLocator` | the generic registry (one instance per registered type) and the static holder of the one the game booted with; in the `Utility` submodule | the per-class `.Instance` statics and their scan-and-create |
| `DebugPanel` | one view that calls the services for the debug spawn, kill, heal and amount-slider controls | the `UnityEvent` debug buttons wired to provider methods |
| `CharacterStatPanel` | a view bound to the hero's change events | the stat display pool the hero owned |

The spec and tickets #106 to #119 say **Hero State**, and call the load event
`SessionChanged`. Read them as the Hero and the World, and as `HeroLoaded`: the Session does
not change when a hero loads. They also say `GameRunner` and "runner": read those as
`GameLoop` (see Amendment 2026-10-04). `DragProvider`, `PreviewProvider` and `SceneProvider` keep their
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
  `AbstractProvider` except through `SceneProvider`. The submodule only gains the generic
  registry and loop hook of the 2026-10-04 amendment, additively.
- #110, #112 and #114 need rewording: the Hero State holder becomes building a Hero and then
  a World. Open for them: how the Hero's stat class and its containers are grouped in code,
  and whether the state services are properties of the current Hero and World or
  locator-resolved facades over them. Either keeps decision 4.

## Amendment 2026-10-04 (#108): a type-keyed registry in Utility, and no runner object

Two decisions made while building the boot, both replacing a line of the original.

**The locator is a registry of plain `IService` instances, and it lives in `Utility`.** The
original left the locator's shape open and put the `Utility` submodule out of scope. A
type-keyed registry has no inventory vocabulary, and it enforces decision 1 directly: a second
registration of one type throws, so a duplicate is unrepresentable. `IService` is a marker with
no members, because config services and state services share nothing but the registry, and a
base class would be empty. `ServiceLocator` holds the one registry the game booted with, is
cleared in `SubsystemRegistration`, and throws when read unarmed. The change is additive and
AutoBattler is unaffected. `GameConfig`, the boot and the frame tick stay in this project
(`InventorySystem.Services`). Each service keeps a one-line `Instance` that delegates to the
locator, so call sites read as before.

*Rejected: a lazy `Instance` per service class.* It needs its own reset hook in every class (the
forgotten-quitting-flag bug, repeated), makes construction order depend on which `Instance`
fires first (the spec's story 10), surfaces a missing config at first use instead of at boot, and
needs a private constructor or a setter per class to substitute a fake. *Rejected: one composite
with a typed property per service.* It is checked at compile time, but every new service edits
the composite, and #108 would have shipped it with nothing in it.

**The frame tick is a player-loop system, not a hidden `MonoBehaviour`.** The only job of the
runner was to give the simulation `Time.deltaTime` each frame, and scene-loading coroutines stay
with `SceneProvider`, so nothing needs a GameObject. `PlayerLoopHook` (in `Utility`) installs a
delegate once per marker type at the end of the `Update` phase, so it runs after every
`MonoBehaviour.Update` as the old `DefaultExecutionOrder(10)` driver did, and removes it on
leaving Play Mode, because the player loop is global and survives Stop with domain reload
disabled. `GameLoop` installs it and holds the tickers; the simulation adds its ticker in #113.
There is nothing in the hierarchy to hide, persist, destroy or leak, and the reset is symmetric
with the locator's. The cost is no `OnDestroy` lifecycle and no coroutine host; neither is
needed.

## Amendment 2026-10-05 (#112): how the Hero and the World are grouped and reached

Settles the two questions the Consequences section left open for #112.

**The Hero is one class: stats, then what it owns.** `Hero` keeps its stats and resources and gains
`Equipment`, `Inventory`, `Stash`, `Wallet` and `Behaviour` (the Behaviour Profile), handed to it once
by `Outfit` after construction, because the Equipment takes the hero as its stat receiver and so
cannot exist first. A hero that was never outfitted throws
when asked for a container (the legacy `DummyTarget` was the one such hero; #117 deleted it). `Hero` also implements `IItemReceiver`: the placement (auto-equip, else
the Inventory, nothing behind that) is the hero's. `World` holds the two Supplies, the Sold
container and the Inventory Context. Both are plain classes.

**State services are locator-resolved facades over the Session, not properties of the Hero.**
`ISession` holds the current Hero and World; `IInventoryService` operates on whichever it holds
*at the time of the call* (containers by role, Quick Move, acquisition with the debug Stash
overflow, Restock). A hero load (#114) swaps the Session's pair and every reader of the service
follows without being told, which is decision 4 for free. Properties of the Hero would have meant
every caller holding a Hero, which a swap invalidates.

**One builder writes the order.** `SessionBuilder.Build(config, heroData, items)` is the one place
that says hero, then what it owns, then the World; it touches no locator and no scene. `GameBoot`
calls it with `GameConfig.DefaultHero`, registers the Session and the inventory service, and
stocks both Supplies, so a bare scene has a Hero and a World at boot. `InventoryProvider` and
`CharacterProvider` forward to these on every call; `LocalPlayer` wraps the booted Hero and holds no
template of its own. The Behaviour Profile's defaults moved from `SimulationProvider`'s Inspector
fields to `GameConfig`, so the Hero is built with them and the provider's `Behaviour` is the Hero's.

