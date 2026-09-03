using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The single data source for the hover-preview panel: what a cursor resting over a
    /// container cell should describe. The slot displays call this on hover and again after
    /// a move they performed - passing the cell the cursor actually rests over, not the
    /// slot's own <c>Position</c>, because a drop follows the drag visual and routinely
    /// lands a cell away (issue #13, QA-2).
    /// </summary>
    public static class HoverPreview
    {
        /// <summary>
        /// The package stored under <paramref name="position"/> of
        /// <paramref name="container"/>, walked back to the item's origin cell so any cell a
        /// multi-cell item covers describes that item. An invalid <see cref="Package"/> when
        /// the cell is empty or there is no container - the caller reads that as "hide the
        /// preview", which is how a swap that vacated the hovered cell clears the stale item
        /// instead of relying on a later pointer-exit.
        /// </summary>
        public static Package Under(AbstractDimensionalContainer container, Vector2Int position)
        {
            Package stored = default;

            _ = container?.TryGetItemAt(ref position, out stored);

            return stored;
        }
    }
}
