using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [System.Serializable]
    public class PlayerStat : ISerializationCallbackReceiver
    {
        [HideInInspector, SerializeField] private string name;

        [field: SerializeField] public float ModifiedValue { get; private set; }
        [field: SerializeField] public uint BaseValue { get; private set; }

        [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }
        [field: SerializeField] public List<StatModifier> StatModifiers { get; private set; }

        public PlayerStat(StatName statName, uint baseValue = 0)
        {
            Stat = statName;
            BaseValue = baseValue;
            ModifiedValue = BaseValue;
        }

        public void RemoveModifier(StatModifier modifier)
        {
            for (var i = StatModifiers.Count; i-- > 0;)
                if (StatModifiers[i].Equals(modifier))
                {
                    StatModifiers.RemoveAt(i);
                    break;
                }

            CalculateModifiedValue();
        }

        public void AddModifier(StatModifier modifier)
        {
            StatModifiers.Add(modifier);

            CalculateModifiedValue();
        }

        private void CalculateModifiedValue()
        {
            if (StatModifiers == null)
            {
                ModifiedValue = BaseValue;
                return;
            }

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
            {
                ModifiedValue = (float)Math.Round(highestOverride, 4);
                return;
            }

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

            ModifiedValue = (float)Math.Round(result, 4);
        }

        public void OnBeforeSerialize() => name = Stat.ToString();

        public void OnAfterDeserialize() => name = Stat.ToString();
    }
}
