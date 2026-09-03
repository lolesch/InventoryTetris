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
    /// <c>Store</c> is still a <see cref="CharacterInventory"/> and the wallet is still the
    /// player <see cref="CharacterInventory"/>; a dedicated vendor container and a
    /// <c>Wallet</c> module are later work (the foundational-rework spec, Phase 3).</para>
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
        /// wallet.
        /// </summary>
        public static void Sell(Package soldItem, CharacterInventory wallet)
        {
            if (wallet == null || !soldItem.IsValid)
                return;

            var proceeds = new Currency(ItemView.Of(soldItem.Item).SellValue * soldItem.Amount);

            if (0u == proceeds.Total)
                return;

            using var transaction = new ItemTransaction(wallet);

            transaction.QueueEffect(() => wallet.Deposit(proceeds));

            transaction.Commit();
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
            CharacterInventory wallet, float price)
        {
            if (store == null || wallet == null || !onShelf.IsValid || !wallet.CanAfford(price))
                return false;

            using var transaction = new ItemTransaction(store, wallet);

            _ = store.RemoveAtPosition(position, onShelf);

            var incoming = new Package(wallet, onShelf.Item, onShelf.Amount);
            if (!wallet.TryAddToContainer(ref incoming))
                return false; // dispose rolls the removal back - item back on the shelf, no charge

            transaction.QueueEffect(() => _ = wallet.TryPay(price));

            transaction.Commit();
            return true;
        }
    }
}
