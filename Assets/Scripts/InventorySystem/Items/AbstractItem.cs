using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [Serializable]
    public abstract class AbstractItem
    {
        [field: SerializeField] public Sprite Icon { get; protected set; } = null;
        [field: SerializeField] public ItemSize Dimensions { get; protected set; } = ItemSize.OneByOne;
        [field: SerializeField] public ItemStack StackLimit { get; protected set; } = ItemStack.Single;
        [field: SerializeField] public ItemRarity Rarity { get; protected set; } = ItemRarity.Common;
        [field: SerializeField] public List<PlayerStatModifier> Affixes { get; protected set; } = new List<PlayerStatModifier>();

        //[field: SerializeField] public List<StatName> AllowedAffixes { get; protected set; } = null;

        //public AbstractItem(Sprite icon, ItemSize dimensions, ItemStack stackLimit, ItemRarity rarity, List<ItemAffix> stats)
        //{
        //    Icon = icon;
        //    Dimensions = dimensions;
        //    StackLimit = stackLimit;
        //    Rarity = rarity;
        //    Stats = stats;
        //}

        // TODO: handle overTime effects => Stats != Effects --> see ARPG_Combat for DoT_effects
        public string Name => Colored($"{Rarity} {"Category"} {(Affixes.Count > 0 ? $"of {Affixes[0].Stat}" : "")}", GetRarityColor(Rarity));

        //Stats = stats.OrderBy(x => x.Stat).ToList();

        private string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.NoDrop => Color.clear,
            //ItemRarity.Crafted => new Color(0.4f, 0, 1, 1), // purple
            ItemRarity.Common => Color.white,
            //ItemRarity.Uncommon => Color.gray,
            ItemRarity.Magic => Color.blue,
            ItemRarity.Rare => Color.yellow,
            //ItemRarity.Set => Color.green,
            ItemRarity.Unique => new Color(1, 0.35f, 0, 1), // orange
            _ => Color.clear,
        };

        public static Vector2Int GetDimensions(ItemSize itemSize) => itemSize switch
        {
            ItemSize.NONE => Vector2Int.zero,
            ItemSize.OneByOne => new Vector2Int(1, 1),
            ItemSize.OneByTwo => new Vector2Int(1, 2),
            ItemSize.OneByThree => new Vector2Int(1, 3),
            ItemSize.OneByFour => new Vector2Int(1, 4),

            ItemSize.TwoByOne => new Vector2Int(2, 1),
            ItemSize.TwoByTwo => new Vector2Int(2, 2),
            ItemSize.TwoByThree => new Vector2Int(2, 3),
            ItemSize.TwoByFour => new Vector2Int(2, 3),
            _ => Vector2Int.zero,
        };

    }

    [Serializable]
    public class ConsumableItem : AbstractItem, IUsableItem
    {
        [field: SerializeField] public ConsumableType ConsumableType { get; protected set; }

        public ConsumableItem(ConsumableType consumableType, ItemRarity rarity)
        {
            ConsumableType = consumableType;
            Rarity = rarity;

            if (Rarity == ItemRarity.Unique)
            {
                // TODO: individual probabilityDistribution for each equipment type
                // var unique = correspondingTypeDistribution.GetRandomEnumerator();

                // Icon == unique.Icon
                // Dimensions == unique.Dimensions
                // Stats == stats + unique.Stats + equipmentType specific stats
            }
            else
            {
                Icon = GetIcon(ConsumableType, Rarity);
                Dimensions = GetDimension(ConsumableType);

                var stats = GetRandomAffixes(ConsumableType, Rarity);
                Affixes = GetStats(Rarity, stats);
            }

            Sprite GetIcon(ConsumableType consumableType, ItemRarity rarity) => null;

            ItemSize GetDimension(ConsumableType consumableType) => consumableType switch
            {
                ConsumableType.NONE => ItemSize.NONE,
                ConsumableType.Arrows => ItemSize.OneByOne,
                ConsumableType.Books => ItemSize.TwoByTwo,
                ConsumableType.Potions => ItemSize.OneByTwo,

                _ => ItemSize.NONE
            };

            List<PlayerStatModifier> GetRandomAffixes(ConsumableType consumableType, ItemRarity rarity)
            {
                var randomAffixAmount = GetAffixAmount();

                var affixList = new List<PlayerStatModifier>();

                /// selects item properties
                for (var i = 0; i < randomAffixAmount; i++)
                {
                    var allowedAffixes = Character.Instance.Stats.Select(x => x.Stat).ToList(); // switch => lookup table
                                                                                                // => weighting of allowed Affixes          => more likely to roll

                    /// weighted STATS
                    // => primary/secondary stats? -> requires further design
                    // => double-rolled stats? -> requires further design

                    var randomRoll = UnityEngine.Random.Range(0, allowedAffixes.Count);
                    var randomStat = allowedAffixes[randomRoll];
                    allowedAffixes.RemoveAt(randomRoll); // to exclude double rolls

                    /// weighted RANDOM ROLL
                    var lootLevel = Character.Instance.CharacterLevel; // define base min/max stat range
                    var minMax = Vector2.up; // as min/max is different for each stat we need a lookup table to convert the lootLevel into min/max ranges

                    var rangeModifier = rarity switch
                    {
                        ItemRarity.NoDrop => 0f,
                        //ItemRarity.Crafted => 1f,
                        ItemRarity.Common => 1f,
                        //ItemRarity.Uncommon => 1f,
                        ItemRarity.Magic => .9f,
                        ItemRarity.Rare => .8f,
                        //ItemRarity.Set => .8f,
                        ItemRarity.Unique => .7f,
                        _ => 0f,
                    };

                    minMax *= rangeModifier;

                    var value = UnityEngine.Random.Range(minMax.x, minMax.y);                           // TODO: should favor lower range value within min and max
                    var statModifier = new StatModifier(value, StatModifierType.FlatAdd); // TODO: lookup table for each statName => rework the statModifier to derive the type from the name?
                    var itemStat = new PlayerStatModifier(randomStat, statModifier);

                    affixList.Add(itemStat);
                }

                #region REQUIREMENTS / ITEM VALUE
                // => these are derived values from the random affixes
                #endregion

                return affixList;

                uint GetAffixAmount() => rarity switch    // TODO: itemType sensitive?
                {
                    ItemRarity.NoDrop => 0,
                    ItemRarity.Common => 0,
                    ItemRarity.Magic => 1,
                    ItemRarity.Rare => 2,
                    ItemRarity.Unique => 2,     // plus unique stats
                    _ => 0,

                    //ItemRarity.Crafted => 0,
                    //ItemRarity.Uncommon => 0,
                    //ItemRarity.Set => 2,      // plus set stats
                };
            }

            List<PlayerStatModifier> GetStats(ItemRarity rarity, List<PlayerStatModifier> randomAffixes) =>
                // plus itemType specific stats
                randomAffixes;
        }

        public void Consume() { }

        public void UseItem() => Consume();
    }

    [Serializable]
    public class EquipmentItem : AbstractItem, IUsableItem
    {
        [field: SerializeField] public EquipmentCategory EquipmentCategory { get; protected set; } // make EquipmentItem abstract and inherite for each category
        [field: SerializeField] public EquipmentType EquipmentType { get; protected set; } // might want to use inheritance instead and make EquipmentItem abstract to get more detailed itemTypes

        public EquipmentItem(EquipmentType equipmentType, ItemRarity rarity)
        {
            EquipmentType = equipmentType;
            Rarity = rarity;

            StackLimit = ItemStack.Single;

            // TODO: GetItemTypeData
            // -> this includes the icons, uniques to pick from, itemType specific affixes and allowedAffixes, their distribution and minMax range

            if (Rarity == ItemRarity.Unique)
            {
                // TODO: individual probabilityDistribution for each equipment type
                // var unique = correspondingTypeDistribution.GetRandomEnumerator();

                // Icon == unique.Icon
                // Dimensions == unique.Dimensions
                // Stats == stats + unique.Stats + equipmentType specific stats
            }
            else
            {
                Icon = GetIcon(EquipmentType, Rarity);
                Dimensions = GetDimension(EquipmentType);

                var stats = GetRandomAffixes(EquipmentType, Rarity);
                Affixes = GetStats(Rarity, stats);
            }

            // TODO: equipmentType defines the list of icons 
            // TODO: rarity defines what icon within the list
            Sprite GetIcon(EquipmentType equipmentType, ItemRarity rarity) => null;

            ItemSize GetDimension(EquipmentType equipmentType) => equipmentType switch
            {
                EquipmentType.NONE => ItemSize.NONE,

                EquipmentType.ARMAMENTS => ItemSize.NONE,
                EquipmentType.Belt => ItemSize.TwoByOne,
                EquipmentType.Boots => ItemSize.TwoByTwo,
                EquipmentType.Bracers => ItemSize.TwoByTwo,
                EquipmentType.Chest => ItemSize.TwoByThree,
                EquipmentType.Cloak => ItemSize.TwoByTwo,
                EquipmentType.Gloves => ItemSize.TwoByTwo,
                EquipmentType.Helm => ItemSize.TwoByTwo,
                EquipmentType.Pants => ItemSize.TwoByTwo,
                EquipmentType.Shoulders => ItemSize.TwoByTwo,

                EquipmentType.ONEHANDEDWEAPONS => ItemSize.NONE,
                EquipmentType.Sword => ItemSize.OneByThree,

                EquipmentType.TWOHANDEDWEAPONS => ItemSize.NONE,
                EquipmentType.Bow => ItemSize.TwoByThree,
                EquipmentType.GreatSword => ItemSize.TwoByFour,

                EquipmentType.OFFHANDS => ItemSize.NONE,
                EquipmentType.Shield => ItemSize.TwoByThree,
                EquipmentType.Quiver => ItemSize.OneByThree,

                EquipmentType.JEWELRY => ItemSize.NONE,
                EquipmentType.Amulet => ItemSize.OneByOne,
                EquipmentType.Ring => ItemSize.OneByOne,

                _ => ItemSize.NONE
            };

            List<PlayerStatModifier> GetRandomAffixes(EquipmentType equipmentType, ItemRarity rarity)
            {
                var randomAffixAmount = GetAffixAmount();

                var affixList = new List<PlayerStatModifier>();

                var allowedAffixes = Character.Instance.Stats.Select(x => x.Stat).ToList(); // switch => lookup table
                                                                                            // => weighting of allowed Affixes          => more likely to roll

                /// selects item properties
                for (var i = 0; i < randomAffixAmount; i++)
                {
                    var randomRoll = UnityEngine.Random.Range(0, allowedAffixes.Count);
                    var randomStat = allowedAffixes[randomRoll];
                    allowedAffixes.RemoveAt(randomRoll); // to exclude double rolls

                    /// weighted RANDOM ROLL
                    var lootLevel = Character.Instance.CharacterLevel; // define base min/max stat range
                    var minMax = Vector2.up; // as min/max is different for each stat we need a lookup table to convert the lootLevel into min/max ranges

                    var rangeModifier = rarity switch
                    {
                        ItemRarity.NoDrop => 0f,
                        //ItemRarity.Crafted => 1f,
                        ItemRarity.Common => 1f,
                        //ItemRarity.Uncommon => 1f,
                        ItemRarity.Magic => .9f,
                        ItemRarity.Rare => .8f,
                        //ItemRarity.Set => .8f,
                        ItemRarity.Unique => .7f,
                        _ => 0f,
                    };

                    minMax *= rangeModifier;

                    var value = UnityEngine.Random.Range(minMax.x, minMax.y);             // TODO: should favor lower range value within min and max
                    var statModifier = new StatModifier(value, StatModifierType.FlatAdd); // TODO: lookup table for each statName => rework the statModifier to derive the type from the name?
                    var itemStat = new PlayerStatModifier(randomStat, statModifier);

                    affixList.Add(itemStat);
                }

                #region REQUIREMENTS / ITEM VALUE
                // => these are derived values from the random affixes
                #endregion

                return affixList;

                uint GetAffixAmount() => rarity switch    // TODO: itemType sensitive?
                {
                    ItemRarity.NoDrop => 0,
                    ItemRarity.Common => 0,
                    ItemRarity.Magic => 1,
                    ItemRarity.Rare => 2,
                    ItemRarity.Unique => 2,     // plus unique stats
                    _ => 0,

                    //ItemRarity.Crafted => 0,
                    //ItemRarity.Uncommon => 0,
                    //ItemRarity.Set => 2,      // plus set stats
                };
            }

            List<PlayerStatModifier> GetStats(ItemRarity rarity, List<PlayerStatModifier> randomAffixes) =>
                // plus itemType specific stats
                randomAffixes;
        }

        //public EquipmentItem(Sprite icon, ItemSize dimensions, ItemStack stackLimit, ItemRarity rarity, List<ItemAffix> stats, EquipmentType equipmentType) : base(icon, dimensions, stackLimit, rarity, stats)
        //    => EquipmentType = equipmentType;


        public void UseItem()
        {
            foreach (var package in InventoryProvider.Instance.PlayerEquipment.StoredPackages.Values)
                if (package.Item == this)
                {
                    Unequip(new(this));
                    return;
                }

            Equip(new(this));
        }

        private void Equip(Package package)
        {
            var remaining = InventoryProvider.Instance.PlayerEquipment.AddToContainer(package);

            if (0 < remaining.Amount)
                InventoryProvider.Instance.PlayerInventory.RemoveFromContainer(package);
        }

        private void Unequip(Package package)
        {
            var remaining = InventoryProvider.Instance.PlayerInventory.AddToContainer(package);

            if (0 < remaining.Amount)
                InventoryProvider.Instance.PlayerEquipment.RemoveFromContainer(package);
        }
    }

    public interface IUsableItem
    {
        public abstract void UseItem();
    }
}