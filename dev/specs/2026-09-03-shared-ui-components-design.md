---
status: draft
---

# Shared UI primitives move into the Utility submodule, and the submodule owns tweening

## Problem Statement

InventoryTetris has a set of UI base classes that took real work to get right — a
`Selectable`-derived button/toggle family with hover and press feedback, an exclusive-
selection radio group, a fade/scale/move panel with before/after hooks, a `MonoBehaviour`
prefab pool, and small canvas/layout/selection helpers. They all sit in `GUI/Components/`,
which compiles into the predefined `Assembly-CSharp`.

The sister project (AutoBattler / GlyphsHero) already consumes the `Utility` submodule but
has none of this. Its UI is hand-rolled: every view re-implements pointer-enter/exit and
submit wiring, spawns children with raw `Instantiate` and no pooling, and it even wrote its
own `BundleVersionView` `MonoBehaviour` to surface `Utility`'s `BundleVersionSetter.GetVersion()`
— a widget InventoryTetris does not have at all. Any third project would start from zero
again.

The one thing standing between these classes and a clean extraction is DOTween. Three of
them (`AbstractButton`, `AbstractToggle`, `AbstractPanel`) call `DG.Tweening`, which lives
in a 2.3 MB vendored plugin under `Assets/Plugins/Demigiant/`. An asmdef cannot reference
`Assembly-CSharp`, and AutoBattler does not ship DOTween. The obvious "fix" — an
`IPanelTransition` seam with a DOTween adapter on one side and a coroutine fallback on the
other — introduces an animation seam that would have exactly one real implementation
forever. That is a seam the developer does not want to cut.

## Solution

The `Utility` submodule already runs a `PlayerLoop`-driven tick pump: `TimerBootstrapper`
inserts `TimerTicker.TickTimers` into the `Update` loop, and `Timer` / `Stopwatch` ride it.
A tween is a `Timer` plus an easing function plus a per-tick setter. So the submodule gains
a small tweening capability (`Ease` + `Tween` + a handful of UI extension methods) built on
the pump it already owns, with no external dependency.

With tweening owned by `Utility`, the UI base classes call `canvasGroup.TweenAlpha(0, 0.2f,
Ease.InOutQuad)` exactly the way they call `.DOFade(...)` today — no indirection, no
`IPanelTransition`. The generic primitives then move into a new `Utility.UI` assembly that
both projects reference. InventoryTetris deletes the DOTween plugin. AutoBattler gets a
real button/toggle/panel framework, a prefab pool, and the shared version widget for free.

The domain-coupled views (`CoinDisplay`, `CurrencyDisplay`, `CharacterStatDisplay`,
`PreviewDisplay`, …) stay in InventoryTetris as adapters over the shared `IView<T>` seam and
the existing providers.

## User Stories

1. As the maintainer of both InventoryTetris and AutoBattler, I want the button/toggle/panel
   base classes in one shared place, so that a fix or improvement lands once and both
   projects get it.
2. As the maintainer, I want the primitives in the `Utility` submodule rather than a
   project-local `GUI` assembly, so that a third project picks them up by adding one
   submodule.
3. As a developer starting a new Unity UI, I want a `MonoBehaviour` I can subclass with a
   single `OnClick()` override, so that I never re-wire `IPointerClickHandler` /
   `ISubmitHandler` / `interactable` gating by hand again.
4. As a developer building a settings or pause screen in AutoBattler, I want an
   `AbstractPanel` with `FadeIn()` / `FadeOut()` / `Toggle()` and `BeforeAppear()` /
   `OnAppear()` hooks, so that show/hide transitions and raycast-blocking are handled for me.
5. As a developer building a tab bar, I want a `RadioGroup` that enforces single selection
   across self-registering toggles with an `OnGroupChanged` event and an optional
   allow-all-off, so that I do not hand-code mutual exclusion.
6. As a developer, I want toggles that swap sprites and run the `Selectable` state
   transition on toggle, so that on/off state is visible without extra scripting.
7. As a developer, I want hover-scale and press-scale feedback on buttons and toggles out
   of the box, so that interactive elements feel responsive with zero per-widget setup.
8. As a developer who cares about the build, I want InventoryTetris to stop shipping the
   2.3 MB DOTween plugin, so that the project depends only on what it uses.
9. As a developer in AutoBattler, I want the shared UI classes to work without installing
   DOTween or any other tween library, so that adopting them costs nothing.
