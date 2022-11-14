using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class PlayerEquipment : AbstractDimensionalContainer
    {
        public PlayerEquipment(Vector2Int dimensions) : base(dimensions) { }

        [SerializeField] public bool autoEquip = true;

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> requiredPositions = new();

            requiredPositions.Add(position);

            return requiredPositions;
        }

        protected internal override List<Vector2Int> GetOverlappingPositionsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();

            foreach (var package in storedPackages)
                if (package.Key == position)
                    otherPackagePositions.Add(package.Key);

            return otherPackagePositions;
        }

        public Vector2Int GetEquipmentTypePosition(EquipmentType equipment) => equipment switch
        {
            EquipmentType.Amulet => new(0, 0),
            EquipmentType.Belt => new(1, 0),
            EquipmentType.Boots => new(2, 0),
            EquipmentType.Bracers => new(3, 0),
            EquipmentType.Chest => new(4, 0),
            EquipmentType.Cloak => new(5, 0),
            EquipmentType.Gloves => new(6, 0),
            EquipmentType.Helm => new(7, 0),
            EquipmentType.Pants => new(8, 0),
            EquipmentType.Shoulders => new(9, 0),
            EquipmentType.Ring => IsEmptyPosition(new(10, 0), new(1, 1)) ? new(10, 0) : (IsEmptyPosition(new(11, 0), new(1, 1)) ? new(11, 0) : new(10, 0)),
            EquipmentType.Weapon_1H => IsEmptyPosition(new(12, 0), new(1, 1)) ? (new(12, 0)) : (IsEmptyPosition(new(13, 0), new(1, 1)) ? (new(13, 0)) : new(12, 0)),
            EquipmentType.Weapon_2H => new(12, 0),
            EquipmentType.Shield => new(13, 0),
            EquipmentType.Quiver => new(13, 0),
            EquipmentType.None => new(-1, -1),
            _ => new(-1, -1),
        };

        protected internal override bool IsWithinDimensions(Vector2Int position) =>
           -1 < position.x && position.x < Dimensions.x &&
           -1 < position.y && position.y < Dimensions.y;
    }
}
