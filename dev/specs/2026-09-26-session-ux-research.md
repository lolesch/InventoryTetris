# One session, mouse and keyboard: a UX read of the current build

**Date:** 2026-09-26
**Baseline:** `main` @ `ac8f423` (working tree)
**Question:** a visitor arrives through the GitHub Pages entry point and plays one session
with a mouse and a keyboard. What do they see, what can they do, and where does the build
fail them?

## Scope and method

Primary sources only: the C# under `Assets/Scripts/`, the single scene
`Assets/Scenes/Example.unity`, prefab and ScriptableObject YAML, `ProjectSettings/`, and the
uGUI package source shipped in `Library/PackageCache/`. Unity runtime semantics are quoted
from the Unity 6 manual. Specs in `dev/specs/` and `docs/adr/` are cited as *intent*, never
as evidence of behaviour.

Every claim carries a `path:line`. Claims about how something will feel to a player, rather
than what the code does, are marked **[judgement]**. Questions the YAML cannot answer are
marked **[unsettled]** and collected in section 7.

**The published build is deliberately not the baseline.** The site at
<https://lolesch.github.io/InventoryTetris/> serves `docs/media/InventoryTetris_20231015/`
(`productVersion 1.0.3`) from the `GitPage` branch, roughly two years behind `main`. It is
used here for one purpose only: it establishes that the entry point is a windowed WebGL
canvas in a desktop browser, so the input devices are a mouse and a keyboard — no gamepad,
no touch.

**Nothing here was confirmed in Play mode or in a WebGL build**; the WebGL build module is
not installed on this machine. Section 7 lists what only a running build can settle.

---

## 1. What the browser imposes

### The UI renders at two-thirds of its authored size

The scene has exactly one `CanvasScaler` (`Assets/Scenes/Example.unity:11253-11258`):
`ScaleWithScreenSize`, reference resolution **1920×1080**, `ScreenMatchMode` 0, and
`m_MatchWidthOrHeight: 1`. The shipped uGUI source confirms `MatchWidthOrHeight = 0`
(`Library/PackageCache/com.unity.ugui@23caec89ae27/Runtime/UGUI/UI/Core/Layout/CanvasScaler.cs:78-83`)
and that at match weight 1 the scale factor reduces to `screenHeight / 1080`
(`CanvasScaler.cs:315-325`). No prefab overrides it — `m_ReferenceResolution` appears nowhere
under `Assets/Prefabs/`.

The project's web target is **1280×720** (`ProjectSettings/ProjectSettings.asset:47-48`,
`defaultScreenWidthWeb` / `defaultScreenHeightWeb`), so the scale factor is
`720/1080 = 0.667` and every element renders at two-thirds of its authored size:

| Authored `m_fontSize` | Device px at 1280×720 | Instances |
| --- | --- | --- |
| 12 | **8.0** | 12 in scene |
| 18 (prefab body text) | 12.0 | 20 in prefabs |
| 24 | 16.0 | 5 |
| 28 | 18.7 | 3 |
| 32 | 21.3 | 5 |
| 36 | 24.0 | 1 |

Only 4 of 32 text components in the scene and 2 of 27 in prefabs enable auto-sizing
(`m_enableAutoSizing: 1`), so these are fixed sizes.

The 12pt tier is not incidental — it is the entire run readout. The live encounter stats
including the only hero-HP figure in the build (`Example.unity:7346`), the post-run summary
(`:10005`), and all five slider captions with their five value labels are 12pt with
auto-sizing off, built by `BehaviourSlidersPanel.cs:111-155`.

**[judgement]** Eight device pixels is not readable. The single highest-leverage
presentation fix in the project is the reference resolution or that 12pt tier, and it is a
one-field change if the UI was simply authored at 1080p and never re-checked at the web
target.

**Vertical layout is resolution-invariant.** Because the match weight is height, the logical
canvas is always 1920×1080 tall regardless of window size — so anything that does not fit
vertically does not fit at *any* resolution. That is what makes finding 6 below a real
overflow rather than a small-window artefact.

Horizontally, matching on height leaves width unprotected. At 16:9 the authored 1920 fits
exactly. Narrower than 16:9 and the logical width shrinks: the currently published 960×600
wrapper (16:10) yields 1728 logical px, losing 192px of authored width. **[judgement]** Going
fullscreen on a 16:10 laptop panel will clip the edges of the layout.

### Three runtime settings that shape a browser session