10. As a developer worried about GC in the loot- and combat-number-heavy MVP sim loop, I
    want the tween to allocate only once per tween start and never per frame, so that UI
    animation does not add hitching.
11. As a developer, I want `Ease` to cover the common curves (linear, quad in/out/inout,
    sine inout, and the usual ~dozen), so that I am not limited to a straight lerp.
12. As a developer, I want `canvasGroup.TweenAlpha(...)`, `rectTransform.TweenAnchoredPosition(...)`,
    `transform.TweenScale(...)`, `graphic.TweenColor(...)` and `image.TweenFillAmount(...)`,
    so that the property tweens the components actually use are one call each.
13. As a developer, I want a tween handle with `.OnComplete(...)` and `.Kill()`, so that I
    can react to completion and cancel in flight.
14. As a developer, I want `Tween.Kill(someObject)` / `Tween.IsTweening(someObject)`, so
    that a component tearing down can cancel every tween it started on a transform — the
    `DOTween.Kill(transform)` pattern `AbstractPanel` and `AbstractToggle` rely on today.
15. As a developer, I want a tween linked to a Unity object to stop when that object is
    destroyed, so that a fade on a closing panel does not throw after the panel is gone.
16. As a developer, I want `AbstractPanel`'s delayed fade-in / auto-fade-out to use the
    submodule's `Timer`, so that the last `DG.Tweening.Sequence` usage disappears with the
    rest.
17. As a UI designer working in the Unity Editor, I want the panel's fade duration,
    auto-fade-out delay, scale-from and move-from still exposed in the inspector, so that
    tuning does not require a code change.
18. As a UI designer, I want `RootCanvas` to keep forcing a consistent canvas-scaler setup
    on validate, but with the reference resolution and match factor as serialized fields,
    so that each project sets its own target resolution.
19. As a developer targeting mobile, I want `ScreenRotator` available, so that orientation
    locking is not re-implemented per project.
20. As a developer, I want `FirstSelected` in the shared set, so that the controller-
    navigation "set `EventSystem.firstSelectedGameObject` if unset" gotcha is solved once.
21. As a developer, I want `RefreshLayoutOnEnable` to live next to `UIExtensions.RefreshContentFitter`,
    which is already in the submodule, so that the helper and the extension it calls are
    together.
22. As a developer, I want `PrefabPool<T>` / `IObjectPool<T>` in `Utility`, so that
    AutoBattler can pool damage numbers, slot pips and loot cards instead of calling
    `Instantiate` every time.
23. As the maintainer, I want one `BundleVersionView` in the submodule, so that AutoBattler
    deletes its local copy and InventoryTetris can finally show a build version.
24. As a developer, I want the misnamed `TooltipRequester` (whose tooltip calls are all
    commented out) renamed to something honest like `InteractiveElement`, so that the base
    class name reflects what it is: the shared `Selectable` + submit wiring.
25. As a developer in InventoryTetris, I want the interactive element to surface a tooltip
    through a nullable `ITooltipSink` the project sets at startup, so that the pointer
    wiring is shared but the tooltip rendering stays in the app.
26. As a developer in AutoBattler, I want the interactive element to work with no tooltip
    sink set, so that I am not forced to build a tooltip system to use a button.
27. As a developer, I want audio to stay behind the existing `PlayHoverSound()` /
    `PlayClickSound()` / `PlayToggleSound(bool)` virtual no-op hooks, so that the submodule
    never references an `AudioProvider`.
28. As a developer, I want the shared views to cross an `IView<T>` seam with a single
    `Refresh(T)` method, so that InventoryTetris's currency, stat and preview displays are
    adapters over a named contract.
29. As the maintainer, I want InventoryTetris's domain displays (coin, currency, character
    stat, stat modifier, resource, item preview) to stay in the project, so that the
    submodule takes on no inventory, currency or character vocabulary.
30. As the maintainer, I want `LoadSceneButton` and `QuitGameButton` to stay in
    InventoryTetris, so that `SceneProvider`, the `SceneRef` attribute and editor-only quit
    do not leak into the submodule.
31. As a developer, I want the `Test*` sample classes kept out of the shared runtime
    assembly, so that demo scripts do not ship with the library.
32. As a developer, I want the moved classes to standardise on `NaughtyAttributes.ReadOnly`,
    so that InventoryTetris's local global-namespace `ReadOnlyAttribute` is not dragged
    into the submodule.
