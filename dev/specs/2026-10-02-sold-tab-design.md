# Sold tab: immediate sale and rebuy

Date: 2026-10-02
Status: Scoping spec — not a plan. Slice into a GitHub epic with `/to-tickets`, build with
`/implement`.
Base: `main` at `1e6e7d7`.
Derived with `/rederive`; the route to it audited twice with `/drift-review`, and the gaps
those passes found folded into the Implementation Decisions below.
Supersedes: ADR-0012 (*Staging a sale blocks the Supply*) and the **Sell Basket** entry in
`CONTEXT.md`.

## Problem Statement

Selling costs too many clicks. To sell anything the player stages each item into the Sell
Basket, checks the total, then presses Confirm — and while anything is staged the Supply is
dead and dimmed, and walking away from the Town Stop cancels the whole thing. Players want to
shift-click an item and have it sold.

Underneath, the basket is a state that is neither sold nor owned: an item has left the
Inventory, the **Wallet** has not been paid, and the item is waiting to be either banked or
returned. Everything awkward about the flow is a guard that makes that state survivable. The
Supply blocks while the basket holds anything, so Cancel always fits (ADR-0012). A Town Stop
change cancels, so nothing is stranded in a basket nobody can see. A 0 payout refuses to
confirm. A basket → Inventory move is a free `MoveTo`, so the "staged" item can come back for
nothing. The rule the player actually holds is simpler than any of these: *a sale pays me
now, and if I regret it I can buy the item back.*

The same shape shows up in the Supply: an item the player cannot afford still lifts off the
shelf onto the cursor with a red tint, and only fails when it is released. The player picks
something up that the game already knows they cannot have.

## Solution

Selling is one gesture. Shift-clicking an item in the Inventory or on the Equipment while the
Vendor or the Healer is open sells it on the spot: the **Wallet** is paid the item's sell
value, once, immediately. Dragging an item and dropping it on the Supply or on the Sold tab
sells it the same way; the item appears in the Sold tab, never on the shelf.

The sold item goes to a **Sold tab** beside the Supply, one tab shared by the Vendor and the
Healer. The Sold tab is a Supply: its items are bought back exactly like any shelf — shift-click
or right-click buys, a drag buys on release — at the Supply price, which is the sell value plus
the Markup. A mis-click is therefore recoverable but not free. The Sold tab holds a limited
number of items; when a new sale needs room, the oldest sold items fall off. A Restock clears
it.

Nothing is ever staged, so nothing blocks and nothing cancels. An item the player cannot afford
cannot be picked up from any shelf at all.

## User Stories

1. As a player, I want shift-clicking an item in my Inventory to sell it immediately while the
   Vendor is open, so that selling does not need a second confirming click.
2. As a player, I want shift-clicking an item in my Inventory to sell it immediately while the
   Healer is open, so that both selling Town Stops behave the same way.
3. As a player, I want shift-clicking a worn item to unequip and sell it in one step, so that I
   do not have to unequip it first.
4. As a player, I want my Wallet paid the item's full sell value the moment I sell, so that I
   see my money change when I click.
5. As a player, I want a stack to sell as a whole when I shift-click it, so that clearing out a
   pile of arrows is one click.
6. As a player, I want to drag an item and drop it on the Supply to sell it, so that I can sell
   with the pointer I am already holding.
7. As a player, I want to drag an item and drop it on the Sold tab to sell it, so that the
   obvious target for a sale works.
8. As a player, I want a Ctrl-drag of half a stack dropped on the Supply to sell only that
   half, so that I can sell part of a stack.
9. As a player, I want an item I drop on the Supply to appear in the Sold tab and not on the
   shelf, so that the Supply stays the Town Stop's own stock.
10. As a player, I want sold items collected in a Sold tab, so that I can see what I sold and
    undo a mistake.
11. As a player, I want to switch between the Supply and the Sold tab with a toggle, so that I
    can look at either without opening anything else.
12. As a player, I want a panel to always reopen on the Supply tab, so that I know where I am
    when I come back. (Amended 2026-10-05 after play-testing: the original story kept the tab I
    last looked at.)
