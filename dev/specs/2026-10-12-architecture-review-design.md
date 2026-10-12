# Architecture Review: Five Deepenings

Date: 2026-10-12
Status: Scoping spec - not a plan. Slice into GitHub issues with `/to-tickets`, build with
`/implement`, one issue at a time.
Base: `origin/main` at `5f494ab`.
Derived with: an architecture review of the changes since `e4c38a1` (spatial combat, then the
floor-loot epic), followed by a design interview in which the owner made every decision below.
Amends: GLOSSARY.md (**Brain**, **Intent**). Adds: ADR-0020.
Relates to: ADR-0010 (Strike and Cast are the two verbs), ADR-0018 (the simulation owns position,
the view projects it), `2026-10-08-floor-loot-design.md`, `2026-10-08-spatial-combat-design.md`.

## Problem Statement

Four places in the code say one thing more than once, and one place does two jobs.

- A tunable is declared four times: a `GameConfig` field and range, a default again, a silent clamp
  in `SimulationService.TuningFor`, and a `Validate()` that throws on what the clamp hid.
- A damage number asks the live Encounter for the Location, the hero and the damage spread to work
  out how big a hit is, and each rising number re-checks that the Encounter is still the Run's.
- `EnemyView` and `HeroFigure` carry the same placement code, and the arena branches on "is it the
  hero?" in placement, depth sort and number origin. The hero shows no health bar and no hit
  feedback, though it is the one fighter the player watches.
- Every acquisition path restates which of the hero's containers a pick-up touches
  (`hero, hero.Inventory, hero.Equipment`), and `LootFlow` has its own hand-written copy of the
  pick-up transaction. Add a container to the hero and each copy needs finding.
- `EncounterSimulation` (765 lines) decides what a fighter does and carries it out in the same
  step. The roadmap changes who decides - a player at the controls, AI for enemies - without
  changing the rules, and today there is no seam to change it at.

## Solution

Five changes, each small except D, listed below. None changes what the player sees on
the first day, except that the hero's figure gains a health bar and hit feedback.

| | Change | Blocked by |
|---|---|---|
| A | The tuning owns its defaults and sanitising | - |
| B | A hit carries the scale and place its number is drawn at | - |
| C | One figure for hero and enemy | B |
| D | Brains decide, the Encounter carries out Intents (ADR-0020) | A |
| E | The receiver owns its pick-up transaction | - |

A, B and E can run in parallel. The ground-fade setting that the review first proposed as its own
change is part of A (see below); there is no separate ticket for it.

## Implementation Decisions

### A. The tuning owns its defaults and sanitising

- `EncounterTuning`, `ArenaTuning` and `CastDefinition` carry their own defaults and their own
  sanitising. `SimulationService.TuningFor` is deleted. `GameConfig` holds the tuning objects
  instead of mirroring each field.
- The Simulation assembly is engine-free, so the sanitising cannot call `Mathf` and the types
  cannot use `[SerializeField]`. They are plain `[Serializable]` classes with public fields.
- The three roll streams (`rolls`, `movementRolls`, `hitRolls`) become one constructor parameter
  of `EncounterSimulation`, so a new stream is no longer a parameter in two modules. The rule that
  they stay separate so a seeded outcome never reorders is stated once, on that parameter. Every
  seeded outcome is unchanged.
- The ground's fade (`GroundFadeSettings`, serialized once on the grid and once on the list, with
  a tooltip asking someone to keep them equal) becomes one setting on `GameConfig` that both views
  read. The ground stays reached through `LootFlow`: with no Run there is no ground
  (GLOSSARY.md **Ground**), so the ground's open/closed state riding on `LootFlow`'s lifetime is
  by design and not a defect.

### B. A hit carries the scale and place its number is drawn at

- `HitEvent` gains the scale its number is judged against - the best raw hit the dealer could do
  with that damage type, which `DamageReference` computes today from the live Encounter - and the
  `Coordinate` the target stood at when the hit landed.
- `EncounterSimulation` sheds `DamageSpread`, which it exposed only so a view could compute that
  reference. The rule for "what a big hit is" now sits beside the damage rule.
- `ArenaDamageNumbers` and `DamageNumber` stop reading `SimulationService.Instance.Run.Encounter`.
  Pending numbers are dropped when the arena unbinds, so a number never has to ask whether its
  Encounter is still live.
- The numbers rise from the hit's own `Coordinate`, projected by the arena. This is what lets C
  delete `IDamageNumberOrigins` (see C), and it needs no fallback for a fallen enemy: the killing
  blow carries its own place.

### C. One figure for hero and enemy

- `EnemyView` and `HeroFigure` become one figure component holding placement, facing, visibility,
  the arena position, and the slots for the health bar, hit flash and hit shake. `HeroFigure` is
  deleted. The hero gains a health bar, a hit flash and a hit shake, exactly as an enemy has; the
  flash already fires only on a health drop, so regeneration does not trigger it.
- The pool, the death fade and the target ring stay enemy-only, owned by the arena. The hero's
  figure stays a scene object (one, always there), bound when an Encounter binds and hidden when
  it is released. It is not pooled and has no entry in `EnemyVisuals`.
- `EnemyHealthBarDisplay`, `EnemyHitFlash` and `EnemyHitShake` drop the "Enemy" prefix and bind to
  a bare `CharacterResource` the arena passes in, plus the sprite for the flash. `ICombatant` is
  untouched, so D can shape it freely. The scripts are renamed together with their `.meta` files so
  every prefab and scene reference survives.
- `IDamageNumberOrigins` shrinks to a projection from a `Coordinate` to the arena's anchored space,
  and the `target is Enemy` / hero branching goes. The arena's placement, depth sort and number
  origin no longer treat the hero as a special case.
