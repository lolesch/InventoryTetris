namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// What a quick-move (shift-click) should do for a (source, context) pair. Only
    /// <see cref="None"/> and <see cref="MoveToContainer"/> are produced by this ticket
    /// (#30); <see cref="Buy"/> is produced for the vendor shelf, whose own shift-click is
    /// always a buy. <see cref="SellBasket"/> is the seam #33 fills in once the basket
    /// exists - it is a distinct intent so the resolver's matrix can grow without the slot
    /// displays branching on containers.
    /// </summary>
    public enum QuickMoveIntentKind
    {
        None = 0,
        MoveToContainer = 1,
        SellBasket = 2,
        Buy = 3,
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

        public static QuickMoveIntent SellBasket => new(QuickMoveIntentKind.SellBasket, null);

        public static QuickMoveIntent Buy => new(QuickMoveIntentKind.Buy, null);
    }
}