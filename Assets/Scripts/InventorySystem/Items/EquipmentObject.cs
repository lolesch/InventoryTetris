using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Equipment")]
    public class EquipmentObject : AbstractItemObject
    {
        [field: SerializeField] public EquipmentItem Item { get; protected set; }

        public override AbstractItem GetItem() => Item;
    }
}
