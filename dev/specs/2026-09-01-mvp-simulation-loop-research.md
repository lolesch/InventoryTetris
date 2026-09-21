# MVP simulation loop — design-options research

Date: 2026-09-01
Status: **Research input for a future spec. Not a spec, not a decision.** Survey of
what is already decided internally, the developer's own prior art in the sibling
projects, and external genre prior art, with concrete options and trade-offs for each
part of an idle/auto-combat loop built on the systems alive in this prototype.

## Conclusion first — the thinnest playable slice

Send the hero to a location on a small map; a fixed-timestep `Encounter` runs
wave-by-wave against scaled `DummyTarget`-style enemies while the inventory stays
open; loot, XP and currency accumulate into an in-memory `EncounterResult`; the
player clicks **Recall** (keep the haul) or the hero dies (lose most of it) and lands
back in town, where the Shop and Stash toggles re-enable and the haul is Tetris-sorted
into the bag. Two field locations plus town is enough.

- **Reused unchanged:** `BaseCharacter.DealDamageTo` / `ReceiveDamageFrom`, the
  `CharacterResource` regen semantics, `BaseCharacterExtensions.Calculate*`,
  `LocalPlayer.GainExperience` / `PickUpItem` / `AddItemStats`, `ItemProvider`'s loot
  roll, all four containers + drag/drop/`Sort`/`Consolidate`, and the
  `AbstractPanel` / `PanelToggle` / `RadioGroup` / `Slider` UI primitives.
- **Ported once:** `CombatClock` from AutoBattler
  (`Assets/Code/Runtime/Core/Combat/CombatClock.cs` + its test file) — this *is* the
  answer to `// TODO: COMBAT TICK RATE` (`BaseCharacter.cs:53`).
- **Stubbed:** one parametric enemy archetype scaled by location level; no skills
  beyond the existing physical/magical split; instant teleport (no travel time);
  single session (nothing persists across quit); one shared loot table biased by
  source level.
- **Genuinely new:** `CombatClock` carve-out, an `Encounter` wave driver, a `RunState`
  FSM (`InTown` ↔ `InField`), an `EncounterResult` value type, a `LocationConfig`
  ScriptableObject, 3–5 behaviour sliders feeding a `HeroBehaviour` config, a map
  panel (`RadioGroup` + Send/Recall), a "Haul" container role, and a `MonoBehaviour`
  sim driver.

This sits **on top of** today's code and **beside** the foundational rework, not
downstream of it — with one honest caveat: the `Encounter` module wants
`Items`/`RollContext` from rework Phase 1, and ADR-0006 sequences the combat sim after
all three seams. See [ADR pressure](#adr-pressure-and-contradictions).

---

## 1. Scope and method

The owner wants an **idle/auto-combat ARPG loop**: the hero auto-fights per
player-tuned behaviour; the player's only inputs are behaviour sliders, map clicks
(send out / recall), and inventory/shop/stash management. Town is the management hub;
the field earns loot and XP.

Sources, in authority order:

1. **Internal specs + ADRs + code** (this repo) — authoritative for this project.
2. **Sibling Unity projects by the same developer** on this machine — how this
   developer already structures these problems. Cited by absolute path.
3. **External primary / high-trust** — Game Programming Patterns, Unity docs,
   developer talks/postmortems/official wikis for genre prior art.

Every non-obvious claim is traced to its source: `path:line` for code, doc-name +
section for internal specs, inline links for the web. Where a source cannot answer, it
says so.

### What the rework spec already commits to (build on this, do not contradict)

`dev/specs/2026-08-31-foundational-rework-design.md`, **Out of Scope → "Tier 2 — the
combat simulation cluster (the stated direction)"** (lines 417–430) already sketches:

- an **`Encounter` module** — `StartEncounter(config) → EncounterResult` hiding "a
  fixed-timestep combat tick (replaces the `async void` regen and
  `// TODO: COMBAT TICK RATE`), enemy waves, hero auto-attack with attack-speed
  timing, death/flee outcomes", producing loot "via `ItemGenerator.RollLoot` with a
  real `RollContext`";
- a **`RunState` machine** — `InTown ↔ InRun(encounter…) → ReturnToTown(loot) | Death`
  — "the module that makes inventory management *matter*. Its `EncounterResult` shape
  and what it must persist both constrain Seam 3 — sketch its interface before
  building the save system";
- a **ground loot pile** — "encounter output lands at a location; player picks up.
  Rides on Seam 2. Replaces `DummyTarget.OnDeath`'s auto-vacuum."

`RollContext` is already specified: `{ int SourceLevel; float MagicFind; LootTable
Table; }` (foundational-rework §"The generator", line ~244).

**Where the spec is silent:** the number and nature of locations; whether the sim
runs live or resolves on arrival; what the behaviour sliders are; what Recall vs Death
differ by concretely; whether a run survives an app quit; the map UI.

### What is alive in the prototype today

| System | State | Reference |
| --- | --- | --- |
| Combat | "A debug harness, not a system." `DealDamageTo` / `ReceiveDamageFrom` fire once per button press; regen is `async void` + `Task.Delay` in `Update()`; `// TODO: COMBAT TICK RATE` | foundational-rework §"Where the code is"; `BaseCharacter.cs:51-74,108,136` |
| Damage formulas | `CalculateDamageOutput` = `damageType stat × (1 + AttackSpeed·0.01)`; "implementation is just for testing" | `BaseCharacterExtensions.cs:17,30-43` |
| Enemy | `DummyTarget` respawns (refills own Health/Shield in `OnDeath`), rolls loot, vacuums it into the bag via `PickUpItem`, grants XP; `//rework to drop items on the floor` | `DummyTarget.cs:14-29` |
| XP / level | `LocalPlayer.GainExperience(exp, monsterLevel)` — level-difference balance, level-up adds a growth `StatModifier` and re-deepletes the Experience resource; `//TODO: design exp gain` | `LocalPlayer.cs:65-89` |
| Loot roll | `ItemProvider.GenerateRandomLoot(amount)` → category → equipment/consumable/currency; `IncreasedItemQuantity` adds bonus drops; magic find via `IncreasedItemRarity` | `ItemProvider.cs:59-91,290` |
| Debug buttons | `CharacterProvider.PlayerDealsPhysicalDamageToDummy()` etc.; `ToggleSpendingResource()` flips `Player.SpendResource` | `CharacterProvider.cs:15-26` |
| Resource gating | `BaseCharacter.SpendResource` (bool, public settable); `CalculateRequiredResource` returns 0 when off, else `Resource × {Physical .03, Magical .1}` | `BaseCharacter.cs:21`; `BaseCharacterExtensions.cs:11-28` |
| Stats | `StatName` enum: `AttackSpeed`, `PhysicalDamage`, `MagicalDamage`, `Armor`, `MagicResist`, `Health`, `HealthRegeneration`, `Shield`, `MovementSpeed`, `Resource`, `ResourceRegeneration`, `IncreasedItemRarity`, `IncreasedItemQuantity`, `Experience`. **No crit, no retreat threshold, no aggression.** | `Data/Statistics/Enums/StatName.cs` |
| Resource type | `CurrentValue`, `IsDepleted`/`IsFull`, `AddToCurrent`/`RemoveFromCurrent` return the un-applied remainder, `RefillCurrent`/`DepleteCurrent`, events on change/deplete/recharge | `CharacterResource.cs` |
| Faction | `CombatFaction` SO with `IsEnemy` / `IsAlly` — exists, **unused by `BaseCharacter`** | `Runtime/Character/CombatFaction.cs` |
| Containers | `AbstractDimensionalContainer`: `Capacity`, `StoredPackages` dict, `OnContentChanged` event, `TryAddToContainer(ref Package)` → true iff fully placed, logs "X is full!" otherwise. **No `IsFull` property.** 4 containers `new()`'d in `InventoryProvider.Awake` | `AbstractDimensionalContainer.cs:17,27,38-84`; `InventoryProvider.cs:51-62` |
| `LocalPlayer.PickUpItem` | auto-equip → `Inventory.TryAddToContainer` → (debug) `Stash`; returns false if all full | `LocalPlayer.cs:147-169` |
| Provider | `InventoryProvider` is a god object — 4 containers + ~40 UI callbacks + `RestockStore` (20 items) + `StashInventory` | `InventoryProvider.cs` |
| Scenes | `Example.unity` + `HUD.unity`; `SceneProvider.LoadScene(name)` is a coroutine with a loading screen; `LoadSceneButton` wires a button to it | `SceneProvider.cs:39`; `GUI/Components/Buttons/LoadSceneButton.cs` |
| UI primitives | `AbstractPanel` (DOTween fade/scale/move, `FadeIn`/`FadeOut`/`Toggle`); `PanelToggle` (toggle → panel fade); `RadioGroup` (one-active-toggle + `OnGroupChanged`); `AbstractButton` / `AbstractToggle`; `Slider` already used for `InventoryProvider.amountSlider` | `GUI/Components/**`; `InventoryProvider.cs:38-40` |
| Derived stats | `LocalPlayer.cs:21` — `// TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood`. Not built. | `LocalPlayer.cs:21` |
| Assemblies | `Utility`, `InventorySystem.Data`, `.Geometry`, `.Probability` (+ 3 test asmdefs). Everything else is `Assembly-CSharp` | `git ls-files '*.asmdef'`; ADR-0007 |
| Determinism seam | `ProbabilityTable<T>.Sample(float roll)` — roll is a **parameter**, no `Random` inside | `Probability/ProbabilityTable.cs:52-79`; ADR-0005 |

The glossary (`CONTEXT.md`) has **no term** for run / encounter / location / town /
haul / expedition. A `/domain-modeling` pass should pin these before a spec.

---

## 2. The developer's own prior art (sibling projects)

### AutoBattler (`C:\Users\loles\Desktop\LEONID\AutoBattler`) — the closest model

ADR-0007 of *this* repo says InventoryTetris's assembly graph is being reshaped to
mirror AutoBattler. AutoBattler is a **no-player-input auto-battler**, and its
**ADR-0012** (`AutoBattler/Docs/adr/0012-core-loop-hands-off-and-itemized-enemies.md`)
worked the exact "who plays the fight?" question the owner is asking, and landed on:

- **"Combat is hands-off — the player issues no input during Resolution"** (Decision
  1, "one-way door").
- **"The plan is the seat of mastery, and Resolution owes the player a complete
  readout"** (Decision 2) — variance is allowed, *unexplained* variance is not; a
  post-combat recap reconstructs the fight.
- **"No permadeath; the run ends on the first loss; pawns are restored between battles
  — interim rule: full Health and full resource pools"** (Decision 5, explicitly an
  interim rule, health economy unresolved).
