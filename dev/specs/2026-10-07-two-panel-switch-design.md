# The two-panel switch and the Alt peek

Date: 2026-10-07
Status: Scoping spec - not a plan. Slice into a GitHub epic with `/to-tickets`, build with
`/implement`.
Base: `main` at `9c533e54`; the prototype lives on `prototype/sold-tab-peek`.
Derived with `/rederive`; the route audited once with `/drift-review` over the built slice (#127
and the 2026-10-05 spec amendment).
Amends: `dev/specs/2026-10-02-sold-tab-design.md` - its "Tabs are `PanelToggle`s in a group of
their own" decision (the tab pair becomes a two-panel switch) and its Out of Scope line about
switching to the Sold tab (a held key now shows it; a sale still never does).

## Problem Statement

A sold item is one glance away only if the player leaves the Supply: to check what they just
sold, or to compare it with the shelf, they click the Sold tab and click back. The player wants
to look at the other tab for as long as a key is held, and land back where they were.

A prototype did this by finding the Sold tab's toggle by its object name and rewriting the
group's selection, then rewriting it back. It works, but it needs guards the moment the
situation is not the happy one: a group with no previous tab, a panel that closed during the
hold, a key held before the panel opened, a group with more than two tabs. Each guard is
evidence that the tab group is the wrong shape for "exactly two, and a peek shows the other".
The group can hold any number of tabs, so "the other one" is not something it can say.

## Solution

A tab pair is a **two-panel switch**: one **driver toggle** owns a single bool, and exactly
two panels follow it - one while the toggle is off, the other while it is on. The two tab
buttons the player sees stay as they are: the driver is one of them, and the other is an inert
toggle in the same group, which forbids switch-off, so clicking either button flips the bool.

The **peek** flips that bool while a key is held and sets it back on release. It is a real
selection, not a view-only overlay: it goes through the group, so both tab buttons follow. On
release it restores the state it captured only if nothing else has written the bool since the
peek began. A click on a tab and a panel's reset on closing are both writes, and each cancels
the restore. Holding the key, closing the panel, reopening on the Supply and releasing leaves
the panel on the Supply.

The key is Alt. Shift moves items, Ctrl manipulates stacks, Alt shows more information about
the player's things.

## User Stories

1. As a player, I want to hold Alt to see the other tab of the selling panel, so that I can
   glance at what I sold without clicking away from the Supply.
2. As a player, I want letting go of Alt to bring me back to the tab I was on, so that a peek
   costs no clicks.
3. As a player, I want the same behaviour in the Vendor and the Healer, so that both selling
   Town Stops feel alike.
4. As a player, I want a peek from the Sold tab to show the Supply, so that the peek is the
   other tab in both directions.
5. As a player, I want a click on a tab during a peek to keep that tab, so that a peek can
   become a choice.
6. As a player, I want closing the panel while I hold Alt, then reopening it, to show the
   Supply and stay there when I let go, so that a panel always opens on the Supply.
7. As a player, I want holding Alt before I open the panel to peek as soon as it opens, so
   that I need not time the key press.
8. As a player, I want a peek to do nothing while the panel is closed or hidden behind another,
   so that Alt never moves a tab I cannot see.
9. As a player, I want Alt+Tab away from the game to let go of the peek, so that a held key
   never sticks.
10. As a player, I want selling, buying, dragging and dropping to work exactly as before while
    I peek, so that the peek is only a look.
11. As a player, I want to drop a carried item on the Sold tab while peeking at it and have it
    sell, so that the peek is a legitimate way to reach the sale target.
12. As a player, I want an item I carry from the Supply and drop on the Sold tab to return to
    its origin free, so that peeking can never make a purchase in progress a sale.
13. As a player, I want a sale never to switch the tab, so that selling several items in a row
    leaves the view where I put it.
14. As a player, I want the tab buttons to look and click as they do now, so that nothing about
    the tabs has to be relearned.
15. As a player, I want the Alt tooltip detail and the peek to coexist on a hover, so that Alt
    stays "show me more".
16. As a developer, I want a tab pair's exclusivity to be a bool, so that a third tab, no
    active tab, or both panels showing cannot be authored.
17. As a developer, I want the peek to name its driver by a serialized reference, so that
    renaming a scene object cannot silently disable it.
18. As a developer, I want the peek's restore to be one rule - nothing else wrote the bool -
    so that a click, a reset or a Run phase change cancels it without a case of its own.
19. As a developer, I want the group's reset on closing to write the bool through the same path
    as a click, so that the peek needs no knowledge of resets.
20. As a developer, I want the peek's "key held" and "panel open" answers injectable, so that
    a test drives it without a keyboard or a canvas.
21. As a developer, I want a warning when a tab pair is authored wrongly (no inert partner, a
    group that lets the user switch off, the first member not the off-state toggle), so that a
    wiring slip is loud.
22. As a developer, I want the Map's InTown and InFields faces to be able to use the same
    switch later, so that the two-panel shape is not invented twice.
23. As a developer, I want the prototype and its debug checkbox deleted when the peek is
    settled, so that no throwaway outlives the decision.

## Implementation Decisions

- **The Sold tab is a two-state switch.** One toggle owns a bool, and exactly two panels follow
  it: the Supply panel while the bool is off, the Sold panel while it is on. The toggle is the
  driver; panels follow, never the other way round.
- **The tab buttons stay two.** The driver is the Sold button. The other is an inert toggle
  with no panel, in the same `ToggleGroup`, which forbids switch-off. Clicking the inert one
  switches the driver off through the group; clicking the driver while it is on is refused.
  The group is the one mirror of the bool, as it already is for any two toggles; no second
  piece of state is added.
