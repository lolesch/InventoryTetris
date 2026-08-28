using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    internal sealed class DragProvider : AbstractProvider<DragProvider>
    {
        public bool IsDragging => itemDisplay.gameObject.activeSelf;

        [SerializeField] private RectTransform itemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private Image background;
        [Tooltip("The dragged item's own background colour. Set per drag from its rarity; the overlap tint multiplies on top of it.")]
        [SerializeField, ReadOnly] private Color initialColor;
        [SerializeField] private TextMeshProUGUI amount;

        //private Canvas rootCanvas;

        public RectTransform ItemDisplay => itemDisplay;

        public AbstractSlotDisplay Origin { get; private set; }
        public AbstractSlotDisplay Hovered { get; private set; }
        public Package DraggingPackage { get; private set; }

        public Vector2Int PositionOffset { get; private set; }

        private float frameAlpha = 1f;
        private float backgroundAlpha = 1f;


        private void Awake()
        {
            //_ = transform.root.TryGetComponent(out rootCanvas);

            itemDisplay.gameObject.SetActive(false);

            /// The drag display floats over the grid, so it is authored translucent to keep
            /// what it overlaps readable. Rarity supplies the hue; these keep the alpha.
            if (frame)
                frameAlpha = frame.color.a;

            if (background)
                backgroundAlpha = background.color.a;
        }

        private void Update()
        {
            if (IsDragging)
            {
                SetToMousePosition();

                HighlightOverlappingSlots();
            }

            void HighlightOverlappingSlots()
            {
                /// Every early return has to clear the tint. Bailing out while a previous
                /// slot's red is still on screen is what made large items look undroppable
                /// over the floor and sell slots, which have no container of their own.
                if (Hovered == null || DraggingPackage.Item == null)
                {
                    ResetOverlapTint();
                    return;
                }

                /// The pivot is the mouse position within the items dimensions
                var positionPivot = itemDisplay.pivot;
                positionPivot *= AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions);

                var positionDiff = new Vector2Int(Mathf.FloorToInt(positionPivot.x), Mathf.FloorToInt(positionPivot.y));
                positionDiff -= new Vector2Int(0, AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions).y - 1);
                positionDiff.y *= -1;

                var positionToAdd = Hovered.Position - positionDiff;

                if (Hovered.Container == null)
                {
                    ResetOverlapTint();
                    return;
                }

                var storedPositions = Hovered.Container?.GetStoredItemsAt(positionToAdd, AbstractItem.GetDimensions(DraggingPackage.Item.Dimensions));

                if (background)
                    /// Assigned, not multiplied: tinting by Color.red is component-wise, so it
                    /// zeroes the green and blue of the rarity colour underneath - magic blue
                    /// came out black rather than red.
                    /// 0 overlaps drops into empty space, 1 swaps with the item already there
                    /// (AddAtPosition handles both). Only 2+ cannot be placed at all.
                    background.color = 1 < storedPositions.Count
                        ? WithAlpha(Color.red, backgroundAlpha)
                        : initialColor;


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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;

            return color;
        }

        private void ResetOverlapTint()
        {
            if (background)
                background.color = initialColor;
        }

        private void SetToMousePosition()
        {
            /// anchor to BottomLeft to match screen/mouse coordinates
            itemDisplay.anchorMin = Vector2.zero;
            itemDisplay.anchorMax = Vector2.zero;

            itemDisplay.anchoredPosition = (Vector2)Input.mousePosition / itemDisplay.lossyScale;
        }

        public void SetPackage(AbstractSlotDisplay slot, Package package, Vector2Int positionOffset)
        {
            Origin = slot;
            DraggingPackage = package;
            PositionOffset = positionOffset;

            if (!DraggingPackage.IsValid)
            {
                itemDisplay.gameObject.SetActive(false);
                return;
            }

            SetHoveredSlot(Origin);

            RefreshDisplay(package);

            void RefreshDisplay(Package package)
            {
                SetPosition(package);

                if (icon)
                {
                    icon.sprite = package.Item.Icon;
                    icon.color = Color.white;
                }

                /// Mirrors AbstractSlotDisplay.RefreshSlotDisplay: the drag display is an
                /// ItemDisplay instance, so it has to be painted the same way or the item
                /// loses its rarity colours for the duration of the drag.
                var rarityColor = AbstractItem.GetRarityColor(package.Item.Rarity);

                if (frame)
                    frame.color = WithAlpha(rarityColor, frameAlpha);

                /// Half brightness, not the quarter a slot uses. A slot background is an
                /// opaque backdrop behind the icon; this one is translucent and floats over
                /// the grid, so the same darkening would leave it barely visible.
                initialColor = WithAlpha(rarityColor * Color.gray, backgroundAlpha);

                if (background)
                    background.color = initialColor;

                if (amount)
                    amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                itemDisplay.gameObject.SetActive(true);

                void SetPosition(Package package)
                {
                    var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

                    itemDisplay.sizeDelta = dimensions * 60; // slotSize

                    var pointerRelativeToOrigin = (Vector2)(Input.mousePosition - Origin.transform.position) / transform.lossyScale;

                    /// pointerPosition is in pixelCoordinates anchored TopLeft
                    var pivot = pointerRelativeToOrigin / 60; // slotSize
                    /// convert to screenCoordinates anchored BottomLeft
                    pivot.y += 1;
                    /// scale to match item dimensions
                    pivot /= dimensions;

                    /// The positionOffset was calculated in InventorySpace (anchored TopLeft)
                    positionOffset.y -= dimensions.y - 1;
                    /// convert to screenCoordinates anchored BottomLeft
                    positionOffset.y *= -1;

                    var positionPivot = (Vector2)positionOffset;
                    positionPivot /= dimensions;

                    itemDisplay.pivot = pivot + positionPivot;

                    SetToMousePosition();
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
