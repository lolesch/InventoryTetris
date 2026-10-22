# Settings menu

## Problem

The player has no way to tune the game. Values that are really taste (how long a tooltip waits,
how long a drop lies on the floor) are either hardcoded or live in a throwaway prototype.
`UserSettings` (`Assets/Scripts/InventorySystem/Data/Statistics/UserSettings.cs`) now holds them with
defaults and bounds, but nothing in the UI reads or writes them.

## What exists

| Setting | Where | State |
| --- | --- | --- |
| Hover delay (0 - 1.5 s, default 0.3) | `UserSettings.HoverDelay`, read by `HoverPreview.Delay` | Stored and used; no UI |
| Ground item fade delay (5 - 600 s, default 60) | `UserSettings.GroundItemFadeDelay` | Stored; **nothing reads it, the ground has no fade-out** |
| Sold-tab peek while Alt is held | `SellingPanelPeek`, always on | Built (two-panel switch epic #195); the prototype's checkbox and `PlayerPrefs` key are gone, so there is no setting |

Storage is `PlayerPrefs`, per machine: a setting follows the player, not the hero, so it does not belong
in a hero save or the Account file (ADR-0017 keeps the save format out of Utility; settings are not part
of it).

## Candidate settings

Each is a hardcoded value or a debug toggle today. Not all of them belong in the first cut.

| Candidate | Today | Notes |
| --- | --- | --- |
| Roll ranges always shown | Alt swaps the comparison for the range (`ModifierKeys.Alt`) | Bool; some players never want to hold a key |
| Tooltip comparison on / off | Always on | Bool |
| Quick-move key | `LeftShift` in `AbstractSlotDisplay` | Rebind |
| Half-stack key | `LeftControl` in `AbstractSlotDisplay` | Rebind |
| Second-slot compare key | `LeftShift` in `PreviewProvider` / `InventorySlotDisplay` | Rebind; shares the quick-move key today |
| Pause key | `Space` in `PauseHotkey` | Rebind |
| Panel hotkeys | `SidePanelToggle.hotkey` serialized per toggle | Rebind |
| Auto-equip on pickup | `DebugPanel.ToggleAutoEquip` | Promote from debug toggle |
| Sell confirmation | None | Confirm above a rarity; needs a design call |
| Ground loot filter | None | Dim or hide drops below a rarity; pairs with the fade delay |
| UI scale | Canvas scaler on the scene | Needs a layout pass |

Out of scope: balance (`GameConfig`), hero saves, Editor-only prefs (`HeroSavesMenu` "Start Fresh Each
Play" lives in `EditorPrefs`), audio (there is no audio system yet).

## Proposal

1. **A settings panel** built on the shared UI components (epic #36) and the existing side-panel pattern
   (ADR-0013: entry points request the context, panels derive visibility). One row per setting: a
   `ValueSlider` for a range, a toggle for a bool, a key capture for a binding.
2. **`UserSettings` stays the one source.** The panel reads each setting's default and bounds from it, so
   a range is never written twice. Add a `Changed` event so a live value (hover delay) needs no polling.
3. **Applies immediately**, with a per-setting reset and a "reset all". No Apply button.
4. **Rebinding is its own slice.** Hardcoded `KeyCode`s become a binding table in `UserSettings`; the
   modifier reads move from `Input.GetKey(KeyCode.X)` to that table.
5. The ground fade-out itself is **a separate issue**: it needs a lifetime on a ground item and a fade on
   the ground row, then it reads `GroundItemFadeDelay`.

## Slices

1. Settings panel with the two existing settings (hover delay, ground fade delay) and reset.
2. Ground item fade-out reading `GroundItemFadeDelay`.
3. Bool settings: ranges always shown, tooltip comparison, auto-equip.
4. Key rebinding.
5. Loot filter and sell confirmation, after a design call.

## Open questions

- Where does the panel open from: the pause overlay, a side panel with its own toggle, or both?
- Should a setting survive across machines? `PlayerPrefs` is per machine; a settings file beside the saves
  (`SaveLocation`) would travel with them and respect `-savesFolder`.
- Rebinding: single key per action, or key plus modifier? Conflicts: refuse or swap?
- Sell confirmation threshold: by rarity, by value, or both?

## Acceptance (first slice)

- A player can change hover delay and ground fade delay in a panel and the value persists across a restart.
- Each slider's range and default come from `UserSettings`, not the panel.
- Reset restores the default; the panel shows the value actually in effect.
- `UserSettings` round-trips and clamps are covered by EditMode tests (done for both existing settings).
