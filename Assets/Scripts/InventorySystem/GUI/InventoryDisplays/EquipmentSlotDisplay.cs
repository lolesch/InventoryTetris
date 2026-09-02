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

            /// The whole equip runs inside one transaction (issue #10): a 2H over a weapon
            /// and off-hand re-homes both displaced items in the fixed
            /// cursor -> origin -> inventory order, or - if one has nowhere to go - rolls the
            /// swap back and leaves the incoming item in hand. Commit fires the container
            /// refreshes, applies the worn affixes and hands the cursor its displaced item.
            var origin = DragProvider.Instance.Origin?.Container;
            var inventory = InventoryProvider.Instance.Inventory;
            var cursor = new CursorHolder(DragProvider.Instance);

            using (var transaction = new ItemTransaction(cursor, Container, origin, inventory).ReHomeThrough(origin, inventory))
            {
                var displaced = Container.AddAtPosition(Position, package);

                if (displaced.IsValid)
                    _ = transaction.TryReHome(ref displaced);

                if (transaction.Aborted)
                    return;

                transaction.Commit();
            }

            /// A clean equip - nothing came back to the cursor, so the drag is over.
            if (cursor.IsFree)
                DragProvider.Instance.EndDrag();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
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

                if (!inventory.TryAddToContainer(ref package))
                    _ = cursor.TryHold(package);

                transaction.Commit();

                return;
            }
            #endregion UNEQUIP ITEM

            // TODO: trade context system
            #region QUICK MOVE ITEM
            if (Input.GetKey(KeyCode.LeftShift))
            {
                var stash = InventoryProvider.Instance.Stash;

                using var transaction = new ItemTransaction(Container, stash);

                _ = Container.RemoveAtPosition(position, package);

                if (stash.TryAddToContainer(ref package))
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
