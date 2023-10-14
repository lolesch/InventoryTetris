using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(CanvasGroup))]
    public class DropToFloorSlotDisplay : AbstractSlotDisplay
    {
        private CanvasGroup canvasGroup;
        public CanvasGroup CanvasGroup => canvasGroup != null ? canvasGroup : canvasGroup = GetComponent<CanvasGroup>();
        protected override void DropItem(Package package)
        {
            DragProvider.Instance.SetPackage(this, new Package(), Vector2Int.zero);

            DragProvider.Instance.Origin.Container?.InvokeRefresh();
        }

        protected override void MoveItem(PointerEventData eventData) { }

        private void Update() => CanvasGroup.interactable = DragProvider.Instance.IsDragging;
    }
}
