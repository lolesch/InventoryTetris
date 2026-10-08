# Deferred design work — a parking lot harvested from shipped specs

Date: 2026-09-29
Status: Backlog — not scheduled. Collected on 2026-09-29 when the specs that recorded this
work were pruned, because their implementation had shipped and only these entries were still
live. Same shape as `2026-08-26-shop-currency-followups.md`: a list, not a design.

Entries are quoted from the spec they came from, with the line range named, so a decision is
read in the words it was made in rather than paraphrased. Nothing here is a ticket yet.

Updated 2026-10-08: entries 4-7 were not harvested from a pruned spec. They record work parked by
the design interview behind `2026-10-08-spatial-combat-design.md` and
`2026-10-08-floor-loot-design.md`, so they are summaries of that interview, not quotes.

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

## 4. An effects system (spec C)

Parked by the 2026-10-08 interview. One reusable system for anything that changes a stat or a
resource over time, not a potion system: potions (a flat heal, or a short health-regen boost),
books (experience, or a short stat boost), damage over time, and later slows, shields and
immunity. Tier 3's "Consumable effects + potion belt" in entry 3 is the same work.

- **Prior art:** AbilityCombat's `Pawns/Abilities/Effects/`: a command, an effect and a receiver
  per kind (damage, DoT, stat modifier, shield, root, immunity, drain and fill resource).
  Read it before designing; do not port it, it is built on scene objects.
- **Prerequisites:** stat-backed enemies (#210), so an effect can target the hero and an enemy the
  same way, and the typed hit event (#211).
- **To decide:** the duration and tick model on the sim clock; stacking and refresh rules; what an
  effect can target and who its source is; whether the player sees active effects; whether active
  effects outlive a Run (mid-Run state is otherwise discarded on quit).

## 5. Itemization and difficulty (spec D)

Parked by the 2026-10-08 interview. The symptom is that the hero cannot die and his gear
outscales the enemies within a few levels. Locations stay a difficulty ladder with no monster
scaling; what scales is loot.

- **D1, the numbers and the model:** hero starter stats; item level driving affix ranges or pools
  (loot generation only); an affix design (the current ones are a prototype, with no thought for
  where they appear or how high); and a level-scaling formula (the XP balance term in the
  Encounter is the only one today). Tune from a headless run of the Unity-free sim that reports
  time to kill and time to die across levels and builds. Entry 1 (magic-find calibration) belongs
  to the same pass. Weapon range stays a base property, not an affix (spatial-combat spec).
- **D2, consumables:** *identify* as a use for consumables (entry 3's "Identification"); potions
  that are more frequent and actually heal; books that give XP or a short boost (potions and
  books need entry 4); and *arrows*, which carry a stack limit but cannot stack because they
  carry affixes, and have no slot. Arrows need a design decision, either an ammo slot or
  dropping their affixes so they stack again.
- **Order:** the numbers depend on the spatial combat epic (#204), because ranged enemies cost the
  hero Strike time.

## 6. Enemy loadouts (spec E)

Parked by the 2026-10-08 interview. The idea: loot is generated when an enemy spawns, the enemy
equips what it can, and it drops it on death. That makes enemies vary and ties their strength to
the same affix tables as gear. It is a mechanism swap (loot rolls move from kill time to spawn
time), so it follows CLAUDE.md's rule: `/rederive` before the spec, then `/drift-review` twice.

It starts as a **prototype**: a headless run that rolls gear for enemies from the current affix
tables scaled by source level, fights them against several hero builds, and answers whether the
variety reads as variety (spread in time to kill and damage taken) or as noise, and whether it
narrows the gap between hero and enemy strength. It needs item levels (entry 5) and stat-backed
enemies (#210), and the prototype can fake the first.

The six problems to settle before a spec:

1. It inherits the affix design problem; enemies wearing today's affixes are as unbalanced as the gear.
2. Magic find and item quantity apply at kill time today; with spawn-time rolls they would use
   stale values if the hero swaps gear mid-fight.
3. What drops: everything the enemy wore, or each item at a chance, which keeps the existing
   drop-rate tuning.
4. Archetype identity: the archetype owns range and damage type, gear only adjusts stat values.
5. Equip rules: reuse the hero's slot types, not a second system.
6. Regeneration: an enemy wearing a regeneration affix would regenerate, which contradicts
   "enemies do not regenerate", so the enemy affix pool must exclude it or the rule changes.

## 7. Parked from the 2026-10-08 specs

Left out of the spatial-combat and floor-loot specs on purpose:

- A Cast wind-up and telegraphs; enemy casts, enemy mana, friendly fire.
- Separation steering between enemies, and a stamina cost for movement.
- A visual marker for the Cast's area; annulus and arc as shapes of their own (they are the
  inner radius on disk and sector).
- Skills as such; the Cast definition is tuning until they exist.
- The auto-pickup redesign: a pickup policy that reads `UserSettings` (a minimum coin
  denomination, for example), and any rescue of evicted items beyond banking coins.
- A timed decay for floor drops, on top of capacity.
- Death taking equipped gear. ADR-0009 stands; a hardcore mode would make the Corpse obsolete.

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
