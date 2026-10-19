# Portal review rework - the portal is a one-way street, and the Combat Panel splits

Decisions from the playtest of epic #359 (PR #375). They amend `2026-10-16-town-portal-design.md` and
ADR-0022 where they disagree; the older text yields.

## The portal is one-way: the Field side goes to Town, the Town side goes to the Run

- **Field-map marker: a click takes the hero to Town; the portal stays open and the Run stays held.** It no
  longer resumes the Run (this reverses #370). The trip is the Recall's: the hero walks into the portal, vanishes,
  the Town map comes up and he stands at the Waypoint beside the Town portal.
- **Town portal: a click uses it, and using it closes it.** With the Run held it resumes it. With none held it
  Sends a fresh Encounter at the portal's Location, the hero arriving through the portal. Either way the Hero's
  open portal is then none, so a Send through the portal also closes it (the older "a Send leaves it open"
  applies to a Send by the Location toggles, which never touches the portal).
- Both markers are `AbstractButton`s that own their click: each knows its map, so inherits interaction, the
  interactable state and the click feedback instead of being a bare `IPointerClickHandler`.

## The hero goes through the portal, not into a cut

- He **walks** to the portal at his own movement speed (the canvas speed the arena already derives from his
  walking speed), never over a fixed time. Then he **vanishes** over a short fixed beat (0.5 s): scaled down to
  nothing and faded out. The map then waits a short beat (0.25 s) before it changes.
- Same on the Town side: the Town figure walks to the portal at the same speed, vanishes, then the command
  (resume or Send) runs and the Field map comes up. The button takes no second click while he is on his way.

## The portal grows, and he comes out of it the way he went in

- A portal does not appear at once: it scales up, its width from 0 to 1 and its height from 0.5 to 1 (0.4 s), and
  a hero only walks into it, or out of it, once it is open. It is not a click target while it grows.
- On the Field map the portal grows as the Recall opens; the hero then walks in, vanishes, waits, and the map changes.
- On arrival the map is still fading in, so the portal waits a beat (0.3 s) before it grows, and the hero is not
  there until it has. Coming out is exactly going in reversed: going in he starts beside the portal, walks onto it and
  scales down; coming out he scales up on top of the portal (0.5 s) and walks the few steps to the side, to the Town
  Waypoint or to where the fight has him. This holds in both directions: the Town Portal on the Town map after a
  Recall, and a portal on the Field map (opened beside the hero, closed once he is clear of it) after the Town
  Portal resumes the Run or Sends one.

## The Town hero comes back where the Town map is entered

Walking out through the Town Exit and back through the Field Exit with a held Run must not leave the Town
figure where he stood or mid-walk. When the Town map comes up and he did not arrive by a portal, he is placed
at the Town Exit (a Return's entry); when a portal brought him, he stands at the Waypoint as for a Recall.

## Consumables have no rarity

Every consumable is Common. A rolled consumable skips the rarity outcome (the drop chance, NoDrop, is kept), so
Portal Scrolls of different rarities can no longer sit side by side in one stack. One place decides it, the
item generator's roll of a definition.

## The Recall button follows events

`RecallButton` subscribes instead of polling: the hero Inventory's content change (re-bound on a hero load, which
replaces the Inventory) and the Run's phase changes. Its count and interactable state are recomputed in those
handlers only.

## The Combat Panel splits (replaces #361's Side Panel gating)

- The sim settings (the four sliders, Auto Pickup, Pause, and the combat-stats text) leave the Side Panel group
  and become their own `SimplePanel` at the top centre of the HUD, with its own HUD toggle. The player opens and
  closes it; it is neither a context nor tied to a map.
- What remains is the **Ground Items panel**, a Side Panel shown whenever no Town Stop is open: on the contexts
  `None`, `Hero` and `Ground`. Closing any Town Stop shows it, which is also where the ground is shown in Town.
  It lives at HUD level, no longer under the Field map's face.
- Gone with it: `SidePanel`'s `isCombatPanel`/`mapUp`/`SetMapUp`, `InventoryContextState.CombatPanelUpIn`, and the
  Field face's calls into the panel. What #361 kept: the map decides which contexts are reachable.

## Out of scope

Coins dropped by hand reordering highest-first, and the up-front refusal of a pickup that may not fit, are
Inventory behaviour, not the Recall: tracked separately.

## The Run waits for the hero; the Exits wait for him too

- Coming out of a portal onto the Field map, the Run does not start until he has grown on top of the portal and
  walked clear of it. A held Run stays held until then and resumes on arrival; a fresh Send is frozen by a third
  hold on the simulation, `IsHeroInTransit`, beside the player's Pause and a menu's freeze. A Recall is refused
  while it holds.
- The Town Exit and the Field Exit do the same going out: the hero walks to the Exit at his own pace first, the
  Field Run frozen where it stands, and the map changes (the Return happens) once he stands there. A click on a
  Town stop on the way cancels the Town Exit.
- A Recall opens the Field portal beside where the hero is drawn, not beside the fight he has not reached yet; a
  portal he comes out of opens beside the fight. On the Town map he walks to the Town Portal's `Beside` offset.
