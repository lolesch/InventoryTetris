using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("InventorySystem.Data.Tests")]

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    // TODO: inherit AbstractDisplay or rename this pattern
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    internal abstract class AbstractSlotDisplay : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
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
        [SerializeField, Range(0f, 1f)] protected float hoverLighten = 0.1f;

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

        private void OnEnable()
        {
            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.ShowDebugPositions ? Position.ToString() : "";

            DragProvider.Instance.OnOverlapping -= SetBackgroundColor;
            DragProvider.Instance.OnOverlapping += SetBackgroundColor;
        }

        private void OnDisable()
        {
            DragProvider.Instance.OnOverlapping -= SetBackgroundColor;

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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (DragProvider.Instance.IsDragging)
                DropItem(DragProvider.Instance.DraggingPackage);
            else
                MoveItem(eventData);
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

        protected virtual Color GetBackgroundColor() => isHighlighted
            ? Color.Lerp(baseBackgroundColor, Color.white, hoverLighten)
            : baseBackgroundColor;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (DragProvider.Instance.IsDragging)
                DropItem(DragProvider.Instance.DraggingPackage);
            else
                MoveItem(eventData);
        }

        /// required for OnBeginDrag() to work => #ThanksUnity
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData) { }

        public void OnDrop(PointerEventData eventData) => DropItem(DragProvider.Instance.DraggingPackage);

        protected abstract void MoveItem(PointerEventData eventData);

        protected void FadeInPreview()
        {
            if (Container == null)
                return;

            var position = Position;

            if (Container.TryGetItemAt(ref position, out var hoveredIten))
                if (hoveredIten.Item != null && 0 < hoveredIten.Amount)
                    _ = StartCoroutine(FadeIn(hoveredIten, position));

            IEnumerator FadeIn(Package package, Vector2Int storedPosition)
            {
                hovering = true;

                var timeStamp = Time.time;

                while (hovering)
                {
                    yield return null;

                    var canFadeIn = 0.5f < Time.time - timeStamp;

                    if (canFadeIn && hovering)
                    {
                        PreviewProvider.Instance.RefreshPreviewDisplay(package, this);
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

        protected virtual void SetDisplaySize(RectTransform display, Package package) { }

        public void RefreshSlotDisplay(Package package)
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
                        icon.sprite = package.Item.Icon;
                        icon.color = Color.white;
                    }

                    if (amount)
                        amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                    var rarityColor = AbstractItem.GetRarityColor(package.Item.Rarity);

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

        public void SetBackgroundColor(List<Vector2Int> overlappingPositions)
        {
            if (slotBackground)
            {
                var alpha = slotBackground.color.a;

                if (0 <= overlappingPositions.Count) // OR if not containing any item
                    slotBackground.color = Color.white;
                else
                {
                    foreach (var item in overlappingPositions)
                        if (item == Position)
                            slotBackground.color = (overlappingPositions.Count == 1) ? Color.yellow : Color.red;
                }

                slotBackground.color *= new Vector4(1, 1, 1, alpha);
            }
        }
    }
}
