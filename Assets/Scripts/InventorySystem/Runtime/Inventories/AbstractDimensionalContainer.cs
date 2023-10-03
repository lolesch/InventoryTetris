using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
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

        [field: SerializeField] public Dictionary<Vector2Int, Package> StoredPackages { get; protected set; } = new();

        public abstract Package AddToContainer(Package package);
        public abstract Package AddToEmptyPosition(Package package);

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
            var storedPositions = GetStoredItemsAt(position, new(1, 1)); // will this ever return more than one position?

            if (storedPositions.Count == 1)
            {
                if (StoredPackages.TryGetValue(storedPositions[0], out var storedPackage))
                {
                    if (this is CharacterEquipment)
                        CharacterProvider.Instance.Player.RemoveItemStats(storedPackage.Item.Affixes);

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
        public void Sort() // TODO: implement garbage free sorting
        {
            var sortedValues = StoredPackages.Values
                .OrderByDescending(x => x.Item.Dimensions)  // by size     //AbstractItem.GetDimensions(x.Item.Dimensions).sqrMagnitude)
                .ThenBy(x => x.Item is CurrencyItem)        // by equipment infront of consumables infront of currency 
                .ThenBy(x => x.Item is ConsumableItem)
                .ThenBy(x => x.Item is EquipmentItem)
                .ThenByDescending(x => x.Item.Rarity)       // by rarity
                .ThenByDescending(x => x.Item.GoldValue)    // by goldValue
                .ThenBy(x => x.Item.ToString())             // by name
                .ToList();

            StoredPackages.Clear(); // This won't removed stats from character // but AddToContainer will add stats to character
            // Might want to remove each package instead of Clear()

            foreach (var package in sortedValues)
                _ = AddToContainer(package);
        }

        protected internal void InvokeRefresh() => OnContentChanged?.Invoke(StoredPackages);
    }
}
