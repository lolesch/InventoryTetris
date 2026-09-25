namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// A destination that decides for itself how an item is placed - auto-equip, bag
    /// overflow, stash fallback - rather than a plain container add. Lives in
    /// <c>InventorySystem.Items</c> because that's the lowest assembly that has
    /// <see cref="ItemInstance"/>; <c>Package</c> is Containers-resident, so this takes
    /// the item and amount apart instead. Implemented by <c>LocalPlayer</c>.
    /// </summary>
    public interface IItemReceiver
    {
        bool PickUpItem(ItemInstance item, uint amount);
    }
}
