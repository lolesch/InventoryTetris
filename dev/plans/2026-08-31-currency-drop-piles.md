# Currency Drop Piles — Implementation Plan (Phase 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task, with a review checkpoint after each task. Steps use checkbox (`- [ ]`) syntax. Do **not** dispatch subagents for this work (project rule).

**Goal:** A currency drop becomes one roll that yields a *pile* of one coin type, amount from a per-type range, and the loot pipeline carries quantity end to end.

**Architecture:** A new `CurrencyDropTable` ScriptableObject holds four `Vector2Int` amount ranges. A pure `CurrencyDropRoll.Amount(min, max, roll01)` maps a uniform roll to a whole amount (this is the only unit-testable piece). `ItemProvider.GenerateRandomCurrency` returns a `Package` (coin + rolled amount) instead of a bare `AbstractItem`; that `Package` type flows up through `GenerateRandomItem` and `GenerateRandomLoot` to the two loot consumers. The `CurrencyType` drop-weight asset is reweighted so iron and copper dominate.

**Tech Stack:** Unity 6000.3.9f1, C# (Roslyn), Unity Test Framework (NUnit) EditMode tests, `.asmdef` assemblies, unity-mcp bridge for compile verification.

**Design:** `dev/specs/2026-08-31-currency-drop-piles-design.md`. Umbrella: `dev/specs/2026-08-30-currency-redesign-design.md` § Phase 2.

## Global Constraints

- **Base branch:** `feature/currency-redesign`, currently `21c3fa3`. Phase 0/1 shipped on it (`727c742`..`8daa96b`). Continue on this branch. Do **not** rebase onto `main`.
- **Gold's value does not change.** No `Currency` constant, item price, affix `goldRatio`, or vendor markup is touched. `Currency` and `CurrencyTests` are not modified by this plan.
- **Do not reorder the `CurrencyType` enum.** `NONE = 0, Copper = 1, Iron = 2, Silver = 3, Gold = 4` stays exactly as it is.
- **Do not hand-edit `probabilities` or `exampleResults`** in `Currency Type Distribution.asset`. They are `[ReadOnly]` caches `AbstractProbabilityDistribution.OnValidate` regenerates from `quantities`. Edit `Quantity` only; let the editor rebake (Task 6).
- **Every new `.cs` needs a `.cs.meta`** with a fresh GUID, or Unity will generate one on import and the plan is no longer reproducible headless. Each create-a-file task includes the `.meta` via a `printf` + `powershell [guid]::NewGuid()` command. If `powershell` is unavailable, swap the GUID subshell for `python -c "import uuid; print(uuid.uuid4().hex)"`. If the Editor is open and imports the `.cs` before you write the `.meta`, Unity already made one — `git status` will show it; keep Unity's and skip the `printf`.
- **Check `git status` before every commit.** Stage only the files the task names. `.idea/` and `dev/specs/2026-08-30-probability-distribution-rebuild-design.md` are untracked and not part of this work — leave them.
- **Commits:** one per task. Prefix `feat:` / `fix:` / `refactor:` / `docs:`, body explains *why*. End every message with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  ```

## Green

**Green** = the project compiles with zero `error CS` *and* EditMode tests pass (`InventorySystem.Data.Tests`, `InventorySystem.Geometry.Tests`). See the memory `unity-mcp-compile-verification` for the full mechanics.

**If the Unity Editor is open** (check: `ls Temp/UnityLockfile`) — compile-check via the unity-mcp bridge:
- `Unity_ValidateScript` with `Uri: "Assets/…/File.cs"`, `Level: "standard"`, `IncludeDiagnostics: true` for a single changed file, **or** `Unity_RunCommand` with
  ```csharp
  AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
  global::UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
  ```
  then `Unity_GetConsoleLogs` (not `Unity_ReadConsole` — it has returned 0 entries for real errors here). Expected: no `error CS`.
- Tests: the bridge cannot run them. Ask the user to run **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**. Do not propose closing their editor.

**If the Editor is closed** — batch mode compiles *and* runs tests in one shot:
```bash
rm -f Temp/UnityLockfile
"/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" -runTests -batchmode \
  -projectPath "C:/Users/loles/Desktop/LEONID/InventoryTetris" -testPlatform EditMode \
  -testResults "C:/Users/loles/AppData/Local/Temp/claude/p2-results.xml" \
  -logFile "C:/Users/loles/AppData/Local/Temp/claude/p2-log.txt"
