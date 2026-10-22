using Submodules.Utility.Persistence;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// Editor-only tools for the hero saves (#171): wipe them, open the folder, and a Start Fresh Each Play
    /// toggle. The toggle lives in <c>EditorPrefs</c>, so it is per machine and no asset changes; the boot
    /// reads it (<see cref="GameBoot.StartFreshEachPlay"/>). A player build has none of this.
    /// </summary>
    internal static class HeroSavesMenu
    {
        private const string Root = EditorMenus.HeroSaves;
        private const string WipeMenu = Root + "Wipe All Hero Saves";
        private const string OpenMenu = Root + "Open Saves Folder";
        private const string FreshMenu = Root + "Start Fresh Each Play";

        [MenuItem(WipeMenu, false, 100)]
        private static void WipeAll()
        {
            if (!EditorUtility.DisplayDialog("Wipe all hero saves",
                    $"Delete every hero save and the Account file in\n{SaveLocation.Folder()}?\n\nThis cannot be undone.",
                    "Wipe", "Cancel"))
                return;

            var deleted = SaveLocation.WipeAll(new FileSaveStore(SaveLocation.Folder()));
            Debug.Log($"Wiped {deleted} hero save file(s). The next Play creates a fresh hero.");
        }

        // A wipe under a running game would leave the live hero saving over nothing it loaded.
        [MenuItem(WipeMenu, true)]
        private static bool CanWipeAll() => !Application.isPlaying;

        [MenuItem(OpenMenu, false, 101)]
        private static void OpenFolder()
        {
            var folder = SaveLocation.Folder();
            _ = System.IO.Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem(FreshMenu, false, 102)]
        private static void ToggleStartFresh() =>
            EditorPrefs.SetBool(GameBoot.StartFreshKey, !GameBoot.StartFreshEachPlay);

        [MenuItem(FreshMenu, true)]
        private static bool CheckStartFresh()
        {
            Menu.SetChecked(FreshMenu, GameBoot.StartFreshEachPlay);
            return true;
        }
    }
}
