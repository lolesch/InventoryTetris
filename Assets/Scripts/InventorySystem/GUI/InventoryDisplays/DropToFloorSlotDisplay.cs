using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class DropToFloorSlotDisplay : AbstractSlotDisplay
    {
        protected override void DropItem()
        {
            DragProvider.Instance.SetPackage(this, new Package(Container, null, 0), Vector2Int.zero);

            DragProvider.Instance.Origin.Container?.InvokeRefresh();
        }
    }
}
