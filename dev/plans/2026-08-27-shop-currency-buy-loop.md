# Shop Currency Buy Loop Implementation Plan

> **For agentic workers:** execute this plan inline in the current session, task-by-task, with a review checkpoint after each task — never dispatch subagents (project rule). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Taking an item out of the Store deducts its buy price (`1.5 × SellValue`) from the player's coins, makes change, and shows at a glance which vendor items are affordable.

**Architecture:** The change-making math becomes a pure, unit-tested method on the `Currency` struct (`TryGetPayment`), which moves into the `InventorySystem.Data` assembly so the EditMode test project can reach it. `CharacterInventory.TryPay` becomes a thin adapter that calls it and moves real coin packages in/out of the inventory. `VendorSlotDisplay` gates purchases on affordability, applies the markup, and reorders its buy paths so money is never taken for an item that fails to land. Two independent slot indicators (a hover outline on `AbstractSlotDisplay`, an unaffordable tint on `VendorSlotDisplay`) give visual feedback.

**Tech Stack:** Unity 6000.3.9f1, C# (Roslyn), Unity Test Framework (NUnit) EditMode tests, `.asmdef` assembly definitions.

## Global Constraints

- **Base branch:** work stays on `feature/shop-currency` (checked out), cut from `feature/mutablefloat-port`. Keep that base — `main` is pre-Unity-6000.3 and pre-port.
- **Money model:** money-as-items only. `CurrencyItem` packages in the inventory. No scalar wallet, no wallet HUD.
- **Buy price:** `1.5 × AbstractItem.SellValue`. Buyback (`VendorSlotDisplay.DropItem`) and the sell slot (`SellItenSlotDisplay`) keep the `1 ×` payout they have today.
- **`Currency` namespace:** keep the `namespace` line as `ToolSmiths.InventorySystem.Data` even though the file changes folders, so no `using` changes at any call site.
- **`Currency.cs.meta` GUID travels with the `.cs`** (`git mv` both) so every asset reference resolves unchanged.
- **Payment math lives on `Currency`, unit-tested; `TryPay` is a thin adapter** (mirrors the MutableFloat-port shape).
- **Commits:** small and frequent, one per step group below. Repo message style: `feat:` / `fix:` / `refactor:` / `chore:` prefix where it fits, body explains *why*. End every commit message with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  ```
- **Scope is Tasks 1–5 exactly.** The deferred items in `dev/specs/2026-08-26-shop-currency-followups.md` (proper vendor container, full-inventory item-loss fix, "keep the change" pricing) stay for later.

## Green

**Green** = the project compiles with zero console errors *and*, wherever automated tests exist, `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` passes (`InventorySystem.Data.Tests`). A compile error anywhere blocks the Test Runner entirely, so every commit point below lands on green. The "recompile → green" steps are real gates — do not commit past a red one.

Batch-mode test run (needs the Editor **closed** — it holds a single-instance project lock):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" -runTests -batchmode -projectPath "C:/Users/loles/Desktop/LEONID/InventoryTetris" -testPlatform EditMode -testResults "C:/Users/loles/AppData/Local/Temp/claude/results.xml" -logFile -
```

Exit code `0` = all passed, `2` = failures. `results.xml` root carries `result="Passed" total="N" passed="N" failed="0"`.

---

## File Structure

| File | Responsibility | Change |
| --- | --- | --- |
| `Assets/Scripts/InventorySystem/Data/Structs/Currency.cs` → `Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs` | The currency value type + pure change-making math | **Move** into `InventorySystem.Data` asmdef; delete `GetClosestPriceWithoutChange` / `RoundTo`; add `TryGetPayment` |
| `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs` | Player inventory; now the real payment adapter | Rewrite `TryPay`; add `CanAfford`, `RemoveCurrency`, `AddChange`; delete `VendorSupply` + `// CONTINUE HERE` marker |
| `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs` | Locks in `Currency.TryGetPayment` | **Create** |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/VendorSlotDisplay.cs` | Vendor slot: markup, affordability gate, pay-after-landing, unaffordable tint | Rewrite `MoveItem`; add `Markup` / `BuyPrice`; override `RefreshSlotDisplay`, `OnEnable`, `OnDisable` |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs` | Base slot behaviour | Add `hoverOutline` field + enter/exit toggle; make `RefreshSlotDisplay`, `OnEnable`, `OnDisable` overridable |
| `Assets/Scripts/InventorySystem/Runtime/Provider/PreviewProvider.cs` | Routes the hovered package + slot to the preview | Pass a `priceOverride` for vendor slots |
| `Assets/Scripts/InventorySystem/GUI/Components/Displays/PreviewDisplay.cs` | Renders the hovered-item tooltip | Two-arg `RefreshDisplay` gains optional `priceOverride` |
| `Assets/Prefabs/SlotDisplay.prefab` | Inventory / Stash / (variant) Store slot | **Manual Unity edit:** add `HoverOutline` child Image, wire the field |
| `Assets/Prefabs/EquipmentISlotDisplay.prefab` | The 14 equipment slots | **Manual Unity edit:** same |