- **One unhandled exception ends the session silently.** `webGLExceptionSupport: 0`
  (`ProjectSettings/ProjectSettings.asset:601`) is "None", which the Unity manual defines as
  *"Any exception thrown causes your content to stop with an error."* **[judgement]** For a
  portfolio entry point this is the worst failure mode available: a null reference yields a
  frozen canvas, no message, and no recovery but a reload — which also erases all progress.
- **Losing focus freezes a run.** `runInBackground: 0` (`:86`); the manual describes the
  enabled state as letting *"your content continue to run when the canvas or the browser
  window loses focus."* The encounter is a live clock-driven simulation
  (`docs/adr/0008-encounter-is-a-live-simulation.md`), so alt-tabbing mid-fight stops the
  fight rather than letting it resolve.
- **Legacy input only.** `activeInputHandler: 0` (`:742`) — no `com.unity.inputsystem`.
  Already documented in issue #69.

### Three absences, each established by exhaustive search

- **No audio anywhere.** No `.wav`/`.mp3`/`.ogg` in `Assets/`, no `AudioSource` or
  `AudioClip` in the scene, no script under `Assets/Scripts/` referencing either. Three
  `//TODO: implement audio feedback` markers record the intent. Every acknowledgement in the
  game is visual or absent — which is what makes the silent no-ops below so costly.
- **No persistence anywhere.** No `PlayerPrefs`, `JsonUtility`, `File.Write`,
  `Application.persistentDataPath`, or save/load type in `Assets/Scripts/` or
  `Assets/Submodules/Utility/`. A refresh erases level, XP, gear, stash, wallet and corpse.
  This contradicts `docs/adr/0009-death-is-corpse-recovery-not-forfeit.md`, which specifies a
  Corpse that *"persists between Sessions"* — an ADR describing a guarantee the code cannot
  currently make.
- **No onboarding.** No tutorial, help, or how-to-play string in `Assets/Scripts/`.

---

## 2. Five things the player can never see

`SimplePanel` is explicit that panels are shown by fading, not activation: *"A panel should
always stay enabled, only its canvasGroup alpha is set to 0"*
(`Assets/Submodules/Utility/UI/Panels/SimplePanel.cs:11`); `Toggle` only tweens the
`CanvasGroup` (`:68`, `:124`, `:168`). Across `Assets/Submodules/Utility/` the only
`SetActive` calls are in `Tools/PrefabPool.cs:37-67` (pooling); across `Assets/Scripts/` all
13 target child widgets — an icon, a stat row, a label — never a panel root.

**So a GameObject authored inactive can never be shown.** Walking the scene hierarchy yields
exactly five topmost inactive objects:

| Inactive root | Contents |
| --- | --- |
| `Canvas/CombatContext` (`:25257`) | `HealthGlobe`, `ResourceGlobe`, `ExperiencePanel` |
| `Canvas/CenterPanels` (`:3012-3017`) | the drop-to-floor sink, the debug item-spawn panel, and the only instructional string |
| `…/CombatPanel/CombatPanel/Items` | legacy debug item-selection row |
| `…/CombatPanel/CombatPanel/Settings` | legacy debug add/remove + amount slider |
| `…/DummyHealth_BG/Percent` | an HP percentage readout |

Two of these matter a great deal:

1. **The hero's health, resource and XP readouts never appear in any session.** All three are
   children of `Canvas/CombatContext`, whose root is `m_IsActive: 0`. Their own children are
   active, which reads as an authoring accident rather than a decision. It also contradicts
   `HeroCombatant.cs:17-19`, whose doc says damage routes through `BaseCharacter` *"so the
   globes reflect sim state"*. The only live health bar during a run belongs to the training
   **DummyTarget**, not the hero (`Example.unity:16974`). **[judgement]** The player has no
   persistent readout of health, resource or progress at any point; issues #93 and #94 (hero
   resources, enemy HP bars) both sit downstream of a container that is switched off.
2. **There is no way to throw an item away.** `DropToFloorSlotDisplay` — the only destructive
   sink — is under the inactive `CenterPanels`, along with the project's one piece of
   instructional text, *"Drop items on the floor to delete them"* (`:8474`). A full bag has no
   manual remedy. Had it been reachable, it would also have destroyed items without
   confirmation (`DropToFloorSlotDisplay.cs:15-22`).

The `CombatPanel` itself is unaffected: it is an active sibling at
`Canvas/LeftSidePanels/CombatPanel`, faded in for the length of a run by
`MinimapController.cs:140-141`. The behaviour sliders and the run's HP text ride on it and do
appear.

---

## 3. The input model

### The complete keyboard surface is four keys

