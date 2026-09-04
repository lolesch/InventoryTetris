using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    internal sealed class DragProvider : AbstractProvider<DragProvider>, ICursorSink
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
                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelDrag();

                SetToMousePosition();

                HighlightOverlappingSlots();
            }

            void HighlightOverlappingSlots()
            {
                if (!background)
                    return;

                /// One rule, shared with the drop (issue #12): each slot display answers
                /// WouldAcceptDrop exactly the way its own DropItem behaves - CanPlaceAt at
                /// the pixel-derived cell for the inventory grid, the fixed type-specific
                /// slot for the paper-doll equipment layout, always-yes for a container-less
                /// sink (the floor, the sell slot). Red only while the cursor is over a real
                /// slot that would turn the drop away; hovering nothing clears it.
                var refused = Hovered != null && !Hovered.WouldAcceptDrop(DraggingPackage);

                /// Assigned rather than multiplied so it stays red whatever the scrim is
                /// tinted to; colour multiplication is component-wise and only lands on red
                /// while the scrim happens to be white.
                background.color = refused
                    ? WithAlpha(Color.red, initialColor.a)
                    : initialColor;
            }
        }

        /// <summary>
        /// The container position the drag display currently covers. The single answer to
        /// "where would this drop" - the overlap tint and every DropItem read it, so the
        /// warning the player sees and the placement they get can never disagree.
        /// </summary>
        public bool TryGetDropPosition(AbstractSlotDisplay hovered, out Vector2Int position)
        {
            position = default;

            if (hovered == null || hovered.Container == null || !DraggingPackage.IsValid)
                return false;

            position = DragGeometry.DropPosition(
                (Vector2)Input.mousePosition / transform.lossyScale,
                itemDisplay.pivot,
                ItemView.Of(DraggingPackage.Item).Dimensions,
                (Vector2)hovered.transform.position / transform.lossyScale,
                hovered.Position,
                slotSize);

            return true;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;

            return color;
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

            var dimensions = ItemView.Of(package.Item).Dimensions;

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

            RefreshDisplay(package);
            SetToMousePosition();
        }

        /// <summary>
        /// A different package is now in hand - a swap handed the displaced item over. It is
        /// centred on the cursor: the previous item's positionOffset describes a footprint
        /// this one does not have, and reusing it left small items floating a fixed distance
        /// from the pointer.
        /// </summary>
        public void ReplacePackage(Package package)
        {
            if (!package.IsValid)
            {
                EndDrag();
                return;
            }

            DraggingPackage = package;
            PositionOffset = Vector2Int.zero;

            var dimensions = ItemView.Of(package.Item).Dimensions;

            itemDisplay.sizeDelta = (Vector2)dimensions * slotSize;
            itemDisplay.pivot = DragGeometry.HandOverPivot;

            RefreshDisplay(package);
            SetToMousePosition();
        }

        /// <summary>
        /// The drag is over - the package landed, or a sink (sell slot, floor) consumed it.
        /// Clears the hand and hides the display; the sinks used to hand an empty Package to
        /// SetPackage purely to reach this.
        /// </summary>
        public void EndDrag()
        {
            DraggingPackage = default;
            PositionOffset = Vector2Int.zero;

            itemDisplay.gameObject.SetActive(false);
        }

        /// Paints the drag display for the package now in hand - icon, rarity frame, stack
        /// amount - then shows it. Size and pivot are the caller's: a grab keeps the grip it
        /// was picked up by, a hand-over centres on the cursor.
        private void RefreshDisplay(Package package)
        {
            if (icon)
            {
                icon.sprite = ItemView.Of(package.Item).Icon;
                icon.color = Color.white;
            }

            /// The drag display is an ItemDisplay instance but nothing repainted it, so a
            /// dragged item lost its rarity for the length of the drag. The frame carries
            /// rarity here, exactly as it does in a slot.
            if (frame)
                frame.color = WithAlpha(ItemView.RarityColorOf(package.Item.Rarity), frameAlpha);

            if (amount)
                amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

            itemDisplay.gameObject.SetActive(true);
        }

        public void SetHoveredSlot(AbstractSlotDisplay slot) => Hovered = slot;

        /// <summary>
        /// Cancels the drag in progress and sends the held Package back where it came from
        /// (issue #29): the exact origin cell if it is still free, the backpack if not, or -
        /// with neither available - nowhere, leaving the item on the cursor exactly as it was.
        /// Wired to Escape rather than right-click: a slot display already reads any click
        /// during a drag as a drop attempt (<see cref="AbstractSlotDisplay.OnPointerClick"/>),
        /// so reusing right-click here would race that path instead of replacing it.
        ///
        /// <para><b>Known gap:</b> <see cref="Origin"/> and <see cref="PositionOffset"/> only
        /// describe the drag's original pick-up. After a mid-drag swap hands a different
        /// Package to the cursor (<see cref="ReplacePackage"/>), cancelling re-homes that
        /// swapped-out Package against the *first* pick-up's cell, not the slot it actually
        /// came from - safe (nothing is lost; it lands in the backpack instead) but not
        /// exactly right, and a re-equip's affix is not re-applied. <see cref="ICursorSink"/>
        /// would need to carry the displaced item's real origin to close this; out of scope
        /// here per the issue's "not a full audit" note.</para>
        /// </summary>
        public void CancelDrag()
        {
            if (!IsDragging || !DraggingPackage.IsValid)
                return;

            var origin = Origin != null ? Origin.Container : null;
            var originPosition = Origin != null ? Origin.Position - PositionOffset : default;
            var backpack = InventoryProvider.Instance.Inventory;

            var leftOnCursor = ReturnToOrigin.Return(DraggingPackage, origin, originPosition, backpack);

            if (!leftOnCursor.IsValid)
                EndDrag();
        }

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
