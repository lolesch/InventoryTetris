# Foundational Rework — Phase 0 Implementation Plan

> **For agentic workers:** execute this plan inline, task-by-task, in the current
> session — never by dispatching subagents (project rule). Steps use checkbox
> (`- [ ]`) syntax for tracking. This is issues #3 and #4 of the foundational-rework
> epic (#2); later phases are GitHub Issues, not plan documents.

**Goal:** Make the InventorySystem value types reachable from EditMode test
assemblies, and settle — by spike — how the container core (`AbstractDimensionalContainer`,
`CharacterInventory`, `CharacterEquipment`, `Package`) will be reached from tests,
with zero behaviour change.

**Architecture:** Two independent deliverables. (1) A mechanical carve-out: the ten
`InventorySystem` enums and `CharacterStatModifier` move into the existing
`InventorySystem.Data` assembly, namespaces unchanged, `.meta` GUIDs preserved —
exactly the move the MutableFloat port made for `StatModifier`. (2) A timeboxed spike
that determines whether Unity Test Framework can test the predefined `Assembly-CSharp`
directly; its outcome is recorded in this document and decides whether Phase 0b (the
container-core extraction) is needed as a follow-up plan.

**Tech Stack:** Unity `6000.3.9f1`; `com.unity.test-framework` 1.4.x/1.6 (NUnit,
`[TestFixture] sealed`, `Assert.That`); Unity assembly definitions
(`.asmdef`, `autoReferenced: true`); git for file moves (`git mv` to keep history and
carry `.meta` alongside `.cs`).

## Global Constraints

- **Compile verification is via the unity-mcp bridge, never `dotnet build`.** Trigger
  `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()` (fully
  qualified) through the bridge, then read the Editor console. The project's `.csproj`
  files are stale and `dotnet build` reports false results here.
- **Green gate** = compiles with **zero** console errors **and** a human runs
  `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` and it passes. The bridge
  cannot run the Test Runner; ask the user to run it at each gate.
- **Zero behaviour change in Phase 0.** No method body changes, no signature changes,
  no new runtime code paths. Only file locations, `.meta` GUIDs, `.asmdef` contents,
  and new test-only files.
- **Namespaces do not change.** Every moved file keeps
  `ToolSmiths.InventorySystem.Data` / `ToolSmiths.InventorySystem.Data.Enums`, so no
  `using` in any call site changes.
- **`.meta` files move with their `.cs` files in the same commit.** GUIDs live inside
  the `.meta`; moving them together preserves every serialized reference in scenes,
  prefabs and assets. Regenerating a `.meta` would break those references.
- **Commit style:** `type: subject` (`feat:` / `fix:` / `refactor:` / `chore:` /
  `test:` / `docs:`), body explains *why*. End every commit message with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  ```
- **Branch:** `feature/foundational-rework` (already cut from `main` @ `44b61a0`,
  which carries the probability-distribution rebuild and the item-movement spec).

---

## File Structure

### Task 1 — the value-type carve-out

| Path | Responsibility | Change |
| --- | --- | --- |
| `Assets/Scripts/InventorySystem/Data/Statistics/Enums/` | new folder, inside the `InventorySystem.Data` asmdef tree | **create** |
| `…/Data/Statistics/Enums/StatName.cs` (+`.meta`) | stat identifiers | **move** from `…/Data/Enums/` |
| `…/Data/Statistics/Enums/StatModifierType.cs` (+`.meta`) | modifier apply-order | **move** from `…/Data/Statistics/` (already in the asmdef; relocated for tidiness) |
| `…/Data/Statistics/Enums/ItemRarity.cs` (+`.meta`) | rarity ladder | **move** from `…/Data/Enums/` |
| `…/Data/Statistics/Enums/ItemCategory.cs` (+`.meta`) | Equipment/Consumable/Currency | **move** |
| `…/Data/Statistics/Enums/ItemSize.cs` (+`.meta`) | grid footprint | **move** |
| `…/Data/Statistics/Enums/EquipmentType.cs` (+`.meta`) | slot + `EquipmentCategory` + `WeaponCategory` (all three enums live in this one file); `using UnityEngine;` for `[Tooltip]` | **move** |
| `…/Data/Statistics/Enums/EquipmentCategory.cs` (+`.meta`) | empty namespace stub (`namespace … { }`) | **move** (or delete — see step 2 note) |
| `…/Data/Statistics/Enums/ConsumableType.cs` (+`.meta`) | consumable kinds | **move** |
| `…/Data/Statistics/Enums/CurrencyType.cs` (+`.meta`) | coin denominations | **move** |
| `…/Data/Statistics/Enums/DamageType.cs` (+`.meta`) | physical/magical | **move** |
| `…/Data/Statistics/CharacterStatModifier.cs` (+`.meta`) | `(StatName, StatModifier)` pair — an affix | **move** from `…/Data/Structs/` to sit beside `StatModifier.cs` |
| `…/Data/Statistics/InventorySystem.Data.asmdef` | assembly definition | **unchanged** (already `autoReferenced`, already has engine + `Utility` + `NaughtyAttributes.Core` refs — everything the moved files need) |
| `…/Data/Enums/` | old folder | **delete** once empty (its `.meta` too) |
| `…/Data/Structs/` | keeps `Package.cs`, `EquipmentSlot.cs` (both depend on `Assembly-CSharp` types — they do **not** move) | unchanged |

**Not moved, and why:** `Package.cs` references
`ToolSmiths.InventorySystem.Inventories.AbstractDimensionalContainer` and
`ToolSmiths.InventorySystem.Items.AbstractItem`; `EquipmentSlot.cs` references the
`EquipmentSlotDisplay` MonoBehaviour. Both are `Assembly-CSharp`-only today. They are
Phase 0b / Phase 1 concerns.

### Task 2 — the container test-seam spike

| Path | Responsibility | Change |
| --- | --- | --- |
| `Assets/Scripts/Tests/EditMode/Inventories/` | throwaway spike test location | **create** (form depends on which spike option wins) |
| `Assets/Scripts/Tests/EditMode/Inventories/ContainerSeamSpike.cs` | one `[Test]` that news up a `CharacterInventory` and asserts on `StoredPackages` | **create** (deleted or promoted after the gate) |
| `dev/plans/2026-08-31-foundational-rework-phase-0.md` | this file — the **Spike Log** section below | **edit** — record the outcome |

---

## Task 1: The value-type carve-out

**Files:** as the table above. Ten enum files + `CharacterStatModifier.cs`, each with
its `.meta`, moving into the `InventorySystem.Data` asmdef tree.

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: the types `ToolSmiths.InventorySystem.Data.Enums.{StatName, StatModifierType,
  ItemRarity, ItemCategory, ItemSize, EquipmentType, EquipmentCategory, WeaponCategory,
  ConsumableType, CurrencyType, DamageType}` and
  `ToolSmiths.InventorySystem.Data.CharacterStatModifier` compiled into assembly
  `InventorySystem.Data` (was `Assembly-CSharp` for the enums and `CharacterStatModifier`;
  `StatModifierType` was already in `InventorySystem.Data`). No type name, member, or
  namespace changes — only the containing assembly.

- [ ] **Step 1: Confirm the pre-move baseline is green**

Ask the user to run `Test Runner ▸ EditMode ▸ Run All` and report the count. Record it
here. Expected from memory: 77/77 passing across `InventorySystem.Data.Tests`,
`InventorySystem.Geometry.Tests`, `InventorySystem.Probability.Tests`.

If any test is already red, stop — fix or note it before moving files, so a regression
introduced by the move is unambiguous.

- [ ] **Step 2: Create the destination folder and move the enum files**

```bash
cd "C:/Users/loles/Desktop/LEONID/InventoryTetris"
mkdir "Assets/Scripts/InventorySystem/Data/Statistics/Enums"

# 8 enum files currently in Data/Enums/ (each has a sibling .meta)
for f in StatName ItemRarity ItemCategory ItemSize EquipmentType EquipmentCategory ConsumableType CurrencyType DamageType; do
  git mv "Assets/Scripts/InventorySystem/Data/Enums/$f.cs"      "Assets/Scripts/InventorySystem/Data/Statistics/Enums/$f.cs"
  git mv "Assets/Scripts/InventorySystem/Data/Enums/$f.cs.meta" "Assets/Scripts/InventorySystem/Data/Statistics/Enums/$f.cs.meta"
done

# StatModifierType is already in the asmdef (Data/Statistics/) — relocate into Enums/ for tidiness
git mv "Assets/Scripts/InventorySystem/Data/Statistics/StatModifierType.cs"      "Assets/Scripts/InventorySystem/Data/Statistics/Enums/StatModifierType.cs"
git mv "Assets/Scripts/InventorySystem/Data/Statistics/StatModifierType.cs.meta" "Assets/Scripts/InventorySystem/Data/Statistics/Enums/StatModifierType.cs.meta"
```

Note on `EquipmentCategory.cs`: it currently contains only
`namespace ToolSmiths.InventorySystem.Data.Enums { }` — the real `EquipmentCategory`
and `WeaponCategory` enums are declared inside `EquipmentType.cs`. Move the stub as-is
(above). Deleting it is a valid alternative but is a content change, not a move — keep
this commit purely mechanical and leave the stub for a later cleanup.

- [ ] **Step 3: Move `CharacterStatModifier.cs` next to `StatModifier.cs`**

```bash
git mv "Assets/Scripts/InventorySystem/Data/Structs/CharacterStatModifier.cs"      "Assets/Scripts/InventorySystem/Data/Statistics/CharacterStatModifier.cs"
git mv "Assets/Scripts/InventorySystem/Data/Structs/CharacterStatModifier.cs.meta" "Assets/Scripts/InventorySystem/Data/Statistics/CharacterStatModifier.cs.meta"
```

`CharacterStatModifier` is `struct CharacterStatModifier : IComparable<CharacterStatModifier>`
in namespace `ToolSmiths.InventorySystem.Data`; it uses `StatName` (moving into this
asmdef in step 2), `StatModifier` (already here), and `UnityEngine` (`[Tooltip]`,
`SerializeField` — the asmdef references `UnityEngine`). Nothing else. It compiles
here.

- [ ] **Step 4: Delete the now-empty `Data/Enums/` folder**

```bash
git rm "Assets/Scripts/InventorySystem/Data/Enums.meta"
rmdir "Assets/Scripts/InventorySystem/Data/Enums"
```

If `git rm` on the folder `.meta` reports the folder is not empty, list it — a file
was missed in step 2.

- [ ] **Step 5: Trigger a recompile through the unity-mcp bridge and read the console**

Through the bridge: call
`UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()`, wait for the
reload, then fetch console logs.

Expected: **zero errors, zero warnings** introduced by this change.

The one plausible failure is a file that turns out to have an `Assembly-CSharp`-only
dependency and cannot compile inside `InventorySystem.Data`. All ten enum files and
`CharacterStatModifier.cs` were read during planning and none do — but if the console
shows `CS0246`/`CS0234` naming a moved file, that file's move must be reverted and the
dependency noted in the Spike Log as new information for Phase 0b.

- [ ] **Step 6: Ask the user to run the full EditMode suite**

Run: `Test Runner ▸ EditMode ▸ Run All`
Expected: the same count as Step 1, all green. `InventorySystem.Data.Tests`,
`InventorySystem.Geometry.Tests`, `InventorySystem.Probability.Tests` unchanged.

No test references the moved enums today (`MutableFloatTests` uses `StatModifierType`,
which was already in this assembly; `Currency` does not use `CurrencyType`), so the
counts should not move. A drop means a `.meta` GUID was regenerated — check
`git status` for an untracked `.cs.meta` alongside a deleted one.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor: move the InventorySystem enums into InventorySystem.Data

Phase 0 of the foundational rework (dev/specs/2026-08-31-foundational-rework-design.md).
The item model being extracted in Phase 1 is built on these enums and on
CharacterStatModifier (affixes are CharacterStatModifier, rarity is ItemRarity,
footprint is ItemSize), and an asmdef test assembly cannot reference the predefined
Assembly-CSharp where they lived. Same move the MutableFloat port made for
StatModifier: file locations and .meta GUIDs travel, namespaces
(ToolSmiths.InventorySystem.Data / .Data.Enums) are unchanged, so no call site moves.
StatModifierType was already in the assembly and is relocated into Enums/ alongside
the rest. Package.cs and EquipmentSlot.cs stay in Assembly-CSharp - they depend on
container and display types and belong to a later phase.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: The container test-seam spike

**Files:** `Assets/Scripts/Tests/EditMode/Inventories/ContainerSeamSpike.cs` (created,
form depends on the winning option); the **Spike Log** section of this document
(edited).

**Interfaces:**
- Consumes: `CharacterInventory`, `Package` and (for a test item) a hand-rolled
  `AbstractItem` subclass — all from `Assembly-CSharp` today. `AbstractItem` is
  `abstract` with only field initializers in its base (`Icon = null`,
  `Dimensions = ItemSize.OneByOne`, `StackLimit = 1u`, `Rarity = ItemRarity.Common`,
  `Affixes = new()`); a test subclass with an empty constructor and a trivial
  `ToString()` override needs **no** singleton, so a real assertion is possible once
  the assembly is reachable.
- Produces: a recorded decision — **"predefined-assembly testing works"** or
  **"extraction needed"** — plus, if an option works, a green throwaway `[Test]`.

**Timebox: 4 hours.** If none of the options below produce a green test in the Test
Runner within the timebox, stop and record "extraction needed"; do not keep digging.

- [ ] **Step 1: Write the spike test (assembly-agnostic content)**

Create `Assets/Scripts/Tests/EditMode/Inventories/ContainerSeamSpike.cs`:

```csharp
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Inventories
{
    /// <summary>
    /// Phase 0 spike: proves the container core is reachable from an EditMode test
    /// assembly. Delete or promote to a real fixture once Phase 0b picks the seam.
    /// </summary>
    [TestFixture]
    public sealed class ContainerSeamSpike
    {
        private sealed class FakeItem : AbstractItem
        {
            // AbstractItem has only field initializers in its base (no singleton in
            // any constructor path); StackLimit / Dimensions / Rarity have protected
            // setters reachable from a subclass, so a test item needs nothing else.
            public FakeItem(uint stackLimit = 1u) => StackLimit = stackLimit;
            public override string ToString() => "fake";
        }

        [Test]
        public void CharacterInventory_AfterAddingAPackage_HoldsExactlyThatItem()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4));
            var package = new Package(inventory, new FakeItem(stackLimit: 1u), 1u);

            var accepted = inventory.TryAddToContainer(ref package);

            Assert.That(accepted, Is.True);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
        }
    }
}
```

If the reflection hack proves awkward, an even simpler assertion that avoids
`AbstractItem` entirely is acceptable for the spike — the point is only to prove the
`CharacterInventory` type links from a test assembly:

```csharp
[Test]
public void CharacterInventory_NewlyConstructed_IsEmptyWithTheGivenCapacity()
{
    var inventory = new CharacterInventory(new Vector2Int(4, 4));
    Assert.That(inventory.StoredPackages, Is.Empty);
    Assert.That(inventory.Capacity, Is.EqualTo(16));
}
```

- [ ] **Step 2: Option A — an `Editor/` folder with no asmdef**

Move the spike file to `Assets/Scripts/Tests/EditMode/Inventories/Editor/` (a folder
literally named `Editor`). Scripts under an `Editor` folder with no governing `.asmdef`
compile into the predefined `Assembly-CSharp-Editor`, which auto-references
`Assembly-CSharp`.

Trigger a recompile via the bridge; ask the user to open the Test Runner and check
whether `ContainerSeamSpike` appears under EditMode and passes.

Record: does it compile? does the Test Runner discover it? does it pass?

- [ ] **Step 3: Option B — a test asmdef that names `Assembly-CSharp`**

Create `Assets/Scripts/Tests/EditMode/Inventories/InventorySystem.Inventories.Tests.asmdef`:

```json
{
    "name": "InventorySystem.Inventories.Tests",
    "references": ["Assembly-CSharp", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": true
}
```

Unity's asmdef inspector does not offer `Assembly-CSharp` in its reference picker, but
the name can be typed straight into the JSON. This is expected to be rejected by the
compiler ("Assembly with name 'Assembly-CSharp' is not allowed to be referenced from
an assembly definition") — try it anyway, it is a two-minute check, and record the
exact message.

- [ ] **Step 4: Option C — the Test Framework predefined-assembly setting**

In `Edit ▸ Project Settings ▸ Test Framework` (and the Test Runner window's settings),
look for a "predefined assemblies" / "all assemblies" toggle for EditMode. If present,
enable it, put the spike file back under a plain (non-`Editor`) folder with no asmdef,
recompile, and check the Test Runner.

Record whether the setting exists in this Unity/UTF version and whether it surfaces the
test.

- [ ] **Step 5: Record the outcome in the Spike Log and commit whatever landed**

Fill in the **Spike Log** section below with: which option was tried, what happened,
and the decision (`predefined-assembly testing works` → Phase 0 finishes at Task 3a;
`extraction needed` → Phase 0b gets its own plan).

```bash
git add -A
git commit -m "$(cat <<'EOF'
chore: container test-seam spike - <one-line outcome>

Phase 0 of the foundational rework. Spike per
dev/specs/2026-08-30-item-movement-model-design.md Phase 0: <which options tried,
what won or why all failed>. Decision recorded in
dev/plans/2026-08-31-foundational-rework-phase-0.md.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3a: Close out Phase 0 — *only if the spike found a working seam*

- [ ] **Step 1: Give the spike test a permanent home**

If Option A won: keep the `Editor/`-folder layout, rename the file to
`ContainerCoreTests.cs`, drop the "spike" language, keep the two assertions as the
first real container tests.

If Option C won: add the minimal test asmdef the setting requires and move the file
under it.

- [ ] **Step 2: Recompile via the bridge, ask the user to `Run All`**

Expected: `Data.Tests` + `Geometry.Tests` + `Probability.Tests` at their Step-1 count,
**plus** the new container test(s), all green.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
test: container core is reachable from EditMode tests

Phase 0 green gate. <Option A/C> makes AbstractDimensionalContainer /
CharacterInventory / CharacterEquipment / Package testable with no extraction and no
behaviour change. Phase 1 (the item-model split) builds on this seam.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: Hand back**

Phase 0 is done. Phase 1 (`ItemDefinition` / `ItemInstance` / `ItemGenerator` /
catalog) gets its own plan, drawn from
`dev/specs/2026-08-31-foundational-rework-design.md` §"Phase 1", once a human confirms
the gate.

---

## Task 3b: *If the spike found no working seam* — stop and re-plan

Phase 0b (the container-core extraction) is **not planned in this document**. It is a
refactor of the most central class in the system and its true size depends on facts
the spike surfaces (whether `Assembly-CSharp-Editor` tests are discoverable at all,
whether `ItemTypeData` and `StatRange` move cleanly, how the `ItemProvider` singleton
call in the `AbstractItem` subtype constructors is best broken). Writing it before the
spike would be writing against an unknown — which the item-movement spec explicitly
forbids ("Phases 1–3 are written against whichever seam won").

Draw Phase 0b from **Appendix B** below + the recorded Spike Log, as a fresh plan
`dev/plans/<date>-foundational-rework-phase-0b.md`.

---

## Self-Review

- **Spec coverage — foundational-rework spec §"Phase 0":**
  - "shared value-type carve-out … `Data/Enums/*`, `Data/Structs/CharacterStatModifier.cs`
    … namespaces unchanged … as the MutableFloat port moved `StatModifier`" → **Task 1**.
  - "container test seam … timeboxed spike (half a day) with a committed fallback …
    Record the outcome in the Phase 0 plan section" → **Task 2** + the Spike Log +
    Task 3b's re-plan pointer.
  - Phase 0 gate: "`Data.Tests`, `Geometry.Tests`, `Probability.Tests` still green;
    **zero behaviour change**" → Task 1 steps 1/6, Task 3a step 2; the "Global
    Constraints" zero-behaviour-change rule.
  - "container core and item types reachable from an EditMode test assembly" → the
    spike's success criterion (Task 2 step 1's `FakeItem` shows item types are
    reachable once the assembly links); full reachability of the *generated* item
    types is Phase 1, not Phase 0.
