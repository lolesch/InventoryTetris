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
    /// (backpack ↔ Stash), and Equipment sends to the Stash; with the Vendor or the Healer open
    /// (issues #33, #121 - both selling Town Stops), backpack and Equipment are sold through the
    /// <see cref="Sale"/> (issue #128; it replaced staging into the Sell Basket, deleted in #131).
    /// Retrieving from the Stash is <see cref="QuickMoveIntentKind.Acquire"/>, not a
    /// plain move - a Package that lands back in the Inventory this way must have a chance to
    /// auto-equip (issue #35's entry point), which a plain <c>MoveToContainer</c> never
    /// offered. Every other row is unchanged from #30/#33's matrix. Each Supply shelf (the
    /// Store and the Healer's, issue #121) is checked first, outside the table entirely: a
    /// shelf shift-click is always <see cref="QuickMoveIntentKind.Buy"/>, in every context - a
    /// Supply-local act, not a row (the Vendor row's "Supply" source in #86's design table
    /// names this same exemption, not a second entry). The Sold container (issue #126) is one
    /// more shelf in that check: what the player sold is bought back like any Supply.</para>
    ///
    /// <para><see cref="InventoryContext.Hero"/> has no Town Stop, so no sink of its own: the Hero
    /// Panel's sink would be Equipment, but that row would duplicate right-click and has no ticket
    /// (#86's stated out-of-scope). Its one row is the ground (issue #63): while a Run has a
    /// ground, a shift-click on the backpack drops the item there. Equipment stays out of it -
    /// rule two's Hero exemption holds, so a stray shift-click never unequips to the dirt. With
    /// no ground (Town) the Hero context resolves to nothing, a stated outcome, not an omission.
    /// It never reaches <see cref="Route"/>. <see cref="InventoryContext.Ground"/> has the same drop
    /// row and one more: a shift-click on the ground itself picks the Drop up, through the loot flow.</para>
    /// </summary>
    public static class QuickMoveResolver
    {
        /// <param name="sold">The Sold container (issue #126) - a Supply, so a shift-click on it
        /// is a Buy in every context.</param>
        /// <param name="ground">The Run's ground, the source of the Ground context's one pick-up row.</param>
        /// <param name="groundOpen">Whether a Run has a ground to drop on (issue #63): true in the
        /// Field, false in Town.</param>
        public static QuickMoveIntent Resolve(InventoryContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store,
            AbstractDimensionalContainer healerSupply, AbstractDimensionalContainer sold,
            AbstractDimensionalContainer ground, bool groundOpen = false)
        {
            if (source == store || source == healerSupply || source == sold)
                return QuickMoveIntent.Buy;

            var hub = backpack;

            return context switch
            {
                InventoryContext.Hero or InventoryContext.Ground when groundOpen && source == hub => QuickMoveIntent.Drop,

                InventoryContext.Ground when groundOpen && source == ground => QuickMoveIntent.PickUp,

                InventoryContext.Stash => Route(source, hub, equipment,
                    sink: QuickMoveIntent.MoveTo(stash),
                    (stash, QuickMoveIntent.Acquire)),

                InventoryContext.Vendor or InventoryContext.Healer => Route(source, hub, equipment,
                    sink: QuickMoveIntent.Sell),

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
