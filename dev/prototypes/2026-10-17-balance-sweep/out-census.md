# Balance sweep (#296) - census

Generated 2026-10-17 22:10. Real `EncounterSimulation`, `EncounterTuning.Standard()`, Engagement 3. Careless = no Recall, no potion. Played = at 35 % Health drink a potion if carried (+35 %), else Recall: the portal opens over 0,5 s, a trip costs 15 s.

Hero base: Health 400, Armor 12, MagicResist 12, PhysicalDamage 32, MagicalDamage 14, AttackSpeed 0,75, HealthRegeneration 0, Resource 200, ResourceRegeneration 6, MovementSpeed 325.
Per level: Health +20, PhysicalDamage +2,5, MagicalDamage +2,5, Resource +8. XP bar: L1 280, L2 560, L3 940, L4 1420, L5 2000, L6 2680, L7 3460, L8 4340.
Populations: Thornwood 5, Ashfall 6, synthetic 5.

## Loot `live`

The code and data as they are: authored tables (Fail Bucket 0), the generator's uniform pick over all 180 definitions (156 are Uniques), no item-level scaling.

### Thornwood (item level 2), 20000 kills, 70 kills a Send

- Drop per kill: 100,0 % (Fail Bucket 0,0 %); a Send drops ~70,0 items: 46,8 equipment, 1,1 potions.
- Category: Consumable 16 %, Equipment 67 %, Currency 17 %
- Consumables a Send: Book 4,9, Arrow 5,5, Potion 1,1
- Equipment rarity: Common 54,3 %, Magic 25,6 %, Rare 13,2 %, Unique 6,9 %; on a Unique **definition**: 89 % (1 in 2 kills).

| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |
|---|---|---|---|
| Belt | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 47,9, Armor 5,4, HealthRegeneration 37%, MagicResist 8,7 |
| Boots | 6 % | MovementSpeed: 16-25 flat | MovementSpeed 31,3, MagicResist 1, MagicPenetration 0,8, Armor 0,9, AttackSpeed 0,9% |
| Bracers | 7 % | Armor: 10-15 flat; MagicResist: 16-25 flat; Resource: 166-250 flat | Armor 7,5, Resource 108,9, MagicResist 11,1 |
| Chest | 7 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 5,2, MagicResist 7,9, HealthRegeneration 33,9%, Health 53,8 |
| Cloak | 7 % | MagicResist: 16-25 flat; MagicalDamage: 13-20 flat | MagicalDamage 11,9, MagicResist 15,4 |
| Gloves | 6 % | AttackSpeed: 8-12 %; ArmorPenetration: 12-18 % | AttackSpeed 7,1%, ArmorPenetration 11,4% |
| Helm | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | MagicResist 8, Armor 5, Health 50, HealthRegeneration 35,3% |
| Pants | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 5,6, MagicResist 8,4, HealthRegeneration 32,2%, Health 52,3 |
| Shoulders | 7 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | MagicResist 8,4, HealthRegeneration 34,2%, Armor 5,2, Health 50,3 |
| Sword | 2 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 6,1, AttackSpeed 5,5%, ArmorPenetration 5,6%, Health 17, MagicalDamage 1,2% |
| Bow | 1 % | MagicalDamage: 13-20 flat; AttackSpeed: 8-12 %; MagicPenetration: 8-13 % | AttackSpeed 5,5%, MagicalDamage 9,6, MagicPenetration 10,1, MagicPenetration 6,4% |
| Crossbow | 1 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 4,7, ArmorPenetration 5,7%, AttackSpeed 4,8% |
| GreatSword | 2 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 5,7%, PhysicalDamage 8,2, ArmorPenetration 5,9%, MagicalDamage 1,8 |
| Shield | 7 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat; Shield: 100-200 flat | Armor 4, MagicResist 7, Health 41,7, Shield 50,9, HealthRegeneration 29,7% |
| Quiver | 7 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 6%, AttackSpeed 5,3%, PhysicalDamage 4,4 |
| Amulet | 10 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 5,2%, ArmorPenetration 6%, PhysicalDamage 4,4, IncreasedItemRarity 38,6% |
| Ring | 11 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 4,4, AttackSpeed 5,6%, ArmorPenetration 5,9% |

