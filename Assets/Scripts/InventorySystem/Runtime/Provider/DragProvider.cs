using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Items;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.GUI.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class DragProvider : AbstractProvider<DragProvider>
    {
        [SerializeField] private SimplePanel itemPanel;
        [SerializeField] private ItemDisplay itemDisplay;

        [SerializeField] private Image background;
        [SerializeField] private Color initialColor;

        public AbstractSlotDisplay Origin { get; private set; }
        public AbstractSlotDisplay Hovered { get; private set; }
        public Package DraggingPackage { get; private set; }
        public Vector2Int PositionOffset { get; private set; }

        public event Action<List<Vector2Int>> OnOverlapping;
        public bool IsDragging => itemPanel.IsActive;
        private RectTransform ItemTransform => itemDisplay.transform as RectTransform;

        private void Start()
        {
            /// anchor to BottomLeft to match screen/mouse coordinates
            if (itemDisplay)
            {
                (itemDisplay.transform as RectTransform).anchorMin = Vector2.zero;
                (itemDisplay.transform as RectTransform).anchorMax = Vector2.zero;
            }

            if (background)
                initialColor = background.color;

            if (itemPanel)
                itemPanel.FadeOut();
        }

        private void Update()
        {
            if (IsDragging)
            {
                SetToMousePosition();

                HighlightOverlappingSlots();
            }
        }

        private void SetToMousePosition() => ItemTransform.anchoredPosition = (Vector2)Input.mousePosition / ItemTransform.lossyScale;

        private void HighlightOverlappingSlots()
        {
            if (Hovered == null || !DraggingPackage.IsValid)
                return;

            /// The pivot is the mouse position within the items dimensions
            var positionPivot = ItemTransform.pivot;
            positionPivot *= AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions);

            var positionDiff = new Vector2Int(Mathf.FloorToInt(positionPivot.x), Mathf.FloorToInt(positionPivot.y));
            positionDiff -= new Vector2Int(0, AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions).y - 1);
            positionDiff.y *= -1;

            if (Hovered.Container == null)
                return;

            var positionToAdd = Hovered.Position - positionDiff;

            var storedPositions = Hovered.Container?.GetStoredItemsAt(positionToAdd, AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions));

            if (background)
                background.color = storedPositions.Count switch
                {
                    0 => initialColor,
                    1 => initialColor * Color.yellow,
                    _ => initialColor * Color.red,
                };

            OnOverlapping?.Invoke(storedPositions);
            //TODO: invoke an event each time the drag display is entering new overlapping positions
            // each slotDisplay will listen to this event and color its background based on the overlapping result

            //var requiredPositions = Hovered.Container.CalculateRequiredPositions(positionToAdd, Package.Item.Dimensions);
            //
            //var usedPositions = new List<Vector2Int>();
            //for (var i = 0; i < storedPositions.Count; i++)
            //    for (var x = 0; x < Package.Item.Dimensions.x; x++)
            //        for (var y = 0; y < Package.Item.Dimensions.y; y++)
            //            usedPositions.Add(new Vector2Int(x, y));

            //var emptyPositions = requiredPositions.Except(usedPositions);

            //var overlappingPositions = requiredPositions.Intersect(usedPositions);
        }

        public void SetPackage(AbstractSlotDisplay slot, Package package, Vector2Int positionOffset)
        {
            Origin = slot;
            Hovered = slot;
            DraggingPackage = package;
            PositionOffset = positionOffset;

            if (!DraggingPackage.IsValid)
            {
                Debug.LogWarning($"Dragging package is invalid");
                itemPanel.FadeOut();
                return;
            }

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);
            SetPosition(dimensions);

            SetToMousePosition();

            itemDisplay.RefreshDisplay(package);

            itemPanel.FadeIn();
        }

        private void SetPosition(Vector2Int dimensions)
        {
            ItemTransform.sizeDelta = dimensions * 60; // slotSize

            var pointerRelativeToOrigin = (Vector2)(Input.mousePosition - Origin.transform.position) / transform.lossyScale;

            /// pointerPosition is in pixelCoordinates anchored TopLeft
            var pivot = pointerRelativeToOrigin / 60; // slotSize
            /// convert to screenCoordinates anchored BottomLeft
            pivot.y += 1;
            /// scale to match item dimensions
            pivot /= dimensions;

            /// The positionOffset was calculated in InventorySpace (anchored TopLeft)
            var positionOffset = PositionOffset;
            positionOffset.y -= dimensions.y - 1;
            /// convert to screenCoordinates anchored BottomLeft
            positionOffset.y *= -1;

            var positionPivot = (Vector2)positionOffset;
            positionPivot /= dimensions;

            ItemTransform.pivot = pivot + positionPivot;
        }

        public void SetHoveredSlot(AbstractSlotDisplay slot) => Hovered = slot;

        //public void ReturnToOrigin(Package package)
        //{
        //    // tell teh origin to add this package back to its position
        //}

        //public void DropHere()
        //{
        //    // tell the origin to remove the package
        //    //packageOrigin.container.RemoveItemAtPosition(packageOrigin.Position, Package);
        //    // then tell the target to add the package
        //}

        //public void OnEndDrag(PointerEventData eventData) =>
        //    // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        //    throw new System.NotImplementedException();

        //public void OnPointerClick(PointerEventData eventData) =>
        //    // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        //    throw new System.NotImplementedException();
    }
}
