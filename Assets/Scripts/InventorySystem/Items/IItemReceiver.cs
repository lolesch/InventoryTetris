namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// A destination that decides for itself how an item is placed - auto-equip, else the
    /// bag - rather than a plain container add. <c>false</c> means nothing had room and
    /// nothing moved; there is no overflow destination behind it, so a caller can leave the
    /// item where it was (a Buy or Stash retrieval rolls back, a Drop stays on the ground).
    /// Lives in
    /// <c>InventorySystem.Items</c> because that's the lowest assembly that has
    /// <see cref="ItemInstance"/>; <c>Package</c> is Containers-resident, so this takes
    /// the item and amount apart instead. Implemented by <c>LocalPlayer</c>.
    /// </summary>
    public interface IItemReceiver
    {
        bool PickUpItem(ItemInstance item, uint amount);
    }
}
