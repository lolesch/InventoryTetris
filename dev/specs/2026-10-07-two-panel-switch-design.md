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
Amended 2026-10-07 (the mirror toggle, a shareable group) and 2026-10-08 (either button may be
home, a close ends a peek). The body below states the built shape; the amendments at the end are
dated history.

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
buttons the player sees stay as they are: the driver is one of them, and the other is a
**mirror toggle** that names the driver, in the same group, which forbids switch-off, so
clicking either button flips the bool.

The **peek** switches the pair to its other button while a key is held and back on release. It
is a real selection, not a view-only overlay: it is a click made through the group, so both tab
buttons follow. On release it switches back only if the pair is still where the peek put it. A
click on the home tab moves the pair on, and the group's reset on closing stands the peek down;
each leaves the player where they are. A click on the tab being peeked at is refused because it
is already on and changes nothing: on release the player is home. Holding the key, closing the
panel, reopening on the Supply and releasing leaves the panel on the Supply.

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
5. As a player, I want a click on the tab I am peeking at to change nothing, and letting go to
   bring me home, so that a peek is only ever a look and never becomes a choice.
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
16. As a developer, I want a tab pair's exclusivity to be a bool, so that both panels showing,
    or neither, cannot be authored, whatever else shares the group.
17. As a developer, I want the peek to name its driver by a serialized reference, so that
    renaming a scene object cannot silently disable it.
18. As a developer, I want the peek's restore to be one rule - the pair is still where the peek
    put it, and the group has not reset - so that a click cancels it without a case of its own.
19. As a developer, I want the group to announce its reset on closing, so that the peek can
    stand down even where the reset found the pair already home.
20. As a developer, I want the peek's "key held" and "panel open" answers injectable, so that
    a test drives it without a keyboard or a canvas.
21. As a developer, I want a warning when a tab pair is authored wrongly (no group, a group
    that lets the user switch off, no first member, a mirror whose
    driver is unset or in another group), so that a wiring slip is loud.
22. As a developer, I want the Map's InTown and InFields faces to be able to use the same
    switch later, so that the two-panel shape is not invented twice.
23. As a developer, I want the prototype and its debug checkbox deleted when the peek is
    settled, so that no throwaway outlives the decision.

## Implementation Decisions

- **The tab pair is a two-state switch.** One toggle owns a bool, and exactly two panels follow
  it. The toggle is the driver; panels follow, never the other way round. In the Vendor and the
  Healer the driver is the Supply toggle: on shows the Supply panel, off shows the Sold panel.
  The component does not care which way round a pair is authored (a driver may rest on or off),
  so a pair whose driver is the Sold toggle is just as valid: on shows Sold, off shows Supply.
- **The tab buttons stay two.** The driver is one button; the other is a mirror toggle with no
  panel that names the driver by a serialized reference, in the same `ToggleGroup`, which
  forbids switch-off. In the Vendor and the Healer the Sold toggle is the mirror. Clicking the
  mirror switches the driver off through the group; clicking the driver while it is on is
  refused. The mirror has no behaviour of its own: the group does the switching off, in both
  directions. The group is the one mirror of the bool, as it already is for any two toggles; no
  second piece of state is added. The driver may be the group's first member (as built) when a
  mirror names it: the mirror is then the only way to switch the driver off, including from the
  group's side, which is what a peek does.
- **The pair may share its group.** The driver is an ordinary member: any other toggle in the
  group switching on switches the driver off, and the driver switching on switches it off.
  Nothing counts the group's members. What keeps the off panel from clashing with another
  toggle's panel is scene layout - the panels sit in different `PanelGroup`s - not a rule of the
  component.
- **The driver is a tab toggle, not a Side Panel toggle.** It never requests an Inventory
  Context: it chooses a view inside a panel that is already open. This carries over the spec
  2026-10-02 decision unchanged.
- **The peek is its own component on the selling panel,** one per panel, with a serialized
  reference to the driver and its mirror. It does nothing unless its panel is open, evaluated
  every frame as "key held and panel open", not on the key's down edge. When that first holds,
  it switches on whichever of the pair is off, through the group; when the key is let go, it
  switches the former one back on.
- **One restore rule.** The peek restores only while the group's active member is still the one
  it switched on, and only if the group has not reset. A click on the home tab changes the
  active member and so cancels; the group's reset on closing raises an event the peek listens
  to, because a reset that finds the pair already home changes nothing to see. No counters. A
  close during a peek therefore abandons the restore, and a close-and-reopen inside the fade
  keeps the peeked tab.
