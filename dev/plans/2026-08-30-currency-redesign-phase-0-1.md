# Currency Redesign — Phase 0 & 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task, with a review checkpoint after each task. Steps use checkbox (`- [ ]`) syntax for tracking. Do **not** dispatch subagents for this work.

**Goal:** Coins stop being carry digits. The ladder becomes iron → copper → silver → gold at 5 / 12 / 20, stack limits are decoupled from the ratios, and full stacks no longer auto-upgrade — the player consolidates on purpose.

**Architecture:** `Currency` (in the `InventorySystem.Data` assembly, the only piece of this that EditMode tests can reach) gets the new ladder with **iron as the base unit**, so every value in the struct is counted in iron. Everything else — `CurrencyItem`'s stack limits, the removal of the auto-upgrade, the new `Consolidate()` — lives in `Assembly-CSharp` and is verified by compile + in-editor check, not by unit test. Phase 0's three bug fixes are independent of the redesign and land first so the branch starts from a correct baseline.

**Tech Stack:** Unity 6000.3.9f1, C# (Roslyn), Unity Test Framework (NUnit) EditMode tests, `.asmdef` assembly definitions, unity-mcp bridge for compile verification.

## Status — 2026-08-31 (Sonnet 5, this session)

All nine tasks are **coded and committed** on `feature/currency-redesign` (727c742 → 7a2894d, on top of d0b8a5e). Every commit was gated on a clean batch-mode run
(`Unity.exe -runTests -batchmode`, Editor closed, no bridge): **0 `error CS`, 37/37 EditMode tests pass**, including all 10 `CurrencyTests`. The batch pipeline was
negative-control checked (a `DELIBERATE_SENTINEL_ERROR` produced `CS1002` and no results file).