---

## Task 1: Payment math + a `TryPay` that actually charges

**Files:**
- Modify → move: `Assets/Scripts/InventorySystem/Data/Structs/Currency.cs` to `Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs` (+ `.meta`)
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs`
- Create: `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs`

**Interfaces:**
- Consumes: existing `Currency` constants `copperToIron` (20), `copperToSilver` (240), `copperToGold` (1200); `Currency.Total`; `Currency(uint total)` and `Currency(uint copper, uint iron, uint silver, uint gold)` ctors; `AbstractDimensionalContainer.RemoveAtPosition`, `.TryAddToContainer`, `.StoredPackages`; `ItemProvider.Instance.GenerateCurrency(CurrencyType)`.
- Produces:
  - `Currency.TryGetPayment(Currency price, out Currency toRemove, out Currency change) → bool` (readonly instance method)
  - `CharacterInventory.TryPay(float buyValue) → bool` (existing signature, now functional)
  - `CharacterInventory.CanAfford(float buyValue) → bool` (new, public)

This is one task because it is the smallest unit that leaves the project compiling **and** re-adds the automated test coverage: deleting the dead `Currency` methods breaks `CharacterInventory.TryPay`, and `CurrencyTests` cannot compile until `Currency` is in the test-visible assembly. Work the steps in order — the project stays green at every commit point.

- [ ] **Step 1: Add `Currency.TryGetPayment` (file still in `Data/Structs/`)**

In `Assets/Scripts/InventorySystem/Data/Structs/Currency.cs`, add this method to the `Currency` struct (put it just above `ToString()`; leave `GetClosestPriceWithoutChange` / `RoundTo` in place for now):

```csharp
/// <summary>
/// Works out how to pay <paramref name="price"/> from this wallet, spending the
/// smallest denominations first so large coins are kept. Returns false (both
/// outs left at zero) when this wallet's total value is below the price; a zero
/// price returns true and charges nothing.
/// </summary>
public readonly bool TryGetPayment(Currency price, out Currency toRemove, out Currency change)
{
    toRemove = default;
    change = default;

    var owed = price.Total;

    if (Total < owed)
        return false;

    uint paid = 0u;

    var copper = Take(Copper, 1u);
    var iron = Take(Iron, copperToIron);
    var silver = Take(Silver, copperToSilver);
    var gold = Take(Gold, copperToGold);

    toRemove = new Currency(copper, iron, silver, gold);
    change = new Currency(paid - owed); // paid >= owed is guaranteed once Total >= owed
    return true;

    uint Take(uint have, uint denomination)
    {
        if (owed <= paid || 0u == have)
            return 0u;

        var stillOwed = owed - paid;
        var wanted = (stillOwed + denomination - 1u) / denomination; // ceil(stillOwed / denomination)
        var taken = have < wanted ? have : wanted;

        paid += taken * denomination;
        return taken;
    }
}
```

- [ ] **Step 2: Recompile → green**

`TryGetPayment` is unused at this point — that is the expected intermediate state, not a problem to chase.

- [ ] **Step 3: Rewrite the payment path in `CharacterInventory`**

In `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs`, replace the `TryPay` method (around line 133) in full with:

```csharp
public bool TryPay(float buyValue)
{
    if (!CalculateCash().TryGetPayment(new Currency(buyValue), out var toRemove, out var change))
        return false;

    RemoveCurrency(CurrencyType.Copper, toRemove.Copper);
    RemoveCurrency(CurrencyType.Iron, toRemove.Iron);
    RemoveCurrency(CurrencyType.Silver, toRemove.Silver);
    RemoveCurrency(CurrencyType.Gold, toRemove.Gold);

    if (0u < change.Total)
        AddChange(change);

    return true;
}

public bool CanAfford(float buyValue) => new Currency(buyValue).Total <= CalculateCash().Total;

/// <summary>
/// Removes <paramref name="amount"/> coins of <paramref name="type"/> from the
/// stored currency packages. The remove-side mirror of <see cref="CalculateCash"/>;
/// unlike RemoveFromContainer it matches on CurrencyType, not reference equality.
/// </summary>
private void RemoveCurrency(CurrencyType type, uint amount)
{
    if (0u == amount)
        return;

    foreach (var position in StoredPackages.Keys.ToList())
    {
        if (0u == amount)
            break;

        if (!StoredPackages.TryGetValue(position, out var stored))
            continue;

        if (stored.Item is not CurrencyItem coin || coin.CurrencyType != type)
            continue;

        var take = Math.Min(amount, stored.Amount);
        _ = RemoveAtPosition(position, new Package(this, stored.Item, take));
        amount -= take;
    }

    if (0u < amount)
        Debug.LogWarning($"{nameof(RemoveCurrency)}: {amount} {type} left unremoved - wallet desync?");
}

