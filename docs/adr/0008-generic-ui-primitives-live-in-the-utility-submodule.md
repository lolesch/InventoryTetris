---
status: accepted, in progress
---

# Generic UI primitives live in the Utility submodule, not a project GUI assembly

The `Selectable`-derived interaction family (`InteractiveElement`, `AbstractButton`,
`AbstractToggle`, `RadioGroup`) and the panel family (`AbstractPanel`, `PanelToggle`,
`MultiplePanelToggle`, `LoadingScreenPanel`) carry no inventory, currency or character
vocabulary — they are `Selectable` + submit wiring and a fade/scale/move `CanvasGroup`,
nothing else. ADR-0007 already points `InventorySystem.GUI` at the top of a
per-module-assembly stack that shrinks `Assembly-CSharp`; this decision moves these eight
types *below* that stack instead, into the `Utility` submodule both InventoryTetris and
the sister project (AutoBattler / GlyphsHero) consume. `Utility.UI` is a separate assembly
from `Utility` core so that pure-logic assemblies referencing `Utility` (`InventorySystem.Data`
and friends) do not gain a line of sight to `MonoBehaviour` UI components.

This was only possible once the classes stopped depending on DOTween, which cannot be
referenced from an asmdef outside `Assembly-CSharp` — see the tween-core and DOTween-port
tickets (#37, #39) that preceded this move.

`TooltipRequester` is renamed to `InteractiveElement` in the move: its aspirational name
promised a tooltip that was never built, so the class kept only what it actually is — the
shared pointer/submit/select wiring every button and toggle sits on. The unconditional
`LogExtensions.Select(...)` call on every `OnSelect` is dropped rather than gated; a
library base class has no business logging on behalf of every consumer, and nothing
depended on the log itself. `AbstractButton` and `AbstractToggle` build directly on
`InteractiveElement`, which is why the type survives the rename rather than being deleted
outright — a bare log call would not have.

The domain-coupled displays (`CoinDisplay`, `CurrencyDisplay`, `CharacterStatDisplay`,
`PreviewDisplay`, …), `LoadSceneButton`, `QuitGameButton` and the `Test*` demo classes stay
in InventoryTetris — they either name inventory/currency/character vocabulary the
submodule must not know, or are throwaway samples that do not belong in a shared runtime
assembly.

## Consequences

A change or fix to the button/toggle/panel base classes now lands once, in the submodule,
and reaches both consumer projects on their next pointer bump — the same mechanic already
used for `Timer`/`Tween`. `Assets/Plugins/Demigiant/` (DOTween) is gone from
InventoryTetris; `Utility.UI` has no equivalent dependency to reintroduce. `[ReadOnly]`
usages on the moved types resolve to `NaughtyAttributes.ReadOnly`, not InventoryTetris's
local global-namespace attribute of the same name, since the submodule cannot see
`Assembly-CSharp`. `InteractiveElement`'s serialized `tooltip` field still renders
nowhere — wiring it through an ambient `TooltipHost<T>` is the next ticket (#41), not this
one.
