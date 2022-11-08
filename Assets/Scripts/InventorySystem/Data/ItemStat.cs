using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// ItemStats will modify the corresponding playerStat.
    /// </summary>
    [System.Serializable]
    public struct ItemStat
    {
        public ItemStat(StatName stat, StatModifier modifier)
        {
            Stat = stat;
            Modifier = modifier;
        }

        /// <summary>The identiryer of the stat.</summary>
        [Tooltip("The identifyer of the stat.")]
        [field: SerializeField] public StatName Stat { get; private set; }

        /// <summary>The stat's modifier on this item.</summary>
        [Tooltip("The stat's modifier on this item.")]
        [field: SerializeField] public StatModifier Modifier { get; private set; }
    }
}