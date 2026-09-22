namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The pure quick-move matrix (issue #30). Maps every (source container, Inventory Context)
    /// pair to one <see cref="QuickMoveIntent"/>, by container reference identity - the same
    /// comparison the hand-rolled blocks made ("is Container the inventory / the stash?"),
    /// lifted into one tested place. Pure: it names no provider and no GUI, only the
    /// containers the caller already holds.
    ///
    /// Wired rows: with the Stash open, backpack ↔ Stash and equipment → Stash; with the
    /// Vendor open (issue #33), backpack and equipment shift-clicks send to the Sell Basket
    /// and a basket Package returns to the backpack; with neither panel open, nothing moves.
    /// The vendor shelf's own shift-click is a <see cref="QuickMoveIntentKind.Buy"/> in every
    /// context - buying stays a shelf-local act.
    ///
    /// <para>The context replaced <c>SidePanelContext</c> in #85, when the panels stopped
    /// announcing a side-panel context and the Inventory Context became the only record of
    /// which panels are up. <see cref="InventoryContext.Hero"/> and
    /// <see cref="InventoryContext.Healer"/> resolve to nothing here: the Hero Panel's sink is
    /// Equipment, but that row would duplicate right-click and has no ticket, and the Healer
    /// has no containers yet, so it has no sink at all. Both are stated outcomes, not
    /// omissions. The per-context sink/source table that replaces this matrix is #86.</para>
    /// </summary>
    public static class QuickMoveResolver
    {
        /// <param name="basket">The Sell Basket's grid container - recognized as a quick-move
        /// source so a basket shift-click returns its Package to the backpack.</param>
        public static QuickMoveIntent Resolve(InventoryContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store,
            AbstractDimensionalContainer basket)
        {
            if (source == store)
                return QuickMoveIntent.Buy;

            switch (context)
            {
                case InventoryContext.Stash:
                    if (source == backpack)
                        return QuickMoveIntent.MoveTo(stash);
                    if (source == stash)
                        return QuickMoveIntent.MoveTo(backpack);
                    if (source == equipment)
                        return QuickMoveIntent.MoveTo(stash);
                    break;

                case InventoryContext.Vendor:
                    if (source == basket)
                        return QuickMoveIntent.MoveTo(backpack);
                    if (source == backpack || source == equipment)
                        return QuickMoveIntent.SellBasket;
                    break;
            }

            return QuickMoveIntent.None;
        }
    }
}