using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// The dev-invocable hero load (#114): builds a fresh Hero and World from the default hero
    /// template and swaps both into the Session, as a load of a saved hero will. Play Mode only, and
    /// refused while a Run is in the Field (<see cref="ISession.TryLoad"/>) - Recall first. #118's
    /// <c>DebugPanel</c> is where a button for it belongs once that exists.
    /// </summary>
    internal static class LoadHeroMenu
    {
        private const string Menu = "ToolSmiths/Hero/Load Default Hero";

        [MenuItem(Menu)]
        private static void LoadDefaultHero()
        {
            if (Session.Instance.TryLoad(GameBoot.Load().DefaultHero))
                Debug.Log("Loaded a new Hero and World from the default hero.");
            else
                Debug.LogWarning("A Run is in the Field: Recall before loading a hero.");
        }

        [MenuItem(Menu, true)]
        private static bool CanLoadDefaultHero() => Application.isPlaying && Submodules.Utility.Services.ServiceLocator.IsArmed;
    }
}
