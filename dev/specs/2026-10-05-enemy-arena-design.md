# Enemy Arena

Date: 2026-10-05
Status: Built — epic #174, slices #175–#182. Pruning the four feedbacks is #201.
Depends on: `EnemyHealthBarPool` / `EnemyHealthBarDisplay` (#60, #94), `EncounterSimulation.EnemySpawned` / `EnemyDefeated`.

## Problem Statement

The combat panel shows enemies as a vertical list of health bars (`EnemyHealthBarPool`). A bar
says how hurt an enemy is and nothing else: there is no sense of *how many* are on the hero, where
they are, which one the hero is hitting, or when a hit lands. The player cannot *see* the fight.

The simulation is headless and position-free by design: `EncounterSimulation` spawns an `Enemy`
straight into `Enemies`, strikes on a timer and resolves everything per tick. Nothing in it
moves, and nothing should — the sim is deterministic and seeded, and the tests depend on that.
So the answer is a view layer, not a sim change.

## Solution

Replace the list with an **arena**: each living enemy is an `EnemyView` — a sprite and a health
bar — standing on an ellipse around the hero's anchor. Views walk in from outside the ring,
stop at the ring, face the hero, and show four candidate feedbacks (hit flash, hit shake, damage
numbers, target highlight) behind independent switches so they can be compared in play and pruned.

### 1. Cosmetic only

> **Superseded.** The simulation now owns position and this view only projects it: see
> `2026-10-08-spatial-combat-design.md` and ADR-0018. The ring, the walk-in and the view's own
> jitter described here, and in sections 3 and 4, were deleted by #208; what remains of this
> spec is the pooling and binding, the death fade, the four feedbacks and the facing dead zone.
> The text below is the original decision, kept as history.

Movement is **view-only**. The sim keeps spawning an enemy straight into `Enemies`, its
`StrikeTimer` keeps its random offset, and the hero may strike an enemy that is still walking in.
That is accepted: the walk is short (spawn just outside the ring, fast approach) so the mismatch
is a beat, not a lie. The ring is the *engaged* position, nothing more. No arriving phase is
added to the sim; if the mismatch proves ugly that is a later spec.

Everything in the view uses the **sim's** time, not wall time: the same
`deltaSeconds * hero.Behaviour.SimSpeed` that `SimulationService.Tick` feeds `Advance`. At ×8
the enemies walk, flash and fade eight times as fast, and at SimSpeed 0 they freeze with the
sim. The view reads `SimSpeed` the way `BehaviourSlidersPanel` reaches the behaviour — an
implementation detail for the ticket, not a design question.

The view's RNG (spawn angle, jitter) is `UnityEngine.Random`, **never** the sim's `IRollSource`.
Drawing from the sim's rolls would shift every seeded outcome.

### 2. The arena replaces the list

`EnemyHealthBarPool` becomes the arena's pool (rename to `EnemyArena`; the binding logic —
reference-compared in `Update`, `EnemySpawned` takes a view, `EnemyDefeated` retires one, seed
from `Encounter.Enemies` on bind, release-all when the Encounter goes null — is kept as is, for
the reasons in its own header comment). Its prefab changes from `EnemyHealthBarDisplay` to
`EnemyView`, and its `container` becomes the arena root instead of a layout group. The list's
layout group and scroll rect are removed from the Combat Panel; the "list scrolls past the
panel" story from `2026-09-25-enemy-hp-bar-binding-design.md` no longer applies.

`EnemyView` composes what exists:

- **Root** `RectTransform` — moved by the arena. Never flipped.
- **Sprite** child `Image` — the only thing flipped (§4).
- **`EnemyHealthBarDisplay`** child — unchanged, reuses `ResourceDisplay`, bound to
  `Enemy.HealthResource`. The name label is optional here; a bar over a small sprite may not
  want it.
- **Highlight** child `Image` — the target ring (§6), off by default.

A per-archetype `EnemyVisuals` asset maps `EnemyArchetype` to sprite, size, approach speed and
ring radii. Both archetypes are strike-only in the sim, so a different stop distance and speed
is the only identity a Skirmisher can have against a Brute. Cheap, and worth having.

### 3. Placement: an ellipse of stable slots

