using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Extensions;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [Serializable]
    public abstract class AbstractItem
    {
        [field: SerializeField] public Sprite Icon { get; protected set; } = null; // make Icon a class that is serialized (rendered) in editor
        [field: SerializeField] public ItemSize Dimensions { get; protected set; } = ItemSize.OneByOne;
        [field: SerializeField] public ItemStack StackLimit { get; protected set; } = ItemStack.Single;
        [field: SerializeField] public ItemRarity Rarity { get; protected set; } = ItemRarity.Common;
        [field: SerializeField] public List<PlayerStatModifier> Affixes { get; protected set; } = new List<PlayerStatModifier>();

        // TODO: handle overTime effects => Stats != Effects --> see ARPG_Combat for DoT_effects
        public new abstract string ToString();

        //Stats = stats.OrderBy(x => x.Stat).ToList();

        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.NoDrop => Color.clear,
            //ItemRarity.Crafted => new Color(0.4f, 0, 1, 1), // purple
            ItemRarity.Common => Color.white,
            //ItemRarity.Uncommon => Color.gray,
            ItemRarity.Magic => Color.cyan,
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
            if (rarity == ItemRarity.NoDrop)
            {
                Debug.LogWarning("This should not happen");
                return;
            }

            ConsumableType = consumableType;
            Rarity = rarity;

            StackLimit = ItemStack.StackOfTen; // TODO: Get type specific stack limit

            Icon = ItemProvider.Instance.GetIcon(ConsumableType, Rarity);
            Dimensions = GetDimension(ConsumableType);
            var stats = GetRandomAffixes(ConsumableType, Rarity);
            //stats + ConsumableType specific stats

            if (Rarity == ItemRarity.Unique)
            {
                var unique = ItemProvider.Instance.GetUnique(ConsumableType).GetItem();

                Icon = unique.Icon;
                Dimensions = unique.Dimensions;

                for (var i = 0; i < unique.Affixes.Count; i++)
                    stats.Add(unique.Affixes[i]);
            }

            //Affixes = GetStats(Rarity, stats);
            Affixes = stats;

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
                var affixAmount = GetAffixAmount(rarity);

                var affixList = new List<PlayerStatModifier>();

                var allowedAffixes = ItemProvider.Instance.ItemTypeData.GetPossibleStats(consumableType).ToList();

                /// selects item properties
                for (var i = 0; i < affixAmount; i++)
                {
                    if (allowedAffixes.Count <= 0)
                        break;

                    var randomRoll = UnityEngine.Random.Range(0, allowedAffixes.Count);
                    var randomStat = allowedAffixes[randomRoll];
                    allowedAffixes.RemoveAt(randomRoll); // => exclude double rolls

                    /// weighted RANDOM ROLL
                    var lootLevel = Character.Instance.CharacterLevel; // define base min/max stat range

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

                    var modifier = randomStat.GetRandomRoll(rangeModifier);
                    var itemStat = new PlayerStatModifier(randomStat.StatName, modifier);

                    affixList.Add(itemStat);
                }

                #region REQUIREMENTS / ITEM VALUE
                // => these are derived values from the random affixes
                #endregion

                return affixList;

                uint GetAffixAmount(ItemRarity rarity) => rarity switch    // TODO: itemType sensitive?
                {
                    ItemRarity.NoDrop => 0,
                    ItemRarity.Common => 0,     // plus item specific stat
                    ItemRarity.Magic => 1,
                    ItemRarity.Rare => 2,
                    ItemRarity.Unique => 2,     // plus unique stats
                    _ => 0,

                    //ItemRarity.Crafted => 0,
                    //ItemRarity.Uncommon => 0,
                    //ItemRarity.Set => 2,      // plus set stats
                };
            }
        }

        public void Consume() { }

        public void UseItem() => Consume();
        public override string ToString() => $"{Rarity} {ConsumableType}".Colored(GetRarityColor(Rarity));

    }

    [Serializable]
    public class EquipmentItem : AbstractItem, IUsableItem
    {
        [field: SerializeField] public EquipmentCategory EquipmentCategory { get; protected set; } // make EquipmentItem abstract and inherite for each category
        [field: SerializeField] public EquipmentType EquipmentType { get; protected set; } // might want to use inheritance instead and make EquipmentItem abstract to get more detailed itemTypes

        public EquipmentItem(EquipmentType equipmentType, ItemRarity rarity)
        {
            if (rarity == ItemRarity.NoDrop)
                return;

            EquipmentType = equipmentType;
            Rarity = rarity;

            StackLimit = ItemStack.Single;

            Icon = ItemProvider.Instance.GetIcon(EquipmentType, Rarity);
            Dimensions = GetDimension(EquipmentType);
            var stats = GetRandomAffixes(EquipmentType, Rarity);
            //stats + equipmentType specific stats

            if (Rarity == ItemRarity.Unique)
            {
                var unique = ItemProvider.Instance.GetUnique(EquipmentType).GetItem();

                Icon = unique.Icon;
                Dimensions = unique.Dimensions;

                for (var i = 0; i < unique.Affixes.Count; i++)
                    stats.Add(unique.Affixes[i]);
            }

            //Affixes = GetStats(Rarity, stats);
            Affixes = stats;

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
                var affixAmount = GetAffixAmount(rarity);

                var affixList = new List<PlayerStatModifier>();

                var allowedAffixes = ItemProvider.Instance.ItemTypeData.GetPossibleStats(equipmentType).ToList();

                /// selects item properties
                for (var i = 0; i < affixAmount; i++)
                {
                    if (allowedAffixes.Count <= 0)
                        break;

                    var randomRoll = UnityEngine.Random.Range(0, allowedAffixes.Count);
                    var randomStat = allowedAffixes[randomRoll];
                    allowedAffixes.RemoveAt(randomRoll); // => exclude double rolls

                    /// weighted RANDOM ROLL
                    var lootLevel = Character.Instance.CharacterLevel; // define base min/max stat range

                    var rangeModifier = rarity switch
                    {
                        ItemRarity.NoDrop => 0f,
                        ItemRarity.Common => 1f,
                        ItemRarity.Magic => .9f,
                        ItemRarity.Rare => .8f,
                        ItemRarity.Unique => .7f,

                        //ItemRarity.Crafted => 1f,
                        //ItemRarity.Uncommon => 1f,
                        //ItemRarity.Set => .8f,
                        _ => 0f,
                    };

                    var modifier = randomStat.GetRandomRoll(rangeModifier);
                    var itemStat = new PlayerStatModifier(randomStat.StatName, modifier);

                    affixList.Add(itemStat);
                }

                #region REQUIREMENTS / ITEM VALUE
                // => these are derived values from the random affixes
                #endregion

                return affixList;

                uint GetAffixAmount(ItemRarity rarity) => rarity switch    // TODO: itemType sensitive?
                {
                    ItemRarity.NoDrop => 0,
                    ItemRarity.Common => 1,     // plus item specific stat
                    ItemRarity.Magic => 2,
                    ItemRarity.Rare => 3,
                    ItemRarity.Unique => 3,     // plus unique stats
                    _ => 0,

                    //ItemRarity.Crafted => 0,
                    //ItemRarity.Uncommon => 0,
                    //ItemRarity.Set => 2,      // plus set stats
                };
            }

            // List<PlayerStatModifier> GetStats(ItemRarity rarity, List<PlayerStatModifier> randomAffixes) => randomAffixes;
        }

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
        public override string ToString() => $"{Rarity} {EquipmentType}".Colored(GetRarityColor(Rarity));
    }

    public interface IUsableItem
    {
        public abstract void UseItem();
    }
}