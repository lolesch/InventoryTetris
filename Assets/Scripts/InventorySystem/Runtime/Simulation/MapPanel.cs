using Submodules.Utility.UI;
using Submodules.Utility.UI.InteractiveElements;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The map panel (issue #27): a <see cref="RadioGroup"/> of <see cref="LocationToggle"/>
    /// items plus a Town toggle for recall. Selecting a field location sends the hero there
    /// (<see cref="SimulationProvider.Send"/>). No dedicated Send button — selection is the
    /// action.
    ///
    /// Selection is observed through <see cref="RadioGroup.OnGroupChanged"/> rather than
    /// per-toggle: the group already owns "which one is active", and
    /// <see cref="RadioGroup.ActivatedToggle"/> is the single source of truth this panel
    /// reads. Recall lives on the toggle itself (<see cref="TownRecallToggle"/>) — this panel
    /// keeps <see cref="townToggle"/> only to drive <c>interactable</c>.
    ///
    /// While <see cref="RunPhase.InField"/>, only the Town toggle is interactable — the
    /// player must Recall before choosing a new destination. Fades in/out via the parent
    /// <see cref="MultiplePanelToggle"/> on the Switch Context button.
    ///
    /// <see cref="SimplePanel.BeforeAppear"/> runs on every fade-in and the panel stays
    /// enabled between them, so <see cref="SimplePanel.OnPanelDisable"/> is not a reliable
    /// pair with it — a second appear without a teardown would otherwise stack a second
    /// <c>PhaseChanged</c> / <c>OnGroupChanged</c> subscription and fire
    /// <see cref="SimulationProvider.Send"/> twice on one click. Every subscription here is
    /// therefore made idempotent (detach before attach).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapPanel : SimplePanel
    {
        [SerializeField] private RadioGroup locationGroup;
        [SerializeField] private AbstractToggle townToggle;

        protected override void BeforeAppear()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;

            // Idempotent: BeforeAppear can run again before OnDisable ever does (see class doc).
            run.PhaseChanged -= OnPhaseChanged;
            run.PhaseChanged += OnPhaseChanged;

            if (locationGroup != null)
            {
                locationGroup.OnGroupChanged -= OnLocationSelectionChanged;
                locationGroup.OnGroupChanged += OnLocationSelectionChanged;
            }

            // Sync to current phase.
            OnPhaseChanged(run.Phase);
        }

        protected override void OnPanelDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            if (locationGroup != null)
                locationGroup.OnGroupChanged -= OnLocationSelectionChanged;
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            var inTown = phase == RunPhase.InTown;

            // In the field, only the Town toggle is interactable — the player
            // must Recall before choosing a new destination.
            foreach (var toggle in locationGroup.GetComponentsInChildren<LocationToggle>(true))
                toggle.interactable = inTown;

            if (townToggle != null)
                townToggle.interactable = !inTown;
        }

        /// <summary>
        /// Selecting a field location sends the hero there immediately. Fires on deselection
        /// too (<see cref="RadioGroup.ActivatedToggle"/> null) — that is not a destination,
        /// so it is ignored.
        /// </summary>
        private void OnLocationSelectionChanged()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            // Only send while in Town — in the field the toggles are non-interactable,
            // but guard against edge cases (e.g. phase change mid-frame).
            if (provider.Run.Phase != RunPhase.InTown) return;

            var toggle = locationGroup?.ActivatedToggle as LocationToggle;
            if (toggle == null || toggle.Location == null) return;

            provider.Send(toggle.Location);
        }
    }
}
