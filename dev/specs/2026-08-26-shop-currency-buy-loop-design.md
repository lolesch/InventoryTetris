# Close the shop buy loop: charge the player when taking Store items

Date: 2026-08-26
Status: Shipped 2026-08-29 — `03c7c92` (charge on taking Store items), `5409607`
(change-making tests), `1a39662` (1.5x buy price, pay-after-landing); on `main`.
Deferred items were split into `dev/specs/2026-08-26-shop-currency-followups.md`.
Resumes `82146cd "progress on spending cash when picking items from the shop"`.
Base: cut `feature/shop-currency` from `feature/mutablefloat-port` (merge-ready,
EditMode tests green) — not `origin/main`, which is pre-Unity-6000.3 and
pre-port.

## Problem

Taking an item out of the **Store** does not charge the player. Selling works.

The Store *is* already wired to vendor slots on this base (an earlier pickup
note assumed it was not): in `Example.unity` the Shop panel's `InventoryContainerDisplay`
(`InventoryProvider.StoreDisplay`) has its `slotDisplayPrefab` overridden to
`Assets/Prefabs/SlotDisplayVendor.prefab` — a variant of `SlotDisplay.prefab`
that removes `InventorySlotDisplay` and adds `VendorSlotDisplay`. Commit
`82146cd` landed that. So taking a Store item already runs
`VendorSlotDisplay.MoveItem` → `InventoryProvider.Instance.Inventory.TryPay(...)`.

Buying is free because [`CharacterInventory.TryPay`](../../Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs)
(new in `82146cd`, never functional) is broken two ways:

1. **It removes no coins.** It builds `new Package(this, new CurrencyItem(Copper), n)`
   and calls `AbstractDimensionalContainer.RemoveFromContainer`, whose
   `FindAllEqualItems` matches with `package.Value.Item == item` — reference
   equality. A freshly constructed `CurrencyItem` never equals a stored coin,
   so nothing is removed. `TryPay` returns `true` regardless.
2. **The affordability heuristic is wrong.** `Currency.GetClosestPriceWithoutChange`
   plus per-denomination `<` checks reject prices the player can afford
   (1 Gold, price 5 Silver → `cash.Silver 0 < price.Silver 5` → fails).

## Goal

