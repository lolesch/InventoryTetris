# Balance sweep (#296) - campaign

Generated 2026-10-17 22:14. Real `EncounterSimulation`, `EncounterTuning.Standard()`, Engagement 3. Careless = no Recall, no potion. Played = at 35 % Health drink a potion if carried (+35 %), else Recall: the portal opens over 0,5 s, a trip costs 15 s.

Hero base: Health 400, Armor 12, MagicResist 12, PhysicalDamage 32, MagicalDamage 14, AttackSpeed 0,75, HealthRegeneration 0, Resource 200, ResourceRegeneration 6, MovementSpeed 325.
Per level: Health +20, PhysicalDamage +2,5, MagicalDamage +2,5, Resource +8. XP bar: L1 280, L2 560, L3 940, L4 1420, L5 2000, L6 2680, L7 3460, L8 4340.
Populations: Thornwood 5, Ashfall 6, synthetic 5. Mitigation: R / (R + 100).

## Loot `live`

The code and data as they are: authored tables (Fail Bucket 0), the generator's uniform pick over all 180 definitions (156 are Uniques), no item-level scaling.

| policy | build | Location | Sends to Complete (median / max) | Deaths (median) | trips (median) | potions drunk / dropped | level at Complete | items dropped / equipped | time (median, min) | hero at Complete (seed 1) |
|---|---|---|---|---|---|---|---|---|---|---|
| Careless | Physical | Thornwood | 2 / 2 | 1 | 0 | 0 / 1 | 4 | 134 / 30 | 4,8 | HP 1037, Arm 49 (33 %), MR 134 (57 %), Phys 70, Mag 38, AS 1,16, regen 0,0, 14 worn |
| Careless | Physical | Ashfall | 1 / 1 | 0 | 0 | 0 / 1 | 6 | 102 / 11 | 3,1 | HP 1203, Arm 85 (46 %), MR 154 (61 %), Phys 83, Mag 47, AS 1,17, regen 0,0, 14 worn |
| Careless | Magical | Thornwood | 2 / 2 | 1 | 0 | 0 / 1 | 4 | 104 / 26 | 4,5 | HP 1031, Arm 35 (26 %), MR 90 (47 %), Phys 35, Mag 38, AS 1,16, regen 0,0, 13 worn |
| Careless | Magical | Ashfall | 1 / 1 | 0 | 0 | 0 / 1 | 6 | 102 / 13 | 3,8 | HP 1199, Arm 60 (38 %), MR 115 (53 %), Phys 45, Mag 46, AS 1,26, regen 0,0, 14 worn |
| Played | Physical | Thornwood | 1 / 1 | 0 | 1 | 0 / 0 | 3 | 70 / 21 | 3,1 | HP 894, Arm 37 (27 %), MR 67 (40 %), Phys 69, Mag 39, AS 1,06, regen 0,0, 12 worn |
| Played | Physical | Ashfall | 1 / 1 | 0 | 0 | 0 / 1 | 6 | 102 / 15 | 3,2 | HP 1206, Arm 73 (42 %), MR 94 (48 %), Phys 96, Mag 47, AS 1,34, regen 0,0, 14 worn |
| Played | Magical | Thornwood | 1 / 1 | 0 | 2 | 0 / 0 | 3 | 70 / 22 | 4,3 | HP 894, Arm 37 (27 %), MR 67 (40 %), Phys 35, Mag 39, AS 1,06, regen 0,0, 12 worn |
| Played | Magical | Ashfall | 1 / 1 | 0 | 1 | 0 / 1 | 6 | 102 / 15 | 4,0 | HP 1206, Arm 73 (42 %), MR 94 (48 %), Phys 48, Mag 47, AS 1,34, regen 0,0, 14 worn |

## Loot `proposed`

Fail Bucket ~76% of kills; the rarity roll comes first and only a Unique roll picks a Unique definition (~1 in 500 kills); flat affixes scale +25% per item level past 1.

