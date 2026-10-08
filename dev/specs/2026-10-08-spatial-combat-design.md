# Spatial Combat

Date: 2026-10-08
Status: Scoping spec - not a plan. Slice into a GitHub epic with `/to-tickets`, build with
`/implement`.
Base: `main` at `73ec5c5`.
Derived with: a design interview (every decision below was made by the owner), then
`/drift-review` over the built enemy-arena slice (#175-#182) against these decisions; its
findings are folded into Further Notes.
Supersedes: `2026-10-05-enemy-arena-design.md` section 1 ("Cosmetic only"), and amends ADR-0010
(the enemies' damage type and magic resist).
Depends on: the Utility submodule (`Coordinate`, `FloatExtensions.Map`).

## Problem Statement

The arena draws the fight, but the fight does not happen there. An enemy strikes from the
moment it spawns, however far its sprite still has to walk. The hero hits the lowest-HP enemy
anywhere on the screen, and his Cast hits the three highest-HP enemies whatever their
positions. The hero never moves, so there is no reason for him to want to be anywhere. Brute
and Skirmisher differ in stats and sprite but not in how they fight, and both hit with
physical damage, so magic resist never matters. A damage number is the same size and colour
whatever hit, and the hero's own damage taken shows nowhere.

The player cannot build for position, because there is none. The Cast cannot grow into
skills with shapes and areas, because it has no notion of where anything is.

## Solution

The fight takes place on a **ground**: a flat disk with an **origin** that the hero calls home.
The hero and every enemy have a position on it, and the arena draws those positions.

Enemies spawn at the edge of the ground and **walk in for real**. A melee enemy closes in on
the hero until it is within its **Strike Range**, and a ranged enemy does the same with a
longer Strike Range, so it stands off. Enemies only Strike while the hero is within their
Strike Range, and they chase the hero's current position, so when he walks they follow.

The hero **walks to fight**. He picks a target, closes to his own Strike Range (set by his
weapon), and keeps that target until it falls. He prefers targets close to the origin, but
also close to himself, so he fights his way across the ground instead of running past
enemies. When nothing is left alive he walks back to the origin. A single **origin weight**
slider on the behaviour sliders sets how strongly home pulls against nearness.

The **Cast** becomes an area. It stays instant. The hero chooses a spot among the enemies in
his **Cast Range** where the area would catch the most enemies, and every enemy inside the
shape takes the magical hit. Shapes are a disk, a sector (cone) or a rectangle, and a shape
can start at the target or at the hero and point at the target.

Each archetype now declares its **damage type**: Brute hits physically and Skirmisher
magically. The hero's magic resist finally matters, and enemies get one too, so his Cast is
mitigated.

Every hit rolls a little damage spread, so numbers vary. A damage number's size shows how big
the hit was against the best the dealer could do with that damage type, and magical numbers
are tinted dark blue-purple. The hero's incoming hits show numbers too.

## User Stories

1. As a player, I want an enemy to strike only once it has reached me, so that a new arrival
   is not already hitting me from off-screen.
2. As a player, I want my hero to be unable to strike an enemy that is still walking in, so
   that what I see matches what is happening.
3. As a player, I want melee enemies to close in on my hero and stand next to him, so that the
   fight reads as a brawl.
4. As a player, I want ranged enemies to stop at a distance and hit me from there, so that
   they feel different from melee enemies.
5. As a player, I want enemies to keep following my hero when he walks, so that nobody ends up
   stranded out of the fight.
6. As a player, I want the hero to walk toward the enemy he is fighting, so that range
   matters.
7. As a player, I want the hero to finish the enemy he started on, so that a new spawn does
   not make him turn around mid-run.
8. As a player, I want the hero to prefer enemies close to his home, so that he does not
   wander across the whole ground after a straggler.
9. As a player, I want the hero to also prefer enemies close to himself, so that he never runs
   past an enemy to reach one near home.
10. As a player, I want the hero to stop and fight an enemy that closes in on him while he is
    walking to a far target, so that he is not hit for free.
11. As a player, I want the hero to walk back to his origin when nothing is alive, so that he
    wanders but returns.
12. As a player, I want one slider that sets how strongly home pulls against nearness, so that
    I can make the hero a homebody or a brawler.
13. As a player, I want the weapon I wield to set my hero's Strike Range, so that a spear and
    a dagger play differently.
14. As a player, I want gear not to roll extra range, so that range never becomes the one stat
    every build must stack.
15. As a player, I want an unarmed hero to still have a short Strike Range, so that he can
    fight without a weapon.
16. As a player, I want my hero's Cast to hit every enemy inside its area, so that bunching
    enemies up is rewarded.
17. As a player, I want the Cast to pick the spot that catches the most enemies, so that I do
    not have to aim it myself.
18. As a player, I want a Cast to fire only when an enemy is within Cast Range, so that it
    does not spend resource on nothing.
19. As a player, I want my hero to Cast without having to stop walking, so that the Cast
    cadence does not cost me Strike time.
20. As a player, I want the Cast to hit enemies only, so that friendly fire does not exist
    yet.
21. As a player, I want a Cast shape to be able to start at the hero and point at the target,
    so that cones and lines are possible later.
22. As a player, I want Brute to hit physically and Skirmisher magically, so that my armor and
    my magic resist both matter.
23. As a player, I want enemies to have magic resist, so that my Cast is mitigated like my
    Strike is.
24. As a player, I want a damage number's size to show how big the hit was, so that I can tell
    a strong hit from a weak one at a glance.
25. As a player, I want hits to vary a little, so that numbers are not all identical.
26. As a player, I want magical hits tinted dark blue-purple, so that I can tell a Cast from a
    Strike.
27. As a player, I want to see a number when my hero is hit, so that I know what is hurting
    him.
28. As a player, I want incoming numbers tinted by damage type as well, so that I can tell
    which resist is failing me.
29. As a player, I want one number per target per damage type per frame at high sim speed, so
    that numbers stay readable.
30. As a player, I want the marked target to be the enemy my hero will actually hit next, so
    that the ring never lies.
31. As a player, I want the fight to run faster at higher sim speeds and freeze at zero, so
    that movement behaves like everything else.
32. As a designer, I want enemies to be built from stats that can be modified, so that effects
    and gear can change them later.
33. As a designer, I want the Cast's targeting to be a pattern chosen by an enum, so that a
    skill can swap it.
34. As a designer, I want the hero's targeting to be a pattern chosen by an enum, so that it
    can be swapped the same way.
35. As a designer, I want area shapes to be pure functions with an area multiplier, so that
    "increased area" works on every shape.
36. As a designer, I want the Cast's range, shape, size, anchor and targeting in one definition,
    so that a skill can supply it later.
37. As a developer, I want the sim to stay deterministic for a seed, so that tests can assert
    positions and outcomes.
38. As a developer, I want movement and hit rolls on their own random streams, so that adding
    them does not shift any seeded loot or spawn outcome.
39. As a developer, I want the view to read positions from the sim and never move enemies
    itself, so that there is one answer to where an enemy is.
40. As a developer, I want the view's tilt to be an adjustable scalar, so that the ground can
    be drawn top-down or flattened without touching the sim.

## Implementation Decisions

**The ground and positions**

- The simulation owns position. The hero and every enemy have a position on a flat ground
  plane, expressed with the Utility `Coordinate` type (the XZ plane). The ground has an
  **origin**, a radius, and a spawn margin beyond its edge; these are tuning values.
- The arena view only projects. It maps ground positions to canvas positions with one
  adjustable **tilt** scalar: 1 draws the ground top-down, less than 1 flattens the
  depth axis. The sim never sees the tilt. Distances are always ground distances, so a flattened
  view looks closer vertically than the rules treat it; area shapes are drawn through the same
  projection.
- The ground's origin is the existing Hero icon anchor (the selected Location's checkmark rect).
  The hero **figure** that moves on the ground is a new, separate element: the checkmark stays
  the Location's marker and is not moved. The same checkmark gains a corpse variant (see the
  floor-loot spec).
