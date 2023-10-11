using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]

    public class EquipmentSlotDisplay : AbstractSlotDisplay
    {
        [field: SerializeField] public EquipmentItem DebugItem;

        protected override void DropItem()
        {
            if (DragProvider.Instance.Package.Item is EquipmentItem)
            {
                var allowedPositions = CharacterEquipment.GetTypeSpecificPositions((DragProvider.Instance.Package.Item as EquipmentItem).EquipmentType);

                foreach (var position in allowedPositions)
                    if (Position == position)
                    {
                        var remaining = Container.AddAtPosition(Position, packageToMove);

                        if (0 < remaining.Amount)
                        {
                            packageToMove = remaining;
                            DragProvider.Instance.SetPackage(this, remaining, Vector2Int.zero);
                        }
                        else
                        {
                            packageToMove = new Package();

                            DragProvider.Instance.SetPackage(this, packageToMove, Vector2Int.zero);
                        }

                        Container.InvokeRefresh();
                        DragProvider.Instance.Origin.Container?.InvokeRefresh();

                        break;
                    }
            }

            // must come after adding items to the container to have something to preview
            base.DropItem();
        }

        protected override void UnequipItem()
        {
            base.UnequipItem();

            packageToMove = Container.StoredPackages[Position];

            _ = Container.RemoveAtPosition(Position, packageToMove);

            packageToMove = InventoryProvider.Instance.Inventory.AddToContainer(packageToMove);

            if (0 < packageToMove.Amount)
                DragProvider.Instance.SetPackage(this, packageToMove, Vector2Int.zero);

            //Container.InvokeRefresh();
            //StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
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
    }
}