33. As a developer, I want `Ease` and `Tween` covered by fast EditMode tests that tick the
    tween by hand with no scene, so that the animation core has a regression net.
34. As the maintainer, I want the submodule to stay test-assembly-free with its tests
    living in the consuming project, so that the pattern already used for `Timer` in
    AutoBattler continues.
35. As a reviewer, I want the change to arrive as small slices each landing on green
    (tween core, then dependency-free helpers, then the DOTween port + plugin deletion,
    then the interaction family), so that no step leaves the project uncompilable.
36. As the maintainer, I want an ADR recording that generic UI primitives live in the
    `Utility` submodule rather than a project `GUI` assembly, so that the decision is not
    re-argued later.
37. As a developer who may later need chained or path tweens, I want the tween extension
    methods to be the point where a richer library (PrimeTween, LitMotion) could slot in,
    so that today's minimal tween does not foreclose that.
38. As the maintainer, I want both consumer repos to move in lockstep by bumping the
    submodule pointer, so that neither project sits on a stale `Utility`.

## Implementation Decisions

### The tween lives in `Utility`, on the existing tick pump

- `Utility` core gains a `Tools/Tweening` area: an `Ease` enum, an `Easing.Evaluate(Ease, t)`
  pure function (~12 standard curves), and a `Tween` type.
- `Tween` reuses the existing tick contract rather than adding a second `PlayerLoop`
  system. It registers with `TimerTicker` the same way `Timer` does and is advanced by the
  same `Update`-loop insertion `TimerBootstrapper` already makes. No new bootstrapper, no
  new `PlayerLoopUtils` call.
- `Tween`'s surface: a start (`from`, `to`, `duration`, `Ease`), a per-tick applier, a
  fluent `OnComplete(Action)`, `Kill()`, and a link to a Unity `Object` so the tween
  self-cancels when that object is destroyed.
- Static helpers `Tween.Kill(object target)` and `Tween.IsTweening(object target)` replace
  the `DOTween.Kill(transform)` / `DOTween.IsTweening(...)` calls in the moved components.
- UI-typed extension methods, co-located with the existing `UIExtensions` in `Utility`
  core (which already references `UnityEngine.UI`): `TweenAlpha` (CanvasGroup),
  `TweenAnchoredPosition` (RectTransform), `TweenScale` / `TweenLocalScale` (Transform),
  `TweenColor` (Graphic), `TweenFillAmount` (Image). Each returns a `Tween` handle.
- Garbage: `Tween` handles are pooled internally; the only per-*start* allocation is the
  applier/`OnComplete` delegate, never a per-frame allocation. This matches what
  PrimeTween / LitMotion advertise ("no per-frame GC").
- `AbstractPanel`'s delayed fade-in and auto-fade-out drop `DG.Tweening.Sequence` and use
  `new Timer(delay)` with an `OnComplete` handler — `Timer` already is exactly this.

### The new `Utility.UI` assembly

- A new asmdef at `Assets/Submodules/Utility/UI/`, `Utility.UI`, referencing `Utility` only
  (UGUI arrives transitively as it already does for `Utility` core via TextMeshPro).
- Kept separate from `Utility` core so that pure-logic assemblies (`InventorySystem.Data`
  and friends, which reference `Utility`) do not gain a line of sight to `MonoBehaviour` UI
  components.
- Contents moved from `ToolSmiths.InventorySystem.GUI.*` to `Submodules.Utility.UI.*`:
  - The interaction family: `InteractiveElement` (renamed from `TooltipRequester`),
    `AbstractButton`, `AbstractToggle`, `RadioGroup`.
  - Panels: `AbstractPanel`, plus the generic `PanelToggle`, `MultiplePanelToggle`,
    `LoadingScreenPanel`.
  - Helpers: `FirstSelected`, `RefreshLayoutOnEnable`, `RootCanvas`, `ScreenRotator`,
    `BundleVersionView` (authored fresh in the submodule).
  - The view seam: `IView<T>` with one method `Refresh(T)` (renamed from
    `IDisplay<T>.RefreshDisplay`).
- `PrefabPool<T>` and `IObjectPool<T>` move to `Utility` core (`Tools/`), not `Utility.UI`
  — they are `MonoBehaviour`-generic with no UGUI dependency.

### `TooltipRequester` becomes `InteractiveElement` with a tooltip seam