- **Either button may be home.** The group resets to its first member when the panel finishes
  closing (the 2026-10-05 amendment, already built), and the author says which button that is
  by switching it on in the group - in the Vendor and the Healer the driver is the Supply tab
  and the first member. The reset reaches the driver's own toggle callback exactly as a click
  does, so the driver needs no case for it.
- **Where a peek is not allowed to reach.** The peek touches the bool and nothing else: not a
  sale, not a drag, not a purchase, not the Inventory Context. A held purchase dropped on the
  Sold tab returns to its origin free, as spec 2026-10-02 already says.
- **Authoring is checked in the editor.** A warning, in the style of the `LocationToggle`
  warning, when: the driver has no group; the group allows switch-off; the group has no first
  member; the driver's panels are not
  both set or are the same panel; or a mirror's driver is unset, sits in another group than the
  mirror, or is authored on together with the mirror.
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
- **Scene migration.** Both selling panels' tab pairs change component on their Supply toggle
  (the driver), make the Sold toggle its mirror, and gain the peek. No scene or prefab may be left holding a missing-script reference or an
  orphaned serialized panel field.
- **Documentation.** `CONTEXT.md` / `GLOSSARY.md` gain **two-panel switch** and **peek**; spec
  2026-10-02's Sold tab decision is amended, as listed under Amends.

## Testing Decisions

- **One seam: the Utility UI EditMode tests.** Toggles, groups and panels are already tested
  there (`PanelToggleTests`, `ToggleGroupTests`, `PanelGroupTests`) with an in-test canvas.
  Tests assert on which panel is showing, which toggle is on and the bool - never on tweens,
  sprites or event counts. The peek's two injected answers make it testable at the same seam.
- **What makes a good test here.** A test names the rule it protects: the bool has exactly two
  states; the mirror follows the driver; a peek restores unless the pair has moved on; a reset on closing cancels a pending restore; a peek does nothing with its panel
  closed.
- **Modules tested:**
  - the two-panel switch - off shows A, on shows B, a click on either button flips both, the
    driver's click while on is refused, another toggle in the group switches the driver off and
    is switched off by it, and a mirror with no driver or in another group is warned (a driver
    among other toggles is not);
  - the peek - a hold flips and a release restores; hold, click the home tab, release leaves
    it; hold, click the peeked tab (refused), release returns home; hold, close the panel,
    reopen, release leaves the Supply, also when the peek began from the Sold tab; a hold begun
    before the panel opens peeks on open; a hold with the panel closed does nothing; losing
    focus releases; all of it for both orientations of the pair;
  - the group's first-member rule - a reset returns the pair to its first member through the
    driver's own toggle callback, whichever of the pair that is, and raises the group's event
    even where the pair is already home; a group without reset-with-panel is not warned.
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
- A peek on groups of more than two tabs. A bool has two states by design: the peek flips the
  driver's bool and nothing else, whatever else shares the group.
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
  a reopen inside the 0.2 s fade keeps the tab. During a peek the close has already abandoned
  the restore, so the reopen keeps the peeked tab and a release does not put the player back.
  This is accepted.
- **Before Unity compile verification, asmdef changes or scripted multi-file edits,** read
  `docs/agents/codebase-notes.md`.

## Amendment 2026-10-07: the mirror, and a group that may be shared

History, after the review of the Utility PR for ticket 1. That PR paired the driver with any
inert toggle and guarded the pair with five editor warnings, three of them about the group; the
review proposed removing the group from the driver. The decision was the smaller change: keep the
group (it already supplies the exclusion, the refusal of a click on the button that is on, and
the reset on closing) and make the partner a `TwoPanelMirrorToggle` that names the driver, so a
pair may share its group and the member-count and first-member warnings go. The body states the
result. Also ruled then: a refused click is not a write (story 5), and the reset-with-panel
setting is not warned about (a pair need not live in a closing panel; ticket 2 turns it on for the
selling panels). Not taken: a dedicated group type, or forbidding a `ToggleGroup` on the driver -
membership is found by `GetComponent<ToggleGroup>()` on the parent, so neither stops the wrong
group being parented. Tickets 2 and 3's "inert partner" reads as the mirror.

## Amendment 2026-10-08: the driver may be the first member; a close ends a peek

History, decided on the scene wiring. The Vendor and the Healer author the pair the other way
round from the first draft: the driver is the Supply toggle and the group's first member, the Sold
toggle is the mirror. That works, so "the first member must not be the driver" was dropped and its
warning replaced by two (no first member; a driver that is first with no mirror naming it). One
peek case the write count missed - a peek that landed on the first member, then a close, leaves the
reset nothing to write, so a release after the reopen restored the tab the player had left - is
closed by the peek abandoning its restore when it sees its panel closed. The body states the
result; the glossary entries **two-panel switch**, **mirror toggle** and **peek** describe it.
