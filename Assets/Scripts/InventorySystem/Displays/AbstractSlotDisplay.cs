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

        protected internal AbstractDimensionalContainer container;

        public abstract void OnPointerClick(PointerEventData eventData);

        public abstract void OnBeginDrag(PointerEventData eventData);

        public abstract void OnDrag(PointerEventData eventData);

        public abstract void OnDrop(PointerEventData eventData);

        internal protected abstract void PickUpItem();

        internal protected abstract void DropItem();

        internal protected abstract void RefreshSlotDisplay(Package package);

    }
}
