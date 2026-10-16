# Hero travel - one hero figure per map walks where the player sends him, as presentation

Date: 2026-10-16
Status: Settled in the 2026-10-16 grilling (an extension of the arena). The Town Portal that came out of the
same grilling is its own spec, `dev/specs/2026-10-16-town-portal-design.md`, and builds on this one.

## Problem Statement

The hero only exists on screen inside an **Encounter**: `EnemyArena` binds the hero figure to the live
`EncounterSimulation` and draws it at the Origin of the selected Location. Everything else is instant and
figureless. A **Town Stop** opens with nobody walking to it, the Town Exit flips the Minimap with no one
leaving, a **Send** drops the hero onto the Location, a **Relocate** snaps him to the new Origin, and a
Death cuts straight to Town. The player never sees one persistent hero moving through the game.

## Decisions

**Travel is shown, not simulated (ADR-0021).** Every click takes effect the moment it is made, as today:
a Town Stop opens, a Send starts the Run, a Relocate swaps the Encounter, a Return ends the Run. The
**Hero figure** then **Travels** there, and nothing waits for it to arrive. The Run's sim time is not
held during the walk in: the first spawn delay runs while the figure walks, which is fine because walks are
short. If it shows, holding the Encounter until arrival is the fix (see Out of scope).

**One hero figure per map.** The Town map and the Field map each own a hero figure, and each figure fades
with its map. The Exits are not at the same place on screen, so the switch is a crossfade between two
figures, never one figure crossing over. The Field figure is lifted out of `EnemyArena`'s Encounter binding:
it Travels itself, and once it has arrived it draws the sim's `HeroPosition` for as long as the Encounter
runs. A Relocate Travels it from where it stood in the fight, not from the old Location's centre. The arena
view keeps the enemies only.

**Where the figure stands when it has not walked there.**

| Map | When | Where |
| --- | --- | --- |
| Town | entering the game with Play, a hero load, a Respawn | the **Waypoint** |
| Town | after a Return or a Retreat | the **Town Exit** |
| Field | coming in through the Town Exit, Run or not | the **Field Exit** |

The Waypoint is its own point on the Town map. It sits on the Town Exit today, and a campfire or a portal
later only moves it.

**The latest click wins.** A click on a Town Stop opens it at once and turns the figure towards it, so
Healer then Stash mid-walk changes course to the Stash. The Stash, Vendor and Healer hotkeys are clicks.
Nothing is queued.

**An Exit starts the crossfade on the click.** A click on an Exit Travels the figure to it and starts the
Minimap crossfade at once. The collapsing panel already takes input away, so nothing can retarget the
figure. The fade is long enough to watch the hero walk. A fixed length means a far walk may not reach the
Exit before its map is gone; that is accepted. To keep a long fade from lingering in the in-between state,
`SimplePanel` gets an easing per direction: the old map stays nearly opaque and drops late, and the new one
stays nearly clear and rises late.

**The Field Exit on a live Run is a Return.** It ends the Run with everything kept, as the ToTown button's
Recall does today. Retreat (Auto-Recall renamed) is the same walk out, made by the hero.

**Walking speed.** The hero's `MovementSpeed` stat, through the arena's canvas units per arena unit, so a
walk and a fight move at one pace and faster boots walk faster in Town too. `SimSpeed` is ignored: Town has
no sim, and Travel is not the fight. Pause freezes the fight, not Travel.

**Death is shown before it is left.** The Death is settled the moment the hero goes down, as today: the
penalty, the bag set aside as the Corpse, the Run ended. Only the way back waits:

1. The figure turns to lying down, and the view slows: a short-lived multiplier on `SimDelta` (0.25x for
   1.5 s real time, both tuning), so the last damage number hangs in the air. The saved `SimSpeed` slider
   is never touched. The sim has already stopped, so enemy figures stand frozen.
2. The figure becomes the corpse icon where it fell.
3. The Death notice opens: "You fell in Thornwood. You lost 120 XP and a fee of 34 iron. Your bag lies where
   you fell; go back to pick it up." The amounts are the Run's result, the fee in its coins.
4. **Respawn** revives the hero at full Health and Resource and crossfades to the Town map, the figure at
   the Waypoint. Respawn is the only way on: Enter and Esc both press it.

The revive moves from the settlement to Respawn, so the globes do not refill behind the notice. A hero saved
while down (quitting at the notice) is revived on load. Quitting there does not dodge anything, because the
penalty was already settled.

**The Corpse is drawn where he fell.** The Corpse remembers its arena position, and the existing
`CorpseMarker` moves from its Location button to that point, projected around the Location's anchor. It is
seen on the Field map before Sending there. Recovery is unchanged: a Send to that Location picks the bag up
at once, and the marker goes, or stays if something did not fit. A Corpse from a save without a position is
drawn at the Location's centre. Several Corpses, and recovering one by a click, are in the Icebox
("Corpse per Location").

**A Popup opens with a button selected, and Enter presses it.** A choice Popup selects its yes option,
unless that option would delete or discard something; then it selects the no option. A **Notice** (one
button, nothing to choose) selects its button, and Cancel presses it too.

