using System.Runtime.CompilerServices;
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
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    internal sealed class InventorySlotDisplay : AbstractSlotDisplay
    {
        private GridLayoutGroup gridLayout;

        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            /// One rule, read off the drag display's real rect - the same answer the red
            /// overlap tint uses, so where it looks like it will land is where it lands.
            if (!DragProvider.Instance.TryGetDropPosition(this, out var positionToAdd))
                return;

            /// Nothing would land here - out of bounds, or 2+ items in the way. The item
            /// stays in hand exactly as the player is holding it; re-anchoring past this
            /// point is what snapped a rejected drop onto the grid.
            if (!Container.CanPlaceAt(positionToAdd, AbstractItem.GetDimensions(package.Item.Dimensions)))
                return;

            package = Container.AddAtPosition(positionToAdd, package);

            /// Whatever AddAtPosition handed back - nothing (it landed, drag ends) or the
            /// item it displaced (a swap). A displaced item is centred on the cursor, never
            /// given this drop's positionOffset, which describes a footprint it may not have.
            DragProvider.Instance.ReplacePackage(package);

            Container.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
        }

        protected override void SetDisplaySize(RectTransform display, Package package)
        {
            base.SetDisplaySize(display, package);

            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();
            if (gridLayout)
            {
                var additionalSpacing = gridLayout.spacing * new Vector2(AbstractItem.GetDimensions(package.Item.Dimensions).x - 1, AbstractItem.GetDimensions(package.Item.Dimensions).y - 1);

                display.sizeDelta = gridLayout.cellSize * AbstractItem.GetDimensions(package.Item.Dimensions) + additionalSpacing;
            }

            display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
            display.pivot = new Vector2(.5f, .5f);
            display.anchorMin = new Vector2(0, 1);
            display.anchorMax = new Vector2(0, 1);
        }

        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition)
        {
            if (Container == null)
                return;

            var position = Position;

            if (Container.TryGetItemAt(ref position, out var package))
            {
                FadeOutPreview();

                #region USE ITEM
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    switch (package.Item)
                    {
                        case ConsumableItem:
                            Debug.Log($"Consuming {package.Item.ToString()}");

                            _ = Container.RemoveAtPosition(position, new Package(Container, package.Item, 1)); // only consume one amount

                            return;

                        case EquipmentItem:
                        {
                            _ = Container.RemoveAtPosition(position, package);

                            if (InventoryProvider.Instance.Equipment.TryAddToContainer(ref package))
                                DragProvider.Instance.SetPackage(this, package, Vector2Int.zero, pointerPosition);
                            else
                                _ = Container.TryAddToContainer(ref package);

                            return;
                        }
                    }
                }
                #endregion USE ITEM

                // TODO: split in other amount => might want to split on dropping items
                #region SPLIT AMOUNT
                if (Input.GetKey(KeyCode.LeftControl))
                    if (2 <= package.Amount)
                        package.ReduceAmount(package.Amount / 2);
                #endregion SPLIT AMOUNT

                // TODO: trade context system
                #region QUICK MOVE ITEM
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    _ = Container.RemoveAtPosition(position, package);

                    var containerToMoveTo = Container; // rework to context based

                    if (Container == InventoryProvider.Instance.Inventory)
                        containerToMoveTo = InventoryProvider.Instance.Stash;
                    else if (Container == InventoryProvider.Instance.Stash)
                        containerToMoveTo = InventoryProvider.Instance.Inventory;

                    if (containerToMoveTo.TryAddToContainer(ref package))
                        DragProvider.Instance.SetPackage(this, package, Vector2Int.zero, pointerPosition);
                    else
                        _ = Container.AddAtPosition(position, package);

                    return;
                }
                #endregion QUICK MOVE ITEM

                #region DRAG ITEM
                _ = Container.RemoveAtPosition(position, package);

                var positionOffset = Position - position;

                DragProvider.Instance.SetPackage(this, package, positionOffset, pointerPosition);
                #endregion DRAG ITEM
            }

            FadeOutPreview();
        }
    }
}
