using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(CanvasGroup))]
    internal sealed class DropToFloorSlotDisplay : AbstractSlotDisplay
    {
        private CanvasGroup canvasGroup;
        public CanvasGroup CanvasGroup => canvasGroup != null ? canvasGroup : canvasGroup = GetComponent<CanvasGroup>();
        protected override void DropItem(Package package)
        {
            /// An empty hand drops nothing, and has no Origin to repaint: a grab the Supply
            /// refused (an item the player cannot afford) still ends in a drop on this sink.
            if (!package.IsValid)
                return;

            /// The floor is the Run's ground (issue #63): the item joins the Ground Items List,
            /// where a click picks it back up. A purchase in progress never gets here - the base
            /// drop turns it away first. With no Run there is no ground, and the item goes back
            /// where it came from instead of being destroyed.
            if (!DropTransaction.Place(package, SimulationService.Instance.LootFlow))
            {
                _ = CancelHeldDrag();
                return;
            }

            DragProvider.Instance.EndDrag();

            DragProvider.Instance.Origin.Container?.InvokeRefresh();
        }

        /// The red "can't drop" tint tells the truth in Town, where there is no ground to drop on.
        public override bool WouldAcceptDrop(Package package) =>
            package.IsValid && SimulationService.Instance.LootFlow != null && base.WouldAcceptDrop(package);

        private void Update() => CanvasGroup.interactable = DragProvider.Instance.IsDragging;
    }
}