```
Don't trust the exit code. Read `<test-run … passed= failed=>` from the XML and `grep -c "error CS"` the log. A compile failure produces **no** results XML.

**Negative control (once, before believing the final green):** insert `DELIBERATE_SENTINEL_ERROR` into a changed `.cs`, recompile, confirm it surfaces, remove it, recompile. A false green has happened in this project before.

**`dotnet build` is useless here** — stale `.csproj` files report phantom `CS2001`s and don't compile what you changed.

## What is and is not unit-testable

`InventorySystem.Data.asmdef` covers **only** `Assets/Scripts/InventorySystem/Data/Statistics/`. `CurrencyDropRoll.cs` goes there and gets real red-green tests (Task 1).

`CurrencyDropTable` (a `ScriptableObject` in `Data/Distributions/`), `ItemProvider`, `InventoryProvider`, `DummyTarget` all live in `Assembly-CSharp`, which no asmdef test assembly can reference. Tasks 2–5 are verified by compile + the in-editor check in Task 6. Do not invent a test assembly to paper over this.

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `Assets/Scripts/InventorySystem/Data/Statistics/CurrencyDropRoll.cs` | Pure roll→amount mapping | 1 |
| `Assets/Scripts/Tests/EditMode/Statistics/CurrencyDropRollTests.cs` | Locks the mapping, incl. the `roll01 == 1` edge | 1 |
| `Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs` | Serialized per-type ranges; `RollAmount(CurrencyType)` | 2 |
| `Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs` | Debug buttons — delete dead ones (3); update loot/currency adders (4) | 3, 4 |
| `Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs` | `GenerateRandomCurrency`/`Item`/`Loot` become quantity-aware | 4 |
| `Assets/Scripts/InventorySystem/Runtime/Character/DummyTarget.cs` | Drops loot on death — iterate `Package`s | 4 |
| `Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset` | Drop weights → 40/40/8/1 | 5 |
| `Assets/Scenes/Example.unity`, `Assets/Scenes/HUD.unity`, `Currency Drop Table.asset` | Editor wiring | 6 |

---

## Task 1: `CurrencyDropRoll` — the pure roll→amount mapping

**Files:**
- Create: `Assets/Scripts/InventorySystem/Data/Statistics/CurrencyDropRoll.cs` (+ `.cs.meta`)
- Create: `Assets/Scripts/Tests/EditMode/Statistics/CurrencyDropRollTests.cs` (+ `.cs.meta`)

**Interfaces:**
- Consumes: nothing.
- Produces: `ToolSmiths.InventorySystem.Data.CurrencyDropRoll.Amount(int min, int max, float roll01) -> uint`. Task 2 consumes it.

- [ ] **Step 1: Write the failing test file**

Create `Assets/Scripts/Tests/EditMode/Statistics/CurrencyDropRollTests.cs`:

```csharp
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Locks CurrencyDropRoll.Amount: flat map from a [0,1] roll to a whole amount
    /// in [min, max] inclusive, with the half-open top (roll == 1) folded back to max.
    /// </summary>
    [TestFixture]
    public sealed class CurrencyDropRollTests
    {
        [Test]
        public void RollOfZero_ReturnsMin() =>
            Assert.That(CurrencyDropRoll.Amount(10, 30, 0f), Is.EqualTo(10u));

        [Test]
        public void RollOfExactlyOne_ReturnsMax_NotMaxPlusOne() =>
            Assert.That(CurrencyDropRoll.Amount(10, 30, 1f), Is.EqualTo(30u));

        [Test]
        public void RollJustBelowOne_ReturnsMax() =>
            Assert.That(CurrencyDropRoll.Amount(10, 30, 0.9999f), Is.EqualTo(30u));

        [Test]
        public void Midpoint_LandsMidRange() =>
            // span 21, offset = floor(0.5 * 21) = 10
            Assert.That(CurrencyDropRoll.Amount(10, 30, 0.5f), Is.EqualTo(20u));

        [Test]
        public void DegenerateRange_AlwaysReturnsThatValue()
        {
            Assert.That(CurrencyDropRoll.Amount(1, 1, 0f), Is.EqualTo(1u));
            Assert.That(CurrencyDropRoll.Amount(1, 1, 0.5f), Is.EqualTo(1u));
            Assert.That(CurrencyDropRoll.Amount(1, 1, 1f), Is.EqualTo(1u));
        }

        [Test]
        public void EveryRoll_StaysWithinRange()
        {
            for (var i = 0; i <= 1000; i++)
                Assert.That(CurrencyDropRoll.Amount(4, 12, i / 1000f), Is.InRange(4u, 12u), $"roll {i / 1000f}");
        }
    }
}
```

Create its meta — run:
```bash
printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' \
  "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" \
  > "Assets/Scripts/Tests/EditMode/Statistics/CurrencyDropRollTests.cs.meta"
