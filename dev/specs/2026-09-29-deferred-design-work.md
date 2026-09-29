# Deferred design work — a parking lot harvested from shipped specs

Date: 2026-09-29
Status: Backlog — not scheduled. Collected on 2026-09-29 when the specs that recorded this
work were pruned, because their implementation had shipped and only these entries were still
live. Same shape as `2026-08-26-shop-currency-followups.md`: a list, not a design.

Entries are quoted from the spec they came from, with the line range named, so a decision is
read in the words it was made in rather than paraphrased. Nothing here is a ticket yet.

## 1. Calibrate the magic-find ladder

Harvested from `2026-08-30-probability-distribution-rebuild-design.md:311-327`, which shipped
2026-08-31 and was pruned. Base: ADR-0004 (*Magic find is a rarest-first cascade*).

**Nothing in the game grants `IncreasedItemRarity`**, so neither the per-item magic-find values
nor the maximum a fully geared character could reach in *this* game are known. The reference
frame used while choosing the curve was Diablo II's, where a fully geared dual-wielding
Barbarian reaches roughly **1167%** and a class carrying a shield roughly **1038%**, with most
farming builds sitting at 200–400%.

**Revisit this when item balancing starts.** Specifically:

- decide the magic-find budget per gear slot and per affix tier;
- compute the achievable maximum for this game's slot count and affix ranges;
- check that maximum against the landmark table below — if it lands far below ~400%, the
  Rare-overtakes-Magic crossover is unreachable and the factors want lowering; if it lands
  far above ~1200%, the Rare share is heading toward its 76.7% limit and the ladder wants
  a Set tier or a finite Magic factor;
- re-pin the landmark tests to whatever the retune produces.

The landmark table it is measured against — consequences of the base weights and factors, not
inputs, and what to look at when the ladder feels wrong:

| Landmark | Magic find |
| --- | --- |
| Magic overtakes Common | 50% |
| Common reaches 0%, Magic peaks at 55.2% | 200% |
| Rare overtakes Magic | 429% |
| Unique overtakes Magic | 1364% |

Common reaching exactly 0% at 200% magic find is a direct consequence of Diablo II giving
Magic quality no diminishing returns: the Magic rung saturates when `(80/240) × (1 + mf/100)
= 1`. Faithful to D2, where a high-magic-find character stops seeing white drops.

Two related entries stayed out of the shipped spec's Out of Scope and stay live with it: a
`Set` rarity tier (`//Set = 25` is still commented out in `ItemRarity.cs`; D2's factor of 500
sits between Rare and Unique if it is ever introduced), and per-equipment-type distributions
for uniques (the two `// TODO: individual probabilityDistribution for each equipment type`
sites in `ItemProvider`).

## 2. Combat depth — the first lever

Harvested from `2026-09-02-mvp-simulation-loop-design.md:392-394`, which shipped with issues
#16–#27 all closed and was pruned. Base: ADR-0010.

`CalculateDamageOutput` splits: the Strike drops its `× (1 + AttackSpeed · 0.01)` term
(double-counts against a real cadence); the Cast takes no `AttackSpeed` term. **A future
`CastCostReduction` stat is the first depth lever, deferred.** Named skills, cooldowns, crit,
status effects, resistances and penetration are all still out of scope for the loop as built.

## 3. Foundational-rework backlog, tiers 3 and 4

Harvested from `2026-08-31-foundational-rework-design.md:432-451`, which shipped with issues
#2–#15 all closed and was pruned. These are the features the item model, the transaction
seam and the wallet were built to make cheap; none has been designed.

### Tier 3 — inventory & economy features

Each cheap once the three seams exist.

- **Stash tabs** + a stash with `AutoConsolidate => true`.
- **Crafting bench** — `ItemInstance` mutation ops plus Seam 2; re-enable `Crafted`.
- **Sockets & gems** — instance state; gems are a definition category.
- **Identification** — instance flag plus a scroll consumable or a vendor.
- **Consumable effects + potion belt.**
- **Vendor depth** — buyback, repair, gamble, leaning on `ItemGenerator`; a
  currency-exchange NPC. See `2026-08-26-shop-currency-followups.md`.
- **Item requirements enforced on equip** — `ItemRequirement` plus character attributes.
- **Stack-split UI.**
- **A dedicated vendor container** — `2026-08-26-shop-currency-followups.md` §1.
- **Drag-cancel / return-to-origin + purchase refund** —
  `2026-08-26-shop-currency-followups.md` §2; Seam 2 is its foundation.

### Tier 4 — depth cleanups

- Collapse any residual `ItemProvider` switch surface once the catalog is data.
- Ground the `goldRatio` switch in a combat/progression model — see
  `2026-08-31-item-value-open-questions.md` §1. Follows the combat cluster.
- Push the packing implementation down into `AbstractDimensionalContainer` so inventory,
  vendor and stash all go thin — `2026-08-26-shop-currency-followups.md` §1.
- Larger 2H footprints and dimension-based value —
  `2026-08-31-item-value-open-questions.md` §2–3.
- Split the `InventoryProvider` god object (`InventoryProvider.cs:14`).

## Not harvested, and why

- **The Combat Panel `CONTEXT.md` line** — `CONTEXT.md:380` read "Exclusive with the
  Side Panels by Run phase, not by a toggle group", which the PanelGroup rework had made
  false. Issue #87 took the `CONTEXT.md` pass on 2026-09-29 and the line now stands as
  correct: the left-panel group is gone, so Combat Panel exclusivity is phase-only by
  construction rather than by a rule. Not a backlog entry — it was a docs gap with an
  owner.
- **The persistence constraints** (`ItemDefinition.Id` as an explicit serialized string, the
  `ItemInstance` POCO DTO round-trip, container state as `[{x, y, definitionId + DTO, amount}]`,
  the named-but-unbuilt `ISaveStore` seam) — already written up in
  `2026-09-28-save-serialization-prior-art.md`.
- **The Tier 2 combat-simulation cluster** — that is not backlog, it is what the MVP
  simulation loop built. Its outcome is in ADR-0008/0009/0010 and `CONTEXT.md` §Runs/§Combat.
