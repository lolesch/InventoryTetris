using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Canvases
{
    [RequireComponent(typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Canvas))]
    public class RootCanvas : MonoBehaviour
    {
        [SerializeField, ReadOnly] protected Canvas canvas;
        [SerializeField, ReadOnly] protected CanvasScaler scaler;
        [Space]
        [SerializeField] protected ScreenOrientation orientation = ScreenOrientation.AutoRotation;

        public Canvas Canvas => canvas != null ? canvas : canvas = GetComponent<Canvas>();

        public CanvasScaler Scaler => scaler != null ? scaler : scaler = GetComponent<CanvasScaler>();

        private void OnValidate()
        {
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Scaler.matchWidthOrHeight = 1f;
            Scaler.referenceResolution = new(1920, 1080);
        }
    }
}
