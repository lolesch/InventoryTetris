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

        /// Cached so a wallet change can re-tint without waiting for a container refresh.
        private Package displayedPackage;

        /// The displayed package's whole price in base units, taken once when the package
        /// changes - while the services are armed. A repaint (hover, a wallet change, the
        /// un-highlight in OnDisable) then never reads the locator: Stop clears it before the
        /// scene's OnDisable calls run, so a read there throws once per stocked slot.
        private float displayedPrice;

        protected override void OnEnable()
        {
            base.OnEnable();

            var wallet = InventoryProvider.Instance.Wallet;

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
            var wallet = InventoryProvider.Instance.Wallet;

            if (wallet != null)
                wallet.OnBalanceChanged -= OnWalletChanged;

            base.OnDisable();
        }

        /// Cached before the base runs, because refreshing the display repaints the
        /// background and that has to price the incoming item, not the outgoing one.
        public override void RefreshSlotDisplay(Package package)
        {
            displayedPackage = package;
            displayedPrice = package.IsValid
                ? VendorTransaction.BuyPrice(package.Item, ItemService.Instance.Catalog) * package.Amount
                : 0f;

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
            if (!displayedPackage.IsValid)
                return true;

            var wallet = InventoryProvider.Instance.Wallet;

            return wallet == null || wallet.CanAfford(new Currency(displayedPrice));
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

            var unitPrice = VendorTransaction.BuyPrice(package.Item, ItemService.Instance.Catalog);

            // Drag: a pick-up, not a completed move - nothing is charged, and the price is
            // read once and held on the cursor for the length of the drag (issue #31),
            // scaled to the amount actually picked up (BeginDrag). It is paid only when the
            // package lands in a player container; dropping it back on the shelf, cancelling
            // (Esc) or closing the Store returns it with no charge.
            BeginDrag(position, package, pointerPosition, unitPrice);
        }

        /// <summary>
        /// The shelf only accepts its own item back (a free return to the cell it came from,
        /// issue #31). A player Package dropped here is no longer a sale - the Sell Basket
        /// (#32) is the only way to sell, so a non-shelf drop is turned away rather than
        /// banked.
        /// </summary>
        public override bool WouldAcceptDrop(Package package) => package.IsValid && package.Sender == Container;

        protected override void DropItem(Package package)
        {
            if (!package.IsValid || package.Sender != Container)
                return;

            /// The shelf's own item coming back is a return to origin, not a sale (issue #31):
            /// put it straight back on the cell it came from, charge nothing. Any other
            /// drop belongs to the Sell Basket (#32) - this shelf never sells.
            _ = DragProvider.Instance.CancelDrag();

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin?.Container?.InvokeRefresh();

            SyncPreviewAfterMove();
        }
    }
}
