---
status: accepted
---

# Item level scales every rolled value by one formula; a minimum item level gates affixes and Uniques

Item level did nothing to an item, rarity only set the affix count, and a Unique was a Rare with extra stats
(#296). Settled in "What each Rarity is for" (#303); spec `dev/specs/2026-10-18-rarity-roles-design.md`.

## Decision

1. **One global formula.** Every rolled value (affix, implicit, a Unique's fixed affixes) is its roll in an
   authored range times `1 + g x (itemLevel - 1)`, the same g for every stat. Ranges are wide enough to
   overlap across item levels. Item level is fixed at the roll.
2. **Gates, not tiers.** An affix pool entry and a Unique each carry a minimum item level; below it they do
   not roll. There are no tier tables.
3. **Rarity is a role, applied on top.** A Common is the plain base with a higher implicit, a Magic item rolls
   few affixes from a higher range, a Rare rolls more at the plain range, a Unique is its authored list. The
   rarity is rolled before the definition, and a definition is drawn among those eligible for it.

## Considered options

**Affix tiers per item level** (Diablo II's affix levels, Path of Exile's tiers). Discrete jumps and
fine-grained gating, at the cost of authoring a tier table for every affix on every item type. With two
Locations most tiers would be empty. Rejected, not deferred: the user wants one formula to carry it.

**Per-affix slopes and floors.** Lets Magic Resist scale flatter than Health, but it turns every pool entry
into a small curve to tune. A per-stat override of g is held back until the re-sweep (#349) shows a stat that
needs one.

**Fixed Unique values** (Diablo II). A Unique is outgrown the moment the next Location opens; scaling its
narrow ranges by item level keeps a reason to find it again deeper, through the same formula.

**Magic as the top of the plain range.** Narrowing a Magic item's roll toward the max keeps it below a
Rare's best, so a Rare's overlapping affixes would match it soon. A higher range keeps the specialist ahead
on its few stats.

**A rarity card deck** (#296). Exact 1-in-500 Uniques and no dry streak, but it needs saved state and a
rework of Magic Find. Deferred, not rejected: one deck per session, reshuffled each session, rebuilt by Magic
Find.
