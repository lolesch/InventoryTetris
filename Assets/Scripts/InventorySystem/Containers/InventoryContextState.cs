using System;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Inventory Context rule, as a rule the tests can actually reach (issue #83) -
    /// engine-free and below the provider for the same reason #54 gave: the provider compiles
    /// into <c>Assembly-CSharp</c>, which no test assembly can reference.
    ///
    /// <para>With entry points requesting a context rather than panels announcing their own,
    /// nothing needs to name which context it is clearing, so <see cref="Close"/> takes no
    /// argument and always yields <see cref="InventoryContext.None"/> - there is no
    /// per-context clear.</para>
    ///
    /// <para>The rule reaches the scene through two static derivations, <see cref="PanelFor"/>
    /// and <see cref="PanelsFor"/>, so a panel's subscription and a toggle's pressed-visual
    /// resync both read the same statement of "which panels does this context derive" rather
    /// than each re-deriving it (issue #85).</para>
    /// </summary>
    public sealed class InventoryContextState
    {
        /// <summary>The context currently active, or <see cref="InventoryContext.None"/>.</summary>
        public InventoryContext Active { get; private set; } = InventoryContext.None;

        /// <summary>
        /// The panels <see cref="Active"/> derives: the Hero Panel for every non-<c>None</c>
        /// context, plus that context's own Town Stop panel.
        /// </summary>
        public InventoryPanels Panels => PanelsFor(Active);

        /// <summary>
        /// Raised with the new context whenever <see cref="Active"/> actually changes - never
        /// on a no-op <see cref="Set"/>. A handover between two Town Stop contexts fires this
        /// exactly once, with no intermediate <see cref="InventoryContext.None"/>.
        /// </summary>
        public event Action<InventoryContext> Changed;

        /// <summary>
        /// Requests <paramref name="context"/>, replacing whatever was active. Requesting the
        /// already-active context is a no-op.
        /// </summary>
        public void Set(InventoryContext context)
        {
            if (Active == context)
                return;

            Active = context;
            Changed?.Invoke(Active);
        }

        /// <summary>
        /// Closes whatever is active. Closing is always <see cref="InventoryContext.None"/> -
        /// closing a Town Stop's panel closes the Hero Panel with it. A no-op from
        /// <see cref="InventoryContext.None"/>.
        /// </summary>
        public void Close() => Set(InventoryContext.None);

        /// <summary>
        /// Applies a Run-phase change. Only <see cref="InventoryContext.None"/> and
        /// <see cref="InventoryContext.Hero"/> are reachable while <paramref name="inField"/>
        /// - a phase change that leaves <see cref="Active"/> unreachable drops it to
        /// <see cref="InventoryContext.None"/>. Returning to Town reopens nothing on its own.
        /// </summary>
        public void SyncToPhase(bool inField)
        {
            if (inField && !IsReachableInField(Active))
                Close();
        }

        private static bool IsReachableInField(InventoryContext context) =>
            context is InventoryContext.None or InventoryContext.Hero;

        /// <summary>
        /// The one panel a context names for itself: the Hero Panel for
        /// <see cref="InventoryContext.Hero"/>, that Town Stop's own panel otherwise, and
        /// nothing for <see cref="InventoryContext.None"/>. The single statement of "which
        /// panel is mine", which a panel asks about its own authored context and a toggle asks
        /// about the panel it drives (issue #85).
        /// </summary>
        public static InventoryPanels PanelFor(InventoryContext context) => context switch
        {
            InventoryContext.Hero => InventoryPanels.Hero,
            InventoryContext.Stash => InventoryPanels.Stash,
            InventoryContext.Vendor => InventoryPanels.Vendor,
            InventoryContext.Healer => InventoryPanels.Healer,
            _ => InventoryPanels.None,
        };

        /// <summary>
        /// The panels a context derives: <see cref="PanelFor"/> plus the Hero Panel, whose
        /// membership in every non-<c>None</c> context is stated here once rather than repeated
        /// on each Town Stop panel or as a branch inside each one (issue #85).
        /// </summary>
        public static InventoryPanels PanelsFor(InventoryContext context) =>
            context == InventoryContext.None
                ? InventoryPanels.None
                : InventoryPanels.Hero | PanelFor(context);
    }
}
