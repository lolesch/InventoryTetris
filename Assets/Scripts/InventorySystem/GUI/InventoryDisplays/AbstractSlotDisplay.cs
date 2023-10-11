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

        public void OnPointerClick(PointerEventData eventData) =>
            // TODO: if shift clicking try add to other container

            HandleItem(eventData);

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

        public void OnBeginDrag(PointerEventData eventData) => HandleItem(eventData);

        /// required for OnBeginDrag() to work => #ThanksUnity
        public void OnDrag(PointerEventData eventData) { }

        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        public void OnEndDrag(PointerEventData eventData) { }

        public void OnDrop(PointerEventData eventData) => DropItem();

        private void HandleItem(PointerEventData eventData)
        {
            if (DragProvider.Instance.IsDragging)
                DropItem();
            else if (Container != null)
            {
                var storedPositions = Container.GetStoredItemsAt(Position);

                if (storedPositions.Count == 1)
                {
                    packageToMove = Container.StoredPackages[storedPositions[0]];

                    if (eventData.button == PointerEventData.InputButton.Right)
                    {
                        if (packageToMove.Item is ConsumableItem)
                            ConsumeItem();

                        else if (packageToMove.Item is EquipmentItem)
                            if (this is EquipmentSlotDisplay)
                                UnequipItem();
                            else
                                EquipItem();

                        return;
                    }

                    // TODO: SPLIT ITEM STACKS => handle picking up one/all from a stack

                    if (Input.GetKey(KeyCode.LeftControl))
                        if (1 < packageToMove.Amount)
                            packageToMove.ReduceAmount(packageToMove.Amount / 2);

                    // CONTINUE HERE
                    // TODO: implement static trade context -> send packages to the tradeProvider to decide how to handle items 

                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        if (Container == InventoryProvider.Instance.Inventory || Container == InventoryProvider.Instance.Equipment)
                        {
                            _ = Container.RemoveAtPosition(storedPositions[0], packageToMove);
                            InventoryProvider.Instance.Stash.AddToContainer(packageToMove);
                            // -> rework this to a context based system, where each container is eigher left or right sided and therefore moving items to the other side
                        }
                        else //if (Container == InventoryProvider.Instance.Stash)
                        {
                            _ = Container.RemoveAtPosition(storedPositions[0], packageToMove);
                            InventoryProvider.Instance.Inventory.AddToContainer(packageToMove);
                        }
                    }
                    else
                    {
                        _ = Container.RemoveAtPosition(storedPositions[0], packageToMove);

                        var positionOffset = Position - storedPositions[0];

                        DragProvider.Instance.SetPackage(this, packageToMove, positionOffset);
                    }
                }

                FadeOutPreview();
            }
        }

        private void FadeInPreview()
        {
            hovering = true;

            if (Container == null)
                return;

            var itemToDisplay = Container.GetStoredItemsAt(Position);
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
                        PreviewProvider.Instance.RefreshPreviewDisplay(package, this);
                        hovering = false;
                    }
                }
            }
        }

        private void FadeOutPreview()
        {
            hovering = false;

            PreviewProvider.Instance.RefreshPreviewDisplay(new Package(Container, null, 0), this);
        }

        protected virtual void DropItem() => FadeInPreview();

        protected virtual void UnequipItem() => FadeOutPreview();

        protected virtual void EquipItem() => FadeOutPreview();

        protected virtual void ConsumeItem() => FadeOutPreview();

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