```

- [ ] **Step 2: Run the tests to verify they fail**

Per **Green** (Test Runner if the editor is open, batch if closed).
Expected: compile error — `CurrencyDropRoll` does not exist. A compile error blocks the whole run; that is the red.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/InventorySystem/Data/Statistics/CurrencyDropRoll.cs`:

```csharp
using System;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// Maps a uniform roll in [0, 1] to a whole amount in [min, max] inclusive,
    /// flat-weighted. Pure and Unity-free so EditMode tests can reach it;
    /// <see cref="Distributions.CurrencyDropTable"/> feeds it UnityEngine.Random.value.
    /// </summary>
    public static class CurrencyDropRoll
    {
        public static uint Amount(int min, int max, float roll01)
        {
            if (max <= min)
                return (uint)Math.Max(0, min);

            var span = max - min + 1;            // number of distinct outcomes
            var offset = (int)(roll01 * span);   // 0 .. span; == span only when roll01 == 1
            if (offset >= span)
                offset = span - 1;               // fold the half-open top back to max

            return (uint)(min + offset);
        }
    }
}
```

Create its meta:
```bash
printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' \
  "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" \
  > "Assets/Scripts/InventorySystem/Data/Statistics/CurrencyDropRoll.cs.meta"
```

- [ ] **Step 4: Run the tests to verify they pass**

Per **Green**. Expected: all 6 `CurrencyDropRollTests` PASS; `CurrencyTests` and `InventorySystem.Geometry.Tests` still pass; 0 `error CS`.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/InventorySystem/Data/Statistics/CurrencyDropRoll.cs" \
        "Assets/Scripts/InventorySystem/Data/Statistics/CurrencyDropRoll.cs.meta" \
        "Assets/Scripts/Tests/EditMode/Statistics/CurrencyDropRollTests.cs" \
        "Assets/Scripts/Tests/EditMode/Statistics/CurrencyDropRollTests.cs.meta"
git commit -m "feat: add CurrencyDropRoll, the pure amount-from-roll mapping

The one piece of Phase 2 that an EditMode test can reach. Flat map from a
[0,1] roll to a whole amount in [min,max]; folds the half-open top (roll == 1)
back to max so a drop can never come in one over the authored maximum.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: `CurrencyDropTable` ScriptableObject

**Files:**
- Create: `Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs` (+ `.cs.meta`)
- Test: none possible — a `ScriptableObject` that calls `UnityEngine.Random`. The roll core is already tested (Task 1). Verified by compile + Task 6.

