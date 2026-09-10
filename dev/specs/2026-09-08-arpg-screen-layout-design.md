---
status: partly superseded
---

> **Superseded in three places (2026-09-09 / 2026-09-10).** The rest of this spec still
> stands; these are the decisions that moved after it was written.
>
> 1. **Side panels are on the LEFT, not the right.** The left side holds the Town Stop
>    contexts (Stash, Vendor) and the Combat Panel; the right side is the Hero Panel
>    (Equipment over Inventory). Corrected 2026-09-09; see issues #56 and #57 and the
>    **Side Panel** / **Hero Panel** entries in `CONTEXT.md`.
> 2. **The Healer and Go Venture ARE toggles in the minimap's `TownGroup`.** This spec says
>    the Healer is a plain `AbstractButton` that does not join the `RadioGroup` - withdrawn
>    2026-09-10. Every Town interaction deselects its siblings and cancels what they had in
>    flight; group membership gives that for free. See issue #58.
> 3. **The minimap face is its own state, not a projection of `RunPhase`.** `RunPhase`
>    forces the face but does not define it: Go Venture shows the Field face while still
>    `InTown` so the player can pick a destination, and To Town backs out of that preview
>    without a Recall. See issue #56.
>
> The side-panel `RadioGroup` this spec asks for was also narrowed: mutual exclusion comes
> from the minimap's `TownGroup`, and a `SidePanelToggle : PanelToggle` announces
> `SidePanelContext` on both edges. See issue #57.

# ARPG Screen Layout

> Driven by a `/grill-with-docs` session, 2026-09-08. Replaces the `Switch Context` /
> `MultiplePanelToggle` / `RunPhaseUIBinding` / `StoreStashPhaseBinding` wiring from the
> simulation branch with a minimap-centric layout that drives all panel state.

## Problem Statement

The simulation branch (`docs/mvp-simulation-loop`) introduced `MapPanel`,
`BehaviourSlidersPanel`, `RunPhaseUIBinding`, `StoreStashPhaseBinding` and a
`Switch Context` toggle that swaps between `InventoryContext` and `CombatContext`.
This works for testing but doesn't scale to an ARPG in-game screen:

- The context toggle is a coarse switch; real games have the inventory always visible
  and context-sensitive side panels that open/close independently.
- There's no minimap — the location list is a plain button group.
- Stash and vendor have dedicated toggle buttons outside any spatial metaphor.
- The combat debug info is an `OnGUI` overlay, not proper UGUI.
- Enemy HP bars, ability icons, and ground items have no home yet.

