using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The pause key: each frame it is pressed, flips <see cref="ISimulationService.IsPaused"/>. Hosted
    /// by <see cref="GameLoop"/> like the simulation's tick, so it needs no scene object, and added
    /// ahead of that tick so a press freezes the very frame it lands on.
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
        private readonly Func<bool> pressed;

        /// <param name="pressed">Whether the key went down this frame. Defaults to the Space bar;
        /// a test passes its own.</param>
        public PauseHotkey(ISimulationService simulation, Func<bool> pressed = null)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.pressed = pressed ?? (() => Input.GetKeyDown(KeyCode.Space));
        }

        /// <summary>A <see cref="GameLoop"/> ticker; the delta is the loop's and goes unused.</summary>
        public void Tick(float deltaSeconds)
        {
            // A space typed into a text field is the field's, not the pause key's.
            if (pressed() && !HoldsTextFocus(EventSystem.current))
                simulation.SetPaused(!simulation.IsPaused);

            ReleaseUiSelection();
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
