using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// A Town Stop's panel toggle. Fades its panel like any <see cref="PanelToggle"/> — it
    /// carries a hotkey and nothing else; the panel it fades
    /// (<see cref="ToolSmiths.InventorySystem.GUI.Components.Panels.SidePanel"/>) is what
    /// announces the <see cref="ToolSmiths.InventorySystem.Inventories.SidePanelContext"/> the
    /// toggle used to announce itself (#57), since #75 moved that job to the moment the panel
    /// actually appears or disappears rather than the moment its toggle is clicked.
    ///
    /// <para><b>No second group.</b> Mutual exclusion comes from the minimap's <c>TownGroup</c>,
    /// which Stash, Vendor and Healer belong to (Go Venture does not — it is a plain button, not
    /// a panel with state to protect). This class must not introduce a <see cref="RadioGroup"/>
    /// of its own, or "which panel is open" would have two answers.</para>
    ///
    /// <para><b>The Inventory Context request (issue #84).</b> <see cref="RequestAndToggle"/> is
    /// the toggle's own click/hotkey edge — where #57 originally had the announcement, and where
    /// it belongs again now that the Hero Panel joins every context. It runs before
    /// <see cref="AbstractToggle.SetToggle"/>, so it fires only for the toggle actually clicked
    /// or hotkeyed — never for a sibling the group silently deactivates on its way out via
    /// <c>ToggleState</c>/<c>OnToggle</c> — which is what keeps a Stash-to-Vendor handover to the
    /// one <see cref="SidePanel.RequestContext"/> call the incoming toggle makes, with no
    /// intermediate close. Named to avoid <see cref="RadioGroup.Activate"/>, one call away in the
    /// same stack once <see cref="AbstractToggle.SetToggle"/> runs — same word, unrelated method.</para>
    ///
    /// <para><b>Assumes <c>invert</c> is off.</b> <see cref="PanelToggle"/>'s <c>invert</c> field
    /// is private to that base class, so this class cannot read it to correct for it:
    /// <see cref="RequestAndToggle"/> requests <paramref name="turningOn"/> as given, matching the
    /// panel's own show/hide direction only when this toggle is not inverted. A Town Stop or Hero
    /// Panel toggle must not set <c>invert</c> while its panel carries an
    /// <see cref="ToolSmiths.InventorySystem.Inventories.InventoryContext"/> role.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanelToggle : PanelToggle
    {
        [Tooltip("Optional hotkey. Inert whenever the toggle is non-interactable - which the " +
                 "minimap already arranges for the Field face and for InField.")]
        [SerializeField] private KeyCode hotkey = KeyCode.None;

        protected override void OnClick() => RequestAndToggle(!IsOn);

        /// <summary>
        /// The hotkey is the same act as a click, guard for guard - including the
        /// <see cref="RadioGroup.IsClearable"/> rule, so a hotkey cannot switch off a
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

            RequestAndToggle(!IsOn);
        }

        /// <summary>
        /// Requests the Inventory Context this toggle's panel represents, then applies the
        /// toggle state exactly as <see cref="AbstractToggle.SetToggle"/> always has. The request
        /// goes through the panel (<see cref="SidePanel.RequestContext"/>), not straight to the
        /// provider - the toggle still carries no context of its own, only which panel to ask.
        /// <c>panel</c> is every <see cref="SidePanelToggle"/>'s own panel, Hero Panel toggle
        /// included (#82's reuse), so the cast covers every instance of this class.
        ///
        /// <para><see cref="WouldToggleNoOp"/> guards the request the same way
        /// <see cref="AbstractToggle.SetToggle"/> guards itself: <c>OnClick</c> has no other veto
        /// before reaching here, so without this check a click on a toggle
        /// <see cref="AbstractToggle.SetToggle"/> is about to refuse to turn off would still
        /// close the Inventory Context while the panel stays visibly open - the request and the
        /// toggle state would disagree. <c>Update</c>'s hotkey path used to re-implement a looser
        /// version of this same veto by hand; folding it in here removes that second copy.</para>
        /// </summary>
        private void RequestAndToggle(bool turningOn)
        {
            if (WouldToggleNoOp(turningOn))
                return;

            if (panel is SidePanel sidePanel)
                sidePanel.RequestContext(turningOn);

            SetToggle(turningOn);
        }

        /// <summary>
        /// Mirrors <see cref="AbstractToggle.SetToggle"/>'s own no-op condition exactly (that
        /// method is not virtual, so it cannot be asked directly) - turning off the sole active,
        /// non-clearable, non-restorable member of a <see cref="RadioGroup"/> is refused there,
        /// silently as far as this class is concerned.
        /// </summary>
        private bool WouldToggleNoOp(bool turningOn) =>
            !turningOn && IsOn && RadioGroup && RadioGroup.ActiveMember == this
            && !RadioGroup.IsClearable && !RadioGroup.IsRestorable;

#if UNITY_EDITOR
        /// <summary>
        /// The authored-data check <see cref="SidePanel"/>'s own <c>OnValidate</c> cannot make
        /// from its side: <see cref="RequestAndToggle"/> silently skips
        /// <see cref="SidePanel.RequestContext"/> whenever <c>panel</c> is not a
        /// <see cref="SidePanel"/> - the panel still fades correctly, so nothing looks wrong in
        /// Play mode, and the Inventory Context is just never requested for it.
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            if (panel != null && panel is not SidePanel)
                Debug.LogWarning($"{name}: SidePanelToggle's panel is a {panel.GetType().Name}, " +
                                 "not a SidePanel - it will fade in and out but never request " +
                                 "the Inventory Context (issue #84).", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
