using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]

    public class EquipmentSlotDisplay : AbstractSlotDisplay
    {
        [SerializeField] protected internal List<EquipmentType> allowedEquipmentTypes;

        protected internal override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item is Equipment)
                if (allowedEquipmentTypes.Contains((StaticDragDisplay.Instance.Package.Item as Equipment).equipmentType))
                    if (Container.CanAddAtPosition(Position, StaticDragDisplay.Instance.Package.Item.Dimensions, out var otherItems))
                    {
                        Package remaining;

                        remaining = Container.AddAtPosition(Position, packageToMove);

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

            // must come after adding items to the container to have something to preview
            base.DropItem();
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
                        //if (!gridLayout)
                        //    gridLayout = GetComponentInParent<GridLayoutGroup>();
                        //if (gridLayout)
                        //{
                        //    Vector2 additionalSpacing = gridLayout.spacing * new Vector2(package.Item.Dimensions.x - 1, package.Item.Dimensions.y - 1);
                        //
                        //    display.sizeDelta = gridLayout.cellSize * package.Item.Dimensions + additionalSpacing;
                        //}
                        //display.sizeDelta = new Vector2(60, 60) * package.Item.Dimensions;

                        //display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
                        //display.pivot = new Vector2(.5f, .5f);
                        //display.anchorMin = new Vector2(0, 1);
                        //display.anchorMax = new Vector2(0, 1);
                    }
                }
            }
        }

        protected internal override void UnequipItem()
        {
            base.UnequipItem();

            var otherItems = Container.GetStoredPackagePositionsAt(Position, new(1, 1));

            if (otherItems.Count == 1)
            {
                packageToMove = Container.storedPackages[otherItems[0]];

                Container.RemoveItemAtPosition(otherItems[0], packageToMove);

                Package remaining;

                remaining = InventoryProvider.Instance.PlayerInventory.AddToContainer(packageToMove);

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
    }
}