13. As a player, I want the Vendor and the Healer to show the same Sold tab, so that an item I
    sold at either stop is still there when I change my mind at the other.
14. As a player, I want to buy a sold item back with shift-click, so that undoing a mistake is
    as quick as making it.
15. As a player, I want to buy a sold item back with right-click, so that it works the way every
    Supply shelf does.
16. As a player, I want to buy a sold item back by dragging it into my Inventory, so that I can
    choose where it lands.
17. As a player, I want the buy-back price to be the sell value plus the Markup, so that the
    Sold tab behaves exactly like a Supply and selling is not a free storage trick.
18. As a player, I want a bought-back item to auto-equip or land in my bag exactly like a Supply
    purchase, so that I get the same placement behaviour everywhere.
19. As a player, I want a sold item I cannot afford to buy back to be tinted red, so that I can
    see the price is out of reach.
20. As a player, I want an item I cannot afford to be impossible to pick up, on any shelf, so
    that I never lift something only to have the drop refuse it.
21. As a player, I want to be able to pick up half a stack I can afford even when the whole
    stack is out of reach, so that the lock follows what I am actually taking.
22. As a player, I want dropping an item that I am in the middle of buying back onto the Supply
    or the Sold tab to put it back where it came from, so that I am never paid for something I
    have not owned yet.
23. As a player, I want a sale that my Inventory cannot take the payout for to do nothing, so
    that I never lose coins to a full bag.
24. As a player, I want the drop target to show the forbidden tint when the payout will not fit,
    so that I see why the drop would be refused before I release.
25. As a player, I want a sale of an item worth nothing to do nothing, so that I do not give
    away an item for no coins by accident.
26. As a player, I want the oldest sold items to fall off when the Sold tab is full, so that a
    long selling spree never blocks the next sale.
27. As a player, I want a sale to always succeed when it can pay out, even if the Sold tab is
    full, so that my click is never refused for a reason on a tab I cannot see.
28. As a player, I want the Sold tab emptied when the Town Stops restock, so that the Sold tab
    is stock like any other and not a second Stash.
29. As a player, I want closing the panel or switching Town Stops to leave my sold items alone,
    so that nothing is lost by walking around.
30. As a player, I want the Supply never to dim or lock while I sell, so that I can buy and sell
    in any order.
31. As a player, I want the Supply and the Sold tab to be two views of the same panel, so that
    the Vendor does not need a third place to look.
32. As a player, I want a sold item to keep its affixes and rarity when I buy it back, so that
    undoing a sale really restores what I sold.
33. As a player, I want a sold item to price from its own value, so that I can predict the
    buy-back cost from the sell price.
34. As a developer, I want a sale to be one transaction over the source, the Sold container and
    the Wallet, so that it either wholly happens or leaves every container untouched.
35. As a developer, I want the Sold container to be a Supply in the Quick Move table, so that
    adding it is a table entry and not a new branch.
36. As a developer, I want affordability stated once, so that the tint, the grab and the drop
    cannot disagree.
37. As a developer, I want one Wallet answer to "does this payout fit", so that a sale cannot
    deposit blindly.
38. As a developer, I want the basket's origin ledger, Confirm, Cancel, total label and Supply
    blocker deleted, so that there is no second way to sell and no residue of the old one.
39. As a developer, I want an ADR superseding ADR-0012, so that the reason the Supply used to
    block is recorded as gone and not forgotten.
40. As a developer, I want the Sold tab absent from a save, so that persistence does not have to
    return a staged item to its origin before snapshotting.

## Implementation Decisions

- **A sale is one transaction.** Selling takes the Package from its source — the Inventory or
  the Equipment — and adds it to the Sold container and deposits the payout into the Wallet, as
  a single `ItemTransaction` over all three. Taking from the Equipment lifts the item's affixes
  as the transaction's commit-time effect, exactly as an unequip does today. The payout is the
  sell value times the amount, banked once. This is the one statement of a sale; shift-click and
  drop both call it.
