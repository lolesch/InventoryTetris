using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>Locks in the arena's placement maths: slots, approach, facing and damage accumulation.</summary>
    [TestFixture]
    public sealed class ArenaLayoutTests
    {
        private const float Tau = 2f * UnityEngine.Mathf.PI;
        private const float Eps = 1e-4f;

        /// Smallest signed difference between two angles, in (-pi, pi].
        private static float AngleDelta(float a, float b)
        {
            var d = (a - b) % Tau;
            if (d > UnityEngine.Mathf.PI) d -= Tau;
            if (d <= -UnityEngine.Mathf.PI) d += Tau;
            return d;
        }

        [Test]
        public void PickSlotAngle_TakesTheMiddleOfTheLargestGap()
        {
            // Enemies at 0, 90 and 180 degrees: the gap 180 -> 360 is the widest, its middle is 270.
            var angles = new[] { 0f, Tau / 4f, Tau / 2f };

            var picked = ArenaLayout.PickSlotAngle(angles, 0f);

            Assert.That(AngleDelta(picked, 3f * Tau / 4f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void PickSlotAngle_AddsTheInjectedJitter()
        {
            var angles = new[] { 0f, Tau / 4f, Tau / 2f };

            var picked = ArenaLayout.PickSlotAngle(angles, 0.1f);

            Assert.That(AngleDelta(picked, (3f * Tau / 4f) + 0.1f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void PickSlotAngle_FindsTheGapAcrossTheWrapAround()
        {
            // 350, 10 and 100 degrees: the widest gap is 100 -> 350, its middle is 225. The 350 -> 10 gap wraps past zero.
            var angles = new[] { Deg(350f), Deg(10f), Deg(100f) };

            var picked = ArenaLayout.PickSlotAngle(angles, 0f);

            Assert.That(AngleDelta(picked, Deg(225f)), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void PickSlotAngle_WithNobodyStanding_ReturnsTheJitterAlone()
        {
            var picked = ArenaLayout.PickSlotAngle(System.Array.Empty<float>(), 0.3f);

            Assert.That(AngleDelta(picked, 0.3f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void PickSlotAngle_ABatchPlacedOneAfterAnotherSpreadsOut()
        {
            var placed = new System.Collections.Generic.List<float>();
            for (var i = 0; i < 8; i++)
                placed.Add(ArenaLayout.PickSlotAngle(placed, 0f));

            placed.Sort();
            for (var i = 0; i < placed.Count; i++)
            {
                var next = placed[(i + 1) % placed.Count];
                var gap = AngleDelta(next, placed[i]);
                if (gap < 0f) gap += Tau;

                // even spacing is Tau/8; a pick that bunched two enemies would leave a gap well under half of it
                Assert.That(gap, Is.GreaterThanOrEqualTo(Tau / 8f / 2f), $"gap {i} between {placed[i]} and {next}");
            }
        }

        private static float Deg(float degrees) => degrees * UnityEngine.Mathf.Deg2Rad;

        private const float Rx = 300f;
        private const float Ry = 120f;

        private static float Norm(UnityEngine.Vector2 p) => new UnityEngine.Vector2(p.x / Rx, p.y / Ry).magnitude;

        [Test]
        public void SpawnPoint_IsOutsideTheRingOnTheSlotsRay()
        {
            var angle = Deg(30f);

            var slot = ArenaLayout.SlotPoint(angle, Rx, Ry);
            var spawn = ArenaLayout.SpawnPoint(angle, Rx, Ry, 80f);

            Assert.That(Norm(slot), Is.EqualTo(1f).Within(Eps));
            Assert.That(Norm(spawn), Is.GreaterThan(1f));
            Assert.That(UnityEngine.Vector2.Distance(slot, spawn), Is.EqualTo(80f).Within(Eps));
            // same ray from the anchor, so a straight walk to the anchor stops on the slot itself
            Assert.That(slot.x * spawn.y - slot.y * spawn.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Approach_StepsTowardTheAnchorInAStraightLine()
        {
            var start = ArenaLayout.SpawnPoint(Deg(30f), Rx, Ry, 200f);

            var step = ArenaLayout.Approach(start, 10f, Rx, Ry);

            Assert.That(step.Arrived, Is.False);
            Assert.That(UnityEngine.Vector2.Distance(start, step.Position), Is.EqualTo(10f).Within(Eps));
            // collinear with the start and the anchor (the origin): a straight line, not a curve
            Assert.That(start.x * step.Position.y - start.y * step.Position.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(step.Position.magnitude, Is.LessThan(start.magnitude));
        }

        [Test]
        public void Approach_NeverOvershootsTheRing_AndReportsArrivalOnIt()
        {
            var angle = Deg(30f);
            var slot = ArenaLayout.SlotPoint(angle, Rx, Ry);
            var start = ArenaLayout.SpawnPoint(angle, Rx, Ry, 50f);

            var step = ArenaLayout.Approach(start, 10000f, Rx, Ry);

            Assert.That(step.Arrived, Is.True);
            Assert.That(UnityEngine.Vector2.Distance(step.Position, slot), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Norm(step.Position), Is.LessThanOrEqualTo(1f + Eps));
        }

        [Test]
        public void Approach_WalkedInSmallStepsArrivesOnTheRingAndThenStays()
        {
            var position = ArenaLayout.SpawnPoint(Deg(200f), Rx, Ry, 120f);
            var arrivals = 0;
            for (var i = 0; i < 100; i++)
            {
                var step = ArenaLayout.Approach(position, 7f, Rx, Ry);
                position = step.Position;
                if (step.Arrived)
                    arrivals++;
                else
                    Assert.That(Norm(position), Is.GreaterThan(1f), $"step {i} stopped short of the ring");
            }

            Assert.That(arrivals, Is.GreaterThan(0));
            Assert.That(Norm(position), Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void Approach_FromInsideTheRing_DoesNotMove()
        {
            var inside = new UnityEngine.Vector2(10f, 5f);

            var step = ArenaLayout.Approach(inside, 4f, Rx, Ry);

            Assert.That(step.Arrived, Is.True);
            Assert.That(step.Position, Is.EqualTo(inside));
        }

        [TestCase(500f, 100f, -1, 1)]   // anchor to the right of the enemy
        [TestCase(100f, 500f, 1, -1)]   // anchor to the left
        [TestCase(500f, 100f, 1, 1)]
        [TestCase(100f, 500f, -1, -1)]
        public void FacingSign_FollowsWhichSideTheAnchorIsOn(float anchorX, float enemyX, int current, int expected)
        {
            Assert.That(ArenaLayout.FacingSign(enemyX, anchorX, current, 5f), Is.EqualTo(expected));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void FacingSign_InsideTheDeadZone_KeepsTheCurrentSign(int current)
        {
            // 3 units to either side of the anchor, dead zone 5
            Assert.That(ArenaLayout.FacingSign(100f, 103f, current, 5f), Is.EqualTo(current));
            Assert.That(ArenaLayout.FacingSign(100f, 97f, current, 5f), Is.EqualTo(current));
            Assert.That(ArenaLayout.FacingSign(100f, 100f, current, 5f), Is.EqualTo(current));
        }

        [Test]
        public void FacingSign_JustOutsideTheDeadZone_Flips()
        {
            Assert.That(ArenaLayout.FacingSign(100f, 105f, -1, 5f), Is.EqualTo(1));
            Assert.That(ArenaLayout.FacingSign(100f, 95f, 1, 5f), Is.EqualTo(-1));
        }

        [Test]
        public void DamageAccumulator_SumsPerEnemyAndFlushesOnce()
        {
            var accumulator = new DamageAccumulator<string>();
            accumulator.Add("a", 3f);
            accumulator.Add("b", 1f);
            accumulator.Add("a", 4f);

            var flushed = new System.Collections.Generic.Dictionary<string, float>();
            accumulator.Flush((key, total) => flushed.Add(key, total));

            Assert.That(flushed, Has.Count.EqualTo(2));
            Assert.That(flushed["a"], Is.EqualTo(7f));
            Assert.That(flushed["b"], Is.EqualTo(1f));
        }

        [Test]
        public void DamageAccumulator_ResetsAfterFlush()
        {
            var accumulator = new DamageAccumulator<string>();
            accumulator.Add("a", 3f);
            accumulator.Flush((_, _) => { });

            var calls = 0;
            accumulator.Flush((_, _) => calls++);
            Assert.That(calls, Is.EqualTo(0));

            accumulator.Add("a", 2f);
            var total = 0f;
            accumulator.Flush((_, t) => total = t);
            Assert.That(total, Is.EqualTo(2f), "the next frame starts from zero, not from the flushed 3");
        }

        [Test]
        public void DamageAccumulator_FlushesOnlyKeysThatTookDamage()
        {
            var accumulator = new DamageAccumulator<string>();
            accumulator.Add("a", 1f);
            accumulator.Flush((_, _) => { });
            accumulator.Add("b", 1f);

            var keys = new System.Collections.Generic.List<string>();
            accumulator.Flush((key, _) => keys.Add(key));

            Assert.That(keys, Is.EqualTo(new[] { "b" }));
        }
    }
}
