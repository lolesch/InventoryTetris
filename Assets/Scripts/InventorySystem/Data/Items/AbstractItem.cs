﻿using System;
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
    public abstract class AbstractItem : IEquatable<AbstractItem> //, IComparable
    {
        [field: SerializeField] public Sprite Icon { get; protected set; } = null;
        [field: SerializeField] public ItemSize Dimensions { get; protected set; } = ItemSize.OneByOne;
        [field: SerializeField] public ItemStack StackLimit { get; protected set; } = ItemStack.Single;
        [field: SerializeField] public ItemRarity Rarity { get; protected set; } = ItemRarity.Common;

        // consider changing to prefix/suffix system
        /// keep this as list (vs array) since crafting migth add/remove Affixes
        [field: SerializeField] public List<CharacterStatModifier> Affixes { get; protected set; } = new List<CharacterStatModifier>();

        // TODO: handle overTime effects => Stats != Effects --> see ARPG_Combat for DoT_effects
        public new abstract string ToString();

        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Magic => Color.cyan,
            ItemRarity.Rare => Color.yellow,
            ItemRarity.Unique => new Color(1, 0.35f, 0, 1), // orange

            //ItemRarity.Uncommon => Color.gray,
            //ItemRarity.Crafted => new Color(0.4f, 0, 1, 1), // purple
            //ItemRarity.Set => Color.green,

            ItemRarity.NoDrop => Color.clear,
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

        protected static List<CharacterStatModifier> CombineAffixesOfSameTypeAndMod(List<CharacterStatModifier> affixes)
        {
            // for each affix go down the list and check for same affix and same type
            for (var i = 0; i < affixes.Count; i++)
                for (var j = affixes.Count; j-- > i + 1;) // reverse loop because we remove elements
                    if (affixes[i].Stat == affixes[j].Stat && affixes[i].Modifier.Type == affixes[j].Modifier.Type)
                    {
                        var range = affixes[i].Modifier.Range + affixes[j].Modifier.Range;
                        /*
                        var randomRoll = UnityEngine.Random.Range(0f, 1f);
                        var weightedRoll = Distribution.Evaluate(randomRoll);

                        // TODO modified affix range can result in higher min than max values => reorder before setting the range

                        /// the higher the rarity, the higher the min range => Unique items roll with usefull affix values
                        var min = Mathf.CeilToInt(Range.x * modifier);

                        /// the lesser the rarity, the higher the max range => Common items can roll the highest stats => good base for crafting
                        var max = Mathf.CeilToInt(Range.y * (1f + (1f - modifier)));

                        if (max < min)
                            (min, max) = (max, min);

                        var mappedValue = weightedRoll.MapFrom01(min - 1, max);
                        var value = Mathf.Max(min, Mathf.CeilToInt(mappedValue));
                        */
                        var value = affixes[i].Modifier.Value + affixes[j].Modifier.Value;
                        affixes[i] = new CharacterStatModifier(affixes[i].Stat, new StatModifier(range, value, affixes[i].Modifier.Type));
                        affixes.RemoveAt(j);
                    }
            return affixes;
        }

        public bool Equals(AbstractItem other) => Icon == other.Icon && Dimensions == other.Dimensions && StackLimit == other.StackLimit && Rarity == other.Rarity;// && Affixes == other.Affixes;
    }

    [Serializable]
    public class ConsumableItem : AbstractItem
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
                var unique = ItemProvider.Instance.GetUnique(ConsumableType);

                Icon = unique.Icon;
                Dimensions = unique.Dimensions;

                for (var i = 0; i < unique.Affixes.Count; i++)
                    stats.Add(unique.Affixes[i]);
            }

            Affixes = CombineAffixesOfSameTypeAndMod(stats);

            ItemSize GetDimension(ConsumableType consumableType) => consumableType switch
            {
                ConsumableType.NONE => ItemSize.NONE,

                ConsumableType.Arrow => ItemSize.OneByOne,
                ConsumableType.Book => ItemSize.TwoByTwo,
                ConsumableType.Potion => ItemSize.OneByTwo,

                _ => ItemSize.NONE
            };

            List<CharacterStatModifier> GetRandomAffixes(ConsumableType consumableType, ItemRarity rarity)
            {
                var affixAmount = GetAffixAmount(rarity);

                var affixList = new List<CharacterStatModifier>();

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

                    //// TODO: determine statModType => lookup table for each statName
                    //var modifier = new StatModifier(rangeRoll.Range, rangeRoll.value/*, type*/);

                    var itemStat = new CharacterStatModifier(randomStat.StatName, rangeRoll);

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
        public override string ToString() => $"{Rarity} {ConsumableType}".Colored(GetRarityColor(Rarity));

    }

    [Serializable]
    public class EquipmentItem : AbstractItem
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

            // TODO: implement something like GetRandomAffixes() to reroll the value within the affix range
            if (Rarity == ItemRarity.Unique)
            {
                var unique = ItemProvider.Instance.GetUnique(EquipmentType);

                Icon = unique.Icon;

                for (var i = 0; i < unique.Affixes.Count; i++)
                {
                    var itemStat = new CharacterStatModifier(unique.Affixes[i].Stat, unique.Affixes[i].Modifier); // this should clamp the value within the range

                    stats.Add(itemStat);
                }
            }

            Affixes = CombineAffixesOfSameTypeAndMod(stats);

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
                EquipmentType.Bow => ItemSize.TwoByThree,

                EquipmentType.TWOHANDEDWEAPONS => ItemSize.NONE,
                EquipmentType.Crossbow => ItemSize.TwoByFour,
                EquipmentType.GreatSword => ItemSize.TwoByFour,

                EquipmentType.OFFHANDS => ItemSize.NONE,
                EquipmentType.Shield => ItemSize.TwoByThree,
                EquipmentType.Quiver => ItemSize.OneByThree,

                EquipmentType.JEWELRY => ItemSize.NONE,
                EquipmentType.Amulet => ItemSize.OneByOne,
                EquipmentType.Ring => ItemSize.OneByOne,

                _ => ItemSize.NONE
            };

            List<CharacterStatModifier> GetRandomAffixes(EquipmentType equipmentType, ItemRarity rarity)
            {
                var affixAmount = GetAffixAmount(rarity);

                var affixList = new List<CharacterStatModifier>();

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

                    var rangeRoll = randomStat.GetRandomRoll(rarity); //, statModTypeOverride, lootLevel*/);

                    //// TODO: determine statModType => lookup table for each statName
                    //var modifier = new StatModifier(rangeRoll.Range, rangeRoll.value/*, type*/);

                    var itemStat = new CharacterStatModifier(randomStat.StatName, rangeRoll);

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

        //TODO: extend naming
        public override string ToString() => $"{Rarity} {EquipmentType}".Colored(GetRarityColor(Rarity));
    }
}