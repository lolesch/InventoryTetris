using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Run's ground (epic #214): a stash-sized grid of the Packages lying there. It is the
    /// <see cref="OldestOutInventory"/> that never rearranges: a Package stays in the cell it landed
    /// in, holes stay holes, and when a new Package does not fit the oldest ones are removed until it
    /// does. Both the grid and the list view read this one container.
    /// </summary>
    public sealed class GroundContainer : OldestOutInventory
    {
        public GroundContainer(Vector2Int dimensions, IItemCatalog catalog) : base(dimensions, catalog) { }

        /// <summary>
        /// Lands all of <paramref name="package"/>, evicting the oldest Packages until it fits. A
        /// Package that merges into a stack makes that stack the newest. False, with the ground exactly
        /// as it was, for an empty Package or one larger than the whole grid.
        /// </summary>
        /// <param name="onEvicted">Called with each Package pushed out, once the landing is committed -
        /// never for a landing that was refused. The ground deletes them; this is the caller's chance to
        /// save something first.</param>
        public bool TryLand(Package package, Action<Package> onEvicted = null)
        {
            if (!package.IsValid)
                return false;

            using var transaction = new ItemTransaction(this);

            if (!TryPlaceEvicting(package, out var landed, out var order, out var evicted))
                return false;

            transaction.QueueEffect(() => NoteLanded(order, landed));

            if (onEvicted != null)
                transaction.QueueEffect(() => evicted.ForEach(onEvicted));
            transaction.Commit();
            return true;
        }

        /// <summary>The Packages lying here, oldest first - the order the list view reads.</summary>
        public List<Package> PackagesOldestFirst() => AgeOrder().Select(cell => StoredPackages[cell]).ToList();
    }
}
