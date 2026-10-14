# Coin overflow - a sale that cannot bank its payout drops the rest, and Town gets a ground

Date: 2026-10-14
Status: Settled in conversation after a belt sale was refused from a full bag.

## Problem Statement

A shift-click sale from a full bag is refused when the payout needs more coin piles than the sale frees
cells (`Sale.Complete`, `Wallet.CanDeposit`). A 2x1 belt paying gold, silver and copper frees two cells and
needs three. The player sees a sale that does nothing and no reason. Reproduced in
`SaleTests.ABeltWhosePayoutNeedsMorePilesThanItFrees_IsRefused`.

The Run already has the answer for coins that do not fit: `LootFlow` banks what the Wallet takes and lays
the rest on the **Ground** as a coin stack. Town has no Run and so no ground, which is why the sale
refuses instead of dropping. Where coins land in the grid is also unspecified beyond `Wallet.Deposit`
walking the denominations largest first.

## Decisions

**A payout that does not fit drops its lowest denominations.** The Wallet banks the payout highest
denomination first, so the bag keeps gold, then silver, then copper, then iron, and the coins that did not
fit lie on the ground. A sale is refused only for a payout of 0, currency, a held price, or a Package too
big for the Sold container. It never refuses for lack of coin room.

**Coins are placed highest denomination first, on every move.** One placement order for coins, wherever they
come from (sale payout, loot, Quick Move): gold, silver, copper, iron into the first free cells in that
order. A full destination keeps the highest denominations. No bulk move exists, so the bag keeping its
coins in that order is what puts them in order in the Stash when they are shift-clicked across in turn.

**A coin moved into a Wallet-backing container re-lays all its coins.** One coin shift-clicked or dropped
into the container behind the Wallet leaves that container's coins gold first, iron last, even though
coins the player placed by hand move. Any other container (the Stash, the Ground) puts a single coin in
the first free cell; only coins arriving together (a payout) are ordered there.

**Town gets a ground.** The `World` already owns one `GroundContainer`; a Run's `LootFlow` lays on it and
wipes it. In Town the same container takes sale overflow and is shown in the same **Ground Items List**,
as a tab beside the open Town Stop with the Hero Panel. Coins lie there until picked up and are wiped on
Send, as a Run's Drops are wiped when it ends. A click picks one up through the player's acquisition entry
point, so a full bag leaves it lying there.

**Picking up from the ground no longer needs a Run.** `LootFlow.PickUpFromGround` is the only pick-up and
needs a live loot flow. The pick-up moves to a ground-owned operation both Town and the Run call.

**An evicted coin stack banks first, in Town too.** The Run's rule (`LootFlow.BankEvicted`) holds: when the
ground is full and pushes a coin stack out, the Wallet takes what it can; the rest is gone.

## User stories

1. As a player, I sell a belt from a full bag and the sale goes through.
2. As a player, when the payout does not fit the bag, the gold and silver go in the bag and the copper and
   iron lie on the ground.
3. As a player, I see the coins that did not fit in a list in Town and click them to pick them up.
4. As a player, I click an overflow coin with a full bag and it stays on the ground.
5. As a player, the coins I leave on the Town ground are gone when I Send.
6. As a player, my coins sit gold first, iron last, wherever they came from.
7. As a player, when I shift-click my bag's coins into the Stash one by one, the Stash holds them gold
   first, iron last, because the bag's coins were already in that order.
8. As a player, a full destination keeps my highest denominations.
9. As a player, a coin I drop into my bag ends up in order with the coins already there.
10. As a player, a sale still refuses when it pays 0 or is currency, and says nothing has changed.

## Implementation Decisions

- `Wallet.Deposit` returns the `Currency` it could not place; `CanDeposit` goes. `Sale.Complete` queues the
  deposit, then lays the returned remainder on the ground as a commit-time effect, one `TryLand` per
  denomination, highest first.
- The Sale takes the ground through `ILootGround`, which is already the Run's discard seam; the Town passes
  the `World`'s ground.
- The Ground context becomes reachable in Town (`InventoryContextState.IsReachableInField` and
  `SyncToPhase`), and `QuickMoveResolver`'s `groundOpen` is true in Town.
- A single coin-placement rule lives with the coin placement code, not in each caller: the Wallet's
  Deposit, Consolidate and a Quick Move of coins all go through it.
- Tests assert container contents and balances, never event counts (ADR-0007).

## Out of scope

- A persistent Ground panel in every phase, replacing the Combat Panel's ground tab.
- Per-denomination pick-up filters on the Town ground.
