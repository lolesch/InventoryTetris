# Combat-cluster /prototype (issue #18)

**Throwaway.** Lives only on this branch (`prototype/combat-cluster`), off `main`. The
validated decisions are folded into **ADR-0010** (*Prototype outcome*) and
`dev/specs/2026-09-02-mvp-simulation-loop-design.md` (*The combat model — prototype
findings*) on branch `docs/mvp-simulation-loop`. This branch keeps the prototype itself
as a primary source.

## The question

ADR-0010 fixed the *shape* of combat (concurrent Strike + Cast, soft Engagement, Location
Packs, per-kill settle) but left three things to a playable toy:

1. **Finite vs endless, at two scales** — is a Run N Encounters then done, or endless at
   fixed difficulty? Does an Encounter draw a fixed **Roster** or spawn without end? Is a
   rescale-on-restart rule needed?
2. **Are physical, magical and hybrid each tunable to viable**, or is it a two-way choice?
3. **Every constant** — base Strike/Cast damage, `castCost`, the two cadences, the
   `CastThreshold` curve, Roster/Pack sizes and timing, the enemy `SourceLevel` curves.

## The answer (see `FINDINGS.md` for the detail)

1. **Fixed Roster** (continuous spawn erases the Encounter as a unit and lowers
   throughput). **Endless Encounters, no cap, no rescale** — a realistic Run always ends
   on a Recall trigger (bag-full ~5 Encounters easy / retreat-HP ~1–2 hard) or Death; a
   `finiteEncounters` cap fires in <1% of Runs. The difficulty ladder is carried by
   equipped gear. The bag is the whole "return to Town" pressure.
2. **All three viable.** Not a two-way choice, hybrid not dominated. Lean: magical is the
   area-farm build (~1.4× XP/min, thinnest raw survival); physical lasts longest raw /
   opens hard Locations at lower gear; hybrid is the flat middle.
3. **`CastThreshold` is a texture knob, not a power knob** (throughput moves <0.3% across
   its whole range). Other constants: starting points in `FINDINGS.md` / ADR-0010, still
   tunable.

## Files

| file | what |
| --- | --- |
| `combat-cluster-prototype.html` | the shareable single-file sim — double-click to open. Presets, a live/fast-run Encounter, a 200-Run batch button, 8 guided walkthroughs. |
| `combat-model.js` | the pure model behind it (no DOM). The cadence / spawn / hysteresis rules here are written to lift into `InventorySystem.Encounter`; the numbers are the tuning target. |
| `sweep.mjs` | headless batch — `node sweep.mjs`. The tables the findings quote. |
| `calc.mjs` | quick DPS/TTK matchup math — `node calc.mjs`. |
| `sweep-output.txt` | a captured `sweep.mjs` run. |
| `FINDINGS.md` | the full write-up. |

`combat-model.js` and the model block inlined in the HTML are kept byte-identical in
behaviour (verified deterministic across seeds); the HTML omits only the `hpTrace`
instrumentation `sweep.mjs` needs.
