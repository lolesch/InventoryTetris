# Transactional item movement, and the equipment-swap QA fixes

Date: 2026-08-30
Status: Superseded, never implemented. Only the spec itself (`fb805b5`) is on `main`.
`dev/specs/2026-08-31-foundational-rework-design.md` re-orders this work: the
`ItemTransaction` seam now lands *after* the item-model split, folded in there as
Phase 2. Everything else here still stands - read it through that spec, not alone.
Base: `feature/mutablefloat-port` @ `3e9a7d4` (pushed). Cut a new branch from there —
suggested `feature/item-movement-model`. Do **not** rebase onto `main`.

## Problem Statement

Three symptoms recorded while play-testing the drag/drop branch
(`dev/plans/2026-08-29-drag-cursor-anchoring.md`, "QA findings" sections). They look
independent but share one gap: **item transfers between containers have no
transactional guarantee.** A transfer that removes an item and then fails to place it,
or the thing it displaced, loses the item silently.

### QA-4 — equipment swap recurses infinitely and deletes an item

**Repro:** drop a two-handed weapon onto the equipped bow while a shield is also
equipped. Console: `StackOverflowException`. The bow ends up in the inventory; the
shield is gone. Trying to re-equip a shield afterwards throws
`KeyNotFoundException: key '(13, 0)'` at `CharacterEquipment.cs:32`.

A 2H weapon sits at slot `(12,0)` with a 2×1 footprint, covering both `(12,0)` (bow)
and `(13,0)` (shield). `CharacterEquipment.AddAtPosition` allows the double-swap
(`else if (otherItems.Count <= 2)`), then `TrySwap` re-homes each displaced item with
`package.Sender.TryAddToContainer(ref current)`. Three defects there:

1. **Recursion.** `package.Sender` is frequently the equipment itself (equipped items
   are stored `new Package(this, …)`, and a force-swap started from
   `CharacterEquipment.TryAddToContainer` carries that sender forward).
   `CharacterEquipment.TryAddToContainer`'s "Force swap with current equipment" branch
   has no give-up condition, so displaced → re-equip → slots full → force-swap →
   displaces again → … `StackOverflowException`.
2. **Raw indexer.** `CharacterEquipment.cs:32` reads `StoredPackages[x].Item` inside a
   `.Where(...)` over type-specific positions. Any position that is not a live key
   (e.g. `(13,0)` while a 2H is keyed only at `(12,0)`) throws `KeyNotFoundException`.
   It must be `TryGetValue`.
3. **Item loss.** `CharacterEquipment.cs:128` — `// TODO: check for item loss, else
   revert` — is never done. When a displaced item cannot be re-homed it is simply
   dropped from the model.

### QA-3 — the "can't drop" tint disagrees with the placement rule

**Repro:** hover a 2H weapon over the equipped bow → red tint, though the drop is
legal (a 2H replaces both weapon slots). Hover it over the shield instead → no tint.

`DragProvider.HighlightOverlappingSlots` hardcodes `1 < storedPositions.Count → red`.
`CharacterEquipment.AddAtPosition` allows `otherItems.Count <= 2`. The tint rule is
stricter than the placement rule, and the two are separate copies of "can this land".
Task 3 of the drag plan already unified the tint and the drop for
`InventorySlotDisplay` via `DragProvider.TryGetDropPosition`; the *count* check
(`AbstractDimensionalContainer.CanPlaceAt`, added in Task 4) was left non-virtual and
the tint never routed through it.

### QA-2 — the hover preview keeps the swapped-out item after a swap

**Repro (to be re-confirmed — see below):** hover a slot holding item B until its
preview fades in, then drop item A onto it. A is now in the slot, B is in hand, but
the preview still shows B.

`AbstractSlotDisplay.FadeInPreview` reads the slot's own `Position`. Since drag Task 3
the drop follows the visual and often lands on a neighbouring cell, so the hovered
slot goes empty, `TryGetItemAt` finds nothing, the fade-in coroutine never starts, and
the stale preview from the pre-drop hover is never replaced. `FadeOutPreview` is only
wired to `OnPointerExit`, so standing still after the drop leaves it stale.

