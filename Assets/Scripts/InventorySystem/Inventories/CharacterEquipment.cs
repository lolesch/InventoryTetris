using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class CharacterEquipment : AbstractDimensionalContainer
    {
        public CharacterEquipment(Vector2Int dimensions) : base(dimensions) { }

        [SerializeField] public bool autoEquip = true;

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension) => new() { position };

        public override List<Vector2Int> GetOtherItemsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();

            foreach (var package in StoredPackages)
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
            EquipmentType.Ring => IsEmptyPosition(new(10, 0), new(1, 1), out _) ? new(10, 0) : (IsEmptyPosition(new(11, 0), new(1, 1), out _) ? new(11, 0) : new(10, 0)),

            //EquipmentType.Shield => new(13, 0),
            //EquipmentType.Quiver => new(13, 0),
            //EquipmentType.Sword => IsEmptyPosition(new(12, 0), new(1, 1), out _) ? (new(12, 0)) : (IsEmptyPosition(new(13, 0), new(1, 1), out _) ? (new(13, 0)) : new(12, 0)),
            //EquipmentType.Bow => new(13, 0),
            //EquipmentType.GreatSword => new(12, 0),

            // might require enhanced logic
            > EquipmentType.ONEHANDEDWEAPONS and < EquipmentType.TWOHANDEDWEAPONS => IsEmptyPosition(new(12, 0), new(1, 1), out _) ? (new(12, 0)) : (IsEmptyPosition(new(13, 0), new(1, 1), out _) ? (new(13, 0)) : new(12, 0)),
            > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS => new(12, 0),
            > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY => new(13, 0),

            EquipmentType.NONE => new(-1, -1),
            EquipmentType.ARMAMENTS => new(-1, -1),
            EquipmentType.ONEHANDEDWEAPONS => new(-1, -1),
            EquipmentType.TWOHANDEDWEAPONS => new(-1, -1),
            EquipmentType.OFFHANDS => new(-1, -1),
            EquipmentType.JEWELRY => new(-1, -1),
            _ => new(-1, -1),
        };
    }
}