| Key | Effect | Owner |
| --- | --- | --- |
| `I` | Hero panel | `SidePanelToggle.cs:57,77-86`; `hotkey: 105` at `Example.unity:12380` |
| `S` | Stash panel | `hotkey: 115` at `Example.unity:19335` |
| `V` | Vendor panel | `hotkey: 118` at `Example.unity:6663` |
| `Escape` | cancels an in-flight drag, nothing else | `DragProvider.cs:115-125` |
| — | the Healer has `hotkey: 0`, i.e. none | `Example.unity:9119` |

`LeftShift` (quick-move) and `LeftControl` (halve a stack) are modifiers, not bindings
(`InventorySlotDisplay.cs:153-158`), and both are read as the **left-hand key only** — right
Shift and right Ctrl do nothing.

**Nothing communicates any of this.** No `m_text` in the scene or any prefab contains
"press", "shift", "ctrl", "esc", "key", "click" or "drag", and `InteractiveElement` carries
`//TODO: show tooltip on hover` with no tooltip implementation
(`Assets/Submodules/Utility/UI/InteractiveElements/InteractiveElement.cs:12`). **[judgement]**
Every binding is source-only knowledge. Issue #69 scopes a rebindable input service, but
*discoverability* is a separate gap that ticket does not cover.

### Shift-click is modal, and silent when it does nothing

`QuickMoveResolver.Resolve` (`Assets/Scripts/InventorySystem/Containers/QuickMoveResolver.cs:36-55`)
maps one gesture to different outcomes depending on which panel happens to be open:

- Stash open → backpack ⇄ Stash; Equipment → Stash
- Vendor open → backpack and **Equipment** → Sell Basket; basket → backpack
- `None`, `Hero`, `Healer` → `QuickMoveIntent.None` — nothing at all

Only two of the five `InventoryContext` members are wired (`InventoryContext.cs:18-25`). The
handler is a bare `return` with no feedback (`AbstractSlotDisplay.cs:275-276`), and its own
comment concedes *"with neither panel open … this is a no-op."* With no audio in the project,
the gesture fails with **zero** acknowledgement.

**[judgement]** This is the sharpest learnability problem in the build. A player who learns
shift-click at the Stash, then tries it with only the Hero panel open, gets nothing and will
reasonably conclude the feature is broken rather than context-dependent. The source calls
these *"stated outcomes, not omissions"* (`:21-22`), which is a defensible design position —
but a deliberate refusal still has to say no. Note #63's last acceptance criterion (drop to
ground when no panel is open) would give the `None` case a meaning, and #86 rebuilds this
table.

Two further consequences of the same table:

- With the Vendor open, one shift-click takes a **worn item** off the paperdoll into the sell
  basket (`:50-51`), and returning it lands in the backpack rather than its original slot
  (`:48-49`) — the round trip silently unequips.
- `if (source == store) return QuickMoveIntent.Buy` (`:33-34`) fires for **any** Store-role
  container in **any** context. Because the Healer's grid is also bound to the Store (section
  5), shift-clicking a slot on the panel labelled "Healer" **buys the item** — while the same
  file's comment states *"the Healer has no containers yet"* (`:19-22`).

### Middle-click picks items up

`MoveItem` handles right-click explicitly (`InventorySlotDisplay.cs:106`) then falls through
to an **unfiltered** `BeginDrag` (`:164`). uGUI's `ProcessMouseEvent` runs `ProcessMousePress`
*and* `ProcessDrag` for the middle button as well as the right
(`Library/PackageCache/com.unity.ugui@23caec89ae27/Runtime/UGUI/EventSystem/InputModules/StandaloneInputModule.cs`,
`ProcessMouseEvent`). So middle-clicking a slot lifts its item onto the cursor.
`AbstractButton` filters correctly to Left
(`Assets/Submodules/Utility/UI/InteractiveElements/Buttons/AbstractButton.cs:32`), so buttons
and slots disagree about which buttons count.

**[judgement]** In a browser, middle-click is autoscroll or open-in-new-tab, so this is both
an accidental pickup and a gesture the page itself may react to.

### Right-click is the primary verb, and the context menu is unresolved

Equip, unequip, consume and buy all hang off `eventData.button == Right`
(`InventorySlotDisplay.cs:106`, `EquipmentSlotDisplay.cs:106`, `VendorSlotDisplay.cs:104`).
The project ships no `.jslib`, no `Assets/Plugins`, no `DllImport`, and no custom WebGL
template (`ProjectSettings/ProjectSettings.asset:608` = `APPLICATION:Default`), so nothing in
*this repo* suppresses the browser context menu. Whether Unity 6000.6's own default template
or runtime calls `preventDefault` on `contextmenu` could not be verified here — section 7.

