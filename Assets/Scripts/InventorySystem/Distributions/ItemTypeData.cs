using System;
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
            [SerializeField] public Vector2Int MinMax;

            [SerializeField] public AnimationCurve Distribution;

            public StatRange(StatName statName, Vector2Int minMax)
            {
                StatName = statName;
                MinMax = minMax;

                name = StatName.ToString();
            }

            public StatModifier GetRandomRoll(float modifier = 1)
            {
                var randomRoll = UnityEngine.Random.Range(0.0f, 1.0f);
                var weightedRoll = Distribution.Evaluate(randomRoll);
                var mapping = weightedRoll.MapFrom01((MinMax.x - 1) * modifier, MinMax.y * modifier);

                if (mapping == (MinMax.x - 1) * modifier)
                { } // TODO exclude int out of range

                var roll = Mathf.CeilToInt(mapping);

                var statModifier = new StatModifier(roll, StatModifierType.FlatAdd); // TODO: determine statModType => lookup table for each statName
                                                                                     // => rework the statModifier to derive the type from the name?
                return statModifier;
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