| policy | build | Location | Sends to Complete (median / max) | Deaths (median) | trips (median) | potions drunk / dropped | level at Complete | items dropped / equipped | time (median, min) | hero at Complete (seed 1) |
|---|---|---|---|---|---|---|---|---|---|---|
| Careless | Physical | Thornwood | 2 / 2 | 1 | 0 | 0 / 2 | 4 | 30 / 11 | 5,0 | HP 806, Arm 26 (20 %), MR 123 (55 %), Phys 40, Mag 70, AS 0,83, regen 0,0, 8 worn |
| Careless | Physical | Ashfall | 1 / 3 | 0 | 0 | 0 / 1 | 6 | 27 / 9 | 3,6 | HP 1332, Arm 68 (40 %), MR 263 (72 %), Phys 45, Mag 99, AS 0,90, regen 0,0, 9 worn |
| Careless | Magical | Thornwood | 2 / 3 | 1 | 0 | 0 / 1 | 4 | 24 / 11 | 5,1 | HP 806, Arm 26 (20 %), MR 123 (55 %), Phys 20, Mag 70, AS 0,83, regen 0,0, 8 worn |
| Careless | Magical | Ashfall | 2 / 5 | 1 | 0 | 0 / 2 | 7 | 40 / 13 | 7,0 | HP 1921, Arm 88 (47 %), MR 251 (72 %), Phys 33, Mag 110, AS 0,98, regen 0,0, 12 worn |
| Played | Physical | Thornwood | 1 / 1 | 0 | 1 | 0 / 1 | 3 | 16 / 8 | 3,1 | HP 786, Arm 12 (11 %), MR 97 (49 %), Phys 48, Mag 44, AS 0,75, regen 0,0, 5 worn |
| Played | Physical | Ashfall | 1 / 1 | 0 | 1 | 1 / 1 | 6 | 21 / 9 | 4,1 | HP 1136, Arm 32 (24 %), MR 162 (62 %), Phys 77, Mag 52, AS 0,84, regen 0,0, 8 worn |
| Played | Magical | Thornwood | 1 / 1 | 0 | 2 | 0 / 1 | 3 | 16 / 8 | 4,3 | HP 786, Arm 12 (11 %), MR 97 (49 %), Phys 24, Mag 44, AS 0,75, regen 0,0, 5 worn |
| Played | Magical | Ashfall | 1 / 1 | 0 | 2 | 1 / 1 | 6 | 21 / 9 | 5,3 | HP 1136, Arm 32 (24 %), MR 162 (62 %), Phys 29, Mag 86, AS 0,75, regen 0,0, 8 worn |

## Loot `deck`

Proposed, with the rarity dealt from a card deck of 1000 per Location per hero (760 NoDrop, 2 Unique): exactly 1 Unique in every 500 kills, and no long dry streak.

| policy | build | Location | Sends to Complete (median / max) | Deaths (median) | trips (median) | potions drunk / dropped | level at Complete | items dropped / equipped | time (median, min) | hero at Complete (seed 1) |
|---|---|---|---|---|---|---|---|---|---|---|
| Careless | Physical | Thornwood | 2 / 2 | 1 | 0 | 0 / 1 | 4 | 32 / 11 | 5,0 | HP 623, Arm 31 (24 %), MR 100 (50 %), Phys 40, Mag 45, AS 0,82, regen 0,0, 5 worn |
| Careless | Physical | Ashfall | 2 / 2 | 1 | 0 | 0 / 1 | 7 | 34 / 11 | 5,3 | HP 1548, Arm 40 (29 %), MR 249 (71 %), Phys 103, Mag 29, AS 1,09, regen 0,0, 10 worn |
| Careless | Magical | Thornwood | 2 / 3 | 1 | 0 | 0 / 1 | 4 | 28 / 10 | 5,2 | HP 460, Arm 12 (11 %), MR 100 (50 %), Phys 20, Mag 45, AS 0,82, regen 0,0, 4 worn |
| Careless | Magical | Ashfall | 2 / 4 | 1 | 0 | 0 / 2 | 7 | 41 / 13 | 6,5 | HP 1296, Arm 96 (49 %), MR 203 (67 %), Phys 44, Mag 63, AS 1,00, regen 0,0, 12 worn |
| Played | Physical | Thornwood | 1 / 1 | 0 | 1 | 0 / 0 | 3 | 16 / 7 | 3,1 | HP 440, Arm 12 (11 %), MR 100 (50 %), Phys 37, Mag 42, AS 0,75, regen 0,0, 4 worn |
| Played | Physical | Ashfall | 1 / 1 | 0 | 2 | 0 / 1 | 6 | 26 / 10 | 4,2 | HP 500, Arm 12 (11 %), MR 164 (62 %), Phys 85, Mag 59, AS 0,96, regen 0,0, 7 worn |
| Played | Magical | Thornwood | 1 / 1 | 0 | 2 | 0 / 0 | 3 | 16 / 7 | 4,3 | HP 440, Arm 12 (11 %), MR 100 (50 %), Phys 19, Mag 42, AS 0,75, regen 0,0, 4 worn |
| Played | Magical | Ashfall | 1 / 1 | 0 | 3 | 0 / 1 | 6 | 26 / 10 | 5,1 | HP 500, Arm 12 (11 %), MR 164 (62 %), Phys 32, Mag 81, AS 0,90, regen 0,0, 7 worn |

