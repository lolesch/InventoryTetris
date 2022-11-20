using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class PlayerInventory : AbstractDimensionalContainer
    {
        public PlayerInventory(Vector2Int dimensions) : base(dimensions) { }

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
                for (var x = package.Key.x; x < package.Key.x + package.Value.Item.Dimensions.x; x++)
                    for (var y = package.Key.y; y < package.Key.y + package.Value.Item.Dimensions.y; y++)
                        foreach (var requiredPosition in requiredPositions)
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);

            return otherPackagePositions.Distinct().ToList();
        }
    }
}