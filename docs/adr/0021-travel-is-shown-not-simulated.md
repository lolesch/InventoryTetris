---
status: accepted
---

# Travel is shown, not simulated

The hero now walks everywhere: to a Town Stop, to an Exit, from the Field Exit to a Location, and between
Locations on a Relocate (`dev/specs/2026-10-16-hero-travel-design.md`). ADR-0018 gave position to the
simulation and said the view never moves anyone. A figure walking on its own looks like a break from that,
and the simulation has no position at all outside an Encounter. This settles which side owns those walks.

## Decision

1. **Travel is presentation.** Every action takes effect on the click, as before: a Town Stop opens, a
   Send starts the Run, a Relocate swaps the Encounter, a Return ends the Run. The hero figure then walks
   there. No rule waits for it, and no rule reads where it is.
2. **Inside an Encounter, ADR-0018 holds.** Once the Field figure has arrived, it draws the simulation's
   `HeroPosition` through `ArenaProjection` and never moves itself. It walks on its own clock only when
   no simulated position exists for it: in Town, at the Field Exit, and on the way to a Location.
3. **The Encounter is not held during the walk in.** The first spawn delay runs while the figure is still
   on its way, and the simulated hero is already at the Origin. Walks are short, so the two rarely
   disagree, and this is accepted for now.

## Considered options

**Travel as rules.** A `Traveling` state: the Run starts on arrival, a Return completes at the Exit, and
enemies can strike a hero who is leaving. It would change what leaving costs, which is a balance change
the request did not ask for. It also needs positions across the whole Field map, not one arena per
Encounter. Rejected.

**Hold the Encounter until the figure arrives.** Keeps the figure and the simulation in agreement on the
walk in, using the same freeze the menus already use. It is not needed while walks stay shorter than the
spawn delay. Deferred: it is the fix if a walk in gets struck.

## Consequences

- The Field figure has two modes, walking on its own and drawing the simulation's position, and a
  Relocate, Return or Retreat switches it back to walking.
- Tests of the rules never wait for a figure; Travel is tested on its own, without a simulation.
- A hero can be struck before his figure arrives if a walk in ever outlasts the spawn delay.