- This needs a prefab edit and a hero scene-object edit. It is the one change here that touches
  authored assets.

### D. Brains decide, the Encounter carries out Intents

Recorded in full in ADR-0020; the decisions that matter to a ticket:

- A **Brain** (GLOSSARY.md) decides once a tick and returns **Intents**: walk toward a point, Strike
  a target, Cast at a point, or (hero only) request a Recall. Intents are commands, not goals.
- The Encounter carries Intents out. It owns reach, both cadences with the "ready but banks no
  burst" clamp, Resource cost, `Land`, kills and XP, and refuses an Intent at no cost. It tells a
  Brain when a Strike is ready and when a Cast beat is open.
- Every Brain decides from the start-of-tick state, then the Intents run in today's order: hero
  walks, enemies walk, hero Strikes, hero Casts, enemies Strike. Recall is handled after the
  hero-down check, so a tick that kills the hero and triggers Recall stays a Death.
- A new **Arena** module is the spatial module only: positions, the movement step, reach and shape
  queries, spawn bearings. It never decides and never deals damage. The simulation still owns
  position (ADR-0018).
- A Brain sees a narrow read-only view and gets no random rolls. Spawn placement keeps rolling
  from the movement stream.
- The first two adapters are today's behaviour: an auto-hero Brain (Behaviour Profile, sticky
  target, `CastThreshold` latch, stepped on every Cast beat) and a chase-and-Strike enemy Brain,
  built by default.
- The target ring reads the target of the latest Strike or approach Intent. The pure `StrikeTarget`
  peek is deleted.
- Not decided here: the exact class and interface names, the Brain view's members, and the
  Intent types' shapes. The ticket settles them against the code.

### E. The receiver owns its pick-up transaction

- The receiver declares the containers its `PickUpItem` may write to.
  `PickUpTransaction.Run(source, cell, receiver, onAcquired)` enrols them itself; its `inventory`
  and `equipment` parameters go. `IItemReceiver` lives in the Items assembly, which cannot name a
  container type, so the ticket chooses between moving the interface to Containers and adding a
  sibling interface there.
- `AbstractSlotDisplay`'s Stash retrieval and `VendorTransaction.Buy` stop naming
  `hero.Inventory` and `hero.Equipment`. `Buy` loses its `equipment` parameter.
- `LootFlow.PickUpFromGround` calls `Run(Ground, cell, _player)` and its `params
  receivingContainers` goes. It keeps three things that are the ground's own: the `try/catch` that
  surfaces a throw through `PlacementFailed`, the coin branch (a coin stack banks to the Wallet and
  leaves what the Wallet cannot take), and the auto-pickup path for fresh drops, which has no
  source cell. `Run` already rolls back when the receiver throws, since the transaction is
  disposed.
- `Buy` requires a receiver. Its `player ?? new BagOnlyReceiver(bag)` fallback - a test stand-in
  living in production code - moves into the tests, and the five tests that omit `player`
  (`VendorTransactionTests` four, `SaleTests` one) pass one. The tests already build a receiver
  over `ItemAcquisition`.
- Corpse recovery (`ContainerSettlementBag.TryStore`) places one item with no source container, so
  it stays a direct `PickUpItem`.

## Testing Decisions

- A good test asserts external behaviour, not how it is stored.
- **A:** build a tuning directly and assert the sanitised values and the defaults; no `GameConfig`
  asset. Every existing seeded simulation test passes unchanged.
- **B:** assert on `HitEvent` - the scale for a hit on the hero and a hit on an enemy, and the
  target's `Coordinate` - through `Advance`. Prior art: the hit tests that exist for `HitEvent`.
- **C:** the pure parts (placement and facing maths) are already covered by `ArenaLayout` and
  `ArenaProjection` tests. The figure, its prefab and the hero scene object are checked by hand in
  Play: the hero's bar, flash and shake, enemies as before, the numbers rising from the right
  places, a killing blow's number, and a Run ending.
- **D:** the existing simulation tests keep running through `Advance` with the default Brains and
  are the regression bar. New tests add scripted Brains for the Encounter (an Intent out of reach,
  on cooldown or without Resource costs nothing; decide-then-execute ordering; Recall after the
  hero-down check) and direct queries for the Arena.
- **E:** `PickUpTransactionTests` assert the rollback once, through a receiver that declares its
  containers (including a receiver with a third container, to prove it is enrolled). `LootFlowTests`
  for `PickUpFromGround` pass unchanged.

## Out of Scope

- A player-controlled hero Brain and AI enemy Brains; D builds the seam and the first adapters
  only. A player Brain deciding at the 0.1 s tick needs its own spec (ADR-0020).
- Auto-pickup redesign, a ground module extracted from `LootFlow`, and any change to the ground's
  rules (`2026-10-08-floor-loot-design.md` stays the authority).
- Pooling the hero's figure or giving it an `EnemyVisuals` entry.
- Moving the Corpse's single-item placement into the transaction.

## Further Notes

**Order:** A, B and E in parallel; then C (after B); then D (after A). Run `python dev/frontier.py`
before each `/implement #N`.

**ADR numbering:** ADR-0020 was numbered against `origin/main`, whose last ADR is 0019. Renumber
on merge if another ADR lands first.

**The one behaviour change a player sees:** the hero's figure gains a health bar and a hit
flash and shake. Everything else is a refactor with the existing tests as the bar.

**Candidates considered and dropped:** a separate ground-module ticket (the review first read the
ground's open/closed state riding on `LootFlow`'s lifetime as a defect; the glossary and the
floor-loot spec say it is by design), and a hero-only Brain seam (the executor would be
half-written).
