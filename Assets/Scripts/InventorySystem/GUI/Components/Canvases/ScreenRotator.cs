using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Canvases
{
    public class ScreenRotator : RootCanvas
    {
        private void Update()
        {
            if (Screen.orientation != orientation)
            {
                orientation = Screen.orientation;

                if (Screen.width < Screen.height)
                    Screen.orientation = ScreenOrientation.LandscapeRight;
                else
                    Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }
    }
}
