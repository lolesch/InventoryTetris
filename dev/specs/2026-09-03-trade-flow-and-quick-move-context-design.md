# Trade flow and the quick-move context

Date: 2026-09-03
Status: Scoping spec — not a plan. Slice into a GitHub epic with `/to-tickets`, build
with `/implement`.
Base: cut after issue #14 closes the foundational-rework epic (#2) — this sits on the
`feature/foundational-rework` surface and assumes `ItemTransaction` / `VendorTransaction`
as of issue #11 (`4c9bd0b`). Do not start it while that epic's frontier (#12–#14) is
open.

Third pass over the vendor flow. `2026-08-26-shop-currency-buy-loop-design.md` built the
buy loop; `2026-08-30-item-movement-model-design.md` routed buy and sell through the
Transaction (issue #11) and **explicitly deferred** two threads — the drag-cancel /
return-to-origin path (that spec's Out of Scope) and the dedicated vendor container
(`2026-08-26-shop-currency-followups.md` §1–§2). This spec picks up the drag-cancel
thread and adds a confirmation-sale UX on top. Respects ADR-0006 (item → transaction →
wallet order: this is post-transaction, pre-wallet-module work) and ADR-0007 (the one
test seam is `InventorySystem.Containers`).

## Problem Statement

Three rough edges in trading and quick-move, all reported while playing the vendor
build:

1. **Quick-move has one hard-coded destination.** Shift-clicking a Package in the
   backpack always aims at the Stash, even when the Stash panel is closed and the Store
   is the panel actually on screen. There is no notion of "the panel I'm looking at",
   so selling by shift-click — the natural mirror of the shift-click buy that already
   works on the shelf — isn't possible.

2. **Buying charges at pick-up.** Dragging an item off the shelf deducts the price the
   instant the drag starts. Picking an item up to look at it and putting it back costs
   money. If the drag is never completed the coins are just gone — the drag system has
   no cancel path.

3. **Selling is instant, irreversible, and has two doors.** Dropping a Package on the
   dedicated sell slot sells it on the spot; so does dropping it on the buy shelf.
   There is no confirmation, no way to stage a sale, no preview of what the pile is
   worth, and no way to back out.

## Solution

**A Menu Context that follows the open panel.** The Stash and the Store share a screen
slot — whichever is open is the *active secondary panel*. Quick-move targets it:
shift-click in the backpack sends to the Stash when the Stash is open, and to the Sell
Basket when the Store is open. Closing both leaves no context and shift-click does
nothing. The existing menu toggles already enforce "one at a time"; the context is a
thin read on top of them.

**Pay on drop, not on pick-up.** Taking an item off the shelf is an ordinary pick-up —
nothing is charged. The price is paid only when the item lands in one of the player's
containers, as part of the same Transaction that places it. Drop it back on the shelf,
or cancel the drag, and nothing is charged. A cancel path is added to the drag system
so an abandoned or interrupted drag returns its Package to where it came from.

**A Sell Basket with a confirmation step.** The dedicated sell slot becomes a small grid
the player fills with the Packages they mean to sell. It shows the running total the
vendor will pay. A Confirm button banks the coins and clears the basket in one
Transaction; a Cancel button — or closing the Store — returns every Package to where it
came from. While the basket holds anything, buying is blocked, so the backpack space a
Cancel needs is still free.

## User Stories

### Quick-move context

1. As a player, I want shift-click in my backpack to send the item to whichever panel I
   have open — Stash or Store — so that quick-move follows what I'm looking at instead
   of a fixed guess.
2. As a player, when the Stash is open, I want shift-click to move a Package between
   backpack and Stash exactly as it does today, so that nothing about stashing changes.
3. As a player, when the Store is open, I want shift-click on a backpack Package to drop
   it into the Sell Basket, so that staging a sale is as fast as the shift-click buy on
   the shelf.
4. As a player, when the Store is open, I want shift-click on an equipped item to
   unequip it straight into the Sell Basket, so that selling gear I'm wearing doesn't
   need a detour through the backpack — the confirmation step makes this safe to do
   quickly.
5. As a player, when the Store is open, I want shift-click on a Package already in the
   Sell Basket to send it back to my backpack, so that I can correct a mistake without
   dragging.
6. As a player, when I have neither the Stash nor the Store open, I want shift-click to
   do nothing, so that I never fling an item into a container I can't see.
7. As a player, I want the most recently opened of Stash / Store to be the active one,
   so that opening the Store while the Stash is up just switches context with no extra
   click.
8. As a player, I want closing the open panel to clear the context, so that "nothing is
   open" is a real state.
9. As a player, I want shift-click buy on the shelf to keep working while the Store is
   the active context, so that the shelf's own quick action is unaffected by the new
   routing.
10. As a player, I want right-click use / equip / consume in the backpack to be
    unchanged whatever panel is open, so that the context only ever affects shift-click.

### Buying — pay on drop

11. As a player, I want to pick an item up off the shelf and put it back without being
    charged, so that I can inspect items freely.
12. As a player, I want to be charged for a bought item only when it lands in my
    backpack or on my paperdoll, so that payment and possession happen together.
13. As a player, when I buy by dragging and my backpack has no room, I want the item to
    stay on the shelf and nothing to be charged, so that a failed buy costs nothing —
    the same guarantee the shift-click buy already gives.
14. As a player, when I start a drag from the shelf and then abandon it, I want the item
    to return to the shelf and nothing charged, so that an interrupted buy leaves the
    shop as it was.
15. As a player, when I close the Store with a shelf item still on the cursor, I want it
    returned to the shelf, so that closing the shop can't strand or duplicate an item.
16. As a player, I want shift-click and right-click buys to keep working exactly as they
    do now, so that the instant-buy path is unchanged.
17. As a player, I want the "can't afford" tint on the shelf to keep meaning what it
    does now, so that the visual read of price versus wallet is unchanged.
18. As a player, I want the price I pay to be the price shown when I picked the item up,
    so that a restock mid-drag can't change the deal.

### Drag cancel / return to origin

19. As a player, I want a way to cancel a drag in progress and have the item go back
    where it came from, so that a mis-grab is recoverable.
20. As a player, I want an interrupted drag — a panel closing, an equip that rolls back
    — to return the item to its origin container, or to my backpack if the origin can't
    take it, so that no drag can lose an item.
21. As a developer, I want one "return this Package to its sender" primitive built on
    the Transaction, so that every cancel and interrupt path shares one tested
    implementation instead of ad-hoc rollback.

### Sell Basket

22. As a player, I want the sell slot to be a grid I can place several Packages into, so
    that I can line up a whole sale before committing to it.
23. As a player, I want the Sell Basket to show the total coins the vendor will pay for
    everything in it, so that I can see the payout before I commit.
24. As a player, I want the preview to update as I add or remove Packages, so that the
    number always matches the basket's contents.
25. As a player, I want a Confirm button that banks the coins and empties the basket, so
    that the sale happens only when I say so.
26. As a player, I want a Cancel button that returns every Package in the basket to
    where it came from, so that I can back out of a staged sale with no loss.
27. As a player, when I close the Store with Packages still in the basket, I want them
    returned the same way Cancel returns them, so that closing the shop is a safe way to
    abandon a sale.
28. As a player, I want a confirmed sale to bank all the coins in one go, consolidated
    the way a normal payout is, so that selling five items doesn't scatter loose change.
29. As a player, I want selling to conserve value exactly — the coins I get equal the
    previewed total — so that the preview is trustworthy.
30. As a player, I want a stack in the basket — a coin pile, a bundle of arrows — to be
    priced by its full amount, so that the preview matches what selling the whole stack
    pays.
31. As a player, I want dropping a Package on the buy shelf to no longer sell it, so
    that there's exactly one place selling happens.
32. As a player dragging a Package, I want to be able to drop it onto the Sell Basket
    grid, so that dragging still works alongside shift-click.
33. As a player, I want an item I shift-clicked into the basket by accident to be one
    shift-click (story 5) or one Cancel away from undone, so that the basket is never a
    trap.

### Modal sell — protecting the Cancel

34. As a player, while the Sell Basket holds anything, I want the buy shelf disabled, so
    that I can't spend the backpack room my Cancel would need.
35. As a player, I want the disabled shelf to read clearly as disabled — dimmed, not
    responding — so that I understand why buying isn't working.
36. As a player, I want emptying the basket, by Confirm or Cancel, to re-enable the
    shelf, so that the block lasts exactly as long as the staged sale.
37. As a player, I want Cancel to return each Package to its origin first — re-equip
    what was unequipped, back to the backpack what came from the backpack — and, if
    something genuinely cannot be placed, to leave it on the cursor rather than destroy
    it, so that a cancelled sale never loses an item.

### Developer / maintainer

38. As a developer, I want the quick-move destination decided by one pure function from
    (source container, context) to an intent, so that the routing rule is in one place
    and unit-tested, not copy-pasted across three slot displays.
39. As a developer, I want the menu toggles' radio group to expose "the active toggle
    changed, and it might now be none", so that the context adapter has a single event
    to listen to.
40. As a maintainer, I want game logic kept out of the radio group, so that it stays a
    reusable UI widget.
41. As a developer, I want the Sell Basket to be another role played by the existing
    container type, so that no new container class is introduced for it.
42. As a developer, I want buy-on-drop, sale confirmation, and sale cancellation
    expressed as effects on the existing Transaction, so that partial failures roll back
    like every other move.
43. As a developer, I want all of this covered at one seam — the containers assembly —
    reusing the movement-matrix test style, so that the test surface doesn't sprawl.
44. As a maintainer, I want the drop-on-shelf sell path and the single-slot instant sell
    deleted once the basket lands, so that no dead second door to selling is left behind.
45. As a maintainer, I want every "is the shop open" boolean replaced by the Menu
    Context, so that panel state has one representation.

## Implementation Decisions

### The Menu Context

- **Concept.** The Stash panel and the Store panel occupy one shared screen slot and
  are already mutually exclusive through the menu toggles' radio group. The *Menu
  Context* is the currently-open one of the two, or none. It is the single value that
  replaces any "is the shop open" boolean.
- **Kinds.** `None`, `Stash`, `Store`.
- **Source of truth.** A thin adapter subscribes to the radio group's change event,
  reads the active toggle, and resolves it to a kind plus the container that kind
  quick-moves to (`Stash` → the Stash; `Store` → the Sell Basket). It does not poll and
  holds no state beyond the current value. It lives in the GUI layer with the toggles
  and slot displays, not in the containers assembly.
- **Toggle → kind mapping.** Each menu toggle carries its kind as authored data — a
  small panel-toggle subclass with a serialized enum — rather than the adapter
  hard-coding which toggle is which.
- **Radio group change.** The radio group gains an explicit "active toggle deactivated"
  path: turning the active toggle off nulls its active-toggle reference and raises its
  change event, so `None` is reachable. Today it only hears selection and
  enable/disable, never the active toggle switching itself off.
- **Switch-off enabled.** The menu toggle group is set to allow switching off, so a
  player can close the open panel and land in the `None` context. Scene/config change
  plus the radio-group path above.
- **No stack.** Because the secondary panels are a mutually-exclusive pair, the context
  is a single slot, not a stack. A third exclusive panel added later joins the same
  group and the model still holds.

### Quick-move routing

- **One resolver.** A pure function in the containers assembly takes the source
  container and the Menu Context kind and returns a quick-move *intent*: do nothing,
  move to a named container, send to the Sell Basket, or buy. It encodes the whole
  matrix:
  - Context `Stash`: backpack → Stash; Stash → backpack; Equipment → Stash. Unchanged
    from today.
  - Context `Store`: backpack → Sell Basket; Equipment → Sell Basket (unequip, then
    basket); Sell Basket → backpack. The shelf's own shift-click stays "buy" and is
    handled where it is today.
  - Context `None`: every source → do nothing.
- **Slot displays call the resolver.** The three hand-rolled quick-move blocks
  (backpack, equipment, and the shelf's buy branch) are replaced by: ask the resolver
  for the intent, then run it through the existing Transaction pattern — remove from
  source, re-home into target or hand, commit. Equipment → basket keeps the affix-lift
  as a commit-time effect exactly as the current unequip does.
- **Only shift-click changes.** Right-click use / equip / consume, drag, and Ctrl-split
  are untouched.

### Buying — pay on drop

- **Pick-up is uncharged.** A drag started from the Store removes the Package from the
  shelf and puts it on the cursor, like any other pick-up. The price (read once, now)
  and the origin (the Store) are remembered for the length of the drag.
- **Charge rides the drop.** When the dragged Store Package is dropped into a player
  container, the placement runs in a Transaction as it does now, with the payment queued
  as a commit-time effect. No room, or can't afford → the Transaction rolls back: item
  back on the cursor or the shelf, nothing charged. The drag buy converges on the shape
  the shift/right-click buy already has.
- **Drop back on the shelf.** Dropping the Package onto the Store it came from returns
  it to the shelf with no effect — no sale (that path is being removed anyway), no
  charge.
- **The immediate paths are untouched.** Shift-click and right-click buys keep going
  straight through the existing atomic buy.

### Drag cancel / return to origin

- **One primitive.** A "return this Package to its sender" entry, built on the
  Transaction: try the origin container, then the backpack, else fail — leave it on the
  cursor, never drop it. This is the deferred `ReturnToOrigin` from the movement-model
  spec and `shop-currency-followups.md` §2.
- **Triggers wired now.** An explicit cancel input during a drag; a panel closing while
  a drag from that panel is live; the Store / shelf interrupts above. A Store-origin
  cancel returns the item to the shelf and — because pick-up no longer charges — needs
  no refund.
- **Not a full audit.** Cataloguing every drag interrupt across the whole UI is out of
  scope; the primitive makes extending the trigger set cheap later.

### The Sell Basket

- **A role, not a class.** The Sell Basket is a fourth role played by the existing
  container type, alongside Inventory / Stash / Store — it needs grid packing and
  nothing else. No wallet machinery, no new class. A dedicated lean container for Store
  *and* Basket stays out of scope (`shop-currency-followups.md` §1).
- **Display.** A grid container display like the backpack's, plus a total-value label
  and Confirm / Cancel buttons. Replaces the single-slot sell display.
- **Preview.** A pure function sums the vendor sell value over the basket's Packages,
  full amount per stack. The label subscribes to the basket's content-changed event.
- **Confirm.** One Transaction over the basket and the wallet: bank the summed value as
  minted coins — one payout, consolidated like any other — then clear the basket.
  Value-conserving by construction.
- **Cancel.** Return every basket Package through the return-to-origin primitive:
  re-equip what was unequipped, back to the backpack what came from the backpack.
- **Close equals Cancel.** Closing the Store panel with a non-empty basket runs the same
  return.
- **Sell value, not markup.** The basket pays the plain sell value; the vendor markup
  applies only to buying, unchanged.

### Modal sell

- **Buying blocked while the basket is non-empty.** The buy shelf stops responding to
  input and dims. It re-enables the moment the basket empties, by Confirm or Cancel.
- **Why.** It guarantees the backpack room each basket Package came from is still free,
  so a backpack-origin Cancel always fits. Without it, "empty backpack into basket → buy
  into the freed space → Cancel" has nowhere to put the returned items.
- **Equipment-origin items** return by re-equipping their old slot, falling back to the
  backpack; if neither takes it the Package stays on the cursor (story 37). The modal
  block is what makes the common, backpack-origin case a hard guarantee.
- **Not reservation.** Reserving the exact vacated footprints was considered and
  rejected — far more complex for the same guarantee.

### What gets removed

- The instant sale on the dedicated sell slot.
- The sale when a Package is dropped on the buy shelf.
- Any "is the shop open" boolean, in favour of the Menu Context.

## Testing Decisions

- **What a good test asserts here.** External outcomes only: which container holds which
  Package before and after, total item count, wallet value in base units, the previewed
  sale total versus the coins actually banked, and what's left on the cursor. Never how
  many refresh or content-changed events fired, never dictionary identity or order.
- **One seam.** All of it lands in `InventorySystem.Containers`, tested from
  `InventorySystem.Containers.Tests`:
  - the quick-move resolver — every (source, context) pair maps to the expected intent,
    including `None` → nothing and the equipment rows;
  - deferred purchase — charged on drop into a player container, not on pick-up; rolled
    back with no charge on no-room and on can't-afford; no charge on return-to-shelf;
    price fixed at pick-up;
  - return-to-origin primitive — origin first, backpack fallback, failure leaves the
    Package on the cursor and loses nothing;
  - sale preview — sum over mixed Packages including multi-amount stacks;
  - confirm — coins banked equal the preview, basket cleared, one consolidated payout,
    value conserved;
  - cancel — every Package back to its origin, wallet untouched.
- **Prior art.** `VendorTransactionTests` (sell / buy value and rollback assertions,
  `FakeCurrencyMinter`, `TestCatalog`) and `MovementMatrixTests` (drive a move through
  `AddAtPosition` / `RemoveAtPosition` / `ItemTransaction` the way the slot displays do,
  assert on contents plus hand). New cases extend those two fixtures or add a sibling in
  the same folder and asmdef — no new test assembly.
- **GUI shells are smoke-tested, not unit-tested.** The context adapter, the toggle
  subclass, the radio-group deactivate path, the basket display and its buttons, and
  the drag-cancel input are checked by an in-editor smoke pass at each phase gate: open
  Stash / open Store / shift-click each source / stage and confirm a sale / stage and
  cancel / close mid-sale / cancel a drag / buy by drag and by shift-click. Consistent
  with how the movement-model and currency work gated (`Run All` green plus a smoke
  pass).

## Out of Scope

- **A dedicated lean container type for the Store and the Basket.**
  `shop-currency-followups.md` §1. Both stay roles played by the existing container.
- **A Wallet module.** ADR-0006 puts the wallet after this; selling still banks coins
  onto the player container.
- **"Keep the change" / haggle pricing.** `shop-currency-followups.md` §3.
- **Restock behaviour.** Time- or open-based restock is untouched; the shelf stays
  finite-until-`RestockStore`.
- **A physical floor / loot pile for cancelled drags.** Return-to-origin falls back to
  the backpack, then fails onto the cursor — no floor. A real pile is separate work, as
  in the movement-model spec.
- **A full drag-interrupt audit.** Only the Store, shelf, and panel-close triggers are
  wired now.
- **Retuning.** Sell values, the buy markup, stack limits, basket grid size — all
  authored as they stand; no balance changes.
- **Multi-select or drag-a-group into the basket.** One Package per shift-click or drag,
  as everywhere else.
- **PlayMode tests for the drag and button wiring.** The seam is the container model.

## Further Notes

### Suggested ticket slice

1. **Return-to-origin / drag-cancel primitive** — the Transaction entry plus the
   explicit-cancel input and the panel-close trigger. Shared plumbing.
2. **Menu Context** — the radio-group deactivate signal, `AllowSwitchOff`, the toggle
   subclass, the context adapter, and the pure quick-move resolver; retire the three
   copy-pasted quick-move blocks and any shop-open boolean. Stash routing unchanged.
3. **Pay-on-drop for vendor buys** — small once (1) exists; the drag buy becomes a
   deferred-charge Transaction.
4. **Sell Basket container + display + value preview** — the fourth role, the grid
   display, the running total.
5. **Sell Basket confirm / cancel + modal sell** — the batched payout, the return, the
   buy-shelf block; delete the drop-on-shelf sale and the single-slot instant sale.
6. **Shift-click into the Sell Basket** — wire the `Store` context rows of the resolver
   (backpack and equipment → basket, basket → backpack). Needs (2) and (4).

Order: 1, 2, 3, 4, 5, 6. Each lands on green — EditMode `Run All` plus the smoke pass —
per the movement-model spec's gate definition. If a ticket won't fit one context
window, split it further rather than write a plan (CLAUDE.md).

### Glossary additions to propose

A `/domain-modeling` pass should fold these into `CONTEXT.md` when the epic starts:

- **Menu Context** — the currently-open secondary panel (Stash, Store, or none) that
  quick-move targets. Replaces any "shop is open" flag.
- **Sell Basket** — the grid the player stages a sale in; a role played by the container
  type, not a class. Emptied by Confirm (coins banked) or Cancel (Packages returned to
  origin).
- **Quick-move** — already used in code: the shift-click "send this Package to the
  active secondary container" action. Worth pinning.

### Why one spec, not three

The three edges share two pieces of plumbing — the return-to-origin primitive (cancel a
drag, cancel a sale, close a panel mid-move) and the Menu Context (route quick-move,
gate the shelf). Splitting them into separate specs would design that plumbing twice.
The tickets are still independent enough to build one at a time.
