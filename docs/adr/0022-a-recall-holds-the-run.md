---
status: accepted
---

# A Recall holds the Run; the map, not the Run, decides what can be opened

A Run had two states, `InTown` and `InField`, and ended in a Recall or a Death. `InField` did two jobs: it
said the simulation was running, and it locked the Town Stops away. The Town Portal
(`dev/specs/2026-10-16-town-portal-design.md`) needs a hero standing in Town, using the Town Stops, while
his Run waits at a Location to be resumed. Both jobs of `InField` break at once.

## Decision

1. **Recall holds the Run; Return ends it.** A Recall spends a Portal Scroll and holds the Run: the
   Encounter, its enemies, spawn timer and ground freeze, nothing settles, and the Town Portal resumes it
   exactly as it was. A Return (the Field Exit on a live Run) and a Retreat end it as Recall used to, and
   the Encounter is gone. The word Recall moves to the new action; the old one is Return.
2. **A Run is none, live or held.** That state decides only the Run's rules: Send, Relocate, Recall,
   Return, ticking, saving. A held Run ends only from the Field (after a resume or a Relocate) or by
   quitting.
3. **What can be opened follows the map that is up.** The Town Stops belong to the Town map, the Combat
   Panel and the ground to the Field map, and the hero's own panels to both. Panels never read the Run's
   state. The Combat Panel is the Field map's Side Panel and shows on `None` there.
4. **A held Run does not survive a quit.** The World is never saved, so quitting ends a held Run like
   quitting mid-Run. Only the open portal is saved, and after a relaunch it Sends a fresh Encounter.

## Considered options

**A Recall that ends the Run and leaves a portal that re-Sends.** Keeps two Run states. But the point is
to heal mid-fight and come back to the same fight, and a re-Send rolls a fresh Encounter and Population.
Rejected.

**A third phase that still locks the Town Stops by Run state.** It would have to unlock them for the held
state only, which spreads the same exception across every panel. Splitting the Run's state from the map
removes the exception instead. Rejected.

**Save the Encounter so a quit can resume it.** It would put the World, which is never saved, into the
Hero save. Rejected; a fresh Encounter after a relaunch is the cost.

## Consequences

- `RunPhase` gains a member and loses its panel-locking role. `InventoryContextState`'s Field
  reachability reads the map.
- The menus' simulation freeze is renamed, so "held" means only the Run's state.
- The Brain's Recall Intent is a Retreat Intent: a hero never opens a portal on its own.
- The Hero save carries one portal flag; which events close a portal is left to triage (spec, Open
  questions).
