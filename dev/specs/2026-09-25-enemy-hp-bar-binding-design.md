# Enemy HP Bar Binding

Date: 2026-09-25
Status: Sliced — #93 (the type move; prefactor, grabbable now) and #94 (the binding, blocked by
#93 and #60). Build with `/implement`.
Base: `main` at `4e966ee`.
Depends on: `issue-60/enemy-hp-bar-pool` (PR #92) — supplies `EnemyHealthBarPool`,
`EnemyHealthBarDisplay` and the Combat Panel scene wiring this spec binds.

## Problem Statement

#60 shipped an inert shell. `EnemyHealthBarPool.SpawnBar` and `RemoveBar` have no caller, so
the pool container renders empty at runtime — correct for that ticket, which scoped event
binding out by name, and indistinguishable from a bug for anyone who opens the scene.

Binding it needs a data seam to the live `EncounterSimulation`, and none exists. The sim
raises exactly four events — `EnemyDefeated`, `EncounterCleared`, `HeroDowned`,
`RecallRequested` — so a view can learn an enemy *died* but never that one *arrived*. Health
is not observable at all: it is a private `float _health` on `Enemy`, mutated by
`ReceivePhysical` / `ReceiveMagical` inside the tick, with no change signal of any kind.

Underneath that, the enemy's health is a hand-rolled and poorer second copy of a concept the
project already has. `CharacterResource` models current-out-of-a-total with change,
depletion and recharge events, and it is what the hero's globe (`ResourceDisplay`) renders.
The target is that an enemy bar renders *the same* way — same data shape, eventually the same
widget — not a lookalike that drifts.

One structural obstacle stands in the way, and it is a compile fact rather than a design
position. `Enemy.cs` sits in `InventorySystem.Simulation.asmdef`, which references only
`InventorySystem.Data`, `InventorySystem.Items` and `InventorySystem.Containers`.
`CharacterStat.cs` and `CharacterResource.cs` sit under `Runtime/Character/` with no asmdef,
so they compile into `Assembly-CSharp` — and an asmdef cannot reference `Assembly-CSharp`.
The two types are also misfiled relative to themselves, and to each other: `CharacterStat`
declares the namespace `ToolSmiths.InventorySystem.Data`, while its own subclass
`CharacterResource` declares `ToolSmiths.InventorySystem.Runtime.Character`. One base/derived
pair, two answers to where it lives — and the assembly name agrees with only one of them.

## Solution

The enemy's health becomes a real `CharacterResource` — the same type the hero's globe
renders — and the two files move to the assembly that lets `Enemy` hold one.

### 1. Enemy health is a `CharacterResource`

`Enemy` keeps no float. It holds a `CharacterResource` built over its archetype's
`MaxHealth`, `Health` / `MaxHealth` / `HealthFraction` / `IsDown` delegate to it, and
`ReceivePhysical` / `ReceiveMagical` compute mitigation exactly as they do today, then call
`RemoveFromCurrent`. One source of truth, not a mirrored float that can drift.

The resource carries its **full** surface — `CurrentHasChanged(previous, current, total)`,
`CurrentHasDepleted`, `CurrentHasRecharged` — not a trimmed subset. The bar consumes
`CurrentHasChanged` today; the other two exist so a later consumer (a death flourish, a
heal-up flash, a regen tick) does not have to reopen this type. The enemy is meant to stay as
close to the hero's model as possible, and the hero's model has all three.

### 2. `CharacterStat` and `CharacterResource` move into `Data/`

`git mv` both files from `Assets/Scripts/InventorySystem/Runtime/Character/` into
`Assets/Scripts/InventorySystem/Data/`, preserving each `.meta` and therefore its GUID.
`InventorySystem.Data.asmdef` already references `Utility` and `NaughtyAttributes.Core` and
allows engine references, so both files compile there unchanged, and
`InventorySystem.Simulation` already references `Data`.

The asmdef graph does not change. Nothing is dissolved, merged or re-scoped: the sim module
keeps its boundary and simply reaches the type the way it reaches `MutableFloat` today.

`CharacterResource`'s namespace is aligned to `CharacterStat`'s in the same move, so the pair
has one name for one place. That is churn inside a change which is otherwise behaviour-neutral,
and it is taken deliberately: leaving it would put a type named `...Runtime.Character` into the
`Data` assembly — the namespace-versus-assembly mismatch `docs/agents/codebase-notes.md` warns
about, now made worse by splitting a base from its subclass. The blast radius is the consumers
in `Assembly-CSharp` (`BaseCharacter`, `LocalPlayer`, `ResourceRegen`, `HeroCombatant`,
`ResourceDisplay`, `BaseCharacterExtensions`), all compiler-caught. It is serialization-safe
because nothing in the project uses `[SerializeReference]`: the only coupling to YAML is the
`BaseCharacter` field name, which does not change.

### 3. `EncounterSimulation` gains `EnemySpawned`

`public event Action<Enemy> EnemySpawned`, raised in `Spawn()` alongside `_enemies.Add` —
the symmetric pair to the existing `EnemyDefeated`, which is raised *after* the enemy leaves
`_enemies`. Raised from `Spawn()` rather than from the schedule, so the initial batch
`BeginEncounter` spawns is covered by the same path and needs no special case.

`EnemyDefeated` already carries everything removal needs and does not change.

### 4. The pool binds by reference; values are pushed

`EnemyHealthBarPool` gains a small `Update()` whose only job is binding: resolve
`SimulationProvider.Instance.Run.Encounter`, rebind when that reference changes, release every
bar when it becomes null. Everything else is event-driven — `EnemySpawned` activates a bar
and subscribes it to that enemy's `CurrentHasChanged`; `EnemyDefeated` unsubscribes and
releases.

Only the *binding* is polled, and it is one reference comparison per frame. This is
deliberate: the sim is rebuilt on every `Send`, and `SimulationProvider` self-spawns via
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, so at the pool's own `OnEnable` the
provider may not exist yet. It also sidesteps the disabled-domain-reload trap
(`docs/agents/codebase-notes.md`): a component that subscribes in `Awake` and unsubscribes in
`OnDisable` is silently dead from the second Play entry onward. With binding done in one
place in `Update()`, there is no subscribe/unsubscribe asymmetry to get wrong.

### 5. The stranding case

A clear is safe by construction — `ClearEncounter` requires `_enemies.Count == 0`, so every
bar was already released by `EnemyDefeated`. A hero death or an auto-Recall is not: enemies
are still alive when the sim stops, and no `EnemyDefeated` fires for them. The pool releases
every bar when its bound sim goes null, which covers both paths without depending on
`RunEnded`.

### 6. Display, label and border

- **Label is `Archetype.ToString()`** — `"Brute"` / `"Skirmisher"`. `Enemy` exposes no name,
  and its source level comes from the `EncounterProfile`, identical across every bar in an
  encounter, so a level in the label would be constant noise.
- **The rarity border stays unbound.** `EnemyHealthBarDisplay.SetRarityColor(ItemRarity)`
  remains in the prefab and remains uncalled: enemies carry an `EnemyArchetype`, drops carry
  an `ItemRarity`. Binding it to a rarity the enemy does not have would be inventing one.
- **`EnemyHealthBarDisplay` keeps its own fields for now.** It subscribes to
  `CurrentHasChanged` and writes the same three values `ResourceDisplay` writes (fill,
  numeric, and the label it alone has), so the *data* already matches. Swapping the widget for
  a shared `ResourceDisplay` is a later component change, not a data change, and doing it here
  would put the player's HUD at risk in a ticket about enemies.

## User Stories

1. As a player, I want to see a bar per living enemy, so that I can tell how the fight is
   going beyond the aggregate "alive N" count.
2. As a player, I want a bar to appear the moment an enemy arrives and vanish the moment it
   falls, so that the list reflects the fight without me looking away.
3. As a player, I want a bar's fill and numbers to track damage as it lands, so that I can
   see which enemy is nearly down.
4. As a player, I want the newest enemy at the top, so that a fresh arrival is not buried
   below a crowd.
5. As a player, I want the list to scroll once it is taller than the panel, so that a raised
   Engagement target does not push the sliders off the panel.
6. As a player, I want the list to clear when the Run ends, so that dead enemies from a
   finished fight are not left on screen.

## Out of Scope

- Unifying `EnemyHealthBarDisplay` with `ResourceDisplay` (see §6).
- Rarity or elite tiers for enemies; the border binding that would follow.
- Cooldown overlays on bars.
- Enemy regeneration — `Enemy.Regenerate` stays the documented no-op it is today.
- Any change to the hero's stat system beyond relocating two of its files.

## Risks

- **The `Data/` move and serialized state.** `CharacterStat` / `CharacterResource` are
  `[Serializable]` and `[SerializeField]`-backed, held by `BaseCharacter`. `git mv` keeps the
  GUID so Unity should re-resolve them, but this must be checked by loading the scene and
  confirming hero stats and the resource globes are intact — a silent re-serialization to
  default values is exactly the failure this can produce.
- **Event volume.** `Advance` banks real time and runs N ticks in one frame (bounded by
  `MaxTicksPerAdvance`, ×8 sim speed max), so a per-tick `CurrentHasChanged` fires N times per
  frame where a poll read once. Bounded and small, but it is the cost this design accepts in
  exchange for no per-frame diffing of the enemy list.
- **The constructor raises before anyone listens.** `CharacterResource`'s constructor calls
  `RefillCurrent()`, which fires `CurrentHasChanged` with no subscriber attached. `Spawn()`
  must raise `EnemySpawned` *after* the enemy is fully constructed so the bar's first refresh
  comes from the spawn handler, not from an event it was not yet there to hear.
- **Init order.** The pool's binding must tolerate `SimulationProvider.Instance` being null on
  early frames, and must not assume `Awake` runs before or after the provider's.

## Verification

Per `docs/agents/codebase-notes.md`: `dotnet build` lies here. Compile and test verification
goes through the `unity-mcp` bridge or `-runTests -batchmode`, with a negative control before
believing a green. The `Data/` file move additionally needs a by-hand Play-mode pass — load
the scene, send a Run, and confirm bars appear, fill, and clear on Recall and on hero death.
