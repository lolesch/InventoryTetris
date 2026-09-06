using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The vendor rows of the movement matrix (issue #11), as commit-or-rollback moves on
    /// top of <see cref="ItemTransaction"/>. A sale banks the item's sell value into the
    /// wallet; a purchase places the bought item on the transaction's working copies and
    /// queues the exact payment. The currency effect runs only on
    /// <see cref="ItemTransaction.Commit"/>, so a purchase that finds no inventory room
    /// rolls back leaving the item on the shelf and nothing charged - the player can never
    /// be charged with no item, or paid with the item still on the shelf.
    ///
    /// <para><c>VendorSlotDisplay</c> and <c>SellItenSlotDisplay</c> both route through here
    /// rather than each carrying a near-identical remove / add / coin-mint block. The
    /// <c>Store</c> is still a <see cref="CharacterInventory"/>; the <see cref="Wallet"/>
    /// module owns the money (issue #14), and a dedicated vendor container is later work
    /// (the foundational-rework spec).</para>
    /// </summary>
    public static class VendorTransaction
    {
        /// <summary>The vendor sells for <see cref="Markup"/>× what it buys back at.</summary>
        public const float Markup = 1.5f;

        /// <summary>What the vendor charges for <paramref name="item"/>, in base units.</summary>
        public static float BuyPrice(ItemInstance item) => ItemView.Of(item).SellValue * Markup;

        /// <summary>
        /// Banks the proceeds of a sale. <paramref name="soldItem"/> has already left every
        /// container - a drag sink consumed it - so this only mints its sell value into
        /// <paramref name="wallet"/>, as a commit-time effect on a transaction scoped to the
        /// wallet's backing container.
        /// </summary>
        public static void Sell(Package soldItem, Wallet wallet)
        {
            if (wallet == null || !soldItem.IsValid)
                return;

            var proceeds = new Currency(ItemView.Of(soldItem.Item).SellValue * soldItem.Amount);

            if (0u == proceeds.Total)
                return;

            using var transaction = new ItemTransaction(wallet.Container);

            transaction.QueueEffect(() => wallet.Deposit(proceeds));

            transaction.Commit();
        }

        /// <summary>
        /// Whether <paramref name="wallet"/> can pay <paramref name="price"/> base units.
        /// The shared gate for the atomic buy and the dragging buy's drop - a purchase is
        /// only ever made when the price is payable, whatever the input that brought the
        /// item this far (issue #31).
        /// </summary>
        public static bool CanAffordBuy(Wallet wallet, float price) =>
            wallet != null && wallet.CanAfford(new Currency(price));

        /// <summary>
        /// Queues the payment for a shelf purchase on <paramref name="transaction"/> - to
        /// run exactly on commit and be dropped on rollback. Buying by drag reads the price
        /// once at pick-up and pays that exact amount here, so the charge lands only when
        /// the item is actually placed and a restock mid-drag cannot change the deal (issue
        /// #31). Shared with the atomic <see cref="Buy"/> so the two buy paths queue the
        /// same effect.
        /// </summary>
        /// <param name="transaction">The transaction placing the item. Null-safely ignored.</param>
        /// <param name="wallet">The buyer's wallet; the charge runs against it on commit.</param>
        /// <param name="price">The price shown at pick-up, in base units.</param>
        public static void QueuePurchasePayment(ItemTransaction transaction, Wallet wallet, float price)
        {
            if (transaction == null || wallet == null)
                return;

            transaction.QueueEffect(() => _ = wallet.TryPay(new Currency(price)));
        }

        /// <summary>
        /// Buys the package at <paramref name="position"/> out of <paramref name="store"/>
        /// into <paramref name="wallet"/> for <paramref name="price"/> base units. On commit
        /// the item is placed and the price paid exactly; if the wallet cannot afford it or
        /// has no room the whole move rolls back - the item stays on the shelf, nothing is
        /// charged.
        /// </summary>
        /// <returns>Whether the purchase went through.</returns>
        public static bool Buy(AbstractDimensionalContainer store, Vector2Int position, Package onShelf,
            Wallet wallet, float price)
        {
            if (store == null || wallet == null || !onShelf.IsValid || !CanAffordBuy(wallet, price))
                return false;

            var bag = wallet.Container;

            using var transaction = new ItemTransaction(store, bag);

            _ = store.RemoveAtPosition(position, onShelf);

            var incoming = new Package(bag, onShelf.Item, onShelf.Amount);
            if (!bag.TryAddToContainer(ref incoming))
                return false; // dispose rolls the removal back - item back on the shelf, no charge

            QueuePurchasePayment(transaction, wallet, price);

            transaction.Commit();
            return true;
        }
    }
}
