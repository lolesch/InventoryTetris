using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// The dev-invocable hero load (#114) from the Editor menu, for a scene without the
    /// <c>DebugPanel</c> button: <see cref="DebugPanel.LoadDefault"/> builds a fresh Hero and World
    /// from the default hero template and swaps both into the Session. Play Mode only, and refused
    /// while a Run is in the Field - Recall first.
    /// </summary>
    internal static class LoadHeroMenu
    {
        private const string Menu = "ToolSmiths/Hero/Load Default Hero";

        [MenuItem(Menu)]
        private static void LoadDefaultHero() => DebugPanel.LoadDefault();

        [MenuItem(Menu, true)]
        private static bool CanLoadDefaultHero() => Application.isPlaying && ServiceLocator.IsArmed;
    }
}