- The class is renamed; its aspirational tooltip name goes away. It stays a `Selectable`
  implementing `ISubmitHandler`, keeping the shared pointer/submit/select wiring.
- The commented-out `TooltipProvider.Instance` calls become calls through a nullable
  `ITooltipSink` (`Show(string)` / `Hide()`), a static hook the consumer sets once at
  startup. This mirrors the `ItemView.Catalog` ambient static (ADR-0007) and the
  `ICursorSink` / `IStatReceiver` interfaces injected at `InventoryProvider.Awake`
  (foundational rework Phase 0b).
- With no sink set the wiring is inert — AutoBattler uses buttons without a tooltip system.
- The serialized `tooltip` string field stays on the component.
- The unconditional `LogExtensions.Select(...)` call on every `OnSelect` is dropped (or
  left behind the existing `LogExtensions` toggle) — it is noise for a library.

### Policy exposed, not hard-coded

- `RootCanvas` keeps its `OnValidate` enforcement of render mode and scaler mode, but the
  `1920x1080` reference resolution and the match factor become serialized fields so each
  project sets its own.
- `[ReadOnly]` usages in the moved classes resolve to `NaughtyAttributes.ReadOnly` (the
  submodule already depends on `NaughtyAttributes.Core`). InventoryTetris's local
  global-namespace `ReadOnlyAttribute` is not carried down.

### What stays in InventoryTetris

- The domain displays — `CoinDisplay`, `CurrencyDisplay`, `CharacterStatDisplay`,
  `CharacterStatModifierDisplay`, `ResourceDisplay`, `PreviewDisplay` — stay as adapters
  over `IView<T>` and the providers. They take the `using` / namespace update for the
  renamed seam.
- `LoadSceneButton` (`SceneProvider`, `SceneRef`) and `QuitGameButton` (editor-only quit)
  stay. `QuitGameButton`'s unguarded `using UnityEditor` is a pre-existing wrinkle and is
  not addressed here.
- `VisibleCursorRequester`, `DonstDestroyOnLoad`, `ShowNameOnHover` stay in the project;
  they are too shallow to share, and cursor visibility is already owned by the `ICursorSink`
  seam. Deleting them is optional and out of scope.
- `TestButton`, `TestToggle`, `TestPanel` / `TestGrid`, `TestDisplay` do not enter the
  shared runtime assembly.

### The DOTween plugin is deleted

- DOTween is used in exactly three files, all in `GUI/Components/`, with no `DOTweenAnimation`
  components in any prefab or scene. Once those three are ported to the `Utility` tween API
  and verified in the Editor, `Assets/Plugins/Demigiant/` (DOTween + DOTweenPro) is removed.

### Slicing (for `/to-tickets`)

1. Tween core in `Utility`: `Ease`, `Easing.Evaluate`, `Tween` on `TimerTicker`, the UI
   extension methods, `Kill` / `IsTweening`, plus EditMode tests. Ships behind nothing —
   nothing consumes it yet.
2. The dependency-free move into `Utility.UI`: `PrefabPool<T>` (to core), `FirstSelected`,
   `RefreshLayoutOnEnable`, `RootCanvas` / `ScreenRotator`, `BundleVersionView`, `IView<T>`.
   InventoryTetris repoints its `using`s and prefab script references; AutoBattler deletes
   its local `BundleVersionView`.
3. Port `AbstractButton`, `AbstractToggle`, `AbstractPanel` to the `Utility` tween API in
   place, verify in the Editor, delete `Assets/Plugins/Demigiant/`.
4. Move the interaction family and panels into `Utility.UI`, with the
   `TooltipRequester` -> `InteractiveElement` + `ITooltipSink` rename. Add
   `docs/adr/0008-*` recording the "generic UI primitives live in the Utility submodule"
   decision.

## Testing Decisions

- A good test here pins external behaviour only: the value handed to the applier after a
  partial tick, whether `OnComplete` fired and how many times, whether a cancel stays
  silent — never the internal registry or handle-pool mechanics.
- **`Easing.Evaluate`**: for every `Ease`, `Evaluate(e, 0)` is 0 and `Evaluate(e, 1)` is 1;
  `Linear` at 0.5 is 0.5; the monotone eases are non-decreasing across a sampled sweep.
