# Balance sweep - findings (#296, map #293)

PROTOTYPE, throwaway. Branch `prototype/296-balance-sweep`, never merged.

**Harness:** `Assets/Scripts/Tests/EditMode/Simulation/BalanceSweepPrototype.cs`, an `[Explicit]` EditMode
fixture that drives the real `EncounterSimulation` (`EncounterTuning.Standard()`, Engagement 3) headless:

```
dev/run-tests.sh EditMode "BalanceSweepPrototype.Sweep0Census"   # what the loot is
dev/run-tests.sh EditMode "BalanceSweepPrototype.Sweep1Authored" # Thornwood / Ashfall x build x level x gear
dev/run-tests.sh EditMode "BalanceSweepPrototype.Sweep2Curve"    # synthetic S1-10
dev/run-tests.sh EditMode "BalanceSweepPrototype.Sweep3Campaign" # fresh hero -> Thornwood Complete -> Ashfall Complete
```

Each writes `out-<section>.md` here; the curve section has not been rerun on the v2 loot (its v1 table went with the parametric gear). Knobs sit at the top of the file (`Knobs`, `LootVariant`).
The JavaScript combat-cluster prototype was not extended: it predates spatial combat, stat-backed enemies and
the Cast shapes, and the C# sim is engine-free and seeded, so the real fight runs in ~1 min a section.

## What it models

- The Run-risk levers (#295): no level-up heal, base Health regen 0, a flat growth table, enemies never regenerate.
- A Send = a Population of Encounters (ADR-0023, not built yet; the harness stops at the Nth clear).
- **Careless** hero: no Recall, no potion. **Played** hero: at 35% Health drinks a potion if carried (+35% Health,
  a stand-in for #298/#300), else Recalls: the portal opens over 0.5 s of sim time with the fight running, then the
  frozen Run resumes at full Health (Healer free), a trip costs 15 s.
- Loot (v2): the real `ItemGenerator` over the real `Item Catalog` asset, Thornwood's and Ashfall's authored
  category and rarity odds, real affix pools, `MutableFloat`'s math (flat, then percent-add, then percent-mult),
  14 equipment slots, an upgrade is worn when it raises effective Health x DPS. A Death loses 25% of the bar.
- v1 (commit `1cf8b0c8`) used a parametric gear budget instead; its outputs are superseded.

## Findings

1. **A played hero cannot fail.** The Run holds on a Recall, wounded enemies stay wounded and the Healer is
   free, so a hero who Recalls at 35% always Completes; the 0.5 s opening killed no one in any cell. Difficulty for
   a played hero is **trips per Send**; Deaths are the careless hero's. Accepted (user): a hero can die without the
   trips, never has to when played right - a Hardcore-mode requirement.
2. **The live starter hero is too safe and its Cast is dead.** DefaultHero (640 HP, 26/30 resists, 59 Phys,
   0 Mag) with regen 0: a careless L1 hero with nothing equipped clears ~8 Thornwood Encounters before dying; with
   +0.8 HP/s regen from gear it never dies there. Magical Damage 0 means the unequipped Cast deals nothing.
3. **A 200-HP / 0-resist hero is too harsh**: careless Thornwood took 10 Sends and 9 Deaths, a pure caster never
   Completed, played Ashfall took 13-21 trips.
4. **Middle starter hero** (taken as the starting point): Health 400, Armor 12, Magic Resist 12, Physical 32,
   Magical 14, Attack Speed 0.75, Resource 200 @ 6/s, Health regen 0; per level +20 Health, +2.5 Physical,
   +2.5 Magical, +8 Resource. Thornwood Population 5, Ashfall 6. With real loot: a careless fresh hero dies on
   Send 1 and Completes Thornwood on Send 2 (median, all loot variants); a played one Completes Send 1 with 1-2
   trips; level 3-4 at Thornwood's Complete.
5. **Live loot hands out a Unique every other kill.** `ItemGenerator.PickDefinition` draws uniformly over every
   definition of the category; 156 of the 180 are Unique definitions, so 89% of equipment drops are Unique
   definitions whatever rarity was rolled. Thornwood's Fail Bucket is 0 and its Unique rarity weight 20/300.
6. **Proposed loot:** Fail Bucket ~76% of kills (~16 items a Thornwood Send, ~11 of them equipment, ~1 potion),
   the rarity rolled first and only a Unique roll picks a Unique definition (~1 in 500 kills). A **card deck**
   (1000 cards per Location per hero, 760 NoDrop, 2 Unique, dealt without replacement) gives the same campaign
   numbers and makes 1-in-500 exact - handed to "What each Rarity is for".
7. **Defense stacks to immunity.** Magic Resist rolls flat +16-25 on eight item types and Armor +10-15 on six;
   one equipped item per slot still stacks to MR 100-160 within a Send or two (238-263 with +25%/item level), and
   linear percent clamps at 100: immune to Skirmishers. `Armor / (100 + Armor)` (League of Legends; the sweep's
   K = 100) keeps 250 at 71%. The defense model itself is reopened (base defense on items, penetration order).
8. **Gear outgrows the enemies.** An armor piece rolls +100-150 Health against a 400 base; enemy hits are
   2.6 (S2 Brute) to 3.0 (S5 Skirmisher). Even with `Armor/(100+Armor)`, five Thornwood Sends of gear clear Ashfall
   careless, and a hero going straight on from Thornwood's Complete needs 1-3 trips there. Decision (user): shrink
   affixes and scale them by item level, rather than raise the enemy curves.
9. **Health Regeneration on gear is dead**: it rolls only as percent-add, and base regen is now 0.
10. **Item level does nothing to an item yet**: `ItemGenerator.RollModifier` ignores it; rarity only sets the affix
    count (1/2/3/3). The sweep's `ItemLevelScale` knob (+25% flat per level) is an assumption.
11. A NoDrop rarity roll throws inside `ItemGenerator.Roll`; `LootFlow` catches it as "no items this kill", which
    works only because each kill rolls once.

## What waits

The final numbers (enemy curves, affix ranges per item level, XP per Location) wait on **the defense model** and
**What each Rarity is for**; the re-sweep reruns this harness once both are settled.
