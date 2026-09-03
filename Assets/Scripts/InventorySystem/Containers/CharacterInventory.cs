using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class CharacterInventory : AbstractDimensionalContainer
    {
        public CharacterInventory(Vector2Int dimensions) : base(dimensions) { }

        public override Package AddAtPosition(Vector2Int position, Package package)
        {
            if (!package.IsValid)
                return package;

            var dimensions = ItemView.Of(package.Item).Dimensions;

            if (IsEmptySpace(position, dimensions, out var otherItems))
                TryAddToInventory();
            else if (1 == otherItems.Count)
                if (StoredPackages.TryGetValue(otherItems[0], out var storedPackage))
                    if (!TryStack(storedPackage, otherItems[0]))
                        TrySwap(storedPackage, otherItems[0]);

            InvokeRefresh();

            return package;

            void TryAddToInventory()
            {
                var amount = Math.Min(package.Amount, ItemView.Of(package.Item).StackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                    _ = package.ReduceAmount(amount);
            }

            bool TryStack(Package storedPackage, Vector2Int storedPosition)
            {
                if (0 == storedPackage.SpaceLeft)
                    return false;

                if (!package.Item.StacksWith(storedPackage.Item, ItemView.Of(package.Item).StackLimit))
                    return false;

                var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                _ = package.ReduceAmount(addedAmount);

                StoredPackages[storedPosition] = storedPackage;

                return true;
            }

            void TrySwap(Package storedPackage, Vector2Int storedPosition)
            {
                _ = RemoveAtPosition(storedPosition, storedPackage);

                TryAddToInventory();

                // The one displaced item is handed straight back; a drag swap (issue #10)
                // re-homes it to the hand, and a routed move that cannot place it rolls the
                // whole swap back.
                package = storedPackage;
            }
        }

        public override List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();
            var requiredPositions = CalculateRequiredPositions(position, dimension);

            foreach (var package in StoredPackages)
            {
                var itemDimensions = ItemView.Of(package.Value.Item).Dimensions;
                for (var x = package.Key.x; x < package.Key.x + itemDimensions.x; x++)
                    for (var y = package.Key.y; y < package.Key.y + itemDimensions.y; y++)
                        foreach (var requiredPosition in requiredPositions)
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);
            }

            return otherPackagePositions.Distinct().ToList();
        }
    }
}
