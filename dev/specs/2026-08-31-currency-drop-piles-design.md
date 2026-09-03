# Currency drop piles — Phase 2 of the currency redesign

Date: 2026-08-31
Status: Shipped 2026-08-31 - `4330704`..`7eee33b` on `main`.
KNOWN GAP: only `Example.unity` was wired to the Currency Drop Table asset
(guid `c47ab812...`); `HUD.unity` has zero references to it, so the drop table is
unset in that scene. Task 6 of the plan asked for both.
Base: `feature/currency-redesign` (Phase 0/1 shipped — `727c742`..`8daa96b`)

Implements **Phase 2** of `dev/specs/2026-08-30-currency-redesign-design.md`
("delivers *Earned*"). Phase 1 made coins *visible* (real piles, no auto-upgrade,
manual `Consolidate()`); Phase 2 makes each tier *earned* by turning a currency
drop into one roll that yields a pile, and reweighting the type table so iron and
copper are what you mostly see.

## Problem

A currency drop yields exactly one coin. `ItemProvider.GenerateRandomCurrency()`
returns a single `CurrencyItem`; `GenerateRandomLoot()` returns
`List<AbstractItem>`; every caller wraps each item in `new Package(_, _, 1u)`. The
drop pipeline has nowhere to carry a quantity.

Consequences:

- A "currency drop" is one iron coin worth 1 base unit. To feel a drop you need
  the RNG to hand you gold (1-in-a-few-hundred at the old 4/8/2/1 weights).
- The debug currency button (`InventoryProvider.AddRandomCurrency`) papers over it
  by looping `Amount` times — and even that is subtly wrong: it rolls the type
  *once* outside the loop, so the slider only ever adds a stack of one random
  type, never a mix.

Phase 1's `dev/specs/2026-08-30-currency-redesign-design.md` already specified the
fix (§ Phase 2). This document pins the implementation decisions that section left
open.

## Goals

1. One currency drop = one roll → a **pile** of one coin type, amount from a
   per-type range.
2. Iron and copper are ~90% of currency drops; silver ~1-in-11; gold ~1-in-89.
3. The drop pipeline carries quantity end to end, so the same mechanism is
   available to consumables later with no further plumbing.
4. No item price, affix `goldRatio`, or `Currency` value changes. Gold is still
   1200 base units.

Non-goals: changing consumable behaviour (arrows are rolled items and stack when
identical — working as intended); building income measurement (see § Deferred).

## Decisions

### Drop amounts: a `CurrencyDropTable` ScriptableObject

New `Assets/Scripts/InventorySystem/Data/Distributions/CurrencyDropTable.cs`:

```csharp
[CreateAssetMenu(fileName = "Currency Drop Table",
                 menuName = "Inventory System/Currency Drop Table")]
public class CurrencyDropTable : ScriptableObject
{
    [SerializeField] private Vector2Int iron   = new(10, 30);
    [SerializeField] private Vector2Int copper = new(4, 12);
    [SerializeField] private Vector2Int silver = new(1, 3);
    [SerializeField] private Vector2Int gold   = new(1, 1);

    public Vector2Int RangeFor(CurrencyType type) => type switch
    {
        CurrencyType.Iron   => iron,
        CurrencyType.Copper => copper,
        CurrencyType.Silver => silver,
        CurrencyType.Gold   => gold,
        _ => Vector2Int.zero,
    };

    public uint RollAmount(CurrencyType type)
    {
        var r = RangeFor(type);
        return r == Vector2Int.zero ? 0u : (uint)Random.Range(r.x, r.y + 1);
    }
}
```

