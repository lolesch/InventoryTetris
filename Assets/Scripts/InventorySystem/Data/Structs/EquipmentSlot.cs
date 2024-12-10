using System;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    internal struct EquipmentSlot
    {
        public EquipmentSlot( EquipmentSlotDisplay display, Vector2Int dimensions )
        {
            Display = display;
            Dimensions = dimensions;
        }

        [SerializeField] internal EquipmentSlotDisplay Display;
        [SerializeField] internal Vector2Int Dimensions;
    }
}
