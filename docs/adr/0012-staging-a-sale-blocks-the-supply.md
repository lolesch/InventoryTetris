---
status: accepted
---

# Staging a sale blocks the Supply

While the Sell Basket holds anything, the Vendor's Supply stops responding to input and
dims; it re-enables the moment the basket empties on Confirm or Cancel. Selling is modal.

The reason is that Cancel has to be able to finish. Every staged Package remembers its
Package Origin, and Cancel hands each one to Return to Origin, which tries the exact
origin cell, then anywhere in the Inventory, then leaves the Package on the cursor. The
cursor holds **one** Package. So a basket holding five items whose origin cells have since
been filled has exactly one place to put the first of them and nowhere at all for the
other four — the basket would be stuck holding items the player cannot get back.

Nothing else in the trade flow can fill the Inventory while a sale is staged. Loot only
drops in the Field, and a Run cannot start with a town panel open. Buying is the one
action that adds items to the Inventory in Town, and a player with an almost-full bag can
buy it the rest of the way full between staging an item and cancelling. Blocking the
Supply is therefore not a convenience or a piece of visual polish: it is what makes the
"Cancel always fits" guarantee true, and it is the only thing that does.

## Considered options

**Let it degrade gracefully.** Return to Origin already refuses to destroy anything — the
unplaceable Package comes back on the cursor and the ones already returned stay returned.
This reads like sufficient safety, and it is the justification the trade-flow spec
originally recorded. It is wrong: not-destroying is not the same as being recoverable, and
a basket that can only ever hand back one of its five items is a dead end the player
cannot reason about.

**Drop the overflow on the floor.** Rejected because there is no Town ground to drop to.
`DropToFloorSlotDisplay` destroys what it is given, and `ILootGround` is Field-side, bound
to a Location. Building a Town ground to catch a case the modal block already prevents
would add a domain concept — `CONTEXT.md`'s **Drop** is explicitly Field-and-Location-bound
— to serve an error path.

**Refuse the buy instead of blocking the panel.** Gating individual purchases on "would
this leave room for the basket?" keeps the Supply live, but it makes the failure arrive as
an unexplained refusal at the moment of buying, and it re-derives the same arithmetic at
every purchase. Blocking the whole Supply says *you are mid-sale* in one legible gesture.

## Consequences

The block is a `CanvasGroup` over the Supply half of the Vendor's Side Panel, driven by
the basket container's content-changed signal through one predicate, `SellBasket.IsStaging`.
It is the only thing standing between the flow and an unrecoverable basket, so it fails
loud rather than silently: the blocker warns in `OnValidate` when its `CanvasGroup`
reference is unwired, and logs an error the first time a Package is staged without one.

The residue path stays in `SellBasket.Cancel` regardless — one Package on the cursor, the
rest left staged, an error surfaced — as the assertion that fires if this guarantee is ever
wrong. It is not a feature and should not be designed for; if it ever fires in practice,
that is the evidence that justifies a real Town ground.
