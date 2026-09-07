using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The map panel (issue #27): a <see cref="RadioGroup"/> of <see cref="LocationToggle"/>
    /// items plus a <b>Send</b> button (InTown → InField) and a <b>Recall</b> button
    /// (InField → InTown). Subscribes to <see cref="RunState.PhaseChanged"/> to show the
    /// appropriate pair: Send while InTown, Recall while InField. The hero-down state is
    /// handled by <see cref="SimulationProvider.HandleHeroDeath"/> in the driver — no UI
    /// needed here.
    ///
    /// Location selection is live: the player picks a toggle on the RadioGroup, then taps
    /// Send. The panel reads <see cref="LocationToggle.Location"/> off the
    /// <see cref="RadioGroup.ActivatedToggle"/> and hands it to
    /// <see cref="SimulationProvider.Send"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapPanel : MonoBehaviour
    {
        [SerializeField] private RadioGroup locationGroup;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button recallButton;

        private void OnEnable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;
            run.PhaseChanged += OnPhaseChanged;

            sendButton.onClick.AddListener(OnSend);
            recallButton.onClick.AddListener(OnRecall);

            // Sync to current phase.
            OnPhaseChanged(run.Phase);
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            sendButton.onClick.RemoveListener(OnSend);
            recallButton.onClick.RemoveListener(OnRecall);
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            var inTown = phase == RunPhase.InTown;
            SetInteractable(sendButton, inTown);
            SetInteractable(recallButton, !inTown);
        }

        private void OnSend()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var toggle = locationGroup?.ActivatedToggle as LocationToggle;
            if (toggle == null || toggle.Location == null)
                return;

            provider.Send(toggle.Location);
        }

        private void OnRecall()
        {
            SimulationProvider.Instance?.Recall();
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }
    }
}
