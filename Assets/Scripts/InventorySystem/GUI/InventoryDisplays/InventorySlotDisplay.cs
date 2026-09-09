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

            /// A store purchase rides the placement as a commit-time effect (issue #31): the
            /// price was read once at pick-up and is charged exactly and only when the item
            /// actually lands here. No room, or can't afford, and the whole move rolls back -
            /// item stays in hand, nothing charged, exactly the guarantee the atomic buy gives.
            /// TryQueuePurchase upholds the check-then-queue order itself, so an unaffordable
            /// purchase cannot reach the placement and the queued payment can never fail at
            /// commit and strand an unpaid item in the bag.
            var purchasePrice = DragProvider.Instance.PurchasePrice;
            var wallet = InventoryProvider.Instance.Wallet;

            /// The whole drop runs inside one transaction (issue #10): the placement mutates
            /// a working copy, and the item the drag landed on goes to the hand - a drag
            /// swap always puts the displaced item in hand. The move commits as a unit or
            /// rolls back leaving the dragged item in hand. Commit fires the container
            /// refreshes and hands the cursor its displaced item.
            var origin = DragProvider.Instance.Origin?.Container;
            var inventory = InventoryProvider.Instance.Inventory;
            var cursor = new CursorHolder(DragProvider.Instance);

            using (var transaction = new ItemTransaction(cursor, Container, origin ?? inventory).ReHomeThrough(origin ?? inventory))
            {
                if (!VendorTransaction.TryQueuePurchase(transaction, wallet, purchasePrice))
                    return;

                var displaced = Container.AddAtPosition(positionToAdd, package);

                if (displaced.IsValid)
                    _ = transaction.TryReHomeToHandOrContainer(ref displaced, new PackageOrigin(Container, positionToAdd));

                if (transaction.Aborted)
                    return;

                transaction.Commit();
            }

            /// A clean landing - nothing came back to the cursor, so the drag is over.
            /// A swap already handed the displaced item over on commit.
            if (cursor.IsFree)
                DragProvider.Instance.EndDrag();

            SyncPreviewAfterMove();
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
                        /// Route the equip through a transaction (issue #10) as a right-click
                        /// "swap in place": remove here, equip there, and swap whatever the
                        /// equip displaces back into this same container. A player-driven move
                        /// always executes - one displaced item that will not re-fit overflows
                        /// to the hand, and only a second homeless item rolls the move back.
                        var equipment = InventoryProvider.Instance.Equipment;
                        var cursor = new CursorHolder(DragProvider.Instance);

                        using var transaction = new ItemTransaction(cursor, Container, equipment).ReHomeThrough(Container).SwapInPlace();

                        _ = Container.RemoveAtPosition(position, package);
                        _ = equipment.TryAddToContainer(ref package);

                        if (transaction.Aborted)
                            return;

                        transaction.Commit();

                        /// The displaced equipped item re-homes into this container, often
                        /// into the very cell just vacated - i.e. back under the cursor. The
                        /// leading FadeOutPreview dismissed the tooltip on the click; bring it
                        /// back for whatever now sits here (issue #13).
                        SyncPreviewAfterMove();
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

                #region QUICK MOVE ITEM
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    /// Quick-move follows the open side panel (issue #30): one pure resolver
                    /// decides where shift-click sends the item. With the Stash open it is
                    /// the same backpack ↔ Stash as always; with the Vendor open (issue #33)
                    /// it is a shift-click sale - the item goes into the Sell Basket; with
                    /// neither panel open nothing moves.
                    var intent = InventoryProvider.Instance.QuickMoveFor(Container);

                    if (intent.Kind == QuickMoveIntentKind.SellBasket)
                    {
                        /// One transaction over the source and the basket: the item leaves
                        /// this slot and lands in the basket with its origin remembered; a
                        /// full basket leaves it where it is (#33).
                        _ = SellBasketQuickMove.SendToBasket(InventoryProvider.Instance.Basket, Container, position);
                        return;
                    }

                    if (intent.Kind != QuickMoveIntentKind.MoveToContainer)
                        return;

                    var target = intent.Target;
                    var cursor = new CursorHolder(DragProvider.Instance);

                    using var transaction = new ItemTransaction(cursor, Container, target).ReHomeThrough(target);

                    _ = Container.RemoveAtPosition(position, package);
                    _ = transaction.TryReHomeToContainerOrHand(ref package, new PackageOrigin(Container, position));

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
