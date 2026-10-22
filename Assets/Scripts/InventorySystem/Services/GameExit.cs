using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Saves the hero when the game closes. A quit raises <see cref="Application.quitting"/>, and so does
    /// an Editor Stop; the handler ends a live Run first (a Return banks what the hero holds, a hero
    /// already down takes its Death) and then writes, so closing the game mid-Run never throws the
    /// run away and never leaves a hero in the Field.
    ///
    /// The Pause Menu's Quit Game ends the application through <see cref="Quitter"/> once the hero is written,
    /// and is not offered where <see cref="CanQuit"/> says quitting does nothing. After a failed write its
    /// Exit without saving ends the application through <see cref="QuitWithoutSaving"/>, which skips this save.
    ///
    /// The handler uses the services, so it is released on entering Edit Mode, after the scene's
    /// teardown, and not on <c>ExitingPlayMode</c> (docs/agents/codebase-notes/play-mode-and-assemblies.md,
    /// "Static state needs a SubsystemRegistration reset"). The boot installs it; a test of the logic calls
    /// <see cref="SaveHero"/> directly.
    /// </summary>
    public static class GameExit
    {
        private static Action quitHandler;
        private static bool discarding;

        public static bool IsInstalled => quitHandler != null;

        /// <summary>What ending the application does; a test swaps it for a recorder, since a real quit would end the run.</summary>
        public static Action Quitter { get; set; } = QuitApplication;

        /// <summary>Whether a menu offers a quit: not in a browser, where <see cref="Application.Quit"/> does nothing.</summary>
        public static bool CanQuit(RuntimePlatform platform) => platform != RuntimePlatform.WebGLPlayer;

        /// <summary>
        /// Ends the application for a player who chose Exit without saving after a failed write: the quit save
        /// is skipped, so the last save stays as it is. Lasts until the next <see cref="Install"/> or reset.
        /// </summary>
        public static void QuitWithoutSaving()
        {
            discarding = true;
            Quitter();
        }

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
        /// Never throws: the game is closing, and a failure here is logged. Does nothing after <see cref="QuitWithoutSaving"/>.
        /// </summary>
        public static void SaveHero(ISimulationService simulation, IHeroSaveService saves)
        {
            if (discarding)
                return;

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

        // Application.Quit does nothing in the Editor, so there it stops Play Mode, as the Utility's QuitGameButton does.
        private static void QuitApplication()
        {
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private static void Release()
        {
            discarding = false;

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