### A same-slot drag reverts, harmlessly

`OnPointerClick` and `OnBeginDrag` have identical bodies (`AbstractSlotDisplay.cs:85-91`,
`:177-183`): if something is on the cursor, drop it; otherwise lift. uGUI leaves
`eligibleForClick` true when one component is both `pointerPress` and `pointerDrag`
(`PointerInputModule.cs:387-398`), and `ReleaseMouse` fires the click branch and the drop
branch as independent `if`s (`StandaloneInputModule.cs:206-235`). So pressing, moving past the
10px threshold, and releasing **inside the same slot** lifts the item and immediately puts it
back.

The follow-on `OnDrop` is a no-op: `DropItem` returns on `!package.IsValid`
(`InventorySlotDisplay.cs:23-24`) and the first drop already cleared the cursor
(`DragProvider.cs:268`). **There is no duplication and no item loss.**

That shared body is also a real strength: because click and drag do the same thing, **both**
interaction models work — Diablo-style click-to-pick-up / click-to-place *and*
press-drag-release. Releasing over empty space leaves the item on the cursor, which is
coherent Diablo behaviour, though nothing explains it.

### Bound to nothing

Double-click, right-drag as a distinct gesture, number keys, `Tab`, `Delete`, and
**scrolling** — there is no `IScrollHandler` anywhere in `Assets/`, and the repo's only
`ScrollRect` (`Assets/Prefabs/PLAYER.prefab:667`) is unreferenced by the scene. Nothing in the
UI can be scrolled or paged, which matters for a 10×16 shelf.

---

## 4. The session, step by step

### Frame one shows no text at all

Every `SimplePanel` starts at alpha 0 (`SimplePanel.cs:63`) and the Inventory Context starts
at `None` (`InventoryContextState.cs:23`), so the opening screen is the Town minimap's
unlabelled icons and nothing else — no title, no instruction, no tooltip
(`InteractiveElement.cs:12`).

Nine minimap controls (three town stops, two dead stops, two locations, Go Venture, To Town)
have no label or tooltip in any state: bare 60×60 icons — 40 device px at the web target — at
`m_NormalColor` alpha **0.2** (`Example.unity:26979-26982` and siblings). Disabled renders as
black at 50% alpha, so **Smith and Pub**, both `m_Interactable: 0` with `panel: {fileID: 0}`
(`:26998`, `:22012`), read as smudges rather than as disabled controls. "This panel is open"
is conveyed by a 1.06× scale difference alone — both toggle sprites are unwired everywhere.
The `Button`/`Toggle` prefabs do have captions, but they are hidden until hover and drawn from
the **GameObject name** (`ShowNameOnHover.cs:10-16`, label authored inactive).

**[judgement]** A visitor's first act is to click an unlabelled 40px smudge and hope, and two
of the nine options do nothing at all.

### With the shipped defaults, a run never ends on its own

`SimulationProvider` is in **no** scene and **no** prefab — its GUID
(`34a52130df184bd4be63b7dddbbfc41a`) appears zero times in either. It is created at runtime,
so its `[SerializeField]` values are the C# field initialisers: `retreatHealthFraction` is
unset, i.e. **0**, and `recallBagFillFraction = 1f` (`SimulationProvider.cs:51-52`). `Awake`
pushes both into the live behaviour (`:124-125`), and the sliders only *reflect* them back —
`BehaviourSlidersPanel` reads from the behaviour and writes the slider with
`SetValueWithoutNotify` (`BehaviourSlidersPanel.cs:175-176`, `:228`) — so the authored slider
positions never override the defaults.

The retreat test therefore needs `healthFraction <= 0`, which is death, and death is checked
first (`HeroBehaviour.cs:64`, `EncounterSimulation.cs:224-241`); the recall test needs a
**100%** full grid. Encounters themselves are endless (`EncounterSimulation.cs:243-244`,
`:262-285`). The only exits from the field are dying and clicking To Town.

**[judgement]** The hero's two headline autonomy features ship switched off, and because the
Combat Panel is hidden in Town (`MinimapController.cs:140-141`) the player can only find the
sliders *after* committing to a run.

### The vendor's Cancel button rerolls the shop

