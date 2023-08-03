using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [System.Serializable]
    public class PlayerStat : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private string name;
        [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }

        [field: SerializeField] public uint BaseValue { get; private set; }
        [field: SerializeField] public List<StatModifier> StatModifiers { get; private set; }
        [field: SerializeField, ReadOnly] public float ModifiedValue { get; private set; }

        public PlayerStat(StatName statName, uint baseValue = 0)
        {
            Stat = statName;
            BaseValue = baseValue;
            ModifiedValue = CalculateModifiedValue();
        }

        public void RemoveModifier(StatModifier modifier)
        {
            for (var i = StatModifiers.Count; i-- > 0;)
                if (StatModifiers[i].Equals(modifier))
                {
                    StatModifiers.RemoveAt(i);
                    break;
                }

            ModifiedValue = CalculateModifiedValue();
        }

        public void AddModifier(StatModifier modifier)
        {
            StatModifiers.Add(modifier);

            ModifiedValue = CalculateModifiedValue();
        }

        private float CalculateModifiedValue()
        {
            if (StatModifiers == null)
                return BaseValue;

            StatModifiers.Sort((x, y) => x.SortByType(y));

            var result = (float)BaseValue;
            var index = 0;

            /// Overrides
            var highestOverride = result;
            var hasOverrides = false;

            for (var i = index; i < StatModifiers.Count; i++)
                if (StatModifiers[i].Type == StatModifierType.Override)
                {
                    index++;
                    hasOverrides = true;

                    if (highestOverride < StatModifiers[i].Value)
                        highestOverride = StatModifiers[i].Value;
                }

            if (hasOverrides)
                return (float)Math.Round(highestOverride, 4);

            /// FlatAdd
            for (var i = index; i < StatModifiers.Count; i++)
                if (StatModifiers[i].Type == StatModifierType.FlatAdd)
                {
                    index++;
                    result += StatModifiers[i].Value;
                }

            /// PercentAdd
            var sumPercentAdd = 0f;
            for (var i = index; i < StatModifiers.Count; i++)
                if (StatModifiers[i].Type == StatModifierType.PercentAdd)
                {
                    index++;
                    sumPercentAdd += StatModifiers[i].Value / 100;
                }
            result *= 1 + sumPercentAdd;

            /// PercentMult
            for (var i = index; i < StatModifiers.Count; i++, index++)
                if (StatModifiers[i].Type == StatModifierType.PercentMult)
                    result *= 1 + StatModifiers[i].Value / 100;

            return result;
        }

        public void OnBeforeSerialize()
        {
            name = Stat.SplitCamelCase();
            ModifiedValue = CalculateModifiedValue();
        }

        public override string ToString() => $"{Stat.SplitCamelCase()}: {ModifiedValue:0.###}";

        public void OnAfterDeserialize()
        {
            //name = Stat.ToString();
            //ModifiedValue = CalculateModifiedValue();
        }
    }
}
