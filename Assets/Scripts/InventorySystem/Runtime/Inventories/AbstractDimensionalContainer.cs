using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [Serializable]
    public abstract class AbstractDimensionalContainer
    {
        public AbstractDimensionalContainer(Vector2Int dimensions) => Dimensions = dimensions;

        public readonly Vector2Int Dimensions;
        public int Capacity => Dimensions.x * Dimensions.y;

        public event Action<Dictionary<Vector2Int, Package>> OnContentChanged;

        public Dictionary<Vector2Int, Package> StoredPackages { get; protected set; } = new();

        // TODO: make this abstract?
        public virtual Package AddToContainer(Package package)
        {
            if (package.Item == null)
                return package;

            if (ItemStack.Single < package.Item.StackLimit)
                AddToOpenStacks();

            if (0 < package.Amount)
                AddToEmptyPositions();

            if (0 < package.Amount)
                Debug.LogWarning($"Could not add the remaining amount of {package.Amount} to {this}");

            return package;

            void AddToOpenStacks()
            {
                var positions = StoredPackages.Keys.ToList();

                for (var i = 0; i < positions.Count && 0 < package.Amount; i++)
                    if (StoredPackages[positions[i]].Item.Equals(package.Item))
                        if (0 < StoredPackages[positions[i]].SpaceLeft)
                            package = AddAtPosition(positions[i], package);
            }

            void AddToEmptyPositions()
            {
                var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

                for (var x = 0; x < Dimensions.x && 0 < package.Amount; x++)
                    for (var y = 0; y < Dimensions.y && 0 < package.Amount; y++)
                        if (IsEmptyPosition(new(x, y), dimensions, out _))
                            package = AddAtPosition(new(x, y), package);
            }
        }

        // TODO: make this abstract?
        public virtual Package AddAtPosition(Vector2Int position, Package package)
        {
            if (package.Item == null)
                return package;

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

            if (IsEmptyPosition(position, dimensions, out var otherItems))
                TryAddToInventory();
            else if (1 == otherItems.Count)
            {
                if (StoredPackages.TryGetValue(otherItems[0], out var storedPackage))
                {
                    if (!TryStack(storedPackage))
                        TrySwap(storedPackage);
                }

                return package;
            }
            else
                return package;

            OnContentChanged?.Invoke(StoredPackages);

            return package;

            void TryAddToInventory()
            {
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                {
                    _ = package.ReduceAmount(amount);
                }

                if (this is CharacterEquipment && package.Item is EquipmentItem)
                {
                    ItemProvider.Instance.LocalPlayer.AddItemStats(package.Item.Affixes);
                }

                OnContentChanged?.Invoke(StoredPackages);
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

        public Package RemoveFromContainer(Package package)
        {
            FindAllEqualItems(package.Item, out var positions);

            for (var i = positions.Count - 1; 0 <= i && 0 < package.Amount; i--)
                package = RemoveAtPosition(positions[i], package);

            return package;

            void FindAllEqualItems(AbstractItem item, out List<Vector2Int> positions)
            {
                positions = new List<Vector2Int>();

                foreach (var package in StoredPackages)
                    if (package.Value.Item == item)
                        positions.Add(package.Key);

                _ = positions.OrderBy(v => v.x);
            }
        }

        public Package RemoveAtPosition(Vector2Int position, Package package)
        {
            var storedPositions = GetOtherItemsAt(position, new(1, 1)); // will this ever return more than one position?

            if (storedPositions.Count == 1)
            {
                if (StoredPackages.TryGetValue(storedPositions[0], out var storedPackage))
                {
                    if (this is CharacterEquipment)
                        ItemProvider.Instance.LocalPlayer.RemoveItemStats(storedPackage.Item.Affixes);

                    var removed = storedPackage.ReduceAmount(package.Amount);
                    _ = package.ReduceAmount(removed);

                    if (0 < storedPackage.Amount)
                        StoredPackages[storedPositions[0]] = storedPackage;
                    else
                        _ = StoredPackages.Remove(storedPositions[0]);
                }
            }

            OnContentChanged?.Invoke(StoredPackages);

            return package;
        }

        public bool IsEmptyPosition(Vector2Int position, Vector2Int dimension, out List<Vector2Int> otherItems)
        {
            otherItems = null;

            if (IsValidPosition(position, dimension))
            {
                otherItems = GetOtherItemsAt(position, dimension);
                return otherItems.Count <= 0;
            }

            return false;

            bool IsValidPosition(Vector2Int position, Vector2Int dimension)
            {
                foreach (var requiredPosition in CalculateRequiredPositions(position, dimension))
                    if (!IsWithinDimensions(requiredPosition))
                        return false;

                return true;

                bool IsWithinDimensions(Vector2Int position) =>
                   -1 < position.x && position.x < Dimensions.x &&
                   -1 < position.y && position.y < Dimensions.y;
            }
        }

        /// A List of all storedPackages positions that overlap with the requiredPositions
        public abstract List<Vector2Int> GetOtherItemsAt(Vector2Int position, Vector2Int dimension);

        public bool TryGetPackageAt(Vector2Int position, out Package package) => StoredPackages.TryGetValue(position, out package);

        /// A List of all positions that are required to add this item to the container
        // TODO make this abstract! CONTINUE HERE
        protected abstract List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension);

        // TODO package should implement IComparable 
        public void Sort() // TODO garbage free sorting
        {
            var storedValues = StoredPackages.Values.ToList();

            SortAlphabetically(storedValues);
            SortByRarity(storedValues);
            SortByItemDimension(storedValues);
        }

        private void SortAlphabetically(List<Package> storedValues)
        {
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            var sortedValues = storedValues.OrderByDescending(x => x.Item.ToString());

            foreach (var package in sortedValues)
                _ = AddToContainer(package);

            /*var storedNames = storedValues.Select(x => x.Item.ToString()).Distinct().OrderByDescending(x => x).ToList();
            
            foreach (var x in storedNames)
                foreach (var y in storedValues)
                    if (y.ToString() == x)
                        _ = AddToContainer(y);
            */
        }

        private void SortByRarity(List<Package> storedValues)
        {
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            var sortedValues = storedValues.OrderByDescending(x => x.Item.Rarity).ToList();

            foreach (var package in sortedValues)
                _ = AddToContainer(package);

            /*var storedRarities = storedValues.Select(x => x.Item.Rarity).Distinct().OrderByDescending(x => x).ToList();

            foreach (var x in storedRarities)
                foreach (var y in storedValues)
                    if (y.Item.Rarity == x)
                        _ = AddToContainer(y);
            */
        }

        private void SortByItemDimension(List<Package> storedValues)
        {
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            var sortedValues = storedValues.OrderByDescending(x => AbstractItem.GetDimensions(x.Item.Dimensions).sqrMagnitude).ToList();

            foreach (var package in sortedValues)
                _ = AddToContainer(package);

            /*var storedDimensions = storedValues.Select(x => AbstractItem.GetDimensions(x.Item.Dimensions)).Distinct().OrderByDescending(v => v.sqrMagnitude).ToList();

            foreach (var x in storedDimensions)
                foreach (var y in storedValues)
                    if (AbstractItem.GetDimensions(y.Item.Dimensions) == x)
                _ = AddToContainer(package);
            */
        }

        protected internal void InvokeRefresh() => OnContentChanged?.Invoke(StoredPackages);
    }
}
