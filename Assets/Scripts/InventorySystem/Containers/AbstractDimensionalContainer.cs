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

        [field: SerializeField] public readonly Vector2Int Dimensions;
        public int Capacity => Dimensions.x * Dimensions.y;

        /// <summary>
        /// Containers that fold small coins into larger denominations the moment they
        /// arrive, rather than leaving that to the player. False everywhere today; a
        /// future stash subclasses <see cref="CharacterInventory"/> and overrides this
        /// to true, which is the whole "everything becomes gold in the bank" behaviour.
        /// </summary>
        public virtual bool AutoConsolidate => false;

        public event Action<Dictionary<Vector2Int, Package>> OnContentChanged;

        [field: SerializeField] public Dictionary<Vector2Int, Package> StoredPackages { get; protected set; } = new();

        // ── Transaction seam (issue #9) ─────────────────────────────────────
        // While enrolled in an ItemTransaction, StoredPackages is a working copy: the
        // placement code below mutates that copy, OnContentChanged is deferred, and any
        // stat / cursor / currency side effect is queued onto the transaction. Commit
        // writes the working copy back into this same dictionary instance; a rollback
        // restores it. Nothing here changes when ActiveTransaction is null.

        /// <summary>The transaction this container is currently enrolled in, or null.</summary>
        internal ItemTransaction ActiveTransaction { get; private set; }

        /// <summary>The real StoredPackages dictionary, parked while a transaction holds a working copy in its place.</summary>
        private Dictionary<Vector2Int, Package> liveStoredPackages;

        /// <summary>Enrol: snapshot StoredPackages into a working copy the move mutates in isolation.</summary>
        internal void JoinTransaction(ItemTransaction transaction)
        {
            if (ActiveTransaction != null)
                throw new InvalidOperationException($"{GetType().Name} is already enrolled in a transaction.");

            ActiveTransaction = transaction;
            liveStoredPackages = StoredPackages;
            StoredPackages = new Dictionary<Vector2Int, Package>(liveStoredPackages);
        }

        /// <summary>
        /// Commit: re-point <see cref="StoredPackages"/> at the original dictionary
        /// instance, folding the working copy's entries back into it when the move
        /// actually changed this container (<paramref name="contentsChanged"/>). An
        /// enrolled-but-untouched container just detaches - its working copy is a faithful
        /// copy of state nothing wrote to.
        /// </summary>
        internal void ApplyTransaction(bool contentsChanged)
        {
            if (contentsChanged)
            {
                var working = StoredPackages;

                liveStoredPackages.Clear();
                foreach (var entry in working)
                    liveStoredPackages[entry.Key] = entry.Value;
            }

            StoredPackages = liveStoredPackages;
            liveStoredPackages = null;
            ActiveTransaction = null;
        }

        /// <summary>Rollback: discard the working copy, restore the pre-transaction state.</summary>
        internal void DiscardTransaction()
        {
            StoredPackages = liveStoredPackages;
            liveStoredPackages = null;
            ActiveTransaction = null;
        }

        /// <summary>Fires <see cref="OnContentChanged"/> now, bypassing the transaction defer. Used by a commit.</summary>
        internal void RaiseContentChangedNow() => OnContentChanged?.Invoke(StoredPackages);

        /// <summary>
        /// The one place a mutation announces it changed the container. Inline when there
        /// is no transaction; recorded on the transaction (once per container, replayed at
        /// commit) when there is one.
        /// </summary>
        private void RaiseContentChanged()
        {
            if (ActiveTransaction != null)
                ActiveTransaction.NoteContentChanged(this);
            else
                OnContentChanged?.Invoke(StoredPackages);
        }

        /// <summary>
        /// Runs <paramref name="effect"/> now, or - mid-transaction - queues it to run on
        /// commit and be dropped on rollback. Every observable side effect of a mutation
        /// beyond the container's own contents (a worn item's stat apply/remove, the drag
        /// handover) goes through here so a rolled-back move leaves nothing behind.
        /// </summary>
        private protected void RunOrQueue(Action effect)
        {
            if (ActiveTransaction != null)
                ActiveTransaction.QueueEffect(effect);
            else
                effect();
        }

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

            RaiseContentChanged();

            return 0 == package.Amount;
        }

        // TODO: DragDrop adding to stacks is dimension dependent...
        // => this should simply check if a stack of the same item is at the drop position and add it.
        protected bool TryStack(ref Package package)
        {
            if (!package.IsValid)
                return false;

            var stackLimit = ItemView.Of(package.Item).StackLimit;
            if (stackLimit <= 1u)
                return false;

            var positions = StoredPackages.Keys.ToList();

            for (var i = 0; i < positions.Count && 0 < package.Amount; i++)
                if (StoredPackages[positions[i]].Item.StacksWith(package.Item, stackLimit))
                    if (0 < StoredPackages[positions[i]].SpaceLeft)
                        package = AddAtPosition(positions[i], package);

            return 0 == package.Amount;
        }

        protected virtual bool TryAddAtEmpty(ref Package package)
        {
            if (!package.IsValid)
                return false;

            var dimensions = ItemView.Of(package.Item).Dimensions;

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

            for (var i = positions.Count; 0 < package.Amount && i --> 0;)
            //for (var i = positions.Count - 1; 0 <= i && 0 < package.Amount; i--)
                package = RemoveAtPosition(positions[i], package);

            return package;

            void FindAllEqualItems(ItemInstance item, out List<Vector2Int> positions)
            {
                positions = new List<Vector2Int>();

                foreach (var package in StoredPackages)
                    if (ReferenceEquals(package.Value.Item, item))
                        positions.Add(package.Key);

                _ = positions.OrderBy(v => v.x);
            }
        }

        public Package RemoveAtPosition(Vector2Int position, Package package)
        {
            if (TryGetItemAt(ref position, out var storedPackage))
            {
                OnPackageRemoved(storedPackage);

                var removed = storedPackage.ReduceAmount(package.Amount);
                _ = package.ReduceAmount(removed);

                if (0 < storedPackage.Amount)
                    StoredPackages[position] = storedPackage;
                else
                    _ = StoredPackages.Remove(position);
            }

            RaiseContentChanged();

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
            }
        }

        /// Whether <paramref name="position"/> lies inside the container grid. Shared by
        /// IsEmptySpace and CanPlaceAt so both agree on where the edges are.
        private bool IsWithinDimensions(Vector2Int position) =>
            -1 < position.x && position.x < Dimensions.x &&
            -1 < position.y && position.y < Dimensions.y;

        /// <summary>
        /// Whether a drop at this position would land at all: inside the container, and
        /// overlapping at most one stored item. 0 overlaps drops into empty space, 1 swaps
        /// (AddAtPosition handles both); 2+ places nothing, and the caller must keep dragging.
        /// <see cref="CharacterEquipment"/> overrides this to accept the legal two-hander
        /// double-swap its <see cref="AddAtPosition"/> allows (issue #12), so the red
        /// "can't drop" tint and the drop rule stay one and the same check.
        /// </summary>
        public virtual bool CanPlaceAt(Vector2Int position, Vector2Int dimension)
        {
            foreach (var requiredPosition in CalculateRequiredPositions(position, dimension))
                if (!IsWithinDimensions(requiredPosition))
                    return false;

            return GetStoredItemsAt(position, dimension).Count <= 1;
        }

        public bool TryGetPackageAt(Vector2Int position, out Package package) => StoredPackages.TryGetValue(position, out package);

        // TODO package should implement IComparable
        public void Sort()
        {
            var sortedValues = StoredPackages.Values
                .Select(package => (package, view: ItemView.Of(package.Item)))
                .OrderByDescending(x => x.view.Footprint)                                     // by size
                .ThenBy(x => x.view.Definition.Category == ItemCategory.Currency)             // by itemType (equipment before consumables before currency)
                .ThenBy(x => x.view.Definition.Category == ItemCategory.Consumable)
                .ThenBy(x => x.view.Definition.Category == ItemCategory.Equipment)
                .ThenByDescending(x => x.package.Item.Rarity)                                 // by rarity
                .ThenByDescending(x => x.view.SellValue)                                      // by goldValue
                .ThenBy(x => x.view.DisplayName)                                              // by name
                .Select(x => x.package)
                .ToList();

            // Already inside a move: sort on that transaction's working copy - it owns the
            // commit / rollback. Otherwise wrap the remove-all + re-add so a layout that
            // will not re-fit rolls back rather than dropping the overflow (issue #10).
            if (ActiveTransaction != null)
            {
                _ = SortInto(sortedValues);
                return;
            }

            using var transaction = new ItemTransaction(this);

            if (SortInto(sortedValues))
                transaction.Commit();

            bool SortInto(List<Package> values)
            {
                foreach (var package in values)
                    _ = RemoveFromContainer(package);

                var allPlaced = true;

                foreach (var package in values)
                {
                    var packageRef = package;
                    if (!TryAddToContainer(ref packageRef))
                        allPlaced = false;
                }

                return allPlaced;
            }
        }

        /// <summary>
        /// Fires <see cref="OnContentChanged"/> so the bound displays repaint - or, mid
        /// transaction, records the refresh to replay once on commit. Public because the
        /// slot displays in Assembly-CSharp drive a refresh after a move they performed
        /// themselves; it was <c>protected internal</c> when they shared an assembly with
        /// the container core.
        /// </summary>
        public void InvokeRefresh() => RaiseContentChanged();

        /// <summary>
        /// Hook fired for the stored package a <see cref="RemoveAtPosition"/> is about to
        /// take. Empty here; <see cref="CharacterEquipment"/> overrides it to lift the
        /// removed item's affixes off the character it is worn by. Replaces a
        /// <c>this is CharacterEquipment</c> downcast that reached a provider singleton.
        /// </summary>
        protected virtual void OnPackageRemoved(Package package) { }
    }
}
