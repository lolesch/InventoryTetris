using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Equipment")]
    public class Equipment : Item
    {
        // define equipment type to match equipment slots
        [SerializeField] protected internal EquipmentType equipmentType;
    }
}