- **Placeholder scan:** Task 2's `<one-line outcome>` in the commit message and the
  Spike Log's blanks are deliberately unfilled — they are recorded *during* execution,
  which is the task. No code step is left abstract.
- **Type consistency:** `CharacterInventory(Vector2Int)`, `Package(AbstractDimensionalContainer, AbstractItem, uint)`,
  `AbstractDimensionalContainer.TryAddToContainer(ref Package) : bool`,
  `.StoredPackages : Dictionary<Vector2Int, Package>`, `.Capacity : int`,
  `AbstractItem.StackLimit` (protected setter), `AbstractItem.ToString()` (abstract) —
  all verified against the source during planning.

---

## Spike Log

Run 2026-09-01 (issue #4). Verification: **Unity 6000.3.9f1 batch-mode EditMode run**
(`Unity.exe -runTests -batchmode -testPlatform EditMode`) — the unity-mcp bridge was
not connected this session, so the Editor-closed batch path was used. UTF is **1.6.0**.
Spike test: `Assets/Scripts/Tests/EditMode/Inventories/Editor/ContainerSeamSpike.cs` —
two `[Test]`s that `new CharacterInventory(new Vector2Int(4,4))`, one adding a
`Package` built with a test-local `FakeItem : AbstractItem` (empty ctor, `protected`
`StackLimit` setter — no singleton on `AbstractItem`'s own construction path) and
asserting `StoredPackages` holds exactly it.

**Baseline:** EditMode Run All = **77 / 77** passing (`InventorySystem.Data.Tests` 28,
`.Geometry.Tests` 15, `.Probability.Tests` 34).

**Option A — `Editor/` folder, no asmdef → `Assembly-CSharp-Editor`:** ✅ **works.**
Compiles (0 errors, 0 new warnings). The `<test-run>` went to **79 / 79** — both spike
cases discovered under `Assembly-CSharp-Editor.dll` and green; the three existing
assemblies unchanged at 28 / 15 / 34. Negative control passed: flipping the count
assertion to `EqualTo(7)` produced exactly one failure at the edited line
(`Expected: property Count equal to 7 / But was: 1`), proving the batch run recompiles
the file and the test exercises the real `TryAddToContainer` path, not a stale
assembly. `Assembly-CSharp-Editor` auto-references `Assembly-CSharp` and, with
`UNITY_INCLUDE_TESTS` defined, carries the nunit + TestRunner references UTF's
discovery needs.

**Option B — asmdef naming `Assembly-CSharp`:** not tried — Option A (cheaper) already
gave a green test, which is the spike's stop condition. (Known: the compiler rejects
`Assembly-CSharp` as an asmdef reference — "Assembly with name 'Assembly-CSharp' is
not allowed to be referenced from an assembly definition file".)

**Option C — Test Framework predefined-assembly setting:** not tried — not needed.
Option A surfaces the test without any project-settings change.

**Decision: `predefined-assembly testing works` → Phase 0 closes at Task 3a.** The
container core (`AbstractDimensionalContainer` / `CharacterInventory` /
`CharacterEquipment` / `Package` / `AbstractItem`) is reachable from an EditMode test
with **zero extraction and zero behaviour change**. Issue #4's five acceptance criteria
are all met by the Editor-folder seam.

**Known limitation of this seam (the case for the extraction as a follow-up):** an
`Assembly-CSharp-Editor` test can only reach container behaviour that does *not* touch
the `CharacterProvider` / `DragProvider` / `ItemProvider` singletons — so
`CharacterInventory` add / stack / sort are coverable, but `CharacterEquipment` swap
(where QA-4 lives, `CharacterEquipment.cs:91,118`) is not, and there is no per-module
`InventorySystem.Containers.Tests` asmdef. ADR-0007 still wants
`InventorySystem.Containers` extracted, and Phase 2's `ItemTransaction` is specified to
live in it. That extraction is a **separate ticket** per issue #4 ("Don't fold the
extraction into this ticket") and per Task 3b below — filed as **issue #15**
(`blocked-by #4`, `blocking #9`), drawn from Appendix B. It was ADR-0007's assumption
that this spike would *fail* and force the extraction now; it did not.

---

## Appendix A — the ten enum files (verified during planning)

All in namespace `ToolSmiths.InventorySystem.Data.Enums`. Only `EquipmentType.cs`
carries a `using` (`UnityEngine`, for `[Tooltip]`); the rest are dependency-free.

| File | Declares | Notes |
| --- | --- | --- |
| `StatName.cs` | `StatName` | `[System.Serializable]`, explicit values incl. `Experience = -1` |
| `StatModifierType.cs` | `StatModifierType` | already in `InventorySystem.Data`; values are apply-order (`Overwrite=0, FlatAdd=100, …`) |
| `ItemRarity.cs` | `ItemRarity` | `NoDrop=0, Common=5, Magic=15, Rare=20, Unique=30` |
| `ItemCategory.cs` | `ItemCategory` | `NONE, Consumable, Equipment, Currency` |
| `ItemSize.cs` | `ItemSize` | footprint codes `OneByOne=11 … TwoByFour=24` |
| `EquipmentType.cs` | `EquipmentType`, **`EquipmentCategory`**, **`WeaponCategory`** | three enums, one file; `using UnityEngine;` |
| `EquipmentCategory.cs` | — | empty namespace stub; real enum is in `EquipmentType.cs` |
| `ConsumableType.cs` | `ConsumableType` | `NONE, Arrow, Book, Potion` |
| `CurrencyType.cs` | `CurrencyType` | `NONE, Copper, Iron, Silver, Gold` |
| `DamageType.cs` | `DamageType` | `PhysicalDamage, MagicalDamage` (no explicit values) |

`InventorySystem.Data.asmdef` needs **no reference changes** — it already has engine
references (`noEngineReferences: false`), `Utility`, and `NaughtyAttributes.Core`.
No other asmdef in the project (`Geometry`, `Probability`, the three `.Tests`) touches
these enums, and `Assembly-CSharp` auto-references `InventorySystem.Data`, so every
existing call site keeps compiling untouched.

## Appendix B — the container→`Assembly-CSharp` coupling map (for Phase 0b)

If extraction is needed, an `InventorySystem.Containers` assembly (referencing
`InventorySystem.Data`) would take `AbstractDimensionalContainer`, `CharacterInventory`,
`CharacterEquipment`, `Package`, and — because the containers are typed on them —
`AbstractItem` + `EquipmentItem` + `ConsumableItem` + `CurrencyItem`. The couplings to
break, all currently via `.Instance` singletons:

| Site | Call | Break with |
| --- | --- | --- |
| `AbstractDimensionalContainer.RemoveAtPosition` (`:148`) | `CharacterProvider.Instance.Player.RemoveItemStats(storedPackage.Item.Affixes)` — guarded by `this is CharacterEquipment` | an `IStatReceiver` interface (defined in the new assembly) or the existing `OnContentChanged` event pattern; injected once at startup |
| `CharacterEquipment.AddAtPosition/TryAddToInventory` (`:91`) | `CharacterProvider.Instance.Player.AddItemStats(package.Item.Affixes)` | same `IStatReceiver` |
| `CharacterEquipment.TrySwap` (`:118`) | `DragProvider.Instance.ReplacePackage(previouslyEquipped[i])` | an `ICursorSink` interface, injected |
| `CharacterInventory.AddChange` (`:170`) | `ItemProvider.Instance.GenerateCurrency(type)` | an `ICurrencyMinter` / `IItemContentProvider` interface, injected |
| `AbstractItem` subtype constructors (`AbstractItem.cs:133,140,168,237,246,300`) | `ItemProvider.Instance.{GetIcon, GetUnique, ItemTypeData.GetPossibleStats}` | `IItemContentProvider` static locator set by `ItemProvider.Awake()` — **this is Phase 1's core work pulled forward**; sizing it is the reason Phase 0b is planned only after the spike |
| `CharacterProvider` / `DragProvider` / `ItemProvider` | are `AbstractProvider<T>` MonoBehaviours; `LocalPlayer.AddItemStats(List<CharacterStatModifier>)` / `RemoveItemStats(...)` at `LocalPlayer.cs:91,116`; `DragProvider.ReplacePackage(Package)` at `:174` | implement the new interfaces in `Assembly-CSharp`, wire in each provider's `Awake` |

Injection point for the container-held interfaces: `InventoryProvider.Awake()` news up
the four containers today — it is the natural place to pass them their `IStatReceiver` /
`ICursorSink`.

`ItemTypeData` (`Data/ItemTypeData.cs`, a `ScriptableObject` in namespace
`ToolSmiths.InventorySystem.Data`) and its nested `StatRange.GetRandomRoll(ItemRarity)`
are used by the `AbstractItem` constructors; whether they move into the new assembly or
sit behind `IItemContentProvider` is a Phase 0b/Phase 1 decision, not settled here.
