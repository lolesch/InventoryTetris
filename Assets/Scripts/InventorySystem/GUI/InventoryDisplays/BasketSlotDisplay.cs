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
        /// The home a Cancel should return this Package to - <see cref="DragProvider.ReturnOrigin"/>,
        /// not <see cref="DragProvider.Origin"/>: a mid-drag swap hands over a different Package
        /// than the one the drag started with, and only <c>ReturnOrigin</c> tracks that
        /// swapped-in Package's real container and cell (see its doc comment).
        /// </summary>
        private PackageOrigin GetOrigin() => DragProvider.Instance.ReturnOrigin;

        /// <summary>
        /// A pick-up out of the basket is an ordinary drag (see class doc): it lands back on
        /// this same cell if cancelled, so that path leaves the basket's origin ledger alone.
        /// Shift-click follows the same quick-move matrix as every other source (issue #33) -
        /// with the Vendor open, a basket Package returns to the backpack - and, unlike a plain
        /// drag, leaves the basket for good on success, so it also clears the cell's ledger
        /// entry rather than leaving a stale one <see cref="SellBasket.Cancel"/> would only
        /// ever skip over.
        ///
        /// <para>A plain drag dropped somewhere other than back into the basket has the same
        /// stale-entry gap - the ledger cleanup there would need the drag lifecycle itself to
        /// know it started in the basket and did not return, which is a bigger change than
        /// this method; left for a follow-up rather than folded in here.</para>
        /// </summary>
        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition)
        {
            if (Container == null)
                return;

            var position = Position;

            if (!Container.TryGetItemAt(ref position, out var package))
                return;

            FadeOutPreview();

            if (Input.GetKey(KeyCode.LeftShift))
            {
                var intent = InventoryProvider.Instance.QuickMoveFor(Container);

                if (intent.Kind != QuickMoveIntentKind.MoveToContainer)
                    return;

                if (QuickMoveToContainer(intent.Target, position, package))
                    _ = InventoryProvider.Instance.Basket?.Origins.Remove(position);

                return;
            }

            _ = Container.RemoveAtPosition(position, package);

            var positionOffset = Position - position;

            DragProvider.Instance.SetPackage(this, package, positionOffset, pointerPosition);
        }
    }
}