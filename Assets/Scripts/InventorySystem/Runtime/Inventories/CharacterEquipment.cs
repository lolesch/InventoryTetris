using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class CharacterEquipment : AbstractDimensionalContainer
    {
        public CharacterEquipment(Vector2Int dimensions) : base(dimensions) { }

        [SerializeField] public bool autoEquip = true;

        public override List<Vector2Int> GetOtherItemsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();
            var requiredPositions = CalculateRequiredPositions(position, dimension);

            foreach (var package in StoredPackages)
                foreach (var requiredPosition in requiredPositions)
                    if (package.Key == requiredPosition)
                        otherPackagePositions.Add(package.Key);

            return otherPackagePositions;
        }

        public override Package AddToContainer(Package package)
        {
            if (package.Item == null || package.Item is not EquipmentItem)
                return package;

            InvokeRefresh();

            return AddAtEquipmentTypePosition((package.Item as EquipmentItem).EquipmentType, package);

            Package AddAtEquipmentTypePosition(EquipmentType equipmentType, Package package)
            {
                var typePositions = GetTypeSpecificPositions(equipmentType);

                foreach (var position in typePositions)
                    if (IsEmptyPosition(position, new(1, 1), out _))
                        return AddAtPosition(position, package);

                return AddAtPosition(typePositions[0], package);
            }
        }

        public override Package AddAtPosition(Vector2Int position, Package package)
        {
            if (package.Item == null || package.Item is not EquipmentItem)
                return package;

            if (ItemStack.Single < package.Item.StackLimit)
                Debug.LogWarning($"EquipmentItems should not be stackable! {package.Item.StackLimit}");

            var equipmentType = (package.Item as EquipmentItem).EquipmentType;
            var dimensions = IsTwoHandedWeapon(equipmentType) ? new Vector2Int(2, 1) : new Vector2Int(1, 1);

            if (IsEmptyPosition(position, dimensions, out var otherItems))
                TryAddToInventory(dimensions);
            else
                TrySwap(otherItems, dimensions);

            InvokeRefresh();

            return package;

            bool IsTwoHandedWeapon(EquipmentType equipmentType) => equipmentType is > EquipmentType.TWOHANDEDWEAPONS && equipmentType < EquipmentType.OFFHANDS;

            void TryAddToInventory(Vector2Int dimensions)
            {
                // TODO: use the dimensions 
                // var requiredPositions = CalculateRequiredPositions(position, dimensions);
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                {
                    ItemProvider.Instance.LocalPlayer.AddItemStats(package.Item.Affixes);

                    _ = package.ReduceAmount(amount);
                }
            }

            void TrySwap(List<Vector2Int> positions, Vector2Int dimensions)
            {
                // TODO: unequip offhands when equiping a 2H
                // TODO: unequip 2h when equiping an offhand
                // TODO: if adding a 2h add a 2h dummy item in the offhand => obsolete because the dimension handles that?

                //var equipmentType = (package.Item as EquipmentItem).EquipmentType;
                //if (equipmentType is > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS) { }
                //else if (equipmentType is > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY) { }

                var otherPackages = new List<Package>();

                foreach (var position in positions)
                    if (StoredPackages.TryGetValue(position, out var storedPackage))
                    {
                        _ = RemoveAtPosition(position, storedPackage);
                        otherPackages.Add(storedPackage);
                    }

                TryAddToInventory(dimensions);

                if (0 < package.Amount)
                    Debug.LogWarning($"Something went wrong! remaining package will be overwritten: {package} by {otherItems[0]}");

                package = otherPackages[0];

                if (otherPackages.Count == 2)
                    // TODO: try add swaped packages to the container the package was coming from
                    // else set static drag display
                    StaticDragDisplay.Instance.SetPackage(StaticDragDisplay.Instance.Hovered, otherPackages[1], Vector2Int.zero);

                // TODO: check for item loss, else revert
            }
        }

        /*private Vector2Int[] GetTypeSpecificRequiredPositions(EquipmentType equipment) => equipment switch
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
            EquipmentType.Ring => new Vector2Int[1] { new(10, 0) },

            > EquipmentType.ONEHANDEDWEAPONS and < EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[1] { new(12, 0) },

            > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS => new Vector2Int[2] { new(12, 0), new(13, 0) },
            > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY => new Vector2Int[1] { new(13, 0) },

            // INVALID REQUESTS
            EquipmentType.NONE => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ARMAMENTS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ONEHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.OFFHANDS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.JEWELRY => new Vector2Int[1] { new(-1, -1) },
            _ => new Vector2Int[1] { new(-1, -1) },
        };*/

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

            /// INVALID REQUESTS
            EquipmentType.NONE => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ARMAMENTS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ONEHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.OFFHANDS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.JEWELRY => new Vector2Int[1] { new(-1, -1) },
            _ => new Vector2Int[1] { new(-1, -1) },
        };

        protected override List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            // TODO: CHANGE THIS TO MATCH EQIPMENT REQUIREMENTS
            List<Vector2Int> requiredPositions = new() { position };
            return requiredPositions;
        }
    }
}
