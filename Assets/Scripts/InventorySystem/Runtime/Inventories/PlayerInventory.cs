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
                            return equipment.AddAtPosition(position, package);
                }
            }

            package = base.AddToContainer(package);

            if (Debug.isDebugBuild) // remaining package amount => add to stash
            {
                if (0 < package.Amount)
                    if (this == InventoryProvider.Instance.PlayerInventory)
                        return InventoryProvider.Instance.PlayerStash.AddToContainer(package);
            }

            return package;
        }

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> requiredPositions = new();

            for (var x = position.x; x < position.x + dimension.x; x++)
                for (var y = position.y; y < position.y + dimension.y; y++)
                    requiredPositions.Add(new(x, y));

            return requiredPositions;
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
    }
}