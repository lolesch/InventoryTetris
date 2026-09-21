using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Where a package came from: the container and the cell inside it (issue #29). One
    /// value rather than two parameters, because the two are never meaningful apart - a
    /// cell with no container names nothing, and every module on the drop path was passing
    /// them together anyway.
    ///
    /// <para>This is the origin a cancel returns to, which is not always where the drag
    /// started: a mid-drag swap hands the cursor a different package, whose origin is its
    /// own displaced-from cell. Carrying it as a value is what keeps
    /// <c>CursorHolder</c> and <see cref="ICursorSink"/> from naming
    /// <see cref="Vector2Int"/>, and what lets a further origin fact - a slot index, a
    /// timestamp - be added without widening six signatures again.</para>
    /// </summary>
    public readonly struct PackageOrigin
    {
        /// <summary>The container the package was displaced from. Null when unknown.</summary>
        public AbstractDimensionalContainer Container { get; }

        /// <summary>The cell in <see cref="Container"/> it was displaced from.</summary>
        public Vector2Int Cell { get; }

        public PackageOrigin(AbstractDimensionalContainer container, Vector2Int cell)
        {
            Container = container;
            Cell = cell;
        }

        /// <summary>
        /// Whether an origin was actually recorded. The default value - no container, no
        /// cell - is the "came from nowhere we can return to" case a cancel falls back on.
        /// </summary>
        public bool IsKnown => Container != null;

        public void Deconstruct(out AbstractDimensionalContainer container, out Vector2Int cell)
        {
            container = Container;
            cell = Cell;
        }
    }
}
