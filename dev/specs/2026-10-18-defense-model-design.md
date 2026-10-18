# The defense model - Armor is the item's, Magic Resist the hero's, penetration reads a hit

Date: 2026-10-18
Status: Settled in the 2026-10-18 grilling of "The defense model: mitigation formula, penetration, base
defense on items" (#348, map #293). ADR-0024. Every number here is a starting value the balance re-sweep
(#349) fixes.

## Problem Statement

The balance sweep (#296, `dev/prototypes/2026-10-17-balance-sweep/FINDINGS.md` on branch
`prototype/296-balance-sweep`, findings 7-9) found that defense stacks to immunity. Armor (+10-15) and Magic
Resist (+16-25) roll as flat hero affixes on seven and eight item types, read as a linear percent clamped at
100: Magic Resist passes 100 within a Send or two and the hero stops taking magical damage. The clamp in
`Hero.CalculateReceivingDamage` and `Enemy.TakeHit` is a stopgap. Armor Penetration and Magic Penetration
roll on gear and nothing reads them; they roll as `PercentAdd` on a base of 0, so they would be dead even if
something did. Item level does nothing to an item.

## Decisions

**Armor mitigates on a curve.** A hit loses `Armor / (100 + Armor)` of itself: 100 Armor halves it, and each
point adds 1% effective Health against physical damage. K is fixed at 100, not tied to the attacker's level:
the ladder already takes its pressure from item level and enemy curves. Below 0 the hit grows, by League of
Legends' rule `2 - 100 / (100 - Armor)`, which approaches double and never reaches it.

**Magic Resist is a percent with a cap.** A magical hit loses `Magic Resist / 100` of itself, capped at 75.
Magic Resist stands in for elemental resistances until they exist. Below 0 the hit grows linearly, floored
at -100 (double), so both defenses top out at double damage. No per-Location resistance penalty: smaller
rolls on fewer slots keep the cap out of reach of low-level gear. A Max Magic Resist stat that raises or
overwrites the cap is fog.

**One model for both sides.** The hero and an enemy mitigate by the same two rules. Enemy curves become an
Armor rating and a Magic Resist percent on the hero's scales (`ArmorPercent` / `MagicResistPercent` today).

**A stat resolves into parts; each reader computes from them.** A stat is a base plus modifiers. It resolves
them once into:

- **Flat**: the base plus every `FlatAdd`, or the highest `Overwrite` when there is one.
- **PercentAdd**: the sum of every `PercentAdd`; 0 under an `Overwrite`.
- **PercentMult**: each `PercentMult` factor; none under an `Overwrite`.

The total, `Flat x (1 + PercentAdd) x product(1 + PercentMult)`, is the common reading, not what makes it a
stat. A mechanic that needs something else reads the parts and computes locally; caps belong to that
mechanic's rule, never to the stat. No formula is handed into the stat. Each stat's record (the `statIcons`
entry beside its name and icon) says how it is read, total or parts, and the stats sheet and the item
comparison follow it.

**Penetration reads a hit; reduction changes the target.** Armor Penetration and Magic Penetration stay one
stat each, read by parts. A hit turns the target's resist into the effective resist it mitigates with:

```
effective = resist <= 0 ? resist
          : max(0, (resist - Flat) x (1 - min(1, PercentAdd)) x product(1 - PercentMult))
```

So +5 Armor Penetration (`FlatAdd`) ignores 5 points, +12% (`PercentAdd`) adds up with other percents to at
most all of it, a `PercentMult` stacks with diminishing returns, and an `Overwrite` of 30 ignores exactly 30
points. Negative penetration (an effect weakening the attacker) makes the target count as tougher.
Penetration ignores a defense; it never takes one below 0 and does nothing to one already at or below 0.
**Reduction** is the other entrance: an effect that puts modifiers on the target's resist stat, for every
hit while it lasts, below 0 too. It belongs to the effects model (#298), not to the hit. The floor is what
makes penetration a choice: worth most against a high-resist enemy, nothing against one with none, so its
value follows the Location's roster (Brutes carry Armor, Skirmishers none). Only the hero has penetration;
enemies wear no gear.

**Armor is an item stat.** An Armor base carries an Armor implicit, rolled within the base's range and scaled
by the item's item level: item level is a scalar on the roll, so two item-level-1 Chests rolling 32 and 35
roll about 38 and 42 at item level 2 (the curve is #349's). Affixes on Armor, flat or percent, change the
item's Armor, never the hero's. The hero's Armor is a stat of base 0 to which each worn item adds its Armor
total as one `FlatAdd`; effects may add any modifier to it. No equipment changes a hero stat that has an
item stat. The mechanism is general; which weapon stats become item stats (base damage, Attack Speed) waits
on "Who owns the Strike and the Cast" (#304).

**Magic Resist is a hero stat.** It rolls as a hero affix, at most one per item.

**A naked hero has no defense.** Base Armor and Magic Resist are 0; the 12 / 12 from #296's starter hero go.
Base defense and its growth by class wait on "What a hero class owns" (#305).

**Slot layout (starting values).** Armor implicit: Helm, Shoulders, Chest, Shield, Gloves, Pants. Magic
Resist affix: Cloak, Chest, Bracers, Rings, Belt. The Chest is the contested slot that can carry both. The
layout is derived from how much defense each Location asks for, not fixed here.

**Aims for the re-sweep.** Defense rises along the ladder with item level. Average gear for a Location
mitigates roughly 20-30% of a physical hit in Thornwood and 35-45% in Ashfall; a hero committed to Magic
Resist reaches roughly 25-35% in Thornwood and 40-50% in Ashfall, uncommitted about half. The 75 cap is a
goal past the MVP's Locations. The user finds these high; #349 lowers them as needed and derives ranges,
the implicit curve, the slot overlaps, the enemy Armor ratings and the roster mix from them.

**Proposed, not decided:** Magic Resist enters a slot's affix pool only from some item level up, with a
higher floor and flatter scaling than an always-on affix, so low-level gear barely rolls it. The mechanism
(item-level ranges vs per-level pools) is "What each Rarity is for" (#303).

## User stories

1. As a player, stacking Armor keeps making my hero tougher but never makes him immune to physical hits.
2. As a player, my Magic Resist stops at 75%, and low-level gear does not get me there.
3. As a player, an enemy's Armor and Magic Resist work on my hits exactly as mine work on theirs.
4. As a player, Armor Penetration on my gear lets my Strike ignore part of an armored enemy's Armor, and
   does nothing against an enemy that has none.
5. As a player, two Chests of the same item level show different Armor, and a higher item level Chest has
   more.
6. As a player, a "+20% Armor" affix on a Chest raises that Chest's Armor, and the tooltip shows what it
   changes on my hero.
7. As a player, Magic Resist rolls only on a few slots, so wearing it costs me other affixes.
8. As a new player, my hero has no Armor or Magic Resist until he wears gear.
9. As a player, a hit on a target whose resist an effect has pushed below 0 does more than its raw damage,
   at most double.

## Implementation Decisions

- **Stat parts.** `MutableFloat` resolves its modifiers into a parts value (Flat, PercentAdd, PercentMult
  factors) by the rules above; `TotalValue` is computed from it. `CharacterStat` exposes the parts. Base
  folding into Flat and `Overwrite` replacing base + flat live only here. `Overwrite` keeps highest-wins.
- **Mitigation.** One engine-free mitigation rule replaces the linear clamp in both
  `Hero.CalculateReceivingDamage` and `Enemy.TakeHit`: Armor curve with its negative branch, Magic Resist
  percent capped at 75 and floored at -100, after penetration. A hit on an enemy passes the hero's
  penetration parts; a hit on the hero passes none.
- **Item stats.** An `ItemInstance` resolves its Armor from its implicit and its Armor affixes (a stat of its
  own); `Hero.AddItemStats` / `RemoveItemStats` add and remove that total as one `FlatAdd` on the hero's
  Armor, and every other affix as today. "Item stat" in the glossary now names the item's own stat, not an
  affix (`AddItemStats`' current sense).
- **Item level scales the implicit.** `ItemGenerator` rolls the Armor implicit within the base's range and
  multiplies it by an item-level scale (a placeholder curve until #349). `ItemDefinitionAsset.implicitStats`
  gains a range; today it is a fixed modifier.
- **Enemies.** `EnemyArchetypeStats.ArmorPercent` / `MagicResistPercent` become an Armor rating curve and a
  Magic Resist percent curve; values are placeholders until #349.
- **Data.** `Item Type Data.asset`: Armor leaves the affix pools and becomes the implicit of Helm,
  Shoulders, Chest, Shield, Gloves and Pants bases; Magic Resist rolls on Cloak, Chest, Bracers, Ring and
  Belt only; Armor Penetration and Magic Penetration roll as `FlatAdd` or `PercentAdd`. `DefaultHero.asset`:
  Armor and Magic Resist base 0. Each `statIcons` entry gains how the stat is read (total or parts).
- **Comparison.** `StatComparison` compares what the stat's reader reads: an item-stat affix compares the
  item's total against the displaced item's and shows the hero delta of the contributed `FlatAdd`; a
  parts-read stat (Penetration) compares parts ("+5 / +12%"); every other stat its hero total as today.
- **Tests (EditMode).** Parts: base joins Flat, `Overwrite` replaces base + flat and drops the percents.
  Mitigation: 100 Armor halves a hit, Armor never reaches immunity, Magic Resist stops at 75, negative resist
  approaches and caps at double. Penetration: flat then percent, floored at 0, no effect on a resist at or
  below 0, percent capped at all of it. Item stats: a percent Armor affix raises only its item's Armor, the
  hero's Armor is the sum of worn items. Two implicit rolls at one item level differ, a higher item level
  rolls higher.

## Not part of this

- The numbers: ranges, implicit curve, enemy ratings, slot overlaps (#349).
- Reduction effects and other stats that lower an opponent's (the effects model, #298).
- Weapon implicits and which weapon stats are item stats (#304); base defense by class (#305).
- Several bases per item type (a Magic Resist Chest, dual-defense bases, base tiers, Ethereal/Superior):
  fog, with the crafting-base tier.
- Magic Resist as a choice beyond slots and rosters: elemental resistances, Max Magic Resist, over-capping:
  fog.
- Enemy penetration; global Armor on jewelry (Path of Exile's route).
