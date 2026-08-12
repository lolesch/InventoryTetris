using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    [Serializable]
    public class CharacterResource : CharacterStat
    {
        [field: SerializeField, ReadOnly] public float CurrentValue { get; private set; }

        public CharacterResource(StatName resourceName, float baseValue) : base(resourceName, baseValue)
        {
            RefillCurrent();
            TotalHasChanged += _ => SetCurrentTo(CurrentValue);
        }

        public bool IsDepleted => CurrentValue <= 0;
        public bool IsFull => CurrentValue >= TotalValue;
        public float MissingValue => TotalValue - CurrentValue;

        public event Action<float, float, float> CurrentHasChanged; // (previous, newValue, total)
        public event Action CurrentHasDepleted;
        public event Action CurrentHasRecharged;

        /// <summary>Tries to add to the amount to the current value.</summary>
        /// <returns>The remaining amount that was not  added</returns>
        public float AddToCurrent(float amountToAdd)
        {
            var added = Math.Min(MissingValue, amountToAdd);

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

        private void SetCurrentTo(float value) => SetCurrentTo(value, TotalValue);

        private void SetCurrentTo(float value, float total)
        {
            var clampedValue = Mathf.Clamp(value, 0, total);

            if (CurrentValue != clampedValue)
            {
                CurrentHasChanged?.Invoke(CurrentValue, clampedValue, total);

                CurrentValue = clampedValue;

                if (IsDepleted)
                    CurrentHasDepleted?.Invoke();
                else if (clampedValue >= total)
                    CurrentHasRecharged?.Invoke();
            }
        }

        public override CharacterStat GetDeepCopy()
        {
            var other = (CharacterResource)base.GetDeepCopy();
            other.CurrentValue = CurrentValue;
            other.CurrentHasChanged = null; //have no listeners to these deep copies

            return other;
        }
    }
}
