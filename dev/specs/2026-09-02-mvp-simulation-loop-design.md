# MVP simulation loop — design

Date: 2026-09-02
Status: **Design spec.** Decisions settled by the grilling session of 2026-09-02
(`/grill-with-docs` over `dev/specs/2026-09-01-mvp-simulation-loop-research.md`, with a
`/domain-modeling` pass). Built **after** the foundational rework lands (ADR-0006);
this spec is written now so `/to-tickets` can slice it and so the `RunState` /
`EncounterResult` shapes can constrain the save format (ADR-0006 consequence).

Supersedes the open questions in the research doc §6. New vocabulary is pinned in
`CONTEXT.md` (**Session**, **Drop**, **Corpse**, sharpened **Location** / **Death** /
**Denomination**; **Strike**, **Cast**, **Engagement**, **Pack**, **Cast Threshold**
from the combat grilling). Three ADRs record the load-bearing choices: **ADR-0008** (the
Encounter is a live simulation, not a resolved outcome), **ADR-0009** (Death is
corpse-recovery, not haul-forfeit), and **ADR-0010** (combat is a concurrent dual attack
the player gears each half of).

A follow-up grilling on 2026-09-02 reworked the combat model inside the Encounter — see
*The combat model* under Implementation Decisions and **ADR-0010**. The rest of this spec
predates it; where a section still describes one attack per tick or five sliders, that
subsection and the ADR are authoritative.

---

## Problem Statement

The prototype has a grid backpack, an equipment paperdoll, a coin economy and a loot
roll, but nothing that makes any of it *matter*. Combat is a debug harness — a button
fires one hit, regen is `async void` in `Update()`, `// TODO: COMBAT TICK RATE` marks
where a real tick should be. There is no reason to pick one item over another, no
pressure on bag space, no loop. The owner wants an **idle / auto-combat ARPG loop**:
the hero is sent to a place, fights on its own by player-tuned behaviour, and comes
home with loot; the player's job is deciding *where to send it*, *how it should
behave*, *when to pull it out*, and *what to keep* — the last one a packing problem in
a finite bag. Every part of that loop has to make an `AttackSpeed` 1.1× vs 1.4× gear
choice, or a "keep the Rare belt or the room it needs", into a decision with stakes.

## Solution

A **Run**: from **Town**, the player picks a **Location**, tunes six behaviour
sliders, and **Sends** the hero. The Location runs a live fixed-timestep **Encounter**
simulation the player watches in real time — resource globes drain, the XP bar fills,
the inventory/equipment panel stays open and interactive. Enemies fall; **Loot** drops
live as **Drops** on the ground; the sim auto-picks-up whatever passes the player's
**loot filter** and fits the bag; coins auto-bank to the **Wallet**. The player retunes
sliders against live HP, or presses **Recall** — an instant teleport home that keeps
everything already picked up and deletes any Drop still on the ground.

If the hero is downed instead, the Run ends in **Death**: the hero wakes in Town at a
fraction of its health under a penalty — an XP loss, a currency fee, and the bag's
contents set aside as a **Corpse** at the Location where it fell. The Corpse is
recovered by going back in and picking the items up; a second Death before recovery
destroys it. Equipped gear is never touched.

Back in Town, HP and Resource regenerate over real time, the Stash and the Store
re-open, and the player sorts the bag, banks coins, shops, and sends the hero out
again. The whole thing is one Unity scene; the Store and Stash panels are disabled
while the hero is in the Field. A **Session** — app start to quit — is the save unit:
quitting mid-Run banks what the hero already holds, discards the rest, and resumes
`InTown` next launch.

## User Stories

### The Run lifecycle

1. As a player, I want to choose a Location from a map before I commit the hero, so that
   where the loot comes from is my decision.
2. As a player, I want to press **Send** to start a Run, so that the hero leaves Town
   for the Location I picked.
3. As a player, I want Send to be an instant teleport, so that there is no travel wait
   between deciding and playing.
4. As a player, I want to press **Recall** at any moment during a Run, so that I can end
   it the instant I judge the risk too high.
5. As a player, I want Recall to be instant and to keep everything the hero has already
   picked up, so that pulling out early is always a safe choice with a known payoff.
6. As a player, I want a Run to end automatically in Death when the hero's health is
   depleted, so that careless behaviour tuning has a real consequence.
7. As a player, I want the hero to return to Town on Death rather than the game ending,
   so that a bad Run costs me progress, not the save.
