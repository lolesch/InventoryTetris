﻿using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public class CharacterStat : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private string name;
        [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }

        [field: SerializeField] public float BaseValue { get; private set; }

        // TODO: growth requires CharacterLevel but at the moment CharacterStats dont know their character so make it a funktion(float characterLevel)
        //[field: SerializeField] public uint GrowthPerLevel { get; private set; }

        [field: SerializeField] public List<StatModifier> StatModifiers { get; private set; }
        public event Action<float> TotalHasChanged;

        [SerializeField] public float TotalValue => CalculateTotalValue(); // only recalculate when adding/removing mods and store that value for lookups?
        // [SerializeField] public float BonusValue => TotalValue - BaseValue;

        public CharacterStat(StatName statName, float baseValue = 0)
        {
            Stat = statName;
            BaseValue = baseValue;
            //ModifiedValue = CalculateModifiedValue();
            name = Stat.SplitCamelCase();
        }

        public bool TryRemoveModifier(StatModifier modifier)
        {
            for (var i = StatModifiers.Count; i-- > 0;)
                if (StatModifiers[i].Equals(modifier))
                {
                    StatModifiers.RemoveAt(i);

                    TotalHasChanged?.Invoke(TotalValue);
                    return true;
                    break;
                }
            return false;
        }

        public void AddModifier(StatModifier modifier)
        {
            StatModifiers ??= new List<StatModifier>();

            StatModifiers.Add(modifier);

            TotalHasChanged?.Invoke(TotalValue);
        }

        private float CalculateTotalValue() //TODO: improve by using linQ => see CharacterStatsDisplay
        {
            //var levelUps = characterLevel - 1;
            var result = BaseValue;// + GrowthPerLevel * characterLevel; // flat increase

            if (StatModifiers == null || StatModifiers.Count == 0)
                return result;

            StatModifiers.Sort((x, y) => x.SortByType(y));

            var index = 0; // used to skip to the desired type

            #region Overrides
            var highestOverride = 0f;
            var hasOverrides = false;

            for (var i = index; i < StatModifiers.Count; i++)
                if (StatModifiers[i].Type == StatModifierType.Overwrite)
                {
                    index++;
                    hasOverrides = true;

                    if (highestOverride < StatModifiers[i].Value)
                        highestOverride = StatModifiers[i].Value;
                }

            if (hasOverrides)
                return highestOverride;// (float)Math.Round(highestOverride, 4);
            #endregion Overrides

            #region FlatAdd
            for (var i = index; i < StatModifiers.Count; i++)
                if (StatModifiers[i].Type == StatModifierType.FlatAdd)
                {
                    index++;
                    result += StatModifiers[i].Value;
                }
            #endregion FlatAdd

            #region PercentAdd
            var sumPercentAdd = 0f;
            for (var i = index; i < StatModifiers.Count; i++)
                if (StatModifiers[i].Type == StatModifierType.PercentAdd)
                {
                    index++;
                    sumPercentAdd += StatModifiers[i].Value / 100;
                }
            result *= 1 + sumPercentAdd;
            #endregion PercentAdd

            #region PercentMult
            for (var i = index; i < StatModifiers.Count; i++, index++)
                if (StatModifiers[i].Type == StatModifierType.PercentMult)
                    result *= 1 + StatModifiers[i].Value / 100;
            #endregion PercentMult

            return result; // (float)Math.Round(result, 4);
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

        public CharacterStat GetShallowCopy() => (CharacterStat)MemberwiseClone();
        public CharacterStat GetDeepCopy()
        {
            var other = (CharacterStat)MemberwiseClone();
            other.name = string.Copy(name);
            other.Stat = Stat;
            other.BaseValue = BaseValue;
            other.StatModifiers = new List<StatModifier>(StatModifiers);

            return other;
        }
    }

    [Serializable]
    public class CharacterResource : CharacterStat
    {
        [field: SerializeField, ReadOnly] public float CurrentValue { get; private set; }

        public CharacterResource(StatName resourceName, uint baseValue = 0) : base(resourceName, baseValue) => CurrentValue = TotalValue;

        public bool IsDepleted => CurrentValue <= 0;
        public bool IsFull => CurrentValue == TotalValue;
        public float MissingValue => TotalValue - CurrentValue;

        public event Action<float, float, float> CurrentHasChanged;
        public event Action CurrentHasDepleted;
        public event Action CurrentHasRecharged;

        /// <summary>Tries to add to the amount to the current value.</summary>
        /// <returns>The remaining amount that was not  added</returns>
        public float AddToCurrent(float amountToAdd)
        {
            var added = Math.Min(TotalValue - CurrentValue, amountToAdd);

            if (added != 0)
                SetCurrentTo(CurrentValue + added);

            return amountToAdd - added;
        }

        /// <summary>Tries to remove the amount from the current value</summary>
        /// <returns>The remaining amount that was not removed</returns>
        public float RemoveFromCurrent(float amountToRemove)
        {
            var removed = Math.Min(CurrentValue, amountToRemove);

            if (removed != 0)
                SetCurrentTo(CurrentValue - removed);

            return amountToRemove - removed;
        }

        public void RefillCurrent() => SetCurrentTo(TotalValue);
        public void DepleteCurrent() => SetCurrentTo(0);

        private void SetCurrentTo(float value)
        {
            var resultingValue = Mathf.Clamp(value, 0, TotalValue);

            if (CurrentValue != resultingValue)
            {
                CurrentHasChanged?.Invoke(CurrentValue, resultingValue, TotalValue);

                CurrentValue = resultingValue;

                if (IsDepleted)
                    CurrentHasDepleted?.Invoke();
                else if (IsFull)
                    CurrentHasRecharged?.Invoke();
            }
        }
    }
}
