using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// "Send this Package back where it came from" (issue #29), as a commit-or-rollback move
    /// on top of <see cref="ItemTransaction"/>. A cancelled or interrupted drag hands its
    /// Package here and it goes, in order, to:
    /// <list type="number">
    /// <item>its exact origin cell, if <see cref="AbstractDimensionalContainer.CanReturnTo"/>
    /// says that cell is still free;</item>
    /// <item>anywhere in the player's <c>backpack</c>;</item>
    /// <item>nowhere - the Package comes back to the caller unchanged, so it stays on the
    /// cursor.</item>
    /// </list>
    /// It never drops or destroys the Package and conserves item count by construction; the
    /// only character-sheet change it can make is re-applying a worn item's affixes when step
    /// 1 re-keys it on the paper-doll - the exact mirror of the lift a drag pick-up already
    /// ran. The wallet is never touched.
    ///
    /// <para>This is the deferred <c>ReturnToOrigin</c> the item-movement-model spec left for
    /// later and the trade-flow spec's return-to-sender primitive: panel-close and
    /// sale-cancel (issues #31, #32) are meant to call this same entry.</para>
    /// </summary>
    public static class ReturnToOrigin
    {
        /// <param name="package">The Package currently on the cursor.</param>
        /// <param name="origin">The container the drag started from, or null when it is no
        /// longer known.</param>
        /// <param name="originPosition">The cell in <paramref name="origin"/> the Package was
        /// lifted from.</param>
        /// <param name="backpack">The player inventory - the fallback destination. May be the
        /// same instance as <paramref name="origin"/>.</param>
        /// <returns><c>default</c> once the Package found a home and the caller should end the
        /// drag. The Package itself, unchanged, when neither <paramref name="origin"/> nor
        /// <paramref name="backpack"/> had room - the caller leaves it on the cursor.</returns>
        public static Package Return(Package package, AbstractDimensionalContainer origin,
            Vector2Int originPosition, AbstractDimensionalContainer backpack)
        {
            if (!package.IsValid)
                return package;

            using var transaction = new ItemTransaction(origin, backpack);

            if (origin != null && origin.CanReturnTo(originPosition, package.Item))
            {
                var remainder = origin.AddAtPosition(originPosition, package);

                // CanReturnTo already guaranteed a free, in-bounds, correctly-shaped cell, so
                // this always places the whole Package; the check is belt-and-braces - a
                // partial placement here rolls back rather than risk double-counting the rest
                // into the backpack below.
                if (remainder.IsValid)
                    return package;

                transaction.Commit();
                return default;
            }

            var toBackpack = package;

            if (backpack != null && backpack.TryAddToContainer(ref toBackpack))
            {
                transaction.Commit();
                return default;
            }

            return package; // dispose rolls back - nothing moved, the Package stays on the cursor
        }
    }
}