- Positions advance on sim time, in the encounter's tick, in a fixed order: the hero chooses a
  target, the hero moves, enemies move, then attacks resolve against the new positions. Ties
  everywhere break by earliest spawn.

**Utility submodule (a separate repo; its own commits and a pointer bump here)**

- `Coordinate` gains the operations the sim needs: dot product, normalisation, move toward
  with no overshoot, clamp magnitude, lerp, rotate, signed angle. Its equality and hash are made
  consistent with each other, and it is not used as a dictionary key.
- `FloatExtensions.Map` gets a defined degenerate case (a zero-width source range returns the
  low end of the target range instead of shifting the bound) and a clamped variant. The existing
  callers are audited so the roll-quality font size keeps its output.
- Area shapes live in Utility as pure functions: a **disk** with an optional inner radius, a
  **sector** with an angle and optional inner radius, and a **rectangle** with length, width and
  pivot. Each answers whether a point is inside given the shape's origin and facing. An **area
  multiplier** scales every size so that area grows by that factor (lengths by its square
  root). An **anchor** enum says where a shape starts: on the target, or on the origin (the
  caster) pointing at the target. Annulus and arc are the inner radius on disk and sector, not
  separate shapes.

**Enemies**

- Enemies are strike-only. The melee and ranged archetypes differ in Strike Range, speed,
  damage type and stats, not in capability. There is no enemy Cast, no mana and no mana regen.