### Ashfall (item level 5), 20000 kills, 102 kills a Send

- Drop per kill: 100,0 % (Fail Bucket 0,0 %); a Send drops ~102,0 items: 76,4 equipment, 1,2 potions.
- Category: Consumable 13 %, Equipment 75 %, Currency 12 %
- Consumables a Send: Arrow 6,3, Potion 1,2, Book 5,6
- Equipment rarity: Common 32,3 %, Magic 32,4 %, Rare 23,8 %, Unique 11,5 %; on a Unique **definition**: 89 % (1 in 2 kills).

| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |
|---|---|---|---|
| Belt | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | HealthRegeneration 47,5%, Armor 6,4, Health 59,6, MagicResist 10,9 |
| Boots | 7 % | MovementSpeed: 16-25 flat | MovementSpeed 30,9, MagicResist 1,1, Armor 0,9, MagicPenetration 1, AttackSpeed 1% |
| Bracers | 6 % | Armor: 10-15 flat; MagicResist: 16-25 flat; Resource: 166-250 flat | Resource 144,7, Armor 8,5, MagicResist 13,5 |
| Chest | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | MagicResist 10,1, Armor 6,6, HealthRegeneration 40,8%, Health 62,2 |
| Cloak | 7 % | MagicResist: 16-25 flat; MagicalDamage: 13-20 flat | MagicalDamage 13,9, MagicResist 17,2 |
| Gloves | 7 % | AttackSpeed: 8-12 %; ArmorPenetration: 12-18 % | ArmorPenetration 12,3%, AttackSpeed 8,7% |
| Helm | 7 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | HealthRegeneration 40,7%, Health 64, Armor 6,7, MagicResist 10,9 |
| Pants | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 64,5, MagicResist 10,7, HealthRegeneration 42,1%, Armor 6,2 |
| Shoulders | 7 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 62,5, MagicResist 10,3, HealthRegeneration 41,2%, Armor 6,4 |
| Sword | 2 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 7,4%, AttackSpeed 7,1%, PhysicalDamage 7,6, MagicalDamage 1,1%, Health 19,4 |
| Bow | 1 % | MagicalDamage: 13-20 flat; AttackSpeed: 8-12 %; MagicPenetration: 8-13 % | AttackSpeed 6,5%, MagicPenetration 6,9%, MagicalDamage 10,8, MagicPenetration 9,5 |
| Crossbow | 1 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 6, ArmorPenetration 7,5%, AttackSpeed 6,5% |
| GreatSword | 2 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 7,5%, AttackSpeed 6,9%, PhysicalDamage 8,5, MagicalDamage 2,4 |
| Shield | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat; Shield: 100-200 flat | Armor 4,9, MagicResist 8,9, HealthRegeneration 34,1%, Health 51,5, Shield 62,2 |
| Quiver | 7 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 6,8%, ArmorPenetration 7,3%, PhysicalDamage 5,1 |
| Amulet | 10 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 6,9%, IncreasedItemRarity 37,8%, PhysicalDamage 5,4, ArmorPenetration 6,9% |
| Ring | 11 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 6,5%, PhysicalDamage 5,4, ArmorPenetration 7% |

## Loot `proposed`

Fail Bucket ~76% of kills; the rarity roll comes first and only a Unique roll picks a Unique definition (~1 in 500 kills); flat affixes scale +25% per item level past 1.

### Thornwood (item level 2), 20000 kills, 70 kills a Send

