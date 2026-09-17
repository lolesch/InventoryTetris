using System.Collections.Generic;
using Submodules.Utility.UI;
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
    /// already makes a faded-out panel's children non-interactive. Locations are the one
    /// exception — <see cref="inFieldPanel"/> stays shown whether the player is previewing
    /// (InTown) or has actually travelled (InField), so only each <c>LocationToggle</c>'s own
    /// <c>interactable</c>, gated on <see cref="_inTown"/>, stops a stray click from
    /// reassigning <see cref="fieldGroup"/>'s selection while already in the field.
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

        /// <summary>Show one face, hide the other, and re-gate the locations — the only
        /// interactable this controller still manages (see class doc).</summary>
        private void ApplyFace(bool showField)
        {
            if (inTownPanel)
                inTownPanel.Toggle(!showField);

            if (inFieldPanel)
                inFieldPanel.Toggle(showField);

            foreach (var toggle in locationToggles)
                if (toggle != null)
                    toggle.interactable = _inTown;
        }

        /// <summary>
        /// A selected location Sends the hero there. Fires on deselection too
        /// (<see cref="RadioGroup.SelectedToggle"/> null) — that is not a destination, so it
        /// is ignored.
        /// </summary>
        private void OnFieldSelectionChanged()
        {
            if (fieldGroup?.SelectedToggle is not LocationToggle location || location.Location == null) return;

            var provider = SimulationProvider.Instance;
            if (provider == null || provider.Run.Phase != RunPhase.InTown) return;

            provider.Send(location.Location);
        }
    }
}
