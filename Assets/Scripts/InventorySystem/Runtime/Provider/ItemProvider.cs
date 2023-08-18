using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    public class ItemProvider : AbstractProvider<ItemProvider>
    {
        public ItemTypeData ItemTypeData;

        [Header("Distributions")]
        [SerializeField] private ItemCategoryDistribution itemCategoryDistribution;
        [SerializeField] private ItemRarityDistribution itemRarityDistribution;
        [SerializeField] private EquipmentCategoryDistribution equipmentCategoryDistribution;
        [SerializeField] private EquipmentTypeDistribution armamentsDistribution;
        [SerializeField] private WeaponCategoryDistribution weaponCategoryDistribution;
        [SerializeField] private EquipmentTypeDistribution oneHandDistribution;
        [SerializeField] private EquipmentTypeDistribution twoHandDistribution;
        [SerializeField] private EquipmentTypeDistribution offHandDistribution;
        [SerializeField] private EquipmentTypeDistribution jewelryDistribution;
        [SerializeField] private ConsumableTypeDistribution consumableTypeDistribution;

        [Header("Uniques")]
        [SerializeField] private List<AbstractItemObject> Amulets;
        [SerializeField] private List<AbstractItemObject> Belts;
        [SerializeField] private List<AbstractItemObject> Boots;
        [SerializeField] private List<AbstractItemObject> Bracers;
        [SerializeField] private List<AbstractItemObject> Chests;
        [SerializeField] private List<AbstractItemObject> Cloaks;
        [SerializeField] private List<AbstractItemObject> Gloves;
        [SerializeField] private List<AbstractItemObject> Helmets;
        [SerializeField] private List<AbstractItemObject> Pants;
        [SerializeField] private List<AbstractItemObject> Quiver;
        [SerializeField] private List<AbstractItemObject> Rings;
        [SerializeField] private List<AbstractItemObject> Shields;
        [SerializeField] private List<AbstractItemObject> Shoulder;
        [SerializeField] private List<AbstractItemObject> Swords;
        [SerializeField] private List<AbstractItemObject> Bows;
        [SerializeField] private List<AbstractItemObject> GreatSwords;
        [Space]
        [SerializeField] private List<AbstractItemObject> Arrows;
        [SerializeField] private List<AbstractItemObject> Books;
        [SerializeField] private List<AbstractItemObject> Potions;

        public List<AbstractItem> GenerateRandomLoot(uint amount = 1)
        {
            var generatedLoot = new List<AbstractItem>();
            /// calculates the number of items to drop
            CalculateBonusDrops(ref amount);

            for (var i = 0; i < amount; i++)
                generatedLoot.Add(GenerateRandomItem());

            return generatedLoot;

            static void CalculateBonusDrops(ref uint amount)
            {
                var bonusDrops = Character.Instance.GetStatValue(StatName.IncreasedItemQuantityPercent);
                amount += (uint)(bonusDrops / 100f); // TODO: requires a better formula
            }
        }

        private AbstractItem GenerateRandomItem()
        {
            /// selects item type
            var itemCategory = itemCategoryDistribution.GetRandomEnumerator();

            return itemCategory switch
            {
                ItemCategory.Equipment => GenerateRandomEquipment(),
                ItemCategory.Consumable => GenerateRandomConsumable(),

                ItemCategory.NONE => null,
                _ => null,
            };
        }

        private AbstractItem GenerateRandomEquipment()
        {
            var equipmentCategory = equipmentCategoryDistribution.GetRandomEnumerator();

            return equipmentCategory switch
            {
                EquipmentCategory.Armaments => GenerateRandomArmament(),
                EquipmentCategory.Weapons => GenerateRandomWeapon(),
                EquipmentCategory.Jewelry => GenerateRandomJewelry(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomArmament()
        {
            var equipmentType = armamentsDistribution.GetRandomEnumerator();

            return equipmentType switch
            {
                EquipmentType.Belt => GenerateRandomBelt(),
                EquipmentType.Boots => GenerateRandomBoots(),
                EquipmentType.Bracers => GenerateRandomBracers(),
                EquipmentType.Chest => GenerateRandomChest(),
                EquipmentType.Cloak => GenerateRandomCloak(),
                EquipmentType.Gloves => GenerateRandomGloves(),
                EquipmentType.Helm => GenerateRandomHelm(),
                EquipmentType.Pants => GenerateRandomPants(),
                EquipmentType.Shoulders => GenerateRandomShoulders(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomWeapon()
        {
            var weaponCategory = weaponCategoryDistribution.GetRandomEnumerator();

            return weaponCategory switch
            {
                WeaponCategory.Weapon_1H => GenerateRandomOneHand(),
                WeaponCategory.Weapon_2H => GenerateRandomTwoHand(),
                WeaponCategory.Offhand => GenerateRandomOffHand(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomOneHand()
        {
            var equipmentType = oneHandDistribution.GetRandomEnumerator();

            return equipmentType switch
            {
                EquipmentType.Sword => GenerateRandomSword(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomTwoHand()
        {
            var equipmentType = twoHandDistribution.GetRandomEnumerator();

            return equipmentType switch
            {
                EquipmentType.Bow => GenerateRandomBow(),
                EquipmentType.GreatSword => GenerateRandomGreatSword(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomOffHand()
        {
            var equipmentType = offHandDistribution.GetRandomEnumerator();

            return equipmentType switch
            {
                EquipmentType.Shield => GenerateRandomShield(),
                EquipmentType.Quiver => GenerateRandomQuiver(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomJewelry()
        {
            var equipmentType = jewelryDistribution.GetRandomEnumerator();

            return equipmentType switch
            {
                EquipmentType.Amulet => GenerateRandomAmulet(),
                EquipmentType.Ring => GenerateRandomRing(),

                _ => null,
            };
        }

        private AbstractItem GenerateRandomConsumable()
        {
            var consumable = consumableTypeDistribution.GetRandomEnumerator();

            var itemRarity = GetRandomRarity();

            return consumable switch
            {
                ConsumableType.Arrows => new ConsumableItem(consumable, itemRarity),     // TODO: generate specific
                ConsumableType.Books => new ConsumableItem(consumable, itemRarity),      // TODO: generate specific
                ConsumableType.Potions => new ConsumableItem(consumable, itemRarity),    // TODO: generate specific

                _ => null,
            };
        }

        public AbstractItem GenerateRandomBelt() => GenerateRandomOfEquipmentType(EquipmentType.Belt);
        public AbstractItem GenerateRandomBoots() => GenerateRandomOfEquipmentType(EquipmentType.Boots);
        public AbstractItem GenerateRandomBracers() => GenerateRandomOfEquipmentType(EquipmentType.Bracers);
        public AbstractItem GenerateRandomChest() => GenerateRandomOfEquipmentType(EquipmentType.Chest);
        public AbstractItem GenerateRandomCloak() => GenerateRandomOfEquipmentType(EquipmentType.Cloak);
        public AbstractItem GenerateRandomGloves() => GenerateRandomOfEquipmentType(EquipmentType.Gloves);
        public AbstractItem GenerateRandomHelm() => GenerateRandomOfEquipmentType(EquipmentType.Helm);
        public AbstractItem GenerateRandomPants() => GenerateRandomOfEquipmentType(EquipmentType.Pants);
        public AbstractItem GenerateRandomShoulders() => GenerateRandomOfEquipmentType(EquipmentType.Shoulders);
        public AbstractItem GenerateRandomSword() => GenerateRandomOfEquipmentType(EquipmentType.Sword);
        public AbstractItem GenerateRandomBow() => GenerateRandomOfEquipmentType(EquipmentType.Bow);
        public AbstractItem GenerateRandomGreatSword() => GenerateRandomOfEquipmentType(EquipmentType.GreatSword);
        public AbstractItem GenerateRandomShield() => GenerateRandomOfEquipmentType(EquipmentType.Shield);
        public AbstractItem GenerateRandomQuiver() => GenerateRandomOfEquipmentType(EquipmentType.Quiver);
        public AbstractItem GenerateRandomAmulet() => GenerateRandomOfEquipmentType(EquipmentType.Amulet);
        public AbstractItem GenerateRandomRing() => GenerateRandomOfEquipmentType(EquipmentType.Ring);

        public AbstractItem GenerateRandomOfEquipmentType(EquipmentType equipmentType)
        {
            var rarity = GetRandomRarity();
            return rarity == ItemRarity.NoDrop
                ? null
                : equipmentType switch
                {
                    EquipmentType.ARMAMENTS => GenerateRandomArmament(),
                    EquipmentType.Belt => new EquipmentItem(EquipmentType.Belt, rarity),
                    EquipmentType.Boots => new EquipmentItem(EquipmentType.Boots, rarity),
                    EquipmentType.Bracers => new EquipmentItem(EquipmentType.Bracers, rarity),
                    EquipmentType.Chest => new EquipmentItem(EquipmentType.Chest, rarity),
                    EquipmentType.Cloak => new EquipmentItem(EquipmentType.Cloak, rarity),
                    EquipmentType.Gloves => new EquipmentItem(EquipmentType.Gloves, rarity),
                    EquipmentType.Helm => new EquipmentItem(EquipmentType.Helm, rarity),
                    EquipmentType.Pants => new EquipmentItem(EquipmentType.Pants, rarity),
                    EquipmentType.Shoulders => new EquipmentItem(EquipmentType.Shoulders, rarity),

                    EquipmentType.ONEHANDEDWEAPONS => GenerateRandomOneHand(),
                    EquipmentType.Sword => new EquipmentItem(EquipmentType.Sword, rarity),

                    EquipmentType.TWOHANDEDWEAPONS => GenerateRandomTwoHand(),
                    EquipmentType.Bow => new EquipmentItem(EquipmentType.Bow, rarity),
                    EquipmentType.GreatSword => new EquipmentItem(EquipmentType.GreatSword, rarity),

                    EquipmentType.OFFHANDS => GenerateRandomOffHand(),
                    EquipmentType.Shield => new EquipmentItem(EquipmentType.Shield, rarity),
                    EquipmentType.Quiver => new EquipmentItem(EquipmentType.Quiver, rarity),

                    EquipmentType.JEWELRY => GenerateRandomJewelry(),
                    EquipmentType.Amulet => new EquipmentItem(EquipmentType.Amulet, rarity),
                    EquipmentType.Ring => new EquipmentItem(EquipmentType.Ring, rarity),

                    _ => null,
                };
        }

        public AbstractItem GenerateRandomOfConsumableType(ConsumableType consumableType)
        {
            var rarity = GetRandomRarity();
            return rarity == ItemRarity.NoDrop
                ? null
                : consumableType switch
                {
                    ConsumableType.Arrows => new ConsumableItem(ConsumableType.Arrows, rarity),
                    ConsumableType.Books => new ConsumableItem(ConsumableType.Books, rarity),
                    ConsumableType.Potions => new ConsumableItem(ConsumableType.Potions, rarity),

                    _ => null,
                };
        }

        private ItemRarity GetRandomRarity() => itemRarityDistribution.GetRandomEnumerator();

        // TODO: equipmentType defines the list of icons 
        // TODO: rarity defines what icon within the list
        public Sprite GetIcon(EquipmentType equipmentType, ItemRarity rarity)
        {
            var unique = GetUnique(equipmentType);

            return unique != null ? unique.GetItem().Icon : null;
        }

        public Sprite GetIcon(ConsumableType consumableType, ItemRarity rarity)
        {
            var unique = GetUnique(consumableType);

            return unique != null ? unique.GetItem().Icon : null;
        }

        public AbstractItemObject GetUnique(EquipmentType equipmentType)
        {
            // TODO: individual probabilityDistribution for each equipment type
            // var unique = correspondingTypeDistribution.GetRandomEnumerator();
            // => player level sensitive ?

            var uniquesOfType = equipmentType switch
            {
                EquipmentType.ARMAMENTS => null,
                EquipmentType.Belt => Belts,
                EquipmentType.Boots => Boots,
                EquipmentType.Bracers => Bracers,
                EquipmentType.Chest => Chests,
                EquipmentType.Cloak => Cloaks,
                EquipmentType.Gloves => Gloves,
                EquipmentType.Helm => Helmets,
                EquipmentType.Pants => Pants,
                EquipmentType.Shoulders => Shoulder,

                EquipmentType.ONEHANDEDWEAPONS => null,
                EquipmentType.Sword => Swords,

                EquipmentType.TWOHANDEDWEAPONS => null,
                EquipmentType.Bow => Bows,
                EquipmentType.GreatSword => GreatSwords,

                EquipmentType.OFFHANDS => null,
                EquipmentType.Shield => Shields,
                EquipmentType.Quiver => Quiver,

                EquipmentType.JEWELRY => null,
                EquipmentType.Amulet => Amulets,
                EquipmentType.Ring => Rings,

                _ => null,
            };

            return GetUniqueFromList(uniquesOfType);
        }

        public AbstractItemObject GetUnique(ConsumableType consumableType)
        {
            // TODO: individual probabilityDistribution for each equipment type
            // var unique = correspondingTypeDistribution.GetRandomEnumerator();
            // => player level sensitive ?

            var uniquesOfType = consumableType switch
            {
                ConsumableType.Arrows => Arrows,
                ConsumableType.Books => Books,
                ConsumableType.Potions => Potions,

                _ => null,
            };

            return GetUniqueFromList(uniquesOfType);
        }

        private static AbstractItemObject GetUniqueFromList(List<AbstractItemObject> uniquesOfType)
        {
            if (uniquesOfType.Count <= 0)
            {
                Debug.LogWarning($"No unique available");
                return null;
            }

            var index = Random.Range(0, uniquesOfType.Count);
            return uniquesOfType[index];
        }
    }
}