- **"Enemy threat is authored as itemization … not scripted behaviour or stat
  inflation"** (Decision 3) — enemies are built with the same system as the hero.
- It **explicitly closed the "two combat modes" idea** and rejected "auto-combat with
  1–2 command interventions" as "neither a clean spectacle nor real control."

**Caveat — the sister project chose a *different* player-agency model.** AutoBattler's
seat of mastery is **turn-based hex placement** before the fight (ADR-0001 §1, "two
combat time domains"), not sliders. The owner's slider model is a third option
ADR-0012 did not consider — closer to Melvor / Progress Quest. Aligning the *assembly
graph* (ADR-0007) does **not** imply aligning the *design*.

What transfers directly:

| Piece | File | What it is |
| --- | --- | --- |
| `CombatClock` | `Assets/Code/Runtime/Core/Combat/CombatClock.cs` | ~90 lines, **zero dependencies**, unit-tested. `Advance(deltaTime)` banks real time into an accumulator, fires `OnTick` 0..N times per call carrying the remainder, `maxTicksPerAdvance` clamp against the spiral of death, `ElapsedTime` is tick-quantised. "Pure and engine-agnostic so combat can be unit-tested by feeding deltas directly." |
| `CombatClockTests` | `Assets/Code/Tests/EditMode/Combat/CombatClockTests.cs` | 15 tests — sub-interval accumulation, frame-rate independence, spiral-of-death clamp, elapsed-time semantics. Ports with the class. |
| `CombatCoordinator` | `.../Combat/CombatCoordinator.cs` | The `MonoBehaviour` driver. `Update()` → `_clock.Advance(Time.deltaTime)`; `Tick()` = resolve movement, advance attacks, regenerate resources — **read-then-write against a frozen snapshot**. Raises `OnCombatEnded(CombatOutcome)`. |
| `PawnCombatController` | `.../Combat/PawnCombatController.cs` | Per-unit. Each weapon gets an **attack metronome** = `new CombatClock(1f / stats.AttackSpeed)` advanced by the master tick. Auto-targets via `TargetSelector.Select`. No player input anywhere. |
| `CombatOutcome` / `CombatOutcomeResolver` | `.../Combat/CombatOutcome*.cs` | `enum { PlayerVictory, PlayerDefeat }`; `Resolve(playerCount, enemyCount)` is a **pure function** — wiped team ends the fight. Unit-tested. |
| `GamePhaseController` | `Assets/Code/Runtime/Core/GamePhaseController.cs` | FSM. `enum GamePhase { Placement, Combat, Loot, GameOver }`; `TransitionTo(next)` = `GetPhase(Current).Exit()` then `GetPhase(next).Enter()`. Phase classes implement `IGamePhase { Enter(); Exit(); }`. Scripted `List<EncounterConfig>` sequence, index clamps on the last. |
| `CombatPhase` | `Assets/Code/Runtime/Core/CombatPhase.cs` | `Enter` subscribes `OnCombatEnded` + starts combat; `Exit` unsubscribes, stops, emits the recap; victory → Loot, defeat → GameOver. |
| `LootPhase` | `Assets/Code/Runtime/Core/LootPhase.cs` | Offers a **pick-N** from `EncounterConfig.scriptedLoot` — *not* an auto-grant. `TryPick` lands the item in the stash immediately; unclaimed offers are discarded on Continue. |
| `EncounterConfig` | `Assets/Code/Data/Pawns/EncounterConfig.cs` | `ScriptableObject`: `enemies: List<SpawnData>` (full snapshot, respawned every load), `players: List<SpawnData>` (delta — pawns introduced *this* encounter), `scriptedLoot: List<ItemConfig>`, `lootPickCount`. `SpawnData { PawnConfig config; Hex startHex; }`. |
| `PlayerData` | `Assets/Code/Runtime/Core/PlayerData.cs` | `Stash`, `currentEncounter`, `OwnedPawns`, `DeployedPawns`. Inline comment lists the persistence wishlist ("the active combat / the current map / all terrain changes / the current pawns … with their positions") — **not built.** |
| `Resource.Regenerate(ratePerSecond, deltaTime)` | `.../Statistics/Resource.cs:54` | Regen driven per tick by the combat clock — the clean replacement for `BaseCharacter`'s `async void` regen. ADR-0008: mana regenerates *during* combat so the economy is a sustainable fire rate, not a one-shot pool. |

**Heavier than a minimal version needs:** the hex grid + pathfinding +
`MovementResolver` + contested-hex arbitration; the chain/topology weapon system;
`DeliveryResolver` / anchors / affinities / payload cost trees; the `CombatRecap` /
`AttackReport` legibility slice; per-weapon reactor event wiring. The MVP wants the
**clock, the outcome resolver, the phase FSM, and the config SO** — not the spatial
simulation.

### ARPG Combat (`C:\Users\loles\Desktop\LEONID\ARPG Combat`) — fixed-step primitives

- **`Assets/Code/Tools/Ticker.cs`** — `[Serializable]`, `progress += tickInterval`,
  `HasRemainingDuration`, `Progress01`, `Start()` (zero), `Restart()` (subtract
  duration). A per-instance countdown. No event, no 0..N-per-call, no
  accumulator-carry beyond `Restart`. Simpler than `CombatClock`; adequate for a
  single attack cadence, inadequate as a master heartbeat.
- **`Assets/Code/TeppichsTools/Runtime/Time/Ticker.cs`** — even thinner:
  `Tick(delta)` returns `duration <= counter`, `Reset()`, `ChangeDuration()`.
- **`Assets/Code/Tools/StepLock.cs`** — a ref-counted lock; `locked` event fires on
  first-in / last-out. The pattern for **pausing the sim while a modal is open** (open
  the shop → `stepLock.Add(shopPanel)` → clock stops advancing). Transferable and
  cheap.
- **`Assets/Code/Pawns/Pawn.cs`** — still a per-frame `Update()` with DoT effects on
  `Ticker`s and a per-frame `Regenerate(ResourceName, StatName)`. Player-input-driven;
  **no auto-attack loop.** Its `ResourceOverTimeEffect` / `StatusEffect` /
  `ConditionEffect` system is more than the MVP needs.
- **`Assets/Code/Tools/Spawner.cs`** — dead simple: `Instantiate` N prefabs at random
  navmesh points on `Start()`. The MVP enemy spawner can be this small.

**`Ticker.cs` and `StepLock.cs` are the fixed-timestep answer** the task pointed at —
but `CombatClock` (AutoBattler) is the same idea done more completely and already
tested, so port that and keep `StepLock` as the pause primitive.

### Ruadh2 (`C:\Users\loles\Desktop\LEONID\Ruadh2`) — a run/battle/shop loop + abstract resolution

`Assets/Code/Runtime/Provider/CurrentRunProvider.cs` is a complete idle-adjacent run
loop:

- `ISerializable<CurrentRunMemento>` (DataContract mementos) — a **persistence seam**
  already shaped.
- `Hearts` (a lives currency, `maxTries = 5`) and `Gold`; `passiveIncome = 5`.
- `StartNewRun()` clears units, resets streak, seeds gold, resets the shop;
  `GoToBattle()` resolves a battle, spends a Heart on a loss, `Hearts <= 0` → "GAME
  OVER" → `OnRunEnd` → `StartNewRun()`.
- `CalculateIncome()` = `passiveIncome + wonBonus + streakBonus + hoardBonus` — a
  worked example of "the field pays out, town spends it."
- Combat is resolved as an **abstract boolean** — `CombatProvider.PseudoCombatSuccess(Units)`
  (commented out), with the intended maths visible in the debug logs:
  `Units.Sum(health)` vs `Units.Sum(damage × 1/attackSpeed)`. This is the "abstract
  dice-roll resolution" option, from the owner's own hand.
- `RuadhWarbands/Assets/Code/Data/ScriptableObjects/Encounter.cs` — an `Encounter`
  SO: `Party: List<ClassData>`, `Reward: uint`, `IsFinalBossBattle: bool`. The
  minimal location/encounter asset.

### BoneAndBlood (`C:\Users\loles\Desktop\LEONID\BoneAndBlood`) — derived stats (the `see Bone&Blood` note)

- `Prototype/Assets/Core/Runtime/Combat/ModifiableAttributesBase.cs` — **primary
  attributes → secondary (derived) attributes**, each resolved through an ordered
  `AttributeModifierBase.Modify(int)` chain (`GetPrimaryAttribute` /
  `GetSecondaryAttribute`). `CreatePreviewClone()` for equip-comparison.
- `.../Combat/DamageCalculation/DamageCalculation.cs` — a **staged damage pipeline**:
  `Begin → DefenseAttributeDetermined → DamageAndImpactFactorCalculated → … →
  ResultingResourcesCalculated`, with a `DamageModifier` list keyed per stage.
  Defense is `1 - defenseAttribute/100` (the same shape as
  `BaseCharacterExtensions.CalculateReceivingDamage`).

**Much heavier than the MVP needs.** The transferable ideas: (1) derived combat stats
= an ordered modifier chain over a base — which `MutableFloat` already does; (2) a
damage calc with named hook-point stages, if/when the placeholder formulas are
replaced behind the `Encounter` interface. Neither is MVP work.

### DungeonCrawler / ARPG / RuadhWarbands

No run/town loop worth extracting beyond the `Encounter` SO noted above. Not
rabbit-holed.

---

## 3. External prior art

| Source | What it establishes | Link |
| --- | --- | --- |
| **Game Programming Patterns — Game Loop** | The fixed-update / variable-render accumulator: `lag += elapsed; while (lag >= MS_PER_UPDATE) { update(); lag -= MS_PER_UPDATE; } render();`. Variable timesteps are non-deterministic ("the *same* bullet will end up in *different places*"). Clamp the inner loop ("bail after a maximum number of iterations … the game will slow down, but that's better than locking up"). | <https://gameprogrammingpatterns.com/game-loop.html> |
| **GPP — Update Method** | Each entity gets `update()` once per tick; store **explicit state** (execution pauses between ticks); updates are **sequential, not concurrent**; modifying the entity collection mid-iteration skips objects — iterate backward, cache the count, or defer removals; pass elapsed time as a parameter. | <https://gameprogrammingpatterns.com/update-method.html> |
| **GPP — State** | FSM = states as classes with `enter()`/`exit()` actions; **concurrent state machines** (parallel FSMs) avoid state-explosion; **pushdown automaton** (a state stack) models a temporary state layered over an ongoing one; "FSMs excel when behavior maps to a relatively small number of distinct options." | <https://gameprogrammingpatterns.com/state.html> |
| **Loop Hero — postmortem (Game Developer)** | Original pitch: "a hero who walks along the path and chops monsters on his own, and all the player has to do is put on the hero's equipment and build up the road." Design emphasis on **indirect control** — "the player orchestrates conditions rather than executing actions." | <https://www.gamedeveloper.com/design/postmortem-loop-hero> |
| **Loop Hero — Four Quarters (Destructoid)** | The core tension: "constantly consider whether an immediate retreat is necessary or if it's worth the risk to push for either the boss battle or a consequence-free exit all the way back to base camp." Retreat keeps resources; death loses most of the run's haul. | <https://www.destructoid.com/closing-the-loop-four-quarters-on-the-making-of-loop-hero/> |
| **Progress Quest (Wikipedia)** | The zero-input reference: "once the player has set up their artificial character, there is no user interaction at all"; "Combat takes a set length of time." Satirises EverQuest-style auto-attack by removing input entirely. | <https://en.wikipedia.org/wiki/Progress_Quest> |
| **Melvor Idle — Combat (wiki)** | "Enemies are fought automatically." **Auto-Eat** = auto-consume food when HP drops below a player-set threshold — a tunable sustain setting. **Amulet of Looting** = auto-send loot to the bank (loot-return automation as an item). XP is from damage dealt, not received. | <https://wiki.melvoridle.com/w/Combat> |
| **Diablo II mercenaries (Diablo Wiki / GameFAQs)** | Merc AI is **per-type and not player-configurable** — the Act 5 barbarian is "tuned to be more aggressive"; rogues get "confused as to how far they should stand from the action." **A poor model for tunable knobs** — it is fixed behaviour per unit. | <https://diablo2.diablowiki.net/Mercenaries> |
| **Path of Exile minions (dev tracker / community)** | Minion behaviour is driven by **aggro radius**. `Meat Shield` support halves it (stay close, defensive); `Feeding Frenzy` doubles it (roam, aggressive); golems are non-aggressive by default. **"Aggro radius" is the tunable-behaviour primitive** the genre uses. | <https://devtrackers.gg/pathofexile/p/c0856471-what-minions-are-now-a-consideration-with-the-new-aggressive-ai-support> |
| **Soda Dungeon (Steam / TV Tropes)** | Turn-based auto-combat with an **"Auto-Combat" toggle**; dungeon floors of 10 areas (5 = mini-boss, 10 = boss); return to the **tavern** to spend gold on upgrades and hire the next squad. "Arm your squad, turn on auto-grind, and do something else." | <https://store.steampowered.com/app/564710/Soda_Dungeon/> |
| **Hearthstone Battlegrounds (ONE Esports / Card Gamer)** | "Once your minion preparations are locked in … you have no control during this phase, so your preparation and planning are what determine the outcome." Combat resolves deterministically from position + effects; target pick is random unless a Taunt is present. Variance is fine when the log shows what rolled. | <https://www.oneesports.gg/gaming/hearthstone-battlegrounds-everything-you-need-to-know-about-blizzards-auto-battler/> |
| **Unity — `Time.timeScale` (docs)** | Scales the passage of time (slow-mo, pause, fast-forward). To keep a fixed-step sim consistent with *real* time under fast-forward, multiply `Time.fixedDeltaTime` by the scale. **Better here:** since `CombatClock.Advance(dt)` is a pure function, pass `Time.deltaTime × speedMultiplier` and leave global `Time` alone — otherwise DOTween panel tweens and UI speed up too. | <https://docs.unity3d.com/ScriptReference/Time-timeScale.html> |
| **Ryan Hipple — "Game Architecture with ScriptableObjects" (Unite Austin 2017)** | SO-based events, scriptable variables, runtime sets; reduce singletons and global state; systems testable in isolation. The direction ADR-0005 / ADR-0007 already follow. | <https://github.com/roboryantron/Unite2017> |

---

## 4. The loop, piece by piece

Each piece: what the rework spec says → prior art → options for a *minimal* version →
recommendation.

### 4.1 The run/town state machine

**Rework spec:** `InTown ↔ InRun(encounter…) → ReturnToTown(loot) | Death`
(foundational-rework line ~427). Silent on intermediate states and on what
`ReturnToTown` vs `Death` differ by.

**Prior art:** AutoBattler's `GamePhaseController` — a hard FSM, `IGamePhase { Enter;
Exit; }`, `TransitionTo` runs `Exit` then `Enter`
(`AutoBattler/Assets/Code/Runtime/Core/GamePhaseController.cs:136-141`). GPP-State:
concurrent FSMs + pushdown for a temporary state over an ongoing one. Ruadh2's
`CurrentRunProvider` — `StartNewRun` / `GoToBattle` / GAME OVER on `Hearts <= 0`.

| Option | States | Trade-offs |
| --- | --- | --- |
| **A. Two states** | `InTown`, `InField` | Minimal. `SendTo(location)` and `Recall()` are the only transitions; Death is `Recall` with a penalty flag. No travel concept. Fits "minimal inputs." |
| **B. Three states** | `InTown`, `Traveling`, `InField` | `Traveling` is a 1–2 s beat — a natural home for a "resolve on arrival" burst (4.2 opt. B) or a loading-screen reuse (`SceneProvider`). Adds one state, one timer. |
| **C. Full phase FSM** | `InTown`, `Traveling`, `InField`, `Recap`, `GameOver` | Mirrors AutoBattler 1:1. `Recap` shows the `EncounterResult` before returning control. `GameOver` if you want a hard run-end (Ruadh2's Hearts). More UI, more transitions than the owner asked for. |

Sub-state (concurrent FSM, GPP-State): opening the Shop/Stash/Inventory while
`InField` is a **pushed** state — either the sim keeps running underneath (tension:
the hero can die while you shop) or `StepLock` pauses it (safe, less pressure). MVP:
pause the sim while a *modal* town panel is open; keep it running while only the
always-on inventory panel is open.

**Recommendation: A now, with the `Traveling` state (B) stubbed as an immediate
transition** so it can gain a duration later without a refactor. `Recall` and `Death`
both transition `InField → InTown`; they differ only in what happens to the
`pendingResult` (see 4.5 / 4.6). No `GameOver` in the MVP — a dead hero just loses the
haul and lands in town (matches AutoBattler ADR-0012 Decision 5's "restored between
battles" more than Ruadh2's hard run-end).

### 4.2 The combat tick / simulation model

**Rework spec:** "a fixed-timestep combat tick (replaces the `async void` regen and
`// TODO: COMBAT TICK RATE`), enemy waves, hero auto-attack with attack-speed timing,
death/flee outcomes" — inside the `Encounter` module.

