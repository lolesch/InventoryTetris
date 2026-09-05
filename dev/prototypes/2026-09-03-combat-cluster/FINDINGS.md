# Combat-cluster /prototype — findings (issue #18)

Prototype: `combat-cluster-prototype.html` (single file, now an Artifact too) +
`combat-model.js` / `sweep.mjs` (headless, `node sweep.mjs`). The model mirrors ADR-0010,
the **two-enemy model** from the 2026-09-03 handoff, and the live formulas
(`BaseCharacterExtensions.CalculateReceivingDamage` mitigation, `LocalPlayer.GainExperience`
curve). Numbers below are from 40–200 seeded Runs per cell; see `sweep-output.txt`.

Hero power in the sim is **flat within a Run** — the live code only grows the XP bar on
level-up. The only thing that changes hero strength is equipped loot, modelled here as a
gear multiplier ×1.0 / ×1.15 / ×1.3 / ×1.5.

> **This is a rerun.** The first pass (single parametric archetype) settled fixed-Roster /
> endless-Encounters / no-rescale, "CastThreshold is a texture knob", and the bag as the
> whole return-to-Town pressure. All of that **still holds** under two enemies — re-confirmed
> below. What is new: the two-enemy model, XP-on-clear, and the build↔pack matchup.

---

## The two-enemy model

Two shared parametric archetypes, both `stat = base + perLevel · S^exp`, Strike-only.
A Location **Packs** one and trickles the other in singly.

| | Brute (A) | Skirmisher (B) |
|---|---|---|
| Health @ S2 / S5 | 78 / 172 | 29 / 60 |
| raw damage @ S5 | 5.1 @ AS 0.55 = **2.8 DPS/body** | 3.0 @ AS 1.6 = **4.8 DPS/body** |
| Armor % @ S5 | ~8 (Strike-resistant) | 0 |
| xp @ S5 (pre-balance) | 23 | 53 |
| role | few, bulky, hard slow hits, **Cast food** (highest-HP → Cast targets it) | many, fragile, fast light hits, **Strike food** (lowest-HP → Strike targets it) |

`archetypes.brute` / `archetypes.skirmisher` curve-sets; `location` supplies only
`SourceLevel`, `packed` (`'brute'|'skirmisher'`), `roster: {brute:[min,max], skirmisher:[min,max]}`,
`packBatch`, `packedSpawnWeight`, and spawn timing. Both archetypes exist at every Location
(handoff open-question 3, "both everywhere" lean confirmed as workable).

**Incoming aggregate** (3-pack + 1 single, hero Armor 27%): Skirmisher-packed ≈ **12.6**
mitigated DPS vs Brute-packed ≈ **9.6**. A Skirmisher swarm hits harder in total than a
Brute pack at equal S — that is why the S5 Skirmisher Location (Ashfall) is the wall and
the S5 Brute Location is merely hard.

---

## Q1 — Which build for which pack? (the new question)

**It is a two-axis choice: damage *shape* matched to the packed archetype, and Engagement
stance.** Physical and magical are the two poles; hybrid is a valid middle that owns nothing.

### Raw sustain @ S5, natural Engagement (retreat + bag OFF — how long can it out-sustain the pack?)

| pack (S5, ~17 roster) | physical (eng 2) | magical (eng 4) | hybrid (eng 3) | winner |
|---|---|---|---|---|
| **Brute-packed** (12 B, 5 S) | **8 Enc / 317 s** | 4 / 145 | 6 / 255 | **physical**, ~2× magical |
| **Mixed** (8 B, 9 S) | 24 / cap | 26 / cap *(12% died)* | 24 / cap *(1% died)* | wash — hybrid safest |
| **Skirmisher-packed** (5 B, 12 S) | 11 / 324 | **17 / 527** | 14 / 446 | **magical**, ~1.5× physical |

Forcing Engagement to 3 for all three keeps the same ordering (physical best vs Brute,
magical best vs Skirmisher), so it is the **damage shape**, not just the kite/dive stance.

### Why (closed-form, `node calc.mjs`)

- A **Brute pack** is high total-HP but **low count** (≈3 bodies). Physical's single-target
  Strike (68 DPS @ S5, Strike-TTK 2.5 s per Brute) keeps pace with 3 bodies. Magical's Cast
  hits the 3 highest-HP — i.e. the Brutes — at 6 s each but can only touch 3, and its Strike
  is negligible, so a 4th Brute is untouched and incoming never drops.
