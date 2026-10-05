---
status: accepted
---

# A sale is immediate: the Sold tab replaces the staged Sell Basket

Selling no longer stages. Shift-click an item, or drop it on the Supply or the Sold tab, and
it is sold at once: the payout goes into the **Wallet** and the Package lands in the **Sold
tab**, a container the player can buy back from like any **Supply**. This supersedes
ADR-0012 and the **Sell Basket** glossary entry.

The staged basket was a state that is neither sold nor owned, and everything ADR-0012 paid
for followed from it: the Supply had to block while the basket held anything so that Cancel
could always finish, a Town Stop change had to cancel what was staged, and an origin ledger
had to remember each Package's cell. With nothing staged, Cancel has nothing to fit, so the
block, the ledger, the Confirm and Cancel buttons, the previewed total and the context-change
cancel all go. A mis-click stays recoverable, but through a buy-back at the Markup instead of
a free cancel.

## Decisions

- **One transaction.** A sale is one `ItemTransaction` over the source, the Sold container and
  the Wallet, and it wholly happens or leaves all three untouched (`Sale`). A sale that cannot
  pay out (0 payout, a payout the Wallet cannot bank, a Package too big for the Sold
  container) is refused, never half done. A full Sold container discards its oldest Packages
  to make room, so a sale that can pay out always succeeds.
- **The Sold container is a Supply.** It is bought back by shift-click, right-click or drag
  into the Inventory exactly like a shelf, and `QuickMoveResolver` treats it as one more shelf.
- **It is World-owned and never saved.** The Sold container belongs to the World beside the
  Supplies, not to the Hero: it is stock the Town Stops hold, not the hero's belongings, and it
  is excluded from any save. Persistence does not have to decide anything about it.
- **A Restock clears it.** Closing a panel or changing Town Stop leaves it alone.
- **The `ContainerRole` value stays reserved.** The members are serialized by value in scenes
  and prefabs, and `HealerSupply` and `Sold` follow the retired role, so the value is kept as
  `RetiredBasket` rather than deleted. It resolves to no container. Delete it only in a change
  that re-serializes every display.

## Consequences

ADR-0013's consequence about a staged sale cancelling the instant the Vendor leaves is
amended: there is nothing staged. **Return to Origin** is unchanged in behaviour; it no
longer has a cancelled sale to serve, but a purchase in progress that is dropped back on a
Supply still goes through it.

The staged code is deleted outright, not kept behind a flag: `SellBasket`,
`SellBasketQuickMove`, `SellBasketDisplay`, `BasketSlotDisplay`, the `SellBasket` quick-move
intent, the provider's `Basket` and `basketSize`, and the tests that asserted that mechanism.
