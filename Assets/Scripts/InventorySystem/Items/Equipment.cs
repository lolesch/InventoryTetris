using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Equipment")]
    public class Equipment : AbstractItem
    {
        // define equipment type to match equipment slots
        [SerializeField] protected internal EquipmentType equipmentType;

        public override void UseItem() => EquipItem();

        private void EquipItem() { }
    }
}