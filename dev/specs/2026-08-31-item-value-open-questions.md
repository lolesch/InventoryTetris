# Item value — open questions

Date: 2026-08-31
Status: parked — not scheduled, not designed

Captured while designing currency drop piles
(`dev/specs/2026-08-31-currency-drop-piles-design.md`). None of this is currency
work; it is the reason the currency income-split measurement was deferred.

## 1. `goldRatio`s are guesses

`AbstractItem.CalculateValue` (renamed from `CalculateGoldValue` in Phase 1) values
an item as `Σ(affix.Modifier.Value × goldRatio)`, with a per-`StatName` ratio
switch:

```
StatName.AttackSpeed       => 25f,
StatName.PhysicalDamage    => 35f,
StatName.MagicalDamage     => 21.75f,
StatName.Health            => 2.67f,
...
StatName.Shield            => 2.67f,   // "Values not set yet"
StatName.IncreasedItemRarity   => 0f,
StatName.IncreasedItemQuantity => 0f,
```

The code flags itself: `// NOTE that goldRatios should differ based on the
modifier type!` (a flat +5 and a +5% are priced identically today), and several
entries are placeholders.

Until each ratio reflects how much that stat actually contributes to combat
effectiveness or character progression, `SellValue` is not a reliable number —
which means "what fraction of income is loot vs currency" cannot be measured
meaningfully.

**Needs:** a pass that grounds each `goldRatio` (and a per-`StatModifierType`
factor) in a combat/progression model, then re-checks the currency drop table's
income share against it.

## 2. Value from item dimensions

Idea: fold an item's grid footprint into its value, so bulk has a cost/benefit and
the Tetris packing decision carries economic weight.

Rough intent: a 2×4 two-hander should be worth about the same as a 1H (2×3) plus a
shield (2×3), or as two 1×3 one-handers — i.e. value tracks the cells it occupies,
not just its affixes.

**Known flaw:** there are many valid loadout footprints today
(two 1×3 → two 2×3 → one 2×4), so "dimension coverage" is ambiguous and a pure
per-cell price would misrank them. Would need the equivalence classes pinned down
first.

## 3. A larger two-hander footprint

Two-handers currently top out at 2×4 (`Crossbow`, `GreatSword`). Adding a 2×5 or
2×6 size would:

- widen the gap between one- and two-handed so the dimension-value idea in §2 has
  room to express "a 2H is a serious commitment";
- improve inventory dimension coverage — the set of distinct footprints the player
  has to Tetris around is currently thin at the large end.

`ItemSize` / `AbstractItem.GetDimensions` would gain the new case;
`ItemTypeData` and the unique item objects would need the larger art/footprint.

## Relationship

§1 is a prerequisite for trusting any economy measurement. §2 depends on §1 (you
can't add a dimension term to a value function whose affix term is still guessed)
and on resolving its own ambiguity. §3 is independent and cheap, and makes §2 more
worthwhile.
