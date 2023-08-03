using System;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// StatModifier have a type to determine how they apply their modifier value.
    /// </summary>
    [Serializable]
    public struct StatModifier : IComparable<StatModifier>
    {
        public StatModifier(float value, StatModifierType type = StatModifierType.FlatAdd)
        {
            Value = value;
            Type = type;
        }

        /// <summary>The modifier's value.</summary>
        [Tooltip("The modifier's value.")]
        [field: SerializeField] public float Value { get; private set; }

        /// <summary>The modifyer type - defines how and in what order it is applied.</summary>
        [Tooltip("The modifyer type - defines how and in what order it is applied.")]
        [field: SerializeField] public StatModifierType Type { get; private set; }

        public int CompareTo(StatModifier other)
        {
            var typeComparison = Type.CompareTo(other.Type);

            return typeComparison != 0 ? typeComparison : Value.CompareTo(other.Value);
        }

        ///// <summary>The stat's duration in seconds: 0 = instant ; 60 = 1 minute;</summary>
        //[Tooltip("The stat's duration in seconds.\n 0 = instant, 60 = 1 minute")]
        //[field: SerializeField] public uint Duration {get; private set;}

        public int SortByType(StatModifier other) => Type.CompareTo(other.Type);

        public override string ToString() => Type switch
        {
            StatModifierType.Override => $"== {Value:+ #.###;- #.###;#.###}",
            StatModifierType.FlatAdd => $"{Value:+ #.###;- #.###;#.###}",
            StatModifierType.PercentAdd => $"{Value:+ #.###;- #.###;#.###} %",
            StatModifierType.PercentMult => $"* {Value:+ #.###;- #.###;#.###} %",
            _ => $"{Value}",
        };
    }
}