using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class InventorySlotDisplay : AbstractSlotDisplay
    {
        private GridLayoutGroup gridLayout;

        protected override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item)
            {
                var positionOffset = StaticDragDisplay.Instance.Package.Item.Dimensions / 2;
                var mousePositionOffset = (Vector2)(Input.mousePosition - transform.position) / transform.lossyScale; //transform.root.GetComponent<Canvas>().scaleFactor;
                var relativeMouseOffset = (mousePositionOffset - ((transform as RectTransform).rect.size / 2)) / (transform as RectTransform).rect.size;
                var mouseOffset = new Vector2Int(Mathf.CeilToInt(relativeMouseOffset.x), -Mathf.CeilToInt(relativeMouseOffset.y));

                var positionToAdd = Position - positionOffset + mouseOffset;

                if (Container.CanAddAtPosition(positionToAdd, StaticDragDisplay.Instance.Package.Item.Dimensions, out _))
                {
                    Package remaining;

                    remaining = Container.AddAtPosition(positionToAdd, packageToMove);

                    if (0 < remaining.Amount)
                    {
                        packageToMove = remaining;
                        StaticDragDisplay.Instance.SetPackage(this, remaining);
                    }
                    else
                    {
                        packageToMove = new Package();

                        StaticDragDisplay.Instance.SetPackage(this, packageToMove);
                    }

                    Container.InvokeRefresh();
                    StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
                }
            }

            // must come after adding items to the container to have something to preview
            base.DropItem();
        }

        public override void RefreshSlotDisplay(Package package)
        {
            if (itemDisplay)
            {
                if (package.Amount <= 0)
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
                            var additionalSpacing = gridLayout.spacing * new Vector2(package.Item.Dimensions.x - 1, package.Item.Dimensions.y - 1);

                            display.sizeDelta = (gridLayout.cellSize * package.Item.Dimensions) + additionalSpacing;
                        }

                        display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
                        display.pivot = new Vector2(.5f, .5f);
                        display.anchorMin = new Vector2(0, 1);
                        display.anchorMax = new Vector2(0, 1);
                    }
                }
            }
        }

        protected override void EquipItem()
        {
            var otherItems = Container.GetOtherItemsAt(Position, new(1, 1));

            if (otherItems.Count == 1)
            {
                if (Container.StoredPackages[otherItems[0]].Item is Equipment)
                {
                    packageToMove = Container.StoredPackages[otherItems[0]];

                    _ = Container.RemoveAtPosition(otherItems[0], packageToMove);

                    Package remaining;

                    remaining = InventoryProvider.Instance.PlayerEquipment.AddAtPosition(Position, packageToMove);

                    if (0 < remaining.Amount)
                        remaining = Container.AddAtPosition(otherItems[0], remaining);

                    if (0 < remaining.Amount)
                    {
                        packageToMove = remaining;
                        StaticDragDisplay.Instance.SetPackage(this, remaining);
                    }
                    else
                    {
                        packageToMove = new Package();

                        StaticDragDisplay.Instance.SetPackage(this, packageToMove);
                    }

                    Container.InvokeRefresh();
                    StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
                }
                // if is consumable
                //  consume item
            }

            base.EquipItem();
        }
    }
}
