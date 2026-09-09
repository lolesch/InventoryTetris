using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    /// <summary>
    /// A slot in the Sell Basket grid (issue #66). The basket is a grid of many cells, so it
    /// needs a slot display of its own - the legacy single-slot <see cref="SellItenSlotDisplay"/>
    /// is one cell by design (and is deleted outright by #56). Selling happens exactly here or
    /// through a shift-click (<see cref="SellBasketQuickMove"/>); nothing else banks a Package,
    /// so the drop lands it in the basket grid with its origin remembered.
    ///
    /// <para>A drag that starts from the basket is an ordinary pick-up and routes through the
    /// base's drag machinery; only the drop here is the basket's own. The staging transaction is
    /// the same one <see cref="AbstractSlotDisplay"/> uses for every drop, so a Package that
    /// does not fit rolls back and stays on the cursor.</para>
    /// </summary>
    internal sealed class BasketSlotDisplay : AbstractSlotDisplay
    {
        /// <summary>
        /// A drag that ends here stages into the basket: land it at this cell, remember its
        /// origin in the basket's ledger. Directly parallels <see cref="SellBasket.Stage"/>, the
        /// drag-side sibling of the shift-click <see cref="SellBasketQuickMove.SendToBasket"/>.
        /// </summary>
        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            var provider = InventoryProvider.Instance;
            var basket = provider?.Basket;

            if (basket == null)
                return;

            var inHand = package;
            var at = Position;

            if (SellBasket.Stage(basket, ref inHand, GetOrigin(), at))
            {
                DragProvider.Instance.EndDrag();
                Container?.InvokeRefresh();
                DragProvider.Instance.Origin?.Container?.InvokeRefresh();
            }

            SyncPreviewAfterMove();
        }

        /// <summary>
        /// The slot this drag came from - its cell and container - which is where a Cancel
        /// returns the Package. The origin is read off <see cref="DragProvider.Origin"/> the way
        /// the other slot displays hand a Package to the drag.
        /// </summary>
        private PackageOrigin GetOrigin()
        {
            var originSlot = DragProvider.Instance.Origin;

            return originSlot != null
                ? new PackageOrigin(originSlot.Container, originSlot.Position)
                : default;
        }

        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition) { }
    }
}