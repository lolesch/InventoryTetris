# Balance sweep (#296) - authored

Generated 2026-10-17 22:11. Real `EncounterSimulation`, `EncounterTuning.Standard()`, Engagement 3. Careless = no Recall, no potion. Played = at 35 % Health drink a potion if carried (+35 %), else Recall: the portal opens over 0,5 s, a trip costs 15 s.

Hero base: Health 400, Armor 12, MagicResist 12, PhysicalDamage 32, MagicalDamage 14, AttackSpeed 0,75, HealthRegeneration 0, Resource 200, ResourceRegeneration 6, MovementSpeed 325.
Per level: Health +20, PhysicalDamage +2,5, MagicalDamage +2,5, Resource +8. XP bar: L1 280, L2 560, L3 940, L4 1420, L5 2000, L6 2680, L7 3460, L8 4340.
Populations: Thornwood 5, Ashfall 6, synthetic 5. Mitigation: R / (R + 100).

## Loot `live`

The code and data as they are: authored tables (Fail Bucket 0), the generator's uniform pick over all 180 definitions (156 are Uniques), no item-level scaling.

| Location | build | level | gear | careless Completes | careless time to die | Encounters before death | s / kill | played trips / Send | played deaths | hero (seed 1) |
|---|---|---|---|---|---|---|---|---|---|---|
| Thornwood | Physical | 1 | none | 13% | 161 s | 4,1/5 | 2,5 | 1,0 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 32, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 1 | 1 Thornwood Send | 100% | never | 23,8/5 | 1,8 | 0,0 | 0% | HP 1022, Arm 74 (43 %), MR 93 (48 %), Phys 61, Mag 53, AS 1,16, regen 0,0, 13 worn |
| Thornwood | Physical | 1 | 5 Thornwood Sends | 100% | never | 24,4/5 | 1,7 | 0,0 | 0% | HP 1186, Arm 87 (47 %), MR 137 (58 %), Phys 78, Mag 34, AS 1,16, regen 0,0, 14 worn |
| Thornwood | Physical | 1 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1254, Arm 107 (52 %), MR 166 (62 %), Phys 81, Mag 34, AS 1,26, regen 0,0, 14 worn |
| Thornwood | Physical | 1 | 5 Ashfall Sends | 100% | never | 24,1/5 | 1,7 | 0,0 | 0% | HP 1184, Arm 110 (52 %), MR 186 (65 %), Phys 71, Mag 41, AS 1,27, regen 0,0, 14 worn |
| Thornwood | Physical | 3 | none | 100% | 195 s | 5,3/5 | 2,4 | 1,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 37, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 3 | 1 Thornwood Send | 100% | never | 23,8/5 | 1,8 | 0,0 | 0% | HP 1062, Arm 74 (43 %), MR 93 (48 %), Phys 66, Mag 58, AS 1,16, regen 0,0, 13 worn |
| Thornwood | Physical | 3 | 5 Thornwood Sends | 100% | never | 24,3/5 | 1,7 | 0,0 | 0% | HP 1226, Arm 87 (47 %), MR 137 (58 %), Phys 83, Mag 39, AS 1,16, regen 0,0, 14 worn |
| Thornwood | Physical | 3 | 20 Thornwood Sends | 100% | never | 24,3/5 | 1,7 | 0,0 | 0% | HP 1294, Arm 107 (52 %), MR 166 (62 %), Phys 86, Mag 39, AS 1,26, regen 0,0, 14 worn |
| Thornwood | Physical | 3 | 5 Ashfall Sends | 100% | never | 24,1/5 | 1,7 | 0,0 | 0% | HP 1224, Arm 110 (52 %), MR 186 (65 %), Phys 76, Mag 46, AS 1,27, regen 0,0, 14 worn |
| Thornwood | Physical | 6 | none | 100% | 239 s | 7,3/5 | 2,3 | 0,4 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 45, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 6 | 1 Thornwood Send | 100% | never | 23,9/5 | 1,7 | 0,0 | 0% | HP 1122, Arm 74 (43 %), MR 93 (48 %), Phys 74, Mag 66, AS 1,16, regen 0,0, 13 worn |
| Thornwood | Physical | 6 | 5 Thornwood Sends | 100% | never | 24,4/5 | 1,7 | 0,0 | 0% | HP 1286, Arm 87 (47 %), MR 137 (58 %), Phys 91, Mag 47, AS 1,16, regen 0,0, 14 worn |
| Thornwood | Physical | 6 | 20 Thornwood Sends | 100% | never | 24,4/5 | 1,7 | 0,0 | 0% | HP 1354, Arm 107 (52 %), MR 166 (62 %), Phys 94, Mag 47, AS 1,26, regen 0,0, 14 worn |
| Thornwood | Physical | 6 | 5 Ashfall Sends | 100% | never | 24,3/5 | 1,7 | 0,0 | 0% | HP 1284, Arm 110 (52 %), MR 186 (65 %), Phys 84, Mag 54, AS 1,27, regen 0,0, 14 worn |
| Thornwood | Magical | 1 | none | 0% | 131 s | 2,1/5 | 3,7 | 2,0 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 16, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 1 | 1 Thornwood Send | 100% | never | 21,8/5 | 1,9 | 0,0 | 0% | HP 1022, Arm 74 (43 %), MR 93 (48 %), Phys 31, Mag 53, AS 1,16, regen 0,0, 13 worn |
| Thornwood | Magical | 1 | 5 Thornwood Sends | 100% | never | 22,6/5 | 1,9 | 0,0 | 0% | HP 1186, Arm 87 (47 %), MR 137 (58 %), Phys 31, Mag 53, AS 1,16, regen 0,0, 14 worn |
| Thornwood | Magical | 1 | 20 Thornwood Sends | 100% | never | 23,0/5 | 1,8 | 0,0 | 0% | HP 1254, Arm 107 (52 %), MR 166 (62 %), Phys 41, Mag 34, AS 1,26, regen 0,0, 14 worn |
| Thornwood | Magical | 1 | 5 Ashfall Sends | 100% | never | 22,6/5 | 1,9 | 0,0 | 0% | HP 1184, Arm 110 (52 %), MR 186 (65 %), Phys 31, Mag 53, AS 1,25, regen 0,0, 14 worn |
| Thornwood | Magical | 3 | none | 0% | 152 s | 2,9/5 | 3,3 | 2,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 19, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 3 | 1 Thornwood Send | 100% | never | 21,9/5 | 1,9 | 0,0 | 0% | HP 1062, Arm 74 (43 %), MR 93 (48 %), Phys 33, Mag 58, AS 1,16, regen 0,0, 13 worn |
| Thornwood | Magical | 3 | 5 Thornwood Sends | 100% | never | 22,9/5 | 1,8 | 0,0 | 0% | HP 1226, Arm 87 (47 %), MR 137 (58 %), Phys 33, Mag 58, AS 1,16, regen 0,0, 14 worn |
| Thornwood | Magical | 3 | 20 Thornwood Sends | 100% | never | 23,0/5 | 1,8 | 0,0 | 0% | HP 1294, Arm 107 (52 %), MR 166 (62 %), Phys 43, Mag 39, AS 1,26, regen 0,0, 14 worn |
| Thornwood | Magical | 3 | 5 Ashfall Sends | 100% | never | 22,6/5 | 1,8 | 0,0 | 0% | HP 1224, Arm 110 (52 %), MR 186 (65 %), Phys 33, Mag 58, AS 1,25, regen 0,0, 14 worn |
| Thornwood | Magical | 6 | none | 13% | 186 s | 4,1/5 | 2,8 | 1,0 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 22, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 6 | 1 Thornwood Send | 100% | never | 22,6/5 | 1,9 | 0,0 | 0% | HP 1122, Arm 74 (43 %), MR 93 (48 %), Phys 37, Mag 66, AS 1,16, regen 0,0, 13 worn |
| Thornwood | Magical | 6 | 5 Thornwood Sends | 100% | never | 23,3/5 | 1,8 | 0,0 | 0% | HP 1286, Arm 87 (47 %), MR 137 (58 %), Phys 37, Mag 66, AS 1,16, regen 0,0, 14 worn |
| Thornwood | Magical | 6 | 20 Thornwood Sends | 100% | never | 23,4/5 | 1,8 | 0,0 | 0% | HP 1354, Arm 107 (52 %), MR 166 (62 %), Phys 47, Mag 47, AS 1,26, regen 0,0, 14 worn |
| Thornwood | Magical | 6 | 5 Ashfall Sends | 100% | never | 23,3/5 | 1,8 | 0,0 | 0% | HP 1284, Arm 110 (52 %), MR 186 (65 %), Phys 37, Mag 66, AS 1,25, regen 0,0, 14 worn |
| Ashfall | Physical | 1 | none | 0% | 37 s | 0,0/6 | 3,4 | 6,8 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 32, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 1 | 1 Thornwood Send | 100% | 381 s | 11,4/6 | 1,8 | 0,3 | 0% | HP 1022, Arm 74 (43 %), MR 93 (48 %), Phys 61, Mag 53, AS 1,16, regen 0,0, 13 worn |
| Ashfall | Physical | 1 | 5 Thornwood Sends | 100% | 590 s | 17,5/6 | 1,8 | 0,0 | 0% | HP 1186, Arm 87 (47 %), MR 137 (58 %), Phys 78, Mag 34, AS 1,16, regen 0,0, 14 worn |
| Ashfall | Physical | 1 | 20 Thornwood Sends | 100% | never | 18,9/6 | 1,8 | 0,0 | 0% | HP 1254, Arm 107 (52 %), MR 166 (62 %), Phys 81, Mag 34, AS 1,26, regen 0,0, 14 worn |
| Ashfall | Physical | 1 | 5 Ashfall Sends | 100% | never | 18,6/6 | 1,8 | 0,0 | 0% | HP 1184, Arm 110 (52 %), MR 186 (65 %), Phys 71, Mag 41, AS 1,27, regen 0,0, 14 worn |
| Ashfall | Physical | 3 | none | 0% | 59 s | 0,6/6 | 3,4 | 6,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 37, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 3 | 1 Thornwood Send | 100% | 391 s | 12,3/6 | 1,8 | 0,0 | 0% | HP 1062, Arm 74 (43 %), MR 93 (48 %), Phys 66, Mag 58, AS 1,16, regen 0,0, 13 worn |
| Ashfall | Physical | 3 | 5 Thornwood Sends | 100% | never | 17,8/6 | 1,8 | 0,0 | 0% | HP 1226, Arm 87 (47 %), MR 137 (58 %), Phys 83, Mag 39, AS 1,16, regen 0,0, 14 worn |
| Ashfall | Physical | 3 | 20 Thornwood Sends | 100% | never | 18,9/6 | 1,8 | 0,0 | 0% | HP 1294, Arm 107 (52 %), MR 166 (62 %), Phys 86, Mag 39, AS 1,26, regen 0,0, 14 worn |
| Ashfall | Physical | 3 | 5 Ashfall Sends | 100% | never | 18,8/6 | 1,8 | 0,0 | 0% | HP 1224, Arm 110 (52 %), MR 186 (65 %), Phys 76, Mag 46, AS 1,27, regen 0,0, 14 worn |
| Ashfall | Physical | 6 | none | 0% | 68 s | 1,0/6 | 2,6 | 4,3 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 45, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 6 | 1 Thornwood Send | 100% | 434 s | 13,3/6 | 1,8 | 0,0 | 0% | HP 1122, Arm 74 (43 %), MR 93 (48 %), Phys 74, Mag 66, AS 1,16, regen 0,0, 13 worn |
| Ashfall | Physical | 6 | 5 Thornwood Sends | 100% | never | 18,5/6 | 1,8 | 0,0 | 0% | HP 1286, Arm 87 (47 %), MR 137 (58 %), Phys 91, Mag 47, AS 1,16, regen 0,0, 14 worn |
| Ashfall | Physical | 6 | 20 Thornwood Sends | 100% | never | 19,0/6 | 1,8 | 0,0 | 0% | HP 1354, Arm 107 (52 %), MR 166 (62 %), Phys 94, Mag 47, AS 1,26, regen 0,0, 14 worn |
| Ashfall | Physical | 6 | 5 Ashfall Sends | 100% | never | 18,9/6 | 1,8 | 0,0 | 0% | HP 1284, Arm 110 (52 %), MR 186 (65 %), Phys 84, Mag 54, AS 1,27, regen 0,0, 14 worn |
| Ashfall | Magical | 1 | none | 0% | 33 s | 0,0/6 | 4,7 | 9,8 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 16, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 1 | 1 Thornwood Send | 75% | 271 s | 6,8/6 | 2,2 | 1,0 | 0% | HP 1022, Arm 74 (43 %), MR 93 (48 %), Phys 31, Mag 53, AS 1,16, regen 0,0, 13 worn |
| Ashfall | Magical | 1 | 5 Thornwood Sends | 100% | 432 s | 11,4/6 | 2,1 | 0,0 | 0% | HP 1186, Arm 87 (47 %), MR 137 (58 %), Phys 31, Mag 53, AS 1,16, regen 0,0, 14 worn |
| Ashfall | Magical | 1 | 20 Thornwood Sends | 100% | 507 s | 13,6/6 | 2,1 | 0,0 | 0% | HP 1254, Arm 107 (52 %), MR 166 (62 %), Phys 41, Mag 34, AS 1,26, regen 0,0, 14 worn |
| Ashfall | Magical | 1 | 5 Ashfall Sends | 100% | 492 s | 13,4/6 | 2,1 | 0,0 | 0% | HP 1184, Arm 110 (52 %), MR 186 (65 %), Phys 31, Mag 53, AS 1,25, regen 0,0, 14 worn |
| Ashfall | Magical | 3 | none | 0% | 39 s | 0,0/6 | 3,9 | 8,8 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 19, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 3 | 1 Thornwood Send | 75% | 283 s | 7,0/6 | 2,2 | 0,9 | 0% | HP 1062, Arm 74 (43 %), MR 93 (48 %), Phys 33, Mag 58, AS 1,16, regen 0,0, 13 worn |
| Ashfall | Magical | 3 | 5 Thornwood Sends | 100% | 448 s | 12,0/6 | 2,1 | 0,0 | 0% | HP 1226, Arm 87 (47 %), MR 137 (58 %), Phys 33, Mag 58, AS 1,16, regen 0,0, 14 worn |
| Ashfall | Magical | 3 | 20 Thornwood Sends | 100% | 533 s | 14,3/6 | 2,1 | 0,0 | 0% | HP 1294, Arm 107 (52 %), MR 166 (62 %), Phys 43, Mag 39, AS 1,26, regen 0,0, 14 worn |
| Ashfall | Magical | 3 | 5 Ashfall Sends | 100% | 512 s | 14,1/6 | 2,1 | 0,0 | 0% | HP 1224, Arm 110 (52 %), MR 186 (65 %), Phys 33, Mag 58, AS 1,25, regen 0,0, 14 worn |
| Ashfall | Magical | 6 | none | 0% | 70 s | 0,8/6 | 3,6 | 6,8 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 22, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 6 | 1 Thornwood Send | 100% | 301 s | 7,9/6 | 2,2 | 0,8 | 0% | HP 1122, Arm 74 (43 %), MR 93 (48 %), Phys 37, Mag 66, AS 1,16, regen 0,0, 13 worn |
| Ashfall | Magical | 6 | 5 Thornwood Sends | 100% | 489 s | 13,3/6 | 2,1 | 0,0 | 0% | HP 1286, Arm 87 (47 %), MR 137 (58 %), Phys 37, Mag 66, AS 1,16, regen 0,0, 14 worn |
| Ashfall | Magical | 6 | 20 Thornwood Sends | 100% | 574 s | 15,9/6 | 2,1 | 0,0 | 0% | HP 1354, Arm 107 (52 %), MR 166 (62 %), Phys 47, Mag 47, AS 1,26, regen 0,0, 14 worn |
| Ashfall | Magical | 6 | 5 Ashfall Sends | 100% | 564 s | 15,6/6 | 2,0 | 0,0 | 0% | HP 1284, Arm 110 (52 %), MR 186 (65 %), Phys 37, Mag 66, AS 1,25, regen 0,0, 14 worn |

