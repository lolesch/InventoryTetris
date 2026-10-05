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
    /// "is this button pressed" - which its <see cref="ToggleGroup"/> keeps exclusive for the Town
    /// Stops. It does not decide whether the panel is up: the panel subscribes to the Inventory
    /// Context and derives its own visibility (<see cref="SidePanel"/>), which is why
    /// <see cref="OnToggle"/> is empty - the hook a <see cref="PanelToggle"/> would have faded the
    /// panel from is simply not this class's job. Reachability follows the same rule from the
    /// other side: the Town Stops sit under the InTown face, so while that face is hidden
    /// (a Run, or the Go Venture preview) its <c>CanvasGroup</c> takes them out of reach of a
    /// click and of <see cref="hotkey"/> alike - no check of the toggle's own.</para>
    ///
    /// <para><b>The pressed visual resyncs from the context, not from the group it no longer
    /// shares an authority with.</b> <see cref="SyncToContext"/> sets this toggle's own state from
    /// the panel's own <see cref="SidePanel.IsUpIn"/>, so the pressed visual stays truthful after a
    /// phase-driven close and after a click alike - including the Hero Panel's toggle, which
    /// belongs to no group and would otherwise look up while its panel is up.</para>
    ///
    /// <para><b>No second group.</b> Mutual exclusion comes from the shared <c>TownGroup</c>
    /// <see cref="ToggleGroup"/>, which Stash, Vendor and Healer belong to (Go Venture does not -
    /// it is a plain button, not a panel with state to protect). This class must not introduce a
    /// <see cref="ToggleGroup"/> of its own, or "which panel is open" would have two answers.</para>
    ///
    /// <para><b>The request (issues #84, #85).</b> Turning on requests the panel's context from
    /// <see cref="OnToggle"/>, but only while the panel is not already up in the active context,
    /// so a <see cref="SyncToContext"/> or the Hero Panel (up in every non-None context) never
    /// re-requests and overwrites the context that triggered it. Turning off is requested only by
    /// <see cref="UserToggle"/> - a click or hotkey that really switched the toggle off - never for
    /// a sibling the group silently deactivates on its way out, which is what keeps a
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

        [Tooltip("Optional hotkey. Inert whenever the toggle is non-interactable - which includes " +
                 "sitting under a hidden panel: a Town Stop is out of reach while the InTown face " +
                 "is hidden, whereas the Hero Panel's toggle sits under the HUD and keeps its hotkey " +
                 "in the field.")]
        [SerializeField] private KeyCode hotkey = KeyCode.None;

        protected override void OnClick() => UserToggle();

        /// <summary>
        /// Requests the panel's context on the way on, and never fades the panel: a
        /// <see cref="PanelToggle"/> fades its panel from exactly here, but this panel derives its
        /// own visibility from the Inventory Context instead (issue #85) - fading it from the
        /// toggle as well would be a second layer deciding one fact, the bug class this rework
        /// removes.
        ///
        /// <para>Also runs from <see cref="AbstractToggle.Start"/>, from <see cref="SyncToContext"/>
        /// and for a sibling the group switches off, none of which may touch the context - hence
        /// on-edge only, and only while the panel is not already up in it. That guard is what
        /// breaks the sync -> request -> sync cycle.</para>
        /// </summary>
        protected override void OnToggle()
        {
            if (IsOn && panel != null && InventoryProvider.Instance is { } provider && !panel.IsUpIn(provider.ActiveContext))
                panel.RequestContext(true);
        }

        /// <summary>
        /// The hotkey, asked every frame like <see cref="SyncToContext"/> asks the panel. Reachability
        /// is not decided here: <see cref="Selectable.IsInteractable"/> includes the
        /// <c>CanvasGroup</c>s above this toggle, so a hidden parent panel (InTown while the Field face
        /// is up) silences the key without a phase or provider dependency. The Hero Panel's toggle has
        /// no such panel above it and stays reachable in both faces, structurally.
        /// </summary>
        private void Update()
        {
            if (hotkey == KeyCode.None || !IsInteractable())
                return;

            if (!Input.GetKeyDown(hotkey))
                return;

            UserToggle();
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
        /// <para>Applied through <see cref="AbstractToggle.SyncToggle"/> - the group side, so the
        /// toggle follows the context off even where the user may not click it off - which keeps the
        /// <see cref="ToggleGroup"/>'s own <c>ActiveMember</c> in step for a grouped Town Stop and
        /// falls through to the plain state change for the ungrouped Hero Panel toggle. The
        /// <see cref="AbstractToggle.IsOn"/> guard is what stops this from looping: a
        /// <c>SyncToggle</c> on a sibling lands back here through that sibling's own event, finds
        /// nothing left to change, and stops.</para>
        /// </summary>
        private void SyncToContext(InventoryContext context)
        {
            if (panel == null)
                return;

            var shouldBeOn = panel.IsUpIn(context);
            if (IsOn != shouldBeOn)
                SyncToggle(shouldBeOn);
        }

        /// <summary>
        /// The click/hotkey edge. Does nothing when there is no panel or provider to request from -
        /// a toggle that pressed without a context behind it is how a stuck button and a
        /// pressed-but-empty panel start. Turning on is requested by <see cref="OnToggle"/>;
        /// turning off is requested here, and only once the toggle really switched off, so a
        /// group that refuses the un-toggle never closes the context behind a still-open panel.
        /// </summary>
        private void UserToggle()
        {
            if (panel == null || InventoryProvider.Instance == null)
                return;

            var wasOn = IsOn;
            SetToggle(!wasOn);
            if (wasOn && !IsOn)
                panel.RequestContext(false);
        }

#if UNITY_EDITOR
        /// <summary>
        /// The authored-data check <see cref="SidePanel"/>'s own <c>OnValidate</c> cannot make
        /// from its side: <see cref="UserToggle"/> and <see cref="SyncToContext"/> both
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

            // Reachability is the hierarchy's, not this toggle's: a Town Stop (grouped) must sit under
            // the face that hides it, the Hero Panel's (ungrouped) under none, or the hotkey is wrong
            // in one of the two faces.
            var hidingPanel = GetComponentInParent<SimplePanel>(true);

            if (RadioGroup && hidingPanel == null)
                Debug.LogWarning($"{name}: a grouped SidePanelToggle sits under no SimplePanel - its hotkey " +
                                 "stays live while the Town face is hidden.", gameObject);

            if (!RadioGroup && hidingPanel != null)
                Debug.LogWarning($"{name}: an ungrouped SidePanelToggle sits under '{hidingPanel.name}' - its " +
                                 "hotkey goes dead whenever that panel is hidden.", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
