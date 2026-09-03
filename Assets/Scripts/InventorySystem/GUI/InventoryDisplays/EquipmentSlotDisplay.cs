using System.Linq;
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
            if (!package.IsValid)
                return;

            var definition = ItemView.Of(package.Item).Definition;
            if (definition.Category != ItemCategory.Equipment)
                return;

            var allowedPositions = CharacterEquipment.GetTypeSpecificPositions(definition.EquipmentType);

            /// Wrong slot for this item's type: the item stays in hand untouched, the same
            /// contract bug 2 gives a rejected drop on the inventory grid - no re-anchor.
            if (!allowedPositions.Contains(Position))
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
                var displaced = Container.AddAtPosition(Position, package);

                if (displaced.IsValid)
                    _ = transaction.TryReHomeToHandOrContainer(ref displaced);

                if (transaction.Aborted)
                    return;

                transaction.Commit();
            }

            /// A clean equip - nothing came back to the cursor, so the drag is over.
            if (cursor.IsFree)
                DragProvider.Instance.EndDrag();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
        }

        /// <summary>
        /// The equipment slots are a paper-doll, not a uniform grid, so the pixel-derived
        /// drop position the base uses is meaningless here (issue #12) - it drifts off the
        /// 14x1 logical row and reddened every hover. Mirror <see cref="DropItem"/> instead:
        /// the item lands at this slot's own fixed position, and only when the slot is one
        /// this equipment type is allowed in.
        /// </summary>
        public override bool WouldAcceptDrop(Package package)
        {
            if (!package.IsValid || Container == null)
                return false;

            var definition = ItemView.Of(package.Item).Definition;

            return definition.Category == ItemCategory.Equipment
                && CharacterEquipment.GetTypeSpecificPositions(definition.EquipmentType).Contains(Position)
                && Container.CanPlaceAt(Position, ItemView.Of(package.Item).Dimensions);
        }

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
                _ = transaction.TryReHomeToContainerOrHand(ref package);

                transaction.Commit();

                return;
            }
            #endregion UNEQUIP ITEM

            // TODO: trade context system
            #region QUICK MOVE ITEM
            if (Input.GetKey(KeyCode.LeftShift))
            {
                /// Player-driven quick-move (issue #10): the item comes off into the stash,
                /// or - if it is full - into the hand. Always executes; the affix lift rides
                /// the commit.
                var stash = InventoryProvider.Instance.Stash;
                var cursor = new CursorHolder(DragProvider.Instance);

                using var transaction = new ItemTransaction(cursor, Container, stash).ReHomeThrough(stash);

                _ = Container.RemoveAtPosition(position, package);
                _ = transaction.TryReHomeToContainerOrHand(ref package);

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
