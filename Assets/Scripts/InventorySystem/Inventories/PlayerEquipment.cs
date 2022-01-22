using UnityEngine;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Items;

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

        public Vector2Int GetEquipmentTypePosition(Equipment equipment)
        {
            /// this is my first switch I've ever written... after what, 3 years of programming? funny
            switch (equipment.equipmentType)
            {
                case Data.Enums.EquipmentType.Boots:
                    return new(0, 0);
                case Data.Enums.EquipmentType.Pants:
                    return new(1, 0);
                case Data.Enums.EquipmentType.Belt:
                    return new(2, 0);
                case Data.Enums.EquipmentType.Chest:
                    return new(3, 0);
                case Data.Enums.EquipmentType.Helm:
                    return new(4, 0);
                case Data.Enums.EquipmentType.Gloves:
                    return new(5, 0);
                case Data.Enums.EquipmentType.Bracers:
                    return new(6, 0);
                case Data.Enums.EquipmentType.Shoulders:
                    return new(7, 0);
                case Data.Enums.EquipmentType.Ring:
                    if (IsEmptyPosition(new(8, 0), new(1, 1)))
                        return new(8, 0);
                    return new(9, 0);
                case Data.Enums.EquipmentType.Amulet:
                    return new(10, 0);
                case Data.Enums.EquipmentType.MainHand:
                    return new(11, 0);
                case Data.Enums.EquipmentType.Offhand:
                    return new(12, 0);
                case Data.Enums.EquipmentType.Weapon_2H:
                    return new(11, 0);

                default:
                    return new Vector2Int(-1, -1);
            }
            return new Vector2Int();
        }

        protected internal override bool IsWithinDimensions(Vector2Int position) =>
           -1 < position.x && position.x < Dimensions.x &&
           -1 < position.y && position.y < Dimensions.y;
    }
}
