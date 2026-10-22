using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Geometry;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>The arena's arena-to-canvas projection and its depth order (spatial-combat spec, "The arena and positions").</summary>
    [TestFixture]
    public sealed class ArenaProjectionTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void ToCanvas_TheOriginProjectsToTheCanvasOrigin()
        {
            var projection = new ArenaProjection(16f, 0.5f);
            var origin = new Coordinate(3f, -2f);

            Assert.That(projection.ToCanvas(origin, origin), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ToCanvas_ATiltOfOneDrawsTheArenaTopDown()
        {
            var projection = new ArenaProjection(10f, 1f);

            var canvas = projection.ToCanvas(new Coordinate(2f, 3f), default);

            Assert.That(canvas.x, Is.EqualTo(20f).Within(Eps));
            Assert.That(canvas.y, Is.EqualTo(30f).Within(Eps));
        }

        [Test]
        public void ToCanvas_ATiltBelowOneFlattensTheDepthAxisOnly()
        {
            var projection = new ArenaProjection(10f, 0.5f);

            var canvas = projection.ToCanvas(new Coordinate(2f, 3f), default);

            Assert.That(canvas.x, Is.EqualTo(20f).Within(Eps));
            Assert.That(canvas.y, Is.EqualTo(15f).Within(Eps));
        }

        [Test]
        public void ToCanvas_ReadsThePositionRelativeToTheOrigin()
        {
            var projection = new ArenaProjection(10f, 1f);

            var canvas = projection.ToCanvas(new Coordinate(5f, 5f), new Coordinate(4f, 7f));

            Assert.That(canvas.x, Is.EqualTo(10f).Within(Eps));
            Assert.That(canvas.y, Is.EqualTo(-20f).Within(Eps));
        }

        [Test]
        public void ToCanvas_ATiltOfZeroCollapsesTheArenaOntoALine()
        {
            var projection = new ArenaProjection(10f, 0f);

            Assert.That(projection.ToCanvas(new Coordinate(1f, 8f), default).y, Is.EqualTo(0f));
        }

        [Test]
        public void DepthOrder_TheFartherSideOfTheArenaDrawsFirst()
        {
            var behind = new Coordinate(0f, 5f);
            var inFront = new Coordinate(0f, -5f);

            Assert.That(ArenaProjection.DepthOrder(behind, inFront), Is.Negative);
            Assert.That(ArenaProjection.DepthOrder(inFront, behind), Is.Positive);
            Assert.That(ArenaProjection.DepthOrder(behind, new Coordinate(9f, 5f)), Is.Zero);
        }

        [Test]
        public void DepthOrder_FollowsTheArenaNotTheTilt()
        {
            // At a tilt of zero both land on one canvas row, yet one still stands behind the other.
            var projection = new ArenaProjection(10f, 0f);
            var a = new Coordinate(0f, 4f);
            var b = new Coordinate(0f, 1f);

            Assert.That(projection.ToCanvas(a, default).y, Is.EqualTo(projection.ToCanvas(b, default).y));
            Assert.That(ArenaProjection.DepthOrder(a, b), Is.Negative);
        }
    }
}
