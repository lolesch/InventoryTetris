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
  nothing and faded out. Only then does the map change.
- Same on the Town side: the Town figure walks to the portal at the same speed, vanishes, then the command
  (resume or Send) runs and the Field map comes up. The button takes no second click while he is on his way.
- The Field figure's arrival from a Town portal is unchanged (it appears at the fight at once).

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