- **Payout must fit, else nothing happens.** The Wallet gains one answer to "would this payout
  fit in the bag the sale is about to free". A sale checks it before queueing the deposit, the
  same check-then-queue order a purchase already follows. A payout that does not fit refuses the
  whole sale: nothing moves, nothing is paid. The Wallet's existing deposit, which drops what
  does not fit, is not called blind by a sale. A sale that would pay 0 also does nothing — the
  existing zero-payout guard is carried over, and #122 fixes the data that made it matter.
- **The Sold container replaces the basket as a role.** It is a container played by the same
  type as the Supply, bound by role to the Sold tab's grid wherever it appears. One container
  exists; the Vendor's and the Healer's panels both bind it, the pattern the single basket
  already used. It is sized like the Supply grid by default.
- **Eviction is oldest-first, and only the order is remembered.** When a sale would not fit, the
  oldest sold Packages are discarded until it does. The only ledger left is the sale order,
  replacing the basket's origin ledger. A sold item merging into an existing stack takes that
  stack's newest position. A discarded Package is gone — it belongs to the Town Stop, and the
  player has been paid for it.
- **The Quick Move table loses the basket, and gains a sale.** The Vendor and Healer contexts
  keep their sink, but the sink is now a sale intent instead of staging, and the basket → hub
  row is removed. The check that makes a shelf shift-click always a Buy gains the Sold container
  as one more shelf, outside the table, in every context. A new Town Stop remains a new arm and
  not a new branch.
- **The Supply slot display is the Sold tab's slot display.** It already buys by shift-click,
  right-click and drag. It additionally becomes a drop target that sells: it accepts a
  player-owned Package as a sale, and refuses it — so the existing forbidden tint shows — when
  the payout would not fit or would be 0. A Package that carries a price, because it is a
  purchase in progress, is never a sale; it returns to its origin free. The drag provider
  exposes whether the held Package is a purchase so a drop target can ask.
- **Affordability has one predicate and gates the grab.** The shelf slot's tint, the pick-up and
  the drop all read the same affordability check, which prices the amount actually being picked
  up. A half-stack pick-up with Ctrl is priced for the half. The drop's existing gate stays as
  the transaction-level guarantee. Shift-click and right-click already refuse silently and are
  unchanged.
- **Tabs are `PanelToggle`s in a group of their own.** Each selling panel has a Supply toggle
  and a Sold toggle in a `ToggleGroup` that forbids switch-off, so one tab is always showing. A
  tab toggle never requests an Inventory Context: it chooses a view inside a panel that is
  already open, and must not be a `SidePanelToggle`. A panel returns to its Supply tab when it
  closes, so it always reopens on the Supply (amended 2026-10-05; it first kept the selection). A
  sale does not switch tabs.
- **Restock clears the Sold tab.** Each Supply's Restock clears the Sold container at the same
  moment it refills, so the Vendor's Restock and the Healer's both empty it.
- **Deleted outright:** the basket display and its slot display, the Confirm and Cancel
  buttons, the total label, the Supply blocker and its wiring, the origin ledger, the
  staged-sale preview, the Town Stop change cancel, the basket → hub row, and the basket's
  provider property and size field. That includes the Healer half of #121 (its basket rows,
  per-panel blocker and cancel on handover) and the Cancel fix `0cc9481`, which harden a
  mechanism this swap removes; #121's Supply half stays.
- **Scene and prefab residue goes with it.** The basket display's prefab, its instances in the
  scenes, the serialized Supply-blocker references, and any persistent event on the deleted
  buttons are removed, not rewired — a Cancel button once also called a Restock. No scene or
  prefab may be left holding a missing-script reference.
- **The container role enum is serialized by value.** The Sold role is appended after the
  Healer Supply's, and the basket's value stays reserved when the basket role is retired,
  because deleting it would shift every later member and silently rebind displays in scenes and
  prefabs. The value may be deleted only in a change that re-serializes every display.
- **Ownership.** The Sold container belongs to the Hero State, like the basket and the Supplies
  it sits beside, and is excluded from any save: it is stock the Town Stops hold, not the
  hero's belongings.
- **Restock has a service home.** Each Supply's Restock becomes an operation on the Hero State's
  inventory service, with the old provider forwarding to it until the contract ticket, so the
  Sold clear travels with it and the debug buttons rewire to the service.
