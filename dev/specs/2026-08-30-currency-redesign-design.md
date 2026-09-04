# Currency redesign — denominations, consolidation, drops

Date: 2026-08-30
Status: Phases 0-2 shipped; Phase 3 still deferred.
Phase 0/1 landed in `727c742`..`8daa96b` (ladder, decoupled stack limits, manual
consolidation, Consolidate button). Phase 2 was designed separately and shipped as
`dev/specs/2026-08-31-currency-drop-piles-design.md`. The income-split measurement
this spec asked for before Phase 2 shipped was NOT done - see
`dev/specs/2026-08-31-item-value-open-questions.md` for why.

## Problem

The complaint was "only gold feels like the real deal." That turned out to be
structural, not a tuning issue.

`CurrencyItem.StackLimit` was set equal to each coin's conversion ratio
(copper 20, iron 12, silver 5), and `CharacterInventory.TryStack` auto-upgraded a
stack the instant it filled. So copper, iron and silver were never currencies —
they were carry digits in a base-(20,12,5) positional number, and the maximum
holdable below gold was permanently:

    19 copper + 11 iron + 4 silver = 19 + 220 + 960 = 1199

One gold was 1200. **Everything not gold was always worth less than a single gold
coin.** That is a theorem the design produced, not a number that could be tuned.

Income was equally lopsided. Currency drops yield exactly one coin, weighted
53/27/13/7, so the share of currency income by tier was:

| copper | iron | silver | gold |
|---|---|---|---|
| 0.6% | 4.5% | 27% | **68%** |

## Goals

1. **Visible** — hold a real pile of each coin, not `ratio - 1` of it.
2. **Earned** — each tier carries a meaningful share of income.
3. *(deferred)* **Useful** — coins as PoE-style reagents with their own sinks.

## Decisions

### Ratios: 5 / 12 / 20

    iron  --5-->  copper  --12-->  silver  --20-->  gold
      1             5                60             1200

Ratios multiply, so `5 × 12 × 20 == 20 × 12 × 5`: gold keeps its exact value of
1200 base units and **no item price needs retuning**.

This is the English pound-shilling-pence structure — ~4 farthings = 1 penny,
12 pence = 1 shilling, 20 shillings = 1 pound.

### Metal order: iron is the cheapest

The old ladder put iron *above* copper, which is wrong on history, metallurgy and
player intuition. Iron is ~5% of the Earth's crust against copper's ~0.007%, and
it was almost never coined because it is cheap, heavy, brittle when cast, and
rusts — losing the sharpness of its impression. Copper was a genuine coinage
metal from the 3rd century BCE.

The medal podium (bronze < silver < gold) agrees: with iron at the bottom, the
top three tiers *are* the podium.

Iron now has a reason to be the trash coin: it rusts.

**Icons need no change** — grey iron stays iron, orange copper stays copper. Only
the value mapping moves.

> **Do not reorder the `CurrencyType` enum.** A previous reorder is what produced
> two of the bugs listed below. Enum order and value order do not have to agree.

### Stack limits, decoupled from ratios

| Coin | Limit | Cell value | A full stack consolidates to |
|---|---|---|---|
| Iron | 120 | 120 | 24 copper |
| Copper | 60 | 300 | exactly 5 silver |
| Silver | 20 | 1200 | exactly 1 gold |
| Gold | 12 | 14400 | — |

Silver's limit equals its ratio, so a full silver stack is precisely one gold with
no remainder. Descending limits read as "bigger coins are bulkier", and make
hoarding iron cost real grid space — the Tetris expression of "iron is cheap".

### Consolidation: manual, with an auto seam

Auto-upgrade-on-full is removed. Coins accumulate without bound, which is what
lifts the 1199 ceiling and makes *Visible* achievable at all.

- `AbstractDimensionalContainer` gains `public virtual bool AutoConsolidate => false`.
- A `Consolidate()` method converts each tier upward as far as it goes, leaves the
  remainder, and is value-lossless (sum to base units, decompose, re-add).
- Containers with `AutoConsolidate == true` run it after a successful insert.
- The inventory gets a **Consolidate button next to the existing Sort button**.

There is no stash class yet (only `CharacterInventory` and `CharacterEquipment`),
so "everything becomes gold over time in the stash" cannot ship now. The seam
above means a future stash overrides `AutoConsolidate => true` and gets that
behaviour with no rework.

Spending order is unchanged: `TryGetPayment` already exhausts the smallest
denomination first, taking `ceil(owed / denomination)` at each tier. Correct as-is.

### Drop table

Currency drops become stacks rather than single coins. Amounts are deliberately
small so a gold drop reads as a jackpot.

| Coin | Unit | Amount | Value per drop | Weight | Chance | Contribution | Share of income |
|---|---|---|---|---|---|---|---|
| Iron | 1 | 10–30 | 10–30 | 40 | 44.9% | 8.99 | 17.5% |
| Copper | 5 | 4–12 | 20–60 | 40 | 44.9% | 17.98 | 35.1% |
| Silver | 60 | 1–3 | 60–180 | 8 | 9.0% | 10.79 | 21.1% |
| Gold | 1200 | 1 | 1200 | 1 | 1.1% | 13.48 | 26.3% |

Expected value per currency drop: ~51 base units.

