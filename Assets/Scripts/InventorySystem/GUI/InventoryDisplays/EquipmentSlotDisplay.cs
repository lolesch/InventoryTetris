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

            package = Container.AddAtPosition(Position, package);

            /// Nothing back if it just equipped; the previously-equipped item if it swapped.
            /// Either way it is centred on the cursor, not given a stale grip.
            DragProvider.Instance.ReplacePackage(package);

            Container.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

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
                _ = Container.RemoveAtPosition(position, package);

                if (InventoryProvider.Instance.Inventory.TryAddToContainer(ref package))
                    DragProvider.Instance.SetPackage(this, package, Vector2Int.zero, pointerPosition);
                else
                    _ = Container.AddAtPosition(position, package);

                return;
            }
            #endregion UNEQUIP ITEM

            // TODO: trade context system
            #region QUICK MOVE ITEM
            if (Input.GetKey(KeyCode.LeftShift))
            {
                _ = Container.RemoveAtPosition(position, package);

                if (InventoryProvider.Instance.Stash.TryAddToContainer(ref package))
                    DragProvider.Instance.SetPackage(this, package, Vector2Int.zero, pointerPosition);
                else
                    _ = Container.AddAtPosition(position, package);

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
