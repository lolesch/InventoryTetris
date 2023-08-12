using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class DropToFloorSlotDisplay : AbstractSlotDisplay
    {
        protected override void DropItem()
        {
            StaticDragDisplay.Instance.SetPackage(this, new Package(null, 0), Vector2Int.zero);

            StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
        }
    }
}
