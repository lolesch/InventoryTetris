﻿using System;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>PlayerStatModifiers will modify the corresponding playerStat.</summary>
    [Serializable]
    public struct CharacterStatModifier : IComparable<CharacterStatModifier>
    {
        /// <summary>The identiryer of the stat.</summary>
        [Tooltip("The identifyer of the stat.")]
        [field: SerializeField] public StatName Stat { get; private set; }

        /// <summary>The stat's modifier on this item.</summary>
        [Tooltip("The stat's modifier on this item.")]
        [field: SerializeField] public StatModifier Modifier { get; private set; }

        /// <summary>PlayerStatModifiers will modify the corresponding playerStat.</summary>
        public CharacterStatModifier(StatName stat, StatModifier modifier)
        {
            Stat = stat;
            Modifier = modifier;
        }

        public int CompareTo(CharacterStatModifier other) => Stat.CompareTo(other.Stat); // then by modifyer
    }
}