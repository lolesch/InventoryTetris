using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
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
        [SerializeField] protected internal List<EquipmentType> allowedEquipmentTypes;

        private bool IsAllowedEquipmentType(EquipmentType type)
        {
            // TODO should derive this bool from the slot position

            for (var i = allowedEquipmentTypes.Count; i-- > 0;)
                if (allowedEquipmentTypes[i] == type)
                    return true;

            return allowedEquipmentTypes.Count <= 0;
        }

        protected override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item is EquipmentItem)
                if (IsAllowedEquipmentType((StaticDragDisplay.Instance.Package.Item as EquipmentItem).EquipmentType))
                {
                    var remaining = Container.AddAtPosition(Position, packageToMove);

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

            packageToMove = Container.StoredPackages[Position];

            _ = Container.RemoveAtPosition(Position, packageToMove);

            var remaining = InventoryProvider.Instance.Inventory.AddToContainer(packageToMove);

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
