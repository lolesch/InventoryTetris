# Port MutableFloat from GlyphsHero, rebuild CharacterStat on composition

Date: 2026-08-13
Status: Approved

## Problem

`CharacterStat.CalculateTotalValue()` computes the modifier-adjusted total by
sorting `StatModifiers` in place and then walking the list multiple times with
a shared skip-index per modifier type (Overwrite / FlatAdd / PercentAdd /
PercentMult). This is fragile and was also, until recently, called through an
override chain: `CharacterResource.CalculateTotalValue()` called
`base.CalculateTotalValue()` and then re-read the `TotalValue` property, which
recursed back into `CalculateTotalValue()` — a stack overflow (fixed
provisionally in commit `ebfe8a8`, but the underlying override-based
architecture is still re-entrancy-prone).

`GlyphsHero` (repo: `C:\Users\loles\Desktop\LEONID\AutoBattler`) solved the
same problem — a stat total driven by a list of modifiers — with a different
shape: `MutableFloat` is a sealed, non-inheritable value engine that computes
`base + modifiers` with LINQ, caches the result, and only fires a change event
when the cached value actually changes. Consumers that need to react to a
total changing (like a resource pool reacting to its max changing) subscribe
to that event instead of overriding a calculation method. There is no
override chain to recurse through, structurally.

## Goal

Port `MutableFloat`'s engine into InventoryTetris and rebuild `CharacterStat`
/ `CharacterResource` on top of it via composition, eliminating the
override-based recursion risk at its root, not just at the one call site that
already broke.

## Non-goals

- `MutableInt`, `Stat`, `Resource` (GlyphsHero's own consumer wrapper
  classes) are not ported. InventoryTetris's `CharacterStat` /
  `CharacterResource` play that role already and are being adapted in place
  instead of replaced by GlyphsHero's types.
- No `Guid source` field added to `StatModifier`. GlyphsHero's `Modifier`
  struct carries one (to distinguish two identical modifiers from different
  sources, e.g. two rings each granting `+10 FlatAdd`), but `StatModifier`
  does not and this port keeps it that way — it's a pre-existing gap, not one
  this change introduces, and fixing it would touch every `StatModifier`
  construction site (item affix rolling, `ItemTypeData`, `LocalPlayer`, ~7
  files) for a problem that hasn't been reported.
- No change to item affix generation or the `Vector2Int Range` clamping on
  `StatModifier` — orthogonal to the modifier-total calculation this change
  touches.

## Components

### 1. `MutableFloat` (new)