The `Cancel` button in the Sell Basket header carries a persistent UnityEvent calling
`RestockStore` on the `INVENTORY PROVIDER` (`Example.unity:17828-17834`, named at `:17840`),
and the same `Button` is `SellBasketDisplay.cancelButton`, which adds `Cancel` as a second
listener (`SellBasketDisplay.cs:142`). `RestockStore` opens with `RemoveAllItems(Store)`
(`InventoryProvider.cs:249-251`).

**So backing out of a staged sale destroys the vendor's entire unsold stock and rolls a fresh
20 items.** This looks like a leftover debug hookup rather than an intended rule, and it
silently violates ADR-0012's model of the Supply as finite stock persisting until a Restock.
**[judgement]** This is the most damaging single defect found: an ordinary, clearly-labelled
"cancel" wipes state the player was shopping through.

### The Vendor panel does not fit the screen

The Supply shelf is 10×16 and the Sell Basket 5×3 (`InventoryProvider.cs:96-104`). At 60×60
cells with 10px padding (`Assets/Prefabs/InventoryDisplay.prefab:124-132`) that is 620×**980**
and 320×**200** logical px, both with `ContentSizeFitter` VerticalFit = PreferredSize
(`InventoryDisplay.prefab:109-110`, `m_VerticalFit: 2`), inside a panel whose
`VerticalLayoutGroup` padding is top 90 / bottom 10 (`Example.unity:6779-6783`) — 980 logical
px available out of the invariant 1080.

Required is roughly 1180. There is **no `RectMask2D` anywhere in the scene** (zero occurrences),
so nothing clips: the basket's bottom row and its Total / Confirm / Cancel footer render an
estimated 190–200 logical px below the canvas edge. Because vertical layout is
resolution-invariant (section 1), **no window size fixes this.** The Stash and Healer panels
fit at *exactly* 980 — zero slack.

**[unsettled]** Precisely which rows fall off depends on how `VerticalLayoutGroup` resolves an
over-constrained column whose children report min == preferred. The overflow itself follows
from the arithmetic; the exact casualty list needs one Play session.

### Money is invisible, and it competes with loot for space

The Wallet is backed by the backpack itself — `Wallet = new Wallet(Inventory, currencyMinter)`
(`InventoryProvider.cs:175`) — so coins occupy loot cells in the 10×6 grid and count toward
the bag gauge (`ContainerBagGauge.cs:29-46`). `Wallet.OnBalanceChanged` has exactly one
subscriber in the whole codebase: the vendor slot's red "can't afford" tint
(`VendorSlotDisplay.cs:32-56`).

There is **no coin-balance display anywhere**. `CurrencyDisplay.Refresh` has a single caller,
`PreviewDisplay.cs:127`, and the scene's one `CurrencyDisplay` instance sits in the Sell Basket
footer (`Example.unity:28467`, `:28473`) where nothing ever refreshes it — so it ships showing
its authored placeholders — `(###)` (`CurrencyDisplay.prefab:180`) and `##` on each coin
amount (`CoinDisplay.prefab:163`) — directly beside the one label that does update. Coins paid into a
full bag are destroyed silently (`Wallet.cs:122-133`).

**[judgement]** The player must read their net worth by eye off coin stacks in the grid, while
those stacks crowd out the loot the run was for, next to a permanent `(###)`.

### Prices are ambiguous, and one sale shows three different totals

`PreviewProvider.cs:87-89` passes a `priceOverride` only for vendor slots, rendered through the
same `goldValue` widget as sell value (`PreviewDisplay.cs:72-75`) — a 1.5× markup
(`VendorTransaction.cs:25`) with nothing distinguishing buy price from sell price. A single
sale then reports three numbers: per-item truncated coins
(`Assets/Scripts/InventorySystem/Data/Statistics/Currency.cs:43`), an unformatted
`PreviewValue(basket).ToString()` on the total label (`SellBasketDisplay.cs:190`), and the
amount actually paid, which is re-quantised through `new Currency(...)` (`SellBasket.cs:110`).

### Loot that does not fit is destroyed, without a choice

Overflow goes to `LootFlow._groundDrops` (`LootFlow.cs:135`), which has no pickup UI — only a
count in the Combat Panel text (`BehaviourSlidersPanel.cs:123`) — and is cleared at run end on
both outcomes (`SimulationProvider.cs:271-278`). With the discard sink switched off (section 2),
the player can neither retrieve ground loot nor make room for it. Issue #63 builds the display.

### Stat and affix rows contain no words

