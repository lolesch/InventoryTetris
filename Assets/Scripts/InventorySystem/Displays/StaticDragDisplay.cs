using System;
using System.Collections.Generic;
using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class StaticDragDisplay : MonoSingleton<StaticDragDisplay>
    {
        public bool IsDragging => itemDisplay.gameObject.activeSelf;

        [SerializeField] private RectTransform itemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private Image background;
        [SerializeField] private Color initialColor;
        [SerializeField] private TextMeshProUGUI amount;

        private Canvas rootCanvas;

        public AbstractSlotDisplay Origin;
        public AbstractSlotDisplay Hovered;

        public event Action<List<Vector2Int>> OnOverlapping;

        // might not need this after reworking the dropItem();
        public Package Package;

        private void Awake()
        {
            name = "StaticDragDisplay";

            _ = transform.root.TryGetComponent(out rootCanvas);

            itemDisplay.gameObject.SetActive(false);

            if (background)
                initialColor = background.color;
        }

        private void Update()
        {
            if (IsDragging)
            {
                MoveDragDisplay();

                HighlightOverlappingSlots();
            }

            void MoveDragDisplay()
            {
                /// anchor to BottomLeft to match screen/mouse coordinates
                itemDisplay.anchorMin = Vector2.zero;
                itemDisplay.anchorMax = Vector2.zero;

                itemDisplay.anchoredPosition = (Vector2)Input.mousePosition / rootCanvas.scaleFactor;
            }

            void HighlightOverlappingSlots()
            {
                if (Hovered == null || Package.Item == null)
                    return;

                ///NOTE: the display has the Package.Item's dimensions and the pivot is the mouse position within these dimensions
                /// get the displays pivot
                var positionPivot = itemDisplay.pivot;
                positionPivot.x *= AbstractItem.GetDimensions(Package.Item.Dimensions).x;
                positionPivot.y *= AbstractItem.GetDimensions(Package.Item.Dimensions).y;

                var positionDiff = new Vector2Int(Mathf.FloorToInt(positionPivot.x), Mathf.FloorToInt(positionPivot.y));
                positionDiff -= new Vector2Int(0, AbstractItem.GetDimensions(Package.Item.Dimensions).y - 1);
                positionDiff.y *= -1;

                var positionToAdd = Hovered.Position - positionDiff;

                var storedPositions = Hovered.Container.GetOtherItemsAt(positionToAdd, AbstractItem.GetDimensions(Package.Item.Dimensions));

                if (storedPositions.Count <= 0)
                {
                    if (background)
                        background.color = initialColor;
                    return;
                }

                if (background)
                    background.color = (storedPositions.Count == 1) ? initialColor * Color.yellow : initialColor * Color.red;

                //TODO: invoke an event each time the drag display is entering new overlapping positions
                // each slotDisplay will listen to this event and color its background based on the overlapping result
                OnOverlapping?.Invoke(storedPositions);

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
        }

        public void SetPackage(AbstractSlotDisplay slot, Package package, Vector2Int positionOffset)
        {
            Origin = slot;
            Package = package;

            if (package.Item == null || package.Amount <= 0)
            {
                itemDisplay.gameObject.SetActive(false);
                return;
            }

            SetHoveredSlot(Origin);

            RefreshDisplay(Package);

            void RefreshDisplay(Package package)
            {
                SetPosition(package);

                if (icon)
                    icon.sprite = package.Item.Icon;

                if (amount)
                    amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                itemDisplay.gameObject.SetActive(true);

                void SetPosition(Package package)
                {
                    itemDisplay.sizeDelta = AbstractItem.GetDimensions(package.Item.Dimensions) * 60; // * slotSize

                    /// anchor to BottomLeft to match screen/mouse coordinates
                    itemDisplay.anchorMin = new Vector2(0, 0);
                    itemDisplay.anchorMax = new Vector2(0, 0);

                    var mousePosition = (Vector2)Input.mousePosition / rootCanvas.scaleFactor;
                    var slotPosition = (Vector2)Origin.transform.position / rootCanvas.scaleFactor;
                    /// get the BottomLeft position
                    slotPosition -= (Origin.transform as RectTransform).pivot * 60;

                    var slotPivot = (mousePosition - slotPosition) / 60;
                    slotPivot.x /= AbstractItem.GetDimensions(package.Item.Dimensions).x;
                    slotPivot.y /= AbstractItem.GetDimensions(package.Item.Dimensions).y;

                    // NOTE: this is derived from the GridLayoutComponent => get GridLayoutGroup.Corner to implement for all possible cases
                    /// THe positionOffset was calculated in InventorySpace (anchored TopLeft) so we subtract the DimensionHeight-1 to get to the BottomLeft position
                    positionOffset -= new Vector2Int(0, AbstractItem.GetDimensions(package.Item.Dimensions).y - 1);
                    /// and convert it to screenCoordinates
                    positionOffset.y *= -1;

                    var positionPivot = (Vector2)positionOffset;
                    positionPivot.x /= AbstractItem.GetDimensions(package.Item.Dimensions).x;
                    positionPivot.y /= AbstractItem.GetDimensions(package.Item.Dimensions).y;

                    // TODO: slightly offset this to simulate pickup OR do this by color change
                    itemDisplay.pivot = slotPivot + positionPivot;

                    itemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;
                }
            }
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
