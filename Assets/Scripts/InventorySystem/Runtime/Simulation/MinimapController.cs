using System.Collections.Generic;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Toggles;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Orchestrates Town/Field navigation (issues #55, #56; #58 correction 2026-09-17): shows
    /// exactly one of <see cref="inTownPanel"/> / <see cref="inFieldPanel"/> at a time. Not a
    /// panel itself — it never fades, it only decides which of its two children does.
    ///
    /// The face normally follows <see cref="RunPhase"/> — <see cref="inTownPanel"/> shown
    /// InTown, <see cref="inFieldPanel"/> shown InField — but Go Venture previews the InField
    /// panel while still InTown so the player can pick a destination; To Town backs out of
    /// that preview. A location toggle <see cref="SimulationProvider.Send"/>s; To Town
    /// <see cref="SimulationProvider.Recall"/>s while InField. Entering the Field fades
    /// <see cref="combatPanel"/> in and closes any open Side Panel; returning to Town (Recall
    /// or Death) fades it back out.
    ///
    /// <see cref="GoVentureButton"/> / <see cref="ToTownButton"/> hold a reference to this
    /// controller and call <see cref="GoVenture"/> / <see cref="ToTown"/> on click; this class
    /// deliberately does not hold a reference back to them. Interactable gating went with that
    /// reference: Go Venture, To Town, and the Stash/Vendor/Healer <see cref="townGroup"/> all
    /// live on whichever panel is currently faded out, and <c>CanvasGroup.blocksRaycasts</c>
    /// already makes a faded-out panel's children non-interactive. Two sets are the exception,
    /// each for a reason of its own:
    ///
    /// <list type="bullet">
    /// <item>Locations keep their clicks while the Field face is up — <see cref="inFieldPanel"/>
    /// stays shown whether the player is previewing (InTown) or has actually travelled
    /// (InField), so only each <c>LocationToggle</c>'s own <c>interactable</c>, gated on
    /// <see cref="_inTown"/>, stops a stray click from reassigning <see cref="fieldGroup"/>'s
    /// selection while already in the field.</item>
    /// <item>The Town Stops keep their <i>hotkeys</i>, which read <c>interactable</c> and so
    /// bypass that <c>CanvasGroup</c> entirely — nothing about the fade reaches them. They are
    /// gated on the face being shown instead, which covers the Go Venture preview as well
    /// (issue #73).</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Faces")]
        [SerializeField] private SimplePanel inTownPanel;
        [SerializeField] private SimplePanel inFieldPanel;

        [Header("Radio Groups")]
        [SerializeField] private RadioGroup townGroup;
        [SerializeField] private RadioGroup fieldGroup;

        [Header("Locations (children of fieldGroup, manually positioned)")]
        [SerializeField] private List<LocationToggle> locationToggles = new();

        [Header("Combat Panel")]
        [Tooltip("The left-side Combat Panel — fades in on Send (InField), out on Recall / Death (InTown).")]
        [SerializeField] private SimplePanel combatPanel;

        private bool _inTown = true;

        /// <summary>
        /// Every <see cref="SidePanelToggle"/> under <see cref="townGroup"/>, each with the
        /// <c>interactable</c> it was authored with. Resolved from the group rather than listed
        /// by hand so a Town Stop added later is gated by construction instead of by remembering
        /// to author it here. The authored value is what the gate restores on the way back into
        /// Town — the scene's Healer is a placeholder authored non-interactable, and it must not
        /// come back on for merely being InTown.
        /// </summary>
        private readonly Dictionary<SidePanelToggle, bool> _townToggles = new();

        private void Awake()
        {
            if (!townGroup)
                return;

            foreach (var toggle in townGroup.GetComponentsInChildren<SidePanelToggle>(true))
                _townToggles[toggle] = toggle.interactable;
        }

        private void OnEnable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged += SyncToPhase;

            if (fieldGroup)
                fieldGroup.OnGroupChanged += OnFieldSelectionChanged;

            SyncToPhase(provider.Run.Phase);
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= SyncToPhase;

            if (fieldGroup)
                fieldGroup.OnGroupChanged -= OnFieldSelectionChanged;
        }

        /// <summary>
        /// Re-derive the whole minimap from the Run phase: which face is shown, whether the
        /// Combat Panel is up, and — on entering the Field — that no Side Panel is left open.
        /// Death routes through here too: <see cref="RunState.HandleDeath"/> fires
        /// <c>PhaseChanged(InTown)</c>, which resets the face and fades the Combat Panel out.
        /// </summary>
        private void SyncToPhase(RunPhase phase)
        {
            _inTown = phase == RunPhase.InTown;

            ApplyFace(!_inTown);

            if (combatPanel)
                combatPanel.Toggle(!_inTown);

            // Whichever Side Panel is open (if any) belongs to a townGroup toggle — clearing
            // the group closes it and announces the clear via SidePanelToggle.OnToggle(false).
            if (!_inTown && townGroup)
                townGroup.ClearSelection();
        }

        /// <summary>Go Venture (InTown only — the button is non-interactable otherwise):
        /// preview the Field face and close any open Side Panel.</summary>
        public void GoVenture()
        {
            if (townGroup)
                townGroup.ClearSelection();

            ApplyFace(true);
        }

        /// <summary>To Town: Recall while InField (the resulting <c>PhaseChanged</c> re-syncs
        /// the face via <see cref="SyncToPhase"/>), or back out of a Go Venture preview while
        /// still InTown.</summary>
        public void ToTown()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            if (provider.Run.Phase == RunPhase.InField)
            {
                provider.Recall();
                return;
            }

            ApplyFace(false);
        }

        /// <summary>Show one face, hide the other, and re-gate the two sets the fade does not
        /// reach on its own — the locations and the Town Stops (see class doc for why each is
        /// gated differently).</summary>
        private void ApplyFace(bool showField)
        {
            if (inTownPanel)
                inTownPanel.Toggle(!showField);

            if (inFieldPanel)
                inFieldPanel.Toggle(showField);

            foreach (var toggle in locationToggles)
                if (toggle != null)
                    toggle.interactable = _inTown;

            // Gated on the face, not on _inTown: the Go Venture preview has faded the Town face
            // out while still InTown, and a Town Stop is as unreachable there as it is after a
            // Send — a click cannot reach it, so its hotkey must not either (#73).
            foreach (var entry in _townToggles)
                entry.Key.interactable = entry.Value && !showField;
        }

        /// <summary>
        /// A selected location Sends the hero there. Fires on deselection too
        /// (<see cref="RadioGroup.SelectedToggle"/> null) — that is not a destination, so it
        /// is ignored.
        /// </summary>
        private void OnFieldSelectionChanged(AbstractToggle toggle)
        {
            if (toggle is not LocationToggle location || location.Location == null) return;

            var provider = SimulationProvider.Instance;
            if (provider == null || provider.Run.Phase != RunPhase.InTown) return;

            provider.Send(location.Location);
        }
    }
}
