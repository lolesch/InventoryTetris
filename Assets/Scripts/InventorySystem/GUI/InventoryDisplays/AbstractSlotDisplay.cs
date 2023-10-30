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

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    // TODO: inherit AbstractDisplay or rename this pattern
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public abstract class AbstractSlotDisplay : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
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

        private bool hovering;

        private void OnEnable()
        {
            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.ShowDebugPositions ? Position.ToString() : "";

            DragProvider.Instance.OnOverlapping -= SetBackgroundColor;
            DragProvider.Instance.OnOverlapping += SetBackgroundColor;
        }

        private void OnDisable() => DragProvider.Instance.OnOverlapping -= SetBackgroundColor;

        public void SetupSlot(AbstractDimensionalContainer container, Vector2Int position)
        {
            name = $"{position.x} | {position.y}";
            Position = position;
            Container = container;

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
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            DragProvider.Instance.SetHoveredSlot(this);

            FadeInPreview();
        }

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
                    _ = StartCoroutine(FadeIn(hoveredIten));

            IEnumerator FadeIn(Package package)
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

            PreviewProvider.Instance.RefreshPreviewDisplay(new Package(), this);
        }

        protected abstract void DropItem(Package package);

        protected virtual void SetDisplaySize(RectTransform display, Package package) { }

        public void RefreshSlotDisplay(Package package)
        {
            if (itemDisplay)
            {
                if (package.Amount < 1)
                {
                    itemDisplay.gameObject.SetActive(false);
                    return;
                }

                SetDisplay(package);

                itemDisplay.gameObject.SetActive(true);

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
                        background.color = rarityColor * Color.gray * Color.gray;
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