/// <summary>
/// Returns change coins to the inventory, largest denomination first. Each
/// denomination of a valid change amount is always below its stack limit
/// (the overshoot at any denomination is less than that denomination's value).
/// The full-inventory edge (change dropped) is acknowledged - see
/// dev/specs/2026-08-26-shop-currency-followups.md section 2.
/// </summary>
private void AddChange(Currency change)
{
    AddCoins(CurrencyType.Gold, change.Gold);
    AddCoins(CurrencyType.Silver, change.Silver);
    AddCoins(CurrencyType.Iron, change.Iron);
    AddCoins(CurrencyType.Copper, change.Copper);

    void AddCoins(CurrencyType type, uint count)
    {
        if (0u == count)
            return;

        var package = new Package(this, ItemProvider.Instance.GenerateCurrency(type), count);
        _ = TryAddToContainer(ref package);
    }
}
```

Leave `CalculateCash()` exactly as it is (still `private`, still returns `Currency`).

- [ ] **Step 4: Delete the `VendorSupply` stub**

In the same file, below the closing `}` of the `CharacterInventory` class, delete the `// CONTINUE HERE ...` comment line and the entire `public class VendorSupply : AbstractDimensionalContainer { ... }` class (~11 lines). Keep the final `}` that closes the `namespace`. Nothing references `VendorSupply` (`grep` confirms: only this definition and the follow-ups spec).

- [ ] **Step 5: Recompile → green**

`Currency.GetClosestPriceWithoutChange` / `RoundTo` are now unused (only `TryPay` called them) — expected; Step 7 deletes them. `RestockStore()` and `Store` are unchanged.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/InventorySystem/Data/Structs/Currency.cs Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs
git commit -m "$(cat <<'EOF'
feat: charge the player when taking Store items

CharacterInventory.TryPay was inert - it built fresh CurrencyItem packages
and handed them to RemoveFromContainer, whose reference-equality match never
found the stored coins, so it always returned true having removed nothing.

- Currency.TryGetPayment: pure change-making math. Compares on Total, spends
  smallest denominations first, hands back change coins. Returns false when
  the wallet total is below the price; a zero price is free.
- TryPay: thin adapter over TryGetPayment. Removes the exact coins via a new
  CurrencyType-matching RemoveCurrency (mirror of CalculateCash), adds change
  via AddChange.
- CanAfford: Total-based affordability check for the vendor UI.
- Delete the unreferenced VendorSupply stub and its marker comment; the
  proper-vendor-container idea is in the follow-ups spec.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 7: Delete the dead pricing methods from `Currency.cs`**

In `Assets/Scripts/InventorySystem/Data/Structs/Currency.cs`:
- Delete `GetClosestPriceWithoutChange` (the whole method) and `RoundTo` (the whole method).
- Delete the `using ToolSmiths.InventorySystem.Data.Enums;` line — it was only there for `CurrencyType`, which those two methods were the last users of. Keep `using System;` and `using UnityEngine;`.

- [ ] **Step 8: Recompile → green**

`Currency` now has zero references into `Assembly-CSharp` (no more `CurrencyType`), which is what makes the Step 9 move legal.

- [ ] **Step 9: Move `Currency.cs` into the `InventorySystem.Data` assembly**

```bash
git mv Assets/Scripts/InventorySystem/Data/Structs/Currency.cs Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs
git mv Assets/Scripts/InventorySystem/Data/Structs/Currency.cs.meta Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs.meta
```

The `.meta` GUID (`4ea78607c81596243963d5fbb365c86f`) is preserved. The `Data/Statistics/` folder is covered by `InventorySystem.Data.asmdef`, so `Currency` joins that assembly. Keep the `namespace` line as `ToolSmiths.InventorySystem.Data`.

- [ ] **Step 10: Re-import → green, and check for scene re-serialization**

`Assembly-CSharp` auto-references `InventorySystem.Data`, so `CharacterInventory`, `CurrencyItem`, `PreviewDisplay` etc. still see `Currency`. Then run `git status`: if Unity re-serialized `Assets/Scenes/Example.unity` or `Assets/Prefabs/PLAYER.prefab`, inspect the diff — it carries nothing about `Currency` (no scene/prefab serializes a `Currency` field). Commit any unavoidable Unity 6000.3.9f1 editor churn as its own separate commit, the way the MutableFloat port did (`re-serialize ... for the MutableFloat stat shape`).

- [ ] **Step 11: Commit the move**

