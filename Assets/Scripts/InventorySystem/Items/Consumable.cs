using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Consumable Object", menuName = "Inventory System/Consumable")]
    public class Consumable : AbstractItem
    {
        [SerializeField] protected internal ConsumableType consumableType;

        public override void UseItem() => ConsumeItem();

        private void ConsumeItem() { }
    }
}