using UnityEngine;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class PlayerEquipment : AbstractDimensionalContainer
    {
        public PlayerEquipment(Vector2Int dimensions) : base(dimensions) { }

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> requiredPositions = new();

            requiredPositions.Add(position);

            return requiredPositions;
        }

        protected internal override List<Vector2Int> GetStoredPackagesAtPosition(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();

            foreach (var package in storedPackages)
                if (package.Key == position)
                    otherPackagePositions.Add(package.Key);

            return otherPackagePositions;
        }

        protected internal override bool IsWithinDimensions(Vector2Int position) =>
           -1 < position.x && position.x < Dimensions.x &&
           -1 < position.y && position.y < Dimensions.y;
    }
}