- Drop per kill: 23,1 % (Fail Bucket 76,9 %); a Send drops ~16,2 items: 10,8 equipment, 0,8 potions.
- Category: Consumable 17 %, Equipment 67 %, Currency 16 %
- Consumables a Send: Potion 0,8, Arrow 0,9, Book 1,0
- Equipment rarity: Common 56,8 %, Magic 28,0 %, Rare 14,4 %, Unique 0,8 %; on a Unique **definition**: 1 % (1 in 769 kills).

| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |
|---|---|---|---|
| Belt | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 55,2, HealthRegeneration 33,2%, MagicResist 8,9, Armor 5,9 |
| Boots | 7 % | MovementSpeed: 16-25 flat | MovementSpeed 25,7 |
| Bracers | 6 % | Armor: 10-15 flat; MagicResist: 16-25 flat; Resource: 166-250 flat | MagicResist 11,4, Armor 8,5, Resource 150,1 |
| Chest | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 5,1, Health 69,5, MagicResist 10,9, HealthRegeneration 29,6% |
| Cloak | 6 % | MagicResist: 16-25 flat; MagicalDamage: 13-20 flat | MagicResist 18,3, MagicalDamage 15,8 |
| Gloves | 6 % | AttackSpeed: 8-12 %; ArmorPenetration: 12-18 % | AttackSpeed 7,2%, ArmorPenetration 10,6% |
| Helm | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | MagicResist 10,2, HealthRegeneration 31,9%, Health 56,7, Armor 6,9 |
| Pants | 5 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 65,9, Armor 5,6, MagicResist 10,7, HealthRegeneration 28,7% |
| Shoulders | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | HealthRegeneration 31,9%, Armor 6,2, MagicResist 10,9, Health 58,5 |
| Sword | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 5,3%, PhysicalDamage 4,8, AttackSpeed 5,6% |
| Bow | 6 % | MagicalDamage: 13-20 flat; AttackSpeed: 8-12 %; MagicPenetration: 8-13 % | AttackSpeed 5,8%, MagicalDamage 10,4, MagicPenetration 5,4% |
| Crossbow | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 6,4%, PhysicalDamage 4,9, AttackSpeed 5,2% |
| GreatSword | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 5,5%, ArmorPenetration 5,9%, PhysicalDamage 5,1 |
| Shield | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat; Shield: 100-200 flat | Shield 60,9, MagicResist 8,2, Health 54,3, HealthRegeneration 24,3%, Armor 4,7 |
| Quiver | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 5,8%, ArmorPenetration 6%, PhysicalDamage 4,9 |
| Amulet | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 4,9%, PhysicalDamage 5,1, ArmorPenetration 5,9%, IncreasedItemRarity 0,3% |
| Ring | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 4,6, AttackSpeed 6,1%, ArmorPenetration 5,2% |

### Ashfall (item level 5), 20000 kills, 102 kills a Send

- Drop per kill: 22,0 % (Fail Bucket 78,0 %); a Send drops ~22,4 items: 16,8 equipment, 0,9 potions.
- Category: Consumable 13 %, Equipment 75 %, Currency 12 %
- Consumables a Send: Potion 0,9, Arrow 1,0, Book 1,0
- Equipment rarity: Common 35,9 %, Magic 36,3 %, Rare 26,7 %, Unique 1,1 %; on a Unique **definition**: 1 % (1 in 541 kills).

| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |
|---|---|---|---|
| Belt | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | MagicResist 18,7, HealthRegeneration 40,4%, Armor 12,1, Health 107,6 |
| Boots | 7 % | MovementSpeed: 16-25 flat | MovementSpeed 40,7 |
| Bracers | 6 % | Armor: 10-15 flat; MagicResist: 16-25 flat; Resource: 166-250 flat | Armor 16,1, Resource 289,7, MagicResist 25,2 |
| Chest | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 11,6, HealthRegeneration 33,8%, Health 124,9, MagicResist 20 |
| Cloak | 6 % | MagicResist: 16-25 flat; MagicalDamage: 13-20 flat | MagicResist 36, MagicalDamage 27,7 |
| Gloves | 6 % | AttackSpeed: 8-12 %; ArmorPenetration: 12-18 % | AttackSpeed 7,6%, ArmorPenetration 12,6% |
| Helm | 5 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | MagicResist 20,7, Armor 12,3, HealthRegeneration 35,5%, Health 121,4 |
| Pants | 5 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 10,8, MagicResist 18,9, Health 124,8, HealthRegeneration 38,2% |
| Shoulders | 7 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 11,9, HealthRegeneration 39,7%, MagicResist 19, Health 123,5 |
| Sword | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 6,6%, PhysicalDamage 11,1, AttackSpeed 6,8% |
| Bow | 6 % | MagicalDamage: 13-20 flat; AttackSpeed: 8-12 %; MagicPenetration: 8-13 % | MagicalDamage 19,1, AttackSpeed 7%, MagicPenetration 6,9% |
| Crossbow | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 6,1%, PhysicalDamage 10,3, ArmorPenetration 6,6% |
| GreatSword | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 11,2, ArmorPenetration 6,9%, AttackSpeed 5,9% |
| Shield | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat; Shield: 100-200 flat | HealthRegeneration 31,9%, Armor 8,1, Health 96,1, Shield 138,9, MagicResist 15,2 |
| Quiver | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 10,5, AttackSpeed 6,5%, ArmorPenetration 6,8% |
| Amulet | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 10,2, ArmorPenetration 6,9%, AttackSpeed 6,2%, IncreasedItemRarity 0,3% |
| Ring | 7 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 9,6, ArmorPenetration 6,6%, AttackSpeed 6,5% |

## Loot `deck`

Proposed, with the rarity dealt from a card deck of 1000 per Location per hero (760 NoDrop, 2 Unique): exactly 1 Unique in every 500 kills, and no long dry streak.

### Thornwood (item level 2), 20000 kills, 70 kills a Send

- Drop per kill: 24,0 % (Fail Bucket 76,0 %); a Send drops ~16,8 items: 11,2 equipment, 0,9 potions.
- Category: Consumable 17 %, Equipment 66 %, Currency 17 %
- Consumables a Send: Potion 0,9, Arrow 1,0, Book 1,0
- Equipment rarity: Common 53,9 %, Magic 26,3 %, Rare 19,1 %, Unique 0,7 %; on a Unique **definition**: 1 % (1 in 909 kills).

| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |
|---|---|---|---|
| Belt | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 62,9, HealthRegeneration 39,7%, Armor 6,2, MagicResist 11,7 |
| Boots | 5 % | MovementSpeed: 16-25 flat | MovementSpeed 25,6, AttackSpeed 0,1% |
| Bracers | 7 % | Armor: 10-15 flat; MagicResist: 16-25 flat; Resource: 166-250 flat | MagicResist 13,9, Armor 8,2, Resource 149,1 |
| Chest | 5 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 60,6, HealthRegeneration 32,9%, Armor 6,2, MagicResist 11,4 |
| Cloak | 6 % | MagicResist: 16-25 flat; MagicalDamage: 13-20 flat | MagicResist 18,9, MagicalDamage 15 |
| Gloves | 6 % | AttackSpeed: 8-12 %; ArmorPenetration: 12-18 % | AttackSpeed 7,3%, ArmorPenetration 10,3% |
| Helm | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 74, Armor 5,2, MagicResist 11,5, HealthRegeneration 37,3% |
| Pants | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 6,1, HealthRegeneration 39,2%, Health 61,2, MagicResist 9,7 |
| Shoulders | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 6,6, Health 64,1, MagicResist 8,4, HealthRegeneration 36,4% |
| Sword | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 5,5%, AttackSpeed 5,9%, PhysicalDamage 5,4 |
| Bow | 6 % | MagicalDamage: 13-20 flat; AttackSpeed: 8-12 %; MagicPenetration: 8-13 % | MagicPenetration 5,8%, MagicalDamage 11,6, AttackSpeed 5,8% |
| Crossbow | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | AttackSpeed 5,4%, PhysicalDamage 6,1, ArmorPenetration 5,7% |
| GreatSword | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 6,1%, PhysicalDamage 5,6, AttackSpeed 5,4% |
| Shield | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat; Shield: 100-200 flat | HealthRegeneration 24,6%, MagicResist 9,2, Health 49, Armor 5,3, Shield 68,3 |
| Quiver | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 5,7%, PhysicalDamage 5, AttackSpeed 5,2% |
| Amulet | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 6,6%, PhysicalDamage 5,3, AttackSpeed 5,5%, IncreasedItemRarity 0,3% |
| Ring | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 5,3, AttackSpeed 5,7%, ArmorPenetration 5,5% |