## Loot `proposed`

Fail Bucket ~76% of kills; the rarity roll comes first and only a Unique roll picks a Unique definition (~1 in 500 kills); flat affixes scale +25% per item level past 1.

| Location | build | level | gear | careless Completes | careless time to die | Encounters before death | s / kill | played trips / Send | played deaths | hero (seed 1) |
|---|---|---|---|---|---|---|---|---|---|---|
| Thornwood | Physical | 1 | none | 13% | 161 s | 4,1/5 | 2,5 | 1,0 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 32, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 1 | 1 Thornwood Send | 100% | 490 s | 15,1/5 | 2,0 | 0,0 | 0% | HP 565, Arm 46 (31 %), MR 12 (11 %), Phys 43, Mag 30, AS 0,99, regen 0,0, 7 worn |
| Thornwood | Physical | 1 | 5 Thornwood Sends | 100% | never | 23,5/5 | 1,8 | 0,0 | 0% | HP 1299, Arm 65 (39 %), MR 106 (51 %), Phys 76, Mag 33, AS 0,92, regen 0,0, 13 worn |
| Thornwood | Physical | 1 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1314, Arm 92 (48 %), MR 155 (61 %), Phys 76, Mag 41, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Physical | 1 | 5 Ashfall Sends | 100% | never | 24,5/5 | 1,7 | 0,0 | 0% | HP 1648, Arm 146 (59 %), MR 282 (74 %), Phys 146, Mag 44, AS 1,21, regen 0,0, 13 worn |
| Thornwood | Physical | 3 | none | 100% | 195 s | 5,3/5 | 2,4 | 1,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 37, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 3 | 1 Thornwood Send | 100% | 550 s | 18,0/5 | 1,9 | 0,0 | 0% | HP 605, Arm 46 (31 %), MR 12 (11 %), Phys 48, Mag 35, AS 0,99, regen 0,0, 7 worn |
| Thornwood | Physical | 3 | 5 Thornwood Sends | 100% | never | 23,6/5 | 1,8 | 0,0 | 0% | HP 1339, Arm 65 (39 %), MR 106 (51 %), Phys 81, Mag 38, AS 0,92, regen 0,0, 13 worn |
| Thornwood | Physical | 3 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1354, Arm 92 (48 %), MR 155 (61 %), Phys 81, Mag 46, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Physical | 3 | 5 Ashfall Sends | 100% | never | 24,5/5 | 1,7 | 0,0 | 0% | HP 1688, Arm 146 (59 %), MR 282 (74 %), Phys 151, Mag 49, AS 1,21, regen 0,0, 13 worn |
| Thornwood | Physical | 6 | none | 100% | 239 s | 7,3/5 | 2,3 | 0,4 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 45, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 6 | 1 Thornwood Send | 100% | never | 20,9/5 | 1,9 | 0,0 | 0% | HP 665, Arm 46 (31 %), MR 12 (11 %), Phys 56, Mag 43, AS 0,99, regen 0,0, 7 worn |
| Thornwood | Physical | 6 | 5 Thornwood Sends | 100% | never | 23,9/5 | 1,8 | 0,0 | 0% | HP 1399, Arm 65 (39 %), MR 106 (51 %), Phys 88, Mag 45, AS 0,92, regen 0,0, 13 worn |
| Thornwood | Physical | 6 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1414, Arm 92 (48 %), MR 155 (61 %), Phys 88, Mag 54, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Physical | 6 | 5 Ashfall Sends | 100% | never | 24,8/5 | 1,7 | 0,0 | 0% | HP 1748, Arm 146 (59 %), MR 282 (74 %), Phys 159, Mag 57, AS 1,21, regen 0,0, 13 worn |
| Thornwood | Magical | 1 | none | 0% | 131 s | 2,1/5 | 3,7 | 2,0 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 16, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 1 | 1 Thornwood Send | 88% | 302 s | 7,6/5 | 2,5 | 0,5 | 0% | HP 565, Arm 46 (31 %), MR 12 (11 %), Phys 22, Mag 30, AS 0,99, regen 0,0, 7 worn |
| Thornwood | Magical | 1 | 5 Thornwood Sends | 100% | never | 21,3/5 | 2,0 | 0,0 | 0% | HP 1299, Arm 65 (39 %), MR 82 (45 %), Phys 33, Mag 58, AS 0,84, regen 0,0, 13 worn |
| Thornwood | Magical | 1 | 20 Thornwood Sends | 100% | never | 22,0/5 | 1,9 | 0,0 | 0% | HP 1314, Arm 92 (48 %), MR 155 (61 %), Phys 32, Mag 63, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Magical | 1 | 5 Ashfall Sends | 100% | never | 23,3/5 | 1,8 | 0,0 | 0% | HP 1648, Arm 140 (58 %), MR 282 (74 %), Phys 73, Mag 48, AS 1,21, regen 0,0, 13 worn |
| Thornwood | Magical | 3 | none | 0% | 152 s | 2,9/5 | 3,3 | 2,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 19, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 3 | 1 Thornwood Send | 100% | 341 s | 9,4/5 | 2,4 | 0,3 | 0% | HP 605, Arm 46 (31 %), MR 12 (11 %), Phys 24, Mag 35, AS 0,99, regen 0,0, 7 worn |
| Thornwood | Magical | 3 | 5 Thornwood Sends | 100% | never | 21,3/5 | 2,0 | 0,0 | 0% | HP 1339, Arm 65 (39 %), MR 82 (45 %), Phys 35, Mag 63, AS 0,84, regen 0,0, 13 worn |
| Thornwood | Magical | 3 | 20 Thornwood Sends | 100% | never | 22,3/5 | 1,9 | 0,0 | 0% | HP 1354, Arm 92 (48 %), MR 155 (61 %), Phys 35, Mag 68, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Magical | 3 | 5 Ashfall Sends | 100% | never | 23,5/5 | 1,8 | 0,0 | 0% | HP 1688, Arm 140 (58 %), MR 282 (74 %), Phys 76, Mag 53, AS 1,21, regen 0,0, 13 worn |
| Thornwood | Magical | 6 | none | 13% | 186 s | 4,1/5 | 2,8 | 1,0 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 22, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 6 | 1 Thornwood Send | 100% | 419 s | 12,6/5 | 2,2 | 0,0 | 0% | HP 665, Arm 46 (31 %), MR 12 (11 %), Phys 28, Mag 43, AS 0,99, regen 0,0, 7 worn |
| Thornwood | Magical | 6 | 5 Thornwood Sends | 100% | never | 22,0/5 | 1,9 | 0,0 | 0% | HP 1399, Arm 65 (39 %), MR 82 (45 %), Phys 39, Mag 70, AS 0,84, regen 0,0, 13 worn |
| Thornwood | Magical | 6 | 20 Thornwood Sends | 100% | never | 22,8/5 | 1,8 | 0,0 | 0% | HP 1414, Arm 92 (48 %), MR 155 (61 %), Phys 39, Mag 75, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Magical | 6 | 5 Ashfall Sends | 100% | never | 23,8/5 | 1,8 | 0,0 | 0% | HP 1748, Arm 140 (58 %), MR 282 (74 %), Phys 79, Mag 61, AS 1,21, regen 0,0, 13 worn |
| Ashfall | Physical | 1 | none | 0% | 37 s | 0,0/6 | 3,4 | 6,8 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 32, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 1 | 1 Thornwood Send | 0% | 96 s | 2,1/6 | 2,4 | 2,8 | 0% | HP 565, Arm 46 (31 %), MR 12 (11 %), Phys 43, Mag 30, AS 0,99, regen 0,0, 7 worn |
| Ashfall | Physical | 1 | 5 Thornwood Sends | 100% | 469 s | 13,8/6 | 1,9 | 0,0 | 0% | HP 1299, Arm 65 (39 %), MR 106 (51 %), Phys 76, Mag 33, AS 0,92, regen 0,0, 13 worn |
| Ashfall | Physical | 1 | 20 Thornwood Sends | 100% | never | 18,3/6 | 1,8 | 0,0 | 0% | HP 1314, Arm 92 (48 %), MR 155 (61 %), Phys 76, Mag 41, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Physical | 1 | 5 Ashfall Sends | 100% | never | 19,6/6 | 1,8 | 0,0 | 0% | HP 1648, Arm 146 (59 %), MR 282 (74 %), Phys 146, Mag 44, AS 1,21, regen 0,0, 13 worn |
| Ashfall | Physical | 3 | none | 0% | 59 s | 0,6/6 | 3,4 | 6,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 37, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 3 | 1 Thornwood Send | 0% | 99 s | 2,4/6 | 2,4 | 2,6 | 0% | HP 605, Arm 46 (31 %), MR 12 (11 %), Phys 48, Mag 35, AS 0,99, regen 0,0, 7 worn |
| Ashfall | Physical | 3 | 5 Thornwood Sends | 100% | 499 s | 14,4/6 | 1,9 | 0,0 | 0% | HP 1339, Arm 65 (39 %), MR 106 (51 %), Phys 81, Mag 38, AS 0,92, regen 0,0, 13 worn |
| Ashfall | Physical | 3 | 20 Thornwood Sends | 100% | never | 18,4/6 | 1,8 | 0,0 | 0% | HP 1354, Arm 92 (48 %), MR 155 (61 %), Phys 81, Mag 46, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Physical | 3 | 5 Ashfall Sends | 100% | never | 19,6/6 | 1,8 | 0,0 | 0% | HP 1688, Arm 146 (59 %), MR 282 (74 %), Phys 151, Mag 49, AS 1,21, regen 0,0, 13 worn |
| Ashfall | Physical | 6 | none | 0% | 68 s | 1,0/6 | 2,6 | 4,3 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 45, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 6 | 1 Thornwood Send | 0% | 134 s | 3,4/6 | 2,2 | 1,9 | 0% | HP 665, Arm 46 (31 %), MR 12 (11 %), Phys 56, Mag 43, AS 0,99, regen 0,0, 7 worn |
| Ashfall | Physical | 6 | 5 Thornwood Sends | 100% | 535 s | 15,4/6 | 1,8 | 0,0 | 0% | HP 1399, Arm 65 (39 %), MR 106 (51 %), Phys 88, Mag 45, AS 0,92, regen 0,0, 13 worn |
| Ashfall | Physical | 6 | 20 Thornwood Sends | 100% | never | 18,6/6 | 1,8 | 0,0 | 0% | HP 1414, Arm 92 (48 %), MR 155 (61 %), Phys 88, Mag 54, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Physical | 6 | 5 Ashfall Sends | 100% | never | 19,6/6 | 1,8 | 0,0 | 0% | HP 1748, Arm 146 (59 %), MR 282 (74 %), Phys 159, Mag 57, AS 1,21, regen 0,0, 13 worn |
| Ashfall | Magical | 1 | none | 0% | 33 s | 0,0/6 | 4,7 | 9,8 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 16, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 1 | 1 Thornwood Send | 0% | 73 s | 1,1/6 | 3,2 | 4,1 | 0% | HP 565, Arm 46 (31 %), MR 12 (11 %), Phys 22, Mag 30, AS 0,99, regen 0,0, 7 worn |
| Ashfall | Magical | 1 | 5 Thornwood Sends | 100% | 337 s | 7,9/6 | 2,3 | 0,6 | 0% | HP 1299, Arm 65 (39 %), MR 82 (45 %), Phys 33, Mag 58, AS 0,84, regen 0,0, 13 worn |
| Ashfall | Magical | 1 | 20 Thornwood Sends | 100% | never | 16,0/6 | 2,0 | 0,0 | 0% | HP 1314, Arm 92 (48 %), MR 155 (61 %), Phys 32, Mag 63, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Magical | 1 | 5 Ashfall Sends | 100% | never | 18,0/6 | 1,9 | 0,0 | 0% | HP 1648, Arm 140 (58 %), MR 282 (74 %), Phys 73, Mag 48, AS 1,21, regen 0,0, 13 worn |
| Ashfall | Magical | 3 | none | 0% | 39 s | 0,0/6 | 3,9 | 8,8 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 19, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 3 | 1 Thornwood Send | 0% | 78 s | 1,5/6 | 3,0 | 3,9 | 0% | HP 605, Arm 46 (31 %), MR 12 (11 %), Phys 24, Mag 35, AS 0,99, regen 0,0, 7 worn |
| Ashfall | Magical | 3 | 5 Thornwood Sends | 100% | 352 s | 8,6/6 | 2,2 | 0,6 | 0% | HP 1339, Arm 65 (39 %), MR 82 (45 %), Phys 35, Mag 63, AS 0,84, regen 0,0, 13 worn |
| Ashfall | Magical | 3 | 20 Thornwood Sends | 100% | never | 16,4/6 | 2,0 | 0,0 | 0% | HP 1354, Arm 92 (48 %), MR 155 (61 %), Phys 35, Mag 68, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Magical | 3 | 5 Ashfall Sends | 100% | never | 18,1/6 | 1,9 | 0,0 | 0% | HP 1688, Arm 140 (58 %), MR 282 (74 %), Phys 76, Mag 53, AS 1,21, regen 0,0, 13 worn |
| Ashfall | Magical | 6 | none | 0% | 70 s | 0,8/6 | 3,6 | 6,8 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 22, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 6 | 1 Thornwood Send | 0% | 104 s | 1,9/6 | 2,8 | 3,1 | 0% | HP 665, Arm 46 (31 %), MR 12 (11 %), Phys 28, Mag 43, AS 0,99, regen 0,0, 7 worn |
| Ashfall | Magical | 6 | 5 Thornwood Sends | 100% | 398 s | 9,9/6 | 2,2 | 0,4 | 0% | HP 1399, Arm 65 (39 %), MR 82 (45 %), Phys 39, Mag 70, AS 0,84, regen 0,0, 13 worn |
| Ashfall | Magical | 6 | 20 Thornwood Sends | 100% | never | 16,9/6 | 2,0 | 0,0 | 0% | HP 1414, Arm 92 (48 %), MR 155 (61 %), Phys 39, Mag 75, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Magical | 6 | 5 Ashfall Sends | 100% | never | 18,1/6 | 1,9 | 0,0 | 0% | HP 1748, Arm 140 (58 %), MR 282 (74 %), Phys 79, Mag 61, AS 1,21, regen 0,0, 13 worn |

