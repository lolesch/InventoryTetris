using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Displays;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public struct EquipmentSlot
    {
        public EquipmentSlot(EquipmentSlotDisplay display, Vector2Int dimensions)
        {
            Display = display;
            Dimensions = dimensions;
            AllowedTypes = new List<Equipment.EquipmentType>(1);
        }

        [SerializeField] internal EquipmentSlotDisplay Display;
        [SerializeField] internal Vector2Int Dimensions;
        [SerializeField] internal List<Equipment.EquipmentType> AllowedTypes;
    }
}