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
        //CONTINUE HERE
        // adding the Range for later lookup
        /// <summary>The modifier's range.</summary>
        [Tooltip("The modifier's range.")]
        [field: SerializeField] public Vector2Int Range { get; private set; }

        /// <summary>The modifier's value.</summary>
        [Tooltip("The modifier's value.")]
        [field: SerializeField] public float Value { get; private set; }

        /// <summary>The modifyer type - defines how and in what order it is applied.</summary>
        [Tooltip("The modifyer type - defines how and in what order it is applied.")]
        [field: SerializeField] public StatModifierType Type { get; private set; }

        public StatModifier(Vector2Int range, float value, StatModifierType type)
        {
            Range = range;
            Value = Mathf.Clamp(value, range.x, range.y);
            Type = type;
        }

        /// <summary>
        /// Creates a StatModifier with random Type
        /// </summary>
        /// <param name="value"></param>
        public StatModifier(Vector2Int range, float value) // just for development -> REMOVE ME LATER
        {
            Range = range;
            Value = Mathf.Clamp(value, range.x, range.y);

            var randomType = UnityEngine.Random.Range(0, Enum.GetValues(typeof(StatModifierType)).Length);
            Type = randomType switch
            {
                0 => StatModifierType.Override,
                1 => StatModifierType.FlatAdd,
                2 => StatModifierType.PercentAdd,
                3 => StatModifierType.PercentMult,

                _ => StatModifierType.FlatAdd,
            };
        }

        public int CompareTo(StatModifier other)
        {
            var typeComparison = Type.CompareTo(other.Type);

            return typeComparison != 0 ? typeComparison : Value.CompareTo(other.Value);
        }

        ///// <summary>The stat's duration in seconds: 0 = instant ; 60 = 1 minute;</summary>
        //[Tooltip("The stat's duration in seconds.\n 0 = instant, 60 = 1 minute")]
        //[field: SerializeField] public uint Duration {get; private set;}

        public int SortByType(StatModifier other) => Type.CompareTo(other.Type);

        public override readonly string ToString() => Type switch
        {
            /// wording
            // additional, additive, bonus, 

            StatModifierType.Override => $"{Value:+ #.###;- #.###;#.###} override",
            StatModifierType.FlatAdd => $"{Value:+ #.###;- #.###;#.###} additive",
            StatModifierType.PercentAdd => $"{Value:+ #.###;- #.###;#.###} %",
            StatModifierType.PercentMult => $"{Value:+ #.###;- #.###;#.###} *%",

            _ => $"?? {Value:+ #.###;- #.###;#.###}",
        };
    }
}