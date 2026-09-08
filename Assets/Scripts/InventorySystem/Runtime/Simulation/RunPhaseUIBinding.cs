using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Bridges <see cref="RunState.PhaseChanged"/> to a <see cref="MultiplePanelToggle"/>:
    /// InTown → toggle ON (shop/inventory panels), InField → toggle OFF (combat panels).
    /// Attach to the same GameObject as the Switch Context toggle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPhaseUIBinding : MonoBehaviour
    {
        [SerializeField] private MultiplePanelToggle switchContext;

        private void OnEnable()
        {
            var run = SimulationProvider.Instance?.Run;
            if (run == null) return;

            run.PhaseChanged += OnPhaseChanged;

            // Sync to current phase on enable.
            OnPhaseChanged(run.Phase);
        }

        private void OnDisable()
        {
            var run = SimulationProvider.Instance?.Run;
            if (run != null)
                run.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            if (switchContext != null)
                switchContext.SetToggle(phase == RunPhase.InTown);
        }
    }
}
