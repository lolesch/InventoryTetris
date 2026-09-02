using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
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
            if (!Container.CanPlaceAt(positionToAdd, ItemView.Of(package.Item).Dimensions))
                return;

            /// The whole drop runs inside one transaction (issue #10): the placement mutates
            /// a working copy, a displaced item is re-homed in the fixed
            /// cursor -> origin -> inventory order, and the move either commits as a unit or
            /// rolls back leaving the dragged item in hand. Commit fires the container
            /// refreshes and hands the cursor its displaced item.
            var origin = DragProvider.Instance.Origin?.Container;
            var inventory = InventoryProvider.Instance.Inventory;
            var cursor = new CursorHolder(DragProvider.Instance);

            using (var transaction = new ItemTransaction(cursor, Container, origin, inventory).ReHomeThrough(origin, inventory))
            {
                var displaced = Container.AddAtPosition(positionToAdd, package);

                if (displaced.IsValid)
                    _ = transaction.TryReHome(ref displaced);

                if (transaction.Aborted)
                    return;

                transaction.Commit();
            }

            /// A clean landing - nothing came back to the cursor, so the drag is over.
            /// A swap already handed the displaced item over on commit.
            if (cursor.IsFree)
                DragProvider.Instance.EndDrag();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
        }

        protected override void SetDisplaySize(RectTransform display, Package package)
        {
            base.SetDisplaySize(display, package);

            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();
            if (gridLayout)
            {
                var itemDimensions = ItemView.Of(package.Item).Dimensions;
                var additionalSpacing = gridLayout.spacing * new Vector2(itemDimensions.x - 1, itemDimensions.y - 1);

                display.sizeDelta = gridLayout.cellSize * itemDimensions + additionalSpacing;
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
                    var category = ItemView.Of(package.Item).Definition.Category;

                    if (category == ItemCategory.Consumable)
                    {
                        Debug.Log($"Consuming {ItemView.Of(package.Item).DisplayName}");

                        _ = Container.RemoveAtPosition(position, new Package(Container, package.Item, 1)); // only consume one amount

                        return;
                    }

                    if (category == ItemCategory.Equipment)
                    {
                        /// Route the equip through a transaction (issue #10): remove here,
                        /// equip there, re-home whatever the equip displaces back into this
                        /// container - or roll the whole thing back. No cursor: a right-click
                        /// equip is not a drag.
                        var equipment = InventoryProvider.Instance.Equipment;

                        using var transaction = new ItemTransaction(Container, equipment).ReHomeThrough(Container);

                        _ = Container.RemoveAtPosition(position, package);
                        _ = equipment.TryAddToContainer(ref package);

                        if (!transaction.Aborted)
                            transaction.Commit();

                        return;
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
                    var containerToMoveTo = Container; // rework to context based

                    if (Container == InventoryProvider.Instance.Inventory)
                        containerToMoveTo = InventoryProvider.Instance.Stash;
                    else if (Container == InventoryProvider.Instance.Stash)
                        containerToMoveTo = InventoryProvider.Instance.Inventory;

                    /// Atomic remove-here / add-there (issue #10): a target with no room
                    /// leaves the item exactly where it was.
                    using var transaction = new ItemTransaction(Container, containerToMoveTo);

                    _ = Container.RemoveAtPosition(position, package);

                    if (containerToMoveTo.TryAddToContainer(ref package))
                        transaction.Commit();

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