Ported from `Assets/Code/Runtime/Modules/Statistics/MutableFloat.cs` in
GlyphsHero, adapted to operate on InventoryTetris's existing
`StatModifier` / `StatModifierType` rather than GlyphsHero's own
`Modifier` / `ModifierType`. The two are structurally equivalent for this
purpose (same four modifier types, both already implement
`IComparable`/`IEquatable`), so the calculation algorithm ports directly;
only the type names and the explicit `.Value` access change (`StatModifier`
has no implicit `float` conversion, unlike GlyphsHero's `Modifier`).

Adds two read-only accessors GlyphsHero's version does not have:

- `float BaseValue`
- `IReadOnlyList<StatModifier> Modifiers`

These exist because `CharacterStatDisplay.CharacterStatData` reads
`stat.BaseValue` and `stat.StatModifiers` directly to build a breakdown
tooltip (`(base + flatAdd) * percentAdd * percentMult`). GlyphsHero's
`MutableFloat` keeps both private since nothing in that codebase needs them;
InventoryTetris does, so they're exposed as read-only.

`Clone()` is carried over unchanged in spirit: an independent copy (fresh
modifier list, no subscribers) used for "what if" probes. This directly
backs `LocalPlayer.CompareStatModifiers`, which already does exactly this
today via `CharacterStat.GetDeepCopy()` (clone, swap a modifier, compare
totals) — confirming the clone semantics are a good fit, not a new pattern
being introduced speculatively.

### 2. `CharacterStat` (rewritten internals, same public surface)

Holds a `MutableFloat` field instead of its own `List<StatModifier>` plus the
sort-and-skip-index calculation. Public members keep their existing
signatures so calling code doesn't change:

- `float BaseValue` → forwards to `MutableFloat.BaseValue`
- `IReadOnlyList<StatModifier> StatModifiers` → forwards to
  `MutableFloat.Modifiers` (was `List<StatModifier>` with a private setter;
  narrowing to `IReadOnlyList<StatModifier>` is safe because nothing outside
  `CharacterStat` ever assigned to it, only read/enumerated it — confirmed by
  grep across `Assets/Scripts`)
- `float TotalValue` → forwards to the `MutableFloat`'s implicit `float`
  conversion
- `event Action<float> TotalHasChanged` → forwards to
  `MutableFloat.OnTotalChanged` via add/remove accessors
- `AddModifier` / `TryRemoveModifier` → forward to the `MutableFloat`
- `GetDeepCopy()` → clones the `MutableFloat` instead of copying the list

Dropped as part of this rewrite, both confirmed to have zero call sites
outside their own declaration:

- `SetBaseTo(float)` — private and already unused. Superseded by the
  existing in-code comment on `CharacterStat`: growth is meant to be applied
  as a `StatModifier`, not a base-value mutation, so there's nothing for a
  base-mutation method to do that the modifier system doesn't already cover.
  `MutableFloat` has no base-mutation method to back this with even if it
  were needed.
- `GetShallowCopy()` — unused; `MemberwiseClone()` behavior is unaffected by
  this refactor if it's ever needed again, so this is a pure dead-code
  removal, not a capability loss.

### 3. `CharacterResource` (override removed, event subscription added)

No longer overrides a calculation method — there isn't one to override
anymore, since `CharacterStat` no longer exposes a virtual calculation hook.
Instead, mirroring GlyphsHero's `Resource` constructor exactly:

```csharp
public CharacterResource(StatName resourceName, float baseValue) : base(resourceName, baseValue)
{
    RefillCurrent();
    TotalHasChanged += _ => SetCurrentTo(CurrentValue);
}
```

Whenever the underlying `MutableFloat`'s total changes (a modifier added or
removed), the event fires, and `SetCurrentTo(CurrentValue)` re-clamps
`CurrentValue` into the new `[0, TotalValue]` range. This is the change that
actually removes the recursion risk: there is no override chain left for a
future change to accidentally make re-entrant.

`GetDeepCopy()` keeps the same documented caveat GlyphsHero's `Resource` has:
the clone's `MutableFloat` is independent (via `Clone()`), but a clone's
`OnTotalChanged` subscriber list is empty (`Clone()` doesn't carry
subscribers over), so a cloned `CharacterResource` does not automatically
re-track its own max-value changes. Fine for a read-only probe (the only
current use, `LocalPlayer.CompareStatModifiers`), not a substitute for a
real spawned character's resource.

Dropped: `GetResourceCopy()` — unused, zero call sites.

### 4. New asmdef for the ported pieces

InventoryTetris's own game code currently has no asmdef at all — everything
under `Assets/Scripts/InventorySystem` compiles into the implicit
`Assembly-CSharp`. Unity does not allow a custom asmdef (such as the new
Tests assembly, below) to reference `Assembly-CSharp`. To make `MutableFloat`
testable, `MutableFloat.cs`, `StatModifier.cs`, and `StatModifierType.cs` move
into a new folder with its own asmdef:

- New folder: `Assets/Scripts/InventorySystem/Data/Statistics/`
- New asmdef: `InventorySystem.Data` (references `Utility`, the existing
  submodule asmdef at `Assets/Submodules/Utility`, and NaughtyAttributes)
- `StatModifier.cs` and `StatModifierType.cs` physically move from
  `Data/Structs/` and `Data/Enums/` into the new folder. **Namespaces are
  unchanged** (`ToolSmiths.InventorySystem.Data` /
  `ToolSmiths.InventorySystem.Data.Enums`), so none of the ~7 existing call
  sites that construct `StatModifier` need any change — only the physical
  file location and their `.meta` GUIDs move.