`CharacterStatDisplay.cs:46` builds its row as
`$"{stat.TotalValue:0.##}\t{modDetailText}"` with the stat name **commented out** —
`//{statName}` — so character-sheet rows read as a bare number and a formula, e.g.
`142  (100 + 20) * 1.1 * 1.08`. The overwrite branch ships the literal developer placeholder
`"overwritten by: implementStatModSource"` (`:43`). Affixes format as `{Modifier} {Range}`
(`CharacterStatModifierDisplay.cs:30`) → `+ 14 (5, 20)`, identified by icon only.

**[judgement]** The character sheet is unreadable without the source open, and a player can
reach a state where the UI shows them an unimplemented internal string.

### Consuming an item reports itself to a console the player does not have

Right-clicking a consumable removes one, applies no effect, and logs
`Debug.Log($"Consuming {…}")` (`InventorySlotDisplay.cs:110-117`). In a WebGL release build
that goes to the browser console. With no audio and no on-screen message, the item vanishes
with no player-facing acknowledgement whatsoever.

### The corpse cannot be found, and locations have no names

Death buries the whole non-currency bag (`ContainerSettlementBag.cs:23-39`), but
`SimulationProvider.Settlement` is `private` with no accessor (`:74-75`) and nothing renders
it: the player is never told a corpse exists, what is in it, or where — and per section 1 it
does not survive a reload either, against ADR-0009. The revive is also a **full** heal
(`PlayerWalletLedger.cs:36-37`), not the low fraction the MVP spec describes.

A location's `displayName` is rendered in exactly one place, `SimulationDebugPanel.cs:67-68`,
which compiles only under `#if UNITY_EDITOR` (`SimulationProvider.cs:110-113`) with
`show = false` (`SimulationDebugPanel.cs:19`). It is absent from a player build entirely, so
in-game the locations are anonymous dots.

---

## 5. The Healer is a second copy of the vendor's shelf

Two container displays are authored `role: 4` = `ContainerRole.Store`
(`Example.unity:5448-5449` and `:26027-26029`; `ContainerRole.cs:12-23`), both with
`slotDisplayPrefab = SlotDisplayVendor` (`:5452-5454`, `:26030-26033`). So the panel labelled
"Healer" binds to the same Supply container the Vendor uses, shows the same stock, and — per
section 3 — **sells from it on shift-click**.

It heals nothing. The only heal path in the codebase is `PlayerWalletLedger.ReviveIfDown`
(`:30-38`), reached only from `RunSettlement.Settle`. Its hotkey is 0 where the layout spec
asks for `H` plus an instant refill
(`dev/specs/2026-09-08-arpg-screen-layout-design.md:97`, `:170`). Issue #58 covers the intended
behaviour; the duplicate Store binding is not in that ticket.

---

## 6. Findings, ranked

Severity is my judgement: **S1** destroys state or blocks the loop, **S2** blocks
understanding of core state, **S3** is friction or polish.

