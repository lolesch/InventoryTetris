using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class CharacterInventory : AbstractDimensionalContainer
    {
        public CharacterInventory(Vector2Int dimensions) : base(dimensions) { }

        public override Package AddToContainer(Package package)
        {
            if (package.Item == null || package.Amount <= 0)
                return package;

            /// Auto Equip items that enter the localPlayer's inventory if that slot is empty
            if (this == InventoryProvider.Instance.Inventory)
            {
                var equipment = InventoryProvider.Instance.Equipment;

                if (package.Item is EquipmentItem && equipment.autoEquip)
                    package = equipment.AddToEmptyPosition(package);
            }

            /// Stack or add to empty position
            if (0 < package.Amount)
            {
                if (ItemStack.Single < package.Item.StackLimit)
                    AddToOpenStacks();

                if (0 < package.Amount)
                    package = AddToEmptyPosition(package);

                if (0 < package.Amount)
                    Debug.LogWarning($"{GetType().Name} is full!");

                // TODO: DragDrop adding to stacks is dimension dependent...
                // => this should simply check if a stack of the same item is at the drop position and add it.

                void AddToOpenStacks()
                {
                    var positions = StoredPackages.Keys.ToList();

                    for (var i = 0; i < positions.Count && 0 < package.Amount; i++)
                        if (StoredPackages[positions[i]].Item.Equals(package.Item))
                            if (0 < StoredPackages[positions[i]].SpaceLeft)
                                package = AddAtPosition(positions[i], package);
                }
            }

            /// Debug try add remaining package amount to player stash
            if (Debug.isDebugBuild)
            {
                if (0 < package.Amount)
                    if (this == InventoryProvider.Instance.Inventory)
                    {
                        Debug.LogWarning($"Trying to add the remaining amount of {package.Amount} to {InventoryProvider.Instance.Stash}");

                        package = InventoryProvider.Instance.Stash.AddToContainer(package);
                    }
            }

            InvokeRefresh();

            return package;
        }

        public override Package AddToEmptyPosition(Package package)
        {
            if (package.Item == null || package.Amount <= 0)
                return package;

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

            for (var x = 0; x < Dimensions.x && 0 < package.Amount; x++)
                for (var y = 0; y < Dimensions.y && 0 < package.Amount; y++)
                    if (IsEmptyPosition(new(x, y), dimensions, out _))
                        package = AddAtPosition(new(x, y), package);

            return package;
        }

        public override Package AddAtPosition(Vector2Int position, Package package)
        {
            if (package.Item == null || package.Amount <= 0)
                return package;

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

            if (IsEmptyPosition(position, dimensions, out var otherItems))
                TryAddToInventory();
            else if (1 == otherItems.Count)
                if (StoredPackages.TryGetValue(otherItems[0], out var storedPackage))
                    if (!TryStack(storedPackage))
                        TrySwap(storedPackage);

            InvokeRefresh();

            return package;

            void TryAddToInventory()
            {
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                    _ = package.ReduceAmount(amount);
            }

            bool TryStack(Package storedPackage)
            {
                if (0 < storedPackage.SpaceLeft)
                    if (package.Item.Equals(storedPackage.Item))
                    {
                        var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                        StoredPackages[position] = storedPackage;
                        _ = package.ReduceAmount(addedAmount);

                        return true;
                    }

                return false;
            }

            void TrySwap(Package storedPackage)
            {
                _ = RemoveAtPosition(position, storedPackage);

                TryAddToInventory();

                // TODO: check for item loss, else revert
                package = storedPackage;
            }
        }

        public override List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension)
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

    // CONTINUE HERE
    public class VendorSupply : AbstractDimensionalContainer
    {
        public VendorSupply(Vector2Int dimensions) : base(dimensions) { }

        public override Package AddToContainer(Package package)
        {
            if (package.Item == null || package.Amount <= 0)
                return package;
            if (package.Sender == InventoryProvider.Instance.Equipment || package.Sender == InventoryProvider.Instance.Inventory)
                return package;

            /// Stack or add to empty position
            if (0 < package.Amount)
            {
                if (ItemStack.Single < package.Item.StackLimit)
                    AddToOpenStacks();

                if (0 < package.Amount)
                    package = AddToEmptyPosition(package);

                if (0 < package.Amount)
                    Debug.LogWarning($"{this} is full!");

                // TODO: DragDrop adding to stacks is dimension dependent...
                // => this should simply check if a stack of the same item is at the drop position and add it.
                void AddToOpenStacks()
                {
                    var positions = StoredPackages.Keys.ToList();

                    for (var i = 0; i < positions.Count && 0 < package.Amount; i++)
                        if (StoredPackages[positions[i]].Item.Equals(package.Item))
                            if (0 < StoredPackages[positions[i]].SpaceLeft)
                                package = AddAtPosition(positions[i], package);
                }
            }
            return package;
        }

        public override Package AddAtPosition(Vector2Int position, Package package)
            => throw new NotImplementedException();
        public override Package AddToEmptyPosition(Package package) => throw new NotImplementedException();
        public override List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension) => throw new NotImplementedException();

        public void Restock() { }
    }
}
