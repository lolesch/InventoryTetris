using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    [System.Serializable]
    public struct StatModifier
    {
        public StatModifier(float value, float duration, /*object origin,*/ StatModifierType type = StatModifierType.FlatAdd)
        {
            this.value = value;
            this.duration = duration;
            //this.origin = origin;
            this.type = type;
        }

        /// <summary>
        /// The stat's value.
        /// </summary>
        public readonly float Value => value;
        [Tooltip("The stat's value.")]
        //[Range(-100, 100)]
        [SerializeField] internal float value;

        /// <summary>
        /// The stat's modifyer type - defines how and in what order it is applied .
        /// </summary>
        public readonly StatModifierType Type => type;
        [Tooltip("The stat's modifyer type - defines how and in what order it is applied.")]
        [SerializeField] internal StatModifierType type;

        /// <summary>
        /// The stat's duration in seconds: -1 = permanent ; 0 = instant ; 60 = 1 minute;
        /// </summary>
        public readonly float Duration => duration;
        [Tooltip("The stat's duration in seconds.\n -1 = permanent, 0 = instant, 60 = 1 minute")]
        [Range(-1, 100)]
        [SerializeField] internal float duration;

        // /// <summary>
        // /// The object that applied the stat.
        // /// </summary>
        // public object Origin => origin;
        // [Tooltip("The object that applied the stat")]
        // [SerializeField] internal object origin;
        // 
        // /// <summary>
        // /// Sets the object that applied the stat.
        // /// </summary>
        // /// <param name="origin"></param>
        // public void SetOrigin(object origin) => this.origin = origin;
    }
}