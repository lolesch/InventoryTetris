﻿using System;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.Serialization;

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    [CreateAssetMenu(fileName = "Item Type Data", menuName = "Inventory System/ItemType Data")]
    public class ItemTypeData : ScriptableObject
    {
        [Serializable]
        public class StatRange : ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector] public string name;
            [SerializeField] public StatName StatName;
            [FormerlySerializedAs("MinMax")]
            [SerializeField] private Vector2Int Range;
            [SerializeField] private StatModifierType DefaultModType = StatModifierType.FlatAdd;

            [Tooltip("Modifies the likleyness to roll values within the given range")]
            [SerializeField] public AnimationCurve Distribution;

            public StatRange(StatName statName, Vector2Int range, StatModifierType defaultModType)
            {
                StatName = statName;
                Range = range;
                DefaultModType = defaultModType;

                name = StatName.ToString();
            }

            public StatModifier GetRandomRoll(ItemRarity rarity)
            {
                /// ITEM AFFIX DESIGN BY RARITY:
                /// 
                /// the lesser the rarity, the higher the max range => Common items can roll the highest stats => good base for crafting
                /// the higher the rarity, the higher the min range => Unique items roll with usefull affix values

                var modifier = rarity switch
                {
                    ItemRarity.NoDrop => 0f,
                    ItemRarity.Common => 1f,
                    ItemRarity.Magic => 1.1f,
                    ItemRarity.Rare => 1.2f,
                    ItemRarity.Unique => 1.3f,

                    //ItemRarity.Crafted => 1f,
                    //ItemRarity.Uncommon => 1f,
                    //ItemRarity.Set => .8f,
                    _ => 0f,
                };

                var randomRoll = UnityEngine.Random.Range(0f, 1f);
                var weightedRoll = Distribution.Evaluate(randomRoll);

                /// the higher the rarity, the higher the min range => Unique items roll with usefull affix values
                var min = Mathf.CeilToInt(Range.x * modifier);

                /// the lesser the rarity, the higher the max range => Common items can roll the highest stats => good base for crafting
                var max = Mathf.CeilToInt(Range.y * (1f + (1f - modifier)));

                /// reorder before setting the range
                if (max < min)
                    (min, max) = (max, min);

                var mappedValue = weightedRoll.MapFrom01(min - 1, max);
                var value = Mathf.Max(min, Mathf.CeilToInt(mappedValue));

                return new StatModifier(new Vector2Int(min, max), value, DefaultModType);
            }

            public void OnBeforeSerialize() => name = $"{StatName}\t{Range}\t{DefaultModType}";

            public void OnAfterDeserialize() { }
        }

        [Serializable]
        public class EquipmentTypeSpecificStatRange : ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector] public string name;
            [SerializeField, HideInInspector] public EquipmentType EquipmentType;
            [SerializeField] public StatRange[] StatRanges;
            // TODO: implement guarantied type specific stats and random stats
            // => i.e. 2h mace always rolls with splashDamage and random 2h weapon stats

            public void OnBeforeSerialize() => name = EquipmentType.ToString();
            public void OnAfterDeserialize() { }
        }

        [Serializable]
        public class ConsumableTypeSpecificStatRange : ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector] public string name;
            [SerializeField, HideInInspector] public ConsumableType ConsumableType;
            [SerializeField] public StatRange[] StatRanges;

            public void OnBeforeSerialize() => name = ConsumableType.ToString();
            public void OnAfterDeserialize() { }
        }

        [Serializable]
        public class StatIcon : ISerializationCallbackReceiver
        {
            [HideInInspector] public string name;
            public StatName StatName = StatName.Health;
            public Sprite Icon;

            public void OnBeforeSerialize() => name = StatName.SplitCamelCase();
            public void OnAfterDeserialize() { }
        }

        //[field: SerializeField] public StatRange[] PossibleStatRolls { get; private set; } = new StatRange[System.Enum.GetValues(typeof(StatName)).Length];

        // -> TODO: AllowedStats distribution
        [field: SerializeField] public EquipmentTypeSpecificStatRange[] EquipmentTypeAllowedStats { get; private set; } = new EquipmentTypeSpecificStatRange[System.Enum.GetValues(typeof(EquipmentType)).Length];
        [field: SerializeField] public ConsumableTypeSpecificStatRange[] ConsumableTypeAllowedStats { get; private set; } = new ConsumableTypeSpecificStatRange[System.Enum.GetValues(typeof(ConsumableType)).Length];

        //serializable dictionary
        [field: SerializeField] private StatIcon[] statIcons = new StatIcon[Enum.GetValues(typeof(StatName)).Length];

        public Sprite GetStatIcon(StatName stat) => statIcons.Where(x => x.StatName == stat).FirstOrDefault().Icon;

        /*private void OnValidate()
        {
            //var statNames = System.Enum.GetValues(typeof(StatName)) as StatName[];
            //if (PossibleStatRolls.Length != statNames.Length)
            //    PossibleStatRolls = new StatRange[statNames.Length];
            //
            //for (var i = 0; i < statNames.Length; i++)
            //    PossibleStatRolls[i].StatName = statNames[i];

            var equipmentTypes = System.Enum.GetValues(typeof(EquipmentType)) as EquipmentType[];

            if (EquipmentTypeAllowedStats.Length != equipmentTypes.Length)
                EquipmentTypeAllowedStats = new EquipmentTypeSpecificStatRange[equipmentTypes.Length];

            for (var i = 0; i < equipmentTypes.Length; i++)
                EquipmentTypeAllowedStats[i].EquipmentType = equipmentTypes[i];

            var consumableTypes = System.Enum.GetValues(typeof(ConsumableType)) as ConsumableType[];

            if (ConsumableTypeAllowedStats.Length != consumableTypes.Length)
                ConsumableTypeAllowedStats = new ConsumableTypeSpecificStatRange[consumableTypes.Length];

            for (var i = 0; i < consumableTypes.Length; i++)
                ConsumableTypeAllowedStats[i].ConsumableType = consumableTypes[i];
        }*/

        public StatRange[] GetPossibleStats(EquipmentType equipmentType) => EquipmentTypeAllowedStats.Where(x => x.EquipmentType == equipmentType).FirstOrDefault().StatRanges;
        public StatRange[] GetPossibleStats(ConsumableType consumableType) => ConsumableTypeAllowedStats.Where(x => x.ConsumableType == consumableType).FirstOrDefault().StatRanges;
    }
}
