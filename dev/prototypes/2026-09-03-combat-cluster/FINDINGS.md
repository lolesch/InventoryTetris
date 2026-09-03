# Combat-cluster /prototype — findings (issue #18)

Prototype: `combat-cluster-prototype.html` (single file) + `combat-model.js` / `sweep.mjs`
(headless, `node sweep.mjs`). The model mirrors ADR-0010 and the live formulas
(`BaseCharacterExtensions.CalculateReceivingDamage` mitigation, `LocalPlayer.GainExperience`
curve). Numbers below are from 150–200 seeded Runs per cell.

Hero power in the sim is **flat within a Run** — the live code only grows the XP bar on
level-up (`LocalPlayer.GainExperience`), nothing else. The only thing that changes hero
strength is equipped loot, modelled here as a gear multiplier ×1.0 / ×1.25 / ×1.5.

---

## Q1 — Finite vs endless, at both scales, + rescale rule

### Encounter scale → **fixed Roster. Keep it.**

With `finiteRoster:false` (endless spawn) the Encounter counter never advances: no clear,
no beat, no "next Encounter". The fight is one unbounded stream trickling in to the
Engagement target until the bag or HP ends the Run. Throughput is *lower* than with a
Roster (easy: 414 vs 497 XP/min) because there is no clear→beat→reset rhythm and the
spawn just paces itself to Engagement.

The Roster `[min,max]` is the only thing that makes an Encounter a **unit** — the thing
the "short beat, then the next" language in the spec and CONTEXT.md describes. Drop it and
"Encounter" stops meaning anything; the Location becomes one long fight.

**Decision:** an Encounter draws a fixed Roster `[min,max]`; it clears when the Roster is
spent and the last enemy falls. `LocationConfig` carries `roster: [min,max]`.

### Run scale → **endless Encounters, no cap, no rescale.**

A realistic Run (retreat-at-HP + recall-at-bag-full both on) **always** ends on a Recall
trigger or Death — never on "the Location is done":

| build @ location | ends by | Encounters | duration |
|---|---|---|---|
| any @ easy | `Recalled:Bag` | ~5 | ~80 s |
| any @ hard (×1 gear) | `Recalled:HP` | 1–2 | ~40–60 s |

A `finiteEncounters` cap of 8 fires in **<1 %** of easy Runs — the bag fills at ~5
clears, well short of it. A cap of 3 does bind, but it is redundant with "recall when the
bag is 60 % full". **A finite-encounter Run mode is dead config** in the MVP: the bag (or
the HP line) always bounds the Run first, and it is Session state to persist for zero
behaviour change.

