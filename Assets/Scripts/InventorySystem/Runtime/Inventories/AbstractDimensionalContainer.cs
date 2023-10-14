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
        [SerializeField] public AbstractDimensionalContainer(Vector2Int dimensions) => Dimensions = dimensions;

        [field: SerializeField] public readonly Vector2Int Dimensions;
        [SerializeField] public int Capacity => Dimensions.x * Dimensions.y;

        public event Action<Dictionary<Vector2Int, Package>> OnContentChanged;

        [field: SerializeField] public Dictionary<Vector2Int, Package> StoredPackages { get; protected set; } = new();

        // recipient/receiver <-> sender/returningAddress
        /// <summary>
        /// Tries to add the package to the container and updating the package to the state after adding => new Package() 
        //or previous at that position?
        /// </summary>
        /// <param name="package"></param>
        /// <returns>Returns false if there is a remaining package</returns>
        public virtual bool TryAddToContainer(ref Package package)
        {
            if (!package.IsValid)
                return false;

            _ = TryStack(ref package);
            _ = TryAddAtEmpty(ref package);

            OnContentChanged?.Invoke(StoredPackages);

            return 0 == package.Amount;
        }

        // TODO: DragDrop adding to stacks is dimension dependent...
        // => this should simply check if a stack of the same item is at the drop position and add it.
        protected bool TryStack(ref Package package)
        {
            if (!package.IsValid || package.Item.StackLimit <= ItemStack.Single)
                return false;

            var positions = StoredPackages.Keys.ToList();

            for (var i = 0; i < positions.Count && 0 < package.Amount; i++)
                if (StoredPackages[positions[i]].Item.Equals(package.Item))
                    if (0 < StoredPackages[positions[i]].SpaceLeft)
                        package = AddAtPosition(positions[i], package);

            return 0 == package.Amount;
        }

        protected virtual bool TryAddAtEmpty(ref Package package)
        {
            if (!package.IsValid)
                return false;

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

            for (var x = 0; x < Dimensions.x && 0 < package.Amount; x++)
                for (var y = 0; y < Dimensions.y && 0 < package.Amount; y++)
                    if (IsEmptySpace(new(x, y), dimensions, out _))
                        package = AddAtPosition(new(x, y), package);

            if (0 < package.Amount)
                Debug.LogWarning($"{GetType().Name} is full!");

            return 0 == package.Amount;
        }

        public abstract Package AddAtPosition(Vector2Int position, Package package);

        /// A List of all positions that are required to add this item to the container
        protected List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> requiredPositions = new();

            for (var x = position.x; x < position.x + dimension.x; x++)
                for (var y = position.y; y < position.y + dimension.y; y++)
                    requiredPositions.Add(new(x, y));

            return requiredPositions;
        }

        /// A List of all storedPackages positions that overlap with the requiredPositions
        public abstract List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension);

        /// <summary>
        /// Checks for stored packages that occupy the given <paramref name="position"/> 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="storedPackage"></param>
        /// <returns>Returns <code true> if there is only one <paramref name="storedPackage"/></returns>
        public bool TryGetItemAt(ref Vector2Int position, out Package storedPackage)
        {
            var positions = GetStoredItemsAt(position, Vector2Int.one);

            if (positions.Any())
                position = positions.First();

            StoredPackages.TryGetValue(position, out storedPackage);

            return storedPackage.IsValid;
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
            if (TryGetItemAt(ref position, out var storedPackage))
            {
                if (this is CharacterEquipment)
                    CharacterProvider.Instance.Player.RemoveItemStats(storedPackage.Item.Affixes);

                var removed = storedPackage.ReduceAmount(package.Amount);
                _ = package.ReduceAmount(removed);

                if (0 < storedPackage.Amount)
                    StoredPackages[position] = storedPackage;
                else
                    _ = StoredPackages.Remove(position);
            }

            OnContentChanged?.Invoke(StoredPackages);

            return package;
        }

        public bool IsEmptySpace(Vector2Int position, Vector2Int dimension, out List<Vector2Int> otherItems)
        {
            otherItems = new();

            if (IsValidPosition(position, dimension))
            {
                otherItems = GetStoredItemsAt(position, dimension);
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

        public bool TryGetPackageAt(Vector2Int position, out Package package) => StoredPackages.TryGetValue(position, out package);

        // TODO package should implement IComparable 
        public void Sort()
        {
            var sortedValues = StoredPackages.Values
                .OrderByDescending(x => x.Item.Dimensions)  // by size     //AbstractItem.GetDimensions(x.Item.Dimensions).sqrMagnitude)
                .ThenBy(x => x.Item is CurrencyItem)        // by itemType (equipment before consumables before currency)
                .ThenBy(x => x.Item is ConsumableItem)
                .ThenBy(x => x.Item is EquipmentItem)
                .ThenByDescending(x => x.Item.Rarity)       // by rarity
                .ThenByDescending(x => x.Item.SellValue)    // by goldValue
                .ThenBy(x => x.Item.ToString())             // by name
                .ToList();

            foreach (var package in sortedValues)
                _ = RemoveFromContainer(package);

            foreach (var package in sortedValues)
            {
                var packageRef = package;
                _ = TryAddToContainer(ref packageRef);
            }
        }

        protected internal void InvokeRefresh() => OnContentChanged?.Invoke(StoredPackages);
    }
}
