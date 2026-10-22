# Town Portal - a Recall spends a Portal Scroll and holds the Run until the portal takes the hero back

Date: 2026-10-16
Status: Settled in the 2026-10-16 hero-travel grilling. Builds on `dev/specs/2026-10-16-hero-travel-design.md`
(Travel, the Exits, the Waypoint, Return, and Auto-Recall gone); land that first.

## Problem Statement

Leaving the Field always ends the Run. A hero low on Health has two choices: fight on, or leave and lose the
half-fought Encounter, its ground and its **Population**'s progress. There is no way to step out, heal, sort
the bag and come back to the same fight. The ToTown button also mixes two things: the walk out and the
end of the Run.

## Decisions

**Two ways out of the Field, with different costs.**

| Action | How | Run | Encounter | Cost |
| --- | --- | --- | --- | --- |
| **Return** | the Field Exit, on a live Run | ends, everything kept | gone | the fight restarts next time |
| **Recall** | `B`, the HUD button, right-click a Portal Scroll | **held** | frozen as it was | one **Portal Scroll** |

The hero never leaves on his own: Auto-Recall is gone (hero-travel spec).

**A Recall holds the Run (ADR-0022).** It spends one Portal Scroll from the Inventory at once and opens a
**Town Portal** beside the Field figure. The portal takes about 0.5 s of sim time to open (tuning; it
scales with sim speed and stops on pause). Through the opening the Run stays live: the Encounter ticks, the
hero keeps fighting and hits still land. That half second is what a Recall risks (the Run-risk spec,
`dev/specs/2026-10-17-run-risk-levers-design.md`). Only a Death cancels an opening Recall, and the scroll
is gone with it. Return, Relocate and a second Recall are refused while the portal opens. Once it is open
the Run is **held**: the Encounter, its enemies, its spawn timer and its ground are frozen as they were,
and nothing settles. Then the figure moves into the portal (about 0.5 s, shown only, ADR-0021), the Field
map crossfades, and the Town map comes up with the figure at the **Waypoint** beside the portal's other
end. A Recall is refused in Town (no live Run), while the hero is down, during the death beat, and with no
Portal Scroll in the Inventory. The Stash does not count. A **Complete** Location (the Location
unlocks spec, #294) still allows a Recall: a Return there is free and a Recall costs a scroll, which is a
choice. The Run is held in its Complete phase, and the resume lands back in the emptied, Complete Location.

**The portal resumes the Run.** A click on the Town portal, or on the portal's marker on the Field map,
crossfades to the Field map with the figure where it stood. The Run is live again exactly where it was held,
and the portal closes. A Recall and a resume are one Send: the Population's progress carries on, so clearing
the rest still Completes the Location.

**While the Run is held:**

- The **Town Exit** brings up the Field map with the figure at the Field Exit. The portal stays open.
- The **Field Exit** brings back the Town map and ends nothing.
- A click on **any Location**, the portal's own included, is a **Relocate**. The held Encounter is
  discarded and a fresh Population is rolled. The portal stays open, leading to a fresh Encounter at its
  own Location (below). Losing the held fight is the cost of walking out instead of taking the portal.
- A held Run ends only from the Field: by a Return or a Death after it is resumed or Relocated. Quitting also ends it (below).

**A portal stays open until it is used or a new one opens** (Diablo II; resolved 2026-10-18). The portal
is the hero's, not the Run's: it remembers its Location, and there is only ever one. A new Recall replaces
it. A Relocate from held, a Send, a Return, a Death and a quit all leave it open. Used while the Run is held
at its Location, it resumes that Run. Used with no held Run (after a Relocate from held, a later Run's end,
or a relaunch), it Sends a fresh Encounter at its Location, with the figure arriving through the portal,
not walking in. It is used from the Town map only then: the Field-map marker shows only for a held Run.

**The Run's state and where the hero is are two things.** The Run is none, **live** (the simulation ticks)
or **held**. That decides only the Run's rules: what Send, Relocate, Recall and Return may do, whether the
simulation ticks, and when the hero saves. What can be opened follows the map that is up, never the Run:

| Scope | Interactions |
| --- | --- |
| Hero, on both maps | Inventory, Equipment, stat sheet; the quest log later (M2, a Side Panel) |
| **Town map** | Stash, Vendor, Healer, the Town ground |
| **Field map** | the **Combat Panel**: behaviour sliders, enemy health bars, encounter stats, the ground tab |

The Town Stops work in Town during a held Run. That is the point: heal and sort the bag.

**The Combat Panel is the Field map's Side Panel.** It derives its visibility from the **Inventory
Context** like the Town Stops' panels: shown on `None` and `Ground` while the Field map is up. Opening the
hero's bag (`Hero`) and closing it again brings the Combat Panel back by itself. It no longer reads the Run
phase. Its ground tab shows the Run's ground only while the Run is live. With no Run or a held one it is
empty: a held Location's loot is seen by going back there. The behaviour sliders stay on the Combat Panel
for now. They are the hero's, so they can move to the hero scope later without changing a rule.

**Grounds.** The held Location's ground is frozen with its Encounter. The Town ground (#285) is wiped on a
Send, and a resume is not a Send, so it survives a Recall and the trip back.

**Quitting with a portal open.** The World is never saved, so a held Run cannot survive a quit. Quitting
ends it the way quitting mid-Run does: what the hero picked up is kept, the rest is gone. The hero saves
whether a portal is open, and the portal saves with its Location: after a Relocate from held that is not
the hero's selected Location. After a relaunch the hero stands at the Waypoint with the portal up. Using it
Sends a fresh Encounter at that Location, with the figure arriving through the portal, not walking in.

**The hero regenerates in Town while the Run is held** (resolved 2026-10-18). Only the Encounter is frozen;
the hero is in Town and regenerates as with no Run. Only the Pause and the menus' freeze stop it.

**The Portal Scroll.** A new `ConsumableType.PortalScroll`, appended as 4, which stacks. An identification
scroll later gets its own member, not a shared Scroll type. `Arrow` is commented out of the enum, the
catalog and the distributions, and its number 1 stays reserved. The Healer stocks at least 2 Portal
Scrolls on every Restock, on top of its rolled consumables. They are priced like other consumables and
drop as Loot. Consumable weights are Potion 40, Portal Scroll 50, Book 10 (tuning). A new hero starts with
no scrolls but with coins enough for about 3 (tuning).

**The Recall button.** A HUD button with the Inventory's Portal Scroll count on it. It is disabled when
the count is 0 or a Recall would be refused. `GameAction.Recall` (appended as 12, rebindable) defaults to
`B`. A right-click on a Portal Scroll in the bag is the same Recall. Right-clicking other consumables does
what it does today.

## User stories

1. As a player, mid-fight I press `B` and my hero steps into a portal; I am in Town with the portal beside
   him and one scroll fewer.
2. As a player, I heal at the Healer, sell at the Vendor and stash loot while my Run waits.
3. As a player, I click the portal in Town and I am back exactly where I was, the same enemies at the same
   Health, and the portal is gone.
4. As a player, I can also click the portal's marker on the Field map to go back.
5. As a player, the Recall button shows how many Portal Scrolls I carry and is greyed out when I have none.
6. As a player, `B` does nothing in Town, while my hero is down, or with no scroll in my bag.
7. As a player, right-clicking a Portal Scroll in my bag Recalls the same as `B`.
8. As a player, with a portal open I can walk out to the Field map and back without losing the portal.
9. As a player, with a portal open, clicking a Location starts a fresh fight there; the portal stays open
   and now leads to a fresh fight at its own Location.
10. As a player, clicking the Field Exit during a live Run ends it and I walk home; the fight is gone.
11. As a player, clearing the rest of a Location after a portal trip still Completes it.
12. As a player, after a Location Completes I can still Recall to heal or sell, and the portal brings me
    back to the emptied, Complete Location.
13. As a player, opening and closing my bag in the Field brings the Combat Panel back by itself.
14. As a player, the Combat Panel's ground tab is empty unless I am in a live Run at the Location.
15. As a player, coins I left on the Town ground are still there when I come back through a portal.
16. As a player, if I quit with a portal open, the portal is still there next time and takes me to a fresh
    fight at that Location.
17. As a player, the Healer always has Portal Scrolls to sell, and I find them as loot.
18. As a new player, I start with no scrolls but enough coins to buy a few.
19. As a player, after I press `B` my hero keeps fighting while the portal opens, and if he dies before it
    is open, the scroll is gone and it is an ordinary Death.
20. As a player, a portal I opened stays open through later Runs until I use it or open a new one.
21. As a player, my hero regenerates in Town while my Run waits.

## Implementation Decisions

- `RunPhase` becomes none / live / held. The menus' freeze (`SimulationService.SetHeld`) keeps its own flag
  and is renamed, so "held" means only the Run's state.
- A Recall is a `RunState` transition, live to held, through an opening the simulation times: the scroll
  is taken from the Inventory through a transaction when the Recall starts, so a refused Recall spends
  nothing; the Run goes held when the opening's sim-time timer runs out (a `GameConfig` value), and a Death
  first ends the Run as usual. The figure's walk into the portal is presentation after the hold. A resume is held to live. A Relocate from
  held discards the frozen Encounter.
- A resume selects the Location's toggle without re-Sending. `LocationToggle.OnToggle` reconciles with
  the Run instead of always commanding it: if the Run is already live at its Location, it does nothing.
  The resume calls `Run.Resume()` first, then `SyncToggle(true)` on that toggle, so the arena anchor (the
  group's active member) and the highlight follow while the `OnToggle` this fires finds nothing to do. A
  Location click while the Run is held is still a Relocate, because the guard covers only a live Run. A
  click on the selected Location already stops in `AbstractToggle.Apply`. A portal used with no held Run
  (no Run) falls through to `Send`, so it needs no path of its own. A selection that mirrors the Run's
  Location instead of commanding it would fit ADR-0013 better, but it also moves `EnemyArena`'s anchor,
  `CorpseMarker` and the group's reset, so it is not part of this.
- `InventoryContextState`'s reachability reads the map that is up, not the Run phase. A `Field` side of
  that state replaces `IsReachableInField`. The coin-overflow work (`integration/coin-overflow`) already
  makes `Ground` reachable in Town; reconcile with it.
- The Combat Panel is already a `SidePanel`. `FieldFacePanel` requests its context when the Field face
  appears, which opens the Hero Panel with it. It changes to also show on `None` while the Field map is up.
  Then the face requests nothing, the Hero Panel stays closed until asked for, and closing it brings the
  Combat Panel back. Its ground tab reads the Run's ground only while the Run is live.
- The Hero owns the open portal as its Location (none when closed). The Hero save gains a
  `townPortalLocationId` (empty when closed); `HeroMapper` round-trips it, and an id the Location registry
  no longer knows loads as no portal. Loading with a portal and no Run shows it at the Waypoint. Using it
  Sends to its Location.
- `SimulationService.Tick` regenerates the hero while the Run is held; only the Pause and the menus' freeze
  skip it.
- The portal's Field-map marker sits where the hero stood when he Recalled, projected around that
  Location's anchor, as the `CorpseMarker` is.
- `ConsumableType`, the Location loot tables, `Consumable Type Distribution.asset` and the Healer's Restock
  change as above. The starting coins are a `GameConfig` value.
- Tests: Recall refusals and the scroll spent, the opening (the Encounter ticks through it, a Death in it
  ends the Run with the scroll spent, Return and Relocate are refused in it), held-Run freezing (no tick advances the Encounter), resume
  continuity (same enemies, same Population progress), Relocate from held, Field Exit on live vs held,
  context reachability by map, the save flag round-trip, and the Restock guarantee, as EditMode tests. Recall from the
  Complete phase and its resume into the emptied Location are tested by whichever of the Recall ticket and
  #331 (the Population and the Complete phase) lands second.

## Resolved questions (2026-10-18, ticketing)

- Which events close a Town Portal: only using it or opening a new one (Diablo II). One portal, never
  several; it outlives Relocates, Sends, Returns, Deaths and quits (Decisions above).
- The hero regenerates in Town while the Run is held (Decisions above).
- The held Location's ground and the Town ground are one `World.Ground` today, wiped by every Send and Run
  end. Keeping them apart waits on "Loot in the Arena" (#307), which settles where loot lies.

## Out of scope

- Free Recalls at waypoints in Locations (Icebox: "Waypoints at Locations").
- Identification scrolls.
- Stock that never runs out (Icebox), so a Portal Scroll could be bought without emptying its slot.
- Saving the Encounter so a quit could resume it.
