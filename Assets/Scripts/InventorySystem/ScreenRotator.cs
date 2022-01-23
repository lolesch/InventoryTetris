using TeppichsTools.Creation;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ScreenRotator : MonoSingleton<ScreenRotator>
{
    private void Update()
    {
        if (Screen.width < Screen.height)
            Screen.orientation = ScreenOrientation.LandscapeRight;
        else
            Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
