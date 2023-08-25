using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [Serializable]
    public abstract class AbstractItem // TODO: inherit IComparable
    {
        [field: SerializeField] public Sprite Icon { get; protected set; } = null;
        [field: SerializeField] public ItemSize Dimensions { get; protected set; } = ItemSize.OneByOne;
        [field: SerializeField] public ItemStack StackLimit { get; protected set; } = ItemStack.Single;
        [field: SerializeField] public ItemRarity Rarity { get; protected set; } = ItemRarity.Common;
        [field: SerializeField] public List<PlayerStatModifier> Affixes { get; protected set; } = new List<PlayerStatModifier>();
        // might want to change this to prefix/suffix system

        // TODO: handle overTime effects => Stats != Effects --> see ARPG_Combat for DoT_effects
        public new abstract string ToString();

        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.NoDrop => Color.clear,
            ItemRarity.Common => Color.white,
            ItemRarity.Magic => Color.cyan,
            ItemRarity.Rare => Color.yellow,
            ItemRarity.Unique => new Color(1, 0.35f, 0, 1), // orange

            //ItemRarity.Uncommon => Color.gray,
            //ItemRarity.Crafted => new Color(0.4f, 0, 1, 1), // purple
            //ItemRarity.Set => Color.green,
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
            ItemSize.TwoByFour => new Vector2Int(2, 4),
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

            CombineAffixesOfSameTypeAndMod();

            void CombineAffixesOfSameTypeAndMod()
            {
                // for each affix go down the list and check for same affix wich same type
                for (var i = 0; i < Affixes.Count; i++)
                    for (var j = Affixes.Count; j-- > i;) // reverse loop because we remove elements
                        if (Affixes[i].Stat == Affixes[j].Stat && Affixes[i].Modifier.Type == Affixes[j].Modifier.Type)
                        {
                            var range = Affixes[i].Modifier.Range + Affixes[j].Modifier.Range;
                            var value = Affixes[i].Modifier.Value + Affixes[j].Modifier.Value;
                            Affixes[i] = new PlayerStatModifier(Affixes[i].Stat, new StatModifier(range, value, Affixes[i].Modifier.Type));
                            Affixes.RemoveAt(j);
                        }
            }

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
                    //var lootLevel = LocalPlayer.Instance.CharacterLevel; // define base min/max stat range

                    var rangeRoll = randomStat.GetRandomRoll(rarity /*, lootLevel*/);

                    // TODO: determine statModType => lookup table for each statName
                    var modifier = new StatModifier(rangeRoll.Range, rangeRoll.value/*, type*/);

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

        public void Consume() => Debug.Log($"Consuming {ToString()}");

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
            //TODO: stats + equipmentType specific stats
            // TODO: add weapon attacks per second

            if (Rarity == ItemRarity.Unique)
            {
                var unique = ItemProvider.Instance.GetUnique(EquipmentType);

                Icon = unique.Icon;

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

                    // var lootLevel = LocalPlayer.CharacterLevel; // could modify min/max stat range

                    var rangeRoll = randomStat.GetRandomRoll(rarity /*, lootLevel*/);

                    // TODO: determine statModType => lookup table for each statName
                    var modifier = new StatModifier(rangeRoll.Range, rangeRoll.value/*, type*/);

                    var itemStat = new PlayerStatModifier(randomStat.StatName, modifier);

                    affixList.Add(itemStat);
                }

                #region REQUIREMENTS / ITEM VALUE
                // => these are derived values from the random affixes
                #endregion

                affixList.Sort();

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
                // does that mean we cant equip from outside the inventory?
                InventoryProvider.Instance.PlayerInventory.RemoveFromContainer(package);
        }

        private void Unequip(Package package)
        {
            var remaining = InventoryProvider.Instance.PlayerInventory.AddToContainer(package);

            if (0 < remaining.Amount)
                InventoryProvider.Instance.PlayerEquipment.RemoveFromContainer(package);
        }

        //TODO: extend naming
        public override string ToString() => $"{Rarity} {EquipmentType}".Colored(GetRarityColor(Rarity));
    }

    // TODO: should use the package instead => package stores the amount and has more knowledge of the container it is stored in
    public interface IUsableItem
    {
        public abstract void UseItem();
    }
}