- A **Skirmisher pack** is low total-HP but **high count** (5+ bodies). Magical's 3-target
  Cast clears them in parallel (Cast-TTK 2.1 s × 3). Physical's Strike takes one Skirmisher
  per 0.6 s cadence (0.8 s TTK) while the rest of the swarm — 4.8 DPS each — chews the hero;
  its Cast (2 DPS/target) does nothing.
- **AoE counters numerosity; single-target counters bulk.** This is the opposite of the
  naive "bring AoE for the pack of tanks" read.

### Hybrid

Second in **both** specialist packs — ~75–85 % of the winner's Encounter count — roughly on
par on genuinely mixed composition, and the only build with **no death-rate** on mixed.
Its damage budget is a true midpoint; the leftover goes into a thicker HP pool (476 vs 442,
armor 30 vs 27), which is what keeps its bad matchup from being catastrophic.

- **Three-way viable in the weak sense** — all three clear content and progress.
- **Two-way in the strong sense** — only physical and magical are ever the *best* pick.
- Hybrid's justification: a transitional build while you assemble gear for a pole, and a
  no-hard-counter generalist. This directly answers #18's "if it is a two-way choice not
  three-way, say so": it is **2.5-way**.

### Engagement stance (still a real lever, per prototype 1)

Physical wants Engagement **low** (kite — one body at a time; its Strike doesn't benefit
from more targets). Magical wants Engagement **3–4** (feed the 3-target Cast). Hybrid is
flat. Location **Packs overshoot any Engagement setting** and spike incoming — that is the
Location's lever against a kiter (ADR-0010 intent), and `packBatch [3,5]` at Ashfall is it.

---

## Q2 — Finite vs endless (re-confirmed under two enemies)

### Encounter scale → **fixed Roster** (now a per-type composition)

With an unbounded roster the Encounter counter never advances — no clear, no beat, no XP
settle (XP now settles *on the clear*, so endless spawn means **zero XP forever**). The
Roster — `{brute:[min,max], skirmisher:[min,max]}` — is the only thing that makes an
Encounter a unit. Keep it. `LocationConfig` carries the per-type roster.

### Run scale → **endless Encounters, no cap, no rescale**

| Location | build | ends by | Encounters | note |
|---|---|---|---|---|
| Thornwood (S2 Brute-pack) | any | `Recalled:Bag` | ~4 | full HP throughout — the bag is the only limiter |
| Ashfall (S5 Skirm-pack) ×1 gear | any | `Recalled:HP` / `Died` | 2–3 | a wall; settled HP ~0.3 |
| Ashfall ×1.3 gear | physical | farms to 900 s cap | 48+ | one-shot breakpoint on Skirmisher HP → sharp cliff |
| Ashfall ×1.3 gear | magical | mostly farms (11% died) | 41 | opens more gradually |

A `finiteEncounters` cap of 8 still fires in **<1 %** of realistic Runs — the bag or the HP
line always bounds the Run first. Dead config. Fixed difficulty holds; the ladder is
equipped gear (a ~1.15–1.3× swing crosses Ashfall from "dies at Enc 2" to "farms forever").
No restart-rescale — a Run ends on Recall/Death and the Location is still there at the same
S. `RunState` needs no "Location cleared" concept.

### The caveat still stands

Endless Encounters + fixed difficulty + no bag limit ⇒ an over-geared build farms a
Location **forever** (walkthrough "Thornwood is a bag run" with bag off → 900 s cap, never
in danger). **The bag is the entire "come back to Town" pressure.** Bag capacity and the
loot-filter defaults are loop-load-bearing.

---

## Q3 — XP settles per Encounter clear (handoff decision 1)

XP no longer drips per kill. Each kill adds `arch.xp · (1 + (S − heroLevel)/100)` to a
**per-Encounter pot**; the pot pays out on the `clear` event and resets. A Run driven off
mid-Encounter by Recall or Death **forfeits the whole pot**.

- Realistic Runs forfeit little (the exit lands near a clear): **~0 XP at Thornwood, ~400
  at Ashfall** (about half an Encounter).
- A **Death** mid-Encounter forfeits ~185–410 XP depending on how deep it was — a real
  sting that gives the Encounter boundary the player-visible consequence it was missing.
