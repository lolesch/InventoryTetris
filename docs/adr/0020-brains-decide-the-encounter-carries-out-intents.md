---
status: accepted
---

# Brains decide; the Encounter carries out Intents

`EncounterSimulation` decides and resolves in the same `Step` for both sides: the hero's target,
walk and Cast aim sit beside range gating, cadence timers and `Land`, and the enemies' chase sits
beside their Strike. The hero is steered by the player's Behaviour Profile and the enemies chase
and Strike, but the roadmap changes both: the player may take direct control of the hero, and
enemies may gain AI. That is a swap of who decides, not of what the rules are, so the seam goes
between the two.

## Decision

1. **A Brain decides, an Intent is what it asks.** Each fighter has a **Brain**, asked once a
   tick. It returns **Intents**: walk toward a point, Strike a target, Cast at a point, or (hero
   only) request a Recall. Intents are commands, not goals: "engage this target" is a policy a
   Brain implements by issuing commands, so a player Brain maps its input straight onto them.
2. **The Encounter carries Intents out.** It owns the rules: reach, the Strike and Cast cadences
   with the "ready but banks no burst" clamp, Resource cost, `Land`, kills and XP. An Intent it
   refuses costs nothing. It tells a Brain when a Strike is ready and when a Cast beat is open, so
   the auto-hero can step its `CastThreshold` latch on every beat, as it does now.
3. **Decide, then execute.** Every Brain decides from the state at the start of the tick; the
   Intents then run in today's order (hero walks, enemies walk, hero Strikes, hero Casts, enemies
   Strike). No Brain sees another fighter's move from the same tick.
4. **The Arena is the spatial module only**: positions, the movement step, reach and shape
   queries, spawn bearings. It never decides and never deals damage.
5. **A Brain sees a narrow read-only view** (own stats, the readiness flags, positions, the Arena's
   queries) and gets **no random rolls**. Spawn placement keeps rolling from the movement stream,
   so every seeded outcome is unchanged. A Brain that needs randomness later gets a stream of
   its own.
6. **The first two adapters are today's behaviour**: an auto-hero Brain (Behaviour Profile, the
   sticky target, the Cast latch) and a chase-and-Strike enemy Brain, built by default so the
   existing simulation tests keep running through `Advance` unchanged.
7. **The target ring reads the target of the latest Strike or approach Intent.** The pure
   `StrikeTarget` peek goes: it only worked while the sim made the choice itself.
8. **Strike and Cast stay the two verbs** (ADR-0010). Spawn pacing and the hero-down check stay in
   the Encounter. A Recall Intent ends the fight as the auto-Recall triggers do now, after the
   hero-down check, so a tick that both kills the hero and triggers Recall is still a Death.

## Considered options

**Goal-level Intents ("engage target X"), with the Encounter doing the walking.** Keeps the
walk-then-swing policy inside the rules, which is exactly the part a player Brain has to
replace. Rejected.

**Interleave the deciding with the acting.** Later fighters would see earlier moves, so the
outcome depends on visiting order and a scripted Brain is harder to write. Rejected.

**Brains with their own timers.** A player Brain could out-swing the hero's Attack Speed.
Rejected.

**A Brain seam for the hero only.** The enemies' chase and Strike rules would stay inline and
the executor would be half-written. Rejected.

## Consequences

- Each side has one adapter until a player or AI Brain exists, so the seam is hypothetical by
  the usual count. It is built now on the stated roadmap, and kept as narrow as the view above.
- The glossary line that during a Run "the player never controls [the hero] directly" stays true
  while the auto-hero Brain is the only hero Brain. A player Brain is the change that reopens it.
- A player Brain deciding at the 0.1 s tick needs its own spec; this seam does not fix the rate.
- ADR-0018's "tests go through the simulation's advance entry point" still holds. New tests add
  scripted Brains for the Encounter and direct queries for the Arena.
