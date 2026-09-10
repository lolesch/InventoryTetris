using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// A Town Stop's panel toggle that announces itself as the active
    /// <see cref="SidePanelContext"/> (issue #57). Fades its panel like any
    /// <see cref="PanelToggle"/>; the addition is that it tells the
    /// <see cref="InventoryProvider"/> which context is open, so the trade flow can route a
    /// Quick Move and the Sell Basket can cancel a staged sale when its panel goes away.
    ///
    /// <para><b>Both edges, deliberately.</b> The toggle that closes the Vendor panel is
    /// usually not the Vendor toggle - it is whichever sibling the player just activated.
    /// <see cref="RadioGroup.Activate"/> switches every loser off, so the announcement has to
    /// ride <see cref="SetToggle"/> in both directions rather than a click handler on the
    /// winner. That is what lets Healer and Go Venture (#58) cancel a staged sale without
    /// either of them knowing the Vendor exists.</para>
    ///
    /// <para><b>Order-independent.</b> <see cref="AbstractToggle.SetToggle"/> calls
    /// <c>RadioGroup.Activate</c> from inside <c>base.SetToggle</c>, so a loser's
    /// <see cref="InventoryProvider.ClearSidePanel"/> lands <i>before</i> the winner's
    /// <see cref="InventoryProvider.SetSidePanel"/> here. It would be correct the other way
    /// round too: <see cref="SidePanelState.Clear"/> only clears when the context matches, so
    /// a late loser cannot close the panel the winner just opened. Do not rely on the current
    /// order - rely on that guard.</para>
    ///
    /// <para><b>No second group.</b> Mutual exclusion already comes from the minimap's
    /// <c>TownGroup</c>, which Stash, Vendor, Healer and Go Venture all belong to. This class
    /// adds the announcement only; it must not introduce a <see cref="RadioGroup"/> of its
    /// own, or "which panel is open" would have two answers.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanelToggle : PanelToggle
    {
        [Header("Side Panel")]
        [Tooltip("Which Town Stop context this toggle opens. Must not be None.")]
        [SerializeField] private SidePanelContext context = SidePanelContext.None;

        [Tooltip("Optional hotkey. Inert whenever the toggle is non-interactable - which the " +
                 "minimap already arranges for the Field face and for InField.")]
        [SerializeField] private KeyCode hotkey = KeyCode.None;

        /// <summary>The context this toggle announces. Authored data, not inferred.</summary>
        public SidePanelContext Context => context;

        public override void SetToggle(bool isOn)
        {
            base.SetToggle(isOn);

            Announce(isOn);
        }

        /// <summary>
        /// Push the transition to the provider. Play mode only: reading
        /// <see cref="AbstractProvider{T}.Instance"/> in the editor <i>creates</i> a provider
        /// GameObject when none exists (issue #46), and <see cref="AbstractToggle.OnValidate"/>
        /// can reach this method through <c>RadioGroup.Activate</c> while the player is only
        /// editing the scene.
        /// </summary>
        private void Announce(bool isOn)
        {
            if (!Application.isPlaying || context == SidePanelContext.None)
                return;

            var provider = InventoryProvider.Instance;

            if (provider == null)
                return;

            if (isOn)
                provider.SetSidePanel(context);
            else
                provider.ClearSidePanel(context);
        }

        /// <summary>
        /// The hotkey is the same act as a click, guard for guard - including the
        /// <see cref="RadioGroup.AllowSwitchOff"/> rule, so a hotkey cannot switch off a
        /// toggle a click could not. <c>interactable</c> is the phase gate: the minimap turns
        /// the Town toggles off whenever the Field face is up, which covers both InField and
        /// the Go Venture preview, so no <c>RunPhase</c> dependency is needed here.
        /// </summary>
        private void Update()
        {
            if (hotkey == KeyCode.None || !interactable)
                return;

            if (!Input.GetKeyDown(hotkey))
                return;

            if (RadioGroup && !RadioGroup.AllowSwitchOff && IsOn)
                return;

            SetToggle(!IsOn);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (context == SidePanelContext.None)
                Debug.LogWarning($"{name}: SidePanelToggle has no SidePanelContext - it will " +
                                 "open its panel but never announce it, so a Quick Move will " +
                                 "not route and a staged sale will not cancel.", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
