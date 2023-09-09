using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class PlayerInventory : AbstractDimensionalContainer
    {
        public PlayerInventory(Vector2Int dimensions) : base(dimensions) { }

        public override Package AddToContainer(Package package)
        {
            if (package.Item == null)
                return package;

            /// Auto Equip items that enter the localPlayer's inventory if that slot is empty
            if (this == InventoryProvider.Instance.PlayerInventory)
            {
                var equipment = InventoryProvider.Instance.PlayerEquipment;

                if (package.Item is EquipmentItem && equipment.autoEquip)
                {
                    var typePositions = equipment.GetTypeSpecificPositions((package.Item as EquipmentItem).EquipmentType);

                    foreach (var position in typePositions)
                        if (equipment.IsEmptyPosition(position, new(1, 1), out _))
                        {
                            package = equipment.AddAtPosition(position, package);

                            InvokeRefresh();

                            return package;
                        }
                }
            }

            package = base.AddToContainer(package);

            /// try add remaining package amount to player stash
            if (Debug.isDebugBuild)
            {
                if (0 < package.Amount)
                    if (this == InventoryProvider.Instance.PlayerInventory)
                    {
                        Debug.LogWarning($"Trying to add the remaining amount of {package.Amount} to {InventoryProvider.Instance.PlayerStash}");

                        package = InventoryProvider.Instance.PlayerStash.AddToContainer(package);
                    }
            }

            InvokeRefresh();

            return package;
        }

        public override List<Vector2Int> GetOtherItemsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();
            var requiredPositions = CalculateRequiredPositions(position, dimension);

            foreach (var package in StoredPackages)
                for (var x = package.Key.x; x < package.Key.x + AbstractItem.GetDimensions(package.Value.Item.Dimensions).x; x++)
                    for (var y = package.Key.y; y < package.Key.y + AbstractItem.GetDimensions(package.Value.Item.Dimensions).y; y++)
                        foreach (var requiredPosition in requiredPositions)
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);

            return otherPackagePositions.Distinct().ToList();
        }

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> requiredPositions = new();

            for (var x = position.x; x < position.x + dimension.x; x++)
                for (var y = position.y; y < position.y + dimension.y; y++)
                    requiredPositions.Add(new(x, y));

            return requiredPositions;
        }
    }
}