```bash
git add Assets/Scripts/InventorySystem/Data/
git commit -m "$(cat <<'EOF'
refactor: move Currency into the InventorySystem.Data assembly

Currency.TryGetPayment needs EditMode test coverage, and the test assembly
(InventorySystem.Data.Tests) can only reference asmdef assemblies. Deleting
the GetClosestPriceWithoutChange / RoundTo heuristic first drops Currency's
last tie to CurrencyType (Assembly-CSharp), which an asmdef cannot reference.

The .meta GUID moves with the file so no asset reference changes; namespace
stays ToolSmiths.InventorySystem.Data so no call site changes. Data/Statistics/
is an imperfect home for a currency struct - the same wart the port took for
StatModifier.cs; a later "organize the Data assembly" pass can rename it.

The "keep the change" mechanic those methods hinted at is in the follow-ups
spec.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 12: Write the failing test file**

Create `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs`:

```csharp
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Locks in Currency.TryGetPayment: spend smallest denominations first, make
    /// change on overpay, refuse when the wallet total is short, free when zero.
    /// </summary>
    [TestFixture]
    public sealed class CurrencyTests
    {
        // Currency(copper, iron, silver, gold). Denomination values: 1 / 20 / 240 / 1200.
        private static Currency Coins(uint copper = 0, uint iron = 0, uint silver = 0, uint gold = 0) =>
            new(copper, iron, silver, gold);

        private static void AssertCoins(Currency actual, uint copper, uint iron, uint silver, uint gold)
        {
            Assert.That(actual.Copper, Is.EqualTo(copper), "copper");
            Assert.That(actual.Iron, Is.EqualTo(iron), "iron");
            Assert.That(actual.Silver, Is.EqualTo(silver), "silver");
            Assert.That(actual.Gold, Is.EqualTo(gold), "gold");
        }

        [Test]
        public void ExactPayment_TakesThePrice_NoChange()
        {
            var ok = Coins(silver: 3).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 3, 0);
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void Overpay_BreaksASingleGold_AndReturnsChange()
        {
            var ok = Coins(gold: 1).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 0, 1);
            AssertCoins(change, 0, 0, 2, 0); // 1200 - 720 = 480 = 2 silver
        }

        [Test]
        public void SmallestFirst_SpendsSilver_LeavesGoldUntouched()
        {
            var ok = Coins(silver: 4, gold: 1).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 3, 0);
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void PartialBreak_TakesAllSilverThenOneGold_AndReturnsChange()
        {
            var ok = Coins(silver: 2, gold: 1).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 2, 1); // 480 + 1200 = 1680 paid
            AssertCoins(change, 0, 0, 4, 0);   // 1680 - 720 = 960 = 4 silver
        }

        [Test]
        public void CannotAfford_ReturnsFalse_WithDefaultOuts()
        {
            var ok = Coins(silver: 2).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.False);
            Assert.That(toRemove.Total, Is.EqualTo(0u));
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void FreeItem_ReturnsTrue_ChargesNothing()
        {
            var ok = Coins(copper: 5).TryGetPayment(new Currency(0u), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            Assert.That(toRemove.Total, Is.EqualTo(0u));
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void NonCanonicalWallet_LooseCopperCoversAnIronPrice()
        {
            // 25 loose copper (in practice two packages: a full 20-stack + 5)
            var ok = Coins(copper: 25).TryGetPayment(Coins(iron: 1), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 20, 0, 0, 0); // price total is 20
            Assert.That(change.Total, Is.EqualTo(0u));
        }
    }
}
```

- [ ] **Step 13: Run the tests to confirm they pass**

Unity will generate `CurrencyTests.cs.meta` on import. Then `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`.

Expected: `InventorySystem.Data.Tests` shows `CurrencyTests` with 7/7 passing, `MutableFloatTests` still green.

(The algorithm was implemented in Step 1 — it had to be, for the Step 3 rewrite to mean anything. To watch the suite go red first: temporarily change `if (Total < owed)` to `if (false)` and `var taken = have < wanted ? have : wanted;` to `var taken = 0u;` in `TryGetPayment`, Run All, see `CurrencyTests` fail, then revert.)

- [ ] **Step 14: Commit**

```bash
git add Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs.meta
git commit -m "$(cat <<'EOF'
test: lock in Currency.TryGetPayment change-making

Exact payment, overpay-with-change, smallest-first (large coins kept),
partial break (spend all of one denomination then dip into the next),
cannot-afford, free item, and a non-canonical wallet (loose copper past a
stack boundary).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 15: Manual play-mode check**

Open `Assets/Scenes/Example.unity`, enter Play mode. Use the debug buttons to give the player some Gold. Drag an item out of the Store into the inventory. Expected: a Gold coin package disappears and Silver/Iron/Copper change packages appear; the player's total drops by roughly `1.5 ×` the item's sell value. Exit Play mode.

