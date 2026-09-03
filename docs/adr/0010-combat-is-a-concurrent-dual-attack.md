---
status: accepted, not yet implemented — the /prototype (issue #18) has run; see "Prototype outcome"
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
opportunity down to empty, then recharges. At the low end that is a continuous chip of
Casts; at the high end the Casts arrive clumped. Strikes run throughout either way.
*(The prototype found this is a texture knob, not a power one — see "Prototype
outcome".)*

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

## Prototype outcome (issue #18)

The `/prototype` — a single-file logic sim of the combat cluster
(`dev/prototypes/2026-09-03-combat-cluster/`, throwaway branch
`prototype/combat-cluster`; 150–200 seeded Runs per case) — resolved the three open
questions.

### Finite vs endless — decided both ways

- **Encounter scale: the fixed Roster stays.** With continuous spawning the Encounter
  never clears — no beat, no "next", and throughput is *lower* (no clear→reset rhythm).
  The Roster `[min,max]` is the only thing that makes an Encounter a unit. It clears
  when the Roster is spent and the last enemy falls.
- **Run scale: endless Encounters, no cap, no rescale.** A realistic Run always ends on
  a Recall trigger (bag-full ≈ 5 Encounters on the easy Location, retreat-HP ≈ 1–2 on
  the hard one) or Death — never on "the Location is finished". A `finiteEncounters`
  cap fired in <1 % of Runs; the bag or the HP line bounds the Run first every time, so
  a finite-encounter Run mode is dead config and Session state for nothing. There is no
  "restart" to rescale: a Run ends and the Location is still there at the same source
  level. The fixed-difficulty ladder (`CONTEXT.md`, *Location*) is carried entirely by
  **equipped gear** — in the sim a build crosses from "worn down in ~50 s" to
  "out-clears the spawn indefinitely" across roughly a 1.25–1.5× swing in gear power.
  A rescale rule would fight that mechanic.
- **Caveat the sim surfaced:** with fixed difficulty and no bag limit, an over-geared
  build farms a Location forever. The **bag is the whole "return to Town" pressure** —
  bag capacity and the loot-filter default are loop-load-bearing, and the harder
  Location must stay attritional even for a geared hero or it is pure idle.

### Physical / magical / hybrid — all three viable, not a two-way choice

Each sustains the easy Location indefinitely and clears a gear-proportional slice of the
hard one. The lean (a finding, not a blocker): **magical ≈ 1.4× the XP/min** (area farm)
but the thinnest raw survival; **physical** lasts longest raw and opens hard Locations
at lower gear; **hybrid** sits between with no dominance and no soft spot. The
Engagement asymmetry the ADR wanted holds — physical wants Engagement low (kite),
magical wants Engagement 3–4 (feed the Cast), and a Pack (`spawnBatch [4,4]`) overshoots
any Engagement setting.

### CastThreshold is a texture knob, not a power knob — soften the framing

Sweeping the threshold 0.05 → 0.95 moved throughput by under 0.3 %. The "silence broken
by a pack-deleting burst" does not pay for itself against a *continuous* spawn:
collapsing a Pack two seconds early just pulls the next Pack two seconds closer. The
slider stays (keeping the count at six), but it is a feel knob — continuous vs clumped
Casts — until later content adds a pre-chargeable target (an elite or boss with a health
bar worth banking a burst for), which is the kind of depth `CastCostReduction` and
friends are already deferred to. `HeroBehaviour.CastThreshold` and the spec's slider
story 21 take the softened wording; the mechanic (hold below the fraction, burn to empty
above it) is unchanged, and the Encounter driver's tests assert the mechanic, not a
balance outcome.

### Constants (starting points — the prototype file is the tuning surface)

- Cast: `castCost` **16** flat, `castTargets` **3**, `castCadence` **0.35 s** (the burst
  ceiling — must stay well under the emergent steady cadence `castCost /
  ResourceRegeneration` or there is no burst headroom at all).
- Clock: `tick` **0.1 s** (10 Hz), inter-Encounter `beat` **1 s**.
- Enemy archetype, `stat = base + perLevel · SourceLevel^exp`, Strike only, one shared
  set for the whole MVP: Health `≈ 17 + 19·S^1.11`, Damage (pre-mitigation) `≈ 0.9 +
  0.8·S`, Armor `≈ 0.65·S %`, AttackSpeed `≈ 0.8` flat. Keep the exponents barely above
  linear — steeper makes the hard Location un-openable or a one-shot wall; let the
  source-level gap (≈ S2 vs S5) carry the difference.
- Spawn, authored per Location: easy `Roster [8,8], spawnBatch [1,1], interval 2.4 s`;
  hard `Roster [12,12], spawnBatch [2,4], interval 3.6 s`. `xpPerKill` authored per
  Location (≈ 16 easy … 30 hard), not a curve.
- Hero build budget, one geared level-N hero split three ways, is tabulated in the
  prototype's `FINDINGS.md`.

### Downstream

- **`LocationConfig` (#25):** `Id`, `DisplayName`, `SourceLevel`, `LootTableRef`,
  `XpPerKill`, `Roster [min,max]`, `SpawnBatch [min,max]`, `SpawnInterval`,
  `SpawnJitter`. **No** finite-encounter / completion field, **no** per-Location enemy
  curves (the archetype is a module constant reading `SourceLevel`).
- **Encounter driver (#20):** build only the fixed-Roster schedule; the Encounter never
  ends the Run; one Strike-only archetype from the shared curve; `tick`/`beat`/
  `castCadence` are module constants, not config.