- An enemy spawns on the ground's edge at a bearing chosen in the sim (largest gap between
  living enemies' bearings, plus jitter), keeps it until it falls, and chases the hero's
  current position until it is within its Strike Range. Its stop distance is its Strike Range
  reduced by a small seeded jitter, so it always stands within range.
- An enemy Strikes on its cadence only while the hero is within its Strike Range. Its cadence
  timer keeps its existing carry rule, so walking does not bank a burst of strikes.
- Enemies do not collide. Spawn bearings and the stop jitter spread them; separation steering
  is a later refinement.
- Each archetype declares a **damage type**: Brute physical, Skirmisher magical. A Strike
  deals the enemy's physical or magical damage stat, mitigated by the hero's Armor or Magic
  Resist.
- Enemies are **stat-backed**: each carries a modifiable stat for every stat its archetype
  defines (health, armor, magic resist, its damage stat, attack speed, movement speed). The
  archetype curves supply the base values at the Location's source level. Enemies still have no
  regen and no resource pool. Magic resist is new for enemies, so ADR-0010 is amended.
- A combatant reports the amount it actually lost when it takes a hit, after its own
  mitigation, because only the combatant knows it.

**Hero movement and targeting**

- The hero has a position, an origin, and his movement speed stat. His **Strike Range** is a
  base property of his weapon type, not an affixable stat; an unarmed hero has a default. His
  **Cast Range** comes from the Cast definition, a tuning value until skills exist.
- Hero targeting is a pattern chosen by an enum, with one member, **weighted proximity**.
  Rules, in order: the hero keeps one target until it dies. With no target he scores every
  living enemy as the origin weight times its distance from the origin plus one minus the
  origin weight times its distance from the hero, and takes the lowest score. If his target is
  outside his Strike Range while some other enemy is inside it, he switches to the best-scoring
  enemy inside it. He walks toward his target until it is within his Strike Range, without
  overshooting. With no living enemy he walks back to the origin.
- The origin weight is the behaviour slider that replaces the leash idea. There is no leash and
  no hard cap on how far the hero walks.
- The hero Strikes only when his target is within his Strike Range, on his existing cadence
  and carry rule. The target the arena highlights is this sticky target.

**The Cast**

- A **Cast definition** bundles range, targeting pattern, shape, size, and anchor. It is a
  tuning value now and a skill's data later. The old "three highest-HP enemies" count is
  retired.
