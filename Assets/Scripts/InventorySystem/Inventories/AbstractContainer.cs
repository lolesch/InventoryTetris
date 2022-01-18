using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using System.Linq;
using TeppichsTools.Logging;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public abstract class AbstractContainer
    {
        public AbstractContainer(Vector2Int dimensions) => Dimensions = new(Math.Max(1, dimensions.x), Math.Max(1, dimensions.y));

        #region Fields
        public readonly Vector2Int Dimensions;
        public int Capacity => Dimensions.x * Dimensions.y;

        public event Action<Dictionary<Vector2Int, Package>> OnContentChanged;

        protected internal Dictionary<Vector2Int, Package> StoredPackages = new Dictionary<Vector2Int, Package>();

        protected internal Dictionary<Vector2Int, Vector2Int> OccupiedSlots = new Dictionary<Vector2Int, Vector2Int>();
        #endregion Fields

        public Package AddToContainer(Package package)
        {
            if (!IsValidItem(package.Item))
                return package;

            if (1 < package.Item.stackLimit)
            {
                FindAllOpenedStacks(package.Item, out List<Vector2Int> stacks);

                for (int i = 0; i < stacks.Count && 0 < package.Amount; i++)
                    package = AddAtPosition(stacks[i], package);

                if (0 == package.Amount)
                    return package;
            }

            int whileLoops = 0;

            while (FindEmptyPosition(package.Item.Dimensions, out Vector2Int position) && whileLoops < Capacity)
            {
                whileLoops++;

                package = AddAtPosition(position, package);

                if (0 == package.Amount)
                    return package;
            }

            return package;

            void FindAllOpenedStacks(AbstractItem stackableItem, out List<Vector2Int> stacks)
            {
                stacks = new List<Vector2Int>();

                foreach (var item in StoredPackages)
                    if (item.Value.Item == stackableItem && item.Value.Amount < item.Value.Item.stackLimit)
                        stacks.Add(item.Key);

                stacks.OrderBy(v => v.x);
            }
        }

        public Package AddAtPosition(Vector2Int position, Package package)
        {
            if (!IsValidItem(package.Item))
                return package;

            if (!IsWithinDimensions(position, package.Item.Dimensions))
                return package;

            package = ReturnContextSensitive();

            OnContentChanged?.Invoke(StoredPackages);

            return package;

            Package ReturnContextSensitive()
            {
                if (null == package.Item)
                    return package;

                List<Vector2Int> otherItems = FindItemsAtPosition(position, package.Item.Dimensions);

                if (0 == otherItems?.Count)
                    return TryAddToInventory();

                if (1 == otherItems?.Count)
                    return TryStackOrSwap(otherItems[0]);

                return package;

                Package TryAddToInventory()
                {
                    uint amount = Math.Min(package.Amount, package.Item.stackLimit);

                    if (StoredPackages.TryAdd(position, new Package(package.Item, amount)))
                    {
                        for (int x = position.x; x < position.x + package.Item.Dimensions.x; x++)
                            for (int y = position.y; y < position.y + package.Item.Dimensions.y; y++)
                                OccupiedSlots.Add(new(x, y), position);
                        package.ReduceAmount(amount);
                        EditorDebug.LogWarning($"Added package at StoredPackages[{position}]");
                    }

                    return package;
                }

                Package TryStackOrSwap(Vector2Int occupiedPosition)
                {
                    Package other = StoredPackages[occupiedPosition];

                    if (package.Item == other.Item && 1 < package.Item.stackLimit && other.Amount < other.Item.stackLimit)
                    {
                        uint addedAmount = other.IncreaseAmount(package.Amount);
                        StoredPackages[occupiedPosition] = other;
                        package.ReduceAmount(addedAmount);

                        return package;
                    }
                    else /// swap items
                    {
                        RemoveItemAtPosition(occupiedPosition, other);
                        if (0 < TryAddToInventory().Amount)
                        {
                            RemoveAtPosition(occupiedPosition);
                            AddAtPosition(occupiedPosition, other);
                            return package;
                        }
                        else
                            return other;
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

                foreach (var package in StoredPackages)
                    if (package.Value.Item == item)
                        positions.Add(package.Key);

                positions.OrderBy(v => v.x);
            }
        }

        public void RemoveAtPosition(Vector2Int position)
        {
            if (StoredPackages.TryGetValue(position, out Package itemToRemove))
                RemoveItemAtPosition(position, itemToRemove);
        }

        public Package RemoveItemAtPosition(Vector2Int position, Package package)
        {
            if (StoredPackages.TryGetValue(position, out Package storedPackage))
            {
                uint removed = storedPackage.ReduceAmount(package.Amount);
                package.ReduceAmount(removed);

                StoredPackages[position] = storedPackage;

                if (0 == storedPackage.Amount)
                    RemoveItem(position, storedPackage);
            }

            OnContentChanged?.Invoke(StoredPackages);

            return package;

            void RemoveItem(Vector2Int position, Package package)
            {
                StoredPackages.Remove(position);

                for (int x = position.x; x < position.x + package.Item.Dimensions.x; x++)
                    for (int y = position.y; y < position.y + package.Item.Dimensions.y; y++)
                        OccupiedSlots.Remove(new(x, y));
            }
        }

        protected internal bool IsValidItem(AbstractItem item) => null != item;
        protected internal bool IsWithinDimensions(Vector2Int position) => IsWithinDimensions(position, new(1, 1));
        protected internal bool IsWithinDimensions(Vector2Int position, Vector2Int itemDimensions) =>
          0 <= position.x && position.x + itemDimensions.x <= Dimensions.x &&
          0 <= position.y && position.y + itemDimensions.y <= Dimensions.y;

        protected internal bool FindEmptyPosition(Vector2Int dimensions, out Vector2Int position)
        {
            position = new(-1, -1);

            for (int x = 0; x < Dimensions.x; x++)
                for (int y = 0; y < Dimensions.y; y++)
                    if (0 == FindItemsAtPosition(new(x, y), dimensions)?.Count)
                    {
                        position = new(x, y);
                        return true;
                    }

            return false;
        }

        protected internal List<Vector2Int> FindItemsAtPosition(Vector2Int position, Vector2Int dimensions)
        {
            if (!IsWithinDimensions(position, dimensions))
                return null;
            else
            {
                List<Vector2Int> otherItems = new List<Vector2Int>();

                for (int x = position.x; x < position.x + dimensions.x; x++)
                    for (int y = position.y; y < position.y + dimensions.y; y++)
                        if (OccupiedSlots.TryGetValue(new(x, y), out Vector2Int itemPosition))
                            if (!otherItems.Contains(itemPosition))
                                otherItems.Add(itemPosition);

                return otherItems;
            }
        }

        internal void InvokeRefresh() => OnContentChanged?.Invoke(StoredPackages);
    }
}
