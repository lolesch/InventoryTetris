using Submodules.Utility.Bindings;
using System;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The Recall key: each time <see cref="GameAction.Recall"/> performs, calls
    /// <see cref="ISimulationService.Recall"/> if <see cref="ISimulationService.CanRecall"/> says it will not be refused. Like <see cref="PauseHotkey"/> it lives as long as the input service, with no scene object and no tick.
    /// A menu's hold is not the player's to spend a scroll under, and a text field that has focus keeps its letters.
    /// </summary>
    internal sealed class RecallHotkey
    {
        private readonly ISimulationService simulation;

        public RecallHotkey(ISimulationService simulation, IInputService<GameAction> input)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            // Held for the life of the boot: the input service is replaced with it, never outlived.
            input.Performed += OnPerformed;
        }

        private void OnPerformed(GameAction action)
        {
            if (action == GameAction.Recall && !simulation.IsMenuFrozen
                && !PauseHotkey.HoldsTextFocus(EventSystem.current) && simulation.CanRecall)
                simulation.Recall();
        }
    }
}
