using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// A Town Stop's panel toggle, and the Hero Panel's own toggle (#82's reuse): a hotkey, a
    /// panel to ask and nothing else. <see cref="PanelToggle"/> stays the base for the
    /// <c>panel</c> reference itself - the serialized field every scene's wiring lives on, which
    /// is also what <see cref="SyncToContext"/> reads the panel's context from - and not for the
    /// fade, which is overridden away.
    ///
    /// <para><b>Input, not content (issue #85).</b> This toggle answers exactly one question -
    /// "is this button pressed" - which its <see cref="RadioGroup"/> keeps exclusive for the Town
    /// Stops. It does not decide whether the panel is up: the panel subscribes to the Inventory
    /// Context and derives its own visibility (<see cref="SidePanel"/>), and this class overrides
    /// <see cref="OnToggle"/> to drop the fade its <see cref="PanelToggle"/> base would otherwise
    /// run from there. The minimap's Town Stops being non-interactable during a Run is the same
    /// split from the other side - the input-side expression of a fact the context is the
    /// authority for.</para>
    ///
    /// <para><b>The pressed visual resyncs from the context, not from the group it no longer
    /// shares an authority with.</b> <see cref="SyncToContext"/> sets this toggle's own state from
    /// the same derivation the panel uses, so the pressed visual stays truthful after a
    /// phase-driven close and after a click alike - including the Hero Panel's toggle, which
    /// belongs to no group and would otherwise look up while its panel is up.</para>
    ///
    /// <para><b>No second group.</b> Mutual exclusion comes from the minimap's <c>TownGroup</c>,
    /// which Stash, Vendor and Healer belong to (Go Venture does not - it is a plain button, not
    /// a panel with state to protect). This class must not introduce a <see cref="RadioGroup"/>
    /// of its own, or "which panel is open" would have two answers.</para>
    ///
    /// <para><b>The request (issues #84, #85).</b> <see cref="RequestAndToggle"/> is the toggle's
    /// own click/hotkey edge: it runs before <see cref="AbstractToggle.SetToggle"/>, so it fires
    /// only for the toggle actually clicked or hotkeyed - never for a sibling the group silently
    /// deactivates on its way out via <c>ToggleState</c>/<c>OnToggle</c> - which is what keeps a
    /// Stash-to-Vendor handover to the one <see cref="SidePanel.RequestContext"/> call the incoming
    /// toggle makes, with no intermediate close. The request goes through the panel
    /// (<see cref="SidePanel.RequestContext"/>), not straight to the provider - the toggle still
    /// carries no context of its own, only which panel to ask.</para>
    ///
    /// <para><b>Assumes <c>invert</c> is off.</b> <see cref="PanelToggle"/>'s <c>invert</c> field
    /// is private to that base class, so this class cannot read it to correct for it:
    /// <see cref="RequestAndToggle"/> requests <paramref name="turningOn"/> as given, matching the
    /// panel's own show/hide direction only when this toggle is not inverted. A Town Stop or Hero
    /// Panel toggle must not set <c>invert</c> while its panel carries an
    /// <see cref="InventoryContext"/> role.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanelToggle : PanelToggle
    {
        [Tooltip("Optional hotkey. Inert whenever the toggle is non-interactable - which the " +
                 "minimap already arranges for the Field face and for InField.")]
        [SerializeField] private KeyCode hotkey = KeyCode.None;

        protected override void OnClick() => RequestAndToggle(!IsOn);

        /// <summary>
        /// Emptied deliberately. <see cref="PanelToggle"/> fades its panel from exactly here, and
        /// the panel now derives its own visibility from the Inventory Context (issue #85) - so
        /// fading it from the toggle as well would be a second layer deciding one fact, the bug
        /// class this rework removes. What <see cref="AbstractToggle.ToggleState"/> does around
        /// this call - the radio group's state and this toggle's own pressed visual - is untouched.
        /// </summary>
        protected override void OnToggle() { }

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
        /// Subscribed in <see cref="Awake"/> rather than <c>OnEnable</c>: the
        /// <see cref="UnityEngine.UI.Selectable"/> this derives from already owns
        /// <c>OnEnable</c>, and a same-named method here would hide it rather than run beside it.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (!Application.isPlaying)
                return;

            var provider = InventoryProvider.Instance;
            if (provider == null)
                return;

            provider.OnContextChanged -= OnContextChanged;
            provider.OnContextChanged += OnContextChanged;

            SyncToContext(provider.ActiveContext);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Application.isPlaying && InventoryProvider.Instance != null)
                InventoryProvider.Instance.OnContextChanged -= OnContextChanged;
        }

        private void OnContextChanged(InventoryContext context) => SyncToContext(context);

        /// <summary>
        /// Brings this toggle's pressed state back in line with the Inventory Context: on exactly
        /// when the active context derives the panel this toggle drives, off otherwise. The
        /// derivation is the panel's own (<see cref="InventoryContextState.PanelsFor"/> against
        /// <see cref="InventoryContextState.PanelFor"/> of its authored context), so the visual
        /// and the visibility cannot disagree by construction.
        ///
        /// <para>Applied through <see cref="AbstractToggle.SetToggle"/>, which keeps the
        /// <see cref="RadioGroup"/>'s own <c>ActiveMember</c> in step for a grouped Town Stop and
        /// falls through to the plain state change for the ungrouped Hero Panel toggle. The
        /// <see cref="AbstractToggle.IsOn"/> guard is what stops this from looping: a
        /// <c>SetToggle</c> on a sibling lands back here through that sibling's own event, finds
        /// nothing left to change, and stops.</para>
        /// </summary>
        private void SyncToContext(InventoryContext context)
        {
            if (panel is not SidePanel sidePanel)
                return;

            var mine = InventoryContextState.PanelFor(sidePanel.InventoryContext);
            if (mine == InventoryPanels.None)
                return;

            var shouldBeOn = (InventoryContextState.PanelsFor(context) & mine) != InventoryPanels.None;
            if (IsOn != shouldBeOn)
                SetToggle(shouldBeOn);
        }

        /// <summary>
        /// Requests the Inventory Context this toggle's panel represents, then applies the
        /// toggle state exactly as <see cref="AbstractToggle.SetToggle"/> always has.
        ///
        /// <para><see cref="WouldToggleNoOp"/> guards the request the same way
        /// <see cref="AbstractToggle.SetToggle"/> guards itself: <c>OnClick</c> has no other veto
        /// before reaching here, so without this check a click on a toggle
        /// <see cref="AbstractToggle.SetToggle"/> is about to refuse to turn off would still close
        /// the Inventory Context while the panel stays visibly open - the request and the toggle
        /// state would disagree, and <see cref="SyncToContext"/> could not heal the gap, because
        /// the refusal it would hit lives in <c>SetToggle</c> too.</para>
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
        /// from its side: <see cref="RequestAndToggle"/> and <see cref="SyncToContext"/> both
        /// silently skip a panel that is not a <see cref="SidePanel"/> - the panel still fades
        /// with the context, so nothing looks wrong in Play mode, and this toggle just never
        /// requests or tracks anything.
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            if (panel != null && panel is not SidePanel)
                Debug.LogWarning($"{name}: SidePanelToggle's panel is a {panel.GetType().Name}, " +
                                 "not a SidePanel - it will never request the Inventory Context " +
                                 "and its pressed visual will not track one (issues #84, #85).",
                                 gameObject);
        }
#endif // UNITY_EDITOR
    }
}