Positions live in arena-local space, around the **hero anchor**, on an ellipse (rx > ry reads
better in a flat view than a circle). A new enemy picks its angle at spawn — the angle in the
largest gap between the living enemies' angles, plus a small jitter — and **keeps it** until it
falls. Re-spreading evenly on every kill was rejected: it makes the whole ring slide whenever
anything dies. A Pack batch spawning on one tick works with this unchanged, because each pick
sees the ones already placed.

Spawn point is the slot angle projected out by a margin beyond the ring. Approach is a straight
line toward the anchor that stops on the ellipse (stop test in ellipse-normalised space:
`|(x/rx, y/ry)| <= 1`). With the Engagement target raised, enemies can outnumber the ring's
comfortable circumference; they may overlap, and that is accepted over a collision system.

Draw order is the sibling index sorted by Y, lower on screen in front, re-sorted only while
something moves.

### 4. Facing

`scale.x = +1` when the hero anchor is to the **right** of the enemy, `-1` when to the left, on
the **sprite child only**: a flipped root would mirror the bar's fill direction and its text.
A dead zone (`|dx| < deadZone` keeps the current sign) stops an enemy standing directly above
or below the hero from flickering. Art is assumed authored facing right.

A pooled view is **fully reset on release**: position, sign, flash, highlight, dying state.
A stale `scale.x = -1` on the next enemy is the bug to design against.

### 5. Death has a view lifetime

`EnemyDefeated` no longer releases the view immediately; it moves it to a **dying** state
(short fade, sim-time scaled), and the arena releases it when that ends. The killing blow's
`CurrentHasChanged` fires *before* `EnemyDefeated`, so its flash and number have already been
queued — the view must still be allowed to show them while dying. A dying view is detached from
the enemy (health bar unbound) and is not a candidate for the highlight.

When the Encounter goes null (hero death, Recall, Relocate) the arena releases **everything at
once**, dying views included. That is deliberate: the Run ended, so a lingering fade would be
wrong.

### 6. The four feedbacks

Four independent `[SerializeField] bool` switches on the arena — `hitFlash`, `hitShake`,
`damageNumbers`, `targetHighlight` — so all four can run together, be compared, and be deleted
down to the winner. Pruning is a follow-up, not part of this spec; the point of the switches is
that removing a feature is deleting one component.

- **Hit flash.** The view subscribes to its enemy's `HealthResource.CurrentHasChanged` and tints
  the sprite white-to-normal over a short sim-time ramp. **No sim change.** That event raises
  *before* `CurrentValue` is written, so a handler reads `previous` and `current` from its
  arguments, not from the enemy.
- **Hit shake.** The same event kicks a short decaying positional shake — a tween on the
  **sprite child only** (never the root, so the health bar stays still and the arena's own
  movement is not fought). It is a local offset on the sprite, independent of the facing flip,
  on sim time, and it resets to zero offset on release like every other pooled state. Kept
  separate from the flash so either can be pruned alone; they share the event, not a component.
- **Damage numbers.** Same event; the amount is `previous - current`. A coarse frame runs up to
  `MaxTicksPerAdvance` ticks at once (×8), so numbers are **accumulated per enemy and flushed
  once per frame**, not one per event. A pooled `DamageNumber` (TMP) rises and fades from the
  enemy's position. Physical and magical are indistinguishable at this seam — one colour. Telling
  them apart needs a sim event and is out of scope.
- **Target highlight.** The one sim seam in this spec: `EncounterSimulation` exposes
  `public Enemy StrikeTarget => LowestHealth();`. It is a pure peek at the existing private
  targeting function — no new state, no new event, and it cannot disagree with the Strike that
  follows because it *is* the Strike's selection. The arena polls it once per frame (it already
  polls the Encounter reference) and shows the ring on that view. It means "who the next Strike
  hits": it can change between swings as health changes, and it is null with no living enemy.
  The Cast's N highest-health targets are not highlighted.

## User Stories

1. As a player, I want each enemy to appear as a figure around my hero, so that I can see how
   many are on me at a glance.
2. As a player, I want enemies to walk in and stop around the hero, so that a new arrival reads
   as an arrival and not a pop.
3. As a player, I want each enemy to face the hero, so that the scene reads as a fight.
4. As a player, I want a hit to flash and shake the enemy, a number to rise and the next target
   to be marked, so that I can tell what my hero is doing — and I want to try all four before
   keeping any.
