using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Data.Statistics;
using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public class CharacterStat : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private string name;
        [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }

        [SerializeField] private MutableFloat mutableValue;

        // TODO: growth requires CharacterLevel but at the moment CharacterStats dont know their character so make it a funktion(float characterLevel)
        // NO! growth is a statModifier that is applied each levelUp so the Stat doesnt need to know its character

        //[field: SerializeField] public uint GrowthPerLevel { get; private set; }

        public float BaseValue => mutableValue.BaseValue;
        public IReadOnlyList<StatModifier> StatModifiers => mutableValue.Modifiers;
        public float TotalValue => mutableValue;

        public event Action<float> TotalHasChanged
        {
            add => mutableValue.OnTotalChanged += value;
            remove => mutableValue.OnTotalChanged -= value;
        }

        public CharacterStat(StatName statName, float baseValue = 0)
        {
            Stat = statName;
            mutableValue = new MutableFloat(baseValue);
            name = Stat.ToDescription();
        }

        public bool TryRemoveModifier(StatModifier modifier) => mutableValue.TryRemoveModifier(modifier);

        public void AddModifier(StatModifier modifier) => mutableValue.AddModifier(modifier);

        public void OnBeforeSerialize() => name = ToString();

        public override string ToString()
        {
            var isPercent = false;

            var statName = Stat.ToDescription();

            if (statName.Contains("Percent"))
            {
                statName = statName.Replace(" Percent", "");
                isPercent = true;
            }

            return $"{statName}: {TotalValue:0.###}{(isPercent ? "%" : "")}";
        }

        public void OnAfterDeserialize() { }

        public virtual CharacterStat GetDeepCopy()
        {
            var other = (CharacterStat)MemberwiseClone();
            other.name = string.Copy(name);
            other.Stat = Stat;
            other.mutableValue = mutableValue.Clone();

            return other;
        }
    }
}