8. As a player, I want only one Run in flight at a time, so that the loop stays a single
   clear decision cycle.
9. As a player, I want the Run's two states to be exactly "in Town" and "in the Field",
   so that the interface has no in-between mode to reason about.

### Watching the Encounter

10. As a player, I want the fight to run live in real time while I watch, so that I can
    see a gear change speed the enemy globe's drain.
11. As a player, I want the hero's health and resource shown as ARPG-style globes, so
    that the sim's state is readable at a glance without a combat log.
12. As a player, I want the XP bar to fill as Encounters are cleared, so that I feel
    progress accruing during the Run, not only at the end.
13. As a player, I want the inventory and equipment panel to stay open and interactive
    during the Encounter, so that I can re-gear or sort mid-fight.
14. As a player, I want no recap or results screen, so that the globes and the bar *are*
    the readout and the loop never pauses for me to dismiss a dialog.
15. As a player, I want the hero's Strike to land on its own attack-speed cadence and
    its Cast on its own resource-fed cadence, so that `AttackSpeed` and
    `ResourceRegeneration` on gear are visible rates, not hidden numbers (ADR-0010).
16. As a player, I want enemies at a Location to hit back on their own cadence scaled by
    the Location's difficulty, so that a harder Location is visibly more dangerous.
17. As a player, I want an Encounter to end when the enemy group is down and the next to
    begin after a short beat, so that a Run is a continuous series of fights.
18. As a player, I want the fight to be deterministic given the same inputs, so that the
    behaviour I tuned produces a repeatable outcome I can learn from.

### Behaviour sliders

> **ADR-0010 revised this list.** Story 21 is now the **Cast Threshold** (a hysteresis
> knob, not a physical/magical mode switch), and a sixth slider — **Engagement**, the
> kite-vs-dive count — is added. See *The combat model* under Implementation Decisions.
> The issue-#18 `/prototype` found Cast Threshold barely moves outcomes on a continuous
> spawn — it stays a slider but as a feel knob, not a build choice (see *prototype
> findings*).

19. As a player, I want a **retreat-at-HP** slider, so that the hero auto-Recalls when
    its health fraction drops below my threshold.
20. As a player, I want a **recall-when-bag-full** slider, so that the hero auto-Recalls
    once the bag reaches my fill threshold rather than fighting on with nowhere to put
    loot.