**Rescale-on-restart:** not needed and actively harmful. There is no "restart" — a Run
ends on Recall/Death and the Location is still there at the same difficulty. Fixed
difficulty is a design pillar (CONTEXT.md *Location*: "never scaled to the hero… a
difficulty ladder a geared hero outgrows"). The prototype shows the ladder works **through
gear alone**: magical @ hard crosses from "dies at 105 s / 6 kills" to "out-clears the
spawn and farms to the sim cap" between ×1.25 and ×1.5 gear. That cliff *is* the
"outgrows the Location" mechanic; a rescale rule would fight it.

**Decision:** a Location is an endless series of Encounters at a fixed source level. A Run
ends only on Recall (manual, retreat-HP, or bag-full) or Death. No encounter cap, no
completion state, no restart-rescale. `RunState` needs no "Location cleared" concept.

### One caveat the prototype surfaces

With endless Encounters + fixed difficulty + no bag limit, an over-geared build farms a
Location **forever** (walkthrough 8: bag off → runs to the 900 s cap, never in danger).
The **bag is the entire "come back to Town" pressure.** If bag capacity is generous or the
loot filter is set strict, a geared player has no reason to Recall and the loop stalls.
Implications: (a) bag capacity and the loot-filter defaults are loop-load-bearing, not
just flavour; (b) the two "harder" Location should be tuned so even a well-geared hero
takes visible attrition there (`minHealthFraction` settling below ~0.6), or it is pure
idle.

---

## Q2 — Are physical / magical / hybrid each viable?

**Yes — all three. Not a two-way choice, and hybrid is not dominated.**

*Viable* here means: sustains the easy Location indefinitely, and clears a
gear-proportional slice of the hard one.

| build | easy (raw, retreat off) | hard ×1 (raw) | hard ×1.25 | lean |
|---|---|---|---|---|
| physical | sustains to 900 s cap, full HP | dies ~80 s, 31 kills, 725 XP/min | ~5 Enc, 61 kills | **longest raw survival**, lowest throughput |
| magical | sustains to cap, full HP | dies ~50 s, 32 kills, **990 XP/min** | ~6 Enc, 75 kills | **~1.4× the XP/min** (AoE farm), dies soonest |
| hybrid | sustains to cap, full HP | dies ~54 s, 24 kills, 730 XP/min | between | genuinely mid, no standout, no weakness |

The pacing asymmetry ADR-0010 wanted does show up: **physical wants Engagement low**
(kite, kill fast 1-v-1 — Engagement 1 lasts 141 s vs Engagement 5's 42 s), **magical wants
Engagement 3–4** (feed the 3-target Cast — peak XP/min at Engagement 3). Hybrid is flat
across Engagement.

**Meta lean (a finding, not a blocker):** magical is the farm build (higher XP/min via
AoE), physical is the survival/progression build (lasts longest raw, opens hard Locations
at lower gear). Hybrid trades the extremes for no soft spot. This is a healthy triangle
for an itemisation game — no build is a trap, and the choice reads as "farm rate vs
safety".

### Constants that make this true (starting points, tune by feel)

Budget is one "geared level-N hero" split three ways:

| | physical | magical | hybrid |
|---|---|---|---|
| PhysicalDamage | 40 | 9 | 26 |
| AttackSpeed | 1.6 | 1.0 | 1.3 |
| MagicalDamage (per target) | 6 | 26 | 15 |
| Resource / regen /s | 45 / 6 | 130 / 20 | 85 / 15 |
| Health | 440 | 430 | 432 |
| Armor % | 30 | 24 | 26 |

- `castCost` **16** flat. `castTargets` **3**. `castCadence` **0.35 s** (burst ceiling).
- Steady Cast cadence is emergent = `castCost / ResourceRegeneration` (magical ≈ 0.8 s,
  hybrid ≈ 1.07 s). `castCadence` must stay **well below** that for any burst headroom
  (see Q3).
- Strike cadence = `1 / AttackSpeed`, unchanged.
- CombatClock `tick` **0.1 s** (10 Hz — AutoBattler's default; fine, one action per
  attack per tick, no attack lost below AttackSpeed 10). `beat` between Encounters **1 s**.

---

## Q3 — The rest of the constants

### Enemy archetype — `stat = base + perLevel · S^exp`, Strike only

| stat | base | perLevel | exp | @ S2 | @ S5 |
|---|---|---|---|---|---|
| Health | 15–20 | 18–20 | 1.10–1.12 | ~55 | ~135 |
| Damage (raw, pre-mitigation) | 0.8–1.0 | 0.7–0.85 | 1.0 | ~2.2 | ~6.0 |
| Armor % | 0 | 0.5–0.8 | 1.0 | ~1 | ~4 |
| AttackSpeed | 0.75–0.85 (flat, not on a curve) | | | | |
| xpPerKill | 16 (easy) … 30 (hard) — authored per Location, not a curve | | | | |

The exponent is barely above linear on purpose. `exp` ≥ 1.25 on Health makes hard
un-openable at any realistic gear; `exp` ≥ 1.15 on Damage makes hard a one-shot wall.
Keep both curves gentle and let the **source-level gap** (S2 vs S5) carry the difficulty
difference.

### Spawn profile (per Location)

| | easy | hard |
|---|---|---|
| Roster `[min,max]` | `[8,8]` | `[12,12]` |
| initialSpawn | 2 | 3 |
| spawnBatch `[min,max]` | `[1,1]` (pure trickle) | `[2,4]` (mixed, Packs) |
| spawnInterval | 2.4 s | 3.6 s |
| spawnJitter | ±0.6 s | ±0.9 s |

`spawnBatch [4,4]` is a **pure** Pack — it overshoots any Engagement setting and spikes
incoming damage; that is the Location's lever against a kiter (walkthrough 6, ADR-0010
intent). `[1,1]` is a trickle Engagement can fully manage. A hard Location mixes them
(`[2,4]`).

### CastThreshold — **a texture knob, not a power knob**

Sweeping CastThreshold 0.05 → 0.95 (magical @ hard, ×1.25 gear so fights last):

| threshold | kills | duration | XP/min |
|---|---|---|---|
| 0.05 (constant chip) | ~511 | ~615 s | 1483 |
| 0.4 | ~478 | ~576 s | 1482 |
| 0.8 | ~455 | ~551 s | 1479 |
| 0.95 (long charge → burst) | ~433 | ~525 s | 1481 |

**XP/min moves < 0.3 %.** The "pack-deleting burst" ADR-0010 describes does not pay for
itself on a **continuous** spawn: deleting a Pack two seconds early just pulls the next
Pack two seconds closer. A high threshold is mildly *worse* (time spent not casting).
Low-threshold (cast whenever `resource ≥ castCost`) is the marginal winner.

Options, in order of preference:

1. **Keep the slider, redocument it.** It is a *feel* knob — continuous Casts vs clumpy
   Casts — not a build-defining one. It earns real weight only when later content adds a
   pre-chargeable target (an elite/boss with a health bar worth saving a burst for) —
   exactly the kind of depth lever ADR-0010 already defers. ADR-0010's "silence broken by
   a pack-deleting burst" framing should be softened to "continuous chip vs clumped
   Casts; not a power choice in the MVP".
2. Cut it from the MVP sliders (back to five). Cast = "cast whenever `resource ≥
   castCost`". Simplest, and loses nothing the prototype could measure.
3. Redefine it as a **panic reserve** ("hold this fraction unless HP < 25 %") — the sweep
   shows a held pool as an emergency dump is the only version that changes outcomes, and
   only at high variance.

Recommendation: **option 1** — cheapest change to ADR-0010, keeps the slider count at six,
and the knob becomes genuinely interesting the moment elites exist.

---

## What this means downstream

### For #25 (`LocationConfig`)

Fields the prototype confirms it needs, and no more:

```
LocationConfig : ScriptableObject
{
  string   Id;                 // stable serialized, not the asset GUID
  string   DisplayName;
  int      SourceLevel;        // → RollContext.SourceLevel AND every enemy stat
  LootTableRef LootTable;      // → RollContext.Table
  int      XpPerKill;          // authored per Location (16 easy … 30 hard), not a curve
  Vector2Int Roster;           // [min,max] enemies per Encounter — FIXED roster confirmed
  Vector2Int SpawnBatch;       // [1,1] trickle … [4,4] Pack
  float    SpawnInterval;
  float    SpawnJitter;
}
```

- **No `EncounterCount` / `Finite` field** — Q1 killed the finite-Run mode.
- **No enemy-curve fields** — the `base/perLevel/exp` curves are a single shared constant
  set in the `Encounter` module (one archetype for the whole MVP). `LocationConfig` only
  supplies `SourceLevel`; the archetype reads it. If per-Location enemy identity is wanted
  later, that is an `EnemyArchetype` SO reference — out of scope for #25.
- Author two: easy `SourceLevel 2, Roster [8,8], SpawnBatch [1,1], interval 2.4`; hard
  `SourceLevel 5, Roster [12,12], SpawnBatch [2,4], interval 3.6`, richer loot table.
- Validation: `Roster.x ≥ 1`, `Roster.y ≥ Roster.x`, `SpawnBatch.x ≥ 1`,
  `SpawnInterval > 0`, non-empty loot table, stable non-GUID id.

### For #20 (the Encounter driver)

- Build the **fixed-Roster** spawn schedule (Roster spent + last enemy down → clear →
  `beat` → next). No endless-spawn branch.
- The Encounter **never ends the Run** — only Recall/Death do. Clear is a silent
  transition. `EnemiesDefeated` / `EncountersCleared` / `Duration` accumulate across the
  sequence; XP + coins settle per kill (already in ADR-0010).
- `CastThreshold` gates the Cast by resource hysteresis as specced — but the driver's
  tests should assert the *mechanic* (holds below the fraction, burns to empty above it),
  not any balance outcome, because there is no balance outcome to assert (Q3).
- One archetype, Strike-only, all stats from `curve(base, perLevel, exp, SourceLevel)`.
- `tick` 0.1 s, `beat` 1 s, `castCadence` 0.35 s as module constants (not on
  `LocationConfig`, not on `HeroBehaviour`).
- Deterministic: Roster size, batch size, spawn jitter, and the loot roll all pull from
  the injected `IRollSource` (ADR-0005).

### For the spec's Out of Scope

- "Finite vs endless — at two scales" → **resolved**, move to Implementation Decisions:
  fixed Roster, endless Encounters, no cap, no rescale.
- The `HeroBehaviour.CastThreshold` line stays (option 1) but the spec's slider story 21
  and ADR-0010's burst framing get the softened wording.
