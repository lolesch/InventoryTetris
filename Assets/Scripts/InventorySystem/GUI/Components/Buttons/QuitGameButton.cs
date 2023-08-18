using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    public class QuitGameButton : AbstractButton
    {
        protected override void OnClick()
        {
            // TODO
            // - implement a confirmationPrompt
            // - save progression?

            Debug.LogWarning("Quitting the game".Colored(Color.red));

#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#elif UNITY_WEBPLAYER
            Application.OpenURL(webplayerQuitURL);
#else
            Application.Quit();
#endif
        }
    }
}
