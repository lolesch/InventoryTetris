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
        // TODO: make it a container with confirmation button before selling
        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            /// The sold item is already out of every container - the drag put it in hand at
            /// pick-up. The sale banks its value into the wallet as a commit-time effect on
            /// one transaction (issue #11), shared with VendorSlotDisplay; the sink swallows
            /// the item and the drag ends.
            VendorTransaction.Sell(package, InventoryProvider.Instance.Wallet);

            DragProvider.Instance.EndDrag();

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

            SyncPreviewAfterMove();
        }

        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition) { }
    }
}
