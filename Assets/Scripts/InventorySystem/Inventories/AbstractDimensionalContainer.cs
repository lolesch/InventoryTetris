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

        public Package AddToContainer(Package package)
        {
            if (package.Item == null)
                return package;

            if (this is PlayerInventory)
            {
                var equipment = InventoryProvider.Instance.PlayerEquipment;

                if (package.Item is EquipmentItem && equipment.autoEquip)
                {
                    var equipmentPosition = equipment.GetEquipmentTypePosition((package.Item as EquipmentItem).EquipmentType);

                    if (equipment.IsEmptyPosition(equipmentPosition, new(1, 1), out _))
                        return equipment.AddToContainer(package);
                }
            }

            if (ItemStack.Single < package.Item.StackLimit)
                AddToOpenStacks();

            AddToEmptyPositions();

            return package;

            void AddToOpenStacks()
            {
                var positions = StoredPackages.Keys.ToList();

                for (var i = 0; i < positions.Count && 0 < package.Amount; i++)
                    if (StoredPackages[positions[i]].Item == package.Item && 0 < StoredPackages[positions[i]].SpaceLeft)
                        package = AddAtPosition(positions[i], package);
            }

            void AddToEmptyPositions()
            {
                for (var x = 0; x < Dimensions.x && 0 < package.Amount; x++)
                    for (var y = 0; y < Dimensions.y && 0 < package.Amount; y++)
                        if (IsEmptyPosition(new(x, y), AbstractItem.GetDimensions(package.Item.Dimensions), out _)) // this might need to be abstract and with a dimension of (1,1) for equipment
                            package = AddAtPosition(new(x, y), package);
            }
        }

        public Package AddAtPosition(Vector2Int position, Package package)
        {
            if (package.Item == null)
                return package;

            if (this is CharacterEquipment)
            {
                if (package.Item is EquipmentItem)
                    position = (this as CharacterEquipment).GetEquipmentTypePosition((package.Item as EquipmentItem).EquipmentType);
                else
                    return package;
            }

            var dimensions = this is CharacterEquipment ? new(1, 1) : AbstractItem.GetDimensions(package.Item.Dimensions);

            if (CanAddAtPosition(position, dimensions, out var otherItems))
            {
                if (0 == otherItems.Count)
                    TryAddToInventory();

                if (1 == otherItems.Count)
                    TryStackOrSwap(otherItems[0]);

                OnContentChanged?.Invoke(StoredPackages);
            }

            return package;

            void TryAddToInventory()
            {
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(package.Item, amount)))
                {
                    _ = package.ReduceAmount(amount);
                }

                if (this is CharacterEquipment && package.Item is EquipmentItem)
                {
                    Character.Instance.AddItemStats(package.Item.Affixes);
                }

                OnContentChanged?.Invoke(StoredPackages);
            }

            void TryStackOrSwap(Vector2Int position)
            {
                if (StoredPackages.TryGetValue(position, out var storedPackage))
                {
                    if (1 < (uint)package.Item.StackLimit && package.Item == storedPackage.Item && 0 < storedPackage.SpaceLeft)
                    {
                        var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                        StoredPackages[position] = storedPackage;
                        _ = package.ReduceAmount(addedAmount);
                    }
                    else /// swap items
                    {
                        _ = RemoveAtPosition(position, storedPackage);

                        TryAddToInventory();

                        // TODO: check for item loss, else revert
                        package = storedPackage;
                    }
                }
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
            var storedPositions = GetOtherItemsAt(position, new(1, 1));

            if (storedPositions.Count == 1)
            {
                if (StoredPackages.TryGetValue(storedPositions[0], out var storedPackage))
                {
                    var removed = storedPackage.ReduceAmount(package.Amount);
                    _ = package.ReduceAmount(removed);

                    if (0 < storedPackage.Amount)
                    {
                        StoredPackages[storedPositions[0]] = storedPackage;
                    }
                    else
                    {
                        _ = StoredPackages.Remove(storedPositions[0]);

                        if (this is CharacterEquipment)// && storedPackage.Item is Equipment) // must have been an Equipment if it was in the Equipment container
                        {
                            Character.Instance.RemoveItemStats(storedPackage.Item.Affixes);
                        }
                    }
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

        public bool CanAddAtPosition(Vector2Int position, Vector2Int dimension, out List<Vector2Int> otherItems) => IsEmptyPosition(position, dimension, out otherItems) || (!IsEmptyPosition(position, dimension, out otherItems) && otherItems.Count <= 1);

        /// A List of all storedPackages positions that overlap with the requiredPositions
        public abstract List<Vector2Int> GetOtherItemsAt(Vector2Int position, Vector2Int dimension);

        public bool TryGetPackageAt(Vector2Int position, out Package package) => StoredPackages.TryGetValue(position, out package);

        /// A List of all positions that are required to add this item to the container
        protected abstract List<Vector2Int> CalculateRequiredPositions(Vector2Int position, Vector2Int dimension);

        public void SortByItemDimension()
        {
            SortAlphabetically();

            var storedKeys = StoredPackages.Keys.ToList();
            List<Vector2Int> storedDimensions = new();

            for (var i = 0; i < storedKeys.Count; i++)
                storedDimensions.Add(AbstractItem.GetDimensions(StoredPackages[storedKeys[i]].Item.Dimensions));

            storedDimensions = storedDimensions.Distinct().OrderByDescending(v => v.x * v.y/*v.sqrMagnitude*/).ToList();

            var storedValues = StoredPackages.Values.ToList();
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            for (var i = 0; i < storedDimensions.Count; i++)
                for (var j = 0; j < storedValues.Count; j++)
                    if (AbstractItem.GetDimensions(storedValues[j].Item.Dimensions) == storedDimensions[i])
                        _ = AddToContainer(storedValues[j]);
        }

        private void SortAlphabetically()
        {
            var storedNames = StoredPackages.Values.Select(x => x.Item.ToString()).ToList();

            storedNames = storedNames.Distinct().OrderBy(x => x).ToList();

            var storedValues = StoredPackages.Values.ToList();
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            for (var i = 0; i < storedNames.Count; i++)
                for (var j = 0; j < storedValues.Count; j++)
                    if (storedValues[j].Item.ToString() == storedNames[i])
                        _ = AddToContainer(storedValues[j]);
        }

        // TODO: sort by rarity

        protected internal void InvokeRefresh() => OnContentChanged?.Invoke(StoredPackages);
    }
}