**Interfaces:**
- Consumes: `CurrencyDropRoll.Amount` (Task 1); `ToolSmiths.InventorySystem.Data.Enums.CurrencyType`.
- Produces: `ToolSmiths.InventorySystem.Data.Distributions.CurrencyDropTable`, an SO with `Vector2Int RangeFor(CurrencyType)` and `uint RollAmount(CurrencyType)`. Task 4 holds a serialized reference to it; Task 6 creates the `.asset`.

- [ ] **Step 1: Write the class**

Create `Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs`:

```csharp
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    /// <summary>
    /// Per-coin drop amount ranges. A currency drop rolls its type from
    /// <c>Currency Type Distribution</c>, then its pile size from here. Not an
    /// <see cref="AbstractProbabilityDistribution{T}"/> — it is a range table, not a
    /// probability distribution. Four explicit fields because the CurrencyType enum
    /// is frozen and there are exactly four coins.
    /// </summary>
    [CreateAssetMenu(fileName = "Currency Drop Table", menuName = "Inventory System/Currency Drop Table")]
    public class CurrencyDropTable : ScriptableObject
    {
        [SerializeField] private Vector2Int iron = new(10, 30);
        [SerializeField] private Vector2Int copper = new(4, 12);
        [SerializeField] private Vector2Int silver = new(1, 3);
        [SerializeField] private Vector2Int gold = new(1, 1);

        public Vector2Int RangeFor(CurrencyType type) => type switch
        {
            CurrencyType.Iron => iron,
            CurrencyType.Copper => copper,
            CurrencyType.Silver => silver,
            CurrencyType.Gold => gold,

            CurrencyType.NONE => Vector2Int.zero,
            _ => Vector2Int.zero,
        };

        /// <summary>Rolls a pile size for <paramref name="type"/>. 0 for NONE / an unset range.</summary>
        public uint RollAmount(CurrencyType type)
        {
            var range = RangeFor(type);
            return range == Vector2Int.zero ? 0u : CurrencyDropRoll.Amount(range.x, range.y, Random.value);
        }
    }
}
```

- [ ] **Step 2: Write the meta**

```bash
printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' \
  "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" \
  > "Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs.meta"
```

- [ ] **Step 3: Compile-check**

Per **Green** (bridge if editor open, batch if closed).
Expected: 0 `error CS`. EditMode tests unchanged from Task 1.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs" \
        "Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs.meta"
git commit -m "feat: add CurrencyDropTable, the per-coin pile-size ranges

Four serialized Vector2Int ranges (iron 10-30, copper 4-12, silver 1-3,
gold 1-1). RollAmount() draws a flat uniform pile via CurrencyDropRoll.
The .asset and its scene wiring are editor work (see the plan's Task 6).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 3: Delete the dead per-type currency debug methods

`InventoryProvider.SetItemToIron/Copper/Silver/Gold` and their `AddCurrency(CurrencyType)` helper are unreachable — no `Button.onClick` in `Example.unity` or `HUD.unity` wires them, and no other code calls them. `AddRandomCurrency` is the only currency button, and Task 4 reworks it. Removing this first shrinks the file before that edit.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs`
- Test: none possible — `MonoBehaviour` in `Assembly-CSharp`.

**Interfaces:**
- Consumes: nothing.
- Produces: nothing. Pure deletion.

- [ ] **Step 1: Confirm the methods are still dead**

Run:
```bash
grep -rn "SetItemToIron\|SetItemToCopper\|SetItemToSilver\|SetItemToGold\|\.AddCurrency(" --include=*.cs Assets/Scripts
grep -c "SetItemToIron\|AddRandomCurrency" Assets/Scenes/Example.unity Assets/Scenes/HUD.unity
```
Expected: the `.cs` matches are all inside `InventoryProvider.cs` itself. The scene grep shows `AddRandomCurrency` present and `SetItemToIron` absent (count is for the combined pattern; verify no `SetItemTo{coin}` line by eye if unsure).

- [ ] **Step 2: Delete the helper**

Remove this block from `InventoryProvider.cs` (currently around line 82):

```csharp
        private void AddCurrency(CurrencyType currencyType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomCurrency = ItemProvider.Instance.GenerateCurrency(currencyType);
                _ = CharacterProvider.Instance.Player.PickUpItem(new Package(null, randomCurrency, 1u));
            }
        }
