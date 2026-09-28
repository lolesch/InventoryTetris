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
            base.OnDisable();

            var wallet = InventoryProvider.Instance.Wallet;

            if (wallet != null)
                wallet.OnBalanceChanged -= OnWalletChanged;
        }

        /// Cached before the base runs, because refreshing the display repaints the
        /// background and that has to price the incoming item, not the outgoing one.
        public override void RefreshSlotDisplay(Package package)
        {
            displayedPackage = package;

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

            return wallet == null || wallet.CanAfford(new Currency(VendorTransaction.BuyPrice(displayedPackage.Item) * displayedPackage.Amount));
        }

        protected override void SetDisplaySize(RectTransform display, Package package)
        {
            base.SetDisplaySize(display, package);

            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();
            if (gridLayout)
            {
                var itemDimensions = ItemView.Of(package.Item).Dimensions;
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

            // No deliberate right-click action on the shelf: buying is a left-click drag +
            // drop, exactly like every other container. Immediate buy-on-right-click used to
            // shortcut to that same outcome, but a shortcut to an already-reachable outcome
            // isn't deliberate different behavior, so right-click is a no-op here instead.
            // TryBeginMove runs first so the hover preview still fades on a right-click no-op.
            if (eventData.button == PointerEventData.InputButton.Right)
                return;

            var unitPrice = VendorTransaction.BuyPrice(package.Item);

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