| Popup | Selected on open | Cancel |
| --- | --- | --- |
| Death notice (Notice) | Respawn | Respawn |
| Entry notice (Notice) | its button | its button |
| Hero delete | Cancel | closes |
| Save & Exit, in Town or with a Run | Back | steps back |
| Pause Menu | Resume | closes |
| Hero name | Confirm | closes |

Save & Exit selects Back in Town too, because a Town ground (#285) would be lost.

## User stories

1. As a player, when I enter the game I see my hero standing at the Waypoint in Town.
2. As a player, I click the Healer and its panel opens at once while my hero walks over to it.
3. As a player, I click the Stash while my hero is still walking to the Healer and he turns and walks to
   the Stash, which opens at once.
4. As a player, a Town Stop hotkey does the same as clicking the Stop.
5. As a player, I click the Town Exit and watch my hero walk towards it while the Town map fades slowly, and
   the Field map comes up with him standing at the Field Exit.
6. As a player, I click a Location and the Run starts, my hero walks from the Field Exit to the Location
   and fights there.
7. As a player, I click another Location mid-fight and my hero walks there from where he was fighting,
   while the enemies he left fade away.
8. As a player, I click the Field Exit during a Run, the Run ends with everything kept, and I watch my hero
   walk out while the Field map fades; in Town he stands at the Town Exit.
9. As a player, when my hero Retreats he walks out the same way.
10. As a player, my hero walks faster when his movement speed is higher.
11. As a player, pausing freezes the fight but not a hero who is walking.
12. As a player, when my hero dies I see him fall and the last hit hang in the air before a notice tells me
    where I died, what I lost and how to get my bag back.
13. As a player, I press Enter or Esc on the Death notice, or click Respawn, and I am back in Town at the
    Waypoint with full Health.
14. As a player, my Health globe stays empty until I Respawn.
15. As a player, when I go back to the Field I see my corpse where I fell, and Sending there picks my bag
    up.
16. As a player, if I quit at the Death notice, I come back alive and lost what the Death cost, once.
17. As a player, a corpse from an older save still shows, at its Location.
18. As a player, pressing Enter on any popup does the safe thing: deleting a hero or leaving with loot on
    the ground needs a deliberate click.
19. As a player, a long Minimap crossfade shows the map I am leaving until late, then switches quickly.

## Implementation Decisions

- `Utility`: `AbstractButton` implements `ISubmitHandler`, and `OnSubmit` goes through the same path as a
  click (interactable check, feedback, `OnClick`). `InteractiveElement` already clears its selection on
  pointer-up unless `staySelected` is set, so only a Popup's selection survives for Enter to press.
- `Utility`: `SimplePanel` serializes an `Ease` for expand and one for collapse (defaults: today's
  `InOutQuad` and `InQuad`) and widens `FadeDuration`'s range past 1 s. The two Minimap faces use a long fade
  with `InExpo` both ways. Bumped through `dev/bump-utility.sh`.
- A hero figure component per map (one scene object each, never pooled) owns Travel: a target point, a
  speed read from the hero, and arrival. The Field figure switches to drawing `HeroPosition` through
  `ArenaProjection` once it has arrived and an Encounter is bound, and back to Travel on a Relocate,
  Return or Retreat. `EnemyArena` stops binding the hero figure.
- Travel targets are scene anchors: each Town Stop toggle, both Exits, the Waypoint, and each Location's
  Origin anchor. Nothing in the sim reads them (ADR-0018 is unchanged inside an Encounter).
- Rename in code to the settled terms: `RunState.Recall` becomes `Return`, Auto-Recall and
  `RecallRequested` become Retreat. The Town Portal spec reuses the name Recall for the new action.
- `RunPhasePanel` no longer brings the Town face up on a Death; Respawn does. A Return and a Retreat still
  bring it up through the phase change, with the Field figure walking to the Field Exit during the fade.
- The Death beat's slow-down is a transient factor in `SimulationService.SimDelta`, set by the Death and
  cleared by Respawn or a hero load, never by the `SimSpeed` slider.
- `RunSettlement` stops reviving; `ReviveIfDown` moves to Respawn and to hero load.
- `CorpseDto` gains the fall position. A missing one loads as the Origin. `CorpseMarker` repaints on
  `CorpseChanged` as today and places itself through the arena projection.
- Each Popup names its selected button and selects it when enabled. A Notice base or flag routes its Cancel
  layer to its one button.
- Tests: Travel retargeting and arrival, the start-point table, the `SimDelta` factor, the revive on
  Respawn and on load, the Corpse position round-trip, the Popup selection and the Notice's Cancel run as
  EditMode tests. The fade easing and the crossfade length are judged in Play Mode by eye.

## Out of scope

- The Town Portal, Recall, the Portal Scroll, a held Run, the Town map/Field map panel scopes and the
  Combat Panel as a Side Panel: `dev/specs/2026-10-16-town-portal-design.md`.
- Holding the Encounter until the figure arrives. Revisit if a walk-in gets struck before it arrives.
- A fade that lasts exactly as long as the walk.
- Several Corpses, and recovery by clicking the Corpse (Icebox: "Corpse per Location").
- Pathing around obstacles: a figure walks straight.
