namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// What a quick-move (shift-click) should do for a (source, context) pair.
    /// <see cref="MoveToContainer"/> is a plain container-to-container move; <see cref="Buy"/>
    /// is produced for the vendor shelf, whose own shift-click is always a buy;
    /// <see cref="Sell"/> (issue #128) is the shift-click sale through <see cref="Sale"/>, the
    /// sink of the Vendor and Healer contexts. Each is a distinct intent so
    /// the resolver's table can grow without the slot displays branching on
    /// containers. <see cref="Acquire"/> is #86's source-to-hub row that must honour
    /// auto-equip - it routes through the player's acquisition entry point
    /// (<see cref="ToolSmiths.InventorySystem.Items.IItemReceiver"/>) instead of a plain move.
    /// <see cref="Drop"/> (issue #63) lays the item on the Run's ground - the Hero context's one
    /// sink, and only while a Run has a ground. <see cref="PickUp"/> takes a Drop off the ground
    /// through the loot flow, which banks a coin stack rather than placing it.
    /// </summary>
    public enum QuickMoveIntentKind
    {
        None = 0,
        MoveToContainer = 1,
        Buy = 3,
        Acquire = 4,
        Sell = 5,
        Drop = 6,
        PickUp = 7,
    }

    /// <summary>
    /// The resolver's answer for one quick-move call. <see cref="Target"/> names the
    /// container a <see cref="MoveToContainer"/> intent sends the item to; it is null for
    /// every other kind.
    /// </summary>
    public readonly struct QuickMoveIntent
    {
        public QuickMoveIntentKind Kind { get; }
        public AbstractDimensionalContainer Target { get; }

        private QuickMoveIntent(QuickMoveIntentKind kind, AbstractDimensionalContainer target)
        {
            Kind = kind;
            Target = target;
        }

        public static QuickMoveIntent None => new(QuickMoveIntentKind.None, null);

        public static QuickMoveIntent MoveTo(AbstractDimensionalContainer target) =>
            new(QuickMoveIntentKind.MoveToContainer, target);

        public static QuickMoveIntent Buy => new(QuickMoveIntentKind.Buy, null);

        public static QuickMoveIntent Acquire => new(QuickMoveIntentKind.Acquire, null);

        public static QuickMoveIntent Sell => new(QuickMoveIntentKind.Sell, null);

        public static QuickMoveIntent Drop => new(QuickMoveIntentKind.Drop, null);

        public static QuickMoveIntent PickUp => new(QuickMoveIntentKind.PickUp, null);
    }
}