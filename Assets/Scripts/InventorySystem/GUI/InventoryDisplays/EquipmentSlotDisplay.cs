using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]

    internal sealed class EquipmentSlotDisplay : AbstractSlotDisplay
    {
        protected override void DropItem(Package package)
        {
            if (!package.IsValid || Container is not CharacterEquipment equipment)
                return;

            /// One predicate for the drop and the red "can't drop" tint (issue #12): a wrong
            /// category, a slot this type is not allowed in, or a slot that cannot take the
            /// item even with its swap - the item stays in hand untouched, exactly as a
            /// rejected drop on the inventory grid does.
            if (!equipment.CanEquipAt(Position, package.Item))
                return;

            /// A store purchase rides the equip as a commit-time effect (issue #31): the price was
            /// read once at pick-up and is charged exactly and only when the item actually
            /// lands on the paperdoll. No room, or can't afford, and the whole move rolls back
            /// - item stays in hand, nothing charged. A purchase the player can't afford is
            /// rejected up front (item stays in hand), so the queued payment can never fail at
            /// commit and strand an unpaid item equipped.
            var purchasePrice = DragProvider.Instance.PurchasePrice;
            var wallet = InventoryProvider.Instance.Wallet;

            if (purchasePrice is float price && !VendorTransaction.CanAffordBuy(wallet, price))
                return;

            /// The whole equip runs inside one transaction (issue #10): the weapon under the
            /// drop goes to the hand, exactly as a plain swap does; a 2H also sheds a
            /// collateral off-hand, which swaps back into the origin container or - with
            /// nowhere to go - rolls the whole move back and leaves the 2H in hand. Commit
            /// fires the container refreshes, applies the worn affixes and hands the cursor
            /// its displaced item.
            var origin = DragProvider.Instance.Origin?.Container;
            var inventory = InventoryProvider.Instance.Inventory;
            var cursor = new CursorHolder(DragProvider.Instance);

            using (var transaction = new ItemTransaction(cursor, Container, origin ?? inventory).ReHomeThrough(origin ?? inventory))
            {
                if (purchasePrice is float purchase)
                    VendorTransaction.QueuePurchasePayment(transaction, wallet, purchase);

                var displaced = Container.AddAtPosition(Position, package);

                if (displaced.IsValid)
                    _ = transaction.TryReHomeToHandOrContainer(ref displaced, Container, Position);

                if (transaction.Aborted)
                    return;

                transaction.Commit();
            }

            /// A clean equip - nothing came back to the cursor, so the drag is over.
            if (cursor.IsFree)
                DragProvider.Instance.EndDrag();

            SyncPreviewAfterMove();
        }

        /// <summary>
        /// The equipment slots are a paper-doll, not a uniform grid (issue #12): the base's
        /// pixel-derived drop position and the item's inventory-bag footprint are both
        /// meaningless here. Defer to <see cref="CharacterEquipment.CanEquipAt"/> - the same
        /// predicate <see cref="DropItem"/> gates on - so the item lands at this slot's own
        /// fixed position, checked with the equipment layout's footprint rule.
        /// </summary>
        public override bool WouldAcceptDrop(Package package)
            => package.IsValid
            && Container is CharacterEquipment equipment
            && equipment.CanEquipAt(Position, package.Item);

        public void Refresh2HandSlotDisplay(Package package)
        {
            RefreshSlotDisplay(package);

            if (icon)
                icon.color = new Color(1, 1, 1, .3f);

            if (frame)
                frame.color *= new Color(1, 1, 1, .4f);

            if (background)
                background.color *= new Color(1, 1, 1, .4f);
        }

        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition)
        {
            if (Container == null)
                return;

            var position = Position;

            if (!Container.TryGetItemAt(ref position, out var package))
                return;

            FadeOutPreview();

            if (ItemView.Of(package.Item).Definition.Category != ItemCategory.Equipment)
                Debug.LogWarning("Something went wrong!");

            #region UNEQUIP ITEM
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                /// A player-driven unequip always executes (issue #10): the item lands in the
                /// inventory, or - if it is full - in hand. The affix lift is a commit-time
                /// effect either way.
                var inventory = InventoryProvider.Instance.Inventory;
                var cursor = new CursorHolder(DragProvider.Instance);

                using var transaction = new ItemTransaction(cursor, Container, inventory).ReHomeThrough(inventory);

                _ = Container.RemoveAtPosition(position, package);
                _ = transaction.TryReHomeToContainerOrHand(ref package, Container, position);

                transaction.Commit();

                return;
            }
            #endregion UNEQUIP ITEM

            #region QUICK MOVE ITEM
            if (Input.GetKey(KeyCode.LeftShift))
            {
                /// Quick-move follows the open side panel (issue #30): equipment shift-clicks
                /// to whichever panel is open. With the Stash open that is the stash, as
                /// always; with neither panel open - or the Vendor open - nothing moves. The
                /// move stays (issue #10): the item comes off, or - if the target is full -
                /// into the hand; the affix lift rides the commit.
                var intent = QuickMoveResolver.Resolve(InventoryProvider.Instance.ActiveSidePanel, Container,
                    InventoryProvider.Instance.Inventory,
                    InventoryProvider.Instance.Stash,
                    InventoryProvider.Instance.Equipment,
                    InventoryProvider.Instance.Store);

                if (intent.Kind != QuickMoveIntentKind.MoveToContainer)
                    return;

                var target = intent.Target;
                var cursor = new CursorHolder(DragProvider.Instance);

                using var transaction = new ItemTransaction(cursor, Container, target).ReHomeThrough(target);

                _ = Container.RemoveAtPosition(position, package);
                _ = transaction.TryReHomeToContainerOrHand(ref package, Container, position);

                transaction.Commit();

                return;
            }
            #endregion QUICK MOVE ITEM

            #region DRAG ITEM
            _ = Container.RemoveAtPosition(position, package);

            // can equipment displays ever have an offset? See above => SetPackage is using Vector2Int.zero
            var positionOffset = Position - position;

            DragProvider.Instance.SetPackage(this, package, positionOffset, pointerPosition);
            #endregion DRAG ITEM
        }
    }
}
