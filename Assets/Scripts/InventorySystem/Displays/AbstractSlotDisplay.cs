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
        [field: SerializeField, ReadOnly] public AbstractDimensionalContainer Container { get; private set; }
        [field: SerializeField, ReadOnly] public Vector2Int Position { get; private set; }
        [Space]
        [SerializeField] protected internal RectTransform itemDisplay;
        [SerializeField] protected internal Image icon;
        [SerializeField] protected internal TextMeshProUGUI amount;

        [SerializeField] protected internal TextMeshProUGUI debugPosition;

        protected static Package packageToMove;

        private bool hovering;

        private void OnEnable()
        {
            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.Debug ? Position.ToString() : "";
        }

        public void SetupSlot(AbstractDimensionalContainer container, Vector2Int position)
        {
            name = $"{position.x} | {position.y}";
            Position = position;
            Container = container;

            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.Debug ? Position.ToString() : "";
        }

        public void OnPointerClick(PointerEventData eventData) =>
            // TODO: if shift clicking try add to other container

            HandleItem(eventData);

        public void OnPointerExit(PointerEventData eventData)
        {
            StaticDragDisplay.Instance.SetHoveredSlot(null);

            FadeOutPreview();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StaticDragDisplay.Instance.SetHoveredSlot(this);

            FadeInPreview();
        }

        public void OnBeginDrag(PointerEventData eventData) => HandleItem(eventData);

        /// required for OnBeginDrag() to work... #ThanksUnity
        public void OnDrag(PointerEventData eventData) { }

        // OnEndDrag
        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor

        public void OnDrop(PointerEventData eventData) => DropItem();

        private void HandleItem(PointerEventData eventData)
        {
            // handle picking up one/all from a stack

            if (StaticDragDisplay.Instance.IsDragging)
                // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
                DropItem();
            else
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    // TODO: implement item.UseItem(); and move the following in there

                    if (this is EquipmentSlotDisplay)
                        UnequipItem();
                    else
                        EquipItem();
                }
                else
                    PickUpItem();

                void PickUpItem()
                {
                    var storedPositions = Container.GetStoredPackagePositionsAt(Position, new(1, 1));

                    if (storedPositions.Count == 1)
                    {
                        packageToMove = Container.storedPackages[storedPositions[0]];

                        StaticDragDisplay.Instance.SetPackage(this, packageToMove);

                        Container.RemoveItemAtPosition(storedPositions[0], packageToMove);
                    }

                    FadeOutPreview();
                }
            }
        }

        private void FadeInPreview()
        {
            hovering = true;

            var itemToDisplay = Container.GetStoredPackagePositionsAt(Position, new(1, 1));
            if (itemToDisplay.Count == 1)
                if (Container.storedPackages.TryGetValue(itemToDisplay[0], out var hoveredIten))
                    if (hoveredIten.Item != null && 0 < hoveredIten.Amount)
                        StartCoroutine(FadeIn(hoveredIten, itemToDisplay[0]));

            IEnumerator FadeIn(Package package, Vector2Int storedPosition)
            {
                var timeStamp = Time.time;

                while (hovering)
                {
                    yield return null;

                    var canFadeIn = 0.5f < Time.time - timeStamp;

                    if (canFadeIn && hovering)
                    {
                        StaticPrevievDisplay.Instance.SetPackage(package, storedPosition);
                        hovering = false;
                    }
                }
            }
        }

        private void FadeOutPreview()
        {
            var storedPositions = Container.GetStoredPackagePositionsAt(Position, new(1, 1));

            //if (0 < storedPositions.Count && storedPositions[0] != StaticPrevievDisplay.Instance.StoredPosition)
            {
                hovering = false;
                StaticPrevievDisplay.Instance.SetPackage(new Package(null, 0), new(-1, -1));
            }
            //if (container.storedPackages.TryGetValue(Position, out var hoveredIten))
            //    StopCoroutine(FadeIn(hoveredIten, storedPositions[0]));
        }


        protected internal virtual void DropItem() => FadeInPreview();

        protected internal virtual void UnequipItem() => FadeOutPreview();

        protected internal virtual void EquipItem() => FadeOutPreview();

        protected internal abstract void RefreshSlotDisplay(Package package);
    }
}
