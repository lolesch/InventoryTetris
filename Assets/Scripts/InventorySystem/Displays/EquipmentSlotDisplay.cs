using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]

    public class EquipmentSlotDisplay : AbstractSlotDisplay
    {
        [SerializeField] protected internal List<EquipmentType> allowedEquipmentTypes;

        private bool IsAllowedEquipmentType(EquipmentType type)
        {
            if (allowedEquipmentTypes.Count <= 0)
                return true;

            for (var i = 0; i < allowedEquipmentTypes.Count; i++)
                if (allowedEquipmentTypes[i] == type)
                    return true;

            return false;
        }

        protected override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item is EquipmentItem)
                //if (allowedEquipmentTypes.Contains((StaticDragDisplay.Instance.Package.Item as Equipment).equipmentType))
                if (IsAllowedEquipmentType((StaticDragDisplay.Instance.Package.Item as EquipmentItem).EquipmentType))
                    if (Container.CanAddAtPosition(Position, AbstractItem.GetDimensions(StaticDragDisplay.Instance.Package.Item.Dimensions), out _))
                    {
                        Package remaining;

                        remaining = Container.AddAtPosition(Position, packageToMove);

                        if (0 < remaining.Amount)
                        {
                            packageToMove = remaining;
                            StaticDragDisplay.Instance.SetPackage(this, remaining, Vector2Int.zero);
                        }
                        else
                        {
                            packageToMove = new Package();

                            StaticDragDisplay.Instance.SetPackage(this, packageToMove, Vector2Int.zero);
                        }

                        Container.InvokeRefresh();
                        StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
                    }

            // must come after adding items to the container to have something to preview
            base.DropItem();
        }

        protected override void UnequipItem()
        {
            base.UnequipItem();

            var otherItems = Container.GetOtherItemsAt(Position, new(1, 1));

            if (otherItems.Count == 1)
            {
                packageToMove = Container.StoredPackages[otherItems[0]];

                _ = Container.RemoveAtPosition(otherItems[0], packageToMove);

                Package remaining;

                remaining = InventoryProvider.Instance.PlayerInventory.AddToContainer(packageToMove);

                if (0 < remaining.Amount)
                {
                    packageToMove = remaining;
                    StaticDragDisplay.Instance.SetPackage(this, remaining, Vector2Int.zero);
                }
                else
                {
                    packageToMove = new Package();

                    StaticDragDisplay.Instance.SetPackage(this, packageToMove, Vector2Int.zero);
                }

                Container.InvokeRefresh();
                StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
            }
        }
    }
}
