using TeppichsTools.Creation;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ScreenRotator : MonoSingleton<ScreenRotator>
{
    private RectTransform transform;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    [SerializeField] private float screenWidth;
    [SerializeField] private float screenHeight;
    public static bool landscape;

    private void OnEnable()
    {
        transform = GetComponent<RectTransform>();
        canvas = transform.root.GetComponent<Canvas>();
        canvasScaler = transform.root.GetComponent<CanvasScaler>();
        transform.anchorMin = new(.5f, .5f);
        transform.anchorMax = new(.5f, .5f);
        transform.pivot = new(.5f, .5f);
    }

    private void Update()
    {
        screenWidth = Screen.width / canvas.scaleFactor;
        screenHeight = Screen.height / canvas.scaleFactor;

        landscape = Screen.height < Screen.width;
        canvasScaler.matchWidthOrHeight = landscape ? 1 : .5f;

        if (landscape)
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
            transform.sizeDelta = new(screenWidth, screenHeight);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0, 90);
            transform.sizeDelta = new(screenHeight, screenWidth);
        }
    }
}
