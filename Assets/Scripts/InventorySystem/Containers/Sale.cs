using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Sale (issue #126): one transaction over the source, the <see cref="SoldContainer"/>
    /// and the <see cref="Wallet"/>. The Package leaves the Inventory or the Equipment, lands in
    /// the Sold container, and the Wallet is paid the sell value times the amount, once. Taking
    /// from the Equipment lifts the item's affixes as the transaction's commit-time effect,
    /// exactly as an unequip does. This is the one statement of a sale; shift-click and drop
    /// both call it.
    ///
    /// <para>A sale either wholly happens or leaves every container and the Wallet untouched:
    /// a 0 payout, a payout the Wallet cannot bank (checked with <see cref="Wallet.CanDeposit"/>
    /// before the deposit is queued, the same check-then-queue order a purchase follows), a
    /// Package too big for the Sold container even when empty, and currency, which is never for
    /// sale: it has a face value and the Wallet already holds it. A full Sold container never
    /// refuses a sale that can pay out; it discards its oldest instead
    /// (<see cref="OldestOutInventory.TryPlaceEvicting"/>).</para>
    ///
    /// <para>It replaced the staged Sell Basket, which is deleted (issue #131). The shift-click sink calls <see cref="TrySell"/> (issue #128);
    /// the drop sale, <see cref="TrySellHeld"/>, is called by a Supply slot's drop (issue #129),
    /// which asks <see cref="CanSellHeld"/> first for the forbidden tint.</para>
    /// </summary>
    public static class Sale
    {
        /// <summary>
        /// Sells the Package sitting at <paramref name="sourceCell"/> of <paramref name="source"/>
        /// - the shift-click sale. The whole stack goes. The payout may use the space the sale
        /// frees in <paramref name="wallet"/>'s own container.
        /// </summary>
        /// <returns>Whether the sale happened.</returns>
        public static bool TrySell(SoldContainer sold, Wallet wallet, AbstractDimensionalContainer source,
            Vector2Int sourceCell)
        {
            if (sold == null || wallet == null || source == null || source == sold
                || !source.TryGetPackageAt(sourceCell, out var stored) || !stored.IsValid)
                return false;

            using var transaction = new ItemTransaction(source, sold, wallet.Container);

            _ = source.RemoveAtPosition(sourceCell, stored); // reduces its own copy; `stored` stays whole

            return Complete(transaction, sold, wallet, stored);
        }

        /// <summary>
        /// Sells a Package already lifted off its source onto the cursor - the drop sale. The
        /// source was vacated at pick-up, so the sale has only the Sold container and the
        /// Wallet to move. <paramref name="carriedPrice"/> is what the cursor holds for a
        /// Package lifted off a shelf: a purchase in progress is never a sale, and the caller
        /// returns it to its origin free.
        /// </summary>
        /// <returns>Whether the sale happened.</returns>
        public static bool TrySellHeld(SoldContainer sold, Wallet wallet, Package held, float? carriedPrice = null)
        {
            if (carriedPrice != null || sold == null || wallet == null || !held.IsValid)
                return false;

            using var transaction = new ItemTransaction(sold, wallet.Container);

            return Complete(transaction, sold, wallet, held);
        }

        /// <summary>
        /// Whether <see cref="TrySellHeld"/> would take <paramref name="held"/> - the drop target's
        /// own answer, asked before release so it can show the forbidden tint, and by the drop
        /// itself. Reads, never mutates. False for a purchase in progress, a payout of 0 and a
        /// payout the Wallet cannot bank; the same refusals the sale makes.
        /// </summary>
        public static bool CanSellHeld(SoldContainer sold, Wallet wallet, Package held, float? carriedPrice = null)
        {
            if (carriedPrice != null || sold == null || wallet == null || !held.IsValid)
                return false;

            var view = sold.ViewOf(held.Item);

            if (IsCurrency(view))
                return false;

            var payout = PayoutOf(view, held);

            return 0u != payout.Total && wallet.CanDeposit(payout);
        }

        private static bool IsCurrency(ItemView view) => view.Definition.Category == ItemCategory.Currency;

        private static Currency PayoutOf(ItemView view, Package package) => new(view.SellValue * package.Amount);

        /// <summary>
        /// The tail both entries share, with <paramref name="package"/> already off its source
        /// on <paramref name="transaction"/>'s working copies. A false return leaves the
        /// transaction uncommitted, so disposing it restores everything.
        /// </summary>
        private static bool Complete(ItemTransaction transaction, SoldContainer sold, Wallet wallet, Package package)
        {
            var view = sold.ViewOf(package.Item);

            if (IsCurrency(view))
                return false;

            var payout = PayoutOf(view, package);

            if (0u == payout.Total || !wallet.CanDeposit(payout))
                return false;

            if (!sold.TryPlaceEvicting(package, out var landed, out var order))
                return false;

            transaction.QueueEffect(() => wallet.Deposit(payout));
            transaction.QueueEffect(() => sold.NoteLanded(order, landed));

            transaction.Commit();
            return true;
        }
    }
}
