# Drag Cursor Anchoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task, with a review checkpoint after each task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The item you are dragging goes where it looks like it goes. The grip you take at pointer-down is the grip you keep — through a rejected drop, through a swap, through the drag threshold.

**Architecture:** The screen↔grid placement maths moves into a new dependency-free `InventorySystem.Geometry` assembly as two pure functions (`GrabPivot`, `DropPosition`) that an EditMode test project can reach, mirroring the `Currency.TryGetPayment` move. `DragProvider` becomes the single owner of "where would this drop", so the red overlap tint and every `DropItem` read the same answer off the drag display's real rect instead of each deriving it separately. `DropItem` stops re-anchoring the drag display on every call: it re-anchors only when the package in hand actually changes.

**Tech Stack:** Unity 6000.3.9f1, C# (Roslyn), Unity Test Framework (NUnit) EditMode tests, `.asmdef` assembly definitions.

## Global Constraints

- **Base branch:** `fix/drag-cursor-anchoring`, sitting directly on `6653484`, the pushed tip of `feature/mutablefloat-port`. Keep that base — `main` is pre-Unity-6000.3 and pre-port. (`feature/shop-currency` was merged into `feature/mutablefloat-port` and deleted; it is not a base for anything.)
- **Sibling branch:** `feature/currency-redesign` is unrelated work off the same `6653484`. It was briefly branched off this plan's Task 1 commit and was rebased back onto `feature/mutablefloat-port` on 2026-08-30. Do not commit drag work there, and do not commit the `Currency Type Distribution.asset` edit here — it is currency WIP riding along in the working tree.
- **Behaviour target:** the drag display keeps following the cursor freely (no grid snapping mid-drag). The *drop* is what changes to agree with the visual, never the other way round.
- **Swap policy (decided):** a package handed over mid-drag is **centred on the cursor** (pivot `(0.5, 0.5)`). Never inherit the previous item's `positionOffset`.
- **Cell size:** `60` is currently hardcoded in `DragProvider` and read from `GridLayoutGroup.cellSize` in `InventorySlotDisplay`. This plan replaces the literal with one serialized field; unifying it with the layout group is out of scope (see Follow-ups).
- **Geometry stays engine-light:** `DragGeometry` may use `Vector2`, `Vector2Int`, `Mathf` and nothing else. No `Input`, no `Transform`, no project types. That is what makes it testable.
- **Commits:** small and frequent, one per task. Repo message style: `feat:` / `fix:` / `refactor:` / `chore:` prefix, body explains *why*. End every commit message with:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  ```

## Green

**Green** = the project compiles with zero console errors *and* `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` passes (`InventorySystem.Data.Tests`, `InventorySystem.Geometry.Tests`). A compile error anywhere blocks the Test Runner entirely, so every commit point below lands on green.

Batch-mode test run (needs the Editor **closed** — it holds a single-instance project lock):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" -runTests -batchmode -projectPath "C:/Users/loles/Desktop/LEONID/InventoryTetris" -testPlatform EditMode -testResults "C:/Users/loles/AppData/Local/Temp/claude/results.xml" -logFile -
```

Exit code `0` = all passed, `2` = failures.

### The diagnosis harness

`Assets/Scripts/_DragDropHarness.cs` (untracked, `#if UNITY_EDITOR`, tagged `[DEBUG-d1a9]`) drives the real `DragProvider.SetPackage` against real slot `RectTransform`s and prints where the drag display lands versus where the drop rule puts it. Run it through the Unity MCP bridge:

```csharp
result.Log(ToolSmiths.InventorySystem.Debugging.DragDropHarness.Run());
```

It is the end-to-end signal while Tasks 2–5 land, because the EditMode tests only cover the extracted maths, not the `RectTransform` wiring. **Task 6 deletes it.** Keep it updated as the API changes.

---

## The bugs

