# UI naming - Display, Panel, View and Popup mean one thing each

Date: 2026-10-14
Status: Settled in conversation after a review of the Changelog epic (#224), the front-end-flow epic (#245),
the architecture-review epic (#262) and `fix/menu-smoke-test`. The glossary edits ship with this spec; the
renames are one ticket.

## Problem Statement

The glossary gives three UI words a meaning: a **Display** is the visual representation of a data object, a
**Panel** is a `SimplePanel` (it groups content and fades, staying enabled), a **View** shows static data.
Components added since then use the words for what they look like. A `Panel` that toggles with
`SetActive`, a `Display` that only shows and hides buttons, a `Panel` that is a text, and a `Panel` that is a
list each tell the next reader something false, and three of the names (`DebugPanel`, `CharacterStatPanel`,
`GroundItemsPanel`) sit on words the glossary lists under *Avoid* for other things. "Modal" is used by
`CancelRank`, two specs and the doc comments and tests of nine source files (`CancelRank`, `EntryNoticePanel`,
the three `*Prompt`s, `ChangelogPanelView`, `GameBoot`, `GameInput`, `CancelChainTests`) and is defined nowhere.

## Decisions

**A Panel is a `SimplePanel`, and only that.** Anything else is not called a panel, whatever it looks like.

**A Display is pushed or bound.** A *pushed* display implements `IDisplay<T>` and is handed the data
(`CurrencyDisplay`, `CharacterStatDisplay`, `PreviewDisplay`). A *bound* display follows a source it is
given (`Bind`/`Unbind`) or resolves itself (a container display binds by role) and redraws on that source's
events (`ResourceDisplay`, `AbstractContainerDisplay` and its subclasses, `AbstractSlotDisplay`). A
component that is neither is not a display: a text, a list, a layout. A list that binds a container by role
is a bound display that happens to draw a list (`GroundItemsList`); its name follows the glossary's
**Ground Items List**, and `AbstractContainerDisplay` names how it binds.

**`IDisplay<T>` does not grow `Bind`/`Unbind`.** They are a different concern. `Refresh(T)` is "draw this
now", stateless about where the data came from; `Bind` is a subscription with a lifetime and an `Unbind`
that must run. Folding them in would hand `CurrencyDisplay` and `PreviewDisplay` a no-op `Unbind`, and no
shared signature exists to fold in: `ResourceDisplay.Bind(resource, recovery)`,
`GroundItem.Bind(item, amount, view, onClick)` and a container display's no-argument role lookup have
nothing in common. A second interface is rejected for the same reason: it would be named for a shape that
three callers do not share. Revisit if a pool wants to hold bound displays generically (the TODO in
`CharacterStatPanel`, "extend prefabPool to support IDisplay<T>", is pushed displays only and needs
nothing here).

**`AbstractContainerDisplay` keeps its name.** It is a bound display, so `Display` is accurate. Its
"inherit AbstractDisplay or rename this pattern" TODO is deleted, and so are `AbstractSlotDisplay`'s (the
same line) and `PreviewDisplay`'s: no `AbstractDisplay` exists, and one could not coexist with `SimplePanel`
on a type that is both.

**Modal becomes Popup.** A **Popup** is a screen opened over the one beneath it, which Cancel closes. It is
shown either by being enabled (it resets each time it opens: the hero name entry, the delete and Save &
Exit confirmations, the entry notice, the Pause Menu) or as a Panel (the Changelog, which fades). `Overlay`
was the other candidate and is rejected: it names the scrim, and the hero name entry has none. `CancelRank.Modal`
becomes `CancelRank.Popup`. `Prompt`, `Modal` and `Dialog` go on the Avoid list, so one word covers what
`*Prompt` and the Changelog already were. Settings (#193, #260) is built as a Popup.

## Renames

| From | To | Why |
|---|---|---|
| `GroundItemsPanel` | `GroundItemsList` | The glossary's **Ground Items List**; it is not a panel. |
| `GroundItemSlotDisplay` | `GroundItem` | One entry of the list. It is not a slot, and `GroundSlotDisplay` is the grid's. |
| `CharacterStatPanel` | `CharacterStatSheet` | A list of stat rows. "Character panel" is Avoid for the **Hero Panel**. |
| `EncounterStatsPanel` | `EncounterStatsText` | Two texts set every frame. Sibling of `HeroShowcaseText`. |
| `DebugPanel` | `DebugTools` | A set of `UnityEvent` targets. "Debug panel" is Avoid for the **Combat Panel**. |
| `MainMenuStatus` | `MainMenuLayout` | Shows and hides buttons per platform and hero; moves out of `Displays/` and `GUI.Displays`. |
| `EntryNoticePanel` | `EntryNoticePopup` | Toggles a child with `SetActive`. |
| `PauseMenuPanel` | `PauseMenuPopup` | Same. Its Cancel rank stays its own. |
| `HeroNamePrompt`, `HeroDeletePrompt`, `SaveExitPrompt` | `HeroNamePopup`, `HeroDeletePopup`, `SaveExitPopup` | One word for one thing. |
| `CancelRank.Modal` | `CancelRank.Popup` | The term. |

Also: the serialized `prompt` field of the two controllers becomes `popup`; the private argument-less
`Refresh()` of `HeroShowcaseText` and `MainMenuLayout` becomes `Repaint`, as `HeroIdentityDisplay` already
has it, so `Refresh` keeps meaning `IDisplay<T>.Refresh`; the tests named for a renamed type are renamed
with it (`DebugPanelPlayModeTests`, `CancelChainTests`' modal cases); doc comments that say "modal" say
"popup", in `docs/agents/coding-conventions.md` too ("the hero prompts and the Changelog panel"). A class is renamed with its `.meta` so prefab and scene
references survive; the serialized-rename rule in `coding-conventions.md` covers the field.

Unchanged, because the word is right: `ChangelogPanelView` (it fills rows from a static file, which is what a
View is, and "Panel" names the `SimplePanel` it requires on the same object: the Changelog Popup is that
panel, the view only fills it and registers its Cancel layer), `SidePanel`, `RunPhasePanel`, `FieldFacePanel`, `BehaviourSlidersPanel`, `BundleVersionView`,
`ItemView`, the `Panel*` toggles and buttons, `InventoryPanels`.

## Notes for open work

- **#266** merges `EnemyView` and `HeroFigure`. The merged component is a `Figure`, not a `View`: a View shows
  static data and a figure is bound to a combatant. After the issue's own change, `EnemyHealthBarDisplay`
  binds a bare `CharacterResource`, which is what `ResourceDisplay.Bind` does; worth folding its name label and
  rarity border into the figure or the display rather than dropping the prefix on a wrapper.
- **#193 / #260** build Settings as a Popup, registered at `CancelRank.Popup`.
- **The Combat Panel is a `SidePanel` in the code** (`FieldFacePanel` types it so), while the glossary keeps
  the two apart. Not decided here; the glossary or the code moves, in its own change.

## Supersedes

The word "Modal" in `2026-10-12-front-end-flow-design.md` and `2026-10-13-menu-smoke-fixes-design.md` reads
"Popup". The behaviour they describe is unchanged.
