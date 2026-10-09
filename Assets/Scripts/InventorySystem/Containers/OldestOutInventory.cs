using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// A grid whose Packages have an age: when a new Package would not fit, the oldest are
    /// discarded until it does. The age order and the eviction are shared by the Sold container
    /// and the floor; they differ only in <see cref="Arrange"/>, whether the survivors are packed
    /// together before a placement is tried.
    ///
    /// <para>The only ledger is the landing order: a list of cells, oldest first. A Package that
    /// merges into a stack moves that stack's cell to the newest position. The list is reconciled
    /// against the grid whenever it is read, because a cell can be emptied without telling the
    /// ledger (a buy-back, a pick-up), so a cell with no Package is skipped, and a Package the
    /// ledger never heard of (one returned to its cell after an abandoned buy-back) counts as the
    /// oldest. Both are cheaper than a second source of truth about what is in the grid.</para>
    /// </summary>
    public abstract class OldestOutInventory : CharacterInventory
    {
        /// <summary>The cells of the Packages landed, oldest first.</summary>
        private readonly List<Vector2Int> landingOrder = new();

        protected OldestOutInventory(Vector2Int dimensions, IItemCatalog catalog) : base(dimensions, catalog) { }

        /// <summary>
        /// Lets a subclass rearrange the Packages before each placement attempt. Returns the age
        /// order at the Packages' cells afterwards; the default leaves every Package where it is.
        /// </summary>
        protected virtual List<Vector2Int> Arrange(List<Vector2Int> order) => order;

        /// <summary>
        /// Places all of <paramref name="package"/>, discarding the oldest Packages until it fits.
        /// All or nothing for the caller's transaction: false means even an empty container could
        /// not take it, and the caller rolls back, which restores whatever was discarded or moved
        /// on the way. Reads and writes <see cref="AbstractDimensionalContainer.StoredPackages"/>
        /// - the working copy, mid-transaction.
        /// </summary>
        /// <param name="landed">The cells the placement changed, to be made the newest by
        /// <see cref="NoteLanded"/> once the transaction commits.</param>
        /// <param name="order">The age order after the arrangement and discards of this attempt,
        /// oldest first, for <see cref="NoteLanded"/> to adopt at the same moment. It travels with
        /// the attempt rather than living on the container, so an attempt that never commits
        /// leaves nothing behind.</param>
        /// <param name="evicted">The Packages discarded to make room, oldest first, whole as they
        /// were, for a caller that must do something with them once the transaction commits.</param>
        internal bool TryPlaceEvicting(Package package, out List<Vector2Int> landed, out List<Vector2Int> order,
            out List<Package> evicted)
        {
            order = AgeOrder();
            evicted = new List<Package>();

            while (true)
            {
                order = Arrange(order);

                if (TryPlace(package, out landed))
                    return true;

                if (!TryDiscardOldest(order, evicted))
                    return false;
            }
        }

        /// <summary>
        /// Adopts <paramref name="order"/> - the age order <see cref="TryPlaceEvicting"/> ended
        /// with - makes <paramref name="cells"/> the newest, in order, and forgets every cell that
        /// no longer holds a Package. Runs as a commit-time effect, so a rolled-back placement
        /// leaves the order alone.
        /// </summary>
        internal void NoteLanded(IEnumerable<Vector2Int> order, IEnumerable<Vector2Int> cells)
        {
            landingOrder.Clear();
            landingOrder.AddRange(order);

            _ = landingOrder.RemoveAll(cell => !StoredPackages.ContainsKey(cell));

            foreach (var cell in cells)
            {
                _ = landingOrder.Remove(cell);
                landingOrder.Add(cell);
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

        /// <summary>Discards the oldest Package, noting it in <paramref name="evicted"/>. False when
        /// the container is empty.</summary>
        private bool TryDiscardOldest(List<Vector2Int> order, List<Package> evicted)
        {
            var oldest = order.Where(StoredPackages.ContainsKey).Select(c => (Vector2Int?)c).FirstOrDefault();

            if (oldest is not { } cell)
                return false;

            var package = StoredPackages[cell];
            evicted.Add(package);
            _ = RemoveAtPosition(cell, package);
            _ = order.Remove(cell);
            return true;
        }

        /// <summary>The cells now holding a Package, oldest first: what the ledger does not know
        /// is older than any recorded landing (in grid order), then the recorded landings.</summary>
        private protected List<Vector2Int> AgeOrder()
        {
            var known = new HashSet<Vector2Int>(landingOrder);

            return StoredPackages.Keys
                .Where(cell => !known.Contains(cell))
                .OrderBy(cell => cell.x).ThenBy(cell => cell.y)
                .Concat(landingOrder.Where(StoredPackages.ContainsKey))
                .ToList();
        }
    }
}
