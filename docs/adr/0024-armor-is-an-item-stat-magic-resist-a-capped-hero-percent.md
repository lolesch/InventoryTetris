---
status: accepted
---

# Armor is an item stat, Magic Resist a capped hero percent; a stat resolves into parts its readers compute from

Flat Armor and Magic Resist affixes on most armor types, read as a linear percent clamped at 100, stacked the
hero to immunity within a Send or two (#296). Penetration affixes rolled as `PercentAdd` on a base of 0 and
nothing read them. Settled in "The defense model" (#348); spec `dev/specs/2026-10-18-defense-model-design.md`.

## Decision

1. **The two defenses work differently on purpose.** Armor is a rating on a curve, `Armor / (100 + Armor)`
   with a fixed K, and never reaches immunity. Magic Resist is a percent capped at 75, standing in for
   elemental resistances (Diablo II's model). Both make a hit at most double below 0.
2. **Armor is an item stat.** An Armor base carries an Armor implicit rolled in a range and scaled by item
   level; affixes on Armor change the item, and the item adds its total to the hero's Armor. No equipment
   changes a hero stat that has an item stat. Magic Resist stays a hero affix on a few slots.
3. **A stat resolves into parts, and its readers compute from them.** Flat (the base plus every flat, or the
   highest overwrite), the summed percent-add, the percent-mult factors. The total is one reading; a
   mechanic such as penetration reads the parts and applies its own caps. No formula is handed into a stat.
4. **Penetration belongs to the hit, reduction to the target.** Penetration ignores part of a positive resist
   and never takes it below 0; only a reduction effect on the target's resist stat goes negative.

## Considered options

**Both defenses as League-style ratings.** Symmetric and simple, but it leaves no room for the resistance
juggling elemental resistances will bring, and Magic Resist would have to be a rating to become a percent
again later.

**Defense as random flat hero affixes, shrunk and scaled by item level.** Keeps one mechanism, but any slot
then stacks both defenses, and a local "+% Armor" on one Chest is impossible: every percent affix would act
on the hero's whole total.

**Global Armor on jewelry** (Path of Exile). Buys build variety, at the cost of the rule that equipment
defense lives on the item. Out for the MVP.

**Penetration as two stats per type** (percent and flat), or as a parts-only object outside the stat
system. The first reads as "+12% flat penetration"; the second needs a second path through affixes,
equipment, comparison, display and saving for one stat.

**Penetration below 0.** Turns it into a damage stat against everything; the floor keeps it the answer to
high-resist enemies, so its worth follows the roster.

## Consequences

`StatComparison` can no longer measure every affix row as a change in the hero's total: an item-stat affix
compares item totals, a parts-read stat compares parts. Each stat's record declares how it is read.
