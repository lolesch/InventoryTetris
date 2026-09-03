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

            return wallet == null || wallet.CanAfford(new Currency(VendorTransaction.BuyPrice(displayedPackage.Item)));
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
            if (Container == null)
                return;

            var position = Position;

            if (!Container.TryGetItemAt(ref position, out var package))
                return;

            var wallet = InventoryProvider.Instance.Wallet;
            var price = VendorTransaction.BuyPrice(package.Item);

            if (!wallet.CanAfford(new Currency(price)))
                return;

            FadeOutPreview();

            // Right-click and shift-click both move the item straight to the inventory.
            #region BUY: IMMEDIATE MOVE
            if (eventData.button == PointerEventData.InputButton.Right || Input.GetKey(KeyCode.LeftShift))
            {
                /// One transaction (issue #11): the item leaves the shelf and lands in the
                /// bag, and the price is paid, as a unit. No room in the bag rolls the whole
                /// thing back - the item stays on the shelf and nothing is charged.
                _ = VendorTransaction.Buy(Container, position, package, wallet, price);

                return;
            }
            #endregion BUY: IMMEDIATE MOVE

            // Drag: a pick-up, not a completed move - the drag system still has no clean
            // cancel/refund path, so payment stays at pick-up. That gap is pre-existing and
            // deferred (follow-ups spec).
            #region BUY: DRAG
            _ = Container.RemoveAtPosition(position, package);

            _ = wallet.TryPay(new Currency(price));

            var positionOffset = Position - position;

            DragProvider.Instance.SetPackage(this, package, positionOffset, pointerPosition);
            #endregion BUY: DRAG
        }

        /// <summary>
        /// Dropping onto the shelf is a sale, not a placement (issue #12): it takes any
        /// item that is not already the vendor's, so the drop tint must not read the
        /// shelf's own grid the way the base does. Mirrors <see cref="DropItem"/>'s guard.
        /// </summary>
        public override bool WouldAcceptDrop(Package package) =>
            package.IsValid && package.Sender != Container;

        protected override void DropItem(Package package)
        {
            if (!package.IsValid || package.Sender == Container)
                return;

            /// Dropping an item onto the shelf is a sale, exactly as the dedicated sell slot
            /// (SellItenSlotDisplay) handles it: the item is already in hand from the drag,
            /// its value is banked into the wallet on commit (issue #11), and the drag ends.
            VendorTransaction.Sell(package, InventoryProvider.Instance.Wallet);

            DragProvider.Instance.EndDrag();

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

            SyncPreviewAfterMove();
        }
    }
}