**Prior art:** AutoBattler `CombatClock` (accumulator, 0..N ticks/call,
spiral-of-death clamp, pure, tested) + per-weapon `new CombatClock(1f / AttackSpeed)`
metronomes; `CombatOutcomeResolver.Resolve(playerCount, enemyCount)` pure.
GPP-Game-Loop: the same accumulator, and the determinism argument. Ruadh2:
`PseudoCombatSuccess` (abstract). HS Battlegrounds: "locked in," resolves without
input, variance shown not hidden.

| Option | How the fight resolves | Answers `// TODO: COMBAT TICK RATE` | Trade-offs |
| --- | --- | --- | --- |
| **A. Fixed-timestep wave sim** | Port `CombatClock`. Master tick drives: hero attack cadence (`1f / AttackSpeed`), enemy cadences, `Resource.Regenerate(rate, tickInterval)`, wave-clear check. Each cadence tick calls the existing `BaseCharacter.DealDamageTo`. Wave clears → next wave after a beat. `CombatOutcomeResolver`-style end check (hero dead / recalled). | **Directly** — the clock *is* the tick rate. `interval = 0.1 s` (AutoBattler's default, 10 ticks/s). | Reuses `DealDamageTo` / `CalculateDamageOutput` / regen semantics unchanged. Deterministic when the loot RNG is seeded (`ProbabilityTable.Sample` already takes the roll as a param). Runs live while inventory is open. Cost: port 1 class + write the wave driver (~150 lines, the `CombatCoordinator` shape minus movement/hex). |
| **B. Turn/round resolution** | Discrete rounds; each round every combatant acts once (order by `AttackSpeed`); resolve to accumulated damage + a win/lose. Pace rounds ~0.5 s so it is still watchable. | Indirectly — "tick" becomes "round," `AttackSpeed` becomes actions-per-round. | Simpler (no sub-second timing). Loses attack-speed granularity — a 1.1× vs 1.4× AttackSpeed gear choice barely reads. Still needs a per-round loop and a driver. |
| **C. Abstract stat resolution** | Compare hero `Σ(DPS)` and `EHP` vs the enemy set's; compute time-to-kill each way; roll a result (win + duration + leftover HP, or death at time *t*). Instant, or over a fake progress bar. | Sidesteps it — there is no tick. | Cheapest. Throws away `DealDamageTo` and the "watch the race" tension (Loop Hero, BG). Gear differences feel abstract — bad for a game whose point is gear. Ruadh2 shelved this (`PseudoCombatSuccess` commented out). |

**Recommendation: A.** It is the rework spec's stated direction, the implementation
already exists and is tested in the sister project the assembly graph targets
(ADR-0007), it is the only option that keeps `AttackSpeed` / `PhysicalDamage` /
`Armor` and gear choices legible in the sim, and determinism + testability come free
from the pure-clock pattern (`CombatClock.Advance(dt)` and `ProbabilityTable.Sample(roll)`
are the same shape — the caller supplies the non-deterministic input). Fall back to B
only if the wave driver overflows one issue's context.

**Live vs resolve-on-arrival:** run it **live** (A) — the hero fights in real time
while the player manages inventory; Recall is a decision made against live HP. This is
the Loop Hero / Melvor / Soda Dungeon shape and the owner's phrasing ("goes out and
fights … Inventory management should be available at all times") points at it. Add a
**sim-speed multiplier** (`1× / 2× / 4×`, pause) via `clock.Advance(Time.deltaTime ×
multiplier)` — *not* `Time.timeScale`, to spare the UI tweens
(Unity docs; `SceneProvider` and `AbstractPanel` both lean on unscaled/`DOTween` time).

**Where it lives (assembly graph, ADR-0007):**

- `CombatClock` → **carve into a small leaf asmdef now** (`InventorySystem.Combat`, or
  fold into `Utility`). Zero dependencies, no ordering cost, immediately retires
  `// TODO: COMBAT TICK RATE` and can replace the `async void` regen wart
  (`BaseCharacter.cs:64`).
- `Encounter` wave driver + `EncounterResult` + `RunState` → a new
  `InventorySystem.Encounter` asmdef **above `Items`** (it needs `RollContext` /
  `ItemInstance` for loot) and below `Runtime`. Unity-free core; a `MonoBehaviour`
  driver in `Runtime` (the `CombatCoordinator` shape). The hero/enemy are reached
  through a small `ICombatant` interface (Health, damage output, attack speed,
  `TakeDamage`) — exactly AutoBattler's `IPawn` — so the pure core never references
  `BaseCharacter`.
- **Ordering caveat:** `Items` does not exist until rework Phase 1. Doing the
  `Encounter` module before then means either a temporary `Assembly-CSharp` home for
  the driver, or reordering the rework. See [ADR pressure](#adr-pressure-and-contradictions).

### 4.3 Behaviour sliders → sim parameters

**Rework spec:** silent. Owner's examples: aggression / retreat-HP-threshold, skill vs
auto-attack mix, greed / how-full-before-recall, resource spend rate.

**Prior art:** Melvor Auto-Eat (HP-threshold sustain setting). PoE `Meat Shield` /
`Feeding Frenzy` (aggro radius = defensive/aggressive). D2 mercs — *not* a model
(fixed per type). `BaseCharacter.SpendResource` (`:21`) is already the on/off version
of a resource-spend knob; `CalculateRequiredResource` (`BaseCharacterExtensions.cs:11`)
is the gate.

3–5 knobs worth exposing:

| Slider | Range | Sim input | Maps to today |
| --- | --- | --- | --- |
| **Retreat at HP %** | 0–90 % | auto-`Recall` when `health.CurrentValue / health.TotalValue < t` | new `RunState` input. Melvor Auto-Eat analogue. **The single most important knob** — it automates the Loop Hero retreat decision. |
| **Recall when bags % full** ("greed") | 50–100 % | auto-`Recall` when projected fill of bag + haul ≥ t | new input. The push-vs-retreat pressure on the loot axis. |
| **Enemies per wave** ("aggression") | 1 – N | wave spawn count | scales incoming DPS. PoE aggro-radius collapsed to a count (no spatial field in the MVP). |
| **Resource reserve %** | 0–80 % | use the costly `MagicalDamage` attack only while `resource.Current / Total > reserve`; else the cheap `PhysicalDamage` one | `SpendResource` (bool) is the crude version today; `CalculateRequiredResource` gives `{Physical .03, Magical .1}` of Resource. |
| **Sim speed** (optional) | 1× / 2× / 4× | `clock.Advance(dt × multiplier)` | Unity `Time.timeScale` docs; keep it on the clock. |

**Magic Find is not a slider** — it stays gear-driven via `IncreasedItemRarity`
(ADR-0004, CONTEXT.md "Magic Find"). Turning it into a knob would undermine the whole
loot-stat design.

Implementation: a plain serializable `HeroBehaviour { float retreatHpFraction; float
recallFillFraction; int enemiesPerWave; float resourceReserveFraction; }` the sim
reads each tick; the sliders write it (Unity `Slider.onValueChanged`, same wiring as
`InventoryProvider.amountSlider`, `:38-40,103`). No new stat enum entries.

### 4.4 The map / location model

**Rework spec:** `RollContext { int SourceLevel; float MagicFind; LootTable Table; }`
— a location must supply `SourceLevel` and `Table`. Silent on count and on town.

**Prior art:** AutoBattler `EncounterConfig` (`enemies` + `scriptedLoot` +
`lootPickCount`). RuadhWarbands `Encounter` (`Party` + `Reward` + `IsFinalBossBattle`).
Soda Dungeon (floors of 10, boss at 10). Loop Hero (four bosses + camp).

**What a location *is*:**

```
LocationConfig : ScriptableObject
{
  string   Id;            // stable, serialized (same rule as ItemDefinition.Id)
  string   DisplayName;
  int      SourceLevel;   // → RollContext.SourceLevel; scales enemy stats
  EnemySet Enemies;       // MVP: an archetype + a count range
  LootTableRef LootTable; // → RollContext.Table (one shared table until Seam 1)
}
```

| Option | Locations | Trade-offs |
| --- | --- | --- |
| **A. Town + 2 field** | one "easy" (level ~1), one "harder" (level ~5) | Proves "the player chooses where" and "harder = better loot, more risk." Minimum that is a *choice*. |
| **B. Town + 3–4 field, progressive unlock** | unlock the next by clearing X waves in the current | The Soda Dungeon / Loop Hero ramp. More content authoring, an unlock-state to persist. |
| **C. Town + a generated node graph** | procedural map à la Loop Hero's loop | Out of scope for a minimal version — needs a map generator and a graph UI. |

**Town as a location vs a mode:** make **town a `RunState`, not a `LocationConfig`.**
Clicking "town" on the map = `Recall()`. Field locations are assets; town is special
(it is where management lives). Avoids a null-`EnemySet` special case.

**Travel:** **instant teleport** for the MVP (owner: "minimal inputs, no fancy
stuff"). The `Traveling` state (4.1 B) is stubbed so a 1–2 s beat can be added later.

**Map UI:** a `RadioGroup` of location toggles (one selected) + a **Send** button
(`InTown → InField`) and a **Recall** button (`InField → InTown`), built from
`AbstractButton` / `AbstractToggle` / `RadioGroup` — no new UI framework. A
`LoadSceneButton`-style "one button per destination" also works but loses the
"selected destination" affordance.

**Recommendation: A** (town + 2 field, all open), with the `LocationConfig` shape
above so B is additive.

### 4.5 The encounter result

**Rework spec:** "Its `EncounterResult` shape and what it must persist both constrain
Seam 3 — sketch its interface before building the save system."

**Prior art:** AutoBattler `CombatOutcome` enum + `CombatRecap` / `AttackReport`
(a full legibility model — heavier than MVP). Ruadh2 `CalculateIncome` (a payout
formula). Melvor (XP from damage dealt).

```
enum RunOutcome { Recalled, Died }

EncounterResult          // immutable value type; the RunState accumulator
{
  RunOutcome            Outcome;
  IReadOnlyList<Package> Loot;      // pre-Seam-1: List<Package>; post: ItemInstance[]
  int                   Xp;
  Currency              Currency;   // or coin piles (CONTEXT.md "Pile")
  int                   EnemiesDefeated;
  int                   WavesCleared;
  float                 Duration;   // = CombatClock.ElapsedTime
}
```

It feeds:

- `Xp` → `LocalPlayer.GainExperience(xp, location.SourceLevel)` (`LocalPlayer.cs:65`)
  — already does level-up and the growth `StatModifier`.
- `Loot` → the loot-return path (4.6).
- `Currency` → the wallet (post-rework Phase 3) / `Inventory` currency cells today.
- `Outcome` → whether `Loot` is delivered or mostly forfeited (4.6).

During a run, `RunState` holds a **mutable builder** (`pendingResult`) that each
wave-clear appends to; `Recall` / `Death` freezes it into the immutable
`EncounterResult`. This is the AutoBattler `CombatRecap` pattern (accumulate during,
freeze on exit — `CombatPhase.Exit` builds the report) minus the per-attack detail.

### 4.6 Loot return

**Rework spec:** the stated direction is a **ground loot pile** — "encounter output
lands at a location; player picks up. Rides on Seam 2. Replaces `DummyTarget.OnDeath`'s
auto-vacuum."

**Prior art:** `DummyTarget.OnDeath` today vacuums straight into the bag via
`PickUpItem` (`DummyTarget.cs:18-22`). Melvor's Amulet of Looting auto-banks. Loop
Hero: the haul is lost on death, kept on retreat. AutoBattler `LootPhase`: a
**pick-N** offer, not an auto-grant, each pick lands in the stash immediately.

| Option | Where loot goes | Trade-offs |
| --- | --- | --- |
| **A. Ground pile at the field, collect before Recall** | items sit "on the floor"; player picks up | The rework's stated direction. But it fights "inventory always available" and "minimal inputs" — a pickup interaction in a *non-spatial* field is awkward, and unclaimed items on Recall are lost or orphaned. Best once the field has real space (post-Seam-2). |
| **B. Auto-to-stash on Recall** | straight into the Stash (Melvor Amulet of Looting) | Simplest. Skips the "inventory full" tension **and** the Tetris packing decision that is the point of the project. |
| **C. Haul container, delivered on Recall** | `pendingResult.Loot` becomes a read-only **"Haul"** container in town; the player drags items into bag/stash or sells them | Keeps the packing decision, keeps inventory-always-available, needs no field pickup UI. "Haul" is a new *role* of `CharacterInventory` (CONTEXT.md: Inventory / Stash / Store are roles of one type). Loot that will not fit stays in the Haul until the player makes room or sells. |

Because loot accrues to `pendingResult` (not the live bag) *during* the run, the bag
cannot overflow mid-fight — the "recall when bags % full" slider (4.3) is the player's
lever, checking projected bag + haul fill.

**Recall vs Death:**

| | Recall | Death |
| --- | --- | --- |
| Loot (`pendingResult.Loot`) | delivered to the Haul | **mostly forfeited** — keep a fraction (e.g. 25 %), or nothing |
| XP | applied in full | applied in full (Melvor: XP is from damage dealt, already banked per wave) |
| Currency | delivered | small penalty, or forfeited |
| Hero HP / Resource | as-is, or refill in town | refill in town |
| State | `InTown` | `InTown` (no `GameOver` in the MVP) |

The exact Death penalty **is** the retreat-vs-push tension — the genre has no single
answer (Loop Hero: lose most; Soda Dungeon: keep what you banked per floor; roguelites
vary). **Owner decision needed** (open question 2).

**Recommendation: C** for the MVP, with A (the rework's ground pile) as the target
once Seam 2 lands and the field has space. Flag the mild divergence from the spec.

### 4.7 Inventory always available — the field/town UI split

**Rework spec:** "inventory management must always be available" (User Story 12
region; foundational-rework §"What the ARPG scope needs").

**Prior art:** `AbstractPanel.FadeIn/FadeOut`, `PanelToggle`, `RadioGroup` already do
panel show/hide. `Example.unity` and `HUD.unity` are the two scenes; `SceneProvider`
switches them with a loading screen.

The field/town split is a **panel-visibility** concern, not a scene concern:

- **One scene.** The inventory + equipment + character panels stay mounted and
  interactive in both `InTown` and `InField`.
- **Town-only panels** — Shop (`Store`), Stash — have their `PanelToggle`s **disabled
  while `InField`** and re-enabled on Recall. This is what gives town its purpose
  (owner: "more dedicated management tools").
- The map panel is visible in both (you send from town, you recall from the field).
- `Example.unity` is likely the full debug menu; `HUD.unity` a leaner overlay — the
  MVP can build on either, or ignore the split and use one. (Scene *contents* not
  fully audited; `SceneProvider` + `LoadSceneButton` are the only switch wiring.)

**Can you sell from the field?** MVP: **no** — Store/Stash disabled `InField`. Selling
from the field is a later convenience (a "field merchant" consumable, or a stat).

### 4.8 Persistence

**Rework spec / ADR-0006:** Seam 3 is **design-only** — "an explicit serialized
definition id … and an instance-to-POCO round trip" are baked into Seam 1 now; the
save system is built later. `EncounterResult` + `RunState` "constrain Seam 3."

**Prior art:** Ruadh2 `CurrentRunProvider : ISerializable<CurrentRunMemento>` — a
DataContract memento seam. AutoBattler `PlayerData` — the persistence wishlist is a
comment, unbuilt. InventoryTetris today: nothing persists; containers are `new()`'d
every run (`InventoryProvider.cs:51-62`).

| Layer | MVP (single session) | What a future save must cover |
| --- | --- | --- |
| Hero | in memory: level, XP, allocated growth `StatModifier`s, current HP/Resource | level + XP + the growth-modifier list (or recompute from level) |
| Containers | in memory (as today) | all four containers + the Haul + the wallet — **this is exactly rework Seam 1's `[{ x, y, definitionId + instance DTO, amount }]`** (foundational-rework §"Persistence constraints") |
| Run | in memory: `RunState` enum, current `LocationConfig`, `pendingResult` builder, `CombatClock` elapsed | **force `InTown` on load** → the run itself never needs to serialize |
| Map | in memory: which locations are unlocked (none, if all open — 4.4 A) | a small unlocked-set blob (only if 4.4 B) |

**The MVP needs nothing to persist across quit.** What the `RunState` machine *forces*
into memory is small (enum + one config ref + one result builder + the clock). The
constraint worth stating now, per ADR-0006: **saving is only allowed `InTown`** — this
bounds the future save surface to "rework Seam 1's container DTOs + hero level/XP + a
tiny town blob" and keeps mid-run state (the `pendingResult`, the sim clock) out of
the save format entirely. If the owner wants mid-run saves later, that is a separate,
larger decision.

### 4.9 The thinnest playable slice (pulled together)

A recognisable **send hero out → he fights → loot comes back → manage in town → send
out again** loop:

**Reused unchanged**

- `BaseCharacter.DealDamageTo` / `ReceiveDamageFrom` / `IsDead` (`BaseCharacter.cs:108,136,22`)
- `CharacterResource` add/remove/refill/deplete + events (`CharacterResource.cs`)
- `BaseCharacterExtensions.CalculateDamageOutput` / `CalculateReceivingDamage` / `CalculateRequiredResource` — still placeholder, fine (`BaseCharacterExtensions.cs`)
- `LocalPlayer.GainExperience` / `PickUpItem` / `AddItemStats` / `RemoveItemStats` (`LocalPlayer.cs`)
- `ItemProvider.GenerateRandomLoot(amount)` (`ItemProvider.cs:59`) — becomes `ItemGenerator.RollLoot(RollContext)` after rework Phase 1
- The four containers, drag/drop, `Sort`, `Consolidate`, `PreviewProvider`
- `AbstractPanel` / `PanelToggle` / `RadioGroup` / `AbstractButton` / `AbstractToggle` / `Slider`
- `CombatFaction` SO (`Runtime/Character/CombatFaction.cs`) — finally used, for hero vs enemy sides
- `SceneProvider` — one scene is enough; it stays as-is

**Stubbed**

- Enemies: one parametric archetype (a `DummyTarget` subclass) with stats scaled from `LocationConfig.SourceLevel`; no per-monster assets, no variety
- Skills: the existing physical/magical `DamageType` split *is* the skill system
- Travel: instant teleport; `Traveling` state present but immediate
- Persistence: single session; `RunState` forced to `InTown` on start
- Loot table: one shared table biased by `SourceLevel` until Seam 1's `LootTable` exists
- Derived stats from attributes (`LocalPlayer.cs:21`): not in the MVP

**Genuinely new**

| New thing | Size | Notes |
| --- | --- | --- |
| `CombatClock` (+ tests) | port ~90 + ~180 lines | verbatim from `AutoBattler/Assets/Code/Runtime/Core/Combat/CombatClock.cs`; new leaf asmdef; retires `// TODO: COMBAT TICK RATE` and the `async void` regen |
| `Encounter` wave driver | ~150 lines | the `CombatCoordinator` shape minus movement/hex: spawn wave → run hero + enemy cadences on the clock → wave-clear / hero-death → append to `pendingResult` |
| `RunState` FSM | ~80 lines | `InTown` / `InField` (+ stub `Traveling`); `SendTo(location)`, `Recall()`, `HandleDeath()`; owns `pendingResult` |
| `EncounterResult` + `RunOutcome` | ~30 lines | value type (4.5) |
| `LocationConfig : ScriptableObject` | ~20 lines + 2 assets | id / name / source level / enemy set / loot table ref (4.4) |
| `HeroBehaviour` config + 3–5 sliders | ~20 lines + UI wiring | `Slider.onValueChanged` → the config; sim reads it per tick (4.3) |
| Map panel | 1 prefab | `RadioGroup` of location toggles + Send/Recall buttons |
| "Haul" container role + recall hand-off | ~40 lines | a `CharacterInventory` in a new role; `pendingResult.Loot` populates it on Recall (4.6 C) |
| Field/town panel visibility | wiring only | disable Shop/Stash `PanelToggle`s while `InField` (4.7) |
| Sim `MonoBehaviour` driver | ~60 lines | temporary `Assembly-CSharp` home pending the rework (4.2 caveat) |

Roughly: **1 ported class, ~6 new small types, ~4 UI prefabs from existing
primitives, 3 config assets.** No change to the item model, containers, wallet, or
drag system.

---

## 5. ADR pressure and contradictions

| ADR / spec | Pressure | Resolution options |
| --- | --- | --- |
| **ADR-0006** (rework order: item → transaction → wallet; combat sim is Tier 2, *after* all three) | The MVP wants a combat sim **now**. The rework spec assumes it comes last. | (a) Build the `Encounter`/`RunState` MVP now with a temporary `Assembly-CSharp` home for the driver and `List<Package>` loot, refactor onto `Items`/`RollContext` during rework Phase 1; (b) finish Seams 1–3 first; (c) reorder. Note: the spec *itself* says "sketch [the `RunState`] interface before building the save system" — so a **design pass now is consistent**; only *building* it now is the tension. |
| **ADR-0007** (per-module assemblies; combat model named as one of "the two big clusters that have not had this treatment") | A new `Encounter` module is expected and welcome — but it depends on `Items` (for `RollContext`), which does not exist until rework Phase 1, so it cannot cleanly precede it. | Carve `CombatClock` out **immediately** (zero deps, no ordering cost). Keep the `Encounter` core Unity-free + `ICombatant` interface so the eventual module drop is mechanical. |
| **ADR-0005 / determinism** | The sim's loot + any combat rolls must be reproducible for testing. | Route all sim RNG through a seeded source; `ProbabilityTable.Sample(roll)` already takes the roll as a parameter — the same "caller supplies the non-deterministic input" shape as `CombatClock.Advance(dt)`. No contradiction, a constraint to honour. |
| **Rework spec "ground loot pile"** (Tier 2 stated direction) | The recommended MVP loot-return (4.6 C, a Haul container delivered on Recall) diverges — driven by "inventory always available" + a non-spatial field. | Mild divergence. Adopt the ground pile once Seam 2 lands and the field has real space; the `pendingResult.Loot` list is the same data either way. |
| **AutoBattler ADR-0012** (the sister project's own answer to this exact question) | It picked **spatial placement** as the seat of mastery and **explicitly rejected** "a few command interventions" and "two combat modes." The owner's **slider** model is a third option it did not consider. | Not a contradiction with *this* repo — but a signal: aligning the assembly graph (ADR-0007) does **not** mean aligning the design. The slider model is closer to Melvor / Progress Quest. Worth a conscious "we are diverging from AutoBattler here, on purpose." |
| **CONTEXT.md glossary** | Silent on run / encounter / location / town / haul / wave. | A `/domain-modeling` pass to pin the ubiquitous language before the spec is written. |
| **`InventoryProvider` god object** (`InventoryProvider.cs:14` `// TODO`) | The MVP adds a `RunState`, a map, sliders, a Haul — more surface for the god object to absorb. | Put the new state in its own provider(s) (`RunProvider` / `EncounterProvider`), not on `InventoryProvider`. The rework's Tier 4 god-object split is *not* MVP work, but do not make it worse. |

---

## 6. Open questions the owner must answer before a spec

1. **Sequencing.** Build the `Encounter` / `RunState` MVP now (temporary
   `Assembly-CSharp` home, `List<Package>` loot, refactor in rework Phase 1), or
   finish rework Seams 1–3 first? ADR-0006 and the rework spec assume the latter.
2. **Death penalty.** What exactly does Death forfeit that Recall keeps — all pending
   loot? a fraction? currency only? Is there a revive cost? This is the knob the whole
   retreat-vs-push tension hangs on and the genre has no default (Loop Hero loses
   most; Soda Dungeon keeps what was banked per floor).
3. **Live vs resolve-on-arrival.** Does the player watch the fight resolve in real
   time (health bars, a combat log) while managing inventory, or does it resolve on
   arrival with a recap screen? Changes how much UI the MVP needs. (Recommendation:
   live.)
4. **Combat resolution model.** Confirm the fixed-timestep wave sim (4.2 A) over the
   cheaper round (B) or abstract (C) resolution.
5. **Loot delivery.** Haul-container-on-Recall (4.6 C, recommended) vs. the rework's
   ground pile (A) vs. auto-to-stash (B).
6. **Locations.** How many field locations, and are they all open from the start
   (4.4 A) or progressively unlocked (4.4 B)?
7. **Run persistence.** In the eventual save, is `InTown` the only saveable state
   (recommended — bounds the save surface), or must a run survive an app quit?
8. **Between-run hero state.** Does the hero refill HP/Resource on every return to
   town (AutoBattler ADR-0012 interim rule), or is there inter-run attrition?

---

## Appendix — sibling files drawn from

| Project | File | Used for |
| --- | --- | --- |
| AutoBattler | `Assets/Code/Runtime/Core/Combat/CombatClock.cs` (+ `Tests/EditMode/Combat/CombatClockTests.cs`) | the fixed-tick heartbeat — port target |
| AutoBattler | `Assets/Code/Runtime/Core/Combat/CombatCoordinator.cs` | the sim driver shape |
| AutoBattler | `Assets/Code/Runtime/Core/Combat/PawnCombatController.cs` | per-unit attack cadence = `new CombatClock(1f/AttackSpeed)` |
| AutoBattler | `Assets/Code/Runtime/Core/Combat/CombatOutcome.cs` / `CombatOutcomeResolver.cs` | pure win/lose rule |
| AutoBattler | `Assets/Code/Runtime/Core/GamePhaseController.cs` / `CombatPhase.cs` / `LootPhase.cs` / `PlacementPhase.cs` | the phase FSM + `IGamePhase` |
| AutoBattler | `Assets/Code/Data/Pawns/EncounterConfig.cs` | the encounter/location SO shape |
| AutoBattler | `Assets/Code/Runtime/Core/PlayerData.cs` | run/player state container + persistence wishlist |
| AutoBattler | `Assets/Code/Runtime/Modules/Statistics/Resource.cs:54` (`Regenerate`) | clock-driven regen |
| AutoBattler | `Docs/adr/0001-range-movement-and-combat-tick.md`, `0012-core-loop-hands-off-and-itemized-enemies.md`, `0008-mana-regenerates-during-combat.md` | the sister project's design answer to this same question |
| ARPG Combat | `Assets/Code/Tools/Ticker.cs`, `Tools/StepLock.cs`, `TeppichsTools/Runtime/Time/Ticker.cs` | simpler tick primitives; `StepLock` = pause-the-sim pattern |
| ARPG Combat | `Assets/Code/Pawns/Pawn.cs`, `Tools/Spawner.cs` | per-frame regen (what to move off the frame); minimal spawner |
| BoneAndBlood | `Prototype/Assets/Core/Runtime/Combat/ModifiableAttributesBase.cs`, `DamageCalculation/DamageCalculation.cs` | derived-stat + staged-damage reference (the `see Bone&Blood` note) — not MVP |
| Ruadh2 | `Assets/Code/Runtime/Provider/CurrentRunProvider.cs` | a complete run/battle/shop loop + `ISerializable` memento seam + abstract combat resolution (`PseudoCombatSuccess`) |
| RuadhWarbands | `Assets/Code/Data/ScriptableObjects/Encounter.cs` | the minimal encounter SO |

## Sources (web)

- Game Programming Patterns — [Game Loop](https://gameprogrammingpatterns.com/game-loop.html), [Update Method](https://gameprogrammingpatterns.com/update-method.html), [State](https://gameprogrammingpatterns.com/state.html)
- [Postmortem: Loop Hero — Game Developer](https://www.gamedeveloper.com/design/postmortem-loop-hero)
- [Closing the Loop: Four Quarters on the making of Loop Hero — Destructoid](https://www.destructoid.com/closing-the-loop-four-quarters-on-the-making-of-loop-hero/)
- [Progress Quest — Wikipedia](https://en.wikipedia.org/wiki/Progress_Quest)
- [Combat — Melvor Idle wiki](https://wiki.melvoridle.com/w/Combat)
- [Mercenaries — Diablo Wiki](https://diablo2.diablowiki.net/Mercenaries)
- [Aggressive minion AI — Path of Exile dev tracker](https://devtrackers.gg/pathofexile/p/c0856471-what-minions-are-now-a-consideration-with-the-new-aggressive-ai-support)
- [Soda Dungeon — Steam](https://store.steampowered.com/app/564710/Soda_Dungeon/)
- [Hearthstone Battlegrounds explained — ONE Esports](https://www.oneesports.gg/gaming/hearthstone-battlegrounds-everything-you-need-to-know-about-blizzards-auto-battler/)
- [Time.timeScale — Unity Scripting API](https://docs.unity3d.com/ScriptReference/Time-timeScale.html)
- [Game Architecture with ScriptableObjects — Ryan Hipple, Unite Austin 2017 (sample project)](https://github.com/roboryantron/Unite2017)