21. As a player, I want a **cast-threshold** slider, so that the hero holds its Cast
    until resource charges to my mark and then spends it down — continuous, evenly-spaced
    Casts at the low end, clumped Casts at the high end — while its Strike keeps swinging
    throughout (ADR-0010; the `/prototype` found this changes the *rhythm* of the Cast,
    not the Encounter's outcome).
22. As a player, I want a **loot-filter** slider that sets a minimum rarity, so that the
    hero only picks up drops at or above the quality I care about (Common = everything,
    Magic = skip Common, Rare = skip Common and Magic, Unique = only Unique).
23. As a player, I want the loot filter to apply to coin drops too, reading each
    denomination's rarity (iron = Common … gold = Unique), so that one setting governs
    everything the hero stoops for.
24. As a player, I want a **sim-speed** slider with a logarithmic response up to roughly
    8×, so that I can fast-forward a grind and the top of the slider moves much faster
    than the middle.
25. As a player, I want sim-speed to scale only the combat simulation, so that fast-
    forward does not speed up my UI animations or panel tweens.
26. As a player, I want no pause on the sim-speed slider, so that a Run is a commitment
    to real (if accelerated) time.
27. As a player, I want slider changes to take effect immediately, so that I can react
    to a fight going wrong by retuning on the spot.
28. As a player, I want my slider settings to persist across Runs and Sessions, so that
    I set my play style once.
29. As a player, I want the sliders read only when I change them, not sampled every
    tick, so that the sim stays cheap and the behaviour config is a plain value I own.

### Loot, Drops and the bag

30. As a player, I want loot to drop live from defeated enemies, not as one bundle at
    the end, so that a longer Run visibly earns more.
31. As a player, I want dropped items to lie on the ground as Drops until picked up, so
    that pickup is a thing that happens, not an instant vacuum.
32. As a player, I want the sim to auto-pick-up any Drop that passes my loot filter and
    fits the bag, so that I am not clicking items during a fight.
33. As a player, I want coins to auto-bank to my Wallet as they drop (subject to the
    filter), so that currency is never a packing problem.
34. As a player, I want a Drop that passes the filter but does not fit the bag to stay
    on the ground, so that a full bag is a real constraint I feel during the Run.
35. As a player, I want to see what is on the ground, so that I can decide whether to
    make room or Recall.
36. As a player, I want every Drop still on the ground when the Run ends to be gone —
    on Recall and on Death alike — so that the ground is never a second stash.
37. As a player, I want an upgrade that drops into a full bag to force a real choice —
    drop something to make room, or leave the upgrade — so that bag space has a price.
38. As a player, I want XP and banked coins to settle **per kill**, so that a Run cut
    short by Recall keeps everything earned up to the last enemy that fell (ADR-0010).

### Death and the Corpse

39. As a player, I want Death to cost me a chunk of my progress toward the next level,
    so that a wipe is a felt setback.
40. As a player, I want Death to draw a fee from the currency I banked during the Run,
    so that a greedy Run that ends badly loses some of its take.
41. As a player, I want my equipped gear to be completely untouched by Death, so that a
    wipe never undresses the hero.
42. As a player, I want the bag's contents on Death to be set aside as a Corpse at the
    Location where I fell, rather than destroyed, so that there is a route back to them.
43. As a player, I want to recover the Corpse by re-entering that Location and picking
    the items up off the ground, so that the corpse run is itself a Run with its own
    risk.
44. As a player, I want there to be only one Corpse at a time, so that the mechanic
    stays simple to track.
45. As a player, I want a second Death before recovery to destroy the unclaimed Corpse,
    so that pushing recklessly after a wipe can cost me everything.
46. As a player, I want the Corpse to survive quitting the app, so that I do not lose it
    to closing the game.
47. As a player, I want a Corpse stranded at the harder Location to be recoverable only
    by surviving there again, so that the difficulty ladder has teeth.
48. As a player, I want the hero to revive in Town at a low fraction of its health after
    Death, so that I cannot immediately re-send it without waiting for regen.

### Town

49. As a player, I want Town to be a Run *state*, not a place on the map, so that "go to
    Town" and "Recall" are the same action.
50. As a player, I want HP and Resource to regenerate over real time while in Town, so
    that recovery is a wait, not a button.
51. As a player, I want Town regen to cost no currency, so that the only price of a bad
    Run is the Death penalty and the time to heal.
52. As a player, I want the Stash and the Store to be available only in Town, so that
    Town is where deliberate inventory management happens.
53. As a player, I want the inventory, equipment and character panels available in both
    Town and the Field, so that I can always see and change what the hero is carrying
    and wearing.
54. As a player, I want the map and Recall available in the Field and Send available in
    Town, so that the controls match the state I am in.
55. As a player, I want to sort the bag, consolidate coins, and shop between Runs, so
    that Town is a full management stop.
56. As a player, I want selling and stashing to be Town-only for now, so that the Field
    stays about fighting and picking up.

### Locations and the map

57. As a player, I want Town plus two Field Locations, so that "where to send the hero"
    is a genuine choice from the first Run.
58. As a player, I want both Field Locations open from the start, so that the MVP has no
    unlock gate to grind past.
59. As a player, I want each Location to have a fixed difficulty that never scales to my
    hero, so that a geared hero outgrows the easy Location and the hard one is a wall
    until I am ready.
60. As a player, I want the harder Location to roll better loot and give more XP, so
    that the risk of going there is worth taking.
61. As a player, I want the map shown as a set of Location toggles with one selected,
    plus a Send button, so that picking a destination uses controls I already know from
    the rest of the UI.
62. As a player, I want each Location to supply its own source level and loot table to
    the roll, so that what drops is authored per place.

### The Session and persistence

63. As a player, I want the app to resume in Town on launch, so that I always start a
    Session from a clean, safe state.
64. As a player, I want the hero's level, XP, the four containers, the Wallet balance,
    my slider settings and any Corpse to persist across Sessions, so that a Session is
    where progress lives.
65. As a player, I want quitting mid-Run to bank what the hero already picked up and
    discard the rest, so that alt-F4 is not an exploit and not a catastrophe.
66. As a player, I want the Run itself — which Location, the current Encounter, the sim
    clock — to never be saved, so that there is no half-fight to restore into.
67. As a player, I want the save to only ever be written while I am in Town, so that the
    save format never has to describe a Run in progress.

## Implementation Decisions

### Sequencing (ADR-0006)

Nothing here is built until the foundational rework's three seams have landed — item
split (Phase 1, done), the transaction (Phase 2, issues #9–#13 + #15), and the wallet
(Phase 3, issue #14). The `Encounter` module sits *above* `InventorySystem.Containers`
and the wallet module in the assembly stack (ADR-0007) and needs both to exist. Writing
the spec now is deliberate — ADR-0006 says the `RunState` and `EncounterResult` shapes
must be sketched before the save system, and they are, below.

### One new module: `InventorySystem.Encounter`

A new Unity-free assembly, one layer above `InventorySystem.Containers` and the wallet
module, below `InventorySystem.Runtime` (ADR-0007). Everything in it is constructible in
a test with fakes — the same rule `InventorySystem.Items` follows. It contains:

- **`CombatClock`** — ported verbatim from the sister project AutoBattler
  (`Assets/Code/Runtime/Core/Combat/CombatClock.cs`) with its test file. An accumulator
  that banks real delta time and fires a tick event 0..N times per `Advance(deltaTime)`,
  with a spiral-of-death clamp and tick-quantised elapsed time. It deliberately does
  **not** use the shared `Utility` submodule's `Timer` / `TimerTicker`, which is one
  global player-loop driver that also ticks UI timers — routing combat through it would
  make the sim-speed slider drag every panel tween along with it (ADR-0008). The
  submodule's `Stopwatch` is fine for the Town regeneration timer, which needs neither a
  clamp nor speed-scaling.
- **The Encounter simulation** — owns the tick loop, the hero's two attack cadences
  (Strike `1 / AttackSpeed`, Cast `castCost / ResourceRegeneration`) and their target
  selection, the enemy Strike cadence, the Roster/Pack spawn schedule against the
  `Engagement` target, the per-tick resource regeneration call, the end-of-Encounter
  check (hero down, or the Roster spent and cleared), the short beat before the next
  Encounter, and the evaluation of the `HeroBehaviour` triggers. It never references
  `BaseCharacter`. See *The combat model* (ADR-0010).
- **`ICombatant`** — the only new interface. The sim reads `HealthFraction` /
  `ResourceFraction` / `AttackInterval` / `IsDown` and calls a strike method on cadence.
  The hero's implementation is a thin adapter in `Runtime` that forwards strikes to the
  existing `BaseCharacter.DealDamageTo` / `ReceiveDamageFrom` path and reads and writes
  the live `CharacterResource`s so the globes reflect sim state. The enemy is a pure
  parametric archetype the `Encounter` module owns, its stats derived from the
  Location's source level — no per-monster assets. Whether the existing
  `DealDamageTo(BaseCharacter, …)` signature is widened to `ICombatant` or the hero
  adapter bridges by holding both sides as `BaseCharacter` is a first-ticket detail; the
  seam the sim sees is `ICombatant` either way.
- **The `RunState` machine** — two states, `InTown` and `InField`. Transitions:
  `Send(location): InTown → InField`, `Recall(): InField → InTown`,
  `HandleDeath(): InField → InTown`. No `Traveling` state — Send and Recall are instant.
  No `GameOver`. `RunState` owns the running Run totals (XP gained this Run, currency
  banked this Run, Encounters cleared, elapsed) that the Death penalty is computed
  against, and the reference to the current `Location`.
- **`EncounterResult` / `RunOutcome`** — the frozen readout emitted when a Run ends.
  Shape (a value type; encodes the decision more precisely than prose):

  ```
  enum RunOutcome { Recalled, Died }

  EncounterResult
  {
    RunOutcome  Outcome
    int         XpGained          // applied live per kill; summed here
    Currency    CurrencyBanked    // banked live to the Wallet; summed here
    int         XpLost            // Died only — progress toward next level, forfeited
    Currency    CurrencyFee       // Died only — withdrawn from the Wallet
    int         EnemiesDefeated
    int         EncountersCleared
    float       Duration          // = CombatClock.ElapsedTime, summed
  }
  ```

  Most of what a Run earns is applied *live* (XP per kill via `LocalPlayer`, coins to
  the Wallet as they drop, items into the bag on pickup), so `EncounterResult` is a
  summary, not a delivery mechanism. Its one delivery job is the Death case: `XpLost`,
  `CurrencyFee` and the Corpse hand-off.

- **The Corpse rules** — pure and testable: on Death the bag's contents snapshot into a
  single Corpse tagged with the `Location`; a Corpse already present is replaced (the
  old one's items are gone); the Corpse is recovered by re-entering its Location, which
  lays its items out as Drops to be picked back up; the Corpse is the one piece of
  mid-loop state the Session save carries.
- **`HeroBehaviour`** — a plain serializable value with six fields, held in the Session
  save, written by the sliders on their change event and read by the sim from the value
  (never polled):

  ```
  HeroBehaviour
  {
    float      RetreatHealthFraction   // auto-Recall below this HP fraction
    float      RecallBagFillFraction   // auto-Recall at or above this bag fill
    float      CastThreshold           // charge Resource to this fraction, then Cast to empty (ADR-0010)
    ItemRarity LootFilterMinimum       // lowest rarity the hero will pick up
    float      SimSpeed                // 1..~8, applied as clock.Advance(dt * SimSpeed)
    int        Engagement              // enemies to keep engaged at once (ADR-0010)
  }
  ```

### The combat model (ADR-0010)

A follow-up grilling (2026-09-02) reworked what happens *inside* an Encounter. Full
rationale is in **ADR-0010**, and its *Prototype outcome* section records what the
issue-#18 `/prototype` settled (summarised below). The shape:

- **Two concurrent attacks**, independent timers, one action resolved per tick. The
  **Strike** is physical — flat `PhysicalDamage` on a `1 / AttackSpeed` cadence at the
  lowest-HP enemy. The **Cast** is magical and area — flat `MagicalDamage` to each of
  the 3 highest-HP enemies, cadence `castCost / ResourceRegeneration`. Both are innate;
  gear only scales them. A physical build stacks `PhysicalDamage` + `AttackSpeed`, a
  magical build stacks `MagicalDamage` + `Resource` + `ResourceRegeneration`.
- **`CastThreshold`** replaces `ResourceReserveFraction` — a hysteresis knob: hold the
  Cast until resource charges to the fraction, then Cast down to empty, then recharge.
  Continuous chip of Casts at the low end, clumped Casts at the high end. Strikes run
  throughout. *(The prototype found this is a feel knob, not a power knob — see below.)*
- **`Engagement`** is a sixth behaviour slider — the count of enemies the Encounter
  keeps on the hero. A soft target the fight refills toward, not a ceiling: a **Pack**
  (a `spawnBatch` of enemies entering together) overshoots it.
- An Encounter fields enemies from a fixed **Roster** (a `[min,max]` count on
  `LocationConfig`) that spawn in singly or in Packs per a spawn profile (`spawnBatch`,
  `spawnInterval`, `spawnJitter`). It clears when the Roster is spent and the last enemy
  is down. **XP and coins are per kill** — clearing is a silent transition, and only
  Recall or Death ends the Run. *(The `/prototype` confirmed the fixed Roster: continuous
  spawning erases the Encounter as a unit and lowers throughput — issue #18, ADR-0010
  Prototype outcome.)*
- `CalculateDamageOutput` splits: the Strike drops its `× (1 + AttackSpeed · 0.01)`
  term (double-counts against a real cadence); the Cast takes no `AttackSpeed` term.
  The enemy archetype is Strike-only, every stat off `SourceLevel`. A future
  `CastCostReduction` stat is the first depth lever, deferred.

### The combat model — prototype findings (issue #18)

The `/prototype` (a single-file logic sim, `dev/prototypes/2026-09-03-combat-cluster/`,
throwaway branch `prototype/combat-cluster`; 150–200 seeded Runs per case) resolved
ADR-0010's three open questions. Full detail in that folder's `FINDINGS.md` and in
ADR-0010's *Prototype outcome*; the load-bearing results:

- **Finite vs endless — settled both scales.** The Encounter keeps a **fixed Roster**;
  a Run is an **endless** series of Encounters at a fixed source level with **no
  encounter cap and no restart-rescale**. A realistic Run always ends on a Recall
  trigger (bag-full ≈ 5 Encounters on the easy Location, retreat-HP ≈ 1–2 on the hard
  one) or Death — a `finiteEncounters` cap fires in <1 % of Runs and is dead config. The
  fixed-difficulty ladder is carried entirely by **equipped gear**: a build crosses from
  "worn down in ~50 s" to "out-clears the spawn indefinitely" over roughly a 1.25–1.5×
  gear-power swing. This moves the *Finite vs endless* item out of Out of Scope.
- **The bag is the whole "return to Town" pressure.** With fixed difficulty and no bag
  limit, a geared build farms a Location forever. Bag capacity and the loot-filter
  default are loop-load-bearing, and the harder Location should stay attritional even
  for a geared hero.
- **Physical, magical and hybrid are each viable** — each sustains the easy Location
  indefinitely and clears a gear-proportional slice of the hard one. It is *not* a
  two-way choice and hybrid is not dominated. Lean (a finding, not a blocker): magical
  is the area-farm build (≈ 1.4× the XP/min) with the thinnest raw survival; physical
  lasts longest raw and opens hard Locations at lower gear; hybrid trades the extremes
  for no soft spot. Physical wants Engagement low (kite); magical wants Engagement 3–4
  (feed the Cast); a Pack overshoots any Engagement.
- **`CastThreshold` is a texture knob, not a power knob** — sweeping it 0.05 → 0.95
  moved throughput < 0.3 %. A saved burst does not pay for itself against a continuous
  spawn. The slider stays (six sliders), reframed as continuous-vs-clumped Casts; it
  gains real weight only when pre-chargeable elites/bosses exist. Slider story 21 and
  `HeroBehaviour.CastThreshold` take the softened wording.
- **Constant starting points** (the prototype file is the tuning surface): `castCost`
  16, `castTargets` 3, `castCadence` 0.35 s, `tick` 0.1 s, `beat` 1 s; enemy archetype
  `stat = base + perLevel · SourceLevel^exp` with exponents barely above linear (Health
  ≈ `17 + 19·S^1.11`, Damage ≈ `0.9 + 0.8·S`, Armor ≈ `0.65·S %`, AttackSpeed ≈ 0.8
  flat); easy Location `Roster [8,8] / spawnBatch [1,1] / interval 2.4 s`, hard
  `Roster [12,12] / spawnBatch [2,4] / interval 3.6 s`; `xpPerKill` authored per
  Location (≈ 16 easy … 30 hard).

### Loot flow

- When an enemy falls, the `Encounter` module decides a drop count (from the archetype
  and the `IncreasedItemQuantity` multiplier, per the `ItemGenerator.RollLoot`
  docstring — the count is the caller's job) and calls the existing `ItemGenerator`
  with a `RollContext` built from the Location: `Table` = the Location's loot table,
  `SourceLevel` = the Location's source level, `MagicFind` = the hero's
  `IncreasedItemRarity`. Rolled `ItemInstance`s become Drops.
- Currency drops roll from the existing `CurrencyDropTable` and become coin **Piles** on
  the ground, then auto-bank to the Wallet if the denomination's rarity passes the loot
  filter.
- Each Drop is tested against `HeroBehaviour.LootFilterMinimum`; a pass is then offered
  to the bag (the container's own Tetris-fit placement, from Phase 2). A Drop that
  passes the filter but does not fit stays on the ground. A Drop that fails the filter
  stays on the ground. All ground Drops are discarded when the Run ends.
- The pure sim owns the *roll* and the *filter decision*; the *placement* into the bag
  is delegated to the container (Phase 2 code with its own tests) — the sim asks "did it
  fit" and records the answer.

### `LocationConfig` (a ScriptableObject adapter, in `Runtime` or `Data`)

Authored per Location: a stable serialized id (same rule as `ItemDefinition.Id` — not an
asset GUID), a display name, a fixed source level, a loot-table reference, an
`xpPerKill`, and (ADR-0010) the fixed **Roster** range `[min,max]` an Encounter draws
from and the spawn profile — `spawnBatch [min,max]` (`[1,1]` is a pure trickle, `[4,4]` a
charging Pack), `spawnInterval`, `spawnJitter`. It carries **no** encounter-count or
completion field (the issue-#18 `/prototype` killed the finite-Run mode) and **no**
per-Location enemy curves — the enemy archetype is one shared constant set in the
`Encounter` module that reads only `SourceLevel`. Two assets for the MVP: an easy
Location (`SourceLevel 2, Roster [8,8], spawnBatch [1,1], interval 2.4`) and a harder one
(`SourceLevel 5, Roster [12,12], spawnBatch [2,4], interval 3.6`, richer table, more XP).
Town is **not** a `LocationConfig` — it is a `RunState`, and "go to Town" on the map is
`Recall()`.

### Adapters (thin, in `Runtime` / `GUI`, smoke-tested in the editor)

- The sim `MonoBehaviour` driver — `Update()` calls `clock.Advance(Time.deltaTime *
  behaviour.SimSpeed)`; it does not touch `Time.timeScale`.
- The hero `ICombatant` adapter over `BaseCharacter` / `LocalPlayer`.
- Replacing `BaseCharacter`'s `async void` regen in `Update()` with a
  `Regenerate(deltaSeconds)` method a caller drives — the Encounter sim during a fight,
  a trivial `Stopwatch`-driven Town driver otherwise. This retires
  `// TODO: COMBAT TICK RATE`.
- Applying `EncounterResult` and per-kill XP to `LocalPlayer`; banking coin Piles to
  the Wallet; snapshotting the bag into the Corpse store on Death; laying a recovered
  Corpse out as Drops.
- The map panel (a radio group of Location toggles + Send / Recall buttons), the six
  behaviour sliders wired to `HeroBehaviour` on their change event, and panel-visibility
  wiring that disables the Store and Stash toggles while `InField`.
- The Corpse's line in the Session save (Location id + `ItemInstance` DTOs, reusing the
  Phase 1 instance-to-POCO round trip).

### Persistence constraints (ADR-0006, for the future save system)

- The save is written **only** while `InTown`. The Run — Location, current Encounter,
  clock, ground Drops — is never serialized; the app forces `InTown` on load.
- The Session save covers: hero level + XP (or the growth-modifier list), the four
  containers as the Phase 1 DTO list, the Wallet balance, the `HeroBehaviour` value, the
  selected Location, and the Corpse (Location id + instance DTOs).
- Quitting mid-Run is equivalent to an implicit Recall for what the hero already holds:
  the bag and Wallet are saved as they stand; ground Drops are lost.
- The new Run/Encounter/map state lives on its own provider(s), not on the existing
  `InventoryProvider` god object.

## Testing Decisions

A good test here asserts **externally observable behaviour** of the pure `Encounter`
module — given these combatants, this clock delta sequence, this behaviour config, this
fake roll source: the hero strikes on cadence, the Encounter ends when a side is down,
XP and loot come out at these amounts, the Death penalty and Corpse are these. It never
reaches for a private field or a tick counter.

- **The seam is the `InventorySystem.Encounter` module boundary.** One new test
  assembly, `InventorySystem.Encounter.Tests`, mirroring `InventorySystem.Items.Tests`
  (`Assets/Scripts/Tests/EditMode/Encounter/`). It fakes `ICombatant` (both sides),
  fakes `IRollSource` (the determinism seam already used by `ItemGeneratorTests` —
  `RollSources.cs`), uses a real `new()`-able `CharacterInventory` / `Wallet` from the
  post-rework code, and feeds `CombatClock` deltas by hand.
- **`CombatClock`** ports with its own 15 tests from AutoBattler — sub-interval
  accumulation, frame-rate independence, the spiral-of-death clamp, elapsed-time
  semantics.
- **Modules under test:** `CombatClock`; the Encounter sim (the two hero cadences and
  their target selection, the enemy cadence, the Roster/Pack spawn schedule against
  `Engagement`, per-tick regen call, end detection, next-Encounter beat); the `RunState`
  FSM (transitions, running totals, `Recall` vs `HandleDeath` outcomes); the Corpse
  rules (single instance, per-Location tag, replace-on-second-Death, lay-out-on-recovery);
  `HeroBehaviour` triggers (retreat fires at the HP fraction, recall fires at the
  bag-fill fraction, `CastThreshold` gates a casting run by resource hysteresis,
  `Engagement` caps the spawn refill, the loot filter admits and rejects by `ItemRarity`
  including the coin-denomination mapping); the loot count → `RollContext` →
  `ItemGenerator` wiring.
- **Prior art:** `ItemGeneratorTests` (a pure generator driven by a fake `IRollSource`,
  with `InMemoryItemCatalog` / `FakeLootTable` / `FakeItemDefinition` helpers);
  `ProbabilityTableSampleTests` (roll-as-parameter determinism); `ContainerCoreTests`
  (container behaviour from an EditMode test); AutoBattler's `CombatClockTests` and its
  `CombatOutcomeResolver` tests (a pure win/lose rule).
- **The adapters are not unit-tested.** The sim `MonoBehaviour`, the `BaseCharacter`
  `ICombatant` bridge, the `LocationConfig` assets, the map panel, the sliders, panel
  visibility, the Town regen driver and the Corpse-to-save wiring are verified by a
  human in the editor at the phase gate — the same bar the foundational rework phases
  use (a `Test Runner ▸ EditMode ▸ Run All` plus a play-mode smoke pass).

## Out of Scope

- **Anything built before the foundational rework's three seams land** (ADR-0006).
- **A recap or results screen** — ADR-0008; the globes and the XP bar are the readout.
- **Combat depth** — named skills, cooldowns, crit, status effects, resistances and
  penetration, weapon classes. The **Strike** / **Cast** split *is* the skill system for
  the MVP (ADR-0010). Targeting exists but is minimal and deterministic (lowest-HP /
  3-highest-HP); the Cast's rhythm is a resource economy, not a cooldown. This spec is
  about itemization, not abilities.
- **A spatial Field** — no map to walk, no enemy positions, no movement, no aggro
  radius. `Engagement` and `Packs` are enemy *counts*, never positions; an Encounter is
  a group of enemies and a cadence exchange with a spawn schedule (ADR-0010).
- **Enemy variety** — one parametric archetype scaled by source level. No per-monster
  assets, no bosses.
- **Finite vs endless — at two scales.** *Resolved* by the issue-#18 `/prototype` — moved
  up to *The combat model — prototype findings*. Fixed Roster per Encounter; endless
  Encounters at fixed difficulty; no encounter cap, no restart-rescale.
- **Progressive Location unlocks** — both Field Locations are open from the start. The
  `LocationConfig` shape leaves room to add an unlock gate later.
- **A third+ Location, a node graph, or a generated map.**
- **The save system itself** — designed here as constraints, built in a later phase
  (ADR-0006). The MVP can run a Session in memory; the persistence constraints exist so
  the save format is not retrofitted.
- **Selling or stashing from the Field**, a field merchant, mid-Run saves.
- **Derived stats from attributes** (`LocalPlayer`'s `see Bone&Blood` note).
- **Retiring the `InventoryProvider` god object** — the rework's own later tier. This
  spec must not add to it, but does not fix it.
- **Exact balance numbers** — the Death XP-loss and currency-fee percentages, the revive
  health fraction, drop counts, slider ranges and the sim-speed curve remain unfixed. The
  combat constants (base Strike/Cast damages, `castCost`, the two cadences, `tick`, the
  enemy `SourceLevel` curves, Roster and Pack sizes and timing) now have `/prototype`
  starting points — recorded in *The combat model — prototype findings* and ADR-0010,
  with `dev/prototypes/2026-09-03-combat-cluster/` as the live tuning surface — but they
  are still starting points, not frozen.

## Further Notes

- **Divergence from AutoBattler is deliberate.** The sister project whose assembly graph
  this repo mirrors (ADR-0007) worked the same "who plays the fight?" question in its
  ADR-0012 and chose hands-off combat with a full recap, explicitly rejecting mid-combat
  agency. InventoryTetris goes the other way — a live sim the player retunes and Recalls
  against — because the point is itemization legibility, and only a live sub-second sim
  shows a gear choice changing the fight. Aligning the assembly graph is not aligning the
  design (ADR-0008).
- **Determinism is free.** `CombatClock.Advance(dt)` and `ProbabilityTable.Sample(roll)`
  are the same shape — the caller supplies the non-deterministic input (ADR-0005). Route
  every sim roll through the injected `IRollSource`.
- **The Corpse is harsh for an MVP by the owner's own assessment** (ADR-0009) — accepted
  now, revisitable once the loop is actually played.
- **The combat model was reworked after this spec first landed.** The 2026-09-02 combat
  grilling produced ADR-0010, the `CONTEXT.md` **## Combat** section (Strike, Cast,
  Engagement, Pack, Cast Threshold), the sharpened **Encounter** entry, and *The combat
  model* subsection above. Sections written before it that still say "one attack per
  tick" or "five sliders" are superseded there. The issue-#18 `/prototype` (2026-09-03)
  then resolved that model's three open questions — *The combat model — prototype
  findings* and ADR-0010's *Prototype outcome* are authoritative on finite-vs-endless,
  build viability and the combat constants.
- The research doc (`dev/specs/2026-09-01-mvp-simulation-loop-research.md`) remains the
  source for the sibling-project prior art, the external genre references, and the full
  options analysis behind each decision above.
