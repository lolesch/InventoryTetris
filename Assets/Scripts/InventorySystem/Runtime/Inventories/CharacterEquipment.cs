using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
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
        //=> GetTypeSpecificPositions(equipmentType).ToList();
        { //KEEP THIS AS REFERENCE FOR NOW
            List<Vector2Int> otherPackagePositions = new();

            foreach (var package in StoredPackages)
                if (package.Key == position)    // might need rework => get both weaponslots when asking for position 
                    otherPackagePositions.Add(package.Key);

            return otherPackagePositions;
        }

        public override Package AddToContainer(Package package)
        {
            if (package.Item == null)
                return package;

            if (package.Item is EquipmentItem)
                package = AddAtEquipmentTypePosition((package.Item as EquipmentItem).EquipmentType, package);

            return package;

        }
        public Package AddAtEquipmentTypePosition(EquipmentType equipmentType, Package package)
        {
            if (package.Item == null)
                return package;

            // find type specific positions
            var positions = GetTypeSpecificPositions(equipmentType);

            // see if one position is empty
            foreach (var position in positions)
                if (IsEmptyPosition(position, new(1, 1), out var other))
                {
                    // add to empty position
                    package = AddAtPosition(position, package);
                    return package;
                }

            // else try to swap
            package = AddAtPosition(positions[0], package);

            return package;
        }

        public Vector2Int[] GetTypeSpecificPositions(EquipmentType equipment) => equipment switch
        {
            EquipmentType.Amulet => new Vector2Int[1] { new(0, 0) },
            EquipmentType.Belt => new Vector2Int[1] { new(1, 0) },
            EquipmentType.Boots => new Vector2Int[1] { new(2, 0) },
            EquipmentType.Bracers => new Vector2Int[1] { new(3, 0) },
            EquipmentType.Chest => new Vector2Int[1] { new(4, 0) },
            EquipmentType.Cloak => new Vector2Int[1] { new(5, 0) },
            EquipmentType.Gloves => new Vector2Int[1] { new(6, 0) },
            EquipmentType.Helm => new Vector2Int[1] { new(7, 0) },
            EquipmentType.Pants => new Vector2Int[1] { new(8, 0) },
            EquipmentType.Shoulders => new Vector2Int[1] { new(9, 0) },

            EquipmentType.Ring => new Vector2Int[2] { new(10, 0), new(11, 0) },

            > EquipmentType.ONEHANDEDWEAPONS and < EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[2] { new(12, 0), new(13, 0) },

            > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS => new Vector2Int[1] { new(12, 0) },
            > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY => new Vector2Int[1] { new(13, 0) },

            // INVALID REQUESTS
            EquipmentType.NONE => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ARMAMENTS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ONEHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.OFFHANDS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.JEWELRY => new Vector2Int[1] { new(-1, -1) },
            _ => new Vector2Int[1] { new(-1, -1) },
        };
    }
}
