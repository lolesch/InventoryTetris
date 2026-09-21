using System;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which town side panel is open, as a rule the tests can actually reach (issue #54).
    /// Exactly one <see cref="SidePanelContext"/> is active at a time; <see cref="Set"/>
    /// and <see cref="Clear"/> are both idempotent, so a toggle that re-announces the panel
    /// it already opened - or clears one it never owned - publishes nothing.
    ///
    /// <para>Deliberately a plain class here rather than four members on the
    /// <c>InventoryProvider</c>: the provider compiles into <c>Assembly-CSharp</c>, which no
    /// test assembly definition can reference, so a rule living there is reachable only
    /// through a hand-copied mirror - a test that passes while the real state machine
    /// drifts. In <c>InventorySystem.Containers</c> the rule is engine-free and directly
    /// tested; the provider keeps the public surface (#54, #57) and forwards to it.</para>
    /// </summary>
    public sealed class SidePanelState
    {
        /// <summary>The panel currently open, or <see cref="SidePanelContext.None"/>.</summary>
        public SidePanelContext Active { get; private set; } = SidePanelContext.None;

        /// <summary>
        /// Raised with the new context whenever <see cref="Active"/> actually changes -
        /// never on a no-op set or clear. Clearing publishes <see cref="SidePanelContext.None"/>.
        /// </summary>
        public event Action<SidePanelContext> Changed;

        /// <summary>
        /// Opens <paramref name="context"/>, replacing whatever was open. Setting the
        /// already-active context is a no-op.
        /// </summary>
        public void Set(SidePanelContext context)
        {
            if (Active == context)
                return;

            Active = context;
            Changed?.Invoke(context);
        }

        /// <summary>
        /// Closes <paramref name="context"/>, but only if it is the one currently open -
        /// so a panel that already lost the slot to a sibling cannot close the sibling on
        /// its way out. Clearing from <see cref="SidePanelContext.None"/> is a no-op.
        /// </summary>
        public void Clear(SidePanelContext context)
        {
            if (Active == SidePanelContext.None || Active != context)
                return;

            Active = SidePanelContext.None;
            Changed?.Invoke(SidePanelContext.None);
        }
    }
}
