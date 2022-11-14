using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Equipment")]
    public class Equipment : AbstractItemObject
    {
        // define equipment type to match equipment slots
        [SerializeField] protected internal EquipmentType equipmentType;

        //public void Use()
        //{
        //    foreach (var package in InventoryProvider.Instance.PlayerEquipment.storedPackages.Values)
        //        if (package.Item == this)
        //        {
        //            Unequip(new(this));
        //            return;
        //        }
        //
        //    Equip(new(this));
        //}
        //
        //private void Equip(Package package)
        //{
        //    var remaining = InventoryProvider.Instance.PlayerEquipment.AddToContainer(package);
        //
        //    if (0 < remaining.Amount)
        //        InventoryProvider.Instance.PlayerInventory.RemoveFromContainer(package);
        //}
        //
        //private void Unequip(Package package)
        //{
        //    var remaining = InventoryProvider.Instance.PlayerInventory.AddToContainer(package);
        //
        //    if (0 < remaining.Amount)
        //        InventoryProvider.Instance.PlayerEquipment.RemoveFromContainer(package);
        //}
    }
}
