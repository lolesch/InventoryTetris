---
status: accepted, not yet implemented — the /prototype (issue #18) has run twice; see "Prototype outcome" (pass 1) and "Second amendment" (pass 2)
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
down. Loot Drops and coin Piles shed **per kill**; **XP settles on the clear**, summed
over the Encounter's Roster — the boundary needs a player-visible consequence, and a Run
driven off mid-Encounter forfeits that Encounter's accrued XP (issue #18 pass 2, see
*Second amendment*). Clearing is otherwise a silent transition to the next Encounter,
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
- `LocationConfig` gains, per Location, which of the two archetypes is **Packed** (the
  other trickles in singly), a `[min,max]` Roster count for **each** archetype, a
  `packBatch [min,max]` Pack size, a `packedSpawnWeight`, `spawnInterval` and
  `spawnJitter` (issue #18 pass 2 — pass 1's single `Roster` + `spawnBatch` is
  superseded). No `xpPerKill` field: XP is per-archetype and settles on the clear.
- **Two** enemy archetypes, both Strike only, every stat off `SourceLevel` — **Brute**
  (bulky, slow, armored, low XP) and **Skirmisher** (fragile, fast, high XP). Both exist
  at every Location; the Location chooses which one is Packed. Enemies never Cast.
  Resists and penetration stay deferred. *(Pass 2 reversed pass 1's single-archetype cut
  — rationale in* Second amendment*.)*
- A future `CastCostReduction` stat — not the enum's commented-out `CooldownReduction`
  — is the first depth lever: it lowers `castCost`, which both quickens the Cast and
  stretches a burst.