**QA-2 may already be fixed** by drag Task 5's `ReplacePackage` handover path. Phase 3
begins by reproducing it in play mode. If it no longer repros, Phase 3 is a regression
test and a close-out, nothing more.

## Solution

An **`ItemTransaction` scope** for every multi-step item move. Containers stop mutating
their `StoredPackages` in place during a move; they record intended changes into the
active transaction, which either **commits** (all displaced items found a home) or
**rolls back** to the pre-move snapshot. Value is conserved by construction: an item
is never removed from the model until a commit places it somewhere.

Side effects that today run inline inside the mutation path — character-stat
application on equip/unequip, the drag-cursor handover, container refresh events,
vendor currency mint/pay — become **queued actions** on the transaction that flush
only on commit.

`DropItem` in every slot display, `CharacterEquipment.TrySwap`,
`AbstractDimensionalContainer.Sort`, and the vendor buy/sell paths all route through
this one primitive. The QA-3 and QA-2 fixes ride on top of it.

The work is **four phases**, each landing on green:

| Phase | Deliverable |
| --- | --- |
| 0 | A test seam that reaches the container core from EditMode tests |
| 1 | The `Transaction` primitive; every move path routed through it; the movement-matrix tests |
| 2 | QA-4 (`CharacterEquipment.TrySwap` rewrite) and QA-3 (one rule for tint and drop) |
| 3 | QA-2 (preview refresh after a drop), if it still repros |

## User Stories

1. As a player, when I equip a two-hander over a weapon + off-hand, I want both
   displaced items to go to my inventory, so that I never lose gear to a swap.
2. As a player, when my inventory is full and a swap cannot re-home what it displaces,
   I want the swap to simply not happen, so that the game never eats an item to force
   the move through.
3. As a player, I never want the game to freeze or throw when I drag equipment around,
   so that the equipment screen is safe to fiddle with.
4. As a player, I want the red "can't drop" tint to appear exactly when the drop would
   actually be refused, so that the warning is trustworthy on the equipment slots too.
5. As a player, after I swap an item into a slot, I want the hover preview to describe
   what is now under my cursor, not the item I just picked up.
6. As a player, I want selling, buying, dropping to the floor, and auto-sort to
   conserve every item and coin, so that no inventory operation can duplicate or
   destroy anything.
7. As a developer, I want `CharacterInventory`, `CharacterEquipment`,
   `AbstractDimensionalContainer` and `Package` reachable from an EditMode test
   assembly, so that the movement matrix can be covered at all.
8. As a developer, I want one transactional primitive that every move goes through, so
   that "does this operation lose items" is answered in one place and tested once.
9. As a developer, I want character-stat changes and the drag handover to be commit-time
   effects rather than mid-mutation side effects, so that a rolled-back swap leaves the
   character sheet and the cursor exactly as they were.
10. As a maintainer, I want the equipment force-swap to have an explicit give-up
    condition, so that a re-home that cannot succeed fails cleanly instead of
    recursing.

## Implementation Decisions

### Phase 0 — the test seam

The container core (`AbstractDimensionalContainer`, `CharacterInventory`,
`CharacterEquipment`), `Package`, and the item types they need all live in the
predefined `Assembly-CSharp`. `InventorySystem.Data.asmdef` sits at `Data/Statistics/`
and covers only `Currency` / `MutableFloat` / `StatModifier`; an asmdef test assembly
cannot reference `Assembly-CSharp`. The dead `[assembly: InternalsVisibleTo(
"InventorySystem.Data.Tests")]` on `Package.cs:7` and `AbstractSlotDisplay.cs:12`
shows someone has already wanted this.

Phase 0 is a **timeboxed spike (half a day) with a committed fallback**:

- **Spike:** can Unity Test Framework 1.4.6 run EditMode tests directly against
  `Assembly-CSharp`? Things to try, cheapest first — tests in a non-asmdef Editor
  folder landing in `Assembly-CSharp-Editor`; a test-only asmdef that names
  `Assembly-CSharp` as a reference; the predefined-assembly test toggle in Test Runner
  / project settings. Success = a trivial `[Test]` that news up a `CharacterInventory`,
  adds a package, and asserts on `StoredPackages`, showing green in `Run All`.
- **Fallback (if the spike fails the timebox):** extract the container core + `Package`
  + the item types into a new `InventorySystem.Containers` asmdef, the same move made
  for `Currency` and `DragGeometry`. The `CharacterProvider` / `DragProvider` singleton
  coupling (`RemoveAtPosition`'s `CharacterProvider.Instance.Player.RemoveItemStats`,
  `CharacterEquipment.TryAddToInventory`'s `AddItemStats`, `TrySwap`'s
  `DragProvider.Instance.ReplacePackage`) is broken with injected interfaces or C#
  events — the container already raises `OnContentChanged`, so this is the same shape.

Either way the phase ends with: the container core reachable from a test assembly,
zero behaviour change, existing `Data.Tests` and `Geometry.Tests` still green.

The spike result is recorded in the Phase 0 section of the implementation plan, and
Phases 1–3 are written against whichever seam won.

The four phases may be split across more than one plan document (as the currency
redesign split into `…-phase-0-1.md`); `writing-plans` decides the cut. A plan is not
written for a later phase until the earlier one is green.

### The `ItemTransaction` primitive

- **Scope object.** `using var txn = new ItemTransaction(containerA, containerB, …)`
  names every container the move may touch. The drag cursor is wrapped as a
  one-capacity holder so "displaced item goes to the cursor" is an ordinary
  destination.
- **Snapshot on enrol.** Each container's `StoredPackages` is copied
  (`new Dictionary<>(source)` — `Package` is a struct, so this is a full value copy)
  when it joins the transaction.
- **Mutations record, they do not commit.** `AddAtPosition` / `RemoveAtPosition` /
  the displace cascade operate on the transaction's working copy. Nothing an observer
  can see changes until commit.
- **Queued effects.** Stat apply/remove, the drag handover, `OnContentChanged`, vendor
  currency mint/pay are appended to an effect list on the transaction instead of
  running inline.
- **Commit** iff the working state has no unplaced item: swap the working copies in as
  real state, then run the effect list (each `OnContentChanged` fires once), then
  `Dispose` is a no-op.
- **Rollback** on any unplaced item, on an exception, or on an un-committed `Dispose`:
  discard the working copies and the effect list. The character sheet, the cursor, and
  every container are exactly as they were.

Where it lives depends on the Phase 0 outcome — inside the new `InventorySystem.Containers`
assembly if extracted, or beside the containers in `Assembly-CSharp` if the spike
succeeds. It does not depend on any provider.

### Displaced-item landing order

When a placement displaces one or more stored items, each is re-homed by trying, in
order:

1. **the freed cursor slot** — available only if the drop vacated it, and only for one
   item;
2. **the origin container** — where the incoming item came from, at any free space;
3. **the player inventory**, at any free space;
4. **rollback** — no floor fallback, no `Sender` recursion.

This is the "attempt, revert on failure" policy. A 2H over bow + shield with inventory
room: 2H equips, bow to cursor, shield to inventory. With a full inventory: nothing
moves, the 2H stays in hand.

### QA-4 — `CharacterEquipment.TrySwap` rewrite

On top of the primitive:

- `TrySwap` opens a transaction, removes the displaced items into it, places the
  incoming item, and re-homes the displaced items by the landing order above. Commit or
  rollback. No call to `package.Sender.TryAddToContainer`.
- `CharacterEquipment.TryAddToContainer`'s force-swap branch gets an explicit give-up:
  it force-swaps at most once, and a re-home that would re-enter it fails the
  transaction instead.
