# What each Rarity is for - a role per tier, one item-level formula for every rolled value

Date: 2026-10-18
Status: Settled in the 2026-10-18 grilling of "What each Rarity is for" (#303, map #293). ADR-0025. Every
number here is a starting value the balance re-sweep (#349) fixes.

## Problem Statement

Progression feels linear: a higher rarity wins by bigger numbers and more affixes. In the code, rarity sets
only the affix count (`ItemGenerator.AffixCountFor`: Common 1, Magic 2, Rare 3, Unique 3), and a Unique is a
Rare with its fixed list on top. `RollModifier` ignores item level, and a Location's item level is its source
level. The balance sweep (#296, `dev/prototypes/2026-10-17-balance-sweep/FINDINGS.md` on branch
`prototype/296-balance-sweep`, findings 5, 6, 8, 10, 11) found more: `PickDefinition` draws uniformly over
every definition, so 89% of equipment drops land on one of the 156 Unique definitions whatever rarity was
rolled; gear outgrows the enemies, and the user chose to shrink affixes and scale them by item level instead
of raising the enemy curves; and a NoDrop roll throws. No equipment base carries an implicit yet; the defense
model (#348) gives Helm, Shoulders, Chest, Shield, Gloves and Pants an Armor implicit.

## Decisions

**Each rarity has a role.**

| Rarity | What it is | Affixes | Late use |
| --- | --- | --- | --- |
| Common | The plain base | None; its implicit rolls above the base range (start x1.25) | The highest implicit of its type: raw Armor (or weapon damage) traded for affixes. The future crafting base |
| Magic | The specialist | 1-2, each rolled from its range x1.3 | One or two stats higher than any Rare rolls them: the pick to specialise or close a gap |
| Rare | The generalist | 3-4 at the plain range | Breadth: two Rares with overlapping affixes cover what two specialised Magic items cover |
| Unique | An authored item | Its fixed list only, no rolled affixes | A known item placed on the ladder; a deeper copy rolls higher |

**A Common is the plain base.** It carries its implicit and nothing else, and the implicit rolls in its range
times the Common multiplier, so it beats every other rarity's implicit of the same item level. When crafting
exists, that higher implicit carries into the crafted item, so a crafted item can outroll a Rare. An item
type with no implicit (today Amulet, Ring, Belt, Boots, Bracers, Cloak, Quiver) never drops Common.

**A Magic item's range is higher, not narrower.** Its affixes roll the item-level-scaled range times the
Magic multiplier: the low end overlaps a Rare's roll, the top end beats a Rare's best. Narrowing a Magic
item to the top of the plain range was rejected: a Rare's overlap would match it too soon.

**A Unique is authored and gated.** Its fixed list is the whole item: each value rolls within its own narrow
authored range, passed through the item-level formula like any rolled value. The item level is fixed at the
roll, so a copy found at a higher item level is stronger, and nothing grows while it is worn. Each Unique
carries a **minimum item level** and drops only where the rolled item level reaches it (Diablo II's unique
level). A Unique roll with no eligible Unique falls back to a Rare. The 156 migrated Uniques are trimmed to a
few per Location's item-level band; one no Location reaches is noise in the catalog. Uniques stay rare: about
1 kill in 500 (#296). A build-enabling effect per Unique is fog behind the effects model (#298).

**Rarity first, then a definition eligible for it.** A roll picks the category, the item level and the
rarity, then a definition among those eligible: a Common among bases with an implicit, a Magic or Rare among
the non-unique bases, a Unique among the Uniques whose minimum item level the roll reaches. No other rarity's
odds change.

**Rarity gives the affix count; the item type's footprint bounds it.** Magic 1-2, Rare 3-4. A small item
(a 1x1 Ring) rolls fewer, a large one (a two-hander standing in for two slots) may roll more: in a packing
game, value per cell is the trade. The mapping from footprint to the bound is #349's. A Unique's count is its
list; a Common's is 0.

**Item level moves every rolled value by one global formula.** `value = roll in the authored range x (1 +
g x (itemLevel - 1))`, the same g for flat and percent affixes, implicits and Uniques. No affix tiers and no
per-affix slopes: a lower item level rolls lower, and ranges overlap across item levels, so an item level 1
roll can beat an item level 2 roll of the same affix (10-15 at item level 1 is 11-16.5 at item level 2 with
g = 0.1). The rarity multipliers apply on top. A per-stat override of g is held back unless #349 finds a stat
that breaks.

**An affix enters an item type's pool from a minimum item level.** Each entry of a definition's affix pool
carries a minimum item level, so Magic Resist (#348) does not appear on all its slots at once. An item-level
range per entry (a low-level item rolling something a high-level one excludes) can follow; tier tables
cannot.

**A Location drops a narrow, falling range of item levels.** Its base item level is its source level; no
drop rolls lower. A drop rolls `[base, base + 2]`, weighted 70 / 25 / 5, so its best drop is ahead of the
area and rare (#296's addendum). The +2 is a constant for now.

**Magic Find changes quality only.** It never touches the item level or the Fail Bucket; ADR-0004 stands.

**Increased Item Quantity is fractional.** Each 100% is one more roll, and the remainder is the chance of one
more (30% is a 30% chance of an extra roll). Each roll still passes the Fail Bucket. Today it truncates, so
anything below 100%, a Unique's 20-30% among it, does nothing.

**The rarities stay four.** Common, Magic, Rare, Unique; "Legendary" is a Unique. Sets need their own
design; Crafted comes with crafting. No level requirement to equip: the Location's item-level range is the
gate, and `ItemDefinitionAsset.requirementLevel` is there when one is needed.

## User stories

1. As a player, a Common Chest has more Armor than any Magic or Rare Chest of its item level, so it is worth
   picking up when I want raw defense.
2. As a player, a Magic item can roll one stat higher than any Rare, so I wear it to push that stat.
3. As a player, a Rare rolls more affixes than a Magic item, so it covers more of my needs at once.
4. As a player, a Unique is a named item I recognise, with the same stats each time and values that vary a
   little, and it drops about once in 500 kills.
5. As a player, I find a given Unique only from the Location whose items reach its level, and a copy from a
   deeper Location rolls higher.
6. As a player, an item from a deeper Location usually rolls higher, but a lucky roll from an easier one can
   beat it.
7. As a player, a Location sometimes drops items a little above its own level, rarely the highest.
8. As a player, small items such as rings roll fewer affixes than large ones, and a two-handed weapon rolls
   more than a one-handed one.
9. As a player, low-level gear seldom rolls Magic Resist; it starts showing up as item levels rise.
10. As a player, Increased Item Quantity below 100% still gives me extra rolls some of the time.
11. As a player, Magic Find gives me better drops, never more or fewer of them.
12. As a player, a kill that rolls several times keeps every item that dropped, even when one roll drops
    nothing.

## Implementation Decisions

- **The item-level roll.** `RollContext` carries a rolled item level instead of the source level. The
  Location authors its base (its source level); the width (+2) and the weights (70 / 25 / 5) are tuning.
- **Rarity first.** `ItemGenerator` rolls the rarity, then draws a definition among those eligible for it
  (above), weighted uniformly within that set for now. A Unique roll with no eligible Unique rolls a Rare.
  This replaces `PickDefinition`'s draw over every definition; `ItemService`'s debug mirror follows.
- **Values.** One item-level scale function (g placeholder until #349) multiplies every rolled value:
  `RollModifier`, the implicit roll (#348) and a Unique's fixed affixes, which roll within their authored
  `StatModifier` range instead of using the stored value. Rarity multipliers (Common x1.25 on the implicit,
  Magic x1.3 on affixes) apply after it. `RollModifier` keeps its integer rounding.
- **Counts.** `AffixCountFor` becomes a count range per rarity (Common 0, Magic 1-2, Rare 3-4, Unique none),
  bounded by the item type's footprint through one tuning mapping.
- **Gates.** `AffixSlot` (and its authored form) gains `MinItemLevel`; a slot below the roll's item level is
  outside the pool. `ItemDefinitionAsset` gains a minimum item level, read only for Uniques.
- **Item Quantity.** `LootFlow.DropCountFor` and `ItemService.RollLoot` roll the fractional remainder.
- **NoDrop.** A NoDrop roll yields no item instead of throwing, so `LootFlow` keeps the rest of a kill's rolls
  (finding 11).
- **Data.** Trim the Uniques to a few per Location band, each with a minimum item level; give Magic Resist
  its minimum item level per type; shrink the affix ranges (#349 sets them).
- **Tests (EditMode).** Rarity first: a Common is only drawn from bases with an implicit, a Unique roll with
  no eligible Unique becomes a Rare, a Unique roll never yields a non-Unique definition otherwise. Values: a
  higher item level rolls higher on average, two item levels' ranges overlap, a Magic affix's max beats a
  Rare's, a Common implicit beats a Magic's. Counts: each rarity's range, the footprint bound. Gates: an affix
  below its minimum item level never rolls, a Unique below its minimum never drops. Item level: never below
  the base, never above base + 2. Quantity: 30% gives one extra roll about 30% of the time. NoDrop: a
  multi-roll kill keeps the items of its other rolls.

## Not part of this

- The numbers: g, the multipliers, counts, the footprint mapping, the item-level weights, affix ranges and
  minimum item levels, which Uniques stay (#349).
- Affix tiers per item level (Diablo II and Path of Exile): rejected, not deferred (ADR-0025).
- A rarity card deck: fog. One deck per session, reshuffled each session and carried across Locations;
  Magic Find rebuilds it with the adjusted card counts.
- A signature implicit per base for the seven types without one: fog, with several bases per item type,
  since a base choice is what an implicit tells apart (Path of Exile, Last Epoch).
- Base tiers (Diablo II's Normal, Exceptional and Elite): fog, with several bases per item type.
- A build-enabling effect per Unique: fog behind the effects model (#298).
- A level requirement to equip; Set items; crafting and Crafted items; bad-luck protection.
