using UnityEngine.SceneManagement;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Holds the simulation still while the Main Menu scene is the active one. The game loop keeps ticking
    /// across scenes and the hero a player left the game with is still loaded on the menu, so without this he
    /// would go on regenerating out of the game. Only the scene decides: Town and the Field are never held here.
    /// </summary>
    public static class MenuSceneHold
    {
        private static ISimulationService simulation;

        /// <summary>Starts following the active scene for the rest of this boot; a Play entry installs it again, never twice.</summary>
        public static void Install(ISimulationService service)
        {
            Release();

            simulation = service;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Apply(SceneManager.GetActiveScene());
        }

        /// <summary>Whether <paramref name="sceneName"/> is out of the game.</summary>
        public static bool IsOutOfGame(string sceneName) => sceneName == GameEntryGuard.MenuScene;

        private static void OnActiveSceneChanged(Scene previous, Scene next) => Apply(next);

        private static void Apply(Scene scene) => simulation.SetOutOfGame(IsOutOfGame(scene.name));

        private static void Release()
        {
            if (simulation == null)
                return;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            simulation = null;
        }

        // Fires on every Play entry even with domain reload disabled, so a handler from the previous session
        // cannot survive into the next.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() => Release();
    }
}
