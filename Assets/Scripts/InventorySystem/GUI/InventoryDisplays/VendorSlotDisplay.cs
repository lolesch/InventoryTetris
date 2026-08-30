using System.Collections.Generic;
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

        internal const float Markup = 1.5f;
        internal static float BuyPrice(AbstractItem item) => item.SellValue * Markup;

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

            var wallet = InventoryProvider.Instance.Inventory;

            if (wallet != null)
            {
                wallet.OnContentChanged -= OnWalletChanged;
                wallet.OnContentChanged += OnWalletChanged;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            var wallet = InventoryProvider.Instance.Inventory;

            if (wallet != null)
                wallet.OnContentChanged -= OnWalletChanged;
        }

        /// Cached before the base runs, because refreshing the display repaints the
        /// background and that has to price the incoming item, not the outgoing one.
        public override void RefreshSlotDisplay(Package package)
        {
            displayedPackage = package;

            base.RefreshSlotDisplay(package);
        }

        private void OnWalletChanged(Dictionary<Vector2Int, Package> _) => RefreshBackground();

        protected override Color GetBackgroundColor() => CanAffordDisplayed()
            ? base.GetBackgroundColor()
            : Lighten(UnaffordableBackground);

        /// An empty slot has nothing to price, so it never reads as unaffordable - which
        /// also clears the tint for free on the slot an item was just bought out of.
        private bool CanAffordDisplayed()
        {
            if (!displayedPackage.IsValid)
                return true;

            var wallet = InventoryProvider.Instance.Inventory;

            return wallet == null || wallet.CanAfford(BuyPrice(displayedPackage.Item));
        }

        protected override void SetDisplaySize(RectTransform display, Package package)
        {
            base.SetDisplaySize(display, package);

            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();
            if (gridLayout)
            {
                var additionalSpacing = gridLayout.spacing * new Vector2(AbstractItem.GetDimensions(package.Item.Dimensions).x - 1, AbstractItem.GetDimensions(package.Item.Dimensions).y - 1);

                display.sizeDelta = gridLayout.cellSize * AbstractItem.GetDimensions(package.Item.Dimensions) + additionalSpacing;
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

            var wallet = InventoryProvider.Instance.Inventory;
            var price = BuyPrice(package.Item);

            if (!wallet.CanAfford(price))
                return;

            FadeOutPreview();

            // Right-click and shift-click both move the item straight to the inventory.
            #region BUY: IMMEDIATE MOVE
            if (eventData.button == PointerEventData.InputButton.Right || Input.GetKey(KeyCode.LeftShift))
            {
                _ = Container.RemoveAtPosition(position, package);

                if (wallet.TryAddToContainer(ref package))
                    _ = wallet.TryPay(price); // affordability already confirmed
                else
                    _ = Container.AddAtPosition(position, package); // bounced back to the shelf, no charge

                return;
            }
            #endregion BUY: IMMEDIATE MOVE

            // Drag: the drag system has no clean cancel/refund path; that gap is
            // pre-existing and deferred (follow-ups spec).
            #region BUY: DRAG
            _ = Container.RemoveAtPosition(position, package);

            _ = wallet.TryPay(price);

            var positionOffset = Position - position;

            DragProvider.Instance.SetPackage(this, package, positionOffset, pointerPosition);
            #endregion BUY: DRAG
        }

        protected override void DropItem(Package package)
        {
            if (!package.IsValid || package.Sender == Container)
                return;

            var packageToMove = DragProvider.Instance.DraggingPackage;

            _ = (DragProvider.Instance.Origin.Container?.RemoveFromContainer(packageToMove));

            var currency = new Currency(packageToMove.Item.SellValue * packageToMove.Amount);

            //TODO: handle item loss if inventory is full
            var gold = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Gold), currency.Gold);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref gold);
            var silver = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Silver), currency.Silver);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref silver);
            var iron = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Iron), currency.Iron);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref iron);
            var copper = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Copper), currency.Copper);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref copper);

            DragProvider.Instance.SetPackage(this, new Package(), Vector2Int.zero, Input.mousePosition);

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
        }
    }
}
