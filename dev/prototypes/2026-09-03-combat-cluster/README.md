# Combat-cluster /prototype (issue #18)

**Throwaway.** Lives only on this branch (`prototype/combat-cluster`), off `main`. The
validated decisions fold into **ADR-0010** (*Prototype outcome*) and
`dev/specs/2026-09-02-mvp-simulation-loop-design.md` on branch `docs/mvp-simulation-loop`.
This branch keeps the prototype itself as a primary source.

Interactive version (runnable + tweakable in a browser): the file
`combat-cluster-prototype.html` is also published as an Artifact —
<https://claude.ai/code/artifact/4ee0bd0f-ff20-4078-895b-e935c4138acf>.

## History

- **Pass 1** (single parametric archetype): settled fixed-Roster / endless-Encounters /
  no-rescale, "CastThreshold is a texture knob", the bag as the whole return-to-Town
  pressure, three-way build viability. Folded into ADR-0010.
- **Pass 2** (this rerun, per the 2026-09-03 handoff): **two enemy archetypes**
  (Brute / Skirmisher), **XP settles per Encounter clear**, and the new question — which
  build counters which packed archetype.

## The question (pass 2)

1. Does a Location that **Packs** one archetype and trickles the other make the two
   MVP Locations feel distinct beyond source level?
2. Does the deterministic lowest-HP Strike auto-focus the high-XP Skirmisher, and is that
   good or bad?
3. Does the split change the physical / magical / hybrid co-viability finding?
4. What shape do the per-type spawn rules take on `LocationConfig` (#25)?

## The answer (see `FINDINGS.md`)

1. **Yes, strongly.** The packed archetype changes the fight's texture *and* picks the
   favoured build: **Brute-packed → physical** (single-target Strike for the few bulky
   bodies), **Skirmisher-packed → magical** (3-target Cast for the swarm). AoE counters
   count; single-target counters bulk.
2. **Defused by XP-on-clear.** Strike still eats Skirmishers first, but with no per-kill
   XP that is just threat triage, not an exploit.
3. **It sharpens it to ~2.5-way.** Physical and magical are the two poles, each owning one
   packed archetype; hybrid owns nothing, loses nothing badly (2nd everywhere, ~75–85% of
   the winner), and is the safe/transitional pick. Weak-sense three-way viable, strong-sense
   two-way.
4. `Packed` enum + `RosterBrute` / `RosterSkirmisher` `[min,max]` + `PackBatch` +
   `PackedSpawnWeight` + spawn timing. **No `XpPerKill`** (per-archetype xp curve, settled
   on clear). Detail in `FINDINGS.md`.

Fixed Roster, endless Encounters, no cap, no rescale, CastThreshold-is-texture, and
bag-is-the-pressure **all re-confirmed** under two enemies.

## Files

| file | what |
| --- | --- |
| `combat-cluster-prototype.html` | the sim — double-click to open, or use the Artifact link above. Build/Location presets, an isolation grid (S5 Brute-/Mixed-/Skirm-packed), gear multiplier, a live run, the **3×3 matchup grid**, a 200-Run batch, 9 guided walkthroughs, editable constants. |
| `combat-model.js` | the pure model (no DOM). Cadence / spawn / hysteresis / XP-settle rules written to lift into the Run/Encounter module (#19–#20); the numbers are the tuning target. |
| `sweep.mjs` | headless batch — `node sweep.mjs`. Q-A build×pack, Q-B canonical Locations, Q-C auto-focus, Q-D gear, Q-E CastThreshold, Q-F XP forfeit. |
| `calc.mjs` | closed-form matchup math — `node calc.mjs`. Sanity-checks a tuning change fast. |
| `sweep-output.txt` | a captured `sweep.mjs` run. |
| `FINDINGS.md` | the full write-up. |

`combat-model.js` and the model block inlined in the HTML are kept **byte-identical in
behaviour** (verified deterministic across 100+ build×Location×seed configs); the HTML
omits only the `hpTrace` instrumentation `sweep.mjs` needs.