```

- [ ] **Step 3: Delete the four button methods**

Remove these four lines (currently around line 139):

```csharp
        public void SetItemToIron() => AddCurrency(CurrencyType.Iron);
        public void SetItemToCopper() => AddCurrency(CurrencyType.Copper);
        public void SetItemToSilver() => AddCurrency(CurrencyType.Silver);
        public void SetItemToGold() => AddCurrency(CurrencyType.Gold);
```

Leave `SetItemToArrows/Books/Potions` and the equipment `SetItemTo*` methods — those are wired.

- [ ] **Step 4: Compile-check**

Per **Green**. Expected: 0 `error CS`. `CurrencyType` may now be unused in this file; the `using ToolSmiths.InventorySystem.Data.Enums;` stays (it still covers `EquipmentType`, `ConsumableType`). No warning blocks the build.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs
git commit -m "refactor: drop the dead per-type currency debug methods

SetItemToIron/Copper/Silver/Gold and their AddCurrency helper were wired to
no button in either scene. AddRandomCurrency is the only currency debug
entry point; Task 4 makes it roll piles.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 4: Quantity-aware loot pipeline

`GenerateRandomCurrency` returns a `Package` (coin + rolled pile). That `Package` type flows up through `GenerateRandomItem` and `GenerateRandomLoot`, and the two loot consumers stop wrapping in `Package(_, _, 1u)`. `GenerateCurrency` (singular) and equipment/consumable generation are untouched.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Character/DummyTarget.cs`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs`
- Test: none possible. Verified by compile + Task 6.

**Interfaces:**
- Consumes: `CurrencyDropTable` (Task 2).
- Produces:
  - `ItemProvider.GenerateRandomCurrency() -> Package`
  - `ItemProvider.GenerateRandomLoot(uint) -> List<Package>`
  - `ItemProvider.GenerateRandomItem()` stays `private`, now returns `Package`
  - unchanged: `ItemProvider.GenerateCurrency(CurrencyType) -> AbstractItem`, `GenerateRandomEquipment/Consumable(...)` -> `AbstractItem`

- [ ] **Step 1: Add the serialized field to `ItemProvider`**

In `ItemProvider.cs`, in the `[Header("Distributions")]` block (after `currencyTypeDistribution`, around line 28):

```csharp
        [SerializeField] private CurrencyTypeDistribution currencyTypeDistribution;
        [SerializeField] private CurrencyDropTable currencyDropTable;
```

`CurrencyDropTable` is in `ToolSmiths.InventorySystem.Data.Distributions`, already imported at the top of the file (line 3).

- [ ] **Step 2: Rework `GenerateRandomCurrency` to return a `Package`**

Replace (currently around line 270):

```csharp
        public AbstractItem GenerateRandomCurrency()
        {
            var currency = currencyTypeDistribution.GetRandomEnumerator();

            return GenerateCurrency(currency);
        }
```

with:

```csharp
        public Package GenerateRandomCurrency()
        {
            var currency = currencyTypeDistribution.GetRandomEnumerator();

            if (currencyDropTable == null)
            {
                Debug.LogError($"{nameof(ItemProvider)}: {nameof(currencyDropTable)} is not assigned - no currency will drop");
                return default;
            }

            var amount = currencyDropTable.RollAmount(currency);

            return amount == 0u
                ? default
                : new Package(null, GenerateCurrency(currency), amount);
        }
```

Leave `GenerateCurrency(CurrencyType currencyType) => new CurrencyItem(currencyType);` exactly as it is.

- [ ] **Step 3: Change `GenerateRandomItem` to return a `Package`**

Replace (currently around line 76):

```csharp
        private AbstractItem GenerateRandomItem()
        {
            /// selects item type
            var itemCategory = itemCategoryDistribution.GetRandomEnumerator();

            return itemCategory switch
            {
                ItemCategory.Equipment => GenerateRandomEquipment(),
                ItemCategory.Consumable => GenerateRandomConsumable(),
                ItemCategory.Currency => GenerateRandomCurrency(),

                ItemCategory.NONE => null,
                _ => null,
            };
        }