- Reversing this once the Encounter driver (issue #20) is built means rewriting the
  target model and both cadences. Hence the ADR.

## Prototype outcome — pass 1 (issue #18)

> **Pass 2 (the *Second amendment* below) revised the enemy model, the XP timing and the
> constants.** Where this section and *Second amendment* conflict — the enemy archetype
> (one → two), `xpPerKill` (gone), the `LocationConfig` shape, the build-viability
> verdict (all-three → ≈ 2.5-way), the constant tables — **pass 2 wins**. The structural
> results here (fixed Roster, endless Encounters, no cap, no rescale, the bag as the
> whole return pressure, CastThreshold as a texture knob) were re-confirmed under pass 2
> and still stand.

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

*(Pass 2 sharpened this to ≈ 2.5-way once the packed archetype was a variable — see
*Second amendment*. The Engagement asymmetry below still holds.)*

Each sustains the easy Location indefinitely and clears a gear-proportional slice of the
hard one. The lean (a finding, not a blocker): **magical ≈ 1.4× the XP/min** (area farm)
but the thinnest raw survival; **physical** lasts longest raw and opens hard Locations
at lower gear; **hybrid** sits between with no dominance and no soft spot. The
Engagement asymmetry the ADR wanted holds — physical wants Engagement low (kite),
magical wants Engagement 3–4 (feed the Cast), and a Pack overshoots any Engagement
setting.

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
  *(Superseded by* Second amendment*: two per-type Roster counts, a `Packed` flag, no
  `XpPerKill`.)*
- **Encounter driver (#20):** build only the fixed-Roster schedule; the Encounter never
  ends the Run; one Strike-only archetype from the shared curve; `tick`/`beat`/
  `castCadence` are module constants, not config. *(Two archetypes now — see* Second
  amendment*.)*

## Second amendment — two enemy archetypes and XP-on-clear (issue #18 pass 2)

The 2026-09-03 domain-modeling handoff pushed back on the single-archetype cut: with
one parametric enemy, an Encounter's only variety was its size, and — after per-kill
settle, no recap, endless Encounters and no cap — the **Encounter boundary had no
player-observable consequence**. It was a spawn-batching detail. Pass 2 of the
`/prototype` (`prototype/combat-cluster` @ `03a3275`, also an interactive Artifact;
40–200 seeded Runs per cell) validated the fix.

### Two archetypes, both parametric off `SourceLevel`

| | **Brute** | **Skirmisher** |
|---|---|---|
| profile | few, bulky, slow hard hits, some Armor, **low XP** | many, fragile, fast light hits, no Armor, **high XP** |
| targeted by | the **Cast** (highest-HP) | the **Strike** (lowest-HP) |
| curve @ S5 | HP ≈ 172, ≈ 2.8 DPS/body, ≈ 8 % Armor | HP ≈ 60, ≈ 4.8 DPS/body, 0 % Armor |

Both use `stat = base + perLevel · SourceLevel^exp`, one shared constant set for the
whole MVP (curves tabled in the prototype's `FINDINGS.md`). A Location **Packs** one
archetype — it arrives in `packBatch`-sized groups — and trickles the other in one at a
time; both types exist at every Location, so a Location is (source level × which type is
Packed). Enemies still never Cast; resists/penetration still deferred.

### The packed archetype picks the build

Raw-sustain sweeps at S5 (retreat + bag off, natural Engagement):

| packed | physical (kite, eng 2) | magical (brawl, eng 4) | hybrid (eng 3) |
|---|---|---|---|
| **Brute** | **8 Encounters / 317 s** | 4 / 145 | 6 / 255 |
| Mixed | 24 / cap | 26 / cap *(12 % died)* | 24 / cap *(1 % died)* |
| **Skirmisher** | 11 / 324 | **17 / 527** | 14 / 446 |

**AoE counters count; single-target counters bulk.** A Brute pack is a few high-HP
bodies — the single-target Strike keeps pace where the 3-target Cast can only touch 3.
A Skirmisher pack is a 5+ body swarm — the Cast clears it in parallel while the Strike
takes one per cadence and the rest chew the hero. The ordering holds with Engagement
forced equal, so it is the damage *shape*, not just the kite/dive stance.

This **sharpens** pass 1's "all three viable" to **≈ 2.5-way**: physical and magical are
the two poles, each owning one packed archetype; **hybrid owns nothing and loses nothing
badly** — 2nd in both specialist packs (~75–85 % of the winner), on par on mixed
composition, the only build with no death-rate there. Weak-sense three-way viable
(all progress); strong-sense two-way (only physical and magical are ever the *best*
pick). It answers #18's "if it is a two-way choice, say so": it is 2.5-way. The
Engagement asymmetry from pass 1 stands — physical kites, magical brawls.

### XP settles per Encounter clear

XP no longer drips per kill. Each kill adds `archetype.xp · (1 + (SourceLevel −
heroLevel)/100)` to a **per-Encounter pot**; the pot pays out on the `clear` event and
resets. A Run driven off mid-Encounter by Recall or Death **forfeits the whole pot**
(≈ 400 XP at the hard Location, about half an Encounter) — that forfeit is the
Encounter boundary's teeth, and is what justifies keeping "Encounter" as a term.

- **Loot Drops and coin Piles stay per kill** — coins are Loot (`Pile` Drops that
  auto-bank as they fall), not a settle event. ADR-0010's original "XP and coins are per
  kill" **splits**: XP on clear, Drops (items *and* coins) per kill. Spec story 38
  splits to match; spec story 12 ("XP bar fills as Encounters are cleared") is kept and
  is now literally how XP arrives.
- **This defuses the auto-focus worry.** The lowest-HP Strike still eats Skirmishers
  first (time-to-kill ≈ 1–3 s vs Brutes' 4–9 s), but with no per-kill XP that is just
  sensible threat triage — kill the fast, numerous bodies to cut incoming — not an
  exploit. There is no "focus the high-XP target for early XP" play.

### Re-confirmed under two enemies

Fixed Roster · endless Encounters, no cap, no rescale · CastThreshold moves throughput
< 1 % across its whole range (still a texture knob; keep the slider, keep the softened
framing) · the bag is still the entire "return to Town" pressure. The two authored
Locations now differ in *texture*, not just number: **Thornwood** (S2, Brute-packed) is
a full-HP bag run; **Ashfall** (S5, Skirmisher-packed) is a gear-gated wall that leans
magical, and gear opens it on a sharp cliff (physical crosses "dies at Encounter 2" to
"farms to the cap" between ×1.15 and ×1.3 — a one-shot breakpoint on Skirmisher HP).

### Downstream — supersedes pass 1's *Downstream*

- **`LocationConfig` (#25):** `Id`, `DisplayName`, `SourceLevel`, `LootTableRef`,
  `Packed` (`Brute | Skirmisher`), `RosterBrute [min,max]`, `RosterSkirmisher [min,max]`,
  `PackBatch [min,max]`, `PackedSpawnWeight`, `SpawnInterval`, `SpawnJitter`. **No
  `XpPerKill`** — XP is the per-archetype `xp` curve, settled on the clear. **No**
  per-Location enemy curves — the two curve sets are one shared module constant reading
  `SourceLevel`. **No** finite-encounter / completion field (pass 1).
- **Encounter driver (#20):** the per-type fixed-Roster schedule — the packed type in
  `PackBatch`-sized groups, the other one at a time, chosen each spawn tick by
  `PackedSpawnWeight` while both have remainder; Roster spent + last body down →
  `clear` → `beat` → next. **XP settles on the `clear`**, summed over the actual Roster;
  a `RunState` exit before a `clear` forfeits the in-progress pot — test this, it is the
  Encounter boundary's only observable effect. Two Strike-only archetypes from the shared
  curves.
- **Constants** (starting points; the prototype file is the tuning surface): `castCost`
  16, `castTargets` 3, `castCadence` 0.35 s, `tick` 0.1 s, `beat` 1 s. Build defence is a
  shared budget (Health 442 / Armor 27; hybrid spends its leftover damage on 476 / 30);
  damage shape is the differentiator. Full tables in the prototype's `FINDINGS.md`.
