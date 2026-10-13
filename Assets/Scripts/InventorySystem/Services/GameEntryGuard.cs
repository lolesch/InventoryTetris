using Submodules.Utility.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Keeps a built player out of the game scene with a hero that is not from a save. The player opens on the
    /// Main Menu and only Play enters the game, so this fires when something else got there (a build order
    /// that put the game first, a developer shortcut): the Session's template hero would play a Run that
    /// <see cref="IHeroSaveService.Save"/> refuses to write. It sends the player to the menu to pick or
    /// create a hero. The Editor skips it, so Play from the game scene stays a developer shortcut.
    /// </summary>
    public static class GameEntryGuard
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Example";

        private static bool installed;

        /// <summary>Starts watching scene loads for the rest of this boot; a Play entry installs it again, never twice.</summary>
        public static void Install(bool builtPlayer)
        {
            Release();

            if (!builtPlayer)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            installed = true;
        }

        /// <summary>The scene to load instead of <paramref name="sceneName"/>, or <c>null</c> to let it start.</summary>
        public static string Redirect(string sceneName, bool builtPlayer, IHeroSaveService saves) =>
            builtPlayer && sceneName == GameScene && saves.ActiveHeroId == null ? MenuScene : null;

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ServiceLocator.IsArmed)
                return;

            var target = Redirect(scene.name, builtPlayer: true, ServiceLocator.Get<IHeroSaveService>());

            if (target != null)
                SceneManager.LoadScene(target);
        }

        private static void Release()
        {
            if (!installed)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            installed = false;
        }

        // Fires on every Play entry even with domain reload disabled, so a handler from the previous session
        // cannot survive into the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() => Release();
    }
}