---

## Task 2: Vendor markup + pay-after-landing in `VendorSlotDisplay`

**Files:**
- Modify: `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/VendorSlotDisplay.cs`

**Interfaces:**
- Consumes: `CharacterInventory.CanAfford(float)`, `CharacterInventory.TryPay(float)` (Task 1); `AbstractItem.SellValue`; `AbstractDimensionalContainer.RemoveAtPosition`, `.AddAtPosition`, `.TryAddToContainer`, `.TryGetItemAt`; `DragProvider.Instance.SetPackage`.
- Produces:
  - `VendorSlotDisplay.Markup` — `internal const float` = `1.5f`
  - `VendorSlotDisplay.BuyPrice(AbstractItem item) → float` — `internal static`, `item.SellValue * Markup`

**Why the reorder matters:** today `MoveItem` calls `TryPay` *before* the item moves, and the right-click / shift branches put the item back on the shelf on `TryAddToContainer` failure **without refunding**. Now that `TryPay` actually takes money, that path would lose the player's coins. The fix: confirm affordability up front, and only call `TryPay` once the item has somewhere to go.

- [ ] **Step 1: Add the markup constants**

In `VendorSlotDisplay.cs`, inside the class, above `SetDisplaySize`:

```csharp
internal const float Markup = 1.5f;
internal static float BuyPrice(AbstractItem item) => item.SellValue * Markup;
```

`AbstractItem` is already in scope via `using ToolSmiths.InventorySystem.Items;`.

- [ ] **Step 2: Rewrite `MoveItem`**

Replace the `MoveItem` method (around line 34) in full with:

```csharp
protected override void MoveItem(PointerEventData eventData)
{
    if (Container == null)
        return;

    var position = Position;

    if (!Container.TryGetItemAt(ref position, out var package))
        return;

    var wallet = InventoryProvider.Instance.Inventory;
    var price = BuyPrice(package.Item);

    if (!wallet.CanAfford(price))
        return;

    FadeOutPreview();

    // Right-click and shift-click both move the item straight to the inventory.
    #region BUY: IMMEDIATE MOVE
    if (eventData.button == PointerEventData.InputButton.Right || Input.GetKey(KeyCode.LeftShift))
    {
        _ = Container.RemoveAtPosition(position, package);

        if (wallet.TryAddToContainer(ref package))
            _ = wallet.TryPay(price); // affordability already confirmed
        else
            _ = Container.AddAtPosition(position, package); // bounced back to the shelf, no charge

        return;
    }
    #endregion

    // Drag: the drag system has no clean cancel/refund path; that gap is
    // pre-existing and deferred (follow-ups spec).
    #region BUY: DRAG
    _ = Container.RemoveAtPosition(position, package);

    _ = wallet.TryPay(price);

    var positionOffset = Position - position;

    DragProvider.Instance.SetPackage(this, package, positionOffset);
    #endregion
}
```

Touch only `MoveItem`; `SetDisplaySize` and `DropItem` (buyback, `1 ×`) stay as they are.

- [ ] **Step 3: Recompile → green**

- [ ] **Step 4: Manual play-mode check**

`Example.unity`, Play mode. Give the player a moderate amount of Gold.
- Drag a Store item to the inventory → coins deducted at `1.5 ×` sell value, change returned.
- Right-click a Store item → same deduction, item lands in the inventory.
- Shift-click a Store item → same.
- Fill the inventory, then right-click a Store item → item bounces back to the shelf, **no** coins deducted.
- Try to buy an item you cannot afford → nothing happens (no move, no charge).
- Sell an item back on the sell slot → still paid `1 ×` sell value (unchanged).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/InventorySystem/GUI/InventoryDisplays/VendorSlotDisplay.cs
git commit -m "$(cat <<'EOF'
feat: vendor buy price (1.5x) and pay-after-landing

MoveItem paid before the item moved, and its right-click / shift branches
returned the item to the shelf on a failed TryAddToContainer without
refunding - a live money-loss bug now that TryPay takes real coins.

- Markup / BuyPrice: 1.5x AbstractItem.SellValue. Buyback and the sell slot
  stay at 1x.
- Gate every path on CanAfford up front.
- Immediate move (right-click / shift, now merged - they were byte-identical):
  remove from Store, TryAddToContainer, and only TryPay on success; on
  failure AddAtPosition back with no charge.