- **The driver is a tab toggle, not a Side Panel toggle.** It never requests an Inventory
  Context: it chooses a view inside a panel that is already open. This carries over the spec
  2026-10-02 decision unchanged.
- **The peek is its own component on the selling panel,** one per panel, with a serialized
  reference to the driver. It does nothing unless its panel is open, evaluated every frame as
  "key held and panel open", not on the key's down edge. While that holds and the bool is at
  its captured value, it sets the bool to the other state through the group; when it stops
  holding, it restores.
- **One restore rule.** The driver counts every write to its bool. The peek remembers the count
  after its own write and restores only if the count is unchanged on release. A click, the
  group's reset on closing and any future driver of the bool (a Run phase) are all writes and
  cancel the restore without a case each.
- **A reset is a write through the same path.** The group resets to its first member when the
  panel finishes closing (the 2026-10-05 amendment, already built); the first member must be
  the off-state (Supply) toggle. That write reaches the driver's own toggle callback exactly
  as a click does.
- **Where a peek is not allowed to reach.** The peek touches the bool and nothing else: not a
  sale, not a drag, not a purchase, not the Inventory Context. A held purchase dropped on the
  Sold tab returns to its origin free, as spec 2026-10-02 already says.
- **Authoring is checked in the editor.** A warning, in the style of the `LocationToggle`
  warning, when: the driver has no group; the group allows switch-off; the group's first
  member is not the off-state toggle; or the driver's panels are not both set.
- **The key is Alt.** It stays Alt for now, decided by feel; the tooltip's roll-range modifier
  fires with it on a hover, which is accepted. The key is read through the existing held-key
  helper so the tooltip and the peek agree on what Alt is.
- **Where the code lives.** The two-state toggle is generic UI and belongs with the other toggles
  in the Utility submodule; the peek reads the game's modifier-key helper and belongs in the
  game's GUI. The peek's "key held" and "panel open" answers are injected, defaulting to the
  helper and the panel's reachability, in the manner of the pause hotkey's injected key
  predicate.
- **Deleted at the end:** the prototype component, its self-installing hook and its debug
  checkbox. No `PlayerPrefs` keys outlive them.
- **Scene migration.** Both selling panels' tab pairs change component on their Sold toggle and
  gain the peek. No scene or prefab may be left holding a missing-script reference or an
  orphaned serialized panel field.
- **Documentation.** `CONTEXT.md` / `GLOSSARY.md` gain **two-panel switch** and **peek**; spec
  2026-10-02's Sold tab decision is amended, as listed under Amends.

## Testing Decisions

- **One seam: the Utility UI EditMode tests.** Toggles, groups and panels are already tested
  there (`PanelToggleTests`, `ToggleGroupTests`, `PanelGroupTests`) with an in-test canvas.
  Tests assert on which panel is showing, which toggle is on and the bool - never on tweens,
  sprites or event counts. The peek's two injected answers make it testable at the same seam.
- **What makes a good test here.** A test names the rule it protects: the bool has exactly two
  states; the inert toggle follows the driver; a peek restores unless something else wrote the
  bool; a reset on closing cancels a pending restore; a peek does nothing with its panel
  closed.
- **Modules tested:**
  - the two-panel switch - off shows A, on shows B, a click on either button flips both, the
    driver's click while on is refused, and a third toggle or a missing partner is warned;
  - the peek - a hold flips and a release restores; hold, click the other tab, release leaves
    the click; hold, close the panel, reopen, release leaves the Supply; a hold begun before
    the panel opens peeks on open; a hold with the panel closed does nothing; losing focus
    releases;
  - the group's first-member rule - a reset returns the pair to the off state through the
    driver.
- **Prior art.** `PanelToggleTests` and `ToggleGroupTests` for the toggle-and-group fixtures;
  `PanelGroupTests` for panels reacting to a state; the pause hotkey's tests for an injected
  key predicate.
- **Not unit-tested:** the scene wiring, the Alt coexistence with the tooltip, the look of the
  tabs, and the feel of the key - verified by hand in play.

## Out of Scope

- The **Map's InTown and InFields faces.** The switch can express them, but their bool is
  driven by the Run phase, Death and Recall, and InFields is deliberately not phase-driven
  (Go Venture previews it). Migrating them is its own decision; a peek there would Recall and is
  not wanted.
- A peek on groups of more than two tabs. A bool has two states by design.
- Rebinding the peek key, or a per-panel key. One key, Alt.
- Switching to the Sold tab on a sale, an undo-last-sale button, and selling from the Stash
  (all still out of scope from spec 2026-10-02).
- A view-only overlay that leaves the selection alone. The peek is a selection by decision.

## Further Notes

- **Defaults this spec chose without settling them with the user:** the two-state toggle in the
  Utility submodule and the peek in the game's GUI (a submodule change needs its own commit and
  a bump here), the first-member warning, and the peek only reacting while its panel is
  reachable. Change them here before `/to-tickets` if any is wrong.
- **Suggested slicing, from the drift review.** Expand: the two-panel switch and its reset test
  beside the old tab pair. Migrate: both panels' scene wiring and the peek. Contract: delete
  the prototype and its debug checkbox. The seven findings of the high-effort review of the
  prototype are not fixed on the prototype: the peek replaces the code they are about.
- **Close-and-reopen within a fade.** The reset runs when the panel has finished collapsing, so
  a reopen inside the 0.2 s fade keeps the tab and a pending restore still applies. This is
  accepted.
- **Before Unity compile verification, asmdef changes or scripted multi-file edits,** read
  `docs/agents/codebase-notes.md`.
