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
    /// it does (<see cref="OldestOutInventory"/>). A discarded Package belongs to the Town Stop;
    /// the player was paid for it.
    ///
    /// <para>What is its own is the compaction: the Sold container stays old-to-young, so before
    /// anything is placed the existing Packages are packed together oldest first, and a hole a
    /// buy-back left does not take the new sale - it lands after the others. The rest is
    /// <see cref="Sale"/>, which drives these members inside its transaction.</para>
    /// </summary>
    public sealed class SoldContainer : OldestOutInventory
    {
        public SoldContainer(Vector2Int dimensions, IItemCatalog catalog) : base(dimensions, catalog) { }

        /// <summary>
        /// Packs every Package against the start of the grid in age order, oldest first, and
        /// returns the order at its new cells. A rearrangement that somehow does not fit - mixed
        /// footprints can pack worse than they were laid out - restores the grid as it was and
        /// returns <paramref name="order"/> unchanged: carries on uncompacted.
        /// </summary>
        protected override List<Vector2Int> Arrange(List<Vector2Int> order)
        {
            var before = new Dictionary<Vector2Int, Package>(StoredPackages);
            var packed = new List<Vector2Int>();

            StoredPackages.Clear();

            foreach (var cell in order.Where(before.ContainsKey))
            {
                var package = before[cell];

                if (!TryFindEmptyCell(ViewOf(package.Item).Dimensions, out var target)
                    || 0 < AddAtPosition(target, package).Amount)
                {
                    StoredPackages.Clear();

                    foreach (var entry in before)
                        StoredPackages[entry.Key] = entry.Value;

                    return order;
                }

                packed.Add(target);
            }

            return packed;
        }
    }
}
