using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Sold container (issue #126): what the player sold, held as a Supply so it can be
    /// bought back. The same grid as the Vendor's shelf plus the one thing a shelf does not
    /// have, an age - when a sale would not fit, the oldest sold Packages are discarded until
    /// it does. A discarded Package belongs to the Town Stop; the player was paid for it.
    ///
    /// <para>The only ledger is the sale order: a list of cells, oldest first. A sale that
    /// merges into a stack moves that stack's cell to the newest position. The list is
    /// reconciled against the grid whenever it is read, because the container is also a shelf
    /// - a buy-back or a drag empties a cell without telling the ledger - so a cell with no
    /// Package is skipped, and a Package the ledger never heard of (one returned to its cell
    /// after an abandoned buy-back) counts as the oldest. Both are cheaper than a second
    /// source of truth about what is in the grid.</para>
    ///
    /// <para>The container itself is just a <see cref="CharacterInventory"/>; the rest is
    /// <see cref="Sale"/>, which drives these members inside its transaction.</para>
    /// </summary>
    public sealed class SoldContainer : CharacterInventory
    {
        /// <summary>The cells of the Packages sold, oldest first.</summary>
        private readonly List<Vector2Int> saleOrder = new();

        /// <summary>The age order <see cref="TryPlaceEvicting"/> is working on: the ledger as it
        /// stands after the compaction and discards of that attempt, adopted by
        /// <see cref="NoteSold"/> once the transaction commits. Null outside an attempt.</summary>
        private List<Vector2Int> workingOrder;

        public SoldContainer(Vector2Int dimensions, IItemCatalog catalog) : base(dimensions, catalog) { }

        /// <summary>
        /// Places all of <paramref name="package"/>, discarding the oldest sold Packages until
        /// it fits. The Sold container stays old-to-young: before anything is placed the
        /// existing Packages are packed together oldest first, so a hole a buy-back left does not
        /// take the new sale - it lands after the others, and a discard packs the rest again.
        /// All or nothing for the caller's transaction: false means even an empty
        /// container could not take it, and the caller rolls back, which restores whatever was
        /// discarded or moved on the way. Reads and writes <see cref="AbstractDimensionalContainer.StoredPackages"/>
        /// - the working copy, mid-transaction.
        /// </summary>
        /// <param name="landed">The cells the sale changed, to be made the newest by
        /// <see cref="NoteSold"/> once the transaction commits.</param>
        internal bool TryPlaceEvicting(Package package, out List<Vector2Int> landed)
        {
            workingOrder = AgeOrder();

            while (true)
            {
                Compact();

                if (TryPlace(package, out landed))
                    return true;

                if (!TryDiscardOldest())
                {
                    workingOrder = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Makes <paramref name="cells"/> the newest sales, in order, and forgets every cell
        /// that no longer holds a Package. Runs as a commit-time effect, so a rolled-back
        /// sale leaves the order alone.
        /// </summary>
        internal void NoteSold(IEnumerable<Vector2Int> cells)
        {
            if (workingOrder != null)
            {
                saleOrder.Clear();
                saleOrder.AddRange(workingOrder);
                workingOrder = null;
            }

            _ = saleOrder.RemoveAll(cell => !StoredPackages.ContainsKey(cell));

            foreach (var cell in cells)
            {
                _ = saleOrder.Remove(cell);
                saleOrder.Add(cell);
            }
        }

        /// <summary>
        /// One attempt to place the whole of <paramref name="package"/> without discarding
        /// anything, restoring the grid when it does not all fit. Not
        /// <see cref="AbstractDimensionalContainer.TryAddToContainer"/>: that warns "is full!"
        /// on every miss, and a miss is the ordinary first step of an eviction here.
        /// </summary>
        private bool TryPlace(Package package, out List<Vector2Int> landed)
        {
            var before = new Dictionary<Vector2Int, Package>(StoredPackages);
            var remaining = package;

            _ = TryStack(ref remaining);

            while (0 < remaining.Amount && TryFindEmptyCell(ViewOf(remaining.Item).Dimensions, out var cell))
            {
                var left = remaining.Amount;
                remaining = AddAtPosition(cell, remaining);

                if (remaining.Amount == left)
                    break; // the cell took nothing: bail out rather than spin
            }

            if (0 < remaining.Amount)
            {
                StoredPackages.Clear();

                foreach (var entry in before)
                    StoredPackages[entry.Key] = entry.Value;

                landed = null;
                return false;
            }

            landed = StoredPackages
                .Where(entry => !before.TryGetValue(entry.Key, out var was) || was.Amount != entry.Value.Amount)
                .Select(entry => entry.Key)
                .ToList();

            return true;
        }

        /// <summary>Discards the oldest sold Package. False when the container is empty.</summary>
        private bool TryDiscardOldest()
        {
            var oldest = workingOrder.Where(StoredPackages.ContainsKey).Select(c => (Vector2Int?)c).FirstOrDefault();

            if (oldest is not { } cell)
                return false;

            _ = RemoveAtPosition(cell, StoredPackages[cell]);
            _ = workingOrder.Remove(cell);
            return true;
        }

        /// <summary>The cells now holding a Package, oldest first: what the ledger does not know
        /// is older than any recorded sale (in grid order), then the recorded sales.</summary>
        private List<Vector2Int> AgeOrder() => StoredPackages.Keys
            .Where(cell => !saleOrder.Contains(cell))
            .OrderBy(cell => cell.x).ThenBy(cell => cell.y)
            .Concat(saleOrder.Where(StoredPackages.ContainsKey))
            .ToList();

        /// <summary>
        /// Packs every Package against the start of the grid in age order, oldest first. A
        /// rearrangement that somehow does not fit - mixed footprints can pack worse than they
        /// were laid out - restores the grid as it was and carries on uncompacted.
        /// </summary>
        private void Compact()
        {
            var before = new Dictionary<Vector2Int, Package>(StoredPackages);
            var packed = new List<Vector2Int>();

            StoredPackages.Clear();

            foreach (var cell in workingOrder.Where(before.ContainsKey))
            {
                var package = before[cell];

                if (!TryFindEmptyCell(ViewOf(package.Item).Dimensions, out var target)
                    || 0 < AddAtPosition(target, package).Amount)
                {
                    StoredPackages.Clear();

                    foreach (var entry in before)
                        StoredPackages[entry.Key] = entry.Value;

                    return;
                }

                packed.Add(target);
            }

            workingOrder = packed;
        }
    }
}
