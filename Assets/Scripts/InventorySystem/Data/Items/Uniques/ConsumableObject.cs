using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Items
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "New Consumable Object", menuName = "Inventory System/Items/Consumable")]
    public class ConsumableObject : AbstractItemObject
    {
        [field: SerializeField] public ConsumableItem Item { get; protected set; }

        public override AbstractItem GetItem() => Item;
    }
}