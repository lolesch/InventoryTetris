using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// Converts between the cursor, the drag display's rect, and container positions.
    /// Screen space is pixels anchored bottom-left; inventory space is cells anchored
    /// top-left with y growing downward. Every slot's <c>transform.position</c> is its
    /// top-left corner, because the slot prefab's pivot is (0, 1).
    /// <para>
    /// Pure on purpose: no Input, no Transform, no project types. The placement maths
    /// used to live inside MonoBehaviours reading Input.mousePosition, where nothing
    /// could reach it - which is why the drop rule and the drag visual were free to
    /// disagree for as long as they did.
    /// </para>
    /// </summary>
    public static class DragGeometry
    {
        /// <summary>
        /// The drag display pivot that puts <paramref name="pointer"/> on the exact point of
        /// the item it grabbed, so the item keeps the grip it was picked up by.
        /// </summary>
        /// <param name="pointer">Screen position the pointer went down at.</param>
        /// <param name="slotTopLeft">Screen position of the slot it went down on.</param>
        /// <param name="dimensions">The item's footprint in cells.</param>
        /// <param name="positionOffset">Cells from the item's origin to that slot, in inventory space.</param>
        /// <param name="cellSize">Pixels per cell.</param>
        public static Vector2 GrabPivot(Vector2 pointer, Vector2 slotTopLeft, Vector2Int dimensions, Vector2Int positionOffset, float cellSize)
        {
            /// pointer relative to the grabbed slot, in cells, anchored top-left
            var pivot = (pointer - slotTopLeft) / cellSize;
            /// convert to screen coordinates, anchored bottom-left
            pivot.y += 1f;
            /// scale to match the item's dimensions
            pivot /= dimensions;

            /// the offset arrives in inventory space; flip it into screen space the same way
            var offset = new Vector2(positionOffset.x, dimensions.y - 1 - positionOffset.y);
            offset /= dimensions;

            return pivot + offset;
        }

        /// <summary>
        /// The container position the drag display currently covers: its top-left corner
        /// rounded to the nearest cell. Read off the display's real rect rather than off the
        /// offset it was picked up by, so the drop can never disagree with what the player
        /// sees - rounding is what makes the item land in the slot it mostly covers.
        /// </summary>
        /// <param name="pointer">Current screen position of the pointer.</param>
        /// <param name="pivot">The drag display's pivot, as <see cref="GrabPivot"/> set it.</param>
        /// <param name="dimensions">The item's footprint in cells.</param>
        /// <param name="hoveredSlotTopLeft">Screen position of the slot under the cursor.</param>
        /// <param name="hoveredPosition">That slot's container position.</param>
        /// <param name="cellSize">Pixels per cell.</param>
        public static Vector2Int DropPosition(Vector2 pointer, Vector2 pivot, Vector2Int dimensions, Vector2 hoveredSlotTopLeft, Vector2Int hoveredPosition, float cellSize)
        {
            var size = (Vector2)dimensions * cellSize;
            var itemTopLeft = pointer - Vector2.Scale(pivot, size) + new Vector2(0f, size.y);

            var cells = (itemTopLeft - hoveredSlotTopLeft) / cellSize;

            return hoveredPosition + new Vector2Int(Mathf.RoundToInt(cells.x), Mathf.RoundToInt(-cells.y));
        }

        /// The pivot for a package handed over mid-drag by a swap: centred, so the cursor is
        /// inside it whatever its footprint. The previous item's positionOffset describes a
        /// footprint this one does not have.
        public static Vector2 HandOverPivot => new(.5f, .5f);
    }
}
