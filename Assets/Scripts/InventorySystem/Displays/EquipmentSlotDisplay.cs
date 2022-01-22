using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]

    public class EquipmentSlotDisplay : AbstractSlotDisplay
    {
        [SerializeField] protected internal List<EquipmentType> allowedEquipmentTypes;

        protected internal override void PickUpItem()
        {
            var otherItems = container.GetStoredPackagesAtPosition(Position, new(1, 1));

            if (otherItems.Count == 1)
            {
                packageToMove = container.storedPackages[otherItems[0]];

                StaticDragDisplay.Instance.SetPackage(this, packageToMove);

                container.RemoveItemAtPosition(otherItems[0], packageToMove);
            }
        }

        protected internal override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item is Equipment)
                if (allowedEquipmentTypes.Contains((StaticDragDisplay.Instance.Package.Item as Equipment).equipmentType))
                    if (container.CanAddAtPosition(Position, StaticDragDisplay.Instance.Package.Item.Dimensions, out List<Vector2Int> otherItems))
                    {
                        Package remaining;

                        remaining = container.AddAtPosition(Position, packageToMove);

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
                    {
                        icon.sprite = package.Item.Icon;
                        if (package.Item is Equipment)
                            icon.color = package.randomColor;
                    }

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

        protected internal override void EquipItem()
        {
            throw new System.NotImplementedException();
        }

        protected internal override void UnequipItem()
        {
            var otherItems = container.GetStoredPackagesAtPosition(Position, new(1, 1));

            if (otherItems.Count == 1)
            {
                packageToMove = container.storedPackages[otherItems[0]];

                container.RemoveItemAtPosition(otherItems[0], packageToMove);

                Package remaining;

                remaining = InventoryProvider.PlayerInventory.AddToContainer(packageToMove);

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

                container.InvokeRefresh();
                StaticDragDisplay.Instance.packageOrigin.container.InvokeRefresh();
            }
        }
    }
}
