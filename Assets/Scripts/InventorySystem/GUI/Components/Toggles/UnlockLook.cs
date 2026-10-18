using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// How a toggle the Unlock graph drives (a Location, the Vendor) shows its state: Locked is the Selectable's
    /// own non-interactable look - no hover scale, no click, the disabled colour on the target graphic - and its
    /// child icons sit at <see cref="LockedIconAlpha"/> until it opens.
    /// </summary>
    internal static class UnlockLook
    {
        public const float LockedIconAlpha = 0.4f;

        public static void Apply(Selectable toggle, bool open)
        {
            toggle.interactable = open;

            var alpha = open ? 1f : LockedIconAlpha;

            foreach (var graphic in toggle.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == toggle.targetGraphic)
                    continue;

                var color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
        }
    }
}