Taking a Store item deducts its buy price (1.5× the item's `SellValue`) from
the player's coins, making change when the player overpays with larger
denominations. Vendor slots show, at a glance, which items the player can
afford; slots give hover feedback.

## Decisions

| Question | Decision |
| --- | --- |
| Money model | Keep money-as-items (`CurrencyItem` packages in the inventory). Fix `TryPay` in place; no scalar wallet. |
| Change-making | Compare on `Currency.Total`; deduct exact value; hand back change coins. |
| Buy price | `1.5 ×` `AbstractItem.SellValue`. Buyback and the sell slot stay at `1 ×`. |
| Markup in the UI | The item preview shows the buy price for vendor slots. |
| `VendorSupply` stub | Delete it. "Proper vendor container" recorded in the follow-up spec. |
| `GetClosestPriceWithoutChange` / `RoundTo` | Delete. The "keep the change" mechanic they hinted at is recorded in the follow-up spec. |
| Payment math location | Pure method on `Currency`, unit-tested; `TryPay` is a thin adapter (mirrors the MutableFloat-port shape). |
| Testing | Move `Currency.cs` into the `InventorySystem.Data` asmdef; NUnit EditMode tests. |
| Hover highlight | All slot types, via `AbstractSlotDisplay`. |
| Unaffordable indicator | Always visible on unaffordable vendor slots; re-evaluated on wallet change and restock. |

## Non-goals

- Scalar wallet / money-as-display-only.
- Buyback discount; per-vendor or per-item pricing; a wallet HUD; "you can't
  afford this" messaging on click.
- Finishing `VendorSupply` / making the Store a finite-or-infinite dedicated
  vendor container. The buy loop behaves identically whether the shelf is
  finite or infinite — orthogonal to this change.
- Item loss when the inventory is full during a coin payout (`//TODO` in
  `VendorSlotDisplay.DropItem` and `SellItenSlotDisplay`) and the drag-cancel
  refund gap. Recorded in the follow-up spec.
- Renaming the misspelled `SellItenSlotDisplay`.
- Fixing the pre-existing `AbstractDimensionalContainer.RemoveFromContainer`
  reference-equality quirk globally. `TryPay` stops depending on it (see
  component 2); `Sort()` and the drag paths that still call it pass items
  taken straight from `StoredPackages`, where reference equality holds.

## Components

### 1. `Currency` — move into the testable assembly, add the payment method

`Currency.cs` moves from `Assets/Scripts/InventorySystem/Data/Structs/` to
`Assets/Scripts/InventorySystem/Data/Statistics/` so it joins the
`InventorySystem.Data` asmdef and becomes reachable from the EditMode test
assembly. Namespace stays `ToolSmiths.InventorySystem.Data`; the `.meta` GUID
moves with the file, so no call site changes. (`Data/Statistics/` is an
imperfect folder name for a currency struct — the same wart the MutableFloat
port accepted for `StatModifier.cs`. A later "organize the Data assembly" pass
can rename it.)

**Delete** `GetClosestPriceWithoutChange` and `RoundTo`. Their only caller is
`TryPay`, and removing them also drops `Currency`'s last reference to
`CurrencyType` (which lives in `Assembly-CSharp` and would otherwise block the
asmdef move). Drop the now-unused `using ToolSmiths.InventorySystem.Data.Enums;`.

**Add:**

```csharp
/// <summary>
/// Works out how to pay <paramref name="price"/> out of this wallet, paying
/// smallest denominations first so the player keeps large coins. Returns
/// false (and default outs) when the wallet's total value is below the price.
/// </summary>
public readonly bool TryGetPayment(Currency price, out Currency toRemove, out Currency change)
```

Algorithm: `owed = price.Total`; if `Total < owed` return false. Walk
Copper → Iron → Silver → Gold; at each denomination take
`min(have, ceil(stillOwed / denominationValue))` and add its value to `paid`.
`toRemove` is the coins taken; `change = new Currency(paid - owed)`
(`paid >= owed` is guaranteed once `Total >= owed`, so no `uint` underflow).
`price.Total == 0` → returns true with both outs zero (free item, no charge).

Denomination values are the existing `Currency` constants
(`1`, `copperToIron`, `copperToSilver`, `copperToGold`).

### 2. `CharacterInventory` — real `TryPay`, plus `CanAfford`

```csharp
public bool TryPay(float buyValue)
{
    if (!CalculateCash().TryGetPayment(new Currency(buyValue), out var toRemove, out var change))
        return false;

    RemoveCurrency(CurrencyType.Copper, toRemove.Copper);
    RemoveCurrency(CurrencyType.Iron,   toRemove.Iron);
    RemoveCurrency(CurrencyType.Silver, toRemove.Silver);
    RemoveCurrency(CurrencyType.Gold,   toRemove.Gold);

    if (0 < change.Total)
        AddChange(change);

    return true;
}

public bool CanAfford(float buyValue) => new Currency(buyValue).Total <= CalculateCash().Total;
```

- `RemoveCurrency(CurrencyType type, uint amount)` — private. Snapshots
  `StoredPackages.Keys`, and for each `CurrencyItem` package of `type` calls
  `RemoveAtPosition(position, new Package(this, stored.Item, take))` until
  `amount` is satisfied. This is the mirror of `CalculateCash` and replaces
  the four broken `RemoveFromContainer(new Package(this, new CurrencyItem(...)))`
  calls.
- `AddChange(Currency change)` — private. For each non-zero denomination,
  `new Package(this, ItemProvider.Instance.GenerateCurrency(type), amount)`
  then `TryAddToContainer(ref package)` (Gold → Copper order). Mirrors
  `VendorSlotDisplay.DropItem`. Each denomination of change is always below
  its stack limit (the overshoot at any denomination is `< that denomination's
  value`). The full-inventory edge (`TryAddToContainer` fails, change lost)
  matches the codebase's existing acknowledged `//TODO` pattern and is covered
  by the follow-up spec — this change does not make it worse.

**Delete** the `VendorSupply` class and the `// CONTINUE HERE ...` marker at
the bottom of the file (11 lines, zero references). `RestockStore()` in
`InventoryProvider` and `Store` as a `CharacterInventory` are untouched.

### 3. `VendorSlotDisplay` — markup, affordability gating, pay-after-landing

```csharp
internal const float Markup = 1.5f;
internal static float BuyPrice(AbstractItem item) => item.SellValue * Markup;
```

`MoveItem` currently calls `TryPay` *before* the item moves, and its
right-click / shift branches (byte-identical today — merge them) put the item
back on the shelf on `TryAddToContainer` failure **without refunding**. Once
`TryPay` actually takes money, that is a live money-loss bug. Reorder:

1. Gate all paths on `wallet.CanAfford(BuyPrice(package.Item))` up front
   (replaces the current `TryPay` early-return).
2. Right-click / shift (immediate move): remove from Store → `TryAddToContainer`.
   On success, `TryPay(BuyPrice(...))` then hand any remainder to the drag.
   On failure, `AddAtPosition` back to the Store, no charge.
3. Drag: remove from Store → `TryPay(BuyPrice(...))` (affordability already
   confirmed) → `DragProvider.SetPackage`. The drag system has no clean
   cancel/refund; that gap is pre-existing and deferred (follow-up spec).

Buyback (`DropItem`) is unchanged — still pays out `SellValue` at `1 ×`.

### 4. Item preview shows the buy price

`PreviewProvider.RefreshPreviewDisplay(Package package, AbstractSlotDisplay slot)`
already receives the hovered slot. Compute
`slot is VendorSlotDisplay ? VendorSlotDisplay.BuyPrice(package.Item) : -1f`
and pass it to the hovered `PreviewDisplay`.

`PreviewDisplay.RefreshDisplay(Package package, Package compareTo)` gains an
optional `float priceOverride = -1f`. When `>= 0f`, the preview's
`CurrencyDisplay` shows `new Currency(priceOverride)` instead of
`new Currency(package.Item.SellValue)`. Only the hovered-item display and only
the two-arg overload change; the compare displays (which show the player's
equipped item) and the one-arg overload are untouched.

### 5. Slot hover highlight — `AbstractSlotDisplay`, all slot types

- New `[SerializeField] protected Image hoverOutline;` on `AbstractSlotDisplay`,
  null-guarded like the other image fields.
- `OnPointerEnter` enables it; `OnPointerExit` disables it. (~4 lines added to
  methods that already exist and already do `SetHoveredSlot` + preview fade.)
- Prefab work: add a transparent outline `Image` child and wire the field on
  both slot prefabs —
  - `Assets/Prefabs/SlotDisplay.prefab` (covers Inventory, Stash, and — via
    the `SlotDisplayVendor.prefab` variant — the Store)
  - `Assets/Prefabs/EquipmentISlotDisplay.prefab` (the 14 equipment slots)
- Default: a thin white outline Image (an inner or outer border sprite),
  hidden by default. Exact color/alpha/thickness are prefab Inspector values.

### 6. Unaffordable indicator — `VendorSlotDisplay`, always visible

- `AbstractSlotDisplay.RefreshSlotDisplay(Package)` becomes `virtual`.
- `VendorSlotDisplay` overrides it: `base.RefreshSlotDisplay(package)`, then
  apply affordability using `package` (caches it in a field so the state can
  be re-applied without a container refresh). When
  `!InventoryProvider.Instance.Inventory.CanAfford(BuyPrice(package.Item))`:
  tint `slotBackground` dark red (~0.4 alpha) and set `icon.color` to grey
  (~0.5). Otherwise restore both. Empty slot (item bought) → clear the state.
- `OnEnable` / `OnDisable` subscribe / unsubscribe
  `InventoryProvider.Instance.Inventory.OnContentChanged` and re-apply
  affordability to the cached package on that event (same lifecycle pattern
  `AbstractSlotDisplay` already uses for `DragProvider.OnOverlapping`).
  `RestockStore()` already fires the Store's `OnContentChanged`, which drives
  `AbstractContainerDisplay.Refresh` → `RefreshSlotDisplay`.

### How the two indicators stack

`slotBackground` tint = affordability. `hoverOutline` = hover. Independent
images: an unaffordable item under the cursor shows both — red-tinted
background and a bright outline.

## Behavior changes

1. Taking a Store item now costs `1.5 × SellValue` in coins, with change given
   when the player overpays with larger denominations. Previously free.
2. Right-click / shift-buying into a full inventory bounces the item back to
   the shelf and charges nothing (previously would have charged nothing only
   because `TryPay` was inert; now correct by construction).
3. `TryPay` deducts exact value and gives change, instead of the old intent of
   rounding the price up to a no-change amount (that idea is recorded for
   later — see follow-up spec).
4. Vendor slots are visibly dimmed when unaffordable; all slots show a hover
   outline.

## Testing

New `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs` (same asmdef
and folder as `MutableFloatTests.cs`), covering `Currency.TryGetPayment`:

- exact payment → `toRemove` equals price, `change` is zero
- overpay: wallet `{1 Gold}`, price `3 Silver` → `toRemove {1 Gold}`,
  `change {2 Silver}`
- smallest-first: wallet `{4 Silver, 1 Gold}`, price `3 Silver` →
  `toRemove {3 Silver}`, no Gold touched, `change` zero
- partial break: wallet `{2 Silver, 1 Gold}`, price `3 Silver` →
  `toRemove {2 Silver, 1 Gold}`, `change {4 Silver}`
- cannot afford: `Total < price.Total` → false, both outs default
- free item: price `0` → true, both outs zero
- non-canonical wallet: 25 loose Copper (two packages) covering a `1 Iron`
  price

No EditMode tests for `TryPay` / `CanAfford` / the display code — they live in
`Assembly-CSharp`, which has no test assembly, and this change deliberately
does not add one (same call the MutableFloat port made). Manual verification:
open `Example.unity`, buy from the Store via drag, right-click, and shift-click;
watch the player's coin packages and the vendor-slot dimming; hover slots in
every panel; sell an item back and confirm the `1 ×` payout is unchanged.

## Risks

- **`Currency.cs` asmdef move.** Safe as long as the `.meta` moves with the
  `.cs` (GUID is preserved). No scene/prefab/asset serializes a `Currency`
  field — it is constructed at runtime everywhere (`grep` confirms only
  `new Currency(...)` call sites). Deleting `GetClosestPriceWithoutChange` /
  `RoundTo` is a prerequisite: they are the only thing tying `Currency` to
  `CurrencyType` (`Assembly-CSharp`), which the asmdef cannot reference.
- **`RefreshSlotDisplay` → `virtual`.** Dispatch already goes through
  `List<AbstractSlotDisplay>`, so this is transparent to callers.
- **Prefab edits.** Adding one child Image to two slot prefabs; propagates to
  their variants and to runtime-instantiated slots. Must be committed with the
  scene re-serialized if Unity touches it.
- **`feature/container-labels`.** If that branch merges first, expect trivial
  conflicts in the four container constructors (`AbstractDimensionalContainer`,
  `CharacterEquipment`, `CharacterInventory`, `InventoryProvider.Awake`) from
  its added `label` ctor param. Unrelated to the currency logic.