- `CharacterEquipment.cs:32` uses `TryGetValue`, not the raw indexer.
- The `> ONEHANDEDWEAPONS`, 2H, and off-hand position math in `GetTypeSpecificPositions`
  is unchanged; only the swap orchestration changes.

### QA-3 — one rule for the tint and the drop

- `AbstractDimensionalContainer.CanPlaceAt` becomes `virtual`.
- `CharacterEquipment` overrides it to allow the legal 2H double-swap
  (`GetStoredItemsAt(...).Count <= 2` when the incoming item is a two-hander at the
  weapon slot), matching `AddAtPosition`.
- `DragProvider.HighlightOverlappingSlots` asks `Hovered.Container.CanPlaceAt(...)`
  instead of counting overlaps itself — the same "one rule, shared by the tint and the
  drop" move Task 3 made for the drop position.

### QA-2 — preview refresh after a drop

Only if the play-mode repro still stands.

- After a successful `DropItem`, refresh the preview from where the item actually
  landed (the cell `TryGetDropPosition` returned) or from the cursor's real hovered
  slot, rather than from `this.Position`.
- Clear the preview explicitly when the drop leaves the hovered slot empty, rather than
  relying on `OnPointerExit`.

## Phases and green gates

**Green** = compiles with zero console errors *and* `Window ▸ General ▸ Test Runner ▸
EditMode ▸ Run All` passes. Compile-check via the unity-mcp bridge
(`CompilationPipeline.RequestScriptCompilation` fully qualified, then
`Unity_GetConsoleLogs`); the bridge cannot run the Test Runner, so a human runs
`Run All` at each gate, or batch mode is used with the Editor closed. `dotnet build` is
useless here (stale `.csproj`).

| Phase | Green gate |
| --- | --- |
| 0 | Seam in place; a throwaway `CharacterInventory` test shows green in `Run All`; `Data.Tests` + `Geometry.Tests` unchanged; zero behaviour change |
| 1 | The movement-matrix tests (below) all green; every `DropItem`, `Sort`, and vendor path routed through `ItemTransaction`; in-editor smoke: equip / unequip / sell / buy / drop / sort still work |
| 2 | 2H-over-bow+shield with inventory room = both displaced items to inventory; with full inventory = full rollback, 2H stays in hand, no exception; `CanPlaceAt` and the tint agree on every slot type; ring↔ring and weapon↔weapon swaps still correct |
| 3 | QA-2 repro re-checked; if fixed, a regression test; if not, the refresh lands and the preview tracks the cursor after a drop |

Commits: small, one per task, repo message style (`feat:` / `fix:` / `refactor:` /
`chore:` / `test:` / `docs:`), body explains *why*. End every message with:

```
Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
```

## Testing Decisions

**What makes a good test here.** Assert on external behaviour: total item and coin
count before vs after, which container holds what, the character sheet's applied
affixes, the package left in hand. Do not assert on `StoredPackages` internal identity,
dictionary order, or how many times an event fired. Prior art: `CurrencyTests.cs` and
`MutableFloatTests.cs` — `[TestFixture] public sealed class`,
`Subject_Condition_Expectation` names, one behaviour per `[Test]`, a small local helper
to build inputs.

New folder `Assets/Scripts/Tests/EditMode/Inventories/` with its own asmdef (or a
non-asmdef folder, per Phase 0), test-local item fixtures — no `ScriptableObject`
instantiation, no scene.

### The movement matrix

Every cell is: **value conserved** (item + coin totals unchanged unless the operation
is a sale/purchase, in which case the delta is exactly the price), **stats correct**
(affixes applied only while equipped), **hand correct** (the right package, or none,
left on the cursor).

