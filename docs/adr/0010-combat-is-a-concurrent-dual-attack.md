---
status: accepted, not yet implemented — a /prototype still tunes the open parts below
---

# Combat is a concurrent dual attack, one half geared for each damage type

ADR-0008 settled that an Encounter is a live sim the player watches; it left the
*content* of that sim open. The design spec's sketch was one attack per tick with
`ResourceReserveFraction` choosing physical below the threshold and magical above it,
against "a group of enemies and a cadence exchange" (Out of Scope). The grilling of
2026-09-02 replaced that.

The hero runs **two concurrent auto-attacks** on independent timers, one action
resolved per tick:

- **Strike** — physical. Flat `PhysicalDamage`, cadence `1 / AttackSpeed`, hits the
  single lowest-HP engaged enemy.
- **Cast** — magical, area. Flat `MagicalDamage` to each of the 3 highest-HP engaged
  enemies, cadence `castCost / ResourceRegeneration` in the steady state.

Both are innate — gear only moves the numbers. A physical build stacks `PhysicalDamage`
and `AttackSpeed`; a magical build stacks `MagicalDamage`, `Resource` and
`ResourceRegeneration`. The pacing asymmetry — `AttackSpeed` drives the Strike,
`ResourceRegeneration` drives the Cast — is what lets a player gear each half
independently instead of picking one with a slider.

`ResourceReserveFraction` becomes **`CastThreshold`**: hysteresis, not a mode switch.
The hero holds its Cast until resource charges up to the fraction, then Casts every
opportunity down to empty, then recharges. At the low end that is a continuous chip; at
the high end it is a silence broken by a pack-deleting burst. Strikes run throughout
either way.

An **Encounter** fields a fixed **Roster** of enemies that spawn in over the fight —
singly or in **Packs** — up to a soft **Engagement** count the player sets on a sixth
behaviour slider. Engagement is a target the sim maintains, not a ceiling: a Pack
overshoots it. The Encounter clears when the Roster is spent and the last enemy is
down. XP and coins are per kill; clearing is a silent transition to the next Encounter,
and only Recall or Death ends the Run.

The reason is itemisation legibility — the spec's whole point. One attack gated by a
slider makes "physical or magical" a slider setting; two concurrently-running attacks
with different pacing stats keep both halves of the gear space always live, and turn
Engagement into a real choice: an area build dives to feed the Cast, a single-target
build keeps Engagement low to cut incoming hits it cannot out-clear anyway.

## Consequences

- This reverses two lines in the spec's Out of Scope on purpose. **Targeting** now
  exists — but minimal and deterministic (lowest-HP for the Strike, the 3 highest-HP
  for the Cast, no RNG). A **cast rhythm** now exists — but as a resource economy, not a
  cooldown. "No spatial Field" still holds: Engagement and Packs are counts, never
  positions.
- `CalculateDamageOutput` (`BaseCharacterExtensions.cs:30`) splits. The Strike drops
  the `× (1 + AttackSpeed · 0.01)` term — a real cadence already carries `AttackSpeed`
  through frequency, so the term double-counts. The Cast takes no `AttackSpeed` term.
- Realises the `ADVANCED DAMAGE CONCEPT` note at `BaseCharacter.cs:14` — an areal
  attack validated against `Faction`. `CombatFaction` (built, unused) becomes the
  Cast's target-validation seam.
- `HeroBehaviour` swaps `ResourceReserveFraction` → `CastThreshold` (float) and gains
  `Engagement` (int): six behaviour sliders, not five.
- `LocationConfig` gains a Roster range and a spawn profile — `spawnBatch [min,max]`
  (`[1,1]` is a pure trickle, `[4,4]` a charging Pack), `spawnInterval`, `spawnJitter`.
- One enemy archetype, Strike only, every stat off `SourceLevel`. Enemies never Cast.
  Resists and penetration stay deferred — with one archetype there is nothing to vary.
- A future `CastCostReduction` stat — not the enum's commented-out `CooldownReduction`
  — is the first depth lever: it lowers `castCost`, which both quickens the Cast and
  stretches a burst.
- Reversing this once the Encounter driver (issue #20) is built means rewriting the
  target model and both cadences. Hence the ADR.

## The `/prototype` still decides

Folded in from issue #18, to run before issues #20 / #23:

- **Finite or infinite** — both Encounters-per-Run and enemy spawning inside an
  Encounter. A finite Roster is the working assumption; if continuous spawning plays
  better, the Roster and the Encounter-as-a-unit vocabulary retire.
- **Whether physical, magical and hybrid are each tunable to viable**, or the meta is a
  conscious two-way choice — a discovered lean is a finding, not a blocker.
- **Every constant** — base damages, `castCost`, the two cadences, Roster and Pack
  sizes and timing, the `CastThreshold` response curve.

The prototype's output is an amendment here and in the spec; issue #18 closes with it.
