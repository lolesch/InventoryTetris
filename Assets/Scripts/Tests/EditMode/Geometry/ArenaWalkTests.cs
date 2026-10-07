using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>Locks in the walk-in: where a view starts, how far a step goes, and that arrival sticks.</summary>
    [TestFixture]
    public sealed class ArenaWalkTests
    {
        private const float Rx = 160f;
        private const float Ry = 100f;
        private const float Margin = 120f;
        private const float Eps = 1e-3f;

        private static float Deg(float degrees) => degrees * Mathf.Deg2Rad;

        private static float Normalised(Vector2 offset) => new Vector2(offset.x / Rx, offset.y / Ry).magnitude;

        [Test]
        public void Begin_StartsOutsideTheRingOnTheSlotsRay_NotArrived()
        {
            var angle = Deg(30f);

            var walk = ArenaWalk.Begin(angle, Rx, Ry, Margin);

            Assert.That(walk.Offset, Is.EqualTo(ArenaLayout.SpawnPoint(angle, Rx, Ry, Margin)));
            Assert.That(Normalised(walk.Offset), Is.GreaterThan(1f));
            Assert.That(walk.Arrived, Is.False);
        }

        [Test]
        public void Step_WalksTheGivenDistanceStraightTowardTheAnchor()
        {
            var walk = ArenaWalk.Begin(Deg(30f), Rx, Ry, Margin);
            var before = walk.Offset;

            walk.Step(25f, Deg(30f), Rx, Ry);

            Assert.That(before.magnitude - walk.Offset.magnitude, Is.EqualTo(25f).Within(Eps));
            Assert.That(Vector2.Angle(before, walk.Offset), Is.EqualTo(0f).Within(Eps));
            Assert.That(walk.Arrived, Is.False);
        }

        [Test]
        public void Step_ByZeroDistance_StandsStill()
        {
            // SimSpeed 0 (paused) hands the walk a zero step.
            var walk = ArenaWalk.Begin(Deg(30f), Rx, Ry, Margin);
            var before = walk.Offset;

            walk.Step(0f, Deg(30f), Rx, Ry);

            Assert.That(walk.Offset, Is.EqualTo(before));
            Assert.That(walk.Arrived, Is.False);
        }

        [Test]
        public void Step_PastTheRing_StopsOnTheSlotAndArrives()
        {
            var angle = Deg(200f);
            var walk = ArenaWalk.Begin(angle, Rx, Ry, Margin);

            walk.Step(10000f, angle, Rx, Ry);

            var slot = ArenaLayout.SlotPoint(angle, Rx, Ry);
            Assert.That(walk.Arrived, Is.True);
            Assert.That(Vector2.Distance(walk.Offset, slot), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Step_AfterArriving_StaysOnTheSlot()
        {
            var angle = Deg(310f);
            var walk = ArenaWalk.Begin(angle, Rx, Ry, Margin);
            walk.Step(10000f, angle, Rx, Ry);

            for (var i = 0; i < 5; i++)
                walk.Step(10f, angle, Rx, Ry);

            var slot = ArenaLayout.SlotPoint(angle, Rx, Ry);
            Assert.That(walk.Arrived, Is.True);
            Assert.That(Vector2.Distance(walk.Offset, slot), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Step_InSmallSteps_ArrivesAfterTheWalkLength()
        {
            var angle = Deg(90f);
            var walk = ArenaWalk.Begin(angle, Rx, Ry, Margin);

            // Straight up the Y axis the ring is Ry out, so the walk is exactly Margin long.
            walk.Step(Margin - 1f, angle, Rx, Ry);
            Assert.That(walk.Arrived, Is.False);

            walk.Step(2f, angle, Rx, Ry);
            Assert.That(walk.Arrived, Is.True);
        }

        [Test]
        public void Default_IsNotPlaced()
        {
            Assert.That(default(ArenaWalk).Started, Is.False);
            Assert.That(ArenaWalk.Begin(0f, Rx, Ry, Margin).Started, Is.True);
        }
    }
}
