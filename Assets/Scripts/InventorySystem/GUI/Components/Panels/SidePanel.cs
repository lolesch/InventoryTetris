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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanel : SimplePanel
    {
        [Tooltip("Which Town Stop context this panel announces. Must not be None.")]
        [SerializeField] private SidePanelContext context = SidePanelContext.None;

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

#if UNITY_EDITOR
        /// <summary>The same authored-data check <c>SidePanelToggle.OnValidate</c> used to make
        /// before #75 moved <see cref="context"/> here — a panel left at <see cref="SidePanelContext.None"/>
        /// fades normally but silently never announces.</summary>
        private void OnValidate()
        {
            if (context == SidePanelContext.None)
                Debug.LogWarning($"{name}: SidePanel has no SidePanelContext - it will fade in " +
                                 "and out but never announce it, so a Quick Move will not route " +
                                 "and a staged sale will not cancel.", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
