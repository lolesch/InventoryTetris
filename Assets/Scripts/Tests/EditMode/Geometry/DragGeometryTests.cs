using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>
    /// Locks in DragGeometry: the grip is the exact point of the item under the cursor,
    /// and the drop follows the item's body rather than the cell it was picked up by.
    /// The round trip is the invariant the shipped code broke - grab an item anywhere
    /// inside itself, drop it without moving the cursor, and it must land back on its
    /// own origin.
    /// </summary>
    [TestFixture]
    public sealed class DragGeometryTests
    {
        private const float Cell = 60f;

        /// Grid cell (0,0)'s top-left corner. Arbitrary - every result is relative to it.
        private static readonly Vector2 GridOrigin = new(300f, 900f);

        private static Vector2 TopLeftOf(Vector2Int cell) =>
            new(GridOrigin.x + (cell.x * Cell), GridOrigin.y - (cell.y * Cell));

        /// A pointer <paramref name="withinX"/>/<paramref name="withinY"/> of the way across
        /// the cell, measured from its top-left corner.
        private static Vector2 PointerIn(Vector2Int cell, float withinX, float withinY) =>
            TopLeftOf(cell) + new Vector2(withinX * Cell, -withinY * Cell);

        [Test]
        [TestCase(1, 1, 0, 0, 0.1f, 0.1f)]
        [TestCase(1, 1, 0, 0, 0.9f, 0.9f)]
        [TestCase(1, 1, 0, 0, 0.5f, 0.5f)]
        [TestCase(2, 3, 1, 2, 0.08f, 0.92f)]
        [TestCase(2, 3, 0, 0, 0.5f, 0.5f)]
        [TestCase(2, 3, 1, 0, 0.99f, 0.01f)]
        [TestCase(1, 4, 0, 3, 0.99f, 0.01f)]
        [TestCase(2, 4, 1, 3, 0.33f, 0.67f)]
        public void GrabThenDropWithoutMoving_ReturnsTheItemToItsOrigin(
            int width, int height, int offsetX, int offsetY, float withinX, float withinY)
        {
            var dimensions = new Vector2Int(width, height);
            var positionOffset = new Vector2Int(offsetX, offsetY);
            var origin = new Vector2Int(3, 2);
            var grabbed = origin + positionOffset;

            var pointer = PointerIn(grabbed, withinX, withinY);

            var pivot = DragGeometry.GrabPivot(pointer, TopLeftOf(grabbed), dimensions, positionOffset, Cell);
            var dropped = DragGeometry.DropPosition(pointer, pivot, dimensions, TopLeftOf(grabbed), grabbed, Cell);

            Assert.That(dropped, Is.EqualTo(origin));
        }

        [Test]
        public void GrabPivot_IsTheExactPointOfTheItemUnderTheCursor()
        {
            var grabbed = new Vector2Int(2, 2);

            /// 5px from the left edge, 55px below the top => the item's bottom-left corner
            var pointer = TopLeftOf(grabbed) + new Vector2(5f, -55f);

            var pivot = DragGeometry.GrabPivot(pointer, TopLeftOf(grabbed), Vector2Int.one, Vector2Int.zero, Cell);

            Assert.That(pivot.x, Is.EqualTo(5f / Cell).Within(1e-4f), "x");
            Assert.That(pivot.y, Is.EqualTo(5f / Cell).Within(1e-4f), "y");
        }

        [Test]
        public void GrabPivot_ShiftsByWholeCells_ForTheCellOfALargeItemThatWasGrabbed()
        {
            var grabbed = new Vector2Int(3, 3);

            var pointer = TopLeftOf(grabbed) + new Vector2(5f, -55f);

            /// grabbed on the 2x3's bottom-right cell: half a footprint right, none up
            var pivot = DragGeometry.GrabPivot(pointer, TopLeftOf(grabbed), new Vector2Int(2, 3), new Vector2Int(1, 2), Cell);

            Assert.That(pivot.x, Is.EqualTo(0.5f + (5f / 120f)).Within(1e-4f), "x");
            Assert.That(pivot.y, Is.EqualTo(5f / 180f).Within(1e-4f), "y");
        }

        [Test]
        public void DropPosition_FollowsTheItemAcrossACellBoundary()
        {
            var grabbed = new Vector2Int(2, 2);
            var hovered = new Vector2Int(4, 4);

            /// grabbed by its bottom-left corner, now held with the cursor at a slot's top-right:
            /// the body has moved a whole cell right and up from the slot under the cursor
            var pivot = DragGeometry.GrabPivot(PointerIn(grabbed, 5f / Cell, 55f / Cell), TopLeftOf(grabbed), Vector2Int.one, Vector2Int.zero, Cell);

            var dropped = DragGeometry.DropPosition(PointerIn(hovered, 55f / Cell, 5f / Cell), pivot, Vector2Int.one, TopLeftOf(hovered), hovered, Cell);

            /// where the player sees it - 69% covered. The shipped rule said (4,4), at 3%.
            Assert.That(dropped, Is.EqualTo(new Vector2Int(5, 3)));
        }

        [Test]
        public void DropPosition_ForALargeItem_FollowsItsBodyNotItsGrip()
        {
            var dimensions = new Vector2Int(2, 3);
            var positionOffset = new Vector2Int(1, 2);
            var grabbed = new Vector2Int(3, 3);
            var hovered = new Vector2Int(4, 4);

            var pivot = DragGeometry.GrabPivot(PointerIn(grabbed, 5f / Cell, 55f / Cell), TopLeftOf(grabbed), dimensions, positionOffset, Cell);

            var dropped = DragGeometry.DropPosition(PointerIn(hovered, 55f / Cell, 5f / Cell), pivot, dimensions, TopLeftOf(hovered), hovered, Cell);

            /// 87% covered. The shipped rule said (3,2), at 42%.
            Assert.That(dropped, Is.EqualTo(new Vector2Int(4, 1)));
        }

        [Test]
        public void DropPosition_TracksTheHoveredCell_WhenTheItemWasGrabbedDeadCentre()
        {
            var grabbed = new Vector2Int(2, 2);
            var hovered = new Vector2Int(4, 4);
            var pivot = DragGeometry.GrabPivot(PointerIn(grabbed, .5f, .5f), TopLeftOf(grabbed), Vector2Int.one, Vector2Int.zero, Cell);

            /// picked up dead centre, so the item tracks the hovered cell right across it
            var nearTopLeft = DragGeometry.DropPosition(PointerIn(hovered, .05f, .05f), pivot, Vector2Int.one, TopLeftOf(hovered), hovered, Cell);
            var nearBottomRight = DragGeometry.DropPosition(PointerIn(hovered, .95f, .95f), pivot, Vector2Int.one, TopLeftOf(hovered), hovered, Cell);

            Assert.That(nearTopLeft, Is.EqualTo(hovered), "near the top-left");
            Assert.That(nearBottomRight, Is.EqualTo(hovered), "near the bottom-right");
        }

        [Test]
        public void DropPosition_IsReadFromAnySlot_NotJustTheOneUnderTheCursor()
        {
            var grabbed = new Vector2Int(2, 2);
            var pivot = DragGeometry.GrabPivot(PointerIn(grabbed, .5f, .5f), TopLeftOf(grabbed), Vector2Int.one, Vector2Int.zero, Cell);

            var pointer = PointerIn(new Vector2Int(4, 4), .5f, .5f);

            /// the answer describes the grid, so it cannot depend on which slot reported the hover
            var viaHovered = DragGeometry.DropPosition(pointer, pivot, Vector2Int.one, TopLeftOf(new Vector2Int(4, 4)), new Vector2Int(4, 4), Cell);
            var viaNeighbour = DragGeometry.DropPosition(pointer, pivot, Vector2Int.one, TopLeftOf(new Vector2Int(1, 6)), new Vector2Int(1, 6), Cell);

            Assert.That(viaNeighbour, Is.EqualTo(viaHovered));
        }

        [Test]
        public void HandOverPivot_IsCentred() =>
            Assert.That(DragGeometry.HandOverPivot, Is.EqualTo(new Vector2(.5f, .5f)));
    }
}