- **`Tween`**: driven by manual `Tick(dt)` with no scene, no `PlayerLoop`, exactly as
  AutoBattler's `TimerTests` drive `Timer`. Assert the applier receives the eased value
  after a partial tick; `OnComplete` fires exactly once when the duration elapses and not
  before; `Kill()` and `Kill(target)` cancel without firing `OnComplete`; a tween linked to
  a Unity object stops when that object is destroyed; repeated start/complete cycles do not
  grow the handle pool.
- **Modules tested**: the `Utility` tweening core only. The `Utility.UI` `MonoBehaviour`s
  are not unit-tested — the project's EditMode suite is pure-logic throughout, and
  ADR-0007 records that scene-bound behaviour is out of test scope until a module is
  extracted. These components are scene-bound by nature.
- **Prior art**: `AutoBattler/Assets/Code/Tests/EditMode/Utility/TimerTests.cs` — same
  submodule, same manual-`Tick` style, same "Start schedules, does not act synchronously;
  Stop / cancel is silent" contract discipline. InventoryTetris's per-module `*.Tests`
  EditMode asmdefs (`InventorySystem.Geometry.Tests`, `InventorySystem.Probability.Tests`,
  …) are the local structural prior art; a new EditMode asmdef referencing `Utility` hosts
  the tween tests. InventoryTetris test assemblies reference only `nunit.framework.dll`,
  so the tween tests use plain NUnit asserts (not FluentAssertions, which is AutoBattler's
  choice).

## Out of Scope

- Extracting `InventorySystem.GUI` or `InventorySystem.Runtime` from `Assembly-CSharp` —
  that is the top of ADR-0007's stack and a separate effort. This spec moves *generic*
  primitives *down* into the submodule; it does not touch the domain UI, the drag surface,
  or the provider singletons.
- Any change to the domain displays beyond the `IView<T>` rename.
- A tween *sequence* API (chained / parallel timelines), path or spline tweens, and
  shake / punch helpers. The home-grown tween covers single-property lerps with an ease and
  a completion callback — the whole current need.
- Adopting PrimeTween or LitMotion now. Evaluated and deferred; see Further Notes.
- Migrating `DOTweenAnimation` inspector components — there are none.
- Building a tooltip system in AutoBattler. The `ITooltipSink` seam is provided; wiring a
  renderer behind it there is AutoBattler's call.
- A shared UI theme / skin system (colours, sprites, fonts). This spec is behavioural
  primitives only.
- Deleting `VisibleCursorRequester` / `DonstDestroyOnLoad` / `ShowNameOnHover` — optional
  cleanup, not required here.

## Further Notes

- **Why not cut an animation seam.** An `IPanelTransition` (or equivalent) would have one
  implementation forever — a hypothetical seam. Pushing tweening inside the deep module
  keeps `FadeIn()` / hover-scale as the interface and the tween engine invisible. If a
  second animation backend ever becomes necessary, the tween extension methods are the
  place it slots in — a real seam discovered by a real second need.
- **Why home-grown over PrimeTween / LitMotion.** The submodule's identity is
  zero-dependency primitives; it already runs the `PlayerLoop` tick pump; the required
  surface (five property types, ~12 eases, a completion callback, kill-by-target) is tiny
  and stable. Both libraries are MIT and remain drop-in-able later through the same
  extension-method surface. PrimeTween is the stronger candidate if that day comes — its
  own asmdef, no transitive dependencies, a near-identical API.
- **The DOTween footprint is smaller than it looks**: 3 files, ~18 call sites, 0 asset
  references, 2.3 MB of plugin. The seam was guarding against a dependency that barely
  exists.
- **`BundleVersionView` is the concrete proof of value.** The submodule already ships
  `BundleVersionSetter.GetVersion()`; AutoBattler wrote the `MonoBehaviour` to surface it;
  InventoryTetris never displays the version. One shared widget fixes both.
- **Submodule mechanics**: the two repos consume `Utility` at the same commit from
  different remote URLs (`https://` in InventoryTetris, `git@` in AutoBattler). A submodule
  change propagates by a pointer bump in each. The submodule has no test assembly of its
  own and this spec keeps it that way — tests live in the consuming project.
- **ADR**: slice 4 adds `docs/adr/0008-generic-ui-primitives-live-in-the-utility-submodule.md`
  (or similarly named). It is consistent with ADR-0007's direction of shrinking
  `Assembly-CSharp` behind strict layered assemblies, but it moves code *below*
  `InventorySystem` rather than carving within it, which is worth recording explicitly.
