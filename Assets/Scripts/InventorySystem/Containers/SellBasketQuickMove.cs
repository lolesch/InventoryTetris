using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The shift-click-into-the-Sell-Basket move (issue #33): the pure container-seam
    /// sibling of <see cref="SellBasket.Stage"/>, but for a quick-move where the Package is
    /// still sitting in a source cell rather than held on the cursor. It removes the Package
    /// from that cell, auto-places it into the basket at the first free cell, records its
    /// origin in the basket's ledger (so a later shift-click or Cancel can return it), and
    /// runs the whole move in one <see cref="ItemTransaction"/> over the source and the
    /// basket.
    ///
    /// <para>The two sources are the backpack and the paper-doll. Removing from
    /// <see cref="CharacterEquipment"/> routes the affix lift through its
    /// <c>OnPackageRemoved</c> hook, so an unequip-into-the-basket lifts the worn affixes as a
    /// commit-time effect - exactly the guarantee the current unequip gives. A basket with no
    /// room for the item rolls the move back and leaves the Package in the source, matching
    /// <see cref="SellBasket.Stage"/>'s "keep it where it is" on a full basket.</para>
    ///
    /// <para>The slot displays call this when the quick-move resolver returns a
    /// <see cref="QuickMoveIntentKind.SellBasket"/> intent for a held
    /// <see cref="AbstractDimensionalContainer"/>.</para>
    /// </summary>
    public static class SellBasketQuickMove
    {
        /// <param name="basket">The Sell Basket to stage into - its container and origin
        /// ledger.</param>
        /// <param name="source">The container the Package is currently in - the backpack or
        /// the paper-doll. Its <paramref name="sourceCell"/> is vacated on success.</param>
        /// <param name="sourceCell">The cell <paramref name="source"/> holds the Package
        /// at - also recorded as the origin a Cancel returns it to.</param>
        /// <returns>True once the Package is staged in the basket; false when the basket
        /// would not take it (full / footprint), in which case the source keeps the
        /// Package and no origin is recorded.</returns>
        public static bool SendToBasket(SellBasket.Basket basket, AbstractDimensionalContainer source,
            Vector2Int sourceCell)
        {
            if (basket == null || source == null
                || !source.TryGetPackageAt(sourceCell, out var stored) || !stored.IsValid)
                return false;

            var destination = basket.Container;
            var dimensions = ItemView.Of(stored.Item).Dimensions;

            if (!TryFindFreeCell(destination, dimensions, out var at))
                return false;

            using var transaction = new ItemTransaction(source, destination);

            _ = source.RemoveAtPosition(sourceCell, stored);
            var inHand = stored;

            var placed = destination.AddAtPosition(at, inHand);

            if (placed.IsValid)
                return false; // didn't fit - dispose rolls back and the item stays in the source

            transaction.Commit();
            basket.Origins[at] = new PackageOrigin(source, sourceCell);
            return true;
        }

        /// <summary>The first cell the item's footprint fits without displacing anything -
        /// the same scan the container's own <c>TryAddAtEmpty</c> does, but reported so the
        /// origin ledger can be keyed by the landing cell.</summary>
        private static bool TryFindFreeCell(AbstractDimensionalContainer container, Vector2Int dimensions,
            out Vector2Int at)
        {
            for (var x = 0; x < container.Dimensions.x; x++)
                for (var y = 0; y < container.Dimensions.y; y++)
                    if (container.IsEmptySpace(new(x, y), dimensions, out _))
                    {
                        at = new Vector2Int(x, y);
                        return true;
                    }

            at = default;
            return false;
        }
    }
}
