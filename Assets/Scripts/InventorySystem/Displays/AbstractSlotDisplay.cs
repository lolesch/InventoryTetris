using System.Collections;
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
    public abstract class AbstractSlotDisplay : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] protected internal RectTransform itemDisplay;
        [SerializeField] protected internal Image icon;
        [SerializeField] protected internal TextMeshProUGUI amount;

        public Vector2Int Position;

        protected static Package packageToMove;

        protected internal AbstractDimensionalContainer container;
        private bool hovering;

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

        public void OnPointerExit(PointerEventData eventData) => FadeOutPreview();

        public void OnPointerEnter(PointerEventData eventData) => FadeInPreview();

        private void FadeInPreview()
        {
            hovering = true;

            var itemToDisplay = container.GetStoredPackagesAtPosition(Position, new(1, 1));
            if (itemToDisplay.Count == 1)
                if (container.storedPackages.TryGetValue(itemToDisplay[0], out var hoveredIten))
                {
                    // store position
                    if (hoveredIten.Item != null && 0 < hoveredIten.Amount)
                        StartCoroutine(FadeIn(hoveredIten));
                }
        }

        private void FadeOutPreview()
        {
            //TODO: store position that is previewed
            //      if GetStoredPackagesAtPosition(Position, new(1, 1)) != stored position
            //      => fade out

            hovering = false;
            StaticPrevievDisplay.Instance.SetPackage(new Package(null, 0));

            if (container.storedPackages.TryGetValue(Position, out var hoveredIten))
                StopCoroutine(FadeIn(hoveredIten));
        }

        private IEnumerator FadeIn(Package package)
        {
            var timeStamp = Time.time;

            while (hovering)
            {
                yield return null;

                var canFadeIn = 0.5f < Time.time - timeStamp;

                if (canFadeIn && hovering)
                {
                    StaticPrevievDisplay.Instance.SetPackage(package);
                    hovering = false;
                }
            }
        }

        // OnEndDrag
        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor

        protected internal virtual void PickUpItem() => FadeOutPreview();

        protected internal virtual void DropItem() => FadeInPreview();

        protected internal virtual void UnequipItem() => FadeOutPreview();

        protected internal virtual void EquipItem() => FadeOutPreview();

        protected internal abstract void RefreshSlotDisplay(Package package);
    }
}
