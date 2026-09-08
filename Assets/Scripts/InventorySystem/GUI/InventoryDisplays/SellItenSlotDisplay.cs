using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    internal sealed class SellItenSlotDisplay : AbstractSlotDisplay
    {
        /// <summary>
        /// The single-slot instant sale is gone (issue #32) - the Sell Basket is the only way
        /// to sell, so this legacy slot display no longer banks a dropped Package. It is kept
        /// as an inert placeholder until the basket grid panel (the human Editor pass) replaces
        /// it; a Package dropped here is returned to its sender rather than sold.
        /// </summary>
        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            /// No sale: the dedicated sell slot no longer mints (issue #32). A drag that ends
            /// here sends the Package home through the return-to-origin primitive (which
            /// DragProvider.CancelDrag runs), so nothing is lost - and the basket is the one
            /// place selling happens.
            _ = DragProvider.Instance.CancelDrag();

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin?.Container?.InvokeRefresh();

            SyncPreviewAfterMove();
        }

        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition) { }
    }
}