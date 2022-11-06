using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class PlayerInventory : AbstractDimensionalContainer
    {
        public PlayerInventory(Vector2Int dimensions) : base(dimensions) { }

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> requiredPositions = new();

            for (int x = position.x; x < position.x + dimension.x; x++)
                for (int y = position.y; y < position.y + dimension.y; y++)
                    requiredPositions.Add(new(x, y));

            return requiredPositions;
        }

        protected internal override List<Vector2Int> GetStoredPackagePositionsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();

            foreach (var package in storedPackages)
                for (int x = package.Key.x; x < package.Key.x + package.Value.Item.Dimensions.x; x++)
                    for (int y = package.Key.y; y < package.Key.y + package.Value.Item.Dimensions.y; y++)
                        foreach (var requiredPosition in CalculateRequiredPositions(position, dimension))
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);

            return otherPackagePositions.Distinct().ToList();
        }

        protected internal override bool IsWithinDimensions(Vector2Int position) =>
           -1 < position.x && position.x < Dimensions.x &&
           -1 < position.y && position.y < Dimensions.y;
    }
}