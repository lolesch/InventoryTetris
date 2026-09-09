namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The pure quick-move matrix (issue #30). Maps every (source container, side panel)
    /// pair to one <see cref="QuickMoveIntent"/>, by container reference identity - the same
    /// comparison the hand-rolled blocks made ("is Container the inventory / the stash?"),
    /// lifted into one tested place. Pure: it names no provider and no GUI, only the
    /// containers the caller already holds.
    ///
    /// Wired rows (this ticket): with the Stash open, backpack ↔ Stash and equipment →
    /// Stash, exactly as before; with neither panel open, nothing moves. The player's
    /// containers in Vendor context return <see cref="QuickMoveIntentKind.None"/> until #33
    /// fills them in once the Sell Basket exists. The vendor shelf's own shift-click is a
    /// <see cref="QuickMoveIntentKind.Buy"/> in every context - buying stays a shelf-local
    /// act.
    /// </summary>
    public static class QuickMoveResolver
    {
        public static QuickMoveIntent Resolve(SidePanelContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store)
        {
            if (source == store)
                return QuickMoveIntent.Buy;

            if (context != SidePanelContext.Stash)
                return QuickMoveIntent.None;

            if (source == backpack)
                return QuickMoveIntent.MoveTo(stash);

            if (source == stash)
                return QuickMoveIntent.MoveTo(backpack);

            if (source == equipment)
                return QuickMoveIntent.MoveTo(stash);

            return QuickMoveIntent.None;
        }
    }
}