A gold drop is 40× the best possible iron pile and 20× the best copper pile.
Iron and copper are ~90% of what drops; silver is 1-in-11; gold is 1-in-89.

Note the two different questions the columns answer: *value per drop* is how good
a drop feels when it lands; *contribution* is where money comes from over a run.
Gold needed to win decisively on the first without dominating the second.

**Why the income cut is safe:** `Item Category Distribution` rolls Equipment 66.7%
/ Consumable 16.7% / Currency 16.7%, and equipment is vendored at values in the
hundreds. Rough estimate: ~95% of income is selling loot, not picking up coins.
So this table is close to a pure feel dial. *This estimate rests on typical affix
values and should be measured before Phase 2 ships.*

## Implementation

### Phase 0 — bugs (independent, do first)

- `InventoryProvider.cs:139-140` — `SetItemToIron()` adds Copper and
  `SetItemToCopper()` adds Iron. Swapped.
- `Currency Type Distribution.asset` — cached `name` fields are stale from an old
  enum order (`name: Iron` sits on `Enumeration: 1`, which is Copper). Opening the
  asset in the inspector runs `OnValidate`, which relabels entries **without moving
  their weights**, silently locking in the wrong mapping. Fix before touching it.
- `CurrencyTests.cs` — hardcodes `1 / 20 / 240 / 1200`.
- *(optional)* `AbstractProbabilityDistribution.Probabilities` allocates and
  re-sorts on every access, and `GetRandomEnumerator` reads it inside a nested
  loop. Correct, but O(n²) allocations per roll.

### Phase 1 — ladder, limits, consolidation (delivers *Visible*)

`Currency.cs`
- Base unit becomes iron. Replace the constants:
  `ironToCopper = 5`, `ironToSilver = 60`, `ironToGold = 1200`,
  `copperToSilver = ironToSilver / ironToCopper` (12),
  `silverToGold = ironToGold / ironToSilver` (20).
- `Total`, the `Currency(uint)` decomposition and `TryGetPayment`'s `Take` order
  all follow the new ladder (iron, copper, silver, gold).
- Reorder the positional constructor to `(iron, copper, silver, gold)` so it stays
  in ascending value order. **This is signature-compatible with the old
  `(copper, iron, silver, gold)`, so the compiler will not catch a missed call
  site — check each one by hand.** Sites: `Currency.cs`,
  `CharacterInventory.CalculateCash`, tests.
- `ToString()` → `"{Gold}G, {Silver}S, {Copper}C, {Iron}I ({Total})"`.

`AbstractItem.cs` (`CurrencyItem`)
- `CalculateGoldValue` → Iron 1, Copper 5, Silver 60, Gold 1200.
- Rename it to `CalculateValue`; it returns base units, never gold, and the old
  name has always been a misnomer.
- `StackLimit` → Iron 120, Copper 60, Silver 20, Gold 12.
- Change `StackLimit` from `ItemStack` to `uint`. The enum only defines
  1/10/50/100 and the currency code already bypasses it with `(ItemStack)20u`, so
  it provides no safety — only a cast to forget.

`CharacterInventory.cs`
- Delete `CheckForCurrencyUpgrade` / `UpgradeCurrency` from the `TryStack` path.
- Add `Consolidate()`; honour `AutoConsolidate` on insert.

`AbstractDimensionalContainer.cs`
- Add `public virtual bool AutoConsolidate => false`.

`CurrencyDisplay.cs:23-26`
- Readout order → Gold, Silver, Copper, Iron.

UI
- Consolidate button beside Sort, wired to `CharacterInventory.Consolidate()`.

### Phase 2 — drop stacks (delivers *Earned*)

- Add per-type drop amount ranges (table above) to `ItemProvider`.
- `GenerateRandomLoot` returns `List<Package>` rather than `List<AbstractItem>` so
  amounts survive the trip; update `InventoryProvider.AddRandomLoot`.
- Reweight `Currency Type Distribution.asset` to Iron 40 / Copper 40 / Silver 8 /
  Gold 1. (Iron and Copper share a weight, so the stale-name bug is moot for those
  two entries — fix the labels anyway.)
- Measure the equipment-vs-currency income split before shipping.

### Phase 3 — deferred

Stash with `AutoConsolidate => true`. PoE-style currency uses and sinks. A
money-changer NPC who takes a cut on conversion.

## Verification

- `CurrencyTests` updated to the new ladder; round-trip `new Currency(n).Total == n`
  across the range.
- `TryGetPayment` still spends smallest-first, and never overpays by more than one
  coin of the smallest denomination it had to break into.
- `Consolidate()` is value-preserving: total before == total after.
- In play: after one run, iron and copper occupy multiple cells and Consolidate is
  a decision worth making.

## Research sources

- [Metals Used in Coins and Medals](https://www.coins-of-the-uk.co.uk/pics/metal.html)
  — iron "was not used as currency as it was heavy, brittle in the most commonly
  available cast form, and liable to rust."
- [Gold-Silver Ratio — Britannica Money](https://www.britannica.com/money/gold-silver-ratio)
- [What is the Gold to Silver price ratio? — BullionVault](https://www.bullionvault.com/silver-guide/gold-silver-ratio)
  — medieval Western Europe ~12:1, rising to ~16:1 by the 20th century.