- Drag: remove, TryPay (affordability confirmed), hand to DragProvider.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Show the buy price in the item preview

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/PreviewProvider.cs`
- Modify: `Assets/Scripts/InventorySystem/GUI/Components/Displays/PreviewDisplay.cs`

**Interfaces:**
- Consumes: `VendorSlotDisplay.BuyPrice(AbstractItem)` (Task 2); `new Currency(float)`.
- Produces: `PreviewDisplay.RefreshDisplay(Package package, Package compareTo, float priceOverride = -1f)` — the two-arg overload gains a trailing optional parameter; existing two-arg callers are unaffected.

- [ ] **Step 1: Add the `priceOverride` parameter to `PreviewDisplay`**

In `PreviewDisplay.cs`, change the two-arg overload signature:

```csharp
public void RefreshDisplay(Package package, Package compareTo, float priceOverride = -1f)
```

Inside that method, replace the `goldValue` block:

```csharp
if (goldValue)
    goldValue.RefreshDisplay(new Currency(package.Item.SellValue)); //? $"{package.Item.GoldValue}" : string.Empty;
```

with:

```csharp
if (goldValue)
    goldValue.RefreshDisplay(0f <= priceOverride
        ? new Currency(priceOverride)
        : new Currency(package.Item.SellValue));
```

Leave the one-arg `RefreshDisplay(Package package)` overload and the tuple `RefreshDisplay((Package, Package) data)` shim untouched — the shim keeps calling the two-arg overload, which now defaults `priceOverride` to `-1f`.

- [ ] **Step 2: Pass the vendor buy price from `PreviewProvider`**

In `PreviewProvider.RefreshPreviewDisplay`, in the `else` branch (non-equipment-slot path), just before `hoveredItem.RefreshDisplay(package, equippedItems[index]);` add:

```csharp
var priceOverride = slot is VendorSlotDisplay && package.Item != null
    ? VendorSlotDisplay.BuyPrice(package.Item)
    : -1f;
```

and change that call to:

```csharp
hoveredItem.RefreshDisplay(package, equippedItems[index], priceOverride);
```

`PreviewProvider` already has `using ToolSmiths.InventorySystem.GUI.InventoryDisplays;` (it uses `EquipmentSlotDisplay` unqualified a few lines up), so `VendorSlotDisplay` resolves unqualified. The `package.Item != null` guard matters: `RefreshPreviewDisplay` is also called on fade-out with a null-item package, and `BuyPrice` would dereference it.

Change only the hovered-item path; leave the `compareDisplay1` / `compareDisplay2` calls and the `slot is EquipmentSlotDisplay` branch as they are.

- [ ] **Step 3: Recompile → green**

`VendorSlotDisplay` and `PreviewProvider` are both `internal` in `Assembly-CSharp` and `BuyPrice` is `internal static`, so the cross-file reference resolves.

- [ ] **Step 4: Manual play-mode check**

`Example.unity`, Play mode. Hover an item in the inventory → tooltip shows its sell value (`1 ×`). Hover the same kind of item in the Store → tooltip's currency line shows `1.5 ×` that value. Hover an equipped item / equipment slot → unchanged.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/InventorySystem/Runtime/Provider/PreviewProvider.cs Assets/Scripts/InventorySystem/GUI/Components/Displays/PreviewDisplay.cs
git commit -m "$(cat <<'EOF'
feat: item preview shows the vendor buy price

When the hovered slot is a VendorSlotDisplay, the preview's CurrencyDisplay
shows BuyPrice (1.5x SellValue) instead of the raw sell value. Only the
hovered-item display and only the two-arg RefreshDisplay overload change;
the compare displays and the one-arg overload are untouched.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Slot hover highlight on all slot types

**Files:**
- Modify: `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs`
- Manual Unity edit: `Assets/Prefabs/SlotDisplay.prefab`, `Assets/Prefabs/EquipmentISlotDisplay.prefab`

**Interfaces:**
- Consumes: nothing new.
- Produces: `AbstractSlotDisplay.hoverOutline` — `[SerializeField] protected Image`, toggled on pointer enter/exit.

- [ ] **Step 1: Add the `hoverOutline` field and toggle**

In `AbstractSlotDisplay.cs`:

Add the field next to the other image fields (after `[SerializeField] protected Image slotBackground;`):

```csharp
[SerializeField] protected Image hoverOutline;
```

In `OnPointerEnter`, after `FadeInPreview();`:

```csharp
if (hoverOutline)
    hoverOutline.enabled = true;
```

In `OnPointerExit`, after `FadeOutPreview();`:

```csharp
if (hoverOutline)
    hoverOutline.enabled = false;
```

In `OnEnable`, add a reset (slots are pooled / re-instantiated):

```csharp
if (hoverOutline)
    hoverOutline.enabled = false;
```

- [ ] **Step 2: Recompile → green**

`hoverOutline` is unassigned on every prefab until Step 4, so the null guards keep this dormant — expected, not a missing wire to chase.

- [ ] **Step 3: Commit the code**

```bash
git add Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs
git commit -m "$(cat <<'EOF'
feat: hover outline hook on AbstractSlotDisplay

