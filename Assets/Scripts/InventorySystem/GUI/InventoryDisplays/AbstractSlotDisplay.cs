using System.Collections;
using System.Runtime.CompilerServices;
using NaughtyAttributes;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("InventorySystem.Containers.Tests")]

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    // TODO: inherit AbstractDisplay or rename this pattern
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    internal abstract class AbstractSlotDisplay : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField, ReadOnly] public AbstractDimensionalContainer Container { get; private set; }
        [field: SerializeField, ReadOnly] public Vector2Int Position { get; private set; }
        [Space]
        [SerializeField] protected RectTransform itemDisplay;
        [SerializeField] protected Image icon;
        [SerializeField] protected Image frame;
        [SerializeField] protected Image background;
        [SerializeField] protected TextMeshProUGUI amount;
        [SerializeField] protected Image slotBackground;

        [SerializeField] protected TextMeshProUGUI debugPosition;

        [Space]
        [Tooltip("Pixels the item frame grows outward on every side while hovered.")]
        [SerializeField] protected float hoverExpand = 1f;
        [Tooltip("How far the item background is lightened toward white while hovered. Equivalent to overlaying white at this alpha.")]
        [SerializeField, Range(0f, 1f)] protected float hoverLighten = 0.25f;

        private bool hovering;

        /// The container display that owns this slot; needed to reach the slot an item
        /// actually renders on, which is its origin - not necessarily the hovered one.
        private AbstractContainerDisplay owner;

        /// The slot whose frame this slot lit up on enter, so exit can clear the same one.
        private AbstractSlotDisplay highlighted;

        /// Kept so the highlight can be re-applied after a refresh rebuilds the display.
        private bool isHighlighted;

        /// The item's untinted background color, as RefreshSlotDisplay derived it from rarity.
        private Color baseBackgroundColor = Color.white;

        protected virtual void OnEnable()
        {
            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.ShowDebugPositions ? Position.ToString() : "";
        }

        protected virtual void OnDisable()
        {
            ClearHighlight();
            SetHighlighted(false);
        }

        public void SetupSlot(AbstractContainerDisplay containerDisplay, AbstractDimensionalContainer container, Vector2Int position)
        {
            name = $"{position.x} | {position.y}";
            Position = position;
            Container = container;
            owner = containerDisplay;

            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.ShowDebugPositions ? Position.ToString() : "";
        }

        /// Where the pointer went down on this slot. OnBeginDrag only fires once Unity's 10px
        /// drag threshold is crossed, by which time the cursor can already be outside the item -
        /// anchoring to that reading is what made the item jump away from the cursor on grab.
        private Vector2 pressPosition;

        public void OnPointerDown(PointerEventData eventData) => pressPosition = eventData.position;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (DragProvider.Instance.IsDragging)
                DropItem(DragProvider.Instance.DraggingPackage);
            else if (eventData.button != PointerEventData.InputButton.Right && Input.GetKey(KeyCode.LeftShift) && TryQuickMove())
                return;
            else
                MoveItem(eventData, pressPosition);
        }

        /// <summary>
        /// The shift-click quick-move dispatch (issue #67), hoisted here so every slot
        /// display gets it for free instead of hand-rolling the same preamble -
        /// <see cref="QuickMoveResolver"/> read, <see cref="QuickMoveIntentKind"/> switched
        /// on, move executed - four times with growing odds one copy is wrong or missing
        /// (<see cref="BasketSlotDisplay"/> shipped without it entirely). <see cref="MoveItem"/>
        /// is left with only the right-click and drag-pickup branches, which genuinely
        /// differ per container.
        /// </summary>
        /// <returns>
        /// Whether the click was a quick-move the resolver had an answer for. False leaves
        /// the click to fall through to <see cref="MoveItem"/> - there was no item under the
        /// cursor, so a normal click still gets its chance.
        /// </returns>
        private bool TryQuickMove()
        {
            if (Container == null)
                return false;

            var position = Position;

            if (!Container.TryGetItemAt(ref position, out var package))
                return false;

            FadeOutPreview();

            var intent = InventoryProvider.Instance.QuickMoveFor(Container);

            switch (intent.Kind)
            {
                case QuickMoveIntentKind.SellBasket:
                    /// One transaction over the source and the basket: the item leaves this
                    /// slot and lands in the basket with its origin remembered; a full
                    /// basket leaves it where it is (#33).
                    _ = SellBasketQuickMove.SendToBasket(InventoryProvider.Instance.Basket, Container, position);
                    return true;

                case QuickMoveIntentKind.MoveToContainer:
                    var target = intent.Target;
                    var cursor = new CursorHolder(DragProvider.Instance);

                    using (var transaction = new ItemTransaction(cursor, Container, target).ReHomeThrough(target))
                    {
                        _ = Container.RemoveAtPosition(position, package);
                        _ = transaction.TryReHomeToContainerOrHand(ref package, new PackageOrigin(Container, position));

                        transaction.Commit();
                    }
                    return true;

                case QuickMoveIntentKind.Buy:
                    /// The shelf's own shift-click is always a buy (issue #30) - the same
                    /// atomic move VendorTransaction.Buy runs for a right-click purchase.
                    var wallet = InventoryProvider.Instance.Wallet;
                    var price = VendorTransaction.BuyPrice(package.Item);

                    _ = VendorTransaction.Buy(Container, position, package, wallet, price);
                    return true;

                default:
                    /// Nothing to do here - the click is still absorbed rather than falling
                    /// through to a drag pickup, matching the resolver's "no panel open"
                    /// answer.
                    return true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            DragProvider.Instance.SetHoveredSlot(null);

            FadeOutPreview();

            ClearHighlight();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            DragProvider.Instance.SetHoveredSlot(this);

            FadeInPreview();

            if (TryGetOriginSlot(out var origin))
            {
                highlighted = origin;
                origin.SetHighlighted(true);
            }
        }

        /// The slot an item is drawn on is its origin, and one item can cover many slots.
        /// Hovering any covered slot must light up that one item, not the sub-slot the
        /// cursor happens to be over.
        private bool TryGetOriginSlot(out AbstractSlotDisplay slot)
        {
            slot = null;

            if (Container == null || owner == null)
                return false;

            var position = Position;

            if (!Container.TryGetItemAt(ref position, out _))
                return false;

            return owner.TryGetSlotDisplayAt(position, out slot);
        }

        private void ClearHighlight()
        {
            if (highlighted)
                highlighted.SetHighlighted(false);

            highlighted = null;
        }

        /// Grows the item frame outward instead of overlaying a second image, so the
        /// highlight covers exactly the item's own footprint however many slots it spans.
        internal void SetHighlighted(bool highlight)
        {
            isHighlighted = highlight;

            if (frame)
            {
                var expand = highlight ? hoverExpand : 0f;

                frame.rectTransform.offsetMin = new Vector2(-expand, -expand);
                frame.rectTransform.offsetMax = new Vector2(expand, expand);
            }

            RefreshBackground();
        }

        /// Single place the item background color is decided, so hover and any tint on top
        /// of it (see VendorSlotDisplay) compose instead of overwriting each other.
        protected void RefreshBackground()
        {
            if (background)
                background.color = GetBackgroundColor();
        }

        protected virtual Color GetBackgroundColor() => Lighten(baseBackgroundColor);

        /// The hover lift, applied to whatever colour the slot settled on, so a subclass
        /// that *replaces* the colour still responds to hover instead of going flat.
        /// Alpha lifts along with the colour: that is a no-op on an opaque background,
        /// but a translucent tint (see VendorSlotDisplay) needs the opacity to move or
        /// the hover barely registers against it.
        protected Color Lighten(Color color) => isHighlighted
            ? Color.Lerp(color, Color.white, hoverLighten)
            : color;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (DragProvider.Instance.IsDragging)
                DropItem(DragProvider.Instance.DraggingPackage);
            else
                MoveItem(eventData, pressPosition);
        }

        /// required for OnBeginDrag() to work => #ThanksUnity
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData) { }

        public void OnDrop(PointerEventData eventData) => DropItem(DragProvider.Instance.DraggingPackage);

        /// <summary>
        /// Right-click and drag-pickup, the two behaviors that genuinely differ per
        /// container (consume / equip / unequip / buy). Shift-click no longer reaches this -
        /// <see cref="TryQuickMove"/> handles it before <see cref="OnPointerClick"/> ever
        /// calls here. Virtual with an empty default: a container with nothing of its own to
        /// do on right-click or drag-pickup (<see cref="BasketSlotDisplay"/>) needs no
        /// override at all.
        /// </summary>
        protected virtual void MoveItem(PointerEventData eventData, Vector2 pointerPosition) { }

        /// <summary>
        /// A quick-move's <see cref="QuickMoveIntentKind.MoveToContainer"/> arm (issue #30):
        /// removes <paramref name="package"/> from this slot's <see cref="Container"/> at
        /// <paramref name="position"/> and re-homes it into <paramref name="target"/>, rolling
        /// back to the hand if it doesn't fit. Shared by every slot display's shift-click
        /// handling - the same three lines were hand-copied per display before this.
        /// </summary>
        /// <returns>Whether the move committed, i.e. was not aborted.</returns>
        protected bool QuickMoveToContainer(AbstractDimensionalContainer target, Vector2Int position, Package package)
        {
            var cursor = new CursorHolder(DragProvider.Instance);

            using var transaction = new ItemTransaction(cursor, Container, target).ReHomeThrough(target);

            _ = Container.RemoveAtPosition(position, package);
            _ = transaction.TryReHomeToContainerOrHand(ref package, new PackageOrigin(Container, position));

            transaction.Commit();

            return !transaction.Aborted;
        }

        /// <summary>
        /// The guard-clause prologue every <see cref="MoveItem"/> override starts with: no
        /// container, or nothing at <see cref="Position"/>, and there is nothing to move; found
        /// one, and the hover preview fades before anything changes underneath it. Where a
        /// concrete slot actually diverges starts after this returns true.
        /// </summary>
        protected bool TryBeginMove(out Vector2Int position, out Package package)
        {
            position = Position;
            package = default;

            if (Container == null)
                return false;

            if (!Container.TryGetItemAt(ref position, out package))
                return false;

            FadeOutPreview();

            return true;
        }

        /// <summary>
        /// The "DRAG ITEM" tail every concrete slot falls through to once its own special-cases
        /// (use, equip, buy, stage...) don't apply: pick <paramref name="package"/> up off
        /// <paramref name="position"/> and hand it to the cursor. <paramref name="position"/> is
        /// passed rather than re-read from <see cref="Position"/> because a multi-cell item's
        /// origin is not necessarily the cell under the pointer - the offset between them is
        /// what keeps the drag visual anchored to where it was grabbed instead of snapping to
        /// the origin cell. <paramref name="purchasePrice"/> is the Vendor shelf's buy-on-drop
        /// price (issue #31); every other source leaves it unset.
        /// </summary>
        protected void BeginDrag(Vector2Int position, Package package, Vector2 pointerPosition, float? purchasePrice = null)
        {
            _ = Container.RemoveAtPosition(position, package);

            var positionOffset = Position - position;

            DragProvider.Instance.SetPackage(this, package, positionOffset, pointerPosition, purchasePrice);
        }

        /// <summary>
        /// Shift-click quick-move shared by every source the Sell Basket can receive from
        /// (issue #30/#33): resolve the intent for <see cref="Container"/>, stage a sale if
        /// the Vendor is open, otherwise move to whatever container the resolver names: with
        /// neither panel open, the resolver names nothing and this is a no-op.
        /// <see cref="BasketSlotDisplay"/> does not use this - there is no sell-the-basket-to-
        /// itself case, and a successful move there also has to clear the origin ledger entry.
        /// </summary>
        protected void QuickMove(Vector2Int position, Package package)
        {
            var intent = InventoryProvider.Instance.QuickMoveFor(Container);

            if (intent.Kind == QuickMoveIntentKind.SellBasket)
            {
                _ = SellBasketQuickMove.SendToBasket(InventoryProvider.Instance.Basket, Container, position);
                return;
            }

            if (intent.Kind != QuickMoveIntentKind.MoveToContainer)
                return;

            _ = QuickMoveToContainer(intent.Target, position, package);
        }

        protected void FadeInPreview() => RefreshHoverPreview(clearStale: false);

        /// <summary>
        /// Bring the hover preview in line with the container cell the cursor rests over
        /// (<see cref="Position"/>) after a swap this slot performed. The cursor has not
        /// moved, so the cell may now hold the item the swap landed there - a drop follows
        /// the drag visual and often lands a cell over; a right-click "swap in place"
        /// re-homes the displaced item into the vacated cell, i.e. straight back under the
        /// cursor - or be empty because its occupant went to the hand or another container.
        /// Unlike the hover path this wipes a stale preview up front instead of leaving the
        /// old item frozen there until a pointer-exit (issue #13, QA-2).
        /// </summary>
        protected void SyncPreviewAfterMove() => RefreshHoverPreview(clearStale: true);

        /// <summary>
        /// The one path that decides what the hover preview shows: whatever
        /// <see cref="HoverPreview.Under"/> finds under <see cref="Position"/>, faded in the
        /// same way a hover would so a rapid drag-drop-drag never flashes the panel. On a
        /// plain hover the cell is only ever gaining an item; after a move
        /// (<paramref name="clearStale"/>) it may instead have been vacated, so clear first.
        /// </summary>
        private void RefreshHoverPreview(bool clearStale)
        {
            hovering = false;

            if (clearStale)
                PreviewProvider.Instance.RefreshPreviewDisplay(new Package(Container, null, 0), this);

            var package = HoverPreview.Under(Container, Position);

            if (package.IsValid)
                _ = StartCoroutine(FadeIn(package));

            IEnumerator FadeIn(Package toShow)
            {
                hovering = true;

                var timeStamp = Time.time;

                while (hovering)
                {
                    yield return null;

                    var canFadeIn = 0.5f < Time.time - timeStamp;

                    if (canFadeIn && hovering)
                    {
                        PreviewProvider.Instance.RefreshPreviewDisplay(toShow, this);
                        hovering = false;
                    }
                }
            }
        }

        protected void FadeOutPreview()
        {
            hovering = false;

            PreviewProvider.Instance.RefreshPreviewDisplay(new Package(Container, null, 0), this);
        }

        protected abstract void DropItem(Package package);

        /// <summary>
        /// Whether <see cref="DropItem"/> would place <paramref name="package"/> if the
        /// player released it over this slot now - the exact predicate the drag display's
        /// red "can't drop" tint shows, so the warning and the drop can never disagree
        /// (issue #12). The base answers for a uniform grid, via
        /// <see cref="DragProvider.TryGetDropPosition"/> +
        /// <see cref="AbstractDimensionalContainer.CanPlaceAt"/>; a sink with no container
        /// of its own (the floor, the sell slot) takes anything;
        /// <see cref="EquipmentSlotDisplay"/> overrides it for the paper-doll layout.
        /// </summary>
        public virtual bool WouldAcceptDrop(Package package)
        {
            if (!package.IsValid)
                return false;

            if (Container == null)
                return true;

            return DragProvider.Instance.TryGetDropPosition(this, out var position)
                && Container.CanPlaceAt(position, ItemView.Of(package.Item).Dimensions);
        }

        protected virtual void SetDisplaySize(RectTransform display, Package package) { }

        public virtual void RefreshSlotDisplay(Package package)
        {
            if (itemDisplay)
            {
                if (package.Amount < 1)
                {
                    SetHighlighted(false);
                    itemDisplay.gameObject.SetActive(false);
                    return;
                }

                SetDisplay(package);

                itemDisplay.gameObject.SetActive(true);

                /// SetDisplay resets frame geometry; put the highlight back if we are still under the cursor.
                SetHighlighted(isHighlighted);

                void SetDisplay(Package package)
                {
                    SetDisplaySize(itemDisplay, package);

                    if (icon)
                    {
                        icon.sprite = ItemView.Of(package.Item).Icon;
                        icon.color = Color.white;
                    }

                    if (amount)
                        amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                    var rarityColor = ItemView.RarityColorOf(package.Item.Rarity);

                    if (frame)
                        frame.color = rarityColor;

                    if (background)
                    {
                        baseBackgroundColor = rarityColor * Color.gray * Color.gray;
                        background.color = baseBackgroundColor;
                    }
                }
            }
        }
    }
}