## Loot `deck`

Proposed, with the rarity dealt from a card deck of 1000 per Location per hero (760 NoDrop, 2 Unique): exactly 1 Unique in every 500 kills, and no long dry streak.

| Location | build | level | gear | careless Completes | careless time to die | Encounters before death | s / kill | played trips / Send | played deaths | hero (seed 1) |
|---|---|---|---|---|---|---|---|---|---|---|
| Thornwood | Physical | 1 | none | 13% | 161 s | 4,1/5 | 2,5 | 1,0 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 32, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 1 | 1 Thornwood Send | 100% | 480 s | 15,9/5 | 2,0 | 0,1 | 0% | HP 549, Arm 25 (20 %), MR 61 (38 %), Phys 45, Mag 14, AS 0,92, regen 0,0, 5 worn |
| Thornwood | Physical | 1 | 5 Thornwood Sends | 100% | never | 23,4/5 | 1,8 | 0,0 | 0% | HP 1066, Arm 62 (38 %), MR 156 (61 %), Phys 62, Mag 39, AS 1,10, regen 0,0, 10 worn |
| Thornwood | Physical | 1 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1471, Arm 101 (50 %), MR 147 (60 %), Phys 76, Mag 39, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Physical | 1 | 5 Ashfall Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 2016, Arm 168 (63 %), MR 254 (72 %), Phys 82, Mag 50, AS 1,12, regen 0,0, 13 worn |
| Thornwood | Physical | 3 | none | 100% | 195 s | 5,3/5 | 2,4 | 1,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 37, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 3 | 1 Thornwood Send | 100% | 520 s | 17,8/5 | 2,0 | 0,0 | 0% | HP 589, Arm 25 (20 %), MR 61 (38 %), Phys 50, Mag 19, AS 0,92, regen 0,0, 5 worn |
| Thornwood | Physical | 3 | 5 Thornwood Sends | 100% | never | 23,6/5 | 1,8 | 0,0 | 0% | HP 1106, Arm 62 (38 %), MR 156 (61 %), Phys 67, Mag 44, AS 1,10, regen 0,0, 10 worn |
| Thornwood | Physical | 3 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1511, Arm 101 (50 %), MR 147 (60 %), Phys 81, Mag 44, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Physical | 3 | 5 Ashfall Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 2056, Arm 168 (63 %), MR 254 (72 %), Phys 87, Mag 55, AS 1,12, regen 0,0, 13 worn |
| Thornwood | Physical | 6 | none | 100% | 239 s | 7,3/5 | 2,3 | 0,4 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 45, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Physical | 6 | 1 Thornwood Send | 100% | never | 20,1/5 | 1,9 | 0,0 | 0% | HP 649, Arm 25 (20 %), MR 61 (38 %), Phys 57, Mag 27, AS 0,92, regen 0,0, 5 worn |
| Thornwood | Physical | 6 | 5 Thornwood Sends | 100% | never | 23,6/5 | 1,8 | 0,0 | 0% | HP 1166, Arm 62 (38 %), MR 156 (61 %), Phys 75, Mag 52, AS 1,10, regen 0,0, 10 worn |
| Thornwood | Physical | 6 | 20 Thornwood Sends | 100% | never | 24,0/5 | 1,7 | 0,0 | 0% | HP 1571, Arm 101 (50 %), MR 147 (60 %), Phys 88, Mag 52, AS 1,14, regen 0,0, 13 worn |
| Thornwood | Physical | 6 | 5 Ashfall Sends | 100% | never | 24,1/5 | 1,7 | 0,0 | 0% | HP 2116, Arm 168 (63 %), MR 254 (72 %), Phys 95, Mag 63, AS 1,12, regen 0,0, 13 worn |
| Thornwood | Magical | 1 | none | 0% | 131 s | 2,1/5 | 3,7 | 2,0 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 16, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 1 | 1 Thornwood Send | 88% | 318 s | 8,1/5 | 2,6 | 0,5 | 0% | HP 549, Arm 25 (20 %), MR 61 (38 %), Phys 22, Mag 14, AS 0,92, regen 0,0, 5 worn |
| Thornwood | Magical | 1 | 5 Thornwood Sends | 100% | never | 21,1/5 | 2,0 | 0,0 | 0% | HP 1220, Arm 62 (38 %), MR 156 (61 %), Phys 20, Mag 63, AS 0,93, regen 0,0, 10 worn |
| Thornwood | Magical | 1 | 20 Thornwood Sends | 100% | never | 22,1/5 | 1,9 | 0,0 | 0% | HP 1471, Arm 101 (50 %), MR 147 (60 %), Phys 32, Mag 62, AS 1,15, regen 0,0, 13 worn |
| Thornwood | Magical | 1 | 5 Ashfall Sends | 100% | never | 23,0/5 | 1,8 | 0,0 | 0% | HP 2016, Arm 168 (63 %), MR 254 (72 %), Phys 31, Mag 90, AS 1,03, regen 0,0, 13 worn |
| Thornwood | Magical | 3 | none | 0% | 152 s | 2,9/5 | 3,3 | 2,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 19, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 3 | 1 Thornwood Send | 100% | 360 s | 9,9/5 | 2,5 | 0,3 | 0% | HP 589, Arm 25 (20 %), MR 61 (38 %), Phys 25, Mag 19, AS 0,92, regen 0,0, 5 worn |
| Thornwood | Magical | 3 | 5 Thornwood Sends | 100% | never | 21,4/5 | 2,0 | 0,0 | 0% | HP 1260, Arm 62 (38 %), MR 156 (61 %), Phys 22, Mag 68, AS 0,93, regen 0,0, 10 worn |
| Thornwood | Magical | 3 | 20 Thornwood Sends | 100% | never | 22,3/5 | 1,9 | 0,0 | 0% | HP 1511, Arm 101 (50 %), MR 147 (60 %), Phys 34, Mag 67, AS 1,15, regen 0,0, 13 worn |
| Thornwood | Magical | 3 | 5 Ashfall Sends | 100% | never | 23,3/5 | 1,8 | 0,0 | 0% | HP 2056, Arm 168 (63 %), MR 254 (72 %), Phys 34, Mag 95, AS 1,03, regen 0,0, 13 worn |
| Thornwood | Magical | 6 | none | 13% | 186 s | 4,1/5 | 2,8 | 1,0 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 22, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Thornwood | Magical | 6 | 1 Thornwood Send | 100% | 450 s | 13,3/5 | 2,3 | 0,1 | 0% | HP 649, Arm 25 (20 %), MR 61 (38 %), Phys 29, Mag 27, AS 0,92, regen 0,0, 5 worn |
| Thornwood | Magical | 6 | 5 Thornwood Sends | 100% | never | 21,8/5 | 1,9 | 0,0 | 0% | HP 1320, Arm 62 (38 %), MR 156 (61 %), Phys 26, Mag 75, AS 0,93, regen 0,0, 10 worn |
| Thornwood | Magical | 6 | 20 Thornwood Sends | 100% | never | 22,9/5 | 1,9 | 0,0 | 0% | HP 1571, Arm 101 (50 %), MR 147 (60 %), Phys 38, Mag 74, AS 1,15, regen 0,0, 13 worn |
| Thornwood | Magical | 6 | 5 Ashfall Sends | 100% | never | 23,3/5 | 1,8 | 0,0 | 0% | HP 2116, Arm 168 (63 %), MR 254 (72 %), Phys 37, Mag 103, AS 1,03, regen 0,0, 13 worn |
| Ashfall | Physical | 1 | none | 0% | 37 s | 0,0/6 | 3,4 | 6,8 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 32, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 1 | 1 Thornwood Send | 0% | 112 s | 2,4/6 | 2,4 | 2,4 | 0% | HP 549, Arm 25 (20 %), MR 61 (38 %), Phys 45, Mag 14, AS 0,92, regen 0,0, 5 worn |
| Ashfall | Physical | 1 | 5 Thornwood Sends | 100% | 442 s | 12,5/6 | 1,9 | 0,1 | 0% | HP 1066, Arm 62 (38 %), MR 156 (61 %), Phys 62, Mag 39, AS 1,10, regen 0,0, 10 worn |
| Ashfall | Physical | 1 | 20 Thornwood Sends | 100% | never | 18,4/6 | 1,8 | 0,0 | 0% | HP 1471, Arm 101 (50 %), MR 147 (60 %), Phys 76, Mag 39, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Physical | 1 | 5 Ashfall Sends | 100% | never | 19,1/6 | 1,8 | 0,0 | 0% | HP 2016, Arm 168 (63 %), MR 254 (72 %), Phys 82, Mag 50, AS 1,12, regen 0,0, 13 worn |
| Ashfall | Physical | 3 | none | 0% | 59 s | 0,6/6 | 3,4 | 6,0 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 37, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 3 | 1 Thornwood Send | 13% | 134 s | 3,1/6 | 2,3 | 2,1 | 0% | HP 589, Arm 25 (20 %), MR 61 (38 %), Phys 50, Mag 19, AS 0,92, regen 0,0, 5 worn |
| Ashfall | Physical | 3 | 5 Thornwood Sends | 100% | 478 s | 13,4/6 | 1,9 | 0,1 | 0% | HP 1106, Arm 62 (38 %), MR 156 (61 %), Phys 67, Mag 44, AS 1,10, regen 0,0, 10 worn |
| Ashfall | Physical | 3 | 20 Thornwood Sends | 100% | never | 18,8/6 | 1,8 | 0,0 | 0% | HP 1511, Arm 101 (50 %), MR 147 (60 %), Phys 81, Mag 44, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Physical | 3 | 5 Ashfall Sends | 100% | never | 19,5/6 | 1,8 | 0,0 | 0% | HP 2056, Arm 168 (63 %), MR 254 (72 %), Phys 87, Mag 55, AS 1,12, regen 0,0, 13 worn |
| Ashfall | Physical | 6 | none | 0% | 68 s | 1,0/6 | 2,6 | 4,3 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 45, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Physical | 6 | 1 Thornwood Send | 13% | 160 s | 4,1/6 | 2,2 | 1,6 | 0% | HP 649, Arm 25 (20 %), MR 61 (38 %), Phys 57, Mag 27, AS 0,92, regen 0,0, 5 worn |
| Ashfall | Physical | 6 | 5 Thornwood Sends | 100% | 500 s | 14,8/6 | 1,9 | 0,0 | 0% | HP 1166, Arm 62 (38 %), MR 156 (61 %), Phys 75, Mag 52, AS 1,10, regen 0,0, 10 worn |
| Ashfall | Physical | 6 | 20 Thornwood Sends | 100% | never | 19,0/6 | 1,8 | 0,0 | 0% | HP 1571, Arm 101 (50 %), MR 147 (60 %), Phys 88, Mag 52, AS 1,14, regen 0,0, 13 worn |
| Ashfall | Physical | 6 | 5 Ashfall Sends | 100% | never | 19,8/6 | 1,8 | 0,0 | 0% | HP 2116, Arm 168 (63 %), MR 254 (72 %), Phys 95, Mag 63, AS 1,12, regen 0,0, 13 worn |
| Ashfall | Magical | 1 | none | 0% | 33 s | 0,0/6 | 4,7 | 9,8 | 0% | HP 400, Arm 12 (11 %), MR 12 (11 %), Phys 16, Mag 14, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 1 | 1 Thornwood Send | 0% | 99 s | 1,3/6 | 3,3 | 4,0 | 0% | HP 549, Arm 25 (20 %), MR 61 (38 %), Phys 22, Mag 14, AS 0,92, regen 0,0, 5 worn |
| Ashfall | Magical | 1 | 5 Thornwood Sends | 88% | 347 s | 7,6/6 | 2,3 | 0,8 | 0% | HP 1220, Arm 62 (38 %), MR 156 (61 %), Phys 20, Mag 63, AS 0,93, regen 0,0, 10 worn |
| Ashfall | Magical | 1 | 20 Thornwood Sends | 100% | never | 16,1/6 | 2,1 | 0,0 | 0% | HP 1471, Arm 101 (50 %), MR 147 (60 %), Phys 32, Mag 62, AS 1,15, regen 0,0, 13 worn |
| Ashfall | Magical | 1 | 5 Ashfall Sends | 100% | never | 17,6/6 | 1,9 | 0,0 | 0% | HP 2016, Arm 168 (63 %), MR 254 (72 %), Phys 31, Mag 90, AS 1,03, regen 0,0, 13 worn |
| Ashfall | Magical | 3 | none | 0% | 39 s | 0,0/6 | 3,9 | 8,8 | 0% | HP 440, Arm 12 (11 %), MR 12 (11 %), Phys 19, Mag 19, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 3 | 1 Thornwood Send | 0% | 107 s | 1,5/6 | 3,2 | 3,6 | 0% | HP 589, Arm 25 (20 %), MR 61 (38 %), Phys 25, Mag 19, AS 0,92, regen 0,0, 5 worn |
| Ashfall | Magical | 3 | 5 Thornwood Sends | 88% | 367 s | 8,3/6 | 2,3 | 0,5 | 0% | HP 1260, Arm 62 (38 %), MR 156 (61 %), Phys 22, Mag 68, AS 0,93, regen 0,0, 10 worn |
| Ashfall | Magical | 3 | 20 Thornwood Sends | 100% | never | 16,6/6 | 2,1 | 0,0 | 0% | HP 1511, Arm 101 (50 %), MR 147 (60 %), Phys 34, Mag 67, AS 1,15, regen 0,0, 13 worn |
| Ashfall | Magical | 3 | 5 Ashfall Sends | 100% | never | 17,8/6 | 1,9 | 0,0 | 0% | HP 2056, Arm 168 (63 %), MR 254 (72 %), Phys 34, Mag 95, AS 1,03, regen 0,0, 13 worn |
| Ashfall | Magical | 6 | none | 0% | 70 s | 0,8/6 | 3,6 | 6,8 | 0% | HP 500, Arm 12 (11 %), MR 12 (11 %), Phys 22, Mag 27, AS 0,75, regen 0,0, 0 worn |
| Ashfall | Magical | 6 | 1 Thornwood Send | 0% | 128 s | 2,1/6 | 2,9 | 2,8 | 0% | HP 649, Arm 25 (20 %), MR 61 (38 %), Phys 29, Mag 27, AS 0,92, regen 0,0, 5 worn |
| Ashfall | Magical | 6 | 5 Thornwood Sends | 100% | 392 s | 9,4/6 | 2,3 | 0,3 | 0% | HP 1320, Arm 62 (38 %), MR 156 (61 %), Phys 26, Mag 75, AS 0,93, regen 0,0, 10 worn |
| Ashfall | Magical | 6 | 20 Thornwood Sends | 100% | never | 17,0/6 | 2,0 | 0,0 | 0% | HP 1571, Arm 101 (50 %), MR 147 (60 %), Phys 38, Mag 74, AS 1,15, regen 0,0, 13 worn |
| Ashfall | Magical | 6 | 5 Ashfall Sends | 100% | never | 18,0/6 | 1,9 | 0,0 | 0% | HP 2116, Arm 168 (63 %), MR 254 (72 %), Phys 37, Mag 103, AS 1,03, regen 0,0, 13 worn |

