using System.Collections.Generic;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Disables the Store and Stash toggles while the hero is <see cref="RunPhase.InField"/>
    /// and re-enables them on <see cref="RunPhase.InTown"/> (issue #27 acceptance criteria).
    /// Inventory, equipment, and character panels stay live in both states — only Store and
    /// Stash are gated.
    ///
    /// Subscribes to <see cref="RunState.PhaseChanged"/> for instant response. On disable,
    /// the toggles are re-enabled so a teardown race never leaves them stuck.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreStashPhaseBinding : MonoBehaviour
    {
        [Tooltip("The Store and Stash toggles to disable in the Field. Inventory/equipment/character panels are NOT listed here — they stay live.")]
        [SerializeField] private List<AbstractToggle> fieldDisabledToggles = new();

        private void OnEnable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;
            run.PhaseChanged += OnPhaseChanged;

            // Sync to current phase.
            OnPhaseChanged(run.Phase);
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            // Re-enable all toggles on teardown so they are not stuck disabled.
            SetAllInteractable(true);
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            SetAllInteractable(phase == RunPhase.InTown);
        }

        private void SetAllInteractable(bool interactable)
        {
            foreach (var toggle in fieldDisabledToggles)
            {
                if (toggle != null)
                    toggle.interactable = interactable;
            }
        }
    }
}