Measured output from the harness on the current code (`cell = 60`, item grabbed 5 px from a slot's left edge and 55 px below its top, then hovering a slot with the cursor 55 px right / 5 px below *its* top-left):

| # | Symptom | Root cause | Fixed by |
| --- | --- | --- | --- |
| 1 | 1×1 drops at `(4,4)` covering **2.8 %** of the item while it visually sits at `(5,3)` covering **69.4 %**. A 2×3 drops at `(3,2)` (42.1 %) instead of `(4,1)` (86.6 %). | `InventorySlotDisplay.DropItem`: `positionToAdd = Position - positionOffset` uses only the whole-cell offset captured at pickup and discards the sub-cell fraction that the *visual* honours. | Task 3 |
| 2 | A rejected drop jumps the item `(-50, -50)` px and lands 100 % grid-perfect on the hovered slot. | `DropItem` calls `SetPackage` unconditionally, even when `AddAtPosition` placed nothing; `SetPackage` recomputes the pivot relative to the hovered slot, which by construction grid-aligns it. | Task 4 |
| 3 | A 1×1 displaced by a 2×3 gets pivot `(1.917, -1.083)` — the cursor ends up 55×65 px **outside** the item. | The displaced package inherits the dragged item's `positionOffset`, which is meaningless for a different footprint. | Task 5 |
| 4 | Grabbing at the frame and dragging outward gives pivot `(-0.133, 0.5)` — the cursor is outside the item before it starts following. | No `IPointerDownHandler` on the slots. `MoveItem` runs from `OnBeginDrag` and reads `Input.mousePosition` *after* Unity's 10 px threshold (`m_DragThreshold: 10` in both scenes). | Task 2 |

**Third copy of the maths:** `DragProvider.HighlightOverlappingSlots` derives the target cell from `itemDisplay.pivot` with its own formula. Traced against both scenarios it returns `(4,4)` and `(3,2)` — identical to the drop rule, so the red "can't drop" tint agrees with the drop and disagrees with the visual today. Fixing only one of them makes the tint lie. Task 3 unifies them.

**Validated fix formula:** rounding the drag display's real top-left corner to the nearest cell returns `(5,3)` and `(4,1)` — agreeing with the visual in both cases. Confirmed by the harness before any production code was written.

---

## File Structure

| File | Responsibility | Change |
| --- | --- | --- |
| `Assets/Scripts/InventorySystem/Geometry/InventorySystem.Geometry.asmdef` | Engine-light assembly the test project can reference | **Create** |
| `Assets/Scripts/InventorySystem/Geometry/DragGeometry.cs` | The pure screen↔grid placement maths | **Create** |
| `Assets/Scripts/Tests/EditMode/Geometry/InventorySystem.Geometry.Tests.asmdef` | EditMode test assembly | **Create** |
| `Assets/Scripts/Tests/EditMode/Geometry/DragGeometryTests.cs` | Locks in `GrabPivot` / `DropPosition` and their round-trip | **Create** |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs` | Base slot behaviour | Implement `IPointerDownHandler`; thread the pressed pointer position into `MoveItem` / `DropItem` |
| `Assets/Scripts/InventorySystem/Runtime/Provider/DragProvider.cs` | Owns the drag display and the one drop rule | `SetPackage` takes a pointer position and calls `DragGeometry.GrabPivot`; add `TryGetDropPosition`, `ReplacePackage`; `HighlightOverlappingSlots` uses `TryGetDropPosition` |
| `Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs` | Container rules | Promote `IsWithinDimensions`; add `CanPlaceAt` |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/InventorySlotDisplay.cs` | Inventory / stash slot | Rewrite `DropItem`; pass the pointer position at pickup |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/EquipmentSlotDisplay.cs` | The 14 equipment slots | Same re-anchor fix; pass the pointer position |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/VendorSlotDisplay.cs` | Store slot | Same re-anchor fix; pass the pointer position |
| `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/SellItenSlotDisplay.cs`, `DropToFloorSlotDisplay.cs` | Sinks that end the drag | Swap `SetPackage(this, new Package(), …)` for `EndDrag()` |
| `Assets/Scripts/InventorySystem/Runtime/Inventories/CharacterEquipment.cs` | Two-hand unequip hands a package back | `SetPackage(Hovered, …)` → `ReplacePackage` |
| `Assets/Scripts/_DragDropHarness.cs` | Diagnosis harness | Keep updated through Task 5, **delete** in Task 6 |

---

## Task 1: `DragGeometry` + EditMode tests

**Files:**
- Create: `Assets/Scripts/InventorySystem/Geometry/InventorySystem.Geometry.asmdef`
- Create: `Assets/Scripts/InventorySystem/Geometry/DragGeometry.cs`
- Create: `Assets/Scripts/Tests/EditMode/Geometry/InventorySystem.Geometry.Tests.asmdef`
- Create: `Assets/Scripts/Tests/EditMode/Geometry/DragGeometryTests.cs`

**Interfaces:**
- Consumes: `UnityEngine.Vector2`, `Vector2Int`, `Mathf` only.
- Produces:
  - `DragGeometry.GrabPivot(Vector2 pointer, Vector2 slotTopLeft, Vector2Int dimensions, Vector2Int positionOffset, float cellSize) → Vector2`
  - `DragGeometry.DropPosition(Vector2 pointer, Vector2 pivot, Vector2Int dimensions, Vector2 hoveredSlotTopLeft, Vector2Int hoveredPosition, float cellSize) → Vector2Int`

Nothing calls this yet — that is the expected intermediate state. `GrabPivot` is the *current* `SetPosition` maths transcribed unchanged, because the pickup is already correct (the harness measures 100 % fit at the item's origin at grab time). `DropPosition` is the new rule.

- [x] **Step 1: Create the runtime assembly**

`Assets/Scripts/InventorySystem/Geometry/InventorySystem.Geometry.asmdef`:

```json
{
    "name": "InventorySystem.Geometry",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`autoReferenced: true` means `Assembly-CSharp` picks it up without further wiring.

- [x] **Step 2: Write `DragGeometry`**

`Assets/Scripts/InventorySystem/Geometry/DragGeometry.cs`:

```csharp
using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// Converts between the cursor, the drag display's rect, and container positions.
    /// Screen space is pixels anchored bottom-left; inventory space is cells anchored
    /// top-left with y growing downward. Every slot's <c>transform.position</c> is its
    /// top-left corner (the slot prefab's pivot is (0, 1)).
    /// </summary>
    public static class DragGeometry
    {
        /// <summary>
        /// The drag display pivot that puts <paramref name="pointer"/> on the exact point of
        /// the item it grabbed, so the item keeps the grip it was picked up by.
        /// </summary>
        /// <param name="slotTopLeft">Screen position of the slot the pointer went down on.</param>
        /// <param name="positionOffset">Cells from the item's origin to that slot, in inventory space.</param>
        public static Vector2 GrabPivot(Vector2 pointer, Vector2 slotTopLeft, Vector2Int dimensions, Vector2Int positionOffset, float cellSize)
        {
            /// pointer relative to the grabbed slot, in cells, anchored top-left
            var pivot = (pointer - slotTopLeft) / cellSize;
            /// convert to screen coordinates, anchored bottom-left
            pivot.y += 1f;
            /// scale to match item dimensions
            pivot /= dimensions;

            /// the offset arrives in inventory space; flip it into screen space the same way
            var offset = new Vector2(positionOffset.x, dimensions.y - 1 - positionOffset.y);
            offset /= dimensions;

            return pivot + offset;
        }

        /// <summary>
        /// The container position the drag display currently covers: its top-left corner
        /// rounded to the nearest cell. Read off the display's real rect rather than off the
        /// pickup offset, so the drop can never disagree with what the player sees.
        /// </summary>
        /// <param name="hoveredSlotTopLeft">Screen position of the slot under the cursor.</param>
        /// <param name="hoveredPosition">That slot's container position.</param>
        public static Vector2Int DropPosition(Vector2 pointer, Vector2 pivot, Vector2Int dimensions, Vector2 hoveredSlotTopLeft, Vector2Int hoveredPosition, float cellSize)
        {
            var size = (Vector2)dimensions * cellSize;
            var itemTopLeft = pointer - Vector2.Scale(pivot, size) + new Vector2(0f, size.y);

            var cells = (itemTopLeft - hoveredSlotTopLeft) / cellSize;

            return hoveredPosition + new Vector2Int(Mathf.RoundToInt(cells.x), Mathf.RoundToInt(-cells.y));
        }

        /// The pivot for a package handed over mid-drag (a swap): centred, so the cursor is
        /// always inside it whatever its footprint.
        public static Vector2 HandOverPivot => new Vector2(.5f, .5f);
    }
}
```

- [x] **Step 3: Create the test assembly**

`Assets/Scripts/Tests/EditMode/Geometry/InventorySystem.Geometry.Tests.asmdef`:

```json
{
    "name": "InventorySystem.Geometry.Tests",
    "rootNamespace": "",
    "references": [
        "InventorySystem.Geometry",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [x] **Step 4: Write the tests**

Landed as `Assets/Scripts/Tests/EditMode/Geometry/DragGeometryTests.cs` — read the file, it is the source of truth. Naming follows `CurrencyTests`: `[TestFixture] public sealed class`, `Subject_Condition_Expectation` method names, `Assert.That(…, Is.EqualTo(…))`.

The headline is the **round trip**, run as eight `[TestCase]`s across 1×1 / 2×3 / 1×4 / 2×4 footprints and grab points from 1 % to 99 % across a cell: grab an item anywhere inside itself, drop it without moving the cursor, and it must land back on its own origin. That invariant is what the shipped code breaks, and it holds for every footprint and every grab point.

The rest pin the specific numbers this bug was measured at:

| Test | Asserts |
| --- | --- |
| `GrabThenDropWithoutMoving_ReturnsTheItemToItsOrigin` | the round trip, ×8 cases |
| `GrabPivot_IsTheExactPointOfTheItemUnderTheCursor` | a bottom-left grab gives pivot `5/60` on both axes |
| `GrabPivot_ShiftsByWholeCells_ForTheCellOfALargeItemThatWasGrabbed` | a 2×3 grabbed on its `(1,2)` cell shifts half a footprint right |
| `DropPosition_FollowsTheItemAcrossACellBoundary` | `(5,3)` — the shipped rule said `(4,4)`, at 3 % overlap |
| `DropPosition_ForALargeItem_FollowsItsBodyNotItsGrip` | `(4,1)` — the shipped rule said `(3,2)`, at 42 % overlap |
| `DropPosition_TracksTheHoveredCell_WhenTheItemWasGrabbedDeadCentre` | a centred grab stays on the hovered cell right across it |
| `DropPosition_IsReadFromAnySlot_NotJustTheOneUnderTheCursor` | the answer describes the grid, so a distant slot gives the same result |
| `HandOverPivot_IsCentred` | `(0.5, 0.5)` |

- [x] **Step 5: Recompile → green**

`Library/ScriptAssemblies/InventorySystem.Geometry.dll` and `InventorySystem.Geometry.Tests.dll` both built; `Unity_GetConsoleLogs` reports 0 errors, 0 warnings.

Every assertion in `DragGeometryTests` was also executed against the real `DragGeometry` through `Unity_RunCommand` (the bridge cannot drive NUnit, but `DragGeometry` is public so its methods can be called directly) — 21/21 pass. The values are discriminating: the shipped rule returns `(4,4)` and `(3,2)` where these return `(5,3)` and `(4,1)`.

- [ ] **Step 5b: Run All → green** ← *needs a human: `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`*

Confirms the NUnit wiring, not the maths. If the round-trip test fails, the transcription of `GrabPivot` is wrong — compare against `DragProvider.SetPosition` line by line before touching `DropPosition`.

- [x] **Step 6: Commit**

```
feat: extract the drag placement maths into a testable assembly
```

---

## Task 2: Anchor the grab at pointer-down

**Files:**
- Modify: `Assets/Scripts/InventorySystem/GUI/InventoryDisplays/AbstractSlotDisplay.cs`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/DragProvider.cs`
- Modify: `InventorySlotDisplay.cs`, `EquipmentSlotDisplay.cs`, `VendorSlotDisplay.cs`, `SellItenSlotDisplay.cs`, `DropToFloorSlotDisplay.cs` (signature follow-through)

**Interfaces:**
- Produces: `DragProvider.SetPackage(AbstractSlotDisplay slot, Package package, Vector2Int positionOffset, Vector2 pointerPosition)`; `AbstractSlotDisplay.MoveItem(PointerEventData eventData, Vector2 pointerPosition)`.

Fixes bug 4. `Position` (the integer cell) already comes from the pressed slot, because Unity routes drag events to the object that received pointer-down — only the *fraction* is sampled 10 px late. This task makes both come from the same instant.

- [x] **Step 1: Record the press on `AbstractSlotDisplay`**

Add `IPointerDownHandler` to the interface list and:

```csharp
/// Where the pointer went down on this slot. OnBeginDrag only fires once Unity's 10px
/// drag threshold is crossed, by which time the cursor can already be outside the item -
/// anchoring to that reading is what made the item jump away from the cursor on grab.
private Vector2 pressPosition;

public void OnPointerDown(PointerEventData eventData) => pressPosition = eventData.position;
```

Then pass it through both entry points:

```csharp
public void OnPointerClick(PointerEventData eventData)
{
    if (DragProvider.Instance.IsDragging)
        DropItem(DragProvider.Instance.DraggingPackage);
    else
        MoveItem(eventData, pressPosition);
}

public void OnBeginDrag(PointerEventData eventData)
{
    if (DragProvider.Instance.IsDragging)
        DropItem(DragProvider.Instance.DraggingPackage);
    else
        MoveItem(eventData, pressPosition);
}
```

and widen the abstract signature:

```csharp
protected abstract void MoveItem(PointerEventData eventData, Vector2 pointerPosition);
```

- [x] **Step 2: Take the pointer position in `DragProvider.SetPackage`**

`Vector2Int * float` does not compile, so the size line landed as `itemDisplay.sizeDelta = (Vector2)dimensions * slotSize;` and the origin argument as `(Vector2)Origin.transform.position / transform.lossyScale` (cast the numerator, same idiom as the existing `SetToMousePosition`).

Replace the `Input.mousePosition` read in `SetPosition` with the passed position and delegate to `DragGeometry`:

```csharp
public void SetPackage(AbstractSlotDisplay slot, Package package, Vector2Int positionOffset, Vector2 pointerPosition)
```

and inside `SetPosition`:

```csharp
var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

itemDisplay.sizeDelta = dimensions * slotSize;
itemDisplay.pivot = DragGeometry.GrabPivot(
    pointerPosition / transform.lossyScale,
    Origin.transform.position / transform.lossyScale,
    dimensions,
    positionOffset,
    slotSize);

SetToMousePosition();
```

Add the field that replaces the `// slotSize` literals:

```csharp
[Tooltip("Pixels per inventory cell. Must match the GridLayoutGroup cellSize the slots are laid out with.")]
[SerializeField] private float slotSize = 60f;
```

- [x] **Step 3: Follow the signature through every override**

`MoveItem(PointerEventData eventData, Vector2 pointerPosition)` in all five subclasses. Every `SetPackage(this, package, offset)` call inside a `MoveItem` gains `, pointerPosition`. The `SetPackage` calls inside `DropItem` are dealt with in Tasks 4 and 5 — for now pass `Input.mousePosition` there to keep it compiling, and expect the harness to still show bugs 1–3.

`CharacterEquipment.TrySwap` (`CharacterEquipment.cs:118`) also calls `SetPackage` and is not in this task's file list — it took `Input.mousePosition` too, to keep it compiling. Task 5 converts it to `ReplacePackage`.

- [x] **Step 4: Recompile → green; run the harness**

Compiles clean (verified through the unity-mcp bridge, with a `DELIBERATE_SENTINEL_ERROR` negative control). Harness: `BUG 4` "anchored at pointer-down (now)" reads `cursor inside item? yes` (was `NO`, pivot `(-0.133, 0.5)` → `(0.033, 0.5)`); bugs 1–3 still red as expected. `EdgeGrab` was rewritten to pass an explicit press position rather than fake the cursor with grid placement.

- [ ] **Step 5: Play-mode check** ← *needs a human*

Press on an item's outer frame and drag outward briskly. The item must keep the grip from where you pressed, not from 10 px later.

- [x] **Step 6: Commit** — `f2b742c`

```
fix: anchor the drag to the pointer-down position, not the drag threshold
```

---

## Task 3: One drop rule, shared by the tint and the drop

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/DragProvider.cs`
- Modify: `InventorySlotDisplay.cs`, ~~`VendorSlotDisplay.cs`~~

Fixes bug 1, and keeps the red overlap tint honest by making it read the same answer.

> **Plan correction (2026-08-30, during execution):** `VendorSlotDisplay.DropItem` is **not** a grid-placement drop — it is a sell sink, byte-for-byte the same shape as `SellItenSlotDisplay.DropItem` (remove from origin → mint currency → hide the drag display). It has no `positionOffset` / `AddAtPosition` to swap, so Task 3 touched only `DragProvider` + `InventorySlotDisplay`. **This also invalidates Task 5 Step 2's instruction to convert `VendorSlotDisplay.DropItem` to `ReplacePackage`** — it should be treated as a sink there too (`SetPackage(this, new Package(), …)` → `EndDrag()`), alongside `SellItenSlotDisplay` and `DropToFloorSlotDisplay`.

- [x] **Step 1: Add `TryGetDropPosition` to `DragProvider`** — landed. `Input.mousePosition` / `hovered.transform.position` are cast `(Vector2)` before the `/ transform.lossyScale` divide, the same idiom Task 2 established for `SetPosition`.

```csharp
/// <summary>
/// The container position the drag display currently covers. The single answer to
/// "where would this drop" - the overlap tint and every DropItem read it, so the
/// warning the player sees and the placement they get can never disagree.
/// </summary>
public bool TryGetDropPosition(AbstractSlotDisplay hovered, out Vector2Int position)
{
    position = default;

    if (hovered == null || hovered.Container == null || !DraggingPackage.IsValid)
        return false;

    position = DragGeometry.DropPosition(
        Input.mousePosition / transform.lossyScale,
        itemDisplay.pivot,
        AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions),
        hovered.transform.position / transform.lossyScale,
        hovered.Position,
        slotSize);

    return true;
}
```

- [x] **Step 2: Point `HighlightOverlappingSlots` at it** — landed. The whole pivot-decoding block and the dead `requiredPositions` / `usedPositions` comment are gone; the two early-return guards collapsed into the single `TryGetDropPosition` check.

Replace the whole pivot-decoding block (the `positionPivot` / `positionDiff` / `positionToAdd` derivation) with:

```csharp
void HighlightOverlappingSlots()
{
    /// Every early return has to clear the tint. Bailing out while a previous
    /// slot's red is still on screen is what made large items look undroppable
    /// over the floor and sell slots, which have no container of their own.
    if (!TryGetDropPosition(Hovered, out var positionToAdd))
    {
        ResetOverlapTint();
        return;
    }

    var storedPositions = Hovered.Container.GetStoredItemsAt(positionToAdd, AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions));

    if (background)
        /// Assigned rather than multiplied so it stays red whatever the scrim is
        /// tinted to; colour multiplication is component-wise and only lands on red
        /// while the scrim happens to be white.
        /// 0 overlaps drops into empty space, 1 swaps with the item already there
        /// (AddAtPosition handles both). Only 2+ cannot be placed at all.
        background.color = 1 < storedPositions.Count
            ? WithAlpha(Color.red, initialColor.a)
            : initialColor;
}
```

Delete the commented-out `requiredPositions` / `usedPositions` block below it while you are in there — it describes an approach this task supersedes.

- [x] **Step 3: Use it in `InventorySlotDisplay.DropItem`** — landed, incl. deleting the `/* TODO: match position offset… */` block. `VendorSlotDisplay.DropItem` untouched (see plan correction above); `EquipmentSlotDisplay` needs no drop rule.

```csharp
protected override void DropItem(Package package)
{
    if (!package.IsValid)
        return;

    if (!DragProvider.Instance.TryGetDropPosition(this, out var positionToAdd))
        return;

    package = Container.AddAtPosition(positionToAdd, package);

    DragProvider.Instance.SetPackage(this, package, DragProvider.Instance.PositionOffset, Input.mousePosition);

    Container.InvokeRefresh();
    DragProvider.Instance.Origin.Container?.InvokeRefresh();

    FadeInPreview();
}
```

Delete the `/* TODO: match position offset based on most overlapping slots */` block — this task is that TODO. Apply the same `TryGetDropPosition` swap in `VendorSlotDisplay.DropItem`. `EquipmentSlotDisplay` places at its own fixed `Position` and needs no drop rule.

- [x] **Step 4: Recompile → green; run the harness** — compiled clean through the unity-mcp bridge (0 errors, only an unrelated Unity-AI-Assistant network warning). Harness: `BUG 1` reads `match` for the 1×1 (`(5,3)`, 69.4 %) and the 2×3 (`(4,1)`, 86.6 %); BUGs 2 and 3 still red as expected. Harness updated to read the drop cell from `provider.TryGetDropPosition` instead of `hovered - positionOffset`.

- [ ] **Step 5: Play-mode check** ← *needs a human*

Drag a 1×1 so it visibly sits mostly over a neighbouring slot and drop. It must land where it looked. Verify the red tint appears exactly when the item's *visual* footprint covers two or more items.

- [x] **Step 6: Commit** — `90c429e`

```
fix: drop the item where it looks, not where the grab cell says
```

---

## Task 4: Keep the grip when a drop is rejected

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Inventories/AbstractDimensionalContainer.cs`
- Modify: `InventorySlotDisplay.cs`, ~~`VendorSlotDisplay.cs`~~, `EquipmentSlotDisplay.cs`

Fixes bug 2. Today `DropItem` re-anchors on every call; a rejected drop therefore grid-aligns the item under the cursor. Gate the placement first, and return without touching the drag display when nothing can land.

> **Plan correction (2026-08-30, during execution):** the "apply the same gate in `VendorSlotDisplay.DropItem`" instruction in Step 2 is void, for the same reason Task 3 found — `VendorSlotDisplay.DropItem` is a sell sink (mint currency, hide the display), not a grid-placement drop. It has no `positionToAdd` / `AddAtPosition` to gate. Task 4 touched only `AbstractDimensionalContainer` + `InventorySlotDisplay` + `EquipmentSlotDisplay`.

- [x] **Step 1: Add `CanPlaceAt` to `AbstractDimensionalContainer`** — landed. `IsWithinDimensions` promoted to a `private` method; `IsEmptySpace`'s `IsValidPosition` local function and the new `CanPlaceAt` both call it.

Promote the nested `IsWithinDimensions` local function out of `IsEmptySpace` to a private method (both callers use it), then add:

```csharp
/// <summary>
/// Whether a drop at this position would land at all: inside the container, and
/// overlapping at most one stored item. 0 overlaps drops into empty space, 1 swaps
/// (AddAtPosition handles both); 2+ places nothing, and the caller must keep dragging.
/// </summary>
public bool CanPlaceAt(Vector2Int position, Vector2Int dimension)
{
    foreach (var requiredPosition in CalculateRequiredPositions(position, dimension))
        if (!IsWithinDimensions(requiredPosition))
            return false;

    return GetStoredItemsAt(position, dimension).Count <= 1;
}
```

- [x] **Step 2: Gate `DropItem` on it** — landed in `InventorySlotDisplay.DropItem` (between `TryGetDropPosition` and `AddAtPosition`). `EquipmentSlotDisplay.DropItem` had its `foreach ... if (Position == position)` loop replaced with an `!allowedPositions.Contains(Position)` early `return` (`using System.Linq` added). `VendorSlotDisplay.DropItem` untouched — see plan correction above.

```csharp
if (!DragProvider.Instance.TryGetDropPosition(this, out var positionToAdd))
    return;

/// Nothing would land: the item stays in hand exactly as the player is holding it.
/// Re-anchoring here is what snapped a rejected drop onto the grid.
if (!Container.CanPlaceAt(positionToAdd, AbstractItem.GetDimensions(package.Item.Dimensions)))
    return;

package = Container.AddAtPosition(positionToAdd, package);
```

- [x] **Step 3: Recompile → green; run the harness** — compiled clean through the unity-mcp bridge (0 errors, negative control confirmed the pipeline reports). Harness `Scenario` BUG 2 rewritten to mirror the gated `DropItem` against a throwaway blocked container: the 2×3 over two items reads `CanPlaceAt=False`, pivot `(0.542, 0.028)` unchanged, `(0, 0)` px jump — *"rejected drop keeps the grip"*. (A 1×1 covers one cell and can never collide with two items, so it is only ever rejected out of bounds; the harness notes this.) BUG 1 still `match`, BUG 3 still red (Task 5), BUG 4 still fixed.

- [ ] **Step 4: Play-mode check** ← *needs a human*

Hold a 2×3 over a spot where it straddles two items (tint goes red) and click. The item must stay under the cursor, unmoved, still held. Also try an equipment slot with the wrong item type — it must stay held too.

- [x] **Step 5: Commit** — `7dbd9f7`

```
fix: keep holding the item when a drop cannot land
```

---

## Task 5: Centre a handed-over package on the cursor

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/DragProvider.cs`
- Modify: `InventorySlotDisplay.cs`, `EquipmentSlotDisplay.cs`, `VendorSlotDisplay.cs`, `SellItenSlotDisplay.cs`, `DropToFloorSlotDisplay.cs`, `CharacterEquipment.cs`

Fixes bug 3. After Task 4 the only way `DropItem` reaches the drag display is a real handover: the package landed (drag ends) or it swapped (a *different* item is now in hand). Neither wants the old item's `positionOffset`.

> See **QA findings** below: QA-1 confirms bug 3 in play-test and notes it also affects same-footprint swaps. QA-2 (stale hover preview after a swap) is adjacent — decide here whether to fix it as a **Task 5b** or fold it into Step 2.

- [ ] **Step 1: Add `ReplacePackage` and `EndDrag` to `DragProvider`**

```csharp
/// <summary>
/// A different package is now in hand - a swap handed the displaced item over. It gets
/// centred on the cursor: the previous item's positionOffset describes a footprint this
/// one does not have, and reusing it left small items floating a fixed distance away.
/// </summary>
public void ReplacePackage(Package package)
{
    if (!package.IsValid)
    {
        EndDrag();
        return;
    }

    DraggingPackage = package;
    PositionOffset = Vector2Int.zero;

    var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

    itemDisplay.sizeDelta = dimensions * slotSize;
    itemDisplay.pivot = DragGeometry.HandOverPivot;

    RefreshDisplay(package);
    SetToMousePosition();
}

public void EndDrag()
{
    DraggingPackage = default;
    PositionOffset = Vector2Int.zero;

    itemDisplay.gameObject.SetActive(false);
}
```

Lift the `RefreshDisplay` local function out of `SetPackage` into a private method so both entry points share the icon / frame / amount painting. `SetPackage` keeps its `SetPosition` anchor path; `ReplacePackage` uses the centred pivot.

- [ ] **Step 2: Call it from every handover site**

In `InventorySlotDisplay.DropItem`, `VendorSlotDisplay.DropItem`, `EquipmentSlotDisplay.DropItem`:

```csharp
package = Container.AddAtPosition(positionToAdd, package);

DragProvider.Instance.ReplacePackage(package);
```

In `SellItenSlotDisplay.DropItem` and `DropToFloorSlotDisplay.DropItem`, replace `SetPackage(this, new Package(), Vector2Int.zero)` with `EndDrag()` — they are sinks and the empty-package call was only ever a way to hide the display.

In `CharacterEquipment.cs:118`, replace `SetPackage(DragProvider.Instance.Hovered, previouslyEquipped[i], Vector2Int.zero)` with `ReplacePackage(previouslyEquipped[i])`. Unequipping a two-hander hands a weapon back the same way a swap does.

- [ ] **Step 3: Recompile → green; run the harness**

`BUG 3` must read `cursor inside the item? yes` for the 2×3 → 1×1 case, with pivot `(0.5, 0.5)`.

- [ ] **Step 4: Play-mode check**

Drop a 2×3 onto a 1×1. The 1×1 must appear centred under the cursor and follow it with no gap. Also do a plain 1×1 ↔ 1×1 swap (QA-1) — it must centre too. Repeat for a two-handed weapon over a filled off-hand. If QA-2 is being fixed here, check the hover preview now describes the item left in the slot, not the one picked up.

- [ ] **Step 5: Commit**

```
fix: centre a swapped-out item on the cursor instead of inheriting the old grip
```

---

## Task 6: Remove the harness and verify

**Files:**
- Delete: `Assets/Scripts/_DragDropHarness.cs` (+ `.meta`)

- [ ] **Step 1: Full manual pass**

With every fix in, in play mode:

1. Grab a 1×1 by its bottom-left corner, move so it visually covers a neighbour, drop → lands where it looked.
2. Same with a 2×3 and a 1×4, grabbed at several different cells.
3. Straddle two items → red tint → click → the item stays held, unmoved.
4. Swap a 2×3 onto a 1×1 → the 1×1 is centred under the cursor.
5. Press on an item's frame, drag outward fast → grip is from the press point.
6. Equipment slots, the sell slot, the floor slot and the vendor buyback path all still work.
7. Click-to-pick-up (no drag) still works — it shares `MoveItem`.

- [ ] **Step 2: Grep the instrumentation out**

```bash
grep -rn "DEBUG-d1a9" Assets/
```

Must return nothing.

- [ ] **Step 3: Recompile → green, Run All → green**

- [ ] **Step 4: Commit**

```
chore: drop the drag/drop diagnosis harness
```

---

## QA findings — 2026-08-30 (play-test after Task 3)

Two symptoms found while play-testing the swap path. Neither is a Task 3 regression; both are on the drop/handover surface this plan already owns.

### QA-1 — a swapped-in item is not centred on the cursor

**Repro:** drag any item, drop it onto a slot that already holds one. The displaced item is picked up but hangs off the cursor by a fixed offset instead of sitting under it.

This is **bug 3**, and it is broader than the table row: it is *not* limited to a different footprint (2×3 → 1×1). Even a same-size 1×1 ↔ 1×1 swap is off, because `InventorySlotDisplay.DropItem` still re-anchors the displaced package through `SetPackage(this, package, PositionOffset, …)` — feeding the *outgoing* item's `PositionOffset` and `Origin` to the *incoming* one.

**Fix:** Task 5, as written — `ReplacePackage(package)` with `DragGeometry.HandOverPivot`. When doing Task 5, widen Step 4's play check to include a plain 1×1 ↔ 1×1 swap, not just 2×3 → 1×1.

### QA-2 — the hover preview/tooltip keeps the swapped-out item after a swap

**Repro:** hover a slot holding item B long enough for its preview/tooltip to fade in, then drop item A onto it. A now sits in the slot and B is in hand, but the preview still shows **B** (the item now on the cursor), not **A** (the item now in the slot).

**Suspected mechanism:** `AbstractSlotDisplay.DropItem` ends with `FadeInPreview()`, which reads `Container.TryGetItemAt(ref position, out …)` at the slot's own `Position`. After Task 3 the drop follows the visual, so `positionToAdd` (where A lands) is often *not* `this.Position` — A can land on a neighbouring cell. `TryGetItemAt` at `Position` then finds nothing, the fade-in coroutine never starts, and the stale B preview from the pre-drop hover is never replaced. `FadeOutPreview()` is only wired to `OnPointerExit`, so standing still after the drop leaves it stale.

**Fix (not yet scoped):** the preview after a drop should describe whatever the player is now hovering — refresh it from `positionToAdd` (or re-run the hover logic against the cursor's real slot), and clear it explicitly when the drop leaves the slot empty. Candidate: a small **Task 5b** alongside the handover change, or fold into Task 5 Step 2. Decide when picking up Task 5.

---

## Follow-ups (out of scope)

- **Cell size has two sources.** `DragProvider.slotSize` and `GridLayoutGroup.cellSize` must agree or the drop rule drifts; `InventorySlotDisplay.SetDisplaySize` also folds in `gridLayout.spacing`, which `DragGeometry` assumes is zero. Worth deriving one from the other.
- **`Input.mousePosition` vs `eventData.position`.** After Task 2 the *grab* uses the event; `SetToMousePosition` and `TryGetDropPosition` still read `Input` directly, which rules out touch and gamepad pointers.
- **`SetToMousePosition` divides by `itemDisplay.lossyScale` while the anchor maths divides by the provider's `transform.lossyScale`.** Identical today; a scaled drag display would break the two apart.
- **No seam for the `RectTransform` wiring.** `DragGeometry` is tested, but "does `DragProvider` feed it the right numbers" is still only checkable by hand or by a harness. A PlayMode test with a synthetic canvas would close that gap.
