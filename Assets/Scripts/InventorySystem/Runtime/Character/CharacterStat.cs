using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    [Serializable]
    public class CharacterStat : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] protected string name;
        [field: SerializeField, HideInInspector] public StatName Stat { get; protected set; }

        [field: SerializeField] public float BaseValue { get; protected set; }

        // [SerializeField] public float BonusValue => TotalValue - BaseValue;

        // TODO: growth requires CharacterLevel but at the moment CharacterStats dont know their character so make it a funktion(float characterLevel)
        // NO! growth is a statModifier that is applied each levelUp so the Stat doesnt need to know its character

        //[field: SerializeField] public uint GrowthPerLevel { get; private set; }

        [field: SerializeField] public List<StatModifier> StatModifiers { get; protected set; } = new List<StatModifier>();
        [field: SerializeField, ReadOnly] public float TotalValue { get; protected set; }

        public event Action<float> TotalHasChanged;

        public CharacterStat(StatName statName, float baseValue)
        {
            Stat = statName;
            name = Stat.SplitCamelCase();

            SetBaseTo(baseValue);
        }

        public bool TryRemoveModifier(StatModifier modifier)
        {
            for (var i = StatModifiers.Count; i-- > 0;)
                if (StatModifiers[i].Equals(modifier))
                {
                    StatModifiers.RemoveAt(i);

                    CalculateTotalValue();
                    return true;
                }
            return false;
        }

        public void AddModifier(StatModifier modifier)
        {
            StatModifiers ??= new List<StatModifier>();

            StatModifiers.Add(modifier);

            if (Debug.isDebugBuild)
                StatModifiers.Sort((x, y) => x.SortByType(y));

            CalculateTotalValue();
        }

        public float CompareModifiers(StatModifier current, StatModifier other)
        {
            if (current.Value == other.Value)
                return 0;

            var clonedStat = GetDeepCopy();
            var clonedStat2 = clonedStat.GetDeepCopy();

            for (var i = clonedStat.StatModifiers.Count; i-- > 0;)
                if (clonedStat.StatModifiers[i].Equals(current))
                {
                    clonedStat.StatModifiers.RemoveAt(i);
                    clonedStat.StatModifiers.Add(other);

                    if (Debug.isDebugBuild)
                        clonedStat.StatModifiers.Sort((x, y) => x.SortByType(y));
                }

            for (var i = clonedStat2.StatModifiers.Count; i-- > 0;)
                if (clonedStat2.StatModifiers[i].Equals(other))
                {
                    clonedStat2.StatModifiers.RemoveAt(i);
                    clonedStat2.StatModifiers.Add(current);

                    if (Debug.isDebugBuild)
                        clonedStat2.StatModifiers.Sort((x, y) => x.SortByType(y));
                }

            clonedStat.CalculateTotalValue();
            clonedStat2.CalculateTotalValue();

            return clonedStat2.TotalValue - clonedStat.TotalValue;
        }

        public virtual void CalculateTotalValue()
        {
            //var levelUps = characterLevel - 1;
            var result = BaseValue;// + GrowthPerLevel * levelUps;

            ApplyModifiers(ref result);

            if (result != TotalValue)
            {
                TotalValue = result; // (float)Math.Round(result, 4);
                TotalHasChanged?.Invoke(TotalValue);
            }

            void ApplyModifiers(ref float result)
            {
                StatModifiers ??= new List<StatModifier>();

                if (0 < StatModifiers.Count)
                {
                    var overwriteMods = StatModifiers.Where(x => x.Type == StatModifierType.Overwrite).OrderByDescending(x => x.Value);
                    if (overwriteMods.Any())
                        result = overwriteMods.FirstOrDefault().Value;
                    else
                    {
                        var flatAddModValue = StatModifiers.Where(x => x.Type == StatModifierType.FlatAdd).Sum(x => x.Value);
                        result += flatAddModValue;

                        var percentAddModValue = StatModifiers.Where(x => x.Type == StatModifierType.PercentAdd).Sum(x => x.Value / 100);
                        result *= 1 + percentAddModValue;

                        var percentMultMods = StatModifiers.Where(x => x.Type == StatModifierType.PercentMult);
                        foreach (var mod in percentMultMods)
                            result *= 1 + mod.Value / 100;
                    }
                }
            }
        }

        public void OnBeforeSerialize() => name = ToString();

        public override string ToString()
        {
            //var isPercent = false;

            var statName = Stat.SplitCamelCase();

            if (statName.Contains("Percent"))
            {
                statName = statName.Replace(" Percent", "%");
                //isPercent = true;
            }
            //CalculateTotalValue();

            return statName;//{TotalValue:0.###}{(isPercent ? "%" : "")}";
        }

        public void OnAfterDeserialize() { }

        private void SetBaseTo(float newValue)
        {
            BaseValue = newValue;
            CalculateTotalValue();
        }

        public virtual CharacterStat GetDeepCopy()
        {
            var other = (CharacterStat)MemberwiseClone();
            other.name = string.Copy(name);
            other.Stat = Stat;
            other.BaseValue = BaseValue;
            other.StatModifiers = new List<StatModifier>(StatModifiers);
            other.TotalHasChanged = null; //have no listeners to these deep copies
            other.TotalValue = 0;
            other.CalculateTotalValue();

            return other;
        }
    }
}
