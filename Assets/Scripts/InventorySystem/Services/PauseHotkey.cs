using Submodules.Utility.Bindings;
using System;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The pause key: each time <see cref="GameAction.TogglePause"/> performs, flips
    /// <see cref="ISimulationService.IsPaused"/>. Hosted by <see cref="GameLoop"/> like the simulation's
    /// tick, so it needs no scene object; the press lands with the frame's input, ahead of any tick,
    /// so it freezes the very frame it came in.
    ///
    /// <para>Whether the press does anything is the service's call, not this class's: pausing is
    /// refused unless a Run is in the Field, so in Town (and in the Go Venture preview, where no Run
    /// exists yet) the key is inert, and a paused Run that ends goes home unpaused.</para>
    ///
    /// <para>Space is also the EventSystem's Submit key, which presses whatever UI element is selected -
    /// after a click, the element just clicked (the pause toggle itself, Go Venture, Recall). Left alone,
    /// one Space would pause here and press that element too, undoing it on the toggle. So the selection
    /// is let go of at the end of every frame, except a text field's, which keeps its focus and its
    /// spaces. A click still works: a press is tracked by the pointer, not by the selection.</para>
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

        /// <summary>A <see cref="GameLoop"/> ticker; the delta is the loop's and goes unused.</summary>
        public void Tick(float deltaSeconds) => ReleaseUiSelection();

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

        // After the EventSystem has had its frame (GameLoop runs after every Update), so a Space pressed
        // next frame finds nothing selected to Submit.
        private static void ReleaseUiSelection()
        {
            var events = EventSystem.current;

            if (events != null && events.currentSelectedGameObject != null && !HoldsTextFocus(events))
                events.SetSelectedGameObject(null);
        }
    }
}