### Ashfall (item level 5), 20000 kills, 102 kills a Send

- Drop per kill: 24,0 % (Fail Bucket 76,0 %); a Send drops ~24,5 items: 18,3 equipment, 1,1 potions.
- Category: Consumable 13 %, Equipment 75 %, Currency 12 %
- Consumables a Send: Potion 1,1, Arrow 1,1, Book 1,0
- Equipment rarity: Common 33,4 %, Magic 33,6 %, Rare 32,3 %, Unique 0,7 %; on a Unique **definition**: 1 % (1 in 800 kills).

| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |
|---|---|---|---|
| Belt | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | HealthRegeneration 41,9%, Armor 12,9, MagicResist 19,2, Health 144,1 |
| Boots | 5 % | MovementSpeed: 16-25 flat | MovementSpeed 41,2, AttackSpeed 0,1% |
| Bracers | 6 % | Armor: 10-15 flat; MagicResist: 16-25 flat; Resource: 166-250 flat | MagicResist 25,3, Resource 294,9, Armor 15,7 |
| Chest | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | HealthRegeneration 41,7%, Armor 12,9, Health 128,5, MagicResist 15,8 |
| Cloak | 6 % | MagicResist: 16-25 flat; MagicalDamage: 13-20 flat | MagicResist 34,3, MagicalDamage 26,7 |
| Gloves | 7 % | AttackSpeed: 8-12 %; ArmorPenetration: 12-18 % | ArmorPenetration 12,8%, AttackSpeed 8,2% |
| Helm | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Health 138,7, MagicResist 23,8, HealthRegeneration 41,5%, Armor 12,6 |
| Pants | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | Armor 11,6, MagicResist 19,9, Health 127,7, HealthRegeneration 41,4% |
| Shoulders | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat | HealthRegeneration 39,3%, MagicResist 19,6, Armor 12, Health 119,7 |
| Sword | 7 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 7,3%, PhysicalDamage 10,3, AttackSpeed 6,5% |
| Bow | 6 % | MagicalDamage: 13-20 flat; AttackSpeed: 8-12 %; MagicPenetration: 8-13 % | MagicPenetration 7%, AttackSpeed 6,7%, MagicalDamage 22,6 |
| Crossbow | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 7%, AttackSpeed 7%, PhysicalDamage 10,8 |
| GreatSword | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 10,6, AttackSpeed 6,9%, ArmorPenetration 7% |
| Shield | 6 % | Health: 100-150 flat; HealthRegeneration: 67-100 %; Armor: 10-15 flat; MagicResist: 16-25 flat; Shield: 100-200 flat | HealthRegeneration 30,1%, MagicResist 16,2, Shield 132,6, Health 93, Armor 11,3 |
| Quiver | 5 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | ArmorPenetration 6,4%, PhysicalDamage 10,6, AttackSpeed 6,7% |
| Amulet | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 10,4, ArmorPenetration 7,8%, AttackSpeed 6,2%, IncreasedItemRarity 0,5% |
| Ring | 6 % | PhysicalDamage: 6-10 flat; AttackSpeed: 8-12 %; ArmorPenetration: 8-13 % | PhysicalDamage 10,3, AttackSpeed 6,1%, ArmorPenetration 7,4% |

