using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    /// <summary>
    /// Makes the sibling <see cref="Image"/> ignore pointer events on transparent pixels.
    /// <see cref="Image.alphaHitTestMinimumThreshold"/> has no Inspector field, so this exposes it.
    /// The sprite's texture needs Read/Write enabled and its mesh type set to Full Rect.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class AlphaHitTest : MonoBehaviour
    {
        [SerializeField, ReadOnly] private Image image = null;
        private Image Image => image ? image : image = GetComponent<Image>();

        [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.1f;

        // OnDisable also runs when the component or its GameObject is destroyed,
        // so removing the component restores the default rect-only hit test.
        private void Awake()
        {
            if (Image.sprite && !Image.sprite.texture.isReadable)
                Debug.LogWarning($"{nameof(AlphaHitTest)}: texture '{Image.sprite.texture.name}' needs Read/Write enabled.", this);

            Image.alphaHitTestMinimumThreshold = minimumAlpha;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Image && isActiveAndEnabled)
                Image.alphaHitTestMinimumThreshold = minimumAlpha;
        }
#endif // UNITY_EDITOR
    }
}