**Deviation:** Tasks 3 + 4 + 5 are **one commit** (`ae86936`). Task 3 removes `Currency.copperToIron`/`copperToGold`, which `CurrencyItem.CalculateValue` (Task 5's file)
references, and Task 5 needs Task 3's new constants — so `Assembly-CSharp` only compiles with all three applied. Splitting them would put a red commit between them.

**Commits also use `Co-Authored-By: Claude Sonnet 5`** (this session's model), not Opus 5.

**Still open — needs the Unity Editor / Play mode (see the DEFERRED step notes and the checklist at the bottom):** Task 1 S4, Task 2 S4 (asset cache rebake),
Task 5 S6, Task 6 S5, Task 7 S7 (scene button wiring) + S8, Task 8 S3, Task 9 S6 (serialized-item survival).

## Global Constraints

- **Base branch:** `feature/currency-redesign`, currently `2ce31ed`, sitting directly on `6653484` — the pushed tip of `feature/mutablefloat-port`. That base already contains every Currency commit this plan builds on (`03fc86f` moved Currency into the Data assembly, `5409607` added its tests, `9341fcf` rewrote the decomposition constructor, `1a39662` added the 1.5× vendor markup). Do **not** rebase onto `main` — it is 28 commits behind and has none of it.
- **Sibling branch:** `fix/drag-cursor-anchoring` is unrelated in-flight work off the same `6653484`, and usually has uncommitted edits to `DragProvider.cs`, `AbstractSlotDisplay.cs`, `InventorySlotDisplay.cs`, `EquipmentSlotDisplay.cs` and `VendorSlotDisplay.cs` in the working tree. Never commit those here. Check `git status` before every commit and stage **only** the files the task names.
- **Gold's value does not change.** `5 × 12 × 20 == 20 × 12 × 5 == 1200`. No item price, affix `goldRatio`, or vendor markup is retuned anywhere in this plan. If a change makes gold worth something other than 1200 base units, it is wrong.
- **Do not reorder the `CurrencyType` enum.** `NONE = 0, Copper = 1, Iron = 2, Silver = 3, Gold = 4` stays exactly as it is. A previous reorder is what caused two of the Phase 0 bugs. Value order and enum order are allowed to disagree.
- **Do not hand-edit `probabilities` or `exampleResults`** in any `*Distribution.asset`. Both are `[ReadOnly]` caches that `AbstractProbabilityDistribution.OnValidate` regenerates from `quantities`. Edit `Quantity` only.
- **Commits:** small and frequent, one per task. Repo message style: `feat:` / `fix:` / `refactor:` / `chore:` / `docs:` / `perf:` / `test:` prefix, body explains *why*. End every commit message with:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  ```

## Green

**Green** = the project compiles with zero console errors *and* `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` passes (`InventorySystem.Data.Tests`, `InventorySystem.Geometry.Tests`). A compile error anywhere blocks the Test Runner entirely, so every commit point below lands on green.

**Compile check** (Editor open) — via the unity-mcp bridge, `Unity_RunCommand`:
```csharp
AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
global::UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
```
Fully qualify `CompilationPipeline` or it resolves to `Unity.CompilationPipeline` and fails to build. Then read errors with **`Unity_GetConsoleLogs`** — `Unity_ReadConsole` has returned 0 entries for real compile errors here. Don't force a recompile after every edit; Unity notices changed files on focus, and each forced compile costs a domain reload that drops the bridge for 20–40s.

**Test run.** The bridge cannot run tests (`TestRunnerApi` is refused with "User interactions are not supported for MCP tool calls"). While the Editor is open, ask the user to run EditMode tests from **Window ▸ General ▸ Test Runner**. Do not propose closing their Editor. Once it *is* closed, batch mode works and compiles as well as runs:

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" -runTests -batchmode -projectPath "C:/Users/loles/Desktop/LEONID/InventoryTetris" -testPlatform EditMode -testResults "C:/Users/loles/AppData/Local/Temp/claude/results.xml" -logFile -
```

Exit `0` = all passed, `2` = failures. Don't trust the exit code alone — read `<test-run ... passed= failed=>` from the XML and `grep -c "error CS"` the log.

**`dotnet build` is useless here.** The generated `.csproj` files are stale — they omit newer scripts and reference package versions no longer in `Library/PackageCache`, so it reports hundreds of phantom `CS2001`s while silently not compiling what you changed.

## What is and is not unit-testable

`InventorySystem.Data.asmdef` covers **only** `Assets/Scripts/InventorySystem/Data/Statistics/` — `Currency.cs`, `MutableFloat.cs`, `StatModifier.cs`, `StatModifierType.cs`. `InventorySystem.Data.Tests` references it.

Everything else this plan touches — `AbstractItem.cs`, `Package.cs`, `CharacterInventory.cs`, `AbstractDimensionalContainer.cs`, `CurrencyDisplay.cs`, `InventoryProvider.cs` — lives in the predefined `Assembly-CSharp`. **Unity forbids an `.asmdef` assembly from referencing a predefined assembly**, so no EditMode test project can reach them without first moving them into an asmdef. That refactor is out of scope.

Consequence: **Tasks 3 and 4 get real red-green TDD. Tasks 1, 2, 5, 6, 7, 8 and 9 are verified by compile + a scripted in-editor check.** Where a task has no test, it says so and gives the manual check instead. Do not invent a test assembly to paper over this.

## File Structure

| File | Responsibility | Tasks |
|---|---|---|
| `Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs` | Debug harness buttons | 1, 7 |
| `Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset` | Currency drop weights | 2 |
| `Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs` | The ladder, decomposition, payment | 3, 4 |
| `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs` | Locks in the ladder and payment | 3, 4 |
| `Assets/Scripts/InventorySystem/Data/Items/AbstractItem.cs` | `CurrencyItem` values + stack limits | 5, 9 |
| `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs` | Wallet, payment, consolidation | 6, 7, 9 |
| `Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs` | `AutoConsolidate` seam | 7, 9 |
| `Assets/Scripts/InventorySystem/GUI/Components/Displays/CurrencyDisplay.cs` | Coin readout order | 8 |
| `Assets/Scripts/InventorySystem/Data/Structs/Package.cs` | Stack arithmetic | 9 |
| `Assets/Scripts/InventorySystem/GUI/Components/Displays/PreviewDisplay.cs` | `n/limit` label | 9 |
| `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterEquipment.cs` | Rejects stackables | 9 |
| `Assets/Scripts/InventorySystem/Data/Enums/ItemStack.cs` | Deleted | 9 |

---

# Phase 0 — Bugs

These three are independent of the redesign and of each other. They make the *current* game correct, so they land first and could ship on their own.

---

### Task 1: Un-swap the currency debug buttons

`SetItemToIron()` adds Copper and `SetItemToCopper()` adds Iron. Left over from an old enum order. Nothing depends on the broken behaviour, so this is a straight swap.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs:139-140`
- Test: none possible — `InventoryProvider` is a `MonoBehaviour` in `Assembly-CSharp`. Verified in-editor.

**Interfaces:**
- Consumes: nothing.
- Produces: nothing. Purely a fix.

- [x] **Step 1: Read the current lines to confirm the bug is still present**

Run:
```bash
sed -n '139,142p' Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs
```
Expected output — note `Iron` calling `Copper` and vice versa:
```csharp
        public void SetItemToIron() => AddCurrency(CurrencyType.Copper);
        public void SetItemToCopper() => AddCurrency(CurrencyType.Iron);
        public void SetItemToSilver() => AddCurrency(CurrencyType.Silver);
        public void SetItemToGold() => AddCurrency(CurrencyType.Gold);
```

- [x] **Step 2: Swap the two currency types**

Replace those first two lines with:

```csharp
        public void SetItemToIron() => AddCurrency(CurrencyType.Iron);
        public void SetItemToCopper() => AddCurrency(CurrencyType.Copper);
```

- [x] **Step 3: Compile-check via the bridge** — done via batch-mode `Unity.exe -runTests` (Editor closed, no bridge); 0 `error CS`, 34/34 EditMode tests pass.

`Unity_RunCommand` with the recompile snippet from **Green**, then `Unity_GetConsoleLogs`.
Expected: no `error CS` entries.

- [ ] **Step 4: Verify in the editor** — DEFERRED (needs Play mode; on the user checklist)

Enter Play mode, press the debug **Iron** button, and confirm grey iron coins land in the inventory (not orange copper). Then the **Copper** button, and confirm orange copper.

- [x] **Step 5: Commit** — 727c742

```bash
git add Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs
git commit -m "fix: un-swap the iron and copper debug buttons

SetItemToIron() added Copper and SetItemToCopper() added Iron, left over
from an old CurrencyType enum order.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Restore the intended currency drop weights

`AbstractProbabilityDistribution.OnValidate` re-stamps each entry's `name` from `Enum.GetValues(...)[i]` while leaving its `Quantity` at index `i`. The asset was authored when the enum read `Iron = 1, Copper = 2`, so the weights `8` and `4` were attached to Iron and Copper respectively. After the enum was reordered to `Copper = 1, Iron = 2`, opening the asset relabelled the entries **without moving the weights** — so Copper silently inherited Iron's `8` and Iron got Copper's `4`.

That relabel has already happened in the working tree. The original authored intent survives only in the committed blob `755df87`.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset`
- Test: none possible — a `ScriptableObject` asset. Verified by reading the file back.

**Interfaces:**
- Consumes: nothing.
- Produces: nothing. Phase 2 rewrites these weights entirely (Iron 40 / Copper 40 / Silver 8 / Gold 1); this restores correctness in the meantime.

- [x] **Step 1: Confirm the original intent from git** — blob `755df87` had Iron/Enum 1/Qty 8.

Run:
```bash
git show 755df87:"Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset" | sed -n '15,30p'
```
Expected — `name: Iron` on `Enumeration: 1` with `Quantity: 8`, proving Iron was meant to be the common one:
```yaml
  - name: Iron
    Enumeration: 1
    Quantity: 8
  - name: Copper
    Enumeration: 2
    Quantity: 4
```

- [x] **Step 2: Confirm the working tree now has the labels corrected but the weights unmoved** — Copper/Enum 1 held Qty 8.

Run:
```bash
sed -n '15,24p' "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset"
```
Expected — `Copper` (Enumeration 1) wrongly holding `8`:
```yaml
  - name: Copper
    Enumeration: 1
    Quantity: 8
  - name: Iron
    Enumeration: 2
    Quantity: 4
```

- [x] **Step 3: Swap the two Quantity values** — Copper→4, Iron→8.

Edit the file so `Copper` (Enumeration 1) holds `4` and `Iron` (Enumeration 2) holds `8`. Leave `name` and `Enumeration` exactly as they are — they are correct now.

```yaml
  - name: Copper
    Enumeration: 1
    Quantity: 4
  - name: Iron
    Enumeration: 2
    Quantity: 8
```

Iron is the cheapest coin and should be the one you see most often, which is also what the new ladder wants — so this fix points the same way as the redesign.

- [ ] **Step 4: Let Unity regenerate the derived caches** — DEFERRED (needs Editor focus on the asset; on the user checklist). Runtime is unaffected — `Probabilities` recomputes from `quantities` every access.

Focus the Unity Editor and select the asset in the Project window so `OnValidate` runs. Do **not** hand-edit `probabilities` or `exampleResults`.

Run:
```bash
git diff --stat "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset"
```
Expected: the file is modified. Then confirm the probabilities now favour Iron:
```bash
grep -A2 "name: Iron" "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset" | grep Probability
```
Expected: `Probability: 0.53333336`.

- [x] **Step 5: Commit** — d987d44

```bash
git add "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset"
git commit -m "fix: restore the intended currency drop weights

The weights were authored when the enum read Iron=1, Copper=2. Reordering
it to Copper=1, Iron=2 left OnValidate to relabel each entry in place,
handing Copper the 8 that belonged to Iron. Iron is the common coin.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

# Phase 1 — The ladder

---

### Task 3: Re-base `Currency` on iron at 5 / 12 / 20

The struct's unit of account becomes **iron**, not copper. This is the task that changes what every number in the system means, so it is fully test-driven.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs:14-45`
- Test: `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `Currency.ironToCopper` (`uint`, 5), `Currency.ironToSilver` (`uint`, 60), `Currency.ironToGold` (`uint`, 1200)
  - `Currency.copperToSilver` (`uint`, 12), `Currency.silverToGold` (`uint`, 20)
  - `Currency.Total` (`uint`) — value in iron
  - `Currency(uint total)` — decomposes a total in iron
  - Task 4 changes the positional constructor; Task 5 consumes the three `ironTo*` constants.

- [x] **Step 1: Write the failing tests**

Add these three tests to `CurrencyTests.cs`, inside the `CurrencyTests` class:

```csharp
        [Test]
        public void Ladder_IsIronBased_AtFiveTwelveTwenty()
        {
            Assert.That(Currency.ironToCopper, Is.EqualTo(5u), "iron -> copper");
            Assert.That(Currency.copperToSilver, Is.EqualTo(12u), "copper -> silver");
            Assert.That(Currency.silverToGold, Is.EqualTo(20u), "silver -> gold");
            Assert.That(Currency.ironToSilver, Is.EqualTo(60u), "iron -> silver");
            Assert.That(Currency.ironToGold, Is.EqualTo(1200u), "gold keeps its 1200");
        }

        [Test]
        public void Decompose_MaxSubGoldTotal_FillsEveryLowerDenomination()
        {
            var wallet = new Currency(1199u);

            Assert.That(wallet.Gold, Is.EqualTo(0u), "gold");
            Assert.That(wallet.Silver, Is.EqualTo(19u), "silver");
            Assert.That(wallet.Copper, Is.EqualTo(11u), "copper");
            Assert.That(wallet.Iron, Is.EqualTo(4u), "iron");
        }

        [Test]
        public void Decompose_RoundTripsThroughTotal()
        {
            for (var total = 0u; total < 2500u; total++)
                Assert.That(new Currency(total).Total, Is.EqualTo(total), $"round trip at {total}");
        }
```

- [x] **Step 2: Run the tests to verify they fail** — the combined 3+4+5 batch run first surfaced this as 6 `error CS0117` (removed constants); that was the red.

Ask the user to run **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**, or use the batch command from **Green** if the Editor is closed.
Expected: `Ladder_IsIronBased_AtFiveTwelveTwenty` fails to compile (`ironToCopper` does not exist). A compile error blocks the whole run — that *is* the red.

- [x] **Step 3: Replace the ladder constants**

In `Currency.cs`, replace the five `static readonly` lines with:

```csharp
        /// Denomination ladder: iron -> copper -> silver -> gold at 5 / 12 / 20.
        /// Iron is the base unit - the cheapest metal, and the one almost never
        /// coined, because it is heavy, brittle when cast, and rusts. Mirrors
        /// pound-shilling-pence: 12 pence = 1 shilling, 20 shillings = 1 pound.
        /// The ratios multiply to the same 1200 as the old 20/12/5 ladder, so gold
        /// keeps its value and no item price needs retuning.
        public static readonly uint ironToCopper = 5u;
        public static readonly uint ironToSilver = 60u;
        public static readonly uint ironToGold = 1200u;
        public static readonly uint copperToSilver = ironToSilver / ironToCopper; // = 12
        public static readonly uint silverToGold = ironToGold / ironToSilver;     // = 20
```

- [x] **Step 4: Reorder the fields to ascending value and update `Total`**

Replace the four field declarations and `Total` with:

```csharp
        [field: SerializeField] public uint Iron { get; private set; }
        [field: SerializeField] public uint Copper { get; private set; }
        [field: SerializeField] public uint Silver { get; private set; }
        [field: SerializeField] public uint Gold { get; private set; }

        public readonly uint Total => Iron + Copper * ironToCopper + Silver * ironToSilver + Gold * ironToGold;
```

- [x] **Step 5: Update the decomposition constructor**

Replace the body of `Currency(uint total)` with:

```csharp
        public Currency( uint total )
        {
            // Carry the remainder down instead of re-deriving it at each denomination:
            // 3 divisions + 3 modulos instead of 3 + 6, and each div/mod pair on the
            // same operands is one hardware division.
            Gold = total / ironToGold;

            var rest = total % ironToGold;
            Silver = rest / ironToSilver;

            rest %= ironToSilver;
            Copper = rest / ironToCopper;
            Iron = rest % ironToCopper;
        }
```

- [x] **Step 6: Update `ToString()` to the new order**

```csharp
        public readonly override string ToString() => $"{Gold}G, {Silver}S, {Copper}C, {Iron}I ({Total})";
```

- [x] **Step 7: Run the tests to verify the three new ones pass**

Expected: `Ladder_IsIronBased_AtFiveTwelveTwenty`, `Decompose_MaxSubGoldTotal_FillsEveryLowerDenomination` and `Decompose_RoundTripsThroughTotal` all PASS.

The pre-existing `TryGetPayment` tests will **still fail** at this point — they assert against the old 1/20/240/1200 values and still call the old positional constructor argument order. Task 4 fixes them. Do not commit yet.

- [x] **Step 8: Hold the commit until Task 4**

Tasks 3 and 4 share a red bar. Commit once, at the end of Task 4.

---

### Task 4: Reorder the positional constructor and re-anchor the payment tests

`Currency(uint, uint, uint, uint)` currently reads `(copper, iron, silver, gold)`. With iron at the bottom it must read `(iron, copper, silver, gold)` to stay in ascending value order.

⚠️ **The old and new signatures are identical to the compiler — four `uint`s.** Nothing will error if a call site is missed; it will silently swap two denominations. There are exactly three call sites and they are all listed below. Check each by hand.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs:39-45` and `:52-84`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs:227`
- Test: `Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs`

**Interfaces:**
- Consumes: `Currency.ironToCopper`, `Currency.ironToSilver`, `Currency.ironToGold` from Task 3.
- Produces: `Currency(uint iron, uint copper, uint silver, uint gold)`. Task 7 consumes `TryGetPayment` unchanged in signature.

- [x] **Step 1: Update the test helpers and every payment expectation**

Replace the `Coins` helper, the `AssertCoins` helper and all seven pre-existing tests in `CurrencyTests.cs` with the following. Denominations are now **iron 1 / copper 5 / silver 60 / gold 1200**, so a price of 3 silver is 180 iron, not 720 copper.

```csharp
        // Currency(iron, copper, silver, gold). Denomination values: 1 / 5 / 60 / 1200.
        private static Currency Coins(uint iron = 0, uint copper = 0, uint silver = 0, uint gold = 0) =>
            new(iron, copper, silver, gold);

        private static void AssertCoins(Currency actual, uint iron, uint copper, uint silver, uint gold)
        {
            Assert.That(actual.Iron, Is.EqualTo(iron), "iron");
            Assert.That(actual.Copper, Is.EqualTo(copper), "copper");
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
            AssertCoins(change, 0, 0, 17, 0); // 1200 - 180 = 1020 = 17 silver
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
            AssertCoins(toRemove, 0, 0, 2, 1); // 120 + 1200 = 1320 paid
            AssertCoins(change, 0, 0, 19, 0);  // 1320 - 180 = 1140 = 19 silver
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
            var ok = Coins(iron: 5).TryGetPayment(new Currency(0u), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            Assert.That(toRemove.Total, Is.EqualTo(0u));
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void NonCanonicalWallet_LooseIronCoversASilverPrice()
        {
            // 150 loose iron (in practice two packages: a full 120-stack + 30)
            var ok = Coins(iron: 150).TryGetPayment(Coins(silver: 1), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 60, 0, 0, 0); // price total is 60
            Assert.That(change.Total, Is.EqualTo(0u));
        }
```

Also update the class doc comment above `[TestFixture]` to say `iron` where it says `copper`.

- [x] **Step 2: Run the tests to verify they fail** — see Task 3 Step 2 note (combined red).

Expected: compile error — `Coins(iron: ...)` has no matching parameter, because the constructor still reads `(copper, iron, silver, gold)`.

- [x] **Step 3: Reorder the positional constructor**

In `Currency.cs`, replace it with:

```csharp
        public Currency(uint iron, uint copper, uint silver, uint gold)
        {
            Iron = iron;
            Copper = copper;
            Silver = silver;
            Gold = gold;
        }
```

- [x] **Step 4: Update `TryGetPayment` to take iron first**

Inside `TryGetPayment`, replace the four `Take` calls and the `toRemove` assignment with:

```csharp
            var iron = Take(Iron, 1u);
            var copper = Take(Copper, ironToCopper);
            var silver = Take(Silver, ironToSilver);
            var gold = Take(Gold, ironToGold);

            toRemove = new Currency(iron, copper, silver, gold);
```

Leave the rest of the method — including `change = new Currency(paid - owed);` and the `Take` local function — exactly as it is. Smallest-first ordering is already correct and this preserves it.

- [x] **Step 5: Fix the second call site by hand**

`CharacterInventory.CalculateCash()` ends with `return new Currency( copper, iron, silver, gold );`. Its locals are named after the denominations, so the fix is to swap the first two arguments:

Run:
```bash
sed -n '225,229p' Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs
```
Expected: `            return new Currency( copper, iron, silver, gold );`

Replace with:

```csharp
            return new Currency( iron, copper, silver, gold );
```

- [x] **Step 6: Confirm there is no third call site**

Run:
```bash
grep -rn "new Currency(" --include=*.cs Assets/Scripts | grep -v "new Currency( *total\|new Currency(0u\|new Currency(buyValue\|new Currency(priceOverride\|new Currency(package\|new Currency(paid"
```
Expected: only the two four-argument sites you just edited (`Currency.cs` inside `TryGetPayment`, `CharacterInventory.cs:227`) plus the `Coins` helper in the test file. Any other four-argument call must be fixed the same way.

- [x] **Step 7: Run the tests to verify they pass**

Expected: all ten tests in `CurrencyTests` PASS, and `InventorySystem.Geometry.Tests` still passes.

- [x] **Step 8: Commit**

```bash
git add Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs Assets/Scripts/Tests/EditMode/Statistics/CurrencyTests.cs Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs
git commit -m "feat: re-base Currency on iron at 5/12/20

Iron becomes the base unit: it is the cheapest metal and was almost never
coined, so an iron coin worth more than a copper one was backwards. The
ladder now mirrors pound-shilling-pence (12 pence = 1 shilling, 20
shillings = 1 pound).

Ratios multiply, so 5x12x20 == the old 20x12x5 == 1200. Gold keeps its
value and no item price is retuned.

The positional constructor moves to (iron, copper, silver, gold) to stay in
ascending value order. Both remaining call sites were checked by hand - the
signature is four uints either way, so the compiler cannot catch a miss.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Give `CurrencyItem` the new values and stack limits

Stack limits stop being the conversion ratio. They become an independent inventory-pressure dial: `iron 120, copper 60, silver 20, gold 12`.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Items/AbstractItem.cs:23`, `:74`, `:376-397`
- Test: none possible — `AbstractItem` is in `Assembly-CSharp`. Verified by compile + in-editor check.

**Interfaces:**
- Consumes: `Currency.ironToCopper`, `Currency.ironToSilver`, `Currency.ironToGold` from Task 3.
- Produces: `AbstractItem.CalculateValue()` (renamed from `CalculateGoldValue()`), `protected virtual float`. `SellValue` is unchanged in name and type.

- [x] **Step 1: Rename `CalculateGoldValue` to `CalculateValue`**

It has always returned a total in the base denomination, never gold. Three sites, all in `AbstractItem.cs`:

Line 23:
```csharp
        public float SellValue => CalculateValue();
```

Line 74:
```csharp
        protected virtual float CalculateValue()
```

Line 391 (the `CurrencyItem` override) — replaced wholesale in Step 3.

- [x] **Step 2: Set the new stack limits**

In `CurrencyItem`'s constructor, replace the `StackLimit` switch with:

```csharp
            // Deliberately NOT the conversion ratio - coins no longer auto-upgrade at a
            // full stack, so this is purely how much of each coin fits in one cell.
            // A full stack is a clean conversion: 120 iron -> 24 copper, 60 copper ->
            // 5 silver, 20 silver -> exactly 1 gold.
            StackLimit = CurrencyType switch
            {
                CurrencyType.Iron => (ItemStack)120u,
                CurrencyType.Copper => (ItemStack)60u,
                CurrencyType.Silver => (ItemStack)20u,
                CurrencyType.Gold => (ItemStack)12u,

                CurrencyType.NONE => ItemStack.NONE,
                _ => ItemStack.NONE,
            };
```

The `(ItemStack)` casts stay for now; Task 9 removes the enum entirely.

- [x] **Step 3: Set the new denomination values**

Replace the `CurrencyItem` override with:

```csharp
        protected override float CalculateValue() => CurrencyType switch
        {
            CurrencyType.Iron => 1f,
            CurrencyType.Copper => Currency.ironToCopper,
            CurrencyType.Silver => Currency.ironToSilver,
            CurrencyType.Gold => Currency.ironToGold,

            CurrencyType.NONE => 0f,
            _ => 0f,
        };
```

- [x] **Step 4: Confirm no `CalculateGoldValue` references remain**

Run:
```bash
grep -rn "CalculateGoldValue" --include=*.cs Assets/Scripts
```
Expected: no output.

- [x] **Step 5: Compile-check via the bridge**

Expected: no `error CS` entries.

- [ ] **Step 6: Verify in the editor** — DEFERRED (needs Play mode; on the user checklist)

Enter Play mode. Add 1 gold and 1 iron with the debug buttons. The currency readout total must show gold as **1200** and iron as **1**. Add 21 silver: it must occupy **two** cells (20 + 1), not one, and must **not** turn into gold — the auto-upgrade is removed in Task 6, so at this point 20 silver in a full stack simply stays 20 silver.

- [x] **Step 7: Commit**

```bash
git add Assets/Scripts/InventorySystem/Data/Items/AbstractItem.cs
git commit -m "feat: new coin values and stack limits, decoupled from the ratios

Values follow the iron-based ladder (1/5/60/1200). Stack limits become an
independent inventory-pressure dial (120/60/20/12) instead of echoing the
conversion ratio, which is what capped every non-gold holding below the
value of a single gold coin.

Renames CalculateGoldValue to CalculateValue - it has always returned a
total in the base denomination, never gold.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Remove the auto-upgrade

This is the change that lifts the 1199 ceiling. A full stack becomes just a full stack.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs:66-103`
- Test: none possible. Verified by compile + in-editor check.

**Interfaces:**
- Consumes: nothing.
- Produces: nothing. Removal only. Task 7 adds the deliberate replacement.

- [x] **Step 1: Read the current block**

Run:
```bash
sed -n '60,105p' Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs
```
Expected: a `TryStack` local function containing an `if (storedPackage.Item is CurrencyItem currencyItem)` block and a `CheckForCurrencyUpgrade` local function with a nested `UpgradeCurrency` switch.

- [x] **Step 2: Delete the upgrade block and both local functions**

Inside `AddAtPosition`'s `TryStack` local function, delete the three-deep `if` that tests for a full currency stack, and delete the entire `CheckForCurrencyUpgrade` local function (including its nested `UpgradeCurrency`). What remains must read exactly:

```csharp
            bool TryStack(Package storedPackage, Vector2Int storedPosition)
            {
                if (0 == storedPackage.SpaceLeft)
                    return false;

                if (!package.Item.Equals(storedPackage.Item))
                    return false;

                var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                _ = package.ReduceAmount(addedAmount);

                StoredPackages[storedPosition] = storedPackage;

                return true;
            }
```

- [x] **Step 3: Confirm the upgrade path is gone**

Run:
```bash
grep -rn "CheckForCurrencyUpgrade\|UpgradeCurrency" --include=*.cs Assets/Scripts
```
Expected: no output.

- [x] **Step 4: Compile-check via the bridge**

Expected: no `error CS` entries. In particular no "unused variable `currencyItem`" — that declaration goes with the block.

- [ ] **Step 5: Verify in the editor** — DEFERRED (needs Play mode; on the user checklist)

Enter Play mode and press the debug **Iron** button until you have more than 120 iron. Expected: a full 120 stack plus a second cell holding the overflow. **No** copper appears. Repeat with silver past 20 — no gold appears.

- [x] **Step 6: Commit**

```bash
git add Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs
git commit -m "feat: stop auto-upgrading full coin stacks

StackLimit used to equal each coin's conversion ratio and a full stack
collapsed into one coin of the next tier, which made the lower coins carry
digits rather than currency: the most you could ever hold below gold was
1199 of gold's 1200. Coins now accumulate; consolidation becomes a
deliberate act (Task 7).

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Add `Consolidate()`, the `AutoConsolidate` seam, and the button

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs` (add `AutoConsolidate` near `Capacity`, around line 17)
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs` (add `Consolidate()` after `CalculateCash`; hook `TryAddToContainer`; update `AddChange`'s doc comment)
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs` (add the button method beside `SortPlayerInventory`)
- Test: none possible. Verified by compile + in-editor check.

**Interfaces:**
- Consumes: `Currency(uint total)` from Task 3; the existing private `CalculateCash()`, `RemoveCurrency(CurrencyType, uint)` and `AddChange(Currency)` on `CharacterInventory`; `InvokeRefresh()` (`protected internal` on the base, line 207).
- Produces:
  - `AbstractDimensionalContainer.AutoConsolidate` — `public virtual bool`, `false`
  - `CharacterInventory.Consolidate()` — `public void`
  - `InventoryProvider.ConsolidatePlayerCurrency()` — `public void`, for the UI button

- [x] **Step 1: Add the seam to the base container**

In `AbstractDimensionalContainer`, directly under `public int Capacity => Dimensions.x * Dimensions.y;`:

```csharp
        /// <summary>
        /// Containers that fold small coins into larger denominations the moment they
        /// arrive, rather than leaving that to the player. False everywhere today; a
        /// future stash subclasses <see cref="CharacterInventory"/> and overrides this
        /// to true, which is the whole "everything becomes gold in the bank" behaviour.
        /// </summary>
        public virtual bool AutoConsolidate => false;
```

- [x] **Step 2: Add `Consolidate()` to `CharacterInventory`**

Insert directly after the closing brace of `CalculateCash()`, before the final `}` of the class. Note the re-entrancy guard — without it `AddChange` calls `TryAddToContainer`, which would call `Consolidate` again, forever:

```csharp
        private bool isConsolidating;

        /// <summary>
        /// Folds every stored coin into the largest denominations that fit, leaving the
        /// remainder as loose change. Value-preserving: Total before == Total after.
        /// Re-entrancy is guarded because <see cref="AddChange"/> re-enters
        /// <see cref="TryAddToContainer"/>, which is where <see cref="AutoConsolidate"/>
        /// is honoured.
        /// </summary>
        public void Consolidate()
        {
            if (isConsolidating)
                return;

            var wallet = CalculateCash();

            if (0u == wallet.Total)
                return;

            var consolidated = new Currency(wallet.Total);

            if (consolidated.Iron == wallet.Iron
                && consolidated.Copper == wallet.Copper
                && consolidated.Silver == wallet.Silver
                && consolidated.Gold == wallet.Gold)
                return; // already canonical - don't churn the grid

            isConsolidating = true;

            try
            {
                RemoveCurrency(CurrencyType.Iron, wallet.Iron);
                RemoveCurrency(CurrencyType.Copper, wallet.Copper);
                RemoveCurrency(CurrencyType.Silver, wallet.Silver);
                RemoveCurrency(CurrencyType.Gold, wallet.Gold);

                AddChange(consolidated);
            }
            finally
            {
                isConsolidating = false;
            }

            InvokeRefresh();
        }
```

- [x] **Step 3: Honour `AutoConsolidate` on insert**

In `CharacterInventory.TryAddToContainer`, replace the body with:

```csharp
        public override bool TryAddToContainer(ref Package package)
        {
            if (!package.IsValid)
                return false;

            /// TryStack
            _ = TryStack(ref package);

            /// TryAddToEmpty
            _ = TryAddAtEmpty(ref package);

            if (AutoConsolidate)
                Consolidate();

            InvokeRefresh();

            return 0 == package.Amount;
        }
```

- [x] **Step 4: Correct `AddChange`'s doc comment**

Its comment currently claims "Each denomination of a valid change amount is always below its stack limit". That holds for change but **not** for consolidation — 30 gold exceeds gold's limit of 12. `AddCoins` routes through `TryAddToContainer`, which spills into further cells, so the behaviour is fine; only the claim is wrong. Replace the first sentence of the `<summary>` with:

```csharp
        /// Returns coins to the inventory, largest denomination first. Used both for
        /// change (where each denomination is always below its stack limit) and for
        /// <see cref="Consolidate"/> (where it may not be - TryAddToContainer spills
        /// the overflow into further cells).
```

- [x] **Step 5: Add the button entry point**

In `InventoryProvider`, directly under `public void SortPlayerInventory() => Inventory.Sort();`:

```csharp
        public void ConsolidatePlayerCurrency() => Inventory.Consolidate();
```

- [x] **Step 6: Compile-check via the bridge**

Expected: no `error CS` entries.

- [ ] **Step 7: Wire the button in the scene** — DEFERRED (needs the Editor; code entry point `ConsolidatePlayerCurrency()` is in place; on the user checklist)

Open the Example scene, duplicate the existing **Sort** button in the inventory panel, rename it `ConsolidateButton`, set its label to `Consolidate`, and point its `OnClick` at `InventoryProvider.ConsolidatePlayerCurrency`. Save the scene.

- [ ] **Step 8: Verify in the editor** — DEFERRED (needs Play mode; on the user checklist)

Enter Play mode. Press the debug **Iron** button ~130 times (raise the Amount slider) to get more than 120 iron across two cells. Note the total shown in the currency readout. Press **Consolidate**.

Expected: the iron collapses into copper (and silver, if the total reaches 60), the readout **total is unchanged**, and the number of occupied cells drops. Press Consolidate a second time — nothing should move, and there must be no freeze or stack overflow (that is the re-entrancy guard doing its job).

- [x] **Step 9: Commit**

```bash
git add Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs Assets/Scenes
git commit -m "feat: deliberate coin consolidation, with a seam for an auto stash

Replaces the removed auto-upgrade. Consolidate() folds the wallet into the
largest denominations that fit and is value-preserving. AutoConsolidate is
virtual and false everywhere today; a future stash overrides it to true and
gets the 'everything becomes gold in the bank' behaviour with no rework.

Guarded against re-entrancy: AddChange re-enters TryAddToContainer, which is
where AutoConsolidate is honoured.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Fix the coin readout order

`CurrencyDisplay` hardcodes Gold, Silver, **Iron, Copper** — descending under the old ladder, wrong under the new one.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/GUI/Components/Displays/CurrencyDisplay.cs:23-26`
- Test: none possible. Verified visually.

**Interfaces:**
- Consumes: `Currency.Iron/Copper/Silver/Gold` from Task 3.
- Produces: nothing.

- [x] **Step 1: Swap the last two rows**

```csharp
            coinDisplays[0].Display(CurrencyType.Gold, newData.Gold);
            coinDisplays[1].Display(CurrencyType.Silver, newData.Silver);
            coinDisplays[2].Display(CurrencyType.Copper, newData.Copper);
            coinDisplays[3].Display(CurrencyType.Iron, newData.Iron);
```

- [x] **Step 2: Compile-check via the bridge**

Expected: no `error CS` entries.

- [ ] **Step 3: Verify in the editor** — DEFERRED (visual check; on the user checklist)

Enter Play mode with a mixed wallet. Expected left-to-right: gold, silver, copper, iron — descending by value, with the orange copper icon before the grey iron one.

- [x] **Step 4: Commit**

```bash
git add Assets/Scripts/InventorySystem/GUI/Components/Displays/CurrencyDisplay.cs
git commit -m "fix: order the coin readout by the new ladder

Gold, silver, copper, iron. Iron is now the cheapest coin, so it belongs
last rather than above copper.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: Retire the `ItemStack` enum

`StackLimit` is typed `ItemStack`, an enum defining only `NONE/1/10/50/100`, and the currency code already bypasses it with `(ItemStack)120u`. It provides no safety — only a cast to forget. This lands **last** so that if it goes wrong it is one isolated revert.

⚠️ **`Assets/Scripts/_DragDropHarness.cs` is untracked, so it is present in the working tree on every branch, and it uses `ItemStack.Single`.** It will break the compile if you do not update it. Do not commit it.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Items/AbstractItem.cs:17`, `:112`, `:131`, `:235`, `:377-386`
- Modify: `Assets/Scripts/InventorySystem/Data/Structs/Package.cs:21`, `:30`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs:47`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterEquipment.cs:84-87`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs:51`
- Modify: `Assets/Scripts/InventorySystem/GUI/Components/Displays/PreviewDisplay.cs:62`, `:115`
- Modify (do **not** commit): `Assets/Scripts/_DragDropHarness.cs:29`
- Delete: `Assets/Scripts/InventorySystem/Data/Enums/ItemStack.cs` and its `.meta`
- Test: none possible. Verified by compile + in-editor check.

**Interfaces:**
- Consumes: nothing.
- Produces: `AbstractItem.StackLimit` — `uint`, default `1u`.

- [x] **Step 1: Change the property and its in-file uses**

`AbstractItem.cs:17`:
```csharp
        [field: SerializeField] public uint StackLimit { get; protected set; } = 1u;
```
Line 131 (`ConsumableItem`): `StackLimit = 10u; // TODO: Get type specific stack limit`
Line 235 (`EquipmentItem`): `StackLimit = 1u;`
Line 112 (`Equals`) needs no change — `uint == uint` still compiles.

In `CurrencyItem`, drop the casts and the two `ItemStack.NONE` arms:
```csharp
            StackLimit = CurrencyType switch
            {
                CurrencyType.Iron => 120u,
                CurrencyType.Copper => 60u,
                CurrencyType.Silver => 20u,
                CurrencyType.Gold => 12u,

                CurrencyType.NONE => 0u,
                _ => 0u,
            };
```

- [x] **Step 2: Drop the casts at every consumer**

`Package.cs:21`: `if (item != null && item.StackLimit < amount)`
`Package.cs:30`: `public readonly uint SpaceLeft => Item.StackLimit - Amount;`
`AbstractDimensionalContainer.cs:47`: `if (!package.IsValid || package.Item.StackLimit <= 1u)`
`CharacterEquipment.cs:84`: `if (1u < package.Item.StackLimit)`
`CharacterEquipment.cs:87`: `var amount = Math.Min(package.Amount, package.Item.StackLimit);`
`CharacterInventory.cs:51`: `var amount = Math.Min(package.Amount, package.Item.StackLimit);`
`PreviewDisplay.cs:62` and `:115`: `$"{package.Amount}/{package.Item.StackLimit}"`
`_DragDropHarness.cs:29`: `StackLimit = 1u;`

- [x] **Step 3: Delete the enum**

```bash
rm Assets/Scripts/InventorySystem/Data/Enums/ItemStack.cs Assets/Scripts/InventorySystem/Data/Enums/ItemStack.cs.meta
```

- [x] **Step 4: Confirm nothing references it**

Run:
```bash
grep -rn "ItemStack" --include=*.cs Assets/Scripts
```
Expected: no output.

- [x] **Step 5: Compile-check via the bridge**

Expected: no `error CS` entries. Run a **negative control** before believing the green: insert `DELIBERATE_SENTINEL_ERROR` into `Package.cs`, recompile, confirm it surfaces in `Unity_GetConsoleLogs`, then remove it and recompile. A false green has happened in this project before.

- [ ] **Step 6: Verify serialized items survived** — DEFERRED (needs the Editor; on the user checklist)

Changing a serialized field's type from an enum to `uint` can reset it on any item persisted inside a scene or prefab. Open the Example scene, enter Play mode, and confirm the starting inventory still stacks correctly — consumables to 10, equipment to 1, coins to 120/60/20/12. If a stack limit reads 0, that item was serialized and needs its value re-authored.

- [x] **Step 7: Commit**

```bash
git add Assets/Scripts/InventorySystem/Data/Items/AbstractItem.cs Assets/Scripts/InventorySystem/Data/Structs/Package.cs Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterEquipment.cs Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterInventory.cs Assets/Scripts/InventorySystem/GUI/Components/Displays/PreviewDisplay.cs
git rm --cached Assets/Scripts/InventorySystem/Data/Enums/ItemStack.cs Assets/Scripts/InventorySystem/Data/Enums/ItemStack.cs.meta
git commit -m "refactor: type StackLimit as uint and delete the ItemStack enum

The enum defined only 1/10/50/100 while the currency code already bypassed
it with (ItemStack)120u, so it bought no safety - just a cast to forget.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

Check `git status` first and make sure `_DragDropHarness.cs` is **not** staged.

---

## Done when

- The ladder is iron → copper → silver → gold at 5 / 12 / 20, and one gold is still 1200 base units.
- No coin auto-upgrades. Coins accumulate across as many cells as they need.
- The Consolidate button folds the wallet without changing its total, and is idempotent.
- All EditMode tests pass and the console is clean.
- `grep -rn "copperToIron\|copperToGold\|ItemStack\|CalculateGoldValue" --include=*.cs Assets/Scripts` returns nothing.

## Not in this plan

Phase 2 (currency drops become stacks; reweight the table to Iron 40 / Copper 40 / Silver 8 / Gold 1; measure the equipment-vs-currency income split first) and Phase 3 (stash with `AutoConsolidate => true`; PoE-style currency uses; a money-changer who takes a cut). Both are specified in `dev/specs/2026-08-30-currency-redesign-design.md`.

Also deliberately excluded: the spec's optional Phase 0 item to stop
`AbstractProbabilityDistribution.Probabilities` allocating and re-sorting on every
access while `GetRandomEnumerator` reads it inside a nested loop. It is a real
O(n²)-allocations-per-roll wart, but it is correct, it affects every distribution
in the project rather than currency alone, and folding it in here would put an
unrelated hot-path rewrite inside a currency branch. Worth its own commit.
