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

        protected static Package packageToMove;

        private bool hovering;

        private void OnEnable()
        {
            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.ShowDebugPositions ? Position.ToString() : "";

            StaticDragDisplay.Instance.OnOverlapping -= SetBackgroundColor;
            StaticDragDisplay.Instance.OnOverlapping += SetBackgroundColor;
        }

        private void OnDisable() => StaticDragDisplay.Instance.OnOverlapping -= SetBackgroundColor;

        public void SetupSlot(AbstractDimensionalContainer container, Vector2Int position)
        {
            name = $"{position.x} | {position.y}";
            Position = position;
            Container = container;

            if (debugPosition != null)
                debugPosition.text = InventoryProvider.Instance.ShowDebugPositions ? Position.ToString() : "";
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

        /// required for OnBeginDrag() to work => #ThanksUnity
        public void OnDrag(PointerEventData eventData) { }

        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        public void OnEndDrag(PointerEventData eventData) { }

        public void OnDrop(PointerEventData eventData) => DropItem();

        private void HandleItem(PointerEventData eventData)
        {
            // TODO: this should be abstract and each slotDisplay decides how to handle items
            // --> or calculate the context dependent on what UI elements are visable...
            // => stash and inventory will equip/consume/use/sell
            // => equipment will unequip
            // => shop will buy

            // TODO: SPLIT ITEM STACKS => handle picking up one/all from a stack

            if (StaticDragDisplay.Instance.IsDragging)
                DropItem();
            else
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    var packagePosition = Container.GetStoredItemsAt(Position, new(1, 1))[0];
                    var item = Container.StoredPackages[packagePosition].Item;

                    if (item is ConsumableItem)
                        (item as ConsumableItem).Consume();

                    else if (item is EquipmentItem)
                        if (this is EquipmentSlotDisplay)
                            UnequipItem();
                        else
                            EquipItem();

                    // else if (item is Item)
                    // { }
                }
                else
                    PickUpItem();

                void PickUpItem()
                {
                    var storedPositions = Container.GetStoredItemsAt(Position, new(1, 1));

                    if (storedPositions.Count == 1)
                    {
                        packageToMove = Container.StoredPackages[storedPositions[0]];

                        var positionOffset = Position - storedPositions[0];

                        _ = Container.RemoveAtPosition(storedPositions[0], packageToMove);

                        StaticDragDisplay.Instance.SetPackage(this, packageToMove, positionOffset);
                    }

                    FadeOutPreview();
                }
            }
        }

        private void FadeInPreview()
        {
            hovering = true;

            if (Container == null)
                return;

            var itemToDisplay = Container?.GetStoredItemsAt(Position, new(1, 1));
            if (itemToDisplay.Count == 1)
                if (Container.StoredPackages.TryGetValue(itemToDisplay[0], out var hoveredIten))
                    if (hoveredIten.Item != null && 0 < hoveredIten.Amount)
                        _ = StartCoroutine(FadeIn(hoveredIten, itemToDisplay[0]));

            IEnumerator FadeIn(Package package, Vector2Int storedPosition)
            {
                var timeStamp = Time.time;

                while (hovering)
                {
                    yield return null;

                    var canFadeIn = 0.5f < Time.time - timeStamp;

                    if (canFadeIn && hovering)
                    {
                        StaticPrevievDisplay.Instance.RefreshPreviewDisplay(package, this);
                        hovering = false;
                    }
                }
            }
        }

        private void FadeOutPreview()
        {
            hovering = false;

            StaticPrevievDisplay.Instance.RefreshPreviewDisplay(new Package(Container, null, 0), this);
        }

        protected virtual void DropItem() => FadeInPreview();

        protected virtual void UnequipItem() => FadeOutPreview();

        protected virtual void EquipItem() => FadeOutPreview();

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
