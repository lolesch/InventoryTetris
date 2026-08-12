using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Statistics
{
    [Serializable]
    public sealed class MutableFloat : IFormattable
    {
        [SerializeField, ReadOnly] private float totalValue;
        [SerializeField, ReadOnly] private float baseValue;
        [SerializeField, ReadOnly] private List<StatModifier> modifiers;

        public MutableFloat(float baseValue)
        {
            this.baseValue = baseValue;
            totalValue = baseValue;
            modifiers = new List<StatModifier>();
            OnTotalChanged = null;
        }

        public static implicit operator float(MutableFloat mutableFloat) => mutableFloat!.totalValue;

        public event Action<float> OnTotalChanged;

        public float BaseValue => baseValue;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;

        /// <summary>
        /// An independent copy: a fresh modifier list and no subscribers. Mutating the clone (e.g.
        /// removing one modifier to answer "what would this be without it?") never touches this
        /// instance and never fires this instance's <see cref="OnTotalChanged"/>.
        /// </summary>
        public MutableFloat Clone()
        {
            var clone = new MutableFloat(baseValue) { modifiers = new List<StatModifier>(modifiers) };
            clone.CalculateTotalValue();
            return clone;
        }

        public void AddModifier(StatModifier modifier)
        {
            modifiers.Add(modifier);
            CalculateTotalValue();
        }

        public bool TryRemoveModifier(StatModifier modifier) => TryRemoveModifier(modifier, warnIfMissing: true);

        /// <summary>
        /// A caller that expects the modifier might already be gone — e.g. probing a clone — can
        /// pass <paramref name="warnIfMissing"/> false to skip the warning instead of spamming the
        /// console on an expected miss.
        /// </summary>
        public bool TryRemoveModifier(StatModifier modifier, bool warnIfMissing)
        {
            for (var i = modifiers.Count; i-- > 0;)
                if (modifiers[i].Equals(modifier))
                {
                    modifiers.RemoveAt(i);

                    CalculateTotalValue();
                    return true;
                }

            if (warnIfMissing)
                Debug.LogWarning($"Modifier {modifier} not found");
            return false;
        }

        public string ToString(string format) => totalValue.ToString(format);
        public string ToString(string format, IFormatProvider provider) => totalValue.ToString(format, provider);

        private void CalculateTotalValue()
        {
            ApplyModifiers(out var newTotal);

            if (Mathf.Approximately(totalValue, newTotal))
                return;

            totalValue = newTotal;
            OnTotalChanged?.Invoke(totalValue);
        }

        private void ApplyModifiers(out float newTotal)
        {
            newTotal = baseValue;
            if (!modifiers.Any())
                return;

            var overwriteMods = modifiers.Where(x => x.Type == StatModifierType.Overwrite)
                .OrderByDescending(x => x.Value);
            if (overwriteMods.Any())
            {
                newTotal = overwriteMods.FirstOrDefault().Value;
                return;
            }

            var flatAddModValue = modifiers.Where(x => x.Type == StatModifierType.FlatAdd).Sum(x => x.Value);
            newTotal += flatAddModValue;

            var percentAddModValue = modifiers.Where(x => x.Type == StatModifierType.PercentAdd).Sum(x => x.Value / 100f);
            newTotal *= 1 + percentAddModValue;

            var percentMultMods = modifiers.Where(x => x.Type == StatModifierType.PercentMult);
            newTotal = percentMultMods.Aggregate(newTotal, (current, mod) => current * (1 + mod.Value / 100f));
        }
    }
}
