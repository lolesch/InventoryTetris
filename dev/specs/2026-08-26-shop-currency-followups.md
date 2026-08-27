# Shop / currency — deferred work

Date: 2026-08-26
Status: Backlog — not scheduled. Split out of
`2026-08-26-shop-currency-buy-loop-design.md` so these ideas are not lost.

## 1. Proper vendor container

Replace `Store` (currently a `CharacterInventory`) with a dedicated vendor
container. `CharacterInventory` carries machinery a shop never uses (`TryPay`,
`CalculateCash`, currency stack-upgrade), and the shelf is finite — buying an
item removes it until the next `RestockStore()`.

The abandoned `VendorSupply` stub (deleted in the buy-loop change; see commit
history on `feature/shop-currency`) was the start of this. `AbstractDimensionalContainer`
leaves `AddAtPosition` and `GetStoredItemsAt` abstract — the actual spatial
packing / stacking / swapping (~100 lines) lives only in `CharacterInventory`.
Options for a functional vendor container:

- reimplement those two methods (duplication),
- `VendorSupply : CharacterInventory` (inherits packing, re-inherits the
  currency machinery — cleans nothing up),
- **push the packing implementation down into `AbstractDimensionalContainer`**
  so both `CharacterInventory` and a new `VendorSupply` go thin. The right
  shape, but a refactor of the most central class in the system.

Plus: infinite-stock handling in `VendorSlotDisplay` (don't deplete on buy),
a real `Restock()` (time-based or on shop open), threading the container type
through `InventoryProvider`, re-serializing the scene.

## 2. Item loss when the inventory is full during a coin payout

Three instances of the same gap:

- `VendorSlotDisplay.DropItem` — `//TODO: handle item loss if inventory is full`
  (selling back to the vendor).
- `SellItenSlotDisplay.DropItem` — same `//TODO` (selling on the sell slot).
- `CharacterInventory.AddChange` (added in the buy-loop change) — change coins
  are lost if `TryAddToContainer` fails mid-purchase. In practice removing the
  paid coins frees at least as many slots as the change needs, but it is not
  guaranteed.

Also the **drag-cancel refund gap**: `VendorSlotDisplay.MoveItem`'s drag path
charges the player, then hands the item to `DragProvider`. If the drag is never
dropped on a valid target the money is gone and the item may be too. The drag
system has no cancel/return-to-origin path (`DragProvider` has commented-out
`ReturnToOrigin` / `DropHere` stubs).

A single fix probably wants a "return this package to its sender, refunding if
it was a purchase" primitive, plus a floor-drop fallback for genuinely
full inventories.

## 3. "Keep the change" pricing (flavor)

The deleted `Currency.GetClosestPriceWithoutChange` / `RoundTo` (commit
`82146cd`) were a first pass at a deliberate mechanic, not just a broken
heuristic: if you overpay a vendor with a coin they can't break, you don't get
change back — framed in-world as a tip / haggling-reputation cue.

The buy loop now always deducts exact value and gives change. If this mechanic
is wanted later:

- resurrect the "round the price up to the nearest amount the player can pay
  without the vendor making change" math (the old methods, with the `uint`
  underflow and operator-precedence bugs fixed), and
- gate it on something — vendor disposition, a haggle stat, a difficulty
  setting — rather than applying it unconditionally.