New serialized hoverOutline Image, null-guarded like the other image fields,
enabled on OnPointerEnter and disabled on OnPointerExit / OnEnable. Prefab
wiring in the next commit.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: [Manual Unity Editor step] Add the outline Image to `SlotDisplay.prefab`**

In the Project window, open `Assets/Prefabs/SlotDisplay.prefab` in Prefab Mode.
1. Add a child UI Image to the slot root: right-click the root ▸ `UI ▸ Image`. Name it `HoverOutline`.
2. Order it so it draws on top (last sibling), or place it just above the item display — designer's call.
3. RectTransform: anchor preset `stretch / stretch`, all offsets `0` (fills the slot).
4. Image: assign a 9-sliced border/outline sprite (reuse the sprite the slot's `frame` Image uses if it is a border, or any thin-border sprite), Image Type `Sliced`. Set a thin white-ish color, low-to-moderate alpha. Exact color / alpha / thickness are Inspector values to taste.
5. **Uncheck the Image component's `Enabled` checkbox** (starts hidden).
6. Select the slot root, find the `Abstract Slot Display`-derived component (`InventorySlotDisplay`), and drag `HoverOutline` onto the new **`Hover Outline`** field.
7. Save the prefab.

`SlotDisplayVendor.prefab` is a **variant** of `SlotDisplay.prefab` — the child and the field override propagate automatically. Verify: open `SlotDisplayVendor.prefab`, confirm `HoverOutline` is present and the `Hover Outline` field on `VendorSlotDisplay` is populated (inherited).

- [ ] **Step 5: [Manual Unity Editor step] Add the outline Image to `EquipmentISlotDisplay.prefab`**

Same procedure on `Assets/Prefabs/EquipmentISlotDisplay.prefab` (not a variant of `SlotDisplay` — needs its own child). Wire the `Hover Outline` field on its `EquipmentSlotDisplay` component. Save.

- [ ] **Step 6: Manual play-mode check**

`Example.unity`, Play mode. Move the cursor across slots in every panel — Inventory, Stash, Store, the 14 equipment slots, the sell slot. Each shows the outline on hover and loses it on exit. Confirmed with and without an item in the slot.

- [ ] **Step 7: Commit the prefab wiring**

```bash
git add Assets/Prefabs/SlotDisplay.prefab Assets/Prefabs/SlotDisplayVendor.prefab Assets/Prefabs/EquipmentISlotDisplay.prefab
git commit -m "$(cat <<'EOF'
feat: wire the hover outline on the slot prefabs

HoverOutline child Image (hidden by default) on SlotDisplay.prefab - which
covers Inventory, Stash and, via the SlotDisplayVendor variant, the Store -
and on EquipmentISlotDisplay.prefab for the equipment slots. Field wired on
each slot component.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

If Unity re-serialized `Example.unity` when it opened, review and commit that separately.

---

## Task 5: Always-on unaffordable indicator on vendor slots

**Files:**
- Modify: `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs`
- Modify: `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/VendorSlotDisplay.cs`

**Interfaces:**
- Consumes: `CharacterInventory.CanAfford(float)` (Task 1); `VendorSlotDisplay.BuyPrice(AbstractItem)` (Task 2); `AbstractDimensionalContainer.OnContentChanged` (event, `Action<Dictionary<Vector2Int, Package>>`); `InventoryProvider.Instance.Inventory`.
- Produces: `VendorSlotDisplay` reads/writes only its own `slotBackground` and `icon` (both `protected` on the base); no new public surface.

**Base changes needed:** `AbstractSlotDisplay.RefreshSlotDisplay` must become `virtual`, and `OnEnable` / `OnDisable` (currently `private`) must become `protected virtual` so `VendorSlotDisplay` can extend the lifecycle without hiding the base's `DragProvider` subscription. This is a small extension of the spec's Component 6 wording, forced by the `private` lifecycle methods.

- [ ] **Step 1: Make the base members overridable**

In `AbstractSlotDisplay.cs`:
- `private void OnEnable()` → `protected virtual void OnEnable()`
- `private void OnDisable() => ...` → `protected virtual void OnDisable() => ...`
- `public void RefreshSlotDisplay(Package package)` → `public virtual void RefreshSlotDisplay(Package package)`

No behaviour change. Existing callers (`AbstractContainerDisplay.Refresh`, `EquipmentContainerDisplay.Refresh`, `EquipmentSlotDisplay.Refresh2HandSlotDisplay`) all dispatch through `AbstractSlotDisplay` references, so virtual dispatch is transparent.

- [ ] **Step 2: Recompile → green**

No subclass declares its own `OnEnable` / `OnDisable` (`VendorSlotDisplay`, `InventorySlotDisplay`, `EquipmentSlotDisplay`, `SellItenSlotDisplay` have no lifecycle methods), so nothing collides with the new `virtual`.

- [ ] **Step 3: Override in `VendorSlotDisplay`**

In `VendorSlotDisplay.cs`:

Add `using System.Collections.Generic;` at the top (needed for the event handler signature).

Add fields and overrides to the class:

```csharp
private static readonly Color UnaffordableSlotTint = new(0.4f, 0f, 0f, 0.4f);
private static readonly Color UnaffordableIconTint = new(1f, 1f, 1f, 0.5f);

private Package displayedPackage;

protected override void OnEnable()
{
    base.OnEnable();

    var wallet = InventoryProvider.Instance.Inventory;
    if (wallet != null)
    {
        wallet.OnContentChanged -= OnWalletChanged;
        wallet.OnContentChanged += OnWalletChanged;
    }
}

protected override void OnDisable()
{
    base.OnDisable();

    var wallet = InventoryProvider.Instance.Inventory;
    if (wallet != null)
        wallet.OnContentChanged -= OnWalletChanged;
}

public override void RefreshSlotDisplay(Package package)
{
    base.RefreshSlotDisplay(package);

    displayedPackage = package;
    ApplyAffordability();
}

private void OnWalletChanged(Dictionary<Vector2Int, Package> _) => ApplyAffordability();

private void ApplyAffordability()
{
    var affordable = !displayedPackage.IsValid
        || InventoryProvider.Instance.Inventory.CanAfford(BuyPrice(displayedPackage.Item));

    if (slotBackground)
        slotBackground.color = affordable ? Color.white : UnaffordableSlotTint;

    if (icon)
        icon.color = affordable ? Color.white : UnaffordableIconTint;
}
```

Notes:
- `displayedPackage` is cached so the wallet-changed event can re-tint without a container refresh.
- Empty slot (item just bought) → `!displayedPackage.IsValid` → treated as "affordable" → `slotBackground` restored to white, `icon` already hidden by `base.RefreshSlotDisplay`. State cleared.
- `RestockStore()` fires the Store's `OnContentChanged` → `AbstractContainerDisplay.Refresh` → `RefreshSlotDisplay` on every slot → re-evaluated. Buying fires the Inventory's `OnContentChanged` → `OnWalletChanged` → re-evaluated.
- Minor known interaction: `AbstractSlotDisplay.SetBackgroundColor` (drag-overlap highlight) also writes `slotBackground`, and preserves its alpha. Dragging *onto* the Store is not a supported action, so this does not collide in practice; if a designer later enables it, reconcile there.

- [ ] **Step 4: Recompile → green**

- [ ] **Step 5: Manual play-mode check**

`Example.unity`, Play mode.
- Start with little/no money → most or all Store slots are dark-red tinted with greyed icons.
- Add Gold via the debug buttons → affordable slots clear their tint immediately (no shop reopen).
- Buy an affordable item → its slot goes empty and untinted; if the purchase drops you below other items' prices, those slots tint on the same frame.
- Click "Restock Store" → tints re-evaluate against the new stock and current wallet.
- Hover an unaffordable slot → it shows **both** the red background and the hover outline (Task 4) at once.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs Assets/Scripts/InventorySystem/GUI/InventoryDisplays/VendorSlotDisplay.cs
git commit -m "$(cat <<'EOF'
feat: always-on unaffordable indicator on vendor slots

AbstractSlotDisplay.RefreshSlotDisplay becomes virtual; OnEnable / OnDisable
become protected virtual so VendorSlotDisplay can extend the lifecycle
without hiding the base DragProvider subscription.

VendorSlotDisplay caches its package, and when the player cannot afford
BuyPrice it tints slotBackground dark red and greys the icon; otherwise it
restores both. Re-applied on the inventory's OnContentChanged (wallet change)
and on RefreshSlotDisplay (restock), mirroring the DragProvider.OnOverlapping
lifecycle the base already uses.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Final verification

- [ ] Whole project **green**: `CurrencyTests` all passing, `MutableFloatTests` unchanged.
- [ ] `git status` clean apart from intended files; any Unity 6000.3.9f1 re-serialization of `Example.unity` / prefabs committed separately with a descriptive message.
- [ ] Full manual pass in `Example.unity` per the spec's Testing section: buy from the Store via drag, right-click, and shift-click; watch coin packages and vendor-slot dimming; hover slots in every panel; sell an item back and confirm the `1 ×` payout is unchanged.
- [ ] `git log --oneline feature/mutablefloat-port..HEAD` reads as a clean sequence of the commits above.

Spec → task map (each spec component maps to one task; the deferred `feature/container-labels` merge conflict is for whoever merges, not this plan): Component 1 + 2 → Task 1; Component 3 → Task 2; Component 4 → Task 3; Component 5 → Task 4; Component 6 + "how the indicators stack" → Task 5.
