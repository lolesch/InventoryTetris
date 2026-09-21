using System;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Inventory Context rule, as a rule the tests can actually reach (issue #83) -
    /// engine-free and below the provider for the same reason <see cref="SidePanelState"/>
    /// is (#54): the provider compiles into <c>Assembly-CSharp</c>, which no test assembly
    /// can reference.
    ///
    /// <para>Simpler than <see cref="SidePanelState"/> in one respect: with entry points
    /// requesting a context rather than panels announcing their own, nothing needs to name
    /// which context it is clearing, so <see cref="Close"/> takes no argument and always
    /// yields <see cref="InventoryContext.None"/> - there is no per-context clear.</para>
    /// </summary>
    public sealed class InventoryContextState
    {
        /// <summary>The context currently active, or <see cref="InventoryContext.None"/>.</summary>
        public InventoryContext Active { get; private set; } = InventoryContext.None;

        /// <summary>
        /// The panels <see cref="Active"/> derives: the Hero Panel for every non-<c>None</c>
        /// context, plus that context's own Town Stop panel.
        /// </summary>
        public InventoryPanels Panels => ToPanels(Active);

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

        private static InventoryPanels ToPanels(InventoryContext context) => context switch
        {
            InventoryContext.None => InventoryPanels.None,
            InventoryContext.Hero => InventoryPanels.Hero,
            InventoryContext.Stash => InventoryPanels.Hero | InventoryPanels.Stash,
            InventoryContext.Vendor => InventoryPanels.Hero | InventoryPanels.Vendor,
            InventoryContext.Healer => InventoryPanels.Hero | InventoryPanels.Healer,
            _ => InventoryPanels.None,
        };
    }
}
