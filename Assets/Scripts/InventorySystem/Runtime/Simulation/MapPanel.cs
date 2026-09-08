using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The map panel (issue #27): a <see cref="RadioGroup"/> of <see cref="LocationToggle"/>
    /// items plus a plain "Town" <see cref="AbstractToggle" /> for recall. Clicking a field
    /// location toggle sends the hero there (<see cref="SimulationProvider.Send"/>); clicking
    /// the Town toggle recalls (<see cref="SimulationProvider.Recall"/>). No dedicated Send /
    /// Recall buttons — the toggles are the actions.
    ///
    /// While <see cref="RunPhase.InField"/>, only the Town toggle is interactable — the
    /// player must Recall before choosing a new destination. Fades in/out via the parent
    /// <see cref="MultiplePanelToggle"/> on the Switch Context button.
    ///
    /// <see cref="AbstractPanel.BeforeAppear"/> runs on every fade-in and the panel stays
    /// enabled between them, so <see cref="OnDisable"/> is not a reliable pair — a second
    /// appear without a teardown would otherwise stack a second <c>PhaseChanged</c> /
    /// <c>OnToggle</c> subscription and fire <see cref="SimulationProvider.Send"/> twice on one
    /// click. Every subscription here is therefore made idempotent (detach before attach).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapPanel : AbstractPanel
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

            // Subscribe to all location toggles for send-on-click.
            foreach (var toggle in locationGroup.GetComponentsInChildren<LocationToggle>(true))
            {
                toggle.OnToggle -= OnLocationToggled;
                toggle.OnToggle += OnLocationToggled;
            }

            if (townToggle != null)
            {
                townToggle.OnToggle -= OnTownToggled;
                townToggle.OnToggle += OnTownToggled;
            }

            // Sync to current phase.
            OnPhaseChanged(run.Phase);
        }

        protected override void OnAppear()
        {
            base.OnAppear();
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            foreach (var toggle in locationGroup.GetComponentsInChildren<LocationToggle>(true))
                toggle.OnToggle -= OnLocationToggled;

            if (townToggle != null)
                townToggle.OnToggle -= OnTownToggled;
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

        /// <summary>Clicking a field location toggle sends the hero there immediately.</summary>
        private void OnLocationToggled(bool isOn)
        {
            if (!isOn) return;

            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            // Only send while in Town — in the field the toggle is non-interactable,
            // but guard against edge cases (e.g. phase change mid-frame).
            if (provider.Run.Phase != RunPhase.InTown) return;

            var toggle = locationGroup?.ActivatedToggle as LocationToggle;
            if (toggle == null || toggle.Location == null) return;

            provider.Send(toggle.Location);
        }

        /// <summary>Clicking the Town toggle recalls the hero immediately.</summary>
        private void OnTownToggled(bool isOn)
        {
            if (!isOn) return;

            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            // Only recall while InField — in Town the toggle is non-interactable.
            if (provider.Run.Phase != RunPhase.InField) return;

            provider.Recall();
        }
    }
}
