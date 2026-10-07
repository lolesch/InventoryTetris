using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Combat panel's pause switch: pressed exactly while <see cref="ISimulationService.IsPaused"/>,
    /// and a click flips it. The Space bar (<see cref="PauseHotkey"/>) flips the same state, so this
    /// toggle never decides anything itself - it follows <see cref="ISimulationService.PausedChanged"/>,
    /// which is why <see cref="OnClick"/> asks the service instead of switching the toggle and
    /// <see cref="OnToggle"/> is empty.
    ///
    /// <para>Interactable only while a Run is <see cref="RunPhase.InField"/>: in Town and in the Go
    /// Venture preview there is no Run to freeze, and the service refuses a pause there anyway. Read
    /// every frame, not subscribed, because the Run is replaced on a hero load and a Death ends it
    /// without any button click.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseToggle : AbstractToggle
    {
        // The service this toggle subscribed to, so OnDisable lets go of that one: with domain reload
        // disabled the next Play entry arms a new service while this scene object lives on.
        private ISimulationService _simulation;

        protected override void OnClick()
        {
            if (_simulation != null)
                _simulation.SetPaused(!_simulation.IsPaused);
        }

        // The pressed state is the service's; nothing to request from here.
        protected override void OnToggle() { }

        // Subscribed here, not in Awake: a scene object survives a Play session, so an Awake
        // subscription would be gone from the second Play entry on (see codebase-notes.md).
        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying || !ServiceLocator.IsArmed)
                return;

            _simulation = SimulationService.Instance;
            _simulation.PausedChanged += SyncToPaused;

            SyncToPaused(_simulation.IsPaused);
            RefreshInteractable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_simulation == null)
                return;

            _simulation.PausedChanged -= SyncToPaused;
            _simulation = null;
        }

        private void Update()
        {
            RefreshInteractable();

            // A hero load replaces the World, and IsPaused with it, without raising PausedChanged.
            if (_simulation != null && IsOn != _simulation.IsPaused)
                SyncToPaused(_simulation.IsPaused);
        }

        private void SyncToPaused(bool paused) => SyncToggle(paused);

        private void RefreshInteractable()
        {
            if (_simulation == null)
                return;

            var inField = _simulation.Run.Phase == RunPhase.InField;
            if (interactable != inField)
                interactable = inField;
        }
    }
}