5. As a player, I want a defeated enemy to fall away instead of vanishing, so that a kill is
   noticed.
6. As a player, I want fast sim speeds to move everything faster and pause to freeze it.

## Out of Scope

- Any sim change beyond `StrikeTarget`. No arriving phase, no positions, no enemy-strike event.
- Enemy strike animation / lunge, and a hero sprite.
- Highlighting Cast targets; per-damage-type number colours.
- The rarity border (still unbound, per the 2026-09-25 spec).
- Collision avoidance between enemies.
- Pruning the four feedbacks.

## The anchor

The anchor is the **Hero icon** — the `ToggleCheckmark` image of the selected Location. The
arena holds a reference to the Locations' `ToggleGroup` and resolves the anchor from
`ActiveMember`: that member's `ToggleCheckmark` image rect. (If the checkmark proves the wrong
thing to find, the same hook can look up a dedicated hero image instead; the arena only needs a
`RectTransform`.)

- The checkmark only shows on the active member, which is exactly the one the arena follows, so
  the "image disabled when off" behaviour never bites.
- The anchor **moves** (a Relocate selects another Location) and **goes null** (the group resets
  with its parent panel one fade after the Run ends). The arena resolves it each frame, places
  views in arena-local space from the anchor's current position, and treats a null anchor as
  "hold, and place nothing new" — the Encounter going null releases the views anyway.
- The anchor is a small map icon, so ring radii are **serialized constants in canvas units**,
  not derived from the anchor's rect. The arena root must sit **outside** the minimap's
  `InFields` face: that face is a `SimplePanel` with a `CanvasGroup`, and enemies parented
  under it would fade and clip with it.
- Order of operations is a risk, not a given: `LocationToggle.OnToggle` calls `Send`, and whether
  the group's `ActiveMember` is already set at that moment is unverified. The arena tolerates
  either order by resolving the anchor per frame rather than at bind time.

## Risks

- **Prefab reshaping.** `EnemyHealthBarDisplay` was laid out to stretch in a vertical group.
  Under the arena it is a fixed-size child; its anchors need rework or the bar renders wrong.
- **Pooled-state leaks** (§4): position, flip, flash, dying. Reset in one `Release` path.
- **Clipping.** If the arena sits under a `Mask` / `ScrollRect` or the minimap face, enemies
  clip or fade with it. See "The anchor".
- **Event volume.** Each tick raises `CurrentHasChanged`; a view's own subscription adds to the
  `ResourceDisplay`'s. Bounded (see 2026-09-25 risks); numbers are aggregated for that reason.
- **One-frame lag.** `GameLoop` ticks the sim after every `Update` of the frame, so views read
  the previous frame's state. Invisible at frame rate; stated so nobody chases it.
- **Subscription symmetry** under disabled domain reload: subscribe on `Bind`, unsubscribe on
  `Unbind`/`Release`, driven from the arena's `Update`-bound path — not `Awake`/`OnDisable`
  (`docs/agents/codebase-notes.md`).

## Slicing

1. Pure layout math as a static class in a testable assembly: slot choice, approach step and
   stop, facing sign with dead zone, damage accumulation. EditMode tests.
2. `EnemyView`, `EnemyVisuals`, `EnemyArena` replace `EnemyHealthBarPool`; the list layout is
   removed; views stand on the ring around the Hero icon (anchor from the group's
   `ActiveMember`) facing it. Blocked by 1.
3. Views walk in from outside the ring, on sim time. Blocked by 2.
4. Death fade. Blocked by 2.
5. Hit flash. Blocked by 2.
6. Hit shake. Blocked by 2.
7. Damage numbers. Blocked by 2.
8. Target highlight + `EncounterSimulation.StrikeTarget` and its test. Blocked by 2.

After 2, tickets 3-8 are independent of each other.

## Verification

Per `docs/agents/codebase-notes.md`: `dotnet build` lies here; use the `unity-mcp` bridge or
`-runTests -batchmode`, with a negative control before believing a green. Layout math and
`StrikeTarget` are EditMode-testable. The arena needs a by-hand Play pass: Send a Run; raise
Engagement; watch ×1 and ×8; Pause (SimSpeed 0); Recall; hero death; Relocate; second Play
entry (disabled domain reload). Confirm pooled views carry no stale flip or flash after reuse.
