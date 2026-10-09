using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    /// <summary>
    /// A cell of the floor grid (epic #214). It looks and hovers like a stash cell, but the floor lends
    /// nothing to the cursor: a click takes the drop through the acquisition entry point, like a row of the
    /// list, and a dropped item lands through the Run's ground rather than into this cell.
    /// </summary>
    [System.Serializable]
    internal sealed class GroundSlotDisplay : InventorySlotDisplay
    {
        protected override void MoveItem(PointerEventData eventData, Vector2 pointerPosition)
        {
            if (!TryBeginMove(out _, out var package))
                return;

            _ = SimulationService.Instance.LootFlow?.PickUpFromGround(package.Item);

            SyncPreviewAfterMove();
        }

        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            if (!DropTransaction.Place(package, SimulationService.Instance.LootFlow))
            {
                _ = CancelHeldDrag();
                return;
            }

            DragProvider.Instance.EndDrag();

            DragProvider.Instance.Origin.Container?.InvokeRefresh();
        }

        public override bool WouldAcceptDrop(Package package) =>
            package.IsValid && SimulationService.Instance.LootFlow != null && base.WouldAcceptDrop(package);

        public void Fade(float alpha) => itemGroup.alpha = alpha;
    }
}
