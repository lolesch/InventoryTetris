using ToolSmiths.InventorySystem.Data;
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
        [field: SerializeField] public EquipmentItem DebugItem;

        protected override void DropItem(Package package)
        {
            if (!package.IsValid || package.Item is not EquipmentItem item)
                return;

            var allowedPositions = CharacterEquipment.GetTypeSpecificPositions(item.EquipmentType);

            foreach (var position in allowedPositions)
                if (Position == position)
                {
                    package = Container.AddAtPosition(Position, package);

                    DragProvider.Instance.SetPackage(this, package, Vector2Int.zero);

                    Container.InvokeRefresh();
                    DragProvider.Instance.Origin.Container?.InvokeRefresh();

                    break;
                }

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

        protected override void MoveItem(PointerEventData eventData)
        {
            if (Container == null)
                return;

            var position = Position;

            if (!Container.TryGetItemAt(ref position, out var package))
                return;

            FadeOutPreview();

            if (package.Item is not EquipmentItem)
                Debug.LogWarning("Something went wrong!");

            #region UNEQUIP ITEM
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _ = Container.RemoveAtPosition(position, package);

                if (InventoryProvider.Instance.Inventory.TryAddToContainer(ref package))
                    DragProvider.Instance.SetPackage(this, package, Vector2Int.zero);
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
                    DragProvider.Instance.SetPackage(this, package, Vector2Int.zero);
                else
                    _ = Container.AddAtPosition(position, package);

                return;
            }
            #endregion QUICK MOVE ITEM

            #region DRAG ITEM
            _ = Container.RemoveAtPosition(position, package);

            // can equipment displays ever have an offset? See above => SetPackage is using Vector2Int.zero
            var positionOffset = Position - position;

            DragProvider.Instance.SetPackage(this, package, positionOffset);
            #endregion DRAG ITEM
        }
    }
}
