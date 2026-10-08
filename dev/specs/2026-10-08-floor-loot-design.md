# Loot on the Floor

Date: 2026-10-08
Status: Scoping spec - not a plan. Slice into a GitHub epic with `/to-tickets`, build with
`/implement`.
Base: `main` at `73ec5c5`.
Derived with: a design interview in which the owner made every decision below.
Amends: GLOSSARY.md (Drop, Ground Items List, Corpse), ADR-0009 (recovery leftovers stay on the
Corpse).
Depends on: the two-panel switch (epic #195) for the Combat Panel toggle; the Alt peek needs #198.
Sibling: `2026-10-08-spatial-combat-design.md` (the Hero icon and the hero figure).

## Problem Statement

The Ground Items List gets long and cluttered very fast. Every drop the auto-pickup filter
or a full bag turns away becomes a row, every discard adds one more, and a stack of ten arrows
becomes ten rows. The player cannot see at a glance what is lying there, and a row's position
shifts as others are picked up, so clicking one is a moving target.

Coins have it worse: a pile that fails the filter vanishes without a trace, and one that
passes banks at once whether or not the player ever asked for auto-pickup. Nothing about a
coin is ever on the ground to look at.

Recovering a Corpse dumps whatever the bag cannot take onto the same floor, so the hero's own
gear competes with fresh loot for room, and one flood of drops could push it out.

## Solution

The floor becomes a **grid** like the stash, and it stays a list too: two views of the same
set of drops. Items lie where they land and do not move, so the one you want to click is
always where you saw it. The grid has limited room. When a new drop does not fit, the
**oldest** drops are removed until it does, and nothing else is rearranged. How old a drop is
shows as how faded it is: the newest is fully opaque and the oldest is the faintest.

Whatever the player discards - by shift-click or by dropping an item on the floor slot - joins
the floor the same way, and a stack stays one stack.

Coins are loot like any other. A coin pile the auto-pickup filter admits is picked up, and
every other pile lands on the floor, where coins of one denomination **stack** and each new
pile refreshes the stack's age, so they pile up for the player to take. A coin stack pushed out
by newer loot is banked to the Wallet first, and only what the Wallet cannot take is lost.

A Corpse recovery no longer touches the floor. What the bag cannot take stays on the Corpse,
and the Hero icon under the Location toggles shows that a Corpse is lying at that Location.

The floor panel sits in the Combat Panel and is toggled and peeked at the way the Supply and
Sold tabs are.

## User Stories

1. As a player, I want the floor shown as a grid, so that I can see at a glance what is lying
   there.
2. As a player, I want items to stay where they land, so that the one I want to click does not
   move under my cursor.
3. As a player, I want the oldest drops to make room for new ones, so that the floor never
   overflows.
4. As a player, I want older drops drawn fainter, so that I can see which ones are about to go.
5. As a player, I want the fade to show age relative to the other drops, so that it does not
   depend on how fast the sim runs.
6. As a player, I want the list view to stay, so that I can still read item names.
7. As a player, I want the list and the grid to show the same drops, so that there is one
   answer to what is on the floor.
8. As a player, I want clicking a drop in either view to pick it up, so that I do not care which
   view I use.
9. As a player, I want a pick-up with no room to leave the drop where it is, so that nothing is
   lost to a full bag.
10. As a player, I want a shift-clicked item to land on the floor, so that discarding is one
    gesture.
11. As a player, I want an item dropped on the floor slot to land on the floor, so that a drag
    can discard too.
12. As a player, I want a discarded item to make room when the floor is full, so that discarding
    never fails for lack of space.
13. As a player, I want a discarded stack to stay one stack, so that ten arrows are not ten
    entries.
14. As a player, I want the floor to be wiped when the Run ends, so that nothing lingers between
    Runs.
15. As a player, I want coin piles to land on the floor unless they are auto-picked, so that I
    can see and take them.
16. As a player, I want the rarity filter to apply to coins the way it applies to items, so
    that I pick up the denominations I care about.
17. As a player, I want coins of one denomination to stack on the floor, so that a heap reads
    as one pile.
18. As a player, I want a new coin pile to refresh its stack's age, so that a pile I am still
    adding to does not fade away.
19. As a player, I want a coin stack that is pushed out to bank to my Wallet first, so that
    flooding the floor does not cost me money I could have kept.
20. As a player, I want a coin stack the Wallet cannot fully take to lose only the rest, so that
    a full Wallet does not eat the whole stack.
21. As a player, I want clicking a coin stack to bank it, so that taking coins is one click.
22. As a player, I want a Corpse recovery to leave the floor alone, so that my own gear never
    competes with fresh loot.
23. As a player, I want what the bag cannot take to stay on the Corpse, so that I can come back
    for it.
24. As a player, I want the Hero icon under the Location toggles to show a Corpse where one is
    lying, so that I know where to go.
25. As a player, I want the floor panel to toggle with a click and peek with a held key, so that
    I can glance at it without leaving the combat controls.
26. As a developer, I want the floor and the Sold tab to share their eviction logic, so that
    there is one implementation of oldest-out.
27. As a developer, I want the floor to hold one answer to what is lying there, so that the
    list, the grid and the pick-up path cannot disagree.

## Implementation Decisions

**The floor container**

- The Run's ground becomes a grid container sized like the stash (a tuning value), holding
  packages. It replaces the plain list of drops; the list view reads the same container.
- Placement is first-fit, and **nothing is ever moved**: no compaction, so holes stay holes.
- Age is an order over the packages. A package that lands, or a stack that gains coins or
  items, becomes the newest. Eviction removes the oldest package, repeatedly, until the new
  package fits somewhere. A package larger than the whole grid is refused and handed back.
- The eviction and age-order machinery is shared with the Sold container. The difference is
  that the Sold container compacts and the floor does not; this is one implementation with a
  switch or a shared base, not a copy.
- The floor is wiped when the Run ends, as the ground is today. With no Run there is no floor,
  and a released item goes back where it came from.

**Discards**

- Quick Move to the floor and a drop on the floor slot both place the package on the floor,
  evicting the oldest when it is full. A stack lands as one package, replacing the old rule that
  turned a stack of n into n entries.

**Coins**

- A coin pile follows the item rule. When auto-pickup is on and the filter admits the
  denomination, it is deposited to the Wallet, and only what the Wallet can take is deposited;
  everything else lands on the floor. With auto-pickup off, every pile lands on the floor. The
  previous "banks either way when admitted" exception is removed.
- On the floor, a pile becomes a package of that denomination's coin item. Coins of one
  denomination merge into a stack up to the item's stack limit; beyond it a new stack starts.
  Merging refreshes the age. Denominations never consolidate on their own.
- Evicting a coin stack first asks the Wallet whether it can take it and deposits what fits; the
  remainder is deleted. Evicting any other package deletes it. Going deeper into auto-pickup
  for evicted items is a later spec.
- Clicking a coin stack on the floor banks it to the Wallet, and whatever the Wallet cannot
  take stays.

**The Corpse**

- Recovery tries to place each item through the player's acquisition entry point. What does not
  fit stays on the Corpse, re-buried at the same Location, and is never laid on the floor. The
  Corpse stays bag-only; ADR-0009's "equipped gear is never touched" stands.
- The Corpse keeps holding its items as plain lists. There is no Corpse container or panel.
- The Hero icon under the Location toggles gains a Corpse variant, shown on the Location a
  Corpse lies at. The icon remains the Location's marker; the hero figure that moves in the
  arena is a separate element (see the spatial-combat spec).

**Views**

- The grid view draws each package at its cell, and each entry's visual weight comes from its age
  rank: the newest at full opacity, the oldest at a minimum (a tuning value). The fade is
  relative to the other drops and does not use time.
- The list view stays, reading the same container in age order. Both views pick up through the
  same path.
- The floor panel is the second panel of a two-panel switch inside the Combat Panel, toggled
  by a click and peeked at with the held key once the peek exists. Which content is the home
  panel is settled against the scene in the ticket.

**Documentation**

- GLOSSARY.md: **Drop** and **Ground Items List** (grid and list over one bounded floor, oldest
  out, coins stack), **Corpse** (leftovers stay on it), and the coin sentence in **Drop**.
  ADR-0009 gets a one-line note that recovery leftovers stay on the Corpse.

## Testing Decisions

- A good test asserts external behaviour: what is on the floor after a sequence of drops,
  discards and pick-ups, which package was evicted, where the survivors sit, and what the
  Wallet received. It does not assert how the age order is stored.
- **`LootFlow`'s public behaviour** is the seam: oldest-out order, stationary cells after an
  eviction, stack merge refreshing age, discards evicting, a refused oversized package, the coin
  pickup rule with auto-pickup on and off, eviction banking through the Wallet, and the Wallet
  rejecting part of a stack. Prior art: `LootFlowTests`; the Sold container's own tests for
  the eviction order.
- **Corpse recovery** through `RunSettlement` and `Corpse`: leftovers stay buried at the same
  Location and nothing reaches the floor. Prior art: `RunSettlementTests`, `CorpseTests`.
- The floor grid, the list, the fade, the toggle and the Corpse marker are checked by hand in
  Play: fill the floor past capacity, discard with it full, pile up coins, die, recover with a
  full bag, and Recall.

## Out of Scope

- The auto-pickup redesign and any player-settings policy for it; the `AutoPickup` switch and
  the loot filter stay as they are.
- A timed decay for floor drops; age rank and capacity are the only removal rules.
- Auto-selling, auto-equipping or otherwise rescuing items as they are evicted; only coins are
  banked.
- A Corpse container or panel, and death taking equipped gear.
- Dropping onto the floor from anywhere but Quick Move and the floor slot.

## Further Notes

**Tuning placeholders:** the floor grid is the stash's dimensions, and the minimum fade is an
opacity of about 0.35. Both are starting points to be judged by eye.

**A behaviour change worth knowing:** until now an admitted coin pile banked on the spot even
with auto-pickup off. Under this spec, with auto-pickup off, no coin banks by itself; the
player picks them up from the floor or lets eviction bank them. Raising or lowering how much
is picked up automatically is the later auto-pickup spec.

**Dependencies:** the toggle uses the two-panel switch, which has landed (#196). The Alt peek
waits for #198. The Hero icon change should be built together with the hero figure in the
spatial-combat spec so the checkmark's two roles stay separate.