- Cast targeting is a pattern chosen by an enum, with one member, **densest cluster**. The hero
  considers only enemies within his Cast Range as anchors. For each, the shape is placed (at
  that enemy, or at the hero pointing at it, per the anchor), and the enemy whose shape contains
  the most enemies wins; ties go to the one nearest the hero, then the earliest spawned. If no
  enemy is in range the Cast does not fire and spends nothing. Otherwise it spends its cost
  and deals the hero's magical damage to every enemy in the shape. Enemies only.
- The Cast stays instant, keeps its cost, cadence and threshold latch, and does not stop the
  hero walking.

**Damage events and numbers**

- The sim raises a typed **hit event** for every hit that lands, carrying the dealer, the
  target, the damage type, the raw amount and the amount actually lost. It covers the hero's
  Strike and Cast and the enemies' Strikes.
- A **damage spread** tuning value rolls each hit within a symmetric fraction of its base
  damage. Weapons will carry real minimum and maximum damage later.
- Damage-number size is `Map` of the amount from zero up to a reference maximum onto a minimum
  and maximum size, clamped, using the clamped `Map` from Utility. The reference maximum is the
  dealer's best raw hit of the **performed damage type**: for the hero, his physical or
  magical damage stat with the spread applied; for hits on the hero, the strongest raw hit of
  that type among the archetypes the Location can field. A zero reference is guarded.
- Magical numbers use a dark blue-purple tint; physical keeps the current colour. The tint
  colours are serialized. The hero's incoming numbers rise at the hero figure in the same
  two colours.
- The damage-number accumulator is keyed by target and damage type, so a frame with both a
  Strike and a Cast on one enemy gives two numbers. Hit flash and hit shake keep listening to
  the enemy's health change, since they only need to know a hit landed.

**Randomness**

- Movement (spawn bearing, stop jitter) and hit rolls (spread) draw from their own
  `IRollSource` streams, separate from the encounter's existing stream and from the loot and
  coin streams. Adding them must not change any existing seeded outcome.

**Documentation**

- ADR-0010 is amended (enemy damage types, enemy magic resist, strike-only unchanged). A new ADR
  records that the simulation owns position and the view projects it, superseding the arena
  spec's "cosmetic only". The glossary is updated: Strike (single sticky target in range, no
  longer lowest-HP), Cast (area, anchor, range), plus new terms Ground, Origin, Strike Range,
  Cast Range, Origin Weight and Cast Definition.

## Testing Decisions

- A good test here asserts external behaviour: where a combatant ends up after N ticks, who is
  hit, how much they lost, and which events fired. It does not assert how the targeting score is
  computed internally or which helper placed a shape.
- **The simulation, through its advance entry point with a fake hero and scripted rolls.**
  This is the single seam for movement, range gating, hero targeting and movement, the Cast's
  area and anchor choice, mitigation by damage type, and the hit events. Prior art:
  `EncounterLifecycleTests`, `EncounterXpTests` and the shared simulation fakes. Determinism is
  tested by running the same seed twice and comparing positions.
- **Utility's own test suite** covers `Coordinate`'s new operations, shape containment on and
  just outside each boundary, the area multiplier, and the clamped `Map` including the
  zero-width range. Each test is paired with a negative control before a green is believed.
- **One pure function for number size and tint**, in the style of the existing damage-number
  helpers, covering the zero, midpoint, saturated and zero-reference cases.
- The sticky-target tests replace the old lowest-HP ones. The arena view is checked by hand
  per `docs/agents/codebase-notes.md`: Send a Run, raise Engagement, watch at x1 and x8,
  Pause, Recall, hero death, Relocate, and a second Play entry under disabled domain reload.

## Out of Scope

- Enemy loadouts: enemies wearing loot rolled at spawn and dropping it on death. It needs
  item levels and affix pools first and starts with a prototype (spec E).