```

with:

```csharp
        private Package GenerateRandomItem()
        {
            /// selects item type
            var itemCategory = itemCategoryDistribution.GetRandomEnumerator();

            return itemCategory switch
            {
                ItemCategory.Equipment => new Package(null, GenerateRandomEquipment(), 1u),
                ItemCategory.Consumable => new Package(null, GenerateRandomConsumable(), 1u),
                ItemCategory.Currency => GenerateRandomCurrency(),

                ItemCategory.NONE => default,
                _ => default,
            };
        }
```

`GenerateRandomEquipment()` / `GenerateRandomConsumable()` can still return `null` (NoDrop rarity). `new Package(null, null, 1u)` is inert — `Package.IsValid` is false, and every consumer already checks it. This matches the pre-change behaviour where a `null` item was added and skipped.

- [ ] **Step 4: Change `GenerateRandomLoot` to return `List<Package>`**

Replace (currently around line 58):

```csharp
        public List<AbstractItem> GenerateRandomLoot(uint amount = 1)
        {
            var generatedLoot = new List<AbstractItem>();
            /// calculates the number of items to drop
            CalculateBonusDrops(ref amount);

            for (var i = 0; i < amount; i++)
                generatedLoot.Add(GenerateRandomItem());

            return generatedLoot;
```

with (only the two type names change):

```csharp
        public List<Package> GenerateRandomLoot(uint amount = 1)
        {
            var generatedLoot = new List<Package>();
            /// calculates the number of items to drop
            CalculateBonusDrops(ref amount);

            for (var i = 0; i < amount; i++)
                generatedLoot.Add(GenerateRandomItem());

            return generatedLoot;
```

Leave the nested `CalculateBonusDrops` local function unchanged.

- [ ] **Step 5: Update `DummyTarget.OnDeath`**

In `Assets/Scripts/InventorySystem/Runtime/Character/DummyTarget.cs`, replace:

```csharp
            var randomEquipment = ItemProvider.Instance.GenerateRandomLoot();

            foreach (var item in randomEquipment)
                //rework to drop items on the floor
                _ = CharacterProvider.Instance.Player.PickUpItem(new Package(null, item, 1u));
```

with:

```csharp
            var loot = ItemProvider.Instance.GenerateRandomLoot();

            foreach (var package in loot)
                //rework to drop items on the floor
                _ = CharacterProvider.Instance.Player.PickUpItem(package);
```

- [ ] **Step 6: Update `InventoryProvider.AddRandomLoot` and `AddRandomCurrency`**

In `InventoryProvider.cs`, replace:

```csharp
        public void AddRandomLoot()
        {
            var items = ItemProvider.Instance.GenerateRandomLoot(Amount);

            for (var i = 0; i < items.Count; i++)
                _ = CharacterProvider.Instance.Player.PickUpItem(new Package(null, items[i], 1u));
        }

        public void AddRandomCurrency()
        {
            var randomCurrency = ItemProvider.Instance.GenerateRandomCurrency();

            for (var i = 0; i < Amount; i++)
            {
                var package = new Package(null, randomCurrency, 1u);
                _ = CharacterProvider.Instance.Player.PickUpItem(package);
            }
        }
```

with:

```csharp
        public void AddRandomLoot()
        {
            var loot = ItemProvider.Instance.GenerateRandomLoot(Amount);

            for (var i = 0; i < loot.Count; i++)
                _ = CharacterProvider.Instance.Player.PickUpItem(loot[i]);
        }

        public void AddRandomCurrency()
        {
            for (var i = 0; i < Amount; i++)
                _ = CharacterProvider.Instance.Player.PickUpItem(ItemProvider.Instance.GenerateRandomCurrency());
        }
```

- [ ] **Step 7: Confirm no other caller of the changed signatures**

Run:
```bash
grep -rn "GenerateRandomLoot\|GenerateRandomCurrency\|GenerateRandomItem" --include=*.cs Assets/Scripts
```
Expected: only `ItemProvider.cs` (definitions), `DummyTarget.cs:18`, `InventoryProvider.cs` (`AddRandomLoot`, `AddRandomCurrency`). If anything else appears, it needs the same treatment — a `List<AbstractItem>` local becomes `List<Package>`, an `AbstractItem` becomes `Package`.

- [ ] **Step 8: Compile-check + negative control**

Per **Green**. Expected: 0 `error CS`, EditMode tests still 100% pass. Run the `DELIBERATE_SENTINEL_ERROR` control in `ItemProvider.cs` once here.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs \
        Assets/Scripts/InventorySystem/Runtime/Character/DummyTarget.cs \
        Assets/Scripts/InventorySystem/Runtime/Provider/InventoryProvider.cs
git commit -m "feat: currency drops are piles - quantity flows through the loot pipeline

GenerateRandomCurrency rolls a type, then a pile size from CurrencyDropTable,
and returns a Package. That Package type carries up through GenerateRandomItem
and GenerateRandomLoot (now List<Package>); DummyTarget.OnDeath and the two
InventoryProvider adders stop hard-coding amount 1. GenerateCurrency (single
coin) and equipment/consumable generation are untouched.

The debug currency button now rolls one full type+pile per slider tick,
fixing the old roll-once-outside-the-loop bug.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 5: Reweight the currency type table

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset`
- Test: none — a `ScriptableObject` asset. The `[ReadOnly]` caches rebake in the editor (Task 6).

**Interfaces:**
- Consumes: nothing.
- Produces: nothing. Data only.

- [ ] **Step 1: Read the current quantities**

Run:
```bash
sed -n '16,32p' "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset"
```
Expected — labels correct (fixed in `d987d44`), Phase 1 weights:
```yaml
  quantities:
  - name: NONE
    Enumeration: 0
    Quantity: 0
  - name: Copper
    Enumeration: 1
    Quantity: 4
  - name: Iron
    Enumeration: 2
    Quantity: 8
  - name: Silver
    Enumeration: 3
    Quantity: 2
  - name: Gold
    Enumeration: 4
    Quantity: 1
```

- [ ] **Step 2: Set the Phase 2 weights**

Edit only the four `Quantity` values under `quantities:`. Leave `name`, `Enumeration`, and everything below `quantities:` (the `probabilities` / `exampleResults` caches) exactly as they are — the editor rebakes those in Task 6.

```yaml
  - name: Copper
    Enumeration: 1
    Quantity: 40
  - name: Iron
    Enumeration: 2
    Quantity: 40
  - name: Silver
    Enumeration: 3
    Quantity: 8
  - name: Gold
    Enumeration: 4
    Quantity: 1
```

- [ ] **Step 3: Verify the edit**

Run:
```bash
grep -A2 "name: \(Copper\|Iron\|Silver\|Gold\)" "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset" | grep Quantity
```
Expected: `40`, `40`, `8`, `1`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset"
git commit -m "feat: reweight currency drops to iron 40 / copper 40 / silver 8 / gold 1

Phase 2 weights: iron and copper are ~90% of currency drops, silver
~1-in-11, gold ~1-in-89. Only the quantities array changes; OnValidate
rebakes the [ReadOnly] probability caches on the next editor focus.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 6: Editor wiring and play verification

**This task needs the Unity Editor. It cannot be scripted from the repo.** Everything above is code and data; this is the integration.

**Files:**
- Create (in-editor): `Assets/Scripts/InventorySystem/Data/Distributions/Currency Drop Table.asset` (+ `.meta`)
- Modify (in-editor): `Assets/Scenes/Example.unity`, `Assets/Scenes/HUD.unity`
- Modify (in-editor): `Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset` (cache rebake from Task 5)

- [ ] **Step 1: Create the drop-table asset**

In the Project window, right-click `Assets/Scripts/InventorySystem/Data/Distributions/` ▸ **Create ▸ Inventory System ▸ Currency Drop Table**. Name it `Currency Drop Table`. The four ranges default to iron (10,30) / copper (4,12) / silver (1,3) / gold (1,1) — confirm, adjust if you want.

- [ ] **Step 2: Wire it into `ItemProvider` in both scenes**

`ItemProvider` is a component authored directly in each scene, not a prefab, so this is done twice:

1. Open `Assets/Scenes/Example.unity`. Select the GameObject with the `ItemProvider` component (it sits alongside the other `*Distribution` references). Drag `Currency Drop Table.asset` onto the new **Currency Drop Table** field. Save the scene.
2. Open `Assets/Scenes/HUD.unity`. Same object, same field, same asset. Save the scene.

If a scene is missed, `GenerateRandomCurrency` logs `ItemProvider: currencyDropTable is not assigned` and drops nothing — that error is the signal.

- [ ] **Step 3: Let the type-table caches rebake**

Select `Currency Type Distribution.asset` in the Project window so `OnValidate` runs. Confirm:
```bash
grep -A2 "name: Iron" "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset" | grep Probability
```
Expected: `Probability: 0.44943821` (or very close). `git diff --stat` should show the asset modified.

- [ ] **Step 4: Play-test**

Enter Play mode in the Example scene.

- Set the amount slider to **10**, press the currency debug button (`AddRandomCurrency`). Expect ~10 piles to arrive — a spread of types roughly 45% iron / 45% copper / 9% silver / 1% gold, each pile a single stack within its limit (iron ≤ 30, copper ≤ 12, silver ≤ 3, gold 1). The currency readout total should be in the low thousands, not ~10.
- Press **Consolidate**. The total must not change; occupied cells should drop.
- Kill a `DummyTarget` several times. When a currency drop rolls, it must arrive as a pile, not a single coin.
- Press the random-loot button with the slider high. Currency entries in the loot are piles; equipment and consumables are still single items.

- [ ] **Step 5: Commit the editor artifacts**

```bash
git status   # confirm ONLY the files below are modified
git add "Assets/Scripts/InventorySystem/Data/Distributions/Currency Drop Table.asset" \
        "Assets/Scripts/InventorySystem/Data/Distributions/Currency Drop Table.asset.meta" \
        "Assets/Scripts/InventorySystem/Data/Distributions/Currency Type Distribution.asset" \
        Assets/Scenes/Example.unity Assets/Scenes/HUD.unity
git commit -m "chore: create the Currency Drop Table asset and wire it into both scenes

Currency Drop Table.asset with the Phase 2 ranges, assigned to ItemProvider
in Example and HUD. Also commits the rebaked probability caches on
Currency Type Distribution from the reweight.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Done when

- A currency drop is one roll → a pile: type from `Currency Type Distribution`, size from `Currency Drop Table`.
- `grep -rn "GenerateRandomLoot" --include=*.cs Assets/Scripts` shows every caller handling `List<Package>`.
- `grep -rn "SetItemToIron\|SetItemToGold\|CalculateGoldValue\|copperToIron" --include=*.cs Assets/Scripts` returns nothing.
- All EditMode tests pass (6 new `CurrencyDropRollTests` + the existing `CurrencyTests` and `Geometry` tests), console clean.
- In play: slider = 10 on the currency button yields ~10 piles worth low-thousands of base units, and `Consolidate` still preserves the total.

## Not in this plan

- Consumable drop stacks (the pipeline now supports it; consumable generation stays amount 1).
- The equipment-vs-currency income measurement — blocked on `CalculateValue` `goldRatio` calibration. See `dev/specs/2026-08-31-item-value-open-questions.md`.
- Phase 3 (auto-consolidating stash, currency sinks, money-changer NPC).
- `AbstractProbabilityDistribution.Probabilities` per-roll allocation — its own commit, tracked in `dev/specs/2026-08-30-probability-distribution-rebuild-design.md`.
