using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    internal sealed class VendorSlotDisplay : AbstractSlotDisplay
    {
        private GridLayoutGroup gridLayout;

        /// The same "forbidden" feedback the drag display gives an item that cannot be
        /// placed (see DragProvider.HighlightOverlappingSlots): red is assigned rather
        /// than multiplied in, so rarity cannot shift it, and it stays see-through so the
        /// slot underneath still reads.
        private static readonly Color UnaffordableBackground = new(1f, 0f, 0f, 0.2f);

        /// The displayed package's whole price in base units, or <c>null</c> for an empty slot.
        /// Taken once when the package changes, so a wallet change can re-tint without waiting
        /// for a container refresh and a repaint (hover, a wallet change, the un-highlight in
        /// OnDisable) never prices the item again through the catalog.
        private float? displayedPrice;

        protected override void OnEnable()
        {
            base.OnEnable();

            var wallet = CurrentWallet();

            if (wallet != null)
            {
                wallet.OnBalanceChanged -= OnWalletChanged;
                wallet.OnBalanceChanged += OnWalletChanged;
            }
        }

        protected override void OnDisable()
        {
            // Before the base runs: it repaints, and a repaint that throws must not leave this
            // slot subscribed to a Wallet that outlives it.
            var wallet = CurrentWallet();

            if (wallet != null)
                wallet.OnBalanceChanged -= OnWalletChanged;

            base.OnDisable();
        }

        /// Cached before the base runs, because refreshing the display repaints the
        /// background and that has to price the incoming item, not the outgoing one.
        public override void RefreshSlotDisplay(Package package)
        {
            displayedPrice = package.IsValid
                ? VendorTransaction.BuyPrice(package.Item, ItemService.Instance.Catalog) * package.Amount
                : null;

            base.RefreshSlotDisplay(package);
        }

        private void OnWalletChanged(Currency _) => RefreshBackground();

        protected override Color GetBackgroundColor() => CanAffordDisplayed()
            ? base.GetBackgroundColor()
            : Lighten(UnaffordableBackground);

        /// An empty slot has nothing to price, so it never reads as unaffordable - which
        /// also clears the tint for free on the slot an item was just bought out of.
        private bool CanAffordDisplayed()
        {
            if (displayedPrice is not { } price)
                return true;

            var wallet = CurrentWallet();

            return wallet == null || VendorTransaction.CanAffordBuy(wallet, price);
        }

        // Unity's lifetime-aware ==: a destroyed provider is not literally null, and a repaint can
        // run on the way out of Play Mode.
        private static Wallet CurrentWallet()
        {
            var provider = InventoryProvider.Instance;

            return provider != null ? provider.Wallet : null;
        }

        protected override void SetDisplaySize(RectTransform display, Package package)
        {
            base.SetDisplaySize(display, package);

            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();
            if (gridLayout)
            {
                var itemDimensions = ItemService.Instance.View(package.Item).Dimensions;
                var additionalSpacing = gridLayout.spacing * new Vector2(itemDimensions.x - 1, itemDimensions.y - 1);

                display.sizeDelta = gridLayout.cellSize * itemDimensions + additionalSpacing;
            }

            display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
            display.pivot = new Vector2(.5f, .5f);
            display.anchorMin = new Vector2(0, 1);
            display.anchorMax = new Vector2(0, 1);
        }

        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition)
        {
            if (!TryBeginMove(out var position, out var package))
                return;

            // Right-click is a Supply's "use": it buys the item (issue #121), the same
            // immediate buy as shift-click. Drag-and-drop below stays the other way to buy.
            // This class serves every Supply shelf - the Vendor's and the Healer's.
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                BuyAt(position, package);
                return;
            }

            // An item the player cannot afford stays on the shelf: priced for the amount this
            // pick-up would take, so Ctrl-half of a red stack still lifts when the half is
            // affordable (issue #125). The drop keeps its own gate as the transaction-level guarantee.
            if (!VendorTransaction.CanAffordPickUp(CurrentWallet(), package, HalvesOnPickUp, ItemService.Instance.Catalog))
                return;

            var unitPrice = VendorTransaction.BuyPrice(package.Item, ItemService.Instance.Catalog);

            // Drag: a pick-up, not a completed move - nothing is charged, and the price is
            // read once and held on the cursor for the length of the drag (issue #31),
            // scaled to the amount actually picked up (BeginDrag). It is paid only when the
            // package lands in a player container; dropping it back on the shelf, cancelling
            // (Esc) or closing the Store returns it with no charge.
            BeginDrag(position, package, pointerPosition, unitPrice);
        }

        /// <summary>
        /// A Supply slot is a drop target that sells (issue #129), and the Sold tab's slots are the
        /// same slots. It accepts a purchase in progress - from any shelf - as a free return to
        /// its origin, never a sale: nobody is paid for something not yet owned. It accepts a
        /// player-owned Package as a sale, and refuses it, which shows the forbidden tint, when
        /// the payout would not fit or would be 0 (<see cref="Sale.CanSellHeld"/>).
        /// </summary>
        public override bool WouldAcceptDrop(Package package)
        {
            if (!package.IsValid)
                return false;

            if (DragProvider.Instance.IsHoldingPurchase)
                return true;

            var provider = InventoryProvider.Instance;

            return Sale.CanSellHeld(provider.Sold, provider.Wallet, package);
        }

        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            var provider = InventoryProvider.Instance;

            /// A purchase in progress, dropped on any Supply or the Sold tab, is a return to
            /// origin, not a sale (issues #31, #129): put it straight back on the cell it came
            /// from, charge nothing, pay nothing.
            if (DragProvider.Instance.IsHoldingPurchase)
            {
                _ = DragProvider.Instance.CancelDrag();

                Container?.InvokeRefresh();
                DragProvider.Instance.Origin?.Container?.InvokeRefresh();

                SyncPreviewAfterMove();
                return;
            }

            /// A player-owned Package is a sale: the one Sale statement, over the Sold container
            /// and the Wallet - the source was vacated at pick-up. A sale that cannot pay out
            /// leaves the item in hand exactly as the player is holding it.
            if (!Sale.TrySellHeld(provider.Sold, provider.Wallet, package))
                return;

            DragProvider.Instance.EndDrag();

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin?.Container?.InvokeRefresh();

            SyncPreviewAfterMove();
        }
    }
}