- The effects system, DoTs and buffs, and everything that builds on it (spec C).
- Item levels, affix pools, hero starter stats, level scaling and consumables (spec D).
- Cast wind-up and telegraphs, enemy casts, mana for enemies, and friendly fire.
- Separation steering between enemies, and a stamina cost for movement.
- A visual marker for the Cast's area, and the annulus and arc shapes as separate shapes.
- Death taking equipped gear; ADR-0009 stands as written.
- Skills as such; the Cast definition is a tuning value until they exist.

## Further Notes

**Tuning placeholders.** These are untested starting points, to be tuned in play and by a
headless run: ground radius 10 units, spawn margin 2, unarmed Strike Range 1.5, Brute Strike
Range 1.5 and speed 2, Skirmisher Strike Range 6 and speed 3.5, Cast Range 7, Cast shape a disk
of radius 2, origin weight 0.5, damage spread +-20%, stop jitter up to 20% of range. The hero's
base movement speed needs a starter value, which belongs to the starter-stat work in spec D;
a hero with zero movement speed never closes on a ranged enemy and can only Cast at it.

**Assumptions not explicitly confirmed by the owner:** the "stop and fight a blocker" clause in
hero targeting, targeting being an enum for the hero as well as for the Cast, and the hero's
incoming number reference being the strongest raw hit of that type among the Location's
archetypes.

**What the drift review found** (the arena slice #175-#182 is built; these are its pieces
measured against this spec):

| Mechanism | Vs. | Drift | Recommendation |
|---|---|---|---|
| The arena's view-side walk-in: the arena walk helper, the approach and spawn-point and slot-point math, the visuals asset's ring radii and approach speed (#177, #175) | Sim movement with archetype movement speed | stranded fix: their tests and #177's "hero may strike a walking enemy" rule have no callers once the sim moves enemies | retire-together: the sim-movement ticket deletes them and those two fields |
| Enemies placed in canvas units around the Location checkmark (#176) | A ground with a hero figure and a corpse marker | coverage gap: the checkmark is both the Location marker and the arena anchor, so a moving hero and a corpse marker would both claim it | patch-the-gap: the ground's origin is the checkmark rect, the hero figure is separate, the corpse marker is a checkmark variant |
| Spawn bearing from the slot-angle helper with view-injected jitter (#175, #177's "no draw from the sim's rolls") | Sim-owned spawn bearing and stop jitter | coverage gap: the sim needs deterministic rolls, and drawing from the existing stream would reorder seeded outcomes | patch-the-gap: movement gets its own roll stream |
| The strike-target peek as the lowest-health choice (#182) | The sticky target | stranded fix: three strike-target tests assert the old rule | retire-together: rewrite them with the targeting slice; keep the member and the arena's per-frame poll |
| Damage numbers read from the enemy's health-change event (#181) | The typed hit event | stranded fix: that handler cannot see type, dealer or the hero's incoming hits | retire-together: move numbers to the hit event, key the accumulator by target and type; flash and shake keep the health event |
| The arena spec's premise "cosmetic only, the sim stays position-free" (#174) | This spec | swap residue: the built spec states the opposite | patch-the-gap: amend its section 1 with a pointer here |

Cleared: #178 (view-only), #179 and #180 (any health drop still fires), and the pure helpers for
facing, the dying fade and damage accumulation.

**Suggested order for `/to-tickets`** (to be settled there, with its expand/migrate/contract
rule for the swap): the Utility ticket first, since everything depends on `Coordinate`, the shapes
and `Map`; then sim positions and enemy movement, which deletes the view-side walk-in in the
same slice; then hero movement and targeting, stat-backed enemies with damage types, and the
typed hit events, which are independent of each other once positions exist; then the Cast
area, which needs positions and the hit events; then the damage-number rework, which needs the
hit events and the clamped `Map`. Documentation amendments ride with the slice that causes
them.

**Risks.** Cast value now depends on enemy clumping, so shape sizes and spawn spread need
tuning together. Ranged enemies make the hero spend time walking instead of striking, which
lowers his damage output and belongs in the difficulty spec's numbers. Float math in the sim is
deterministic for a given build, which is what the seeded tests rely on. The utility submodule
is a shared checkout on disk, so the Utility ticket runs in a worktree.
