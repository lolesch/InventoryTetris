using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "New Consumable Object", menuName = "Inventory System/Consumable")]
    public class ConsumableObject : AbstractItemObject
    {
        [field: SerializeField] public ConsumableItem Item { get; protected set; }

        public override AbstractItem GetItem() => Item;
    }
}