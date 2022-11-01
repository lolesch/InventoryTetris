using System.Runtime.CompilerServices;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public abstract class AbstractSlotDisplay : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IDropHandler
    {
        [SerializeField] internal protected RectTransform itemDisplay;
        [SerializeField] internal protected Image icon;
        [SerializeField] internal protected TextMeshProUGUI amount;

        public Vector2Int Position;

        protected static Package packageToMove;

        protected internal AbstractDimensionalContainer container;

        public void OnPointerClick(PointerEventData eventData)
        {
            // if shift clicking try add to other container

            // handle picking up one/all from a stack

            if (!StaticDragDisplay.Instance.IsDragging)
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    if (this is not EquipmentSlotDisplay)
                        EquipItem();
                    else
                        UnequipItem();
                }
                else
                    PickUpItem();
            else
                // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
                DropItem();
        }

        public void OnBeginDrag(PointerEventData eventData) => OnPointerClick(eventData);

        public void OnDrag(PointerEventData eventData) { }

        public void OnDrop(PointerEventData eventData) => DropItem();

        // OnEndDrag
        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor

        internal protected abstract void PickUpItem();

        internal protected abstract void DropItem();

        internal protected abstract void UnequipItem();

        internal protected abstract void EquipItem();

        internal protected abstract void RefreshSlotDisplay(Package package);
    }
}
