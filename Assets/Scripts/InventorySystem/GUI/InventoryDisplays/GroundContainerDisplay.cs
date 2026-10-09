using System.Collections.Generic;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    /// <summary>
    /// The floor as a grid of stash-like cells (epic #214). The Run's ground is handed in through
    /// <see cref="AbstractContainerDisplay.SetupDisplay"/> by <c>GroundGridPanel</c>, since it is not a player container.
    /// Each drop is drawn at its cell and older ones fainter, relative to the other drops and not to time.
    /// </summary>
    [System.Serializable]
    internal sealed class GroundContainerDisplay : InventoryContainerDisplay
    {
        [SerializeField, Range(0f, 1f), Tooltip("The oldest drop's alpha; the newest is fully opaque.")]
        private float oldestAlpha = 0.35f;

        protected override bool BindsByRole => false;

        public void FadeByAge(IReadOnlyList<Vector2Int> cellsOldestFirst)
        {
            for (var rank = 0; rank < cellsOldestFirst.Count; rank++)
                if (TryGetSlotDisplayAt(cellsOldestFirst[rank], out var slot))
                    ((GroundSlotDisplay)slot).Fade(GroundFade.Alpha(rank, cellsOldestFirst.Count, oldestAlpha));
        }
    }
}
