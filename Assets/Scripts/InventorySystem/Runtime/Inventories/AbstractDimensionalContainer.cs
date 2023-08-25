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

        public virtual Package AddToContainer(Package package)
        {
            if (package.Item == null)
                return package;

            /// handle autoEquip
            if (this == InventoryProvider.Instance.PlayerInventory)
            {
                var equipment = InventoryProvider.Instance.PlayerEquipment;

                if (package.Item is EquipmentItem && equipment.autoEquip)
                {
                    var equipmentPositions = equipment.GetTypeSpecificPositions((package.Item as EquipmentItem).EquipmentType);

                    foreach (var position in equipmentPositions)
                        if (equipment.IsEmptyPosition(position, new(1, 1), out _))
                            return equipment.AddAtPosition(position, package);
                }
            }

            if (ItemStack.Single < package.Item.StackLimit)
                AddToOpenStacks();

            AddToEmptyPositions();

            // DEVELOPMENT ONLY => remaining amount but inventory is full add to stash
            if (0 < package.Amount)
                if (this == InventoryProvider.Instance.PlayerInventory)
                    return InventoryProvider.Instance.PlayerStash.AddToContainer(package);

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
                var dimensions = this is CharacterEquipment ? new(1, 1) : AbstractItem.GetDimensions(package.Item.Dimensions);

                for (var x = 0; x < Dimensions.x && 0 < package.Amount; x++)
                    for (var y = 0; y < Dimensions.y && 0 < package.Amount; y++)
                        if (IsEmptyPosition(new(x, y), dimensions, out _))
                            package = AddAtPosition(new(x, y), package);
            }
        }

        public virtual Package AddAtPosition(Vector2Int position, Package package)
        {
            if (package.Item == null)
                return package;

            var dimensions = this is CharacterEquipment ? new(1, 1) : AbstractItem.GetDimensions(package.Item.Dimensions);

            if (this is CharacterEquipment)
            {
                if (package.Item is EquipmentItem)
                {
                    var positions = (this as CharacterEquipment).GetTypeSpecificPositions((package.Item as EquipmentItem).EquipmentType);

                    foreach (var pos in positions)
                        if (IsEmptyPosition(pos, dimensions, out var other))
                        {
                            position = pos;
                            break;
                        }
                }
                else
                    return package;
            }

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
                    ItemProvider.Instance.LocalPlayer.AddItemStats(package.Item.Affixes);
                }

                OnContentChanged?.Invoke(StoredPackages);
            }

            void TryStackOrSwap(Vector2Int position)
            {
                if (StoredPackages.TryGetValue(position, out var storedPackage))
                {
                    /// Try stacking
                    if (1 < (uint)package.Item.StackLimit && package.Item == storedPackage.Item && 0 < storedPackage.SpaceLeft)
                    {
                        var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                        StoredPackages[position] = storedPackage;
                        _ = package.ReduceAmount(addedAmount);
                    }
                    else /// swap items
                    {
                        // TODO unequip offhands when equiping a 2H
                        if (this is CharacterEquipment && package.Item is EquipmentItem && (package.Item as EquipmentItem).EquipmentType is > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS)
                        {
                            var positions = InventoryProvider.Instance.PlayerEquipment.GetOtherItemsAt(position, dimensions);
                            // TODO: CONTINUE HERE or implement overrides for CanAddAtPosition or AddAtPosition in the CharacterEquipment
                        }
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
                otherItems = GetOtherItemsAt(position, dimension); // Cant 
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

        // TODO override in PlayerEquipment to check all viable positions (rings, 1h and offhand, 2h...)
        public bool CanAddAtPosition(Vector2Int position, Vector2Int dimension, out List<Vector2Int> otherItems) =>
            IsEmptyPosition(position, dimension, out otherItems) ||
            (!IsEmptyPosition(position, dimension, out otherItems) && otherItems.Count <= 1);

        /// A List of all storedPackages positions that overlap with the requiredPositions
        public abstract List<Vector2Int> GetOtherItemsAt(Vector2Int position, Vector2Int dimension);

        public bool TryGetPackageAt(Vector2Int position, out Package package) => StoredPackages.TryGetValue(position, out package);

        /// A List of all positions that are required to add this item to the container
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

            var storedNames = storedValues.Select(x => x.Item.ToString()).Distinct().OrderByDescending(x => x).ToList();

            foreach (var x in storedNames)
                foreach (var y in storedValues)
                    if (y.ToString() == x)
                        _ = AddToContainer(y);
        }

        private void SortByItemDimension(List<Package> storedValues)
        {
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            var storedDimensions = storedValues.Select(x => AbstractItem.GetDimensions(x.Item.Dimensions)).Distinct().OrderByDescending(v => v.sqrMagnitude/* v.x * v.y*/).ToList();/*v.sqrMagnitude*/

            foreach (var x in storedDimensions)
                foreach (var y in storedValues)
                    if (AbstractItem.GetDimensions(y.Item.Dimensions) == x)
                        _ = AddToContainer(y);
        }

        private void SortByRarity(List<Package> storedValues)
        {
            StoredPackages.Clear(); // This won't unequip => stats not removed from character

            var storedRarities = storedValues.Select(x => x.Item.Rarity).Distinct().OrderByDescending(x => x).ToList();

            foreach (var x in storedRarities)
                foreach (var y in storedValues)
                    if (y.Item.Rarity == x)
                        _ = AddToContainer(y);
        }

        protected internal void InvokeRefresh() => OnContentChanged?.Invoke(StoredPackages);
    }
}
