using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// A Town Stop's Side Panel: announces its own <see cref="SidePanelContext"/> the moment it
    /// appears, and clears it the moment it disappears (issue #75) — so "this context is active"
    /// and "this panel is visible" are one fact, not two that used to merely agree.
    ///
    /// <see cref="ToolSmiths.InventorySystem.GUI.Components.Toggles.SidePanelToggle"/> used to
    /// push this announcement from its own <c>OnToggle</c> (#57); the toggle no longer carries a
    /// context at all; only the panel that actually shows or hides knows whether it is showing.
    ///
    /// <see cref="BeforeAppear"/>/<see cref="BeforeDisappear"/> fire synchronously, before the
    /// fade starts — the same timing the old toggle-click announcement had — so a staged Sell
    /// Basket sale cancels the instant the Vendor panel starts leaving, not once its fade
    /// finishes. <see cref="ExclusiveGroup{TMember}.Activate"/> disappears the loser before it
    /// appears the winner, so a handover between two Side Panels still publishes
    /// <see cref="SidePanelContext.None"/> in between, unchanged from #57's contract.
    ///
    /// <para><see cref="RequestContext"/> is the unrelated, beside-it <see cref="InventoryContext"/>
    /// role (issue #84): <see cref="SidePanelToggle"/> calls it from the toggle's own click/hotkey
    /// edge, not from an appear/disappear hook — the opposite direction from this class's own
    /// <see cref="SidePanelContext"/> announcement, and the reason a handover through this new
    /// path never publishes an intermediate <see cref="InventoryContext.None"/> the way the old
    /// one still does.</para>
    ///
    /// <para>Also the Hero Panel's own panel component (issue #84), the same reuse #82 already
    /// gave its toggle (<see cref="SidePanelToggle"/>, not a <c>HeroPanelToggle</c>) rather than
    /// a near-duplicate class: the Hero Panel authors <see cref="context"/> as
    /// <see cref="SidePanelContext.None"/> (it was never a Town Stop) and
    /// <see cref="inventoryContext"/> as <see cref="InventoryContext.Hero"/> — the one
    /// combination <see cref="OnValidate"/> does not warn about.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanel : SimplePanel
    {
        [Tooltip("Which Town Stop context this panel announces. None for the Hero Panel, which " +
                 "was never one.")]
        [SerializeField] private SidePanelContext context = SidePanelContext.None;

        [Tooltip("Which Inventory Context this panel's toggle requests (issue #84). Must not be None.")]
        [SerializeField] private InventoryContext inventoryContext = InventoryContext.None;

        /// <summary>
        /// Play mode only: reading <see cref="AbstractProvider{T}.Instance"/> in the editor
        /// <i>creates</i> a provider GameObject when none exists (issue #46), and
        /// <see cref="Start"/> calling <see cref="SimplePanel.Disappear"/> at edit-adjacent times
        /// can reach this while the player is only editing the scene.
        /// </summary>
        protected override void BeforeAppear()
        {
            base.BeforeAppear();
            Announce(open: true);
        }

        protected override void BeforeDisappear()
        {
            base.BeforeDisappear();
            Announce(open: false);
        }

        private void Announce(bool open)
        {
            if (!Application.isPlaying || context == SidePanelContext.None)
                return;

            var provider = InventoryProvider.Instance;
            if (provider == null)
                return;

            if (open)
                provider.SetSidePanel(context);
            else
                provider.ClearSidePanel(context);
        }

        /// <summary>
        /// The Inventory Context role (issue #84): requested from <see cref="SidePanelToggle"/>'s
        /// own click/hotkey edge, never from <see cref="BeforeAppear"/>/<see cref="BeforeDisappear"/>
        /// - entry points request, panels do not announce this one. A no-op while
        /// <see cref="inventoryContext"/> is <see cref="InventoryContext.None"/>, matching
        /// <see cref="Announce"/>'s guard for the same reason: a panel with no role in one
        /// system must not touch it at all, not even to close it.
        /// </summary>
        public void RequestContext(bool open)
        {
            if (!Application.isPlaying || inventoryContext == InventoryContext.None)
                return;

            var provider = InventoryProvider.Instance;
            if (provider == null)
                return;

            if (open)
                provider.SetContext(inventoryContext);
            else
                provider.CloseContext();
        }

#if UNITY_EDITOR
        /// <summary>The same authored-data check <c>SidePanelToggle.OnValidate</c> used to make
        /// before #75 moved <see cref="context"/> here — a panel left at <see cref="SidePanelContext.None"/>
        /// fades normally but silently never announces. Skipped for the Hero Panel
        /// (<see cref="inventoryContext"/> is <see cref="InventoryContext.Hero"/>): it never had
        /// a <see cref="SidePanelContext"/> to begin with, so <see cref="SidePanelContext.None"/>
        /// there is correct, not a forgotten field.</summary>
        private void OnValidate()
        {
            if (context == SidePanelContext.None && inventoryContext != InventoryContext.Hero)
                Debug.LogWarning($"{name}: SidePanel has no SidePanelContext - it will fade in " +
                                 "and out but never announce it, so a Quick Move will not route " +
                                 "and a staged sale will not cancel.", gameObject);

            if (inventoryContext == InventoryContext.None)
                Debug.LogWarning($"{name}: SidePanel has no InventoryContext - its toggle will " +
                                 "open and close the panel but never request the Inventory " +
                                 "Context (issue #84).", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