- It **defuses the auto-focus worry** (handoff open-question 7): the lowest-HP Strike still
  eats Skirmishers first (ttk ~1–3 s vs Brutes' 4–9 s), but with no per-kill XP that is
  just sensible threat triage — kill the fast, numerous bodies first to cut incoming — not
  an exploit. There is no "focus the high-XP target for early XP" play.
- **Loot Drops stay per kill** (bag pressure accrues as bodies fall). **Coins**: the
  prototype does not model banking cadence; the lean (physical `Pile` Drops that auto-bank
  as they fall) is unaffected — recommend **per kill**, unchanged.

---

## Q4 — CastThreshold, re-checked with two enemies

Sweeping CastThreshold 0.05 → 0.9 (magical @ Ashfall ×1.3):

| threshold | Encounters | XP/min |
|---|---|---|
| 0.05 (constant chip) | 32 | 2124 |
| 0.30 | 42 | 2127 |
| 0.60 | 27 | 2126 |
| 0.90 (long charge → burst) | 32 | 2038 |

**XP/min moves < 1 %** until the very top of the range, where a long idle-charge is mildly
*worse*. Two enemies do not rescue the "pack-deleting burst" — on a continuous spawn,
deleting a pack a beat early just pulls the next one a beat closer. **Keep the slider
(six sliders), soften ADR-0010's framing** to "continuous chip vs clumped Casts; a feel
knob, not a power one". It earns real weight only when later content adds a pre-chargeable
target (an elite with a health bar worth saving for) — the depth lever ADR-0010 defers.
~0.3 is a reasonable default (enough charge for a small burst, no idle waste).

---

## Q5 — The constants (starting points, tune by feel)

### Builds — equal defensive budget, damage shape is the differentiator

| | physical | magical | hybrid |
|---|---|---|---|
| PhysicalDamage | 46 | 8 | 28 |
| AttackSpeed | 1.6 | 1.0 | 1.35 |
| MagicalDamage (per target) | 5 | 23 | 14 |
| Resource / regen /s | 45 / 6 | 120 / 20 | 86 / 14 |
| Health / regen /s | 442 / 3 | 442 / 3 | **476 / 4** |
| Armor % | 27 | 27 | **30** |
| CastThreshold | 0.30 | 0.40 | 0.35 |
| Engagement | 2 | 4 | 3 |

Physical & magical share defence exactly; hybrid spends its leftover damage budget on HP.
Gear multiplier scales `physicalDamage`, `magicalDamage`, `health` by `m` and
`attackSpeed` by `1 + (m−1)·0.4`.

### Combat module constants

- `castCost` **16** flat. `castTargets` **3**. `castCadence` **0.35 s** (burst ceiling).
- Steady Cast cadence is emergent = `castCost / ResourceRegeneration` (magical ≈ 0.8 s,
  hybrid ≈ 1.14 s). `castCadence` must stay well below it for any burst headroom.
- Strike cadence = `1 / AttackSpeed`, unchanged. ADR-0010's `× (1 + AttackSpeed·0.01)`
  term stays dropped (a real cadence carries AttackSpeed through frequency).
- CombatClock `tick` **0.1 s** (10 Hz). `beat` between Encounters **1 s**.

### Archetype curves — `stat = base + perLevel · S^exp`

| | Brute | Skirmisher |
|---|---|---|
| Health | base 26, perLevel 24, exp **1.12** | base 10, perLevel 9, exp **1.06** |
| Damage (raw) | base 1.0, perLevel 0.82, exp 1.0 | base 0.5, perLevel 0.5, exp 1.0 |
| Armor % | base 3, perLevel 0.9, exp 1.0 | base 0, perLevel 0, exp 1.0 |
| AttackSpeed | 0.55 (flat) | 1.6 (flat) |
| xp | base 5, perLevel 3.5, exp 1.0 | base 13, perLevel 8, exp 1.0 |

Exponents stay near-linear on purpose — `exp ≥ 1.2` on Brute Health makes the hard
Location un-openable at realistic gear; the **source-level gap** (S2 vs S5) carries the
difficulty difference, not the curve shape.

### Locations authored for the MVP

| | Thornwood (Location 1) | Ashfall (Location 2) |
|---|---|---|
| SourceLevel | 2 | 5 |
| packed | `brute` | `skirmisher` |
| Roster | brute [9,9], skirmisher [5,5] | brute [4,4], skirmisher [13,13] |
| packBatch | [2,3] | [3,5] |
| packedSpawnWeight | 0.6 | 0.7 |
| spawnInterval / jitter | 2.6 / ±0.7 s | 2.3 / ±0.7 s |
| feel | tutorial bag-run, no build pressure | gear-gated wall, magical-leaning |

Location 1 Packs Brutes and Location 2 flips to Skirmishers, per the handoff. Note the
S2 Location is too trivial to show the build↔pack lean live — the **isolation grid**
(`s5-brutepack` / `s5-mixed` / `s5-skirmpack`, all S5) is where the axis is visible. A
future hard **Brute** Location would demonstrate physical's edge the way Ashfall shows
magical's.

---

## What this means downstream

### For #25 (`LocationConfig`)

```
LocationConfig : ScriptableObject
{
  string      Id;                 // stable serialized, not the asset GUID
  string      DisplayName;
  int         SourceLevel;        // → RollContext.SourceLevel AND both archetype stat blocks
  LootTableRef LootTable;
  EnemyType   Packed;             // Brute | Skirmisher — Packs of this, singles of the other
  Vector2Int  RosterBrute;        // [min,max] Brutes per Encounter
  Vector2Int  RosterSkirmisher;   // [min,max] Skirmishers per Encounter
  Vector2Int  PackBatch;          // [3,5] = a real Pack ; [1,1] = trickle
  float       PackedSpawnWeight;  // P(next spawn draws the packed type) while both remain
  float       SpawnInterval;
  float       SpawnJitter;
}
```

- **No `XpPerKill`** — XP is per-archetype (`archetype.xp` curve) and settles on the
  Encounter clear, not per kill. Reverses the first prototype's `XpPerKill` field.
- **No `EncounterCount` / `Finite` field** — Q2 killed the finite-Run mode.
- **No per-Location enemy-curve fields** — the two `base/perLevel/exp` curve-sets are one
  shared constant block in the Run/Encounter module. `LocationConfig` supplies only
  `SourceLevel` + which archetype is `Packed` + the roster mix.
- Author two: Thornwood and Ashfall as tabled above.
- Validation: `Roster*.x ≥ 0`, `Roster*.y ≥ Roster*.x`, `RosterBrute.y + RosterSkirmisher.y ≥ 1`,
  `PackBatch.x ≥ 1`, `0 ≤ PackedSpawnWeight ≤ 1`, `SpawnInterval > 0`, non-empty loot table.

### For #20 (the Encounter driver)

- Fixed **per-type** Roster spawn schedule: packed type in `PackBatch`-sized groups, the
  other one at a time, chosen each spawn tick by `PackedSpawnWeight` while both have
  remainder; Roster spent + last body down → `clear` → `beat` → next. No endless-spawn branch.
- **XP settles on `clear`**, summed over the Encounter's actual roster with each body's
  per-archetype `xp` balanced by `(1 + (SourceLevel − heroLevel)/100)`. A `RunState` that
  exits via Recall/Death **before** a `clear` forfeits the in-progress pot — this is the
  Encounter boundary's teeth; test it.
- The Encounter **never ends the Run** — only Recall/Death do. Clear is a silent transition.
- `CastThreshold` gates the Cast by resource hysteresis as specced — but tests assert the
  *mechanic* (holds below the fraction, burns to empty above), not a balance outcome (Q4).
- Two archetypes, Strike-only, every stat from `curve(base, perLevel, exp, SourceLevel)`.
- `tick` 0.1 s, `beat` 1 s, `castCadence` 0.35 s as module constants.
- Deterministic: per-type roster size, spawn-type choice, batch size, spawn jitter, loot
  roll all pull from the injected `IRollSource` (ADR-0005).

### For the spec's Out of Scope

- "Enemy variety" → **retired**. The single-archetype cut is reversed: two archetypes,
  parametric, differentiated per Location by which is Packed. Record the reversal (short
  ADR or an amendment to ADR-0010's *Prototype outcome*).
- "XP and coins are per kill" (ADR-0010) → **split**: XP per Encounter clear, loot Drops
  and coins per kill. Story 12 ("XP bar fills as Encounters are cleared") is kept, story 38
  splits.
- `EncounterResult` → `RunResult` (Run-scoped), module `InventorySystem.Encounter` → a
  Run-scoped name — prototype-independent renames from the handoff, land with the doc pass.
