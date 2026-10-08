using Submodules.Utility.Services;
using System.Linq;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// The dev-invocable hero load (#114) from the Editor menu, for a scene without the
    /// <c>DebugPanel</c> button: <see cref="DebugPanel.LoadDefault"/> builds a fresh Hero and World
    /// from the default hero template and swaps both into the Session. Play Mode only, and refused
    /// while a Run is in the Field - Recall first. <see cref="DebugPanel.CreateNew"/> is the same for a
    /// new saved hero: written at once, so it is a save from then on, which the default-hero load is not.
    /// </summary>
    internal static class LoadHeroMenu
    {
        private const string Menu = EditorMenus.Hero + "Load Default Hero";
        private const string NewMenu = EditorMenus.Hero + "New Saved Hero";
        private const string DeleteMenu = EditorMenus.Hero + "Delete Loaded Hero";

        [MenuItem(Menu)]
        private static void LoadDefaultHero() => DebugPanel.LoadDefault();

        [MenuItem(Menu, true)]
        private static bool CanLoadDefaultHero() => Application.isPlaying && ServiceLocator.IsArmed;

        [MenuItem(NewMenu)]
        private static void NewSavedHero() => DebugPanel.CreateNew();

        [MenuItem(NewMenu, true)]
        private static bool CanCreateNewHero() => Application.isPlaying && ServiceLocator.IsArmed;

        [MenuItem(DeleteMenu)]
        private static void DeleteLoadedHero()
        {
            var id = HeroSaveService.Instance.ActiveHeroId;
            var name = HeroSaveService.Instance.List().FirstOrDefault(hero => hero.Id == id).Name;

            if (EditorUtility.DisplayDialog("Delete the loaded hero",
                    $"Delete the save of '{name}'? This cannot be undone.", "Delete", "Cancel"))
                DebugPanel.DeleteLoaded();
        }

        // Only a hero that came from a save has one to delete.
        [MenuItem(DeleteMenu, true)]
        private static bool CanDeleteLoadedHero() =>
            Application.isPlaying && ServiceLocator.IsArmed && HeroSaveService.Instance.ActiveHeroId != null;
    }
}
