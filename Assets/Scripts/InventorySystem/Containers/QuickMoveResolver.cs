namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The pure quick-move table (issue #86, replacing #30's hand-rolled matrix). Maps every
    /// (source container, Inventory Context) pair to one <see cref="QuickMoveIntent"/> through
    /// three rules, shared by every context that has a sink: the hub (the Inventory) goes to
    /// the sink; Equipment goes to the sink in any non-<see cref="InventoryContext.Hero"/>
    /// context; a listed source goes to the hub. By container reference identity - the same
    /// comparison the hand-rolled blocks made ("is Container the inventory / the stash?").
    /// Pure: it names no provider and no GUI, only the containers the caller already holds.
    ///
    /// <para>Wired rows: with the Stash open, the hub is the sink's source and vice versa
    /// (backpack ↔ Stash), and Equipment sends to the Stash; with the Vendor open (issue #33),
    /// backpack and Equipment send to the Sell Basket and a basket Package returns to the
    /// backpack. Retrieving from the Stash is <see cref="QuickMoveIntentKind.Acquire"/>, not a
    /// plain move - a Package that lands back in the Inventory this way must have a chance to
    /// auto-equip (issue #35's entry point), which a plain <c>MoveToContainer</c> never
    /// offered. Every other row is unchanged from #30/#33's matrix. Each Supply shelf (the
    /// Store and the Healer's, issue #121) is checked first, outside the table entirely: a
    /// shelf shift-click is always <see cref="QuickMoveIntentKind.Buy"/>, in every context - a
    /// Supply-local act, not a row (the Vendor row's "Supply" source in #86's design table
    /// names this same exemption, not a second entry).</para>
    ///
    /// <para><see cref="InventoryContext.Hero"/> and <see cref="InventoryContext.Healer"/>
    /// resolve to nothing: the Hero Panel's sink would be Equipment, but that row would
    /// duplicate right-click and has no ticket (#86's stated out-of-scope), and the Healer has
    /// a Supply shelf but no Sell Basket, so it has no sink at all. Both are stated outcomes, not omissions -
    /// neither reaches <see cref="Route"/>.</para>
    /// </summary>
    public static class QuickMoveResolver
    {
        /// <param name="basket">The Sell Basket's grid container - recognized as a quick-move
        /// source so a basket shift-click returns its Package to the backpack.</param>
        public static QuickMoveIntent Resolve(InventoryContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store,
            AbstractDimensionalContainer healerSupply, AbstractDimensionalContainer basket)
        {
            if (source == store || source == healerSupply)
                return QuickMoveIntent.Buy;

            var hub = backpack;

            return context switch
            {
                InventoryContext.Stash => Route(source, hub, equipment,
                    sink: QuickMoveIntent.MoveTo(stash),
                    (stash, QuickMoveIntent.Acquire)),

                InventoryContext.Vendor => Route(source, hub, equipment,
                    sink: QuickMoveIntent.SellBasket,
                    (basket, QuickMoveIntent.MoveTo(hub))),

                _ => QuickMoveIntent.None,
            };
        }

        /// <summary>
        /// The three rules every context with a sink shares: <paramref name="hub"/> and
        /// <paramref name="equipment"/> both go to <paramref name="sink"/>; each
        /// <paramref name="sources"/> entry goes to whatever intent it names (usually the hub,
        /// by <see cref="QuickMoveIntent.MoveTo"/> or <see cref="QuickMoveIntent.Acquire"/>).
        /// A new Town Stop is a new arm above calling this with its own sink and sources - not
        /// a new branch here.
        /// </summary>
        private static QuickMoveIntent Route(AbstractDimensionalContainer source, AbstractDimensionalContainer hub,
            AbstractDimensionalContainer equipment, QuickMoveIntent sink,
            params (AbstractDimensionalContainer source, QuickMoveIntent intent)[] sources)
        {
            if (source == hub || source == equipment)
                return sink;

            foreach (var (candidate, intent) in sources)
                if (source == candidate)
                    return intent;

            return QuickMoveIntent.None;
        }
    }
}