## Solution — Three-zone layout

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│  ┌──────────┐   ┌──────────────────┐  ┌───────────┐ │
│  │          │   │                  │  │ Inventory │ │
│  │  LEFT    │   │     CENTER       │  │ Equipment │ │
│  │  PANEL   │   │     MINIMAP      │  │ (always)  │ │
│  │ (InField │   │                  │  │           │ │
│  │  only)   │   └──────────────────┘  └───────────┘ │
│  │          │                                        │
│  └──────────┘        ┌──────────┐                    │
│                      │ ABILITY  │                    │
│                      │ HOTBAR   │                    │
│                      └──────────┘                    │
└──────────────────────────────────────────────────────┘
```

### Center — Minimap (always visible)

Two visual states driven by `RunPhase`. Each state has its own background artwork and
button set.

**Town state:**
- Background: town art
- Buttons (hardcoded): Stash, Vendor, Healer, Go Venture
- Stash/Vendor: `AbstractToggle` inside a `RadioGroup` (`AllowSwitchOff = true`).
  Clicking opens the corresponding side panel. Clicking again (or pressing the
  same hotkey) deselects and closes the panel.
- Healer: plain `AbstractButton` (not a toggle). Click = instant HP/Resource refill.
- Go Venture: plain button. Click = minimap switches to Field state, closes all
  open side panels (stash/vendor).

**Field state:**
- Background: field art
- Buttons: one `LocationToggle` per authored `LocationConfig` (serialized list, manually
  positioned) + "To Town" button.
- `RadioGroup` with `AllowSwitchOff = false` — exactly one location is always selected
  once the hero is InField.
- Clicking a location toggle = `SimulationProvider.Send(location)`.
- "To Town" = `SimulationProvider.Recall()` + minimap switches back to Town state.
  In the `RadioGroup` "To Town" acts as a deselect-then-select or a plain button
  depending on implementation.

### Right side — Hero panels (always visible)

- **Inventory** + **Equipment** panels, stacked or tabbed on the right.
- Always open, never toggled by the minimap. These are hero-context, not
  town-context.
- No hotkey toggle needed (they're always showing).

### Right side — Town panels (toggleable)

- **Stash** panel + **Vendor** panel, sharing the same position (one visible at a time).
- Opened by clicking their minimap button or pressing their hotkey (`S`/`V`).
- `RadioGroup` with `AllowSwitchOff = true` — only one active at a time.
- The active side panel is exposed as `SidePanelContext { None, Stash, Vendor }` on
  `InventoryProvider`, so the trade flow can read it without coupling to the UI.
- Closing: click the active minimap button again, press the hotkey again, or press
  `Esc`.

### Left side — Combat panel (InField only)

- Visible only during `RunPhase.InField`. Fades in on Send, fades out on Recall/Death.
- Fixed layout, top-to-bottom:
  1. **Behaviour sliders** — from existing `BehaviourSlidersPanel`: retreat HP, recall
     bag fill, resource reserve, loot filter, sim speed.
  2. **Enemy HP bar pool** — `PrefabPool<T>` of HP bar prefabs in a
     `VerticalLayoutGroup`. Each bar shows: enemy label, HP fill bar with numeric
     overlay (current/max), rarity-colored border. New entries arrive from top, pushing
     others down. Defeated enemies vanish.
  3. **Combat debug info** — encounter count, enemies alive/defeated, sim time. Compact
     TMP labels, not an OnGUI overlay.
- The enemy HP bar data binding depends on per-enemy events from the sim engine
  (`EncounterSimulation` doesn't currently expose per-enemy HP). Part 1 of this
  epic ships the UI shell with a placeholder; Part 2 (enemy HP data binding) is a
  follow-up grill session.

### Center bottom — Ability hotbar (future)

- Separate epic, deferred. Minimal v1: two icons that flash on Strike/Cast.
- Physical: flash = scale tween, CD overlay driven by `AttackSpeed`.
- Magical: greyed out when `Resource < CastCost`, flash on Cast.

## Panel reuse

| New panel | Reuses | Notes |
|---|---|---|
| Town minimap background | — | New artwork, code is `AbstractPanel` subclass |
| Field minimap background | — | New artwork, same subclass |
| Stash side panel | Existing stash panel art/structure | Strip inventory logic, keep as boundary |
| Vendor side panel | Existing vendor panel art/structure | Same treatment |
| Left combat panel | `BehaviourSlidersPanel` + new content | Sliders extracted from current layout |
| Enemy HP bar | `PrefabPool<T>` pattern | New prefab, follows `CharacterStatModifierDisplay` pattern |
| Ground items list | `PrefabPool<T>` + slot display | Same pattern as equipment slots |
| Ability hotbar | New | Minimal flash-on-attack, future expansion |

## Hotkeys

| Key | InTown | InField |
|---|---|---|
| `S` | Toggle stash panel | inert |
| `V` | Toggle vendor panel | inert |
| `H` | Healer (instant refill) | inert |
| `B` | Go Venture (switch minimap to Field) | To Town (recall + switch minimap to Town) |
| `1` | inert | Location 1 (send) |
| `2` | inert | Location 2 (send) |
| `Esc` | Close topmost side panel | Close topmost overlay |

## Trade context

The active side panel (`SidePanelContext`) is tracked on `InventoryProvider`, not on
the UI toggles. When a stash or vendor toggle activates, it notifies the provider.
When it deactivates, it sets context back to `None`. The trade flow
(`feature/trade-flow` branch) reads `InventoryProvider.SidePanelContext` to decide
shift-click routing:
- `Stash` → items go to stash
- `Vendor` → items go to sell basket
- `None` → default (no quick-move target)

This follows the existing provider pattern: UI pushes state, providers own state, other
systems read providers. The toggle doesn't know about inventories.

## What dies

- `Switch Context` toggle — replaced by minimap state
- `MultiplePanelToggle` on Switch Context — replaced by minimap's state-change logic
- `RunPhaseUIBinding` — replaced by minimap's `RunPhase` subscription
- `StoreStashPhaseBinding` — replaced by minimap's hotkey gating
- `MenuToggles` / `StoreToggle` / `StashToggle` — replaced by minimap buttons
- `SimulationDebugPanel` (OnGUI) — replaced by proper UGUI combat panel

## Epics

1. **Minimap system** — Two-state minimap (town/field), toggle groups, location sends,
   recall, "Go Venture" transition, minimap-driven panel orchestration. Replaces
   `Switch Context` and all its bindings.
2. **Side panel management** — Stash/vendor as toggleable side panels with
   `RadioGroup`, `SidePanelContext` on `InventoryProvider`, hotkeys S/V, trade flow
   context derivation.
3. **Combat panel** — Left-side panel with behaviour sliders (from existing code) and
   combat debug info (encounter stats). Enemy HP bar data binding deferred to a
   separate grill session.
4. **Ability hotbar** — Minimal v1: two icons, flash on attack. Own epic, lowest
   priority.
5. **Ground items display** — Pooled slot list for ground drops, hover preview, click
   to pick up. Blocked by LootFlow epic completion.

## Testing Decisions

- EditMode tests for: `SidePanelContext` enum + notification logic on `InventoryProvider`,
  minimap state transitions (mock `RunState`), hotkey routing.
- No scene tests for panel fade/layout — those are `AbstractPanel` subclass behaviour,
  already covered by the Utility submodule's patterns.
- Enemy HP bar data binding tests deferred to the Part 2 grill session.
