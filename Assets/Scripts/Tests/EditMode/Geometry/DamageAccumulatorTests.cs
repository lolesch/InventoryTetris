using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>
    /// The damage number accumulator (issues #181, #213): hits are summed per key and flushed once a frame. The arena
    /// keys it by target and damage type, so the keys here are a (target, type) pair - a Strike and a Cast on one
    /// enemy in one frame are two keys, hence two numbers. Each key carries the amount lost, which the number
    /// shows, and the raw amount, which its size is read from.
    /// </summary>
    [TestFixture]
    public sealed class DamageAccumulatorTests
    {
        private const int Physical = 0;
        private const int Magical = 1;

        private static Dictionary<(string, int), (float Lost, float Raw, int Hits)> Flushed(DamageAccumulator<(string, int)> accumulator)
        {
            var flushed = new Dictionary<(string, int), (float, float, int)>();
            accumulator.Flush((key, lost, raw, hits) => flushed.Add(key, (lost, raw, hits)));
            return flushed;
        }

        [Test]
        public void SumsPerKeyAndFlushesOnce()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("goblin", Physical), 3f, 3f);
            accumulator.Add(("orc", Physical), 1f, 1f);
            accumulator.Add(("goblin", Physical), 4f, 4f);

            var flushed = Flushed(accumulator);

            Assert.That(flushed, Has.Count.EqualTo(2));
            Assert.That(flushed[("goblin", Physical)].Lost, Is.EqualTo(7f));
            Assert.That(flushed[("orc", Physical)].Lost, Is.EqualTo(1f));
        }

        [Test]
        public void AStrikeAndACastOnOneTarget_AreTwoEntries()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("goblin", Physical), 5f, 5f);
            accumulator.Add(("goblin", Magical), 8f, 8f);

            var flushed = Flushed(accumulator);

            Assert.That(flushed, Has.Count.EqualTo(2));
            Assert.That(flushed[("goblin", Physical)].Lost, Is.EqualTo(5f));
            Assert.That(flushed[("goblin", Magical)].Lost, Is.EqualTo(8f));
        }

        [Test]
        public void TheSameTypeOnTwoTargets_AreTwoEntries()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("goblin", Magical), 5f, 5f);
            accumulator.Add(("orc", Magical), 5f, 5f);

            Assert.That(Flushed(accumulator), Has.Count.EqualTo(2));
        }

        [Test]
        public void TheLostAndTheRawAmountsAreSummedSeparately()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("hero", Physical), 6f, 10f);
            accumulator.Add(("hero", Physical), 3f, 5f);

            var total = Flushed(accumulator)[("hero", Physical)];

            Assert.That(total.Lost, Is.EqualTo(9f));
            Assert.That(total.Raw, Is.EqualTo(15f));
        }

        [Test]
        public void EachHitIsCounted_SoASizeCanBeReadFromOneOfThem()
        {
            // Several ordinary hits in one coarse frame sum to a raw amount far above any one hit: the sink
            // divides by the count to read the size from a single hit, while the label keeps the summed loss.
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("goblin", Physical), 4f, 5f);
            accumulator.Add(("goblin", Physical), 4f, 5f);
            accumulator.Add(("goblin", Physical), 4f, 5f);
            accumulator.Add(("orc", Physical), 2f, 2f);

            var flushed = Flushed(accumulator);

            Assert.That(flushed[("goblin", Physical)].Hits, Is.EqualTo(3));
            Assert.That(flushed[("goblin", Physical)].Raw / flushed[("goblin", Physical)].Hits, Is.EqualTo(5f));
            Assert.That(flushed[("goblin", Physical)].Lost, Is.EqualTo(12f), "the label still shows the summed loss");
            Assert.That(flushed[("orc", Physical)].Hits, Is.EqualTo(1));
        }

        [Test]
        public void TheHitCountResetsAfterFlush()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("a", Physical), 1f, 1f);
            accumulator.Add(("a", Physical), 1f, 1f);
            Flushed(accumulator);
            accumulator.Add(("a", Physical), 1f, 1f);

            Assert.That(Flushed(accumulator)[("a", Physical)].Hits, Is.EqualTo(1));
        }

        [Test]
        public void ResetsAfterFlush()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("a", Physical), 3f, 3f);
            Flushed(accumulator);

            Assert.That(Flushed(accumulator), Is.Empty);

            accumulator.Add(("a", Physical), 2f, 2f);
            Assert.That(Flushed(accumulator)[("a", Physical)].Lost, Is.EqualTo(2f), "the next frame starts from zero, not from the flushed 3");
        }

        [Test]
        public void FlushesOnlyKeysThatTookDamage()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("a", Physical), 1f, 1f);
            Flushed(accumulator);
            accumulator.Add(("b", Physical), 1f, 1f);

            Assert.That(Flushed(accumulator).Keys, Is.EquivalentTo(new[] { ("b", Physical) }));
        }

        [Test]
        public void ASinkThatAddsAgain_StartsTheNextFrame()
        {
            var accumulator = new DamageAccumulator<(string, int)>();
            accumulator.Add(("a", Physical), 1f, 1f);

            // A number that cannot be placed yet puts its amount back; it must not loop in this flush.
            var calls = 0;
            accumulator.Flush((key, lost, raw, _) =>
            {
                calls++;
                accumulator.Add(key, lost, raw);
            });

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Flushed(accumulator)[("a", Physical)].Lost, Is.EqualTo(1f));
        }
    }
}
