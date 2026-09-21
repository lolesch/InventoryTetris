# Side Panel hotkey and PanelGroup rework

Date: 2026-09-18
Status: Scoping spec — not a plan. Slice into a GitHub epic with `/to-tickets`, build
with `/implement`.
Base: cut from `feature/trade-flow` as `feature/sidepanel-hotkey-rework`, after the
panel-toggle-cleanup + trade-flow merge (`fbdf28b`). Reverses `SidePanelToggle`'s doc
comment "#58 correction 2026-09-17", which banned introducing `PanelGroup` here — that
ban was about not creating two exclusivity answers within the same object hierarchy; this
design keeps that guarantee by putting `PanelGroup` and `RadioGroup` on two different
hierarchies that answer two different questions (see Implementation Decisions).

## Problem Statement

A `/code-review` pass over the `panel-toggle-cleanup` merge (finding #2) confirmed a real
bug: `MinimapController.ApplyFace` gates `locationToggles`' `interactable` on the Run
phase but never gated the Stash/Vendor/Healer toggles', so their hotkeys stay live while
`InField` — a stray keypress can reopen a Town Stop's Side Panel the player can no longer
see or reach by click.

Chasing that fix surfaced two things worth doing at the same time, not separately:

1. `SidePanelToggle` currently owns three unrelated jobs at once — announcing
   `SidePanelContext` to the `InventoryProvider`, polling its own hotkey, and
   participating in the Stash/Vendor/Healer `RadioGroup`. The `SidePanelContext`
   announcement is supposed to track "is my panel visible", but it is actually driven by
   "is this toggle on" — two sources of truth that happen to agree today only because
   nothing else can show or hide the panel.
2. The **Combat Panel** shares the left side with the Side Panels (CONTEXT.md) but is
   excluded from `RadioGroup`, which only holds toggles — the Combat Panel has none. So
   it is faded in and out by a separate, manual `combatPanel.Toggle(!_inTown)` call in
   `SyncToPhase`, outside any group. "Which left panel is visible" therefore has two
   authorities: the `RadioGroup` for the three Town Stops, and ad-hoc phase-driven calls
   for the Combat Panel — exactly the kind of drift that let finding #2 happen in the
   first place.

## Solution

**Fix the interactable gate.** Extend `ApplyFace`'s existing per-toggle `interactable`
loop (already applied to `locationToggles`) to also cover the Stash/Vendor/Healer
toggles, so their hotkeys go inert whenever the Field face is up — the same guarantee the
click path already has for free via `CanvasGroup.blocksRaycasts`.

**Give "which left panel is visible" one authority.** Adopt `PanelGroup`
(`Assets/Submodules/Utility/UI/Panels/PanelGroup.cs`) — written as a structural mirror of
`RadioGroup` for content instead of input, and unused anywhere in game code until now —
for all four left panels: Stash, Vendor, Healer, and the Combat Panel, as siblings under
one group. `SimplePanel.Toggle`/`FadeIn`/`FadeOut` already route through whichever group
parents them, so this needs no submodule change: the Combat Panel joins the same
exclusivity pool the Town Stop panels are already in, and `MinimapController`'s manual
`combatPanel.Toggle(...)` call is replaced by driving the same group the toggles do.

**Move the `SidePanelContext` announcement from the toggle to the panel.** A new
`SidePanel : SimplePanel` (mirroring the existing `BehaviourSlidersPanel` pattern of a
`SimplePanel` subclass with lifecycle-hook overrides) announces its own context in
`BeforeAppear`/`BeforeDisappear`. This makes the announcement track panel visibility
directly, instead of tracking toggle state as a proxy for it. `RadioGroup` stays on the
toggles, unchanged, answering a different question ("which button is pressed") on a
different object hierarchy — not a second answer to "which panel is open."

## User Stories

### The bug fix

1. As a player, I want the Stash/Vendor/Healer hotkeys to do nothing while I'm in the
   Field, so that a stray keypress can't reopen a Town Stop panel I can no longer see.
2. As a player, I want that gate to match what already happens when I click a Town Stop
   toggle while InField (nothing, because the panel housing it is faded and
   non-interactive), so that hotkey and click agree about when a Town Stop is reachable.
3. As a developer, I want the fix expressed as one guard in the same place the
   `locationToggles` gate already lives, so that "reachable while InTown" has one pattern
   applied uniformly instead of a per-toggle bool that can drift again.

### Panel exclusivity

4. As a player, I want opening the Stash, Vendor, or Healer panel to still close whichever
   one was open, exactly as today, so that clicking a Town Stop behaves unchanged.
5. As a player, I want Send (going InField) to close whatever Side Panel was open and show
   the Combat Panel, and Recall/Death (returning InTown) to close the Combat Panel, so
   that the left side never shows two things at once.
6. As a player, I want Go Venture (previewing the Field face while still InTown) to close
   any open Side Panel, unchanged from today.
7. As a developer, I want the Combat Panel and the three Side Panels to share one
   exclusivity mechanism, so that a future left-side panel only has to join the existing
   group, not learn a second bookkeeping path.
8. As a maintainer, I want `PanelGroup` adopted exactly as already written in the
   submodule — no new method, no new option — so that its first real usage validates the
   component as designed rather than growing it to fit a special case.

### Side Panel Context

9. As a developer, I want the panel itself to own the moment it announces
   `SidePanelContext`, so that "the Vendor's Side Panel Context is active" and "the
   Vendor's panel is visible" are the same fact, not two facts that happen to agree.
10. As a developer, I want the toggle and the panel to hold no reference to each other for
    this purpose, so that neither has to change if the other's triggering mechanism
    changes later (a hotkey, a phase change, a future non-toggle trigger).
11. As a developer, I want the existing handover-ordering guarantee (`SidePanelState`'s
    "loser clears before winner sets, publishing `None` in between") to keep holding
    unchanged, so that the Sell Basket's cancel-on-close behavior, which depends on it,
    is unaffected by where the announcement call moves.
12. As a player, I want the Sell Basket to still cancel a staged sale when the Vendor panel
    closes — however it closed — so that this rework doesn't regress the trade flow.

### Regression guards

13. As a player, I want every existing Side Panel behavior — which panel opens on click,
    mutual exclusion, Go Venture / Send / Recall / Death closing the right thing — to be
    unchanged in outcome, only in how it's wired.
14. As a developer, I want `LocationToggle`/`fieldGroup` untouched, so that this rework
    doesn't creep into destination-picking, which is a different kind of exclusivity
    (a choice, not a visibility switch).
15. As a developer, I want `inTownPanel`/`inFieldPanel` (the two faces) untouched, so that
    this rework stays scoped to the left-side panel set.
16. As a maintainer, I want zero changes to `PanelGroup.cs`, `RadioGroup.cs`, or any other
    submodule script, so that this game-code rework can't destabilize a shared,
    concurrently-edited dependency.

## Implementation Decisions

### Panel exclusivity — `PanelGroup` replaces manual bookkeeping

- One `PanelGroup` (`leftPanels`) parents four siblings: the Stash panel, the Vendor
  panel, the Healer panel, and the Combat Panel. `IsClearable` set to match the existing
  `townGroup` `RadioGroup`'s setting, so "nothing shown" stays reachable.
- `MinimapController.SyncToPhase`: the `townGroup.ClearSelection()` call and the manual
  `combatPanel.Toggle(!_inTown)` call are both replaced by driving `leftPanels` —
  `leftPanels.Show(combatPanel)` on entering the Field, `leftPanels.ClearActive()` on
  returning to Town.
- `MinimapController.GoVenture`: `townGroup.ClearSelection()` becomes
  `leftPanels.ClearActive()`.
- No change to `PanelGroup` itself. `SimplePanel.Toggle`/`FadeIn`/`FadeOut` already check
  for a parenting `PanelGroup` and route through `Show`/`Hide` when one is present — this
  is why reparenting the panels is the entire change on that side.

### `RadioGroup` on the toggles — kept, unchanged

- `townGroup: RadioGroup` stays exactly as authored today, still parenting the
  Stash/Vendor/Healer toggles. It answers "which button is pressed"; `leftPanels`
  answers "which panel is visible" — two groups on two different object hierarchies, not
  the same-hierarchy conflict the "#58 correction" doc comment was actually about.
- Each toggle (`PanelToggle` base, inherited by `SidePanelToggle`) keeps its existing
  direct `panel` reference and its existing `OnToggle → panel.Toggle(IsOn)` call,
  unchanged. Reparenting the panel under `leftPanels` is what makes that unchanged call
  now participate in the new group — no toggle-side code changes for this.
- Considered, rejected for this pass: dropping `RadioGroup`/`AbstractToggle` from
  `SidePanelToggle` in favor of a plain button reacting to `PanelGroup.OnGroupChanged`.
  `RadioGroup` already gives correct, tested toggle-visual exclusivity (both edges, via
  `Select`/`Deselect`) for free once `PanelGroup` handles the panel side — replacing it
  would mean reimplementing that bookkeeping for no behavior change.

### Side Panel Context — moves from the toggle to the panel

- New `SidePanel : SimplePanel` (game code), the lifecycle-hook-subclass pattern
  `BehaviourSlidersPanel` already establishes for this codebase. Overrides
  `BeforeAppear`/`BeforeDisappear` to call `InventoryProvider.Instance?.SetSidePanel(context)`
  / `ClearSidePanel(context)`, guarded by `Application.isPlaying` and
  `context != SidePanelContext.None` — the same guards `SidePanelToggle.OnToggle` has
  today, relocated rather than re-derived.
- `context: SidePanelContext` is authored on the Stash and Vendor panels. The Healer
  panel stays a plain `SimplePanel` — `SidePanelContext` still has no `Healer` member,
  unchanged, out of scope here.
- `SidePanelToggle` loses its `context` field and its two provider-announcement lines
  entirely. It keeps its `panel` reference (inherited), its `hotkey` field, and its
  `Update()` polling loop, all unchanged.
- `PanelGroup.Show`/`Hide` already calls `Disappear` on the panel being replaced before
  `Appear` on the new one — the same "loser clears before winner sets, publishing `None`
  in between" ordering `SidePanelStateTests.cs`'s handover tests already pin for the
  `RadioGroup` path. No change needed to that contract or those tests.

### Hotkey / interactable gating fix

- `SidePanelToggle.Update()` is not touched — it keeps polling `Input.GetKeyDown` exactly
  as it does today. A centralized hotkey handler was considered and rejected: routing a
  key press through any central place would still end by calling the same toggle's own
  `SetToggle`, so keeping it on the toggle avoids adding indirection for no behavioral
  gain.
- The fix is in `MinimapController.ApplyFace`: its existing per-toggle `interactable` loop
  (today applied only to `locationToggles`) is extended to also set the Stash/Vendor/Healer
  toggles' `interactable` from `_inTown`, mirroring the pattern already there rather than
  introducing a new one.

### What stays the same

- `LocationToggle` / `fieldGroup` — untouched, still a `RadioGroup`. Out of scope: this is
  a destination pick, not a panel-visibility switch.
- `inTownPanel` / `inFieldPanel` (the two faces) — untouched, still plain `SimplePanel`s
  manually toggled in `ApplyFace`. Not adopting `PanelGroup` for this pair in this pass.
- `PanelGroup.cs`, `RadioGroup.cs`, and every other submodule script — zero changes.

## Testing Decisions

- **One seam, unchanged.** `SidePanelState` (`Assets/Scripts/InventorySystem/Containers/SidePanelState.cs`),
  already exercised end-to-end by `SidePanelStateTests.cs`. This rework changes *who*
  calls `Set`/`Clear` — `SidePanel` instead of `SidePanelToggle` — not the rule itself,
  so the existing suite (default state, set/clear idempotency, the handover-ordering
  tests) is the regression net for the Side Panel Context change and needs no new cases.
- **No new automated seam** for the `PanelGroup` wiring, the interactable-gating fix, or
  the new `SidePanel` adapter. Each is pure MonoBehaviour/UI-object wiring with no
  decision logic worth extracting — the same reason `MinimapController` has no tests
  today; manufacturing a seam for a three-line `interactable` loop or a two-line
  provider-forwarding call would test the wiring, not a rule.
- **In-editor verification.** Compile clean, then a manual pass: open each of
  Stash/Vendor/Healer by click and by hotkey; confirm mutual exclusivity, including
  against the Combat Panel across Send/Recall; confirm Go Venture, Send, Recall, and
  Death each close whatever Side Panel was open; confirm hotkeys do nothing while
  InField; confirm a staged sale still cancels when the Vendor panel closes by any of
  these paths.
- **Prior art for this call.** `2026-09-03-trade-flow-and-quick-move-context-design.md`'s
  Testing Decisions makes the identical call ("GUI shells are smoke-tested, not
  unit-tested") for the same reason, over a larger surface.

## Out of Scope

- Turning `SidePanelToggle` into a plain button (dropping `RadioGroup`/`AbstractToggle`).
  Revisit only if the toggle's persistent on/off state becomes an actual liability later.
- Adopting `PanelGroup` for `inTownPanel`/`inFieldPanel`. That pair's Go-Venture-preview
  logic doesn't map cleanly onto `PanelGroup`'s show/hide/restore model without more
  thought than this fix warrants.
- Adding a `Healer` member to `SidePanelContext`. Pre-existing gap, not this rework's
  problem.
- Any change to `PanelGroup.cs`, `RadioGroup.cs`, or any other submodule script.
- Retuning hotkeys, fade timings, or left-side layout.

## Further Notes

### Domain-modeling follow-up

CONTEXT.md's **Combat Panel** entry currently reads "Exclusive with the Side Panels by
Run phase, not by a toggle group." After this lands, the mechanism *is* a group
(`PanelGroup`) — Run phase still decides *when* the Combat Panel is requested, but
exclusivity itself is structural, not manual. That line, and the **Side Panel** entry's
"Shares the left side with the Combat Panel" wording, should be revisited by
`/domain-modeling` when this epic starts.

### Suggested ticket slice

1. **Adopt `PanelGroup` for the four left panels** in `MinimapController` — reparent
   Stash/Vendor/Healer/Combat panels under `leftPanels`; replace the manual
   `combatPanel.Toggle(...)` call and the `townGroup.ClearSelection()` calls with
   `leftPanels.Show`/`ClearActive`. `RadioGroup`/toggles untouched.
2. **Move the `SidePanelContext` announcement onto the panel** — new `SidePanel :
   SimplePanel`; author `context` on the Stash and Vendor panels; delete `SidePanelToggle`'s
   `context` field and announcement lines.
3. **Fix the interactable-gating bug (review finding #2)** in `ApplyFace`.

Each is small enough to be its own issue; (2) wants (1) landed first only so the panel is
already `PanelGroup`-aware when its `BeforeAppear`/`BeforeDisappear` timing is checked
against the handover tests, but the dependency is soft — order given is for the cleanest
review diff, not a hard requirement. (3) is independent of both and could land first.
