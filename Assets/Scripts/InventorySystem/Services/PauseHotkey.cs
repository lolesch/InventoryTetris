using Submodules.Utility.Bindings;
using System;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The pause key: each time <see cref="GameAction.TogglePause"/> performs, flips
    /// <see cref="ISimulationService.IsPaused"/>. It subscribes in the constructor and lives as long as
    /// the input service it listens to, so it needs no scene object and no tick; the press lands with
    /// the frame's input, ahead of the simulation's tick, so it freezes the very frame it came in.
    ///
    /// <para>Whether the press does anything is the service's call, not this class's: pausing is
    /// refused unless a Run is in the Field, so in Town (and in the Go Venture preview, where no Run
    /// exists yet) the key is inert, and a paused Run that ends goes home unpaused.</para>
    ///
    /// <para>A text field that has focus keeps its spaces. Space is not the UI module's Submit key
    /// (<c>InputSystemUIInputModule</c> submits on Enter and the pad), so the selection needs no
    /// clearing to keep the key off the element last clicked.</para>
    /// </summary>
    internal sealed class PauseHotkey
    {
        private readonly ISimulationService simulation;

        public PauseHotkey(ISimulationService simulation, IInputService<GameAction> input)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            // Held for the life of the boot: the input service is replaced with it, never outlived.
            input.Performed += OnPerformed;
        }

        private void OnPerformed(GameAction action)
        {
            // A space typed into a text field is the field's, not the pause key's.
            if (action == GameAction.TogglePause && !HoldsTextFocus(EventSystem.current))
                simulation.SetPaused(!simulation.IsPaused);
        }

        /// <summary>Whether the selected element takes keyboard input of its own (a text field).</summary>
        internal static bool HoldsTextFocus(EventSystem events) =>
            events != null && events.currentSelectedGameObject != null
            && events.currentSelectedGameObject.TryGetComponent<IUpdateSelectedHandler>(out _);
    }
}
