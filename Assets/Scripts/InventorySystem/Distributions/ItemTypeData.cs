﻿using System;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Extensions;
using UnityEngine;

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
            [SerializeField] private Vector2Int MinMax;

            [SerializeField] public AnimationCurve Distribution;

            public StatRange(StatName statName, Vector2Int minMax)
            {
                StatName = statName;
                MinMax = minMax;

                name = StatName.ToString();
            }

            public (Vector2Int MinMax, float value) GetRandomRoll(ItemRarity rarity)
            {
                /// ITEM AFFIX DESIGN BY RARITY:
                /// 
                /// the lesser the rarity, the higher the max affix roll => Common items can roll the highest stats => good base for crafting
                /// the higher the rarity, the higher the min affix roll => Unique items roll with usefull affix values

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

                // TODO modified affix range can result in higher min than max values => reorder before setting the range

                /// the higher the rarity, the higher the min affix roll => Unique items roll with usefull affix values
                var min = Mathf.CeilToInt(MinMax.x * modifier);

                /// the lesser the rarity, the higher the max affix roll => Common items can roll the highest stats => good base for crafting
                var max = Mathf.CeilToInt(MinMax.y * (1f + (1f - modifier)));

                var mappedValue = weightedRoll.MapFrom01(min - 1, max);
                var value = Mathf.Max(min, Mathf.CeilToInt(mappedValue));

                return (new Vector2Int(min, max), value);
            }

            public void OnBeforeSerialize() => name = $"{StatName}\t{MinMax}";

            public void OnAfterDeserialize() { }
        }

        [Serializable]
        public class EquipmentTypeSpecificStatRange : ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector] public string name;
            [SerializeField, HideInInspector] public EquipmentType EquipmentType;
            [SerializeField] public StatRange[] StatRanges;

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

        //[field: SerializeField] public StatRange[] PossibleStatRolls { get; private set; } = new StatRange[System.Enum.GetValues(typeof(StatName)).Length];

        // -> TODO: AllowedStats distribution
        [field: SerializeField] public EquipmentTypeSpecificStatRange[] EquipmentTypeAllowedStats { get; private set; } = new EquipmentTypeSpecificStatRange[System.Enum.GetValues(typeof(EquipmentType)).Length];
        [field: SerializeField] public ConsumableTypeSpecificStatRange[] ConsumableTypeAllowedStats { get; private set; } = new ConsumableTypeSpecificStatRange[System.Enum.GetValues(typeof(ConsumableType)).Length];

        private void OnValidate()
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
        }

        public StatRange[] GetPossibleStats(EquipmentType equipmentType) => EquipmentTypeAllowedStats.Where(x => x.EquipmentType == equipmentType).FirstOrDefault().StatRanges;
        public StatRange[] GetPossibleStats(ConsumableType consumableType) => ConsumableTypeAllowedStats.Where(x => x.ConsumableType == consumableType).FirstOrDefault().StatRanges;
    }
}