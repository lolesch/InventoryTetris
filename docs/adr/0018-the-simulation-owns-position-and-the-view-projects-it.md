---
status: accepted
---

# The simulation owns position; the arena view only projects it

The enemy arena (`dev/specs/2026-10-05-enemy-arena-design.md`, section 1 "Cosmetic only") kept
the sim position-free on purpose: the sim stayed deterministic and seeded, and the view walked
enemy sprites in from outside a ring on its own clock, with its own random jitter. The cost
showed in play. An enemy struck from the moment it spawned however far its sprite still had to
walk, the hero hit the lowest-HP enemy anywhere on screen, and "where is this enemy" had two
answers that only agreed by accident. A skill with a shape and an area (the Cast, next) cannot
be built on a sim that has no notion of where anything is.

## Decision

1. **The simulation owns position.** The hero and every enemy have a `Coordinate` (the XZ
   plane) on a flat **Arena** around an **Origin**. Positions advance in the Encounter's tick
   on sim time, so they scale with sim speed and freeze at zero. The spawn bearing and the stop
   jitter are rolled in the sim, from a movement stream of their own, so adding a movement roll
   never reorders a seeded loot or spawn outcome.
2. **The view never moves anyone.** `EnemyArena` reads `Enemy.Position` and
   `EncounterSimulation.HeroPosition` every frame and draws them through one pure projection,
   `ArenaProjection`: canvas units per arena unit, and a **tilt** scalar where 1 draws the
   arena top-down and less than 1 flattens the depth axis. The sim never sees the tilt, and
   every distance the rules use is an arena distance.
3. **The Arena's Origin is the Hero icon anchor** (the selected Location's checkmark rect).
   The checkmark stays the Location's marker and is not moved; the hero **figure** that stands
   on the arena is a separate element, so a moving hero and a corpse marker never both claim
   the checkmark.
4. **Facing and draw order are read from the sim's arena, not the canvas.** An enemy faces the
   hero by which side of him it stands on, with a dead zone, and depth is the arena's z, so a
   tilt of zero still orders figures.
5. **The view-side walk-in is deleted**, not kept beside the sim's: the arena walk, the
   approach, spawn-point and slot-point maths and their tests, the slot-angle pick with its
   view-injected jitter, and the visuals asset's ring radii and approach speed. They would be
   a second answer to where an enemy is.

This supersedes the arena spec's section 1.

## Considered options

**Keep the walk in the view and gate the sim's strikes on a timer.** Cheapest, but the hero's
range, the enemies' stand-off and the Cast's area all need real distances. A timer is a
position the view can contradict. Rejected.

**Let the view send positions back into the sim.** Makes the view a participant in the rules
and breaks seeded tests, which run with no view at all. Rejected.

## Consequences

- Tests assert positions and outcomes through the simulation's advance entry point; the view's
  placement is the one pure `ArenaProjection` (and the facing and depth-order functions beside
  it), tested without a scene.
- `InventorySystem.Geometry` references `Utility` for `Coordinate`.
- The tilt is a serialized value on the arena: drawing the arena flatter or steeper needs no
  sim change, and a flattened view looks closer vertically than the rules treat it.
- A figure that fades out stays where it fell, since only its link to the enemy is cut.
