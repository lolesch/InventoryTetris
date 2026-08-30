using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Geometry;
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
        [Tooltip("The authored scrim colour the background returns to. Captured at Awake; the forbidden tint replaces it.")]
        [SerializeField, ReadOnly] private Color initialColor;
        [SerializeField] private TextMeshProUGUI amount;

        [Tooltip("Pixels per inventory cell. Must match the GridLayoutGroup cellSize the slots are laid out with.")]
        [SerializeField] private float slotSize = 60f;

        //private Canvas rootCanvas;

        public RectTransform ItemDisplay => itemDisplay;

        public AbstractSlotDisplay Origin { get; private set; }
        public AbstractSlotDisplay Hovered { get; private set; }
        public Package DraggingPackage { get; private set; }

        public Vector2Int PositionOffset { get; private set; }

        private float frameAlpha = 1f;


        private void Awake()
        {
            //_ = transform.root.TryGetComponent(out rootCanvas);

            itemDisplay.gameObject.SetActive(false);

            /// Rarity rides on the frame, which keeps its authored alpha. The background stays
            /// the authored scrim - at alpha 0.2 a rarity tint there would say nothing, and the
            /// display has to stay see-through over the grid it is covering.
            if (frame)
                frameAlpha = frame.color.a;

            if (background)
                initialColor = background.color;
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
                    /// Assigned rather than multiplied so it stays red whatever the scrim is
                    /// tinted to; colour multiplication is component-wise and only lands on red
                    /// while the scrim happens to be white.
                    /// 0 overlaps drops into empty space, 1 swaps with the item already there
                    /// (AddAtPosition handles both). Only 2+ cannot be placed at all.
                    background.color = 1 < storedPositions.Count
                        ? WithAlpha(Color.red, initialColor.a)
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

        public void SetPackage(AbstractSlotDisplay slot, Package package, Vector2Int positionOffset, Vector2 pointerPosition)
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

                /// The drag display is an ItemDisplay instance but nothing repainted it, so a
                /// dragged item lost its rarity for the length of the drag. The frame carries
                /// rarity here, exactly as it does in a slot.
                if (frame)
                    frame.color = WithAlpha(AbstractItem.GetRarityColor(package.Item.Rarity), frameAlpha);

                if (amount)
                    amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                itemDisplay.gameObject.SetActive(true);

                void SetPosition(Package package)
                {
                    var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

                    itemDisplay.sizeDelta = (Vector2)dimensions * slotSize;

                    /// Anchor the grip to where the pointer went down, not to Input.mousePosition
                    /// as it reads here - by OnBeginDrag time the cursor has already travelled the
                    /// 10px drag threshold, and pivoting to that is what let the item slip its grip.
                    itemDisplay.pivot = DragGeometry.GrabPivot(
                        pointerPosition / transform.lossyScale,
                        (Vector2)Origin.transform.position / transform.lossyScale,
                        dimensions,
                        positionOffset,
                        slotSize);

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