- **Plain four-field SO**, not the enum-synced `EnumerationQuantity[]` pattern that
  `AbstractProbabilityDistribution` uses. The `CurrencyType` enum is frozen ("do
  not reorder the `CurrencyType` enum") and there are exactly four coins, so the
  array + `OnValidate` machinery buys nothing.
- **Flat uniform roll.** `Random.Range(min, max + 1)`. The ranges are narrow —
  only iron's (10–30) has real width — so a shaping curve is not worth its tuning
  surface. Retune the range bounds or the type weights instead. A curve is a
  localised drop-in later if piles feel swingy.
- Lives next to `CurrencyTypeDistribution.cs` in `Data/Distributions/` for
  discoverability. It is not an `AbstractProbabilityDistribution` subclass — it is
  a range table, not a probability distribution.

### `ItemProvider`: quantity flows to the loot boundary

| Member | Before | After |
|---|---|---|
| `GenerateRandomCurrency()` | `AbstractItem` | **`Package`** — roll type from `currencyTypeDistribution`, amount from `currencyDropTable`, return `new Package(null, new CurrencyItem(type), amount)` |
| `GenerateRandomItem()` | `AbstractItem` | **`Package`** — equipment / consumable arms wrap in `new Package(null, item, 1u)`; currency arm returns the pile; `NONE` returns `default` |
| `GenerateRandomLoot(uint)` | `List<AbstractItem>` | **`List<Package>`** |
| `GenerateCurrency(CurrencyType)` | `AbstractItem` | **unchanged** — one coin; still used by `CharacterInventory.AddChange`, `SellItenSlotDisplay`, `VendorSlotDisplay` |
| `GenerateRandomConsumable` / `GenerateRandomEquipment` / `GenerateRandomOf*Type` | `AbstractItem` | **unchanged** — wrapped by `GenerateRandomItem` |

New `[SerializeField] private CurrencyDropTable currencyDropTable;`.

`currencyTypeDistribution` carries `NONE` at weight 0, so `GenerateRandomCurrency`
never actually rolls it; if it somehow did, `RollAmount` returns 0 and the
resulting `Package` fails `IsValid` (`0 < Amount`) — a silent no-op drop, the same
outcome the current `GenerateCurrency(NONE)` path produces.

The `List<Package>` change stops at `GenerateRandomItem`. Equipment and consumable
generation keep their `AbstractItem` signatures; only the point where the three
categories are unified (`GenerateRandomItem`) and its aggregate
(`GenerateRandomLoot`) become quantity-aware.

`CalculateBonusDrops` (the `IncreasedItemQuantity` bonus-drop count) is unchanged —
it scales the number of rolls, orthogonal to pile size.

### Callers

- **`DummyTarget.OnDeath`** — `foreach (var package in GenerateRandomLoot())` then
  `PickUpItem(package)` directly. Drop the `new Package(null, item, 1u)` wrap.
- **`InventoryProvider.AddRandomLoot`** — iterate `Package`s into `PickUpItem`.
- **`InventoryProvider.AddRandomCurrency`** — loop `Amount` times, each iteration
  `PickUpItem(ItemProvider.Instance.GenerateRandomCurrency())`. One type roll + one
  pile roll per iteration. Same shape as `AddConsumable`; fixes the
  roll-once-outside-the-loop bug.
- **Delete** `InventoryProvider.SetItemToIron` / `SetItemToCopper` /
  `SetItemToSilver` / `SetItemToGold` and the `AddCurrency(CurrencyType)` helper.
  Confirmed dead — no `Button.onClick` in `Example.unity` or `HUD.unity` wires
  them, and no other code path calls them. `GenerateCurrency` (singular) stays; it
  has four live callers.

`LocalPlayer.PickUpItem(Package)` already takes a full package and spills across
cells via `TryAddToContainer`, so a pile larger than a stack limit is handled with
no change. All table amounts are ≤ their stack limit anyway (iron 30 ≤ 120,
copper 12 ≤ 60, silver 3 ≤ 20, gold 1 ≤ 12), so a single drop never spills;
accumulation across drops does, which is the point.

### Type weights: reweight `Currency Type Distribution.asset`

Quantities → **Copper 40 / Iron 40 / Silver 8 / Gold 1**. Enumeration labels are
already correct (fixed in `d987d44`); only the four `Quantity` values change. Focus
the asset in the inspector so `OnValidate` rebakes the `[ReadOnly]` `probabilities`
/ `exampleResults` caches, then commit the churn.

Resulting probabilities: Iron 0.449, Copper 0.449, Silver 0.090, Gold 0.011.

### Expected income (for the record, not a target)

At flat rolls and the reweighted table:

| Coin | Unit | Amount | E[value] | Chance | Contribution | Share |
|---|---|---|---|---|---|---|
| Iron   | 1    | 10–30 | 20   | 44.9% | 8.99  | 17.5% |
| Copper | 5    | 4–12  | 40   | 44.9% | 17.98 | 35.1% |
| Silver | 60   | 1–3   | 120  | 9.0%  | 10.79 | 21.1% |
| Gold   | 1200 | 1     | 1200 | 1.1%  | 13.48 | 26.3% |

EV ≈ 51 base units per currency drop. A gold drop is 40× the best iron pile and
20× the best copper pile — unambiguously the jackpot — while contributing only
about a quarter of currency income.

## Deferred — income-split validation

`dev/specs/2026-08-30-currency-redesign-design.md` § Phase 2 says "measure the
equipment-vs-currency income split before shipping." That check is **not** in this
work.

The denominator is unreliable. `AbstractItem.CalculateValue` (the former
`CalculateGoldValue`) sums `affix.Modifier.Value × goldRatio` with hand-guessed
per-stat ratios the code itself flags as provisional
(`// NOTE that goldRatios should differ based on the modifier type!`,
`StatName.Shield => 2.67f // Values not set yet`). Until those reflect a stat's
real contribution to combat and progression, an equipment-vs-currency income
measurement measures noise.

The reweight and the amount table ship as a feel dial — which the Phase 2 spec
already concedes they essentially are ("this table is close to a pure feel dial").
Revisit the measurement when item values are calibrated. See
`dev/specs/2026-08-31-item-value-open-questions.md`.

## Verification

- `CurrencyTests` (EditMode, `InventorySystem.Data.Tests`) is untouched and stays
  green — no `Currency` value or ratio changes.
- New code is in `Assembly-CSharp` (`ItemProvider`, `InventoryProvider`,
  `DummyTarget`) and a `ScriptableObject` (`CurrencyDropTable`), none of which an
  asmdef test assembly can reach. Verified by compile + in-editor check, per the
  Phase 1 precedent.
  - *Optional:* extract the roll core as a pure
    `static uint RollAmount(Vector2Int range, float roll01)` into
    `Data/Statistics/` (reachable by `InventorySystem.Data.Tests`) and pin: a roll
    of 0 returns `min`, a roll approaching 1 returns `max`, every result is in
    `[min, max]`, `min == max` returns that value.
- Compile: unity-mcp bridge (`Unity_RunCommand` + `Unity_GetConsoleLogs`) while the
  editor is open; batch `Unity.exe -runTests` if it is closed.
- In play: set the amount slider to ~10, press the currency button. Expect ~10
  piles landing, types roughly 45 / 45 / 9 / 1, each pile a single stack within its
  limit, the readout total sane, and `Consolidate` still folding them without
  changing the total. Kill a `DummyTarget` a few times and confirm currency drops
  now arrive as piles.

## Editor work (not scriptable from this repo)

1. Create `Assets/Scripts/InventorySystem/Data/Distributions/Currency Drop Table.asset`
   (`Assets ▸ Create ▸ Inventory System ▸ Currency Drop Table`); set the four
   ranges (defaults match the table above).
2. Assign it to the `ItemProvider` component's new **Currency Drop Table** field in
   **both** `Assets/Scenes/Example.unity` and `Assets/Scenes/HUD.unity` —
   `ItemProvider` is authored per-scene, not a prefab, so every serialized field is
   duplicated across the two copies.

## Out of scope

- Consumable drop stacks. The pipeline will support it (`GenerateRandomItem`
  returns `Package`), but consumable generation keeps amount 1 and its
  `AbstractItem` signature. Arrows rolling a spread of rarities that stack when
  identical is intended behaviour.
- Phase 3 (stash with `AutoConsolidate => true`; PoE-style currency sinks; a
  money-changer NPC).
- Any retune of `AbstractItem.CalculateValue` / affix `goldRatio`s.
- `AbstractProbabilityDistribution.Probabilities` allocating and re-sorting on
  every access — a real O(n²)-per-roll wart, but project-wide and correct; its own
  commit. Tracked in `dev/specs/2026-08-30-probability-distribution-rebuild-design.md`.
