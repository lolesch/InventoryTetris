using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// A Town Stop's panel toggle, and the Hero Panel's own toggle (#82's reuse): a hotkey, a
    /// typed panel reference and nothing else. It derives <see cref="AbstractToggle"/> rather than
    /// the <see cref="PanelToggle"/> that would fade the panel: the panel derives its own
    /// visibility now, so inheriting the fade only to override it away would leave that base
    /// class's <c>invert</c> on the component meaning nothing, and one more way to author a toggle
    /// that disagrees with its own panel.
    ///
    /// <para><b>Input, not content (issue #85).</b> This toggle answers exactly one question -
    /// "is this button pressed" - which its <see cref="RadioGroup"/> keeps exclusive for the Town
    /// Stops. It does not decide whether the panel is up: the panel subscribes to the Inventory
    /// Context and derives its own visibility (<see cref="SidePanel"/>), which is why
    /// <see cref="OnToggle"/> is empty - the hook a <see cref="PanelToggle"/> would have faded the
    /// panel from is simply not this class's job. The Town Stops going non-interactable during a
    /// Run (<see cref="InventoryProvider.IsFieldReachable"/>) is the same split from the other
    /// side - the input-side expression of a fact the context is the authority for.</para>
    ///
    /// <para><b>The pressed visual resyncs from the context, not from the group it no longer
    /// shares an authority with.</b> <see cref="SyncToContext"/> sets this toggle's own state from
    /// the panel's own <see cref="SidePanel.IsUpIn"/>, so the pressed visual stays truthful after a
    /// phase-driven close and after a click alike - including the Hero Panel's toggle, which
    /// belongs to no group and would otherwise look up while its panel is up.</para>
    ///
    /// <para><b>No second group.</b> Mutual exclusion comes from the shared <c>TownGroup</c>
    /// <see cref="RadioGroup"/>, which Stash, Vendor and Healer belong to (Go Venture does not -
    /// it is a plain button, not a panel with state to protect). This class must not introduce a
    /// <see cref="RadioGroup"/> of its own, or "which panel is open" would have two answers.</para>
    ///
    /// <para><b>The request (issues #84, #85).</b> <see cref="RequestAndToggle"/> is the toggle's
    /// own click/hotkey edge: it runs before <see cref="AbstractToggle.SetToggle"/>, so it fires
    /// only for the toggle actually clicked or hotkeyed - never for a sibling the group silently
    /// deactivates on its way out via <c>ToggleState</c>/<c>OnToggle</c> - which is what keeps a
    /// Stash-to-Vendor handover to the one <see cref="SidePanel.RequestContext"/> call the incoming
    /// toggle makes, with no intermediate close. The request goes through the panel
    /// (<see cref="SidePanel.RequestContext"/>), not straight to the provider - the toggle still
    /// carries no context of its own, only which panel to ask.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanelToggle : AbstractToggle
    {
        [Tooltip("The SidePanel this toggle drives: requested on click/hotkey, and the panel " +
                 "whose own visibility answer this toggle's pressed visual resyncs from. The " +
                 "type is the field's contract - a toggle wired to anything else cannot be " +
                 "authored.")]
        [SerializeField] private SidePanel panel;

        [Tooltip("Optional hotkey. Inert whenever the toggle is non-interactable - which " +
                 "InventoryProvider.IsFieldReachable arranges for the Field face and for InField.")]
        [SerializeField] private KeyCode hotkey = KeyCode.None;

        [Tooltip("Whether this toggle is gated by InventoryProvider.IsFieldReachable at all. " +
                 "Only ever consulted for a toggle in a RadioGroup; an ungrouped toggle, such " +
                 "as the Hero Panel's, is never field-gated - the Hero is reachable in both " +
                 "faces, so its hotkey must survive the field.")]
        [SerializeField] private bool gatedByFieldReachability = true;

        /// <summary>The Inspector-authored baseline this toggle's <c>interactable</c> gates
        /// against, captured once so a Town Stop shipped disabled (<c>interactable == false</c>
        /// from the start) stays disabled rather than coming back on for merely being InTown.</summary>
        private bool authoredInteractable;

        protected override void Awake()
        {
            base.Awake();

            authoredInteractable = interactable;
        }

        protected override void OnClick() => RequestAndToggle(!IsOn);

        /// <summary>
        /// Empty deliberately. A <see cref="PanelToggle"/> fades its panel from exactly here, and
        /// the panel derives its own visibility from the Inventory Context instead (issue #85) -
        /// fading it from the toggle as well would be a second layer deciding one fact, the bug
        /// class this rework removes. What <see cref="AbstractToggle.ToggleState"/> does around
        /// this call - the radio group's state and this toggle's own pressed visual - is untouched.
        /// </summary>
        protected override void OnToggle() { }

        /// <summary>
        /// <c>interactable</c> is the phase gate: unreachable whenever
        /// <see cref="InventoryProvider.IsFieldReachable"/> says so (InField and the Go Venture
        /// preview alike, since both show the same face) - so no <c>RunPhase</c> dependency is
        /// needed here. Asked every frame rather than pushed by a controller (issue #85), the
        /// same way <see cref="SyncToContext"/> asks the panel instead of being told.
        ///
        /// <para>Only a toggle in a <see cref="RadioGroup"/> is gated - a Town Stop. The Hero
        /// Panel's toggle belongs to no group and is reachable in both faces, so gating it would
        /// take its hotkey away in the field for nothing; keying the gate on the group makes that
        /// exemption structural instead of a per-instance checkbox the scene can get wrong.</para>
        /// </summary>
        private void Update()
        {
            if (gatedByFieldReachability && RadioGroup && InventoryProvider.Instance != null)
                interactable = authoredInteractable && InventoryProvider.Instance.IsFieldReachable;

            if (hotkey == KeyCode.None || !interactable)
                return;

            if (!Input.GetKeyDown(hotkey))
                return;

            RequestAndToggle(!IsOn);
        }

        /// <summary>
        /// Subscribed here rather than in <see cref="Awake"/>: this project disables both domain
        /// and scene reload (see <c>EditorSettings.enterPlayModeOptions</c>), so a scene object
        /// survives a Play session and <b>an <c>Awake</c> subscription never comes back</b> after
        /// <see cref="OnDisable"/> has torn it down on the way out of the first Play entry — the
        /// toggle then presses and unpresses while the context it should have requested is never
        /// touched.
        ///
        /// <para>Overridden rather than declared: this class derives
        /// <see cref="UnityEngine.UI.Selectable"/>, which owns <c>OnEnable</c>, and a same-named
        /// method that did not chain to it would silently break the button's own state setup.</para>
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            if (InventoryProvider.TrySubscribeContextChanged(SyncToContext, out var activeContext))
                SyncToContext(activeContext);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            InventoryProvider.UnsubscribeContextChanged(SyncToContext);
        }

        /// <summary>
        /// Brings this toggle's pressed state back in line with the Inventory Context: on exactly
        /// when the panel this toggle drives is up in it, off otherwise - the panel's own
        /// <see cref="SidePanel.IsUpIn"/>, asked rather than recomputed, so the visual and the
        /// visibility cannot disagree by construction.
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
            if (panel == null)
                return;

            var shouldBeOn = panel.IsUpIn(context);
            if (IsOn != shouldBeOn)
                SetToggle(shouldBeOn);
        }

        /// <summary>
        /// Requests the Inventory Context this toggle's panel represents, then applies the
        /// toggle state exactly as <see cref="AbstractToggle.SetToggle"/> always has - but only
        /// when the request was actually made (<see cref="SidePanel.RequestContext"/>'s result).
        /// A toggle that moved while its request silently did not would press for a context
        /// nothing set, which is how a stuck button and a pressed-but-empty panel start.
        ///
        /// <para><see cref="WouldToggleNoOp"/> guards the other direction, and for the same
        /// reason: <c>OnClick</c> has no other veto before reaching here, so without it a click on
        /// a toggle <see cref="AbstractToggle.SetToggle"/> is about to refuse to turn off would
        /// still close the Inventory Context while the panel stays visibly open.</para>
        /// </summary>
        private void RequestAndToggle(bool turningOn)
        {
            if (WouldToggleNoOp(turningOn))
                return;

            if (panel == null || !panel.RequestContext(turningOn))
                return;

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
        /// no-op on a toggle that drives no panel - it still presses and unpresses, so nothing
        /// looks wrong in Play mode, and it just never requests or tracks an Inventory Context.
        /// That the panel is a <see cref="SidePanel"/> at all is the field's own type now, so
        /// only its absence is left for authored data to get wrong.
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            if (panel == null)
                Debug.LogWarning($"{name}: SidePanelToggle drives no panel - it will never " +
                                 "request the Inventory Context and its pressed visual cannot " +
                                 "track one (issues #84, #85).", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
