using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// ItemStats will modify the corresponding playerStat on item equip/unequip.
    /// </summary>
    [System.Serializable]
    public struct ItemStat
    {
        public ItemStat(StatName stat, StatModifier modifier)
        {
            this.stat = stat;
            this.modifier = modifier;
        }

        /// <summary>
        /// The identiryer of the stat.
        /// </summary>
        public readonly StatName Stat => stat;
        [Tooltip("The identifyer of the stat.")]
        [SerializeField] internal StatName stat;

        /// <summary>
        /// The stat's values on this item.
        /// </summary>
        public readonly StatModifier Modifier => modifier;
        [Tooltip("The stat's values on this item.")]
        [SerializeField] internal StatModifier modifier;
    }
}