- **Documentation.** A new ADR supersedes ADR-0012: the Supply no longer blocks because nothing
  is staged, so Cancel has nothing to fit. `CONTEXT.md` replaces **Sell Basket** with the Sold
  tab, and **Return to Origin** stops listing a cancelled sale. ADR-0013's consequence about a
  staged sale cancelling on a context change is amended. Domain term: **Sold tab**.

## Testing Decisions

- **One seam: the container layer.** ADR-0007 puts the testable seam at the Containers
  assembly, and the sale, the Quick Move table and the buy path all live there. Tests assert on
  container contents, the Wallet balance and the stat receiver — never on event counts or GUI
  state. GUI shells stay smoke-tested.
- **What makes a good test here.** A test names the rule it protects: the Wallet is paid once at
  the sell value; an item is in exactly one place after a sale; a refused sale leaves every
  container untouched; a bought-back item costs the sell value times the Markup.
- **Modules tested:**
  - the sale — from the Inventory and from the Equipment (affix lift on commit), a stack, a
    full Sold container evicting oldest-first including more than one eviction, a payout that
    does not fit, a payout of 0, and a Package that carries a price not being a sale;
  - the Wallet's payout-fit answer next to its existing deposit tests;
  - the Quick Move table — the sale sink in the Vendor and Healer contexts, the basket row gone,
    and the Sold container always resolving to a Buy;
  - the buy path against the Sold container, extending the Vendor purchase tests;
  - the shared affordability predicate, including the Ctrl-half pick-up price.
- **Prior art.** `SellBasketTests` and `SellBasketQuickMoveTests` for the staged flow's fixtures
  and the equipment-source assertions, `QuickMoveResolverTests` for the table, and
  `VendorTransactionTests` and `WalletTests` for buying and deposit. The staged-flow tests are
  deleted or rewritten, not kept: they assert on a mechanism nothing will call.
- **Not unit-tested:** the Restock clearing the Sold container (a provider-side call, smoke
  tested), the tab toggles and the Sold tab's layout, and the scene wiring.

## Out of Scope

- One Sold tab per Town Stop. It is shared for now; per-stop is a possible later change, and the
  rebuy path does not assume either.
- Tuning the rebuy price. It is the Supply price, exactly; a separate buy-back markup is later.
- Feedback for a refused shift-click. A refused sale or buy is a silent no-op, as an
  unaffordable buy already is.
- Switching to the Sold tab on a sale, an undo-last-sale button, and selling from the Stash.
- Persisting the Sold tab. It is not saved, like Supply stock; the save work owns the decision.
- A Restock cadence. Today a Restock fires from `Awake` and scene buttons; when it fires in play
  is a separate question.
- #122, which gives every item a non-zero value. This spec carries the zero-payout guard and
  leaves the data fix there.
- A Town ground for overflow. Nothing in this flow overflows to one.

## Further Notes

- **Defaults this spec chose without settling them with the user:** the Sold container's size
  (the Supply grid's), a merged stack taking the newest sale's age, a zero-payout sale doing
  nothing, and a tab keeping its selection across close and reopen. Change them here before
  `/to-tickets` if any is wrong.
- **Shared tab, per-Supply Restock.** The tab is shared but each Supply restocks on its own, so
  a Healer Restock clears the Vendor-sold sword and the reverse. That is the settled rule: the
  clear is called wherever a Restock is.
- **The mis-click costs the Markup.** Selling pays the sell value and buying back costs 1.5
  times it, so an undone sale loses half the sell value. That is intended for the first draft.
- **Services epic (#106).** #115 and #116 migrate callers of the inventory provider, and the
  basket's provider property and basket display are among them. Migrating a surface this swap
  deletes is a stranded fix, so #112 and #115 are blocked on the contract ticket. #107 and #108
  name the Sold container where they used to name the Sell Basket, and the Services spec's Hero
  State contents list still says "Sell Basket" and is to be read as the Sold container.
- **Before Unity compile verification, asmdef changes or scripted multi-file edits,** read
  `docs/agents/codebase-notes.md`.
