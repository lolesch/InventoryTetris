using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Saves the hero when the game closes. A quit raises <see cref="Application.quitting"/>, and so does
    /// an Editor Stop; the handler ends a live Run first (a Recall banks what the hero holds, a hero
    /// already down takes its Death) and then writes, so closing the game mid-Run never throws the
    /// run away and never leaves a hero in the Field.
    ///
    /// The handler uses the services, so it is released on entering Edit Mode, after the scene's
    /// teardown, and not on <c>ExitingPlayMode</c> (docs/agents/codebase-notes.md, "Static state needs
    /// a SubsystemRegistration reset"). The boot installs it; a test of the logic calls
    /// <see cref="SaveHero"/> directly.
    /// </summary>
    public static class GameExit
    {
        private static Action quitHandler;

        public static bool IsInstalled => quitHandler != null;

        /// <summary>Attaches the quit handler once; installing again replaces it, never doubles it.</summary>
        public static void Install(ISimulationService simulation, IHeroSaveService saves)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));

            if (saves == null)
                throw new ArgumentNullException(nameof(saves));

            Release();

            quitHandler = () => SaveHero(simulation, saves);
            Application.quitting += quitHandler;

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        /// <summary>
        /// Ends a live Run and writes the hero. The Run's own end is a save point (<see cref="ISimulationService.RunSettled"/>),
        /// so a Run that was ended here has been written already; only a hero in Town is written by this call.
        /// Never throws: the game is closing, and a failure here is logged.
        /// </summary>
        public static void SaveHero(ISimulationService simulation, IHeroSaveService saves)
        {
            try
            {
                if (!simulation.LeaveField())
                    _ = saves.Save();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save the hero on exit: {exception.Message}");
            }
        }

        private static void Release()
        {
            if (quitHandler == null)
                return;

            Application.quitting -= quitHandler;
            quitHandler = null;
        }

        // Fires on every Play entry even with domain reload disabled, so a handler from the previous
        // session cannot survive into the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() => Release();

#if UNITY_EDITOR
        internal static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                Reset();
        }
#endif
    }
}
