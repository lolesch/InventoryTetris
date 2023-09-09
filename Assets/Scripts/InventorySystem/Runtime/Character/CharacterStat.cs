using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [System.Serializable]
    public class CharacterStat : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private string name;
        [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }

        [field: SerializeField] public uint BaseValue { get; private set; }
        [field: SerializeField] public List<StatModifier> StatModifiers { get; private set; }
        public event Action<float> TotalHasChanged;

        [SerializeField] public float TotalValue => CalculateTotalValue(); //(float)Math.Round(CalculateTotalValue(), 4);
        // [SerializeField] public float BonusValue => TotalValue - BaseValue;

        public CharacterStat(StatName statName, uint baseValue = 0)
        {
            Stat = statName;
            BaseValue = baseValue;
            //ModifiedValue = CalculateModifiedValue();
            name = Stat.SplitCamelCase();
        }

        public void RemoveModifier(StatModifier modifier)
        {
            for (var i = StatModifiers.Count; i-- > 0;)
                if (StatModifiers[i].Equals(modifier))
                {
                    StatModifiers.RemoveAt(i);

                    TotalHasChanged?.Invoke(TotalValue);
                    break;
                }
        }

        public void AddModifier(StatModifier modifier)
        {
            StatModifiers ??= new List<StatModifier>();

            StatModifiers.Add(modifier);

            TotalHasChanged?.Invoke(TotalValue);
        }

        private float CalculateTotalValue()
        {
            if (StatModifiers == null)
                return BaseValue;

            StatModifiers.Sort((x, y) => x.SortByType(y));

            var index = 0;

            /// Overrides
            var highestOverride = 0f;
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
                return highestOverride;// (float)Math.Round(highestOverride, 4);

            var result = (float)BaseValue;

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

            return result;// (float)Math.Round(result, 4);
        }

        public void OnBeforeSerialize() => name = ToString();

        public override string ToString()
        {
            var isPercent = false;

            var statName = Stat.SplitCamelCase();

            if (statName.Contains("Percent"))
            {
                statName = statName.Replace(" Percent", "");
                isPercent = true;
            }

            return $"{statName}: {TotalValue:0.###}{(isPercent ? "%" : "")}";
        }

        public void OnAfterDeserialize() { }

        public CharacterStat GetClone() => (CharacterStat)MemberwiseClone();
    }

    [Serializable]
    public class CharacterResource : CharacterStat
    {
        [field: SerializeField, ReadOnly] public float CurrentValue { get; private set; }

        public CharacterResource(StatName resourceName, uint baseValue = 0) : base(resourceName, baseValue) => CurrentValue = TotalValue;//RecoveryStat = recoveryName;
        public bool IsDepleted => CurrentValue <= 0;
        public float MissingValue => TotalValue - CurrentValue;

        public event Action<float> CurrentHasChanged;
        public event Action CurrentHasDepleted;

        public void AddToCurrent(float value) => SetCurrentValue(CurrentValue + value);
        public void RefillCurrent() => SetCurrentValue(TotalValue);

        private void SetCurrentValue(float value)
        {
            var resultingValue = Mathf.Clamp(value, 0, TotalValue);
            if (CurrentValue != resultingValue)
            {
                CurrentValue = resultingValue;

                CurrentHasChanged?.Invoke(CurrentValue);

                if (IsDepleted)
                    CurrentHasDepleted?.Invoke();
            }
        }
    }
}
