using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    /// <summary>
    /// A cell of the Stash. It looks, hovers and drags like a bag cell, but its right-click is a retrieval,
    /// not an equip: the same acquisition a Supply right-click makes, so the item lands in the bag (or an
    /// empty gear slot with auto-equip on). Only a bag right-click swaps gear in place.
    /// </summary>
    [System.Serializable]
    internal sealed class StashSlotDisplay : InventorySlotDisplay
    {
        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition)
        {
            if (eventData.button != PointerEventData.InputButton.Right)
            {
                base.MoveItem(eventData, pointerPosition);
                return;
            }

            if (!TryBeginMove(out var position, out _))
                return;

            AcquireAt(position);
        }
    }
}
