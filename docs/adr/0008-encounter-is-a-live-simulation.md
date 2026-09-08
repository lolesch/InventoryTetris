---
status: accepted, not yet implemented
---

# The Encounter is a live simulation the player watches, not a resolved outcome

The MVP simulation loop needs a combat model. The sister project AutoBattler — whose
assembly graph this repo mirrors (ADR-0007) — worked nearly the same question in its
ADR-0012 and chose combat that is **hands-off during resolution** with a **complete
post-combat recap**, explicitly rejecting both "two combat modes" and "auto-combat with
one or two command interventions."

InventoryTetris goes the other way. An **Encounter** runs as a live fixed-timestep
simulation the player watches in real time — resource globes, the XP bar, the open
inventory/equipment panel — and reacts to by retuning behaviour sliders or pressing
**Recall** (an instant teleport) against live HP. There is no recap screen; the globes
draining *are* the readout, and **Loot** drops live as enemies fall.

The reason is itemization. The project's point is that an `AttackSpeed` 1.1x vs 1.4x
gear choice visibly changes how fast the enemy globe drains — only a live sub-second
sim shows that. Resolve-on-arrival collapses the loop to "click Send, click Recall" and
removes the retreat-vs-push tension that makes inventory management matter. Aligning the
*assembly graph* with AutoBattler does not mean aligning the *design*: the slider-driven
agency model here (closer to Melvor / Loop Hero) is a third option ADR-0012 did not
weigh.

## Consequences

- The sim runs on a dedicated `CombatClock` — ported from AutoBattler
  (`Assets/Code/Runtime/Core/Combat/CombatClock.cs`: an accumulator firing 0..N ticks
  per `Advance(dt)`, a spiral-of-death clamp, pure and unit-tested) — **not** the shared
  `Utility` submodule's `Timer` / `TimerTicker`. That submodule is one global
  player-loop driver ticking UI timers off `Time.deltaTime`; routing combat through it
  would make the sim-speed slider (`clock.Advance(dt * simSpeed)`) drag DOTween panel
  tweens and every other UI timer along with it. AutoBattler's own `CombatClock`
  documents the same reasoning. The submodule `Stopwatch` still suits the Town
  regeneration timer, which needs neither a clamp nor speed-scaling.
- Live combat is real-time UI work — globes, health bars, a running `MonoBehaviour` sim
  driver — that a resolve-on-arrival model would not need. Reversing this later discards
  it.
- Determinism for tests is free: `CombatClock.Advance(dt)` and
  `ProbabilityTable.Sample(roll)` are the same shape — the caller supplies the
  non-deterministic input (ADR-0005).
- Not built until the foundational rework lands — the `InventorySystem.Simulation` module
  (the spec renamed it from `InventorySystem.Encounter`; **Encounter** stays a domain
  term for the pressure-wave unit) needs `Items` / `RollContext` from rework Phase 1
  (ADR-0006).