| From → To | Cases |
| --- | --- |
| Inventory → Inventory | reposition to empty; swap with one item; merge into a stack; reject when the footprint straddles 2+ items |
| Inventory → Equipment | equip to empty slot; swap 1H↔1H; 2H over 1H+off-hand (room / no room); wrong `EquipmentType` rejected, item stays in hand |
| Equipment → Inventory | unequip to empty; unequip when inventory full = rollback |
| Equipment → Equipment | ring↔ring; 1H↔1H across the two weapon slots |
| Inventory/Equipment → Vendor | sell: item removed, coins minted at sell value, hand cleared |
| Inventory/Equipment → Sell slot | same as vendor sell |
| Vendor → Inventory | buy: coins paid exactly, item placed; buy with no room = rollback, no charge |
| Any → Floor | a *deliberate* player drop (not auto-rehoming): item leaves the model exactly once (define "floor" — a sink, or a real pile) |
| Cursor → full container | rollback, item stays on cursor |
| `Sort()` | remove-all + re-add conserves every item; a table that will not re-fit rolls back rather than dropping the overflow |

### Regression pins

- **QA-4:** the 2H-over-bow+shield sequence — no exception, no item loss, both outcomes
  (room / no room) as specified.
- **QA-3:** `CanPlaceAt` returns the same verdict the tint shows, for inventory *and*
  equipment, including the legal 2H double-swap.
- **QA-2:** if reproduced, a test that a post-drop preview describes the landed cell.

## Out of Scope

- **Retuning anything.** Sell values, vendor markup, stack limits, equipment slot
  layout — all unchanged.
- **A real floor / loot pile.** If one does not exist, "drop to floor" stays the
  current sink behaviour; the matrix only asserts the item leaves the model exactly
  once. A physical drop pile is separate work.
- **The drag-cancel / return-to-origin path.** `DragProvider`'s commented-out
  `ReturnToOrigin` / `DropHere` / `OnEndDrag` stubs and the "drag charged me then I
  never dropped it" refund gap (`2026-08-26-shop-currency-followups.md` §2) are
  related but are their own change. This spec's transaction primitive is the right
  foundation for them; wiring the cancel path is not in these four phases.
- **A dedicated vendor container.** `2026-08-26-shop-currency-followups.md` §1. The
  vendor paths are routed through `ItemTransaction` here, but `Store` stays a
  `CharacterInventory`.
- **Removing `this is CharacterEquipment` from `AbstractDimensionalContainer.
  RemoveAtPosition`.** The base-class-knows-subclass smell is real; if Phase 0 extracts
  and injects a stat interface it may fall out naturally, but it is not a goal.
- **`ItemStack` enum removal** (currency plan Task 9) and any currency-redesign work.
  The `Currency Type Distribution.asset` edit in the working tree is currency WIP —
  never stage it on this branch.
- **PlayMode tests for the `RectTransform` drag wiring** (drag plan Follow-ups). The
  seam here is for the container model, not the drag display.

## Further Notes

### Why one spec, phased, rather than three issues

QA-3 and QA-4 are the same feature (the equipment 2H double-swap) seen from two angles,
and neither can be fixed safely without the transactional guarantee — a QA-4 fix that
stops the crash but still loses the shield is not a fix. QA-2 is smaller and on a
different surface, but it is cheap to carry along and it shares the "one rule shared by
tint and drop / preview and drop" theme. The test seam (Phase 0) is a prerequisite for
covering any of it and is worth its own phase.

### Risk: Phase 0 could be large

If the spike fails and the extraction is needed, Phase 0 becomes a refactor of the most
central class in the system, and `shop-currency-followups.md` §1's "push the packing
implementation down into `AbstractDimensionalContainer`" is adjacent temptation.
Resist widening: extract, break the provider coupling, stop. The packing-implementation
consolidation is a separate call.

### Risk: the transaction snapshot and serialized state

`AbstractDimensionalContainer` is `[Serializable]` and `StoredPackages` is a serialized
field. A transaction working on a copy must write back to the *same* dictionary
instance on commit (or reassign the serialized property) so the Inspector and any
serialized reference stay pointed at live state. The plan's Phase 1 first task pins
this with a test that a committed move is visible through a pre-held reference.
