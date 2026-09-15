using System.Collections.Generic;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Two-state minimap (issues #55, #56): swaps between a Town face (Stash / Vendor /
    /// Healer / Go Venture on town background art) and a Field face (location toggles +
    /// To Town on field background art). Two <see cref="RadioGroup"/> components control
    /// mutual exclusion within each face.
    ///
    /// The face normally follows <see cref="RunPhase"/> — Town face InTown, Field face
    /// InField — but Go Venture previews the Field face while still InTown so the player can
    /// pick a destination; To Town backs out of that preview. A location toggle
    /// <see cref="SimulationProvider.Send"/>s; To Town <see cref="SimulationProvider.Recall"/>s
    /// while InField. Entering the Field fades the <c>combatPanel</c> in and closes any open
    /// Side Panel; returning to Town (Recall or Death) fades it back out.
    ///
    /// Clicks are observed through <see cref="RadioGroup.OnGroupChanged"/> on the two groups
    /// rather than per toggle: the group already owns "which one is active", and
    /// <c>AbstractToggle.OnToggle</c> is an override point on the type, not an event to
    /// subscribe to. Subscriptions are made idempotent (detach before attach) because
    /// <see cref="BeforeAppear"/> can run again before <see cref="OnPanelDisable"/>.
    ///
    /// <para><b>Town/field toggles must be actual children of <see cref="townGroup"/> /
    /// <see cref="fieldGroup"/> in the hierarchy.</b> A toggle finds its group by walking up
    /// to its nearest <see cref="RadioGroup"/> ancestor; there is no code-side registration
    /// step. Pointing a toggle's group at one that is not its parent is exactly how a group
    /// ends up with an <c>ActivatedToggle</c> that is not one of its own children — which is
    /// what the manually-positioned Town buttons and the field's location toggles both need
    /// to avoid.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapPanel : SimplePanel
    {
        [Header("Backgrounds")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite townBackground;
        [SerializeField] private Sprite fieldBackground;

        [Header("Radio Groups")]
        [SerializeField] private RadioGroup townGroup;
        [SerializeField] private RadioGroup fieldGroup;

        [Header("Town Toggles (children of townGroup, manually positioned)")]
        [SerializeField] private AbstractToggle stashToggle;
        [SerializeField] private AbstractToggle vendorToggle;
        [SerializeField] private AbstractToggle healerToggle;
        [SerializeField] private AbstractToggle goVentureToggle;

        [Header("Field Toggles (children of fieldGroup, manually positioned)")]
        [SerializeField] private List<LocationToggle> locationToggles = new();
        [SerializeField] private AbstractToggle toTownToggle;

        [Header("Combat Panel")]
        [Tooltip("The left-side Combat Panel — fades in on Send (InField), out on Recall / Death (InTown).")]
        [SerializeField] private SimplePanel combatPanel;

        /// <summary>Which minimap face is shown. Always the Field face while InField; the
        /// player can also flip to it InTown with Go Venture, then back with To Town.</summary>
        private bool _showingFieldFace;
        private bool _inTown = true;

        protected override void BeforeAppear()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;

            // Unsubscribe first to avoid duplicate handlers across show/hide cycles
            // (SimplePanel keeps the GameObject enabled — FadeOut never triggers OnPanelDisable).
            run.PhaseChanged -= OnPhaseChanged;
            run.PhaseChanged += OnPhaseChanged;

            // Clearing first keeps a re-show from leaving a stale ActivatedToggle behind.
            ClearGroupSelection(townGroup);
            ClearGroupSelection(fieldGroup);

            SubscribeToGroups();

            SyncToPhase(run.Phase);
        }

        protected override void OnPanelDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            // Detach before clearing — Deactivate fires OnGroupChanged.
            UnsubscribeFromGroups();

            ClearGroupSelection(townGroup);
            ClearGroupSelection(fieldGroup);
        }

        private void OnPhaseChanged(RunPhase phase) => SyncToPhase(phase);

        /// <summary>
        /// Re-derive the whole minimap from the Run phase: which face is shown, whether the
        /// Combat Panel is up, and — on entering the Field — that no Side Panel is left open.
        /// Death routes through here too: <see cref="RunState.HandleDeath"/> fires
        /// <c>PhaseChanged(InTown)</c>, which resets the face and fades the Combat Panel out.
        /// </summary>
        private void SyncToPhase(RunPhase phase)
        {
            _inTown = phase == RunPhase.InTown;
            _showingFieldFace = !_inTown;

            ApplyFace();

            if (combatPanel != null)
            {
                if (_inTown) combatPanel.FadeOut();
                else combatPanel.FadeIn();
            }

            if (!_inTown)
                CloseSidePanels();
        }

        /// <summary>
        /// Paint the current face: swap the background art and set which toggles are live.
        /// Town toggles follow the Town face; location toggles are only sendable while
        /// actually InTown; To Town is live whenever the Field face is up (to Recall InField,
        /// or to back out of a Go Venture preview InTown).
        /// </summary>
        private void ApplyFace()
        {
            if (backgroundImage != null)
                backgroundImage.sprite = _showingFieldFace ? fieldBackground : townBackground;

            SetGroupInteractable(townGroup, !_showingFieldFace);

            foreach (var toggle in locationToggles)
                if (toggle != null)
                    toggle.interactable = _showingFieldFace && _inTown;

            if (toTownToggle != null)
                toTownToggle.interactable = _showingFieldFace;

            if (!_showingFieldFace && goVentureToggle != null && goVentureToggle.IsOn)
                goVentureToggle.SetToggle(false);
        }

        /// <summary>Idempotent subscribe — detach before attach so a second
        /// <see cref="BeforeAppear"/> before <see cref="OnPanelDisable"/> never stacks
        /// handlers. Subscribed after <see cref="ClearGroupSelection"/>, so its
        /// <c>Deactivate</c> calls cannot fire these.</summary>
        private void SubscribeToGroups()
        {
            if (townGroup != null)
            {
                townGroup.OnGroupChanged -= OnTownSelectionChanged;
                townGroup.OnGroupChanged += OnTownSelectionChanged;
            }

            if (fieldGroup != null)
            {
                fieldGroup.OnGroupChanged -= OnFieldSelectionChanged;
                fieldGroup.OnGroupChanged += OnFieldSelectionChanged;
            }
        }

        private void UnsubscribeFromGroups()
        {
            if (townGroup != null)
                townGroup.OnGroupChanged -= OnTownSelectionChanged;

            if (fieldGroup != null)
                fieldGroup.OnGroupChanged -= OnFieldSelectionChanged;
        }

        /// <summary>Go Venture (InTown only) previews the Field face so the player can pick a
        /// destination, and closes any open Stash / Vendor Side Panel. Read from the group
        /// rather than a per-toggle event: <c>ActivatedToggle</c> is the group's own answer to
        /// "which one is on", and it is already updated by the time this fires.</summary>
        private void OnTownSelectionChanged()
        {
            if (townGroup == null || townGroup.ActivatedToggle != goVentureToggle) return;
            if (goVentureToggle == null || !_inTown) return;

            _showingFieldFace = true;
            ApplyFace();
            CloseSidePanels();
        }

        /// <summary>
        /// The selected field toggle acts: a location Sends the hero there, To Town Recalls
        /// (InField) or backs out of a Go Venture preview (InTown). Deselection leaves
        /// <c>ActivatedToggle</c> null — not an action, so it is ignored.
        /// </summary>
        private void OnFieldSelectionChanged()
        {
            var selected = fieldGroup?.ActivatedToggle;
            if (selected == null) return;

            if (selected == toTownToggle)
            {
                OnToTownSelected();
                return;
            }

            // Only send while InTown — in the Field the toggles are non-interactable, but
            // guard against a phase change mid-frame. The resulting PhaseChanged(InField)
            // re-syncs the rest.
            if (selected is not LocationToggle location || location.Location == null) return;

            var provider = SimulationProvider.Instance;
            if (provider == null || provider.Run.Phase != RunPhase.InTown) return;

            provider.Send(location.Location);
        }

        private void OnToTownSelected()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            if (provider.Run.Phase == RunPhase.InField)
            {
                provider.Recall();
                return;
            }

            _showingFieldFace = false;
            ApplyFace();
        }

        /// <summary>Clear whichever Side Panel is open — a no-op when none is
        /// (<see cref="SidePanelState.Clear"/>). Used on Go Venture and on entering the Field.</summary>
        private static void CloseSidePanels()
        {
            var inventory = InventoryProvider.Instance;
            if (inventory != null)
                inventory.ClearSidePanel(inventory.ActiveSidePanel);
        }

        /// <summary>Deactivate every child of <paramref name="group"/> — membership is the
        /// hierarchy, the same way <see cref="SetGroupInteractable"/> reads it. A no-op per
        /// toggle unless it is the group's current <see cref="RadioGroup.ActivatedToggle"/>.</summary>
        private static void ClearGroupSelection(RadioGroup group)
        {
            if (group == null) return;

            foreach (var toggle in group.GetComponentsInChildren<AbstractToggle>(true))
                group.Deactivate(toggle);
        }

        private static void SetGroupInteractable(RadioGroup group, bool interactable)
        {
            if (group == null) return;

            foreach (var toggle in group.GetComponentsInChildren<AbstractToggle>(true))
                toggle.interactable = interactable;
        }
    }
}