| # | Finding | Sev | Evidence | Ticket |
| --- | --- | --- | --- | --- |
| 1 | Sell Basket **Cancel** also calls `RestockStore`, destroying all unsold vendor stock | S1 | `Example.unity:17828-17840`; `InventoryProvider.cs:249-251` | none — new |
| 2 | Hero health / resource / XP live in an inactive root and can never be shown | S1 | `Example.unity:25257`; `SimplePanel.cs:11` | blocks #93/#94 |
| 3 | No persistence at all; a refresh erases everything, against ADR-0009's Corpse guarantee | S1 | exhaustive grep; `docs/adr/0009-…` | none — new |
| 4 | Runtime-created `SimulationProvider` ships retreat = 0 and recall = 100%, so a run never self-terminates | S1 | `SimulationProvider.cs:51-52`, `:124-125`; `BehaviourSlidersPanel.cs:175-176` | none — new |
| 5 | No way to discard an item — the only sink is in an inactive root | S1 | `Example.unity:3012-3017` | #63 (partly) |
| 6 | Vendor panel needs ~1180 logical px in 980 and has no mask, so the basket footer and Confirm/Cancel fall off screen at every resolution | S1 | `InventoryProvider.cs:96-104`; `InventoryDisplay.prefab:106-132`; `Example.unity:6782` | none — new |
| 7 | Shift-click on the "Healer" shelf **buys**, because it is the Store container | S2 | `QuickMoveResolver.cs:33-34`; `Example.unity:5448-5454` | #58 (partly) |
| 8 | Shift-click is modal and fails silently in 3 of 5 contexts | S2 | `QuickMoveResolver.cs:36-55`; `AbstractSlotDisplay.cs:275-276` | #86, #63 |
| 9 | No coin balance anywhere; the one `CurrencyDisplay` is never refreshed and ships showing `(###)` | S2 | `PreviewDisplay.cs:127`; `CurrencyDisplay.prefab:180`; `Example.unity:28467` | none — new |
| 10 | Coins occupy backpack cells and are destroyed silently when it is full | S2 | `InventoryProvider.cs:175`; `Wallet.cs:122-133` | none — new |
| 11 | The whole run readout is 12pt → 8 device px at the web target | S2 | `Example.unity:7346`, `:10005`, `:11253-11258`; `CanvasScaler.cs:315-325` | none — new |
| 12 | Stat rows have the stat name commented out; affixes are icon-only; a dev placeholder string can reach the screen | S2 | `CharacterStatDisplay.cs:43`, `:46`; `CharacterStatModifierDisplay.cs:30` | none — new |
| 13 | No key binding, tooltip or affordance is ever communicated | S2 | `InteractiveElement.cs:12`; exhaustive `m_text` grep | #69 (rebinding only) |
| 14 | `webGLExceptionSupport: None` — one unhandled exception freezes the canvas silently | S2 | `ProjectSettings.asset:601` + Unity manual | none — new |
| 15 | The "Healer" duplicates the vendor shelf and heals nothing | S2 | `Example.unity:5448-5454`; `PlayerWalletLedger.cs:30-38` | #58 (partly) |
| 16 | Overflow loot is unreachable and deleted at run end | S2 | `LootFlow.cs:135`; `SimulationProvider.cs:271-278` | #63 |
| 17 | The Corpse is unobservable | S2 | `SimulationProvider.cs:74-75` | none — new |
| 18 | Buy and sell price share one unlabelled widget; one sale reports three different totals | S2 | `PreviewProvider.cs:87-89`; `SellBasketDisplay.cs:190`; `SellBasket.cs:110` | none — new |
| 19 | Middle-click lifts an item; slots do not filter the pointer button | S3 | `InventorySlotDisplay.cs:164`; `AbstractButton.cs:32` | #69 (adjacent) |
| 20 | Consuming an item acknowledges only via `Debug.Log`, and applies no effect | S3 | `InventorySlotDisplay.cs:110-117` | none — new |
| 21 | No audio anywhere, so nothing can acknowledge non-visually | S3 | exhaustive search | none — new |
| 22 | `runInBackground: 0` freezes a live run when the tab loses focus | S3 | `ProjectSettings.asset:86` + Unity manual | none — new |
| 23 | Nine unlabelled minimap icons at alpha 0.2; Smith and Pub are dead and read as smudges | S3 | `Example.unity:26973-26998`, `:22012` | none — new |
| 24 | Locations are never named outside the editor-only debug panel | S3 | `SimulationDebugPanel.cs:67-68`; `SimulationProvider.cs:110-113` | none — new |
| 25 | `LeftControl` is both the compare modifier and the split-stack modifier | S3 | `PreviewProvider.cs:86` vs `InventorySlotDisplay.cs:153` | #69 |
| 26 | Right Shift / right Ctrl are dead; nothing anywhere scrolls | S3 | `InventorySlotDisplay.cs:153-158`; no `IScrollHandler` | #69 |
| 27 | Slot hover-expand is 1 logical px ≈ 0.67 device px, i.e. invisible | S3 | scale factor + slot prefab | none — new |
| 28 | A drag beginning and ending on one slot silently reverts (no data loss) | S3 | `AbstractSlotDisplay.cs:85-91`, `:177-183` | none — cosmetic |

**[judgement] If only four things are fixed:** #1, because it destroys state behind an
innocuous label; #2, which restores the game's basic status readout for one checkbox; #6,
because the vendor flow the last three commits built cannot be completed if Confirm is off
screen; and #11, a one-field change that makes the entire UI legible at the size it ships at.

### What is already right

Worth recording, because these are load-bearing and easy to break:

- **One predicate drives both the drop and the red "can't drop" tint**, per slot type
  (`DragProvider.cs:138-145` plus each `WouldAcceptDrop`), so the warning and the placement
  can never disagree.
- **The tooltip and its compare column self-clamp to screen edges** via cursor-derived pivots
  (`PreviewProvider.cs:59-66`, `:95-112`).
- **Per-affix comparison computes a real total-value delta, colour-coded, in both directions**
  (`CharacterStatModifierDisplay.cs:34-74`, `LocalPlayer.cs:171-186`).
- **The Sell Basket is the best-served flow**: previewed total, Confirm/Cancel gated on
  content, Supply dimmed to alpha 0.5 and non-interactive during a staged sale, auto-cancel on
  leaving the Vendor.
