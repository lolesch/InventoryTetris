using System.Collections.Generic;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Equipment")]
    public class Equipment : Item
    {
        [System.Serializable]
        /// The identifier of equipment slots
        public enum EquipmentType
        {
            None = 0,
            Boots = 1,
            Pants = 2,
            Belt = 3,
            Chest = 4,
            Helm = 5,
            Gloves = 6,
            Bracers = 7,
            Shoulders = 8,
            Ring = 9,
            Amulet = 10,
            MainHand = 11,
            Offhand = 12,
            Weapon_2H = 13,
        }

        // define equipment type to match equipment slots
        [SerializeField] protected internal EquipmentType equipmentType;
    }
}