using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class InventorySlotDisplay : AbstractSlotDisplay
    {
        private static Package package;

        //private CanvasGroup canvasGroup;
        private GridLayoutGroup gridLayout;

        //void Awake() => canvasGroup = itemDisplay.GetComponent<CanvasGroup>();

        public override void OnPointerClick(PointerEventData eventData)
        {
            // if shift clicking try add to other container
            // if rightclicking try to equip

            // handle picking up one/all from a stack

            if (!StaticDragDisplay.Instance.IsDragging)
                PickUpItem();
            else
                // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
                DropItem();
        }

        public override void OnBeginDrag(PointerEventData eventData) => PickUpItem();

        public override void OnDrag(PointerEventData eventData) { }

        public override void OnDrop(PointerEventData eventData) => DropItem();

        // OnEndDrag
        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor

        internal protected override void PickUpItem()
        {
            var otherItems = container.GetStoredPackagesAtPosition(Position, new(1, 1));

            if (otherItems.Count == 1)
            {
                package = container.storedPackages[otherItems[0]];

                StaticDragDisplay.Instance.SetPackage(this, package);

                container.RemoveItemAtPosition(otherItems[0], package);
            }
        }

        internal protected override void DropItem()
        {
            if (container.CanAddAtPosition(Position, StaticDragDisplay.Instance.Package.Item.Dimensions, out List<Vector2Int> otherItems))
            {
                Package remaining;

                remaining = container.AddAtPosition(Position, package);

                if (0 < remaining.Amount)
                {
                    package = remaining;
                    StaticDragDisplay.Instance.SetPackage(this, remaining);
                }
                else
                {
                    package = new Package();

                    StaticDragDisplay.Instance.SetPackage(this, package);
                }

                container.InvokeRefresh();
                StaticDragDisplay.Instance.packageOrigin.container.InvokeRefresh();
            }
        }

        protected internal override void RefreshSlotDisplay(Package package)
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

                void SetDisplay(Package package) // move this into AbstractSlotDisplay?
                {
                    SetDisplaySize(itemDisplay, package);

                    if (icon)
                        icon.sprite = package.Item.Icon;

                    if (amount)
                        amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                    void SetDisplaySize(RectTransform display, Package package)
                    {
                        if (!gridLayout)
                            gridLayout = GetComponentInParent<GridLayoutGroup>();
                        if (gridLayout)
                        {
                            Vector2 additionalSpacing = gridLayout.spacing * new Vector2(package.Item.Dimensions.x - 1, package.Item.Dimensions.y - 1);

                            display.sizeDelta = gridLayout.cellSize * package.Item.Dimensions + additionalSpacing;
                        }

                        display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
                        display.pivot = new Vector2(.5f, .5f);
                        display.anchorMin = new Vector2(0, 1);
                        display.anchorMax = new Vector2(0, 1);
                    }
                }
            }
        }
    }
}
