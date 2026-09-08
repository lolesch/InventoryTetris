using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Sell Basket (issue #32) - the fourth role played by the existing
    /// <see cref="CharacterInventory"/> type alongside Inventory / Stash / Store: the grid
    /// the player stages a sale in. Grid packing and nothing else - no wallet machinery, no
    /// new container class. The entries the trade-flow spec asks for, all covered at the one
    /// container seam:
    /// <list type="number">
    /// <item><see cref="Stage"/> - a Package enters the basket from an origin cell, and the
    /// origin is remembered so a Cancel can return it there;</item>
    /// <item><see cref="PreviewValue"/> - the running total the vendor will pay, a pure sum
    /// of <see cref="ItemView.SellValue"/> over the basket's Packages, full amount per
    /// stack;</item>
    /// <item><see cref="Confirm"/> - runs one <see cref="ItemTransaction"/> over the basket
    /// and the wallet, banking the summed value as a single consolidated payout, then clears
    /// the basket. Value-conserving by construction;</item>
    /// <item><see cref="Cancel"/> - returns every Package through the return-to-origin
    /// primitive (<see cref="ReturnToOrigin"/>), wallet untouched.</item>
    /// </list>
    ///
    /// <para>Because the basket is a grid, several staged Packages can sit in different
    /// cells; the <see cref="Basket.Origins"/> ledger keys each one's origin by the cell it
    /// landed in, so a Cancel can hand each back to <see cref="ReturnToOrigin"/> with the
    /// right home. The display (a grid like the backpack's, the total label, Confirm / Cancel,
    /// and the modal buy-block) is a separate, smoke-tested layer ("GUI shells are
    /// smoke-tested, not unit-tested" - the trade-flow spec, §Testing Decisions).</para>
    /// </summary>
    public sealed class SellBasket
    {
        /// <summary>The state a staged sale runs against - the container plus its ledger.</summary>
        public sealed class Basket
        {
            /// <summary>The container the player stages Packages into - a role of
            /// <see cref="CharacterInventory"/>, nothing more. Named like <see cref="Wallet.Container"/>.</summary>
            public CharacterInventory Container { get; }

            /// <summary>
            /// Where each staged Package came from, keyed by the basket cell it landed in - the
            /// ledger a Cancel (or a Store close) re-homes every Package through
            /// <see cref="ReturnToOrigin"/>. The basket is a grid, so the key is the landing cell.
            /// </summary>
            public Dictionary<Vector2Int, Origin> Origins { get; } = new();

            public Basket(CharacterInventory container) => Container = container;
        }

        /// <summary>A Package's home before it was staged: the container it was lifted from and
        /// the exact cell it occupied, re-keyed through <see cref="ReturnToOrigin"/> on cancel.</summary>
        public readonly struct Origin
        {
            public AbstractDimensionalContainer Container { get; }
            public Vector2Int Cell { get; }

            public Origin(AbstractDimensionalContainer container, Vector2Int cell)
            {
                Container = container;
                Cell = cell;
            }
        }

        /// <summary>
        /// Stages the Package the drag holds into <paramref name="basket"/> at
        /// <paramref name="at"/>, remembering its <paramref name="origin"/>. The caller has
        /// already removed the Package from the origin onto the drag (a pick-up); this lands
        /// it in the basket and records the home a Cancel needs. Returns whether it fully
        /// staged - false when the basket would not take it (full / footprint), in which case
        /// the drag keeps the Package.
        /// </summary>
        public static bool Stage(Basket basket, ref Package inHand, Origin origin, Vector2Int at)
        {
            if (basket == null || !inHand.IsValid)
                return false;

            using var transaction = new ItemTransaction(basket.Container);

            var placed = basket.Container.AddAtPosition(at, inHand);

            if (placed.IsValid)
                return false; // didn't fit - dispose rolls back, the drag keeps the Package

            transaction.Commit();
            basket.Origins[at] = origin;
            return true;
        }

        /// <summary>
        /// The total the vendor would pay for everything in <paramref name="basket"/>, in
        /// base units - a pure sum of <see cref="ItemView.SellValue"/> × amount, full amount
        /// per stack. The total label subscribes to the basket's OnContentChanged and re-reads
        /// this whenever a Package is staged or removed.
        /// </summary>
        public static float PreviewValue(Basket basket)
        {
            if (basket == null)
                return 0f;

            var total = 0f;

            foreach (var package in basket.Container.StoredPackages.Values)
                total += ItemView.Of(package.Item).SellValue * package.Amount;

            return total;
        }

        /// <summary>
        /// Runs one <see cref="ItemTransaction"/> over the basket and the wallet: banks the
        /// previewed value as a single consolidated payout, then clears the basket. The coins
        /// land exactly equal to the preview (value-conserving by construction) - this is how
        /// a staged sale commits. The wallet is only ever touched on Commit, so a Move that
        /// cannot complete rolls back with nothing paid and nothing lost.
        /// </summary>
        public static bool Confirm(Basket basket, Wallet wallet)
        {
            if (basket == null || wallet == null || basket.Container.StoredPackages.Count == 0)
                return false;

            var proceeds = new Currency(PreviewValue(basket));

            if (0u == proceeds.Total)
                return false;

            using var transaction = new ItemTransaction(basket.Container, wallet.Container);

            foreach (var cell in basket.Container.StoredPackages.Keys.ToList())
                _ = basket.Container.RemoveAtPosition(cell, basket.Container.StoredPackages[cell]);

            transaction.QueueEffect(() => wallet.Deposit(proceeds));

            transaction.Commit();
            basket.Origins.Clear();
            return true;
        }

        /// <summary>
        /// Returns every staged Package to where it came from, in order, through the
        /// return-to-origin primitive - re-equipping what was unequipped, back to the backpack
        /// what came from the backpack - without touching the wallet. A Package that fits
        /// neither its origin nor the backpack is handed back, unchanged, on the cursor (the
        /// same fallback a cancelled drag uses, per story 37) rather than destroyed; the
        /// already-returned ones stay returned. A Store close is wired to call the same entry.
        /// </summary>
        /// <returns>An invalid Package when every staged Package found a home; otherwise the
        /// first that could not, still on the cursor - never lost.</returns>
        public static Package Cancel(Basket basket, AbstractDimensionalContainer backpack)
        {
            if (basket == null)
                return default;

            foreach (var (cell, origin) in basket.Origins.ToList())
            {
                if (!basket.Container.StoredPackages.TryGetValue(cell, out var removed) || !removed.IsValid)
                    continue;

                /// The Package is in the basket and will be re-homed; take it out so the
                /// return it lands back on is unambiguous, then let ReturnToOrigin decide
                /// origin-cell-first / backpack-fallback / cursor. Captured via
                /// TryGetPackageAt (a full-amount copy) BEFORE the removal - the same pattern
                /// the slot displays use - because RemoveAtPosition reduces the `package`
                /// argument it is handed, returning a zeroed copy.
                _ = basket.Container.RemoveAtPosition(cell, removed);

                var leftover = ReturnToOrigin.Return(removed, origin.Container, origin.Cell, backpack);

                if (leftover.IsValid)
                    return leftover; // nothing destroyed; the caller decides what to do with it

                _ = basket.Origins.Remove(cell);
            }

            return default;
        }
    }
}