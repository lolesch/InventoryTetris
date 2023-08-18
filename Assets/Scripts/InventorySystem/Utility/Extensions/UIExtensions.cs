using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class UIExtensions
    {
        /// <summary>
        /// Returns true if the passed position extends the screen's current resolution
        /// </summary>
        /// <param characterName="position"></param>
        /// <remarks>
        /// Make sure that the position is calculated with the transform's lossyScale in mind.
        /// </remarks>
        public static bool IsOutsideOfGameWindow(Vector2 position) =>
            position.x < 0 ||
            position.x > Screen.width ||
            position.y < 0 ||
            position.y > Screen.height;

        /// <summary>
        /// Forces an immediate rebuild of the layout element and child layout elements affected by the calculations.
        /// </summary>
        /// <remarks>
        /// Usage should be restricted to cases where multiple layout passes are unavaoidable despite the extra cost in performance!
        /// </remarks>
        public static void RefreshContentFitter(this RectTransform transform)
        {
            if (transform == null || !transform.gameObject.activeSelf)
                return;

            foreach (RectTransform child in transform)
                RefreshContentFitter(child);

            if (transform.TryGetComponent(out LayoutGroup layoutGroup))
            {
                layoutGroup.SetLayoutHorizontal();
                layoutGroup.SetLayoutVertical();
            }

            if (transform.GetComponent<ContentSizeFitter>())
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform);
        }

    }
}