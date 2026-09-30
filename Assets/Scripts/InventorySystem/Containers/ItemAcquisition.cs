using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The placement rule behind <see cref="IItemReceiver"/>: auto-equip into an empty slot,
    /// else the Inventory - and nothing behind that. A <c>false</c> means no room, so the
    /// caller decides what that costs (a Buy or Stash retrieval rolls back, a Drop stays on
    /// the ground). Lives in Containers - not on the <c>LocalPlayer</c> MonoBehaviour - so
    /// the rule itself is unit-tested, and the test receivers call it rather than mirror it.
    /// </summary>
    public static class ItemAcquisition
    {
        /// <param name="package">Reduced by whatever was placed; a partial placement leaves the
        /// remainder in it and returns <c>false</c>.</param>
        /// <param name="equipment">Tried first for an Equipment item while its
        /// <see cref="CharacterEquipment.autoEquip"/> is on.</param>
        /// <param name="inventory">Where everything else - and any item the equipment did not
        /// take - goes.</param>
        public static bool TryPlace(ref Package package, CharacterEquipment equipment,
            AbstractDimensionalContainer inventory)
        {
            if (package.Item != null && equipment != null && equipment.autoEquip
                && ItemView.Of(package.Item).Definition.Category == ItemCategory.Equipment
                && equipment.AutoEquip(ref package))
                return true;

            return inventory.TryAddToContainer(ref package);
        }
    }
}