- `CharacterStat.cs` / `CharacterResource.cs` stay in `Assembly-CSharp` (not
  moved) and reference the new asmdef the same way any other Assembly-CSharp
  script references a custom asmdef — no special handling needed, since
  Assembly-CSharp compiles after all custom asmdefs.

This is deliberately the narrow option: only the three files with zero
MonoBehaviour/game-loop coupling move. `CharacterStat`/`CharacterResource`
—which do more than pure math (serialization callbacks, character-level
concerns)— stay where they are.

### 5. Tests asmdef (EditMode)

New folder: `Assets/Scripts/Tests/EditMode/Statistics/` with its own asmdef
(`InventorySystem.Data.Tests`), referencing `InventorySystem.Data`,
`UnityEngine.TestRunner`, `UnityEditor.TestRunner` (test-assembly only), and
`nunit.framework`.

GlyphsHero's `MutableFloatTests.cs` is ported with two adaptations:

- `FluentAssertions`-style assertions (`.Should().Be(...)`) become plain
  NUnit `Assert.That(...)` — InventoryTetris has no FluentAssertions package
  and this port doesn't add one.
- Modifier construction changes from `new Modifier(value, type, Guid.NewGuid())`
  to `new StatModifier(new Vector2Int(int.MinValue, int.MaxValue), value, type)`
  — `StatModifier`'s constructor clamps `Value` into `Range` at construction,
  so tests use an effectively-unbounded range to keep the ported test values
  unclamped, matching GlyphsHero's tests where no such clamping exists.

## Behavior changes (both improvements, called out explicitly)

1. **Event firing is now deduplicated.** `CharacterStat.AddModifier` /
   `TryRemoveModifier` currently call `TotalHasChanged?.Invoke(TotalValue)`
   unconditionally on every successful call, even if the numeric total didn't
   actually change (e.g. removing a modifier that was already being ignored
   because an `Overwrite` modifier was in effect). `MutableFloat` only fires
   `OnTotalChanged` when `Mathf.Approximately(oldTotal, newTotal)` is false.
   Net effect: fewer redundant UI refreshes; no case where a UI listener
   needed a "changed" notification for an unchanged value.
2. **Dead code removed**, not adapted: `SetBaseTo`, `GetShallowCopy`,
   `GetResourceCopy` (see above — all confirmed zero call sites).

## Testing

- Ported `MutableFloatTests.cs` (EditMode/NUnit) covers: base value with no
  modifiers, FlatAdd, full FlatAdd → PercentAdd → PercentMult ordering,
  Overwrite precedence, modifier removal, the `OnTotalChanged` event, `Clone`
  independence (including "remove on clone doesn't touch original" and "no
  subscriber carry-over"), and the quiet `TryRemoveModifier(_, warnIfMissing:
  false)` overload.
- No new tests for `CharacterStat`/`CharacterResource` themselves (no test
  infrastructure exists for `Assembly-CSharp` and this change deliberately
  doesn't create one — see "narrow" asmdef scope above). Manual verification:
  confirm in the Unity Editor that adding/removing item affixes still updates
  character stat displays and that health/resource pools still clamp
  correctly when max value changes from a modifier.

## Risks

- Moving `StatModifier.cs` / `StatModifierType.cs` to a new folder changes
  their `.meta` file paths but not their GUIDs (GUIDs live inside the `.meta`
  file and move with it), so serialized references in scene/prefab/asset
  files are preserved as long as the `.meta` files are moved alongside the
  `.cs` files rather than regenerated.
- First asmdef carve-out from `Assembly-CSharp` for this project's own code;
  if any other unexpected file under `Data/Structs` or `Data/Enums` turns out
  to depend on something only available in `Assembly-CSharp`, moving it would
  fail to compile and surface immediately (not a silent runtime issue).
