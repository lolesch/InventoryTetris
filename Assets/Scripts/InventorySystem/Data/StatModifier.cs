using System;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public struct StatModifier
    {
        public StatModifier(float value, uint duration, StatModifierType type = StatModifierType.FlatAdd)
        {
            this.value = value;
            this.duration = duration;
            this.type = type;
        }

        /// <summary>
        /// The stat's value.
        /// </summary>
        public readonly float Value => value;
        [Tooltip("The stat's value.")]
        //[Range(-100, 100)]
        [SerializeField] internal float value;

        // This struct might need to be converted into an abstract class to provide an abstract method "public floar Modify(float previous)"
        // and instead of using the StatModifierType each inheriting class has an int "Order" that serves as the sorting index to apply the modifier
        /// <summary>
        /// The stat's modifyer type - defines how and in what order it is applied .
        /// </summary>
        public readonly StatModifierType Type => type;
        [Tooltip("The stat's modifyer type - defines how and in what order it is applied.")]
        [SerializeField] internal StatModifierType type;

        /// <summary>
        /// The stat's duration in seconds: 0 = instant ; 60 = 1 minute;
        /// </summary>
        public readonly uint Duration => duration;
        [Tooltip("The stat's duration in seconds.\n 0 = instant, 60 = 1 minute")]
        [Range(0, 600)]
        [SerializeField] internal uint duration;

        public int SortByType(StatModifier other) => type.CompareTo(other.type);
    }
}