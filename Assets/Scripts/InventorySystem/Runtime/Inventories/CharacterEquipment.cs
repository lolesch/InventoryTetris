using System;
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
        {
            List<Vector2Int> otherPackagePositions = new();

            foreach (var package in StoredPackages)
                if (package.Key == position)
                    // might need rework => get both weaponslots when asking for position 
                    // otherPackagePositions = GetTypeSpecificRequiredPositions((package.Value.Item as EquipmentItem).EquipmentType).ToList();
                    otherPackagePositions.Add(package.Key);

            return otherPackagePositions;
        }

        public override Package AddToContainer(Package package)
        {
            if (package.Item == null || package.Item is not EquipmentItem)
                return package;

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

            if (CanAddAtPosition(position, new(1, 1), out var otherItems))
            {
                if (0 == otherItems.Count)
                    TryAddToInventory();

                if (1 == otherItems.Count)
                    TrySwap(otherItems[0]);

                //OnContentChanged?.Invoke(StoredPackages);
            }

            return package;

            void TryAddToInventory()
            {
                //TODO: if adding a 2h add a 2h dummy item in the offhand
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(package.Item, amount)))
                {
                    ItemProvider.Instance.LocalPlayer.AddItemStats(package.Item.Affixes);

                    _ = package.ReduceAmount(amount);
                }

                //OnContentChanged?.Invoke(StoredPackages);
            }

            void TrySwap(Vector2Int position)
            {
                if (StoredPackages.TryGetValue(position, out var storedPackage))
                {
                    var equipmentType = (package.Item as EquipmentItem).EquipmentType;

                    // TODO: swap might return 2 items (2h only)
                    /// unequip offhands when equiping a 2H
                    if (equipmentType is > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS)
                    {
                        var requiredPositions = new Vector2Int[2] { new(12, 0), new(13, 0) };
                        //InventoryProvider.Instance.PlayerEquipment.GetOtherItemsAt(position, dimensions);
                        // TODO: CONTINUE HERE or implement overrides for CanAddAtPosition or AddAtPosition in the CharacterEquipment
                    }
                    /// unequip 2h when equiping an offhand
                    else if (equipmentType is > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY)
                    { }

                    _ = RemoveAtPosition(position, storedPackage);

                    TryAddToInventory();

                    // TODO: check for item loss, else revert
                    package = storedPackage;
                }
            }
        }

        private Vector2Int[] GetTypeSpecificRequiredPositions(EquipmentType equipment) => equipment switch
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
        };
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
