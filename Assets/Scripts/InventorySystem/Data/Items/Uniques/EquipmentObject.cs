using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Items
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Items/Equipment")]
    public class EquipmentObject : AbstractItemObject
    {
        [field: SerializeField] public EquipmentItem Item { get; protected set; }

        public override AbstractItem GetItem() => Item;
    }
}
