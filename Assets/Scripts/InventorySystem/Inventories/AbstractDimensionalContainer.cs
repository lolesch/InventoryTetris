using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using System.Linq;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Inventories
{
    [Serializable]
    public abstract class AbstractDimensionalContainer
    {
        public AbstractDimensionalContainer(Vector2Int dimensions) => Dimensions = dimensions;

        public readonly Vector2Int Dimensions;
        protected internal int Capacity => Dimensions.x * Dimensions.y;

        public event Action<Dictionary<Vector2Int, Package>> OnContentChanged;

        protected internal Dictionary<Vector2Int, Package> storedPackages = new Dictionary<Vector2Int, Package>();

        public Package AddToContainer(Package package)
        {
            if (!package.Item)
                return package;

            if (1 < package.Item.StackLimit)
                AddToOpenStacks();

            AddToEmptyPositions();

            return package;

            void AddToOpenStacks()
            {
                var positions = storedPackages.Keys.ToList();

                for (int i = 0; i < positions.Count && 0 < package.Amount; i++)
                    if (storedPackages[positions[i]].Item == package.Item && 0 < storedPackages[positions[i]].SpaceLeft)
                        package = AddAtPosition(positions[i], package);
            }

            void AddToEmptyPositions()
            {
                for (int x = 0; x < Dimensions.x && 0 < package.Amount; x++)
                    for (int y = 0; y < Dimensions.y && 0 < package.Amount; y++)
                        if (IsEmptyPosition(new(x, y), package.Item.Dimensions)) // this might need to be abstract and with a dimension of (1,1) for equipment
                            package = AddAtPosition(new(x, y), package);
            }
        }

        public Package AddAtPosition(Vector2Int position, Package package)
        {
            if (!package.Item)
                return package;

            if (this is PlayerEquipment && package.Item is Equipment)
                position = (this as PlayerEquipment).GetEquipmentTypePosition(package.Item as Equipment);

            if (CanAddAtPosition(position, package.Item.Dimensions, out List<Vector2Int> otherItems))
            {
                if (0 == otherItems.Count)
                    TryAddToInventory();

                if (1 == otherItems.Count)
                    TryStackOrSwap(otherItems[0]);

                OnContentChanged?.Invoke(storedPackages);
            }

            return package;

            void TryAddToInventory()
            {
                uint amount = Math.Min(package.Amount, package.Item.StackLimit);

                if (storedPackages.TryAdd(position, new Package(package.Item, amount)))
                    package.ReduceAmount(amount);
            }

            void TryStackOrSwap(Vector2Int position)
            {
                if (storedPackages.TryGetValue(position, out Package storedPackage))
                {
                    if (1 < package.Item.StackLimit && package.Item == storedPackage.Item && 0 < storedPackage.SpaceLeft)
                    {
                        uint addedAmount = storedPackage.IncreaseAmount(package.Amount);
                        storedPackages[position] = storedPackage;
                        package.ReduceAmount(addedAmount);
                    }
                    else /// swap items
                    {
                        RemoveItemAtPosition(position, storedPackage);

                        TryAddToInventory();

                        // TODO: check for item loss, else revert
                        package = storedPackage;
                    }
                }
            }
        }

        public Package RemoveFromContainer(Package package)
        {
            FindAllEqualItems(package.Item, out List<Vector2Int> positions);

            for (int i = positions.Count - 1; 0 <= i && 0 < package.Amount; i--)
                package = RemoveItemAtPosition(positions[i], package);

            return package;

            void FindAllEqualItems(AbstractItem item, out List<Vector2Int> positions)
            {
                positions = new List<Vector2Int>();

                foreach (var package in storedPackages)
                    if (package.Value.Item == item)
                        positions.Add(package.Key);

                positions.OrderBy(v => v.x);
            }
        }

        public Package RemoveItemAtPosition(Vector2Int position, Package package)
        {
            var storedPositions = GetStoredPackagesAtPosition(position, new(1, 1));

            if (storedPositions.Count == 1)
                if (storedPackages.TryGetValue(storedPositions[0], out Package storedPackage))
                {
                    uint removed = storedPackage.ReduceAmount(package.Amount);
                    package.ReduceAmount(removed);

                    if (0 < storedPackage.Amount)
                        storedPackages[storedPositions[0]] = storedPackage;
                    else
                        storedPackages.Remove(storedPositions[0]);
                }

            OnContentChanged?.Invoke(storedPackages);

            return package;
        }

        protected internal bool IsEmptyPosition(Vector2Int position, Vector2Int dimension)
        {
            if (IsValidPosition(position, dimension))
                return GetStoredPackagesAtPosition(position, dimension).Count < 1;
            return false;
        }

        protected internal bool CanAddAtPosition(Vector2Int position, Vector2Int dimension, out List<Vector2Int> otherItems)
        {
            otherItems = new();

            if (IsValidPosition(position, dimension))
            {
                otherItems = GetStoredPackagesAtPosition(position, dimension);
                return otherItems.Count < 2;
            }

            return false;
        }

        protected internal bool IsValidPosition(Vector2Int position, Vector2Int dimension)
        {
            foreach (var requiredPosition in CalculateRequiredPositions(position, dimension))
                if (!IsWithinDimensions(requiredPosition))
                    return false;
            return true;
        }

        protected internal abstract bool IsWithinDimensions(Vector2Int position);

        /// A List of all storedPackages positions that overlap with the requiredPositions
        protected internal abstract List<Vector2Int> GetStoredPackagesAtPosition(Vector2Int position, Vector2Int dimension);

        /// A List of all positions that are required to add this item to the container
        protected abstract List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension);

        internal void Sort()
        {
            List<Vector2Int> storedKeys = storedPackages.Keys.ToList();
            List<Vector2Int> storedDimensions = new();

            for (int i = 0; i < storedKeys.Count; i++)
                storedDimensions.Add(storedPackages[storedKeys[i]].Item.Dimensions);

            storedDimensions = storedDimensions.Distinct().OrderByDescending(v => v.sqrMagnitude).ToList();

            List<Package> storedValues = storedPackages.Values.ToList();
            storedPackages.Clear();

            for (int i = 0; i < storedDimensions.Count; i++)
                for (int j = 0; j < storedValues.Count; j++)
                    if (storedValues[j].Item.Dimensions == storedDimensions[i])
                    {
                        AddToContainer(storedValues[j]);
                    }
        }

        protected internal void InvokeRefresh() => OnContentChanged?.Invoke(storedPackages);
    }
}
