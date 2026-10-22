namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Where a purchase in progress - an item lifted off a Supply or the Sold tab and not yet paid
    /// for - may go (issues #31, #129). The two answers a drag needs, held apart from the cursor
    /// and the displays so they are tested at the container seam: a purchase lands only in the
    /// Hero's bag or on the Equipment, and when it has to be sent back it goes to its own shelf,
    /// never the bag. Anything that is not a purchase is the player's own item and is unrestricted.
    /// </summary>
    public static class PurchaseDrop
    {
        /// <summary>
        /// Whether the held item may land in <paramref name="target"/>. A purchase released
        /// anywhere else - the Stash, another shelf, the world (<c>null</c>) - is cancelled
        /// instead: it goes back free, it is never destroyed and never charged for a place it
        /// does not belong.
        /// </summary>
        public static bool MayLandIn(bool holdingPurchase, AbstractDimensionalContainer target,
            AbstractDimensionalContainer inventory, AbstractDimensionalContainer equipment) =>
            !holdingPurchase || target != null && (target == inventory || target == equipment);

        /// <summary>
        /// The container a cancelled drag falls back to when its exact origin cell is gone. A
        /// purchase falls back to its own shelf: a Ctrl-half pick-up leaves the other half in the
        /// origin cell, and the Hero's bag as the fallback would hand over the unpaid half for
        /// free. The player's own item falls back to the bag.
        /// </summary>
        public static AbstractDimensionalContainer FallbackFor(bool holdingPurchase,
            AbstractDimensionalContainer origin, AbstractDimensionalContainer backpack) =>
            holdingPurchase && origin != null ? origin : backpack;
    }
}