- **Equipment slots keep per-type silhouettes** on an Image the item repaint never touches, so
  the empty-slot hint survives.
- **Both interaction models work** — click-to-pick-up/place and press-drag-release — from one
  shared handler body.
- Refused drops mutate nothing; guards precede the transaction
  (`InventorySlotDisplay.cs:29`, `:35`), and `ReturnToOrigin.cs:38-70` conserves item count.

### Doc and code contradictions found along the way

- `MinimapController`'s class doc claims location toggles stay interactable mid-run and that
  `OnFieldSelectionChanged` recalls-and-re-sends (`:35-39`); `ApplyFace` sets
  `interactable = _inTown` (`:193-195`) and the handler returns unless `InTown` (`:214`).
- The layout spec's "What dies" list
  (`dev/specs/2026-09-08-arpg-screen-layout-design.md:196-197`) marks `SimulationDebugPanel`,
  `MenuToggles`, `StoreToggle` and `StashToggle` as removed; all still exist
  (`SimulationProvider.cs:110-113`; `Example.unity:15758`, `:14136`, `:14808`).
- `QuickMoveResolver`'s comment says *"the Healer has no containers yet"* (`:19-22`); it is
  bound to the Store.
- `HeroCombatant.cs:17-19` says damage routes through `BaseCharacter` *"so the globes reflect
  sim state"*; the globes are switched off.
- ADR-0009 promises a Corpse persisting between sessions; nothing persists.
- ADR-0012 treats the Supply as finite until a Restock; the Cancel button restocks it.

---

## 7. Open questions only a running build can settle

1. **Does right-click raise the browser context menu over the canvas?** Nothing in this repo
   suppresses it and right-click is the primary action verb. Settle by building for WebGL and
   right-clicking a slot, or by grepping the installed
   `PlaybackEngines/WebGLSupport/BuildTools` for `contextmenu` — the module is not installed
   on this machine.
2. **Exactly which Vendor-panel rows fall off screen**, given an over-constrained
   `VerticalLayoutGroup` whose children report min == preferred. The overflow follows from the
   arithmetic; the casualty list does not.
3. **Does the Sell Basket wire up at runtime?** `SellBasketDisplay`'s host and `confirmButton`
   (`Example.unity:2245`, `:2234`) target object ids present in no revision of
   `InventoryDisplay.prefab`. Most likely Unity's computed nested-prefab ids, in which case all
   is well; if stale, there is no `SellBasketDisplay` at runtime and all four panel titles read
   `Inventory` (`Assets/Prefabs/Title.prefab:169`).
4. **Do the Hero Panel's 14 absolutely-positioned paperdoll slots fit the height the layout
   hands them** (`LayoutElement m_MinHeight: 520`, `EquipmentDisplay.prefab:138`), or do they
   overlap the backpack grid?
5. **Does a content-sized tooltip plus its two compare panels fit** the centre strip left when
   both side panels are open? The Hero Panel opens with *every* context
   (`InventoryContextState.cs:94-97`).
6. **Do drops land where they look like they will?** `DragProvider` uses a pixel-constant
   `slotSize = 60f` (`:36-37`) against a live `GridLayoutGroup.cellSize` on a
   `ScaleWithScreenSize` canvas — a plausible source of off-by-one-cell drops at any window
   size other than 1080p.
7. **Does the preview panel flicker?** Three `icon` images in `StaticPreviewDisplay.prefab`
   have `raycastTarget: 1` on a panel that follows the cursor by ±10px
   (`PreviewProvider.cs:45-55`).
8. **Which child does the `m_IsActive: 0` override at `Example.unity:31407-31410` deactivate?**
   It targets fileID `940184344107485643` of `EquipmentDisplay.prefab`, an id absent from that
   file — a remapped nested-prefab object. Worth an editor look.

---

## Appendix: how this was produced

Three parallel read-only investigations over `main` — input mechanics, session flow,
presentation — with their raw notes in the session scratchpad (`ux-A-input.md`,
`ux-B-session.md`, `ux-C-presentation.md`). Load-bearing claims were then re-verified against
the source before inclusion. Two corrections made during that pass are worth recording, as
both were plausible and wrong:

- The same-slot drag revert was initially read as a possible item-duplication path. It is not:
  `DropItem` guards on `!package.IsValid` (`InventorySlotDisplay.cs:23-24`). Finding 28 is
  cosmetic.
- The `#85` commit line "the announcements go" describes removing an internal
  panel-announces-its-context mechanism, not player-facing notifications. No feedback
  regression there.
