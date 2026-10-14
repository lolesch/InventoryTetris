using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The Encounter's three roll streams travel as one value (epic #262, ticket #263): a new stream is a member
    /// here, not a parameter in every module that hands them on.
    /// </summary>
    [TestFixture]
    public sealed class RollStreamsTests
    {
        [Test]
        public void TheMainStreamIsRequired() =>
            Assert.That(() => new RollStreams(null), Throws.ArgumentNullException);

        [Test]
        public void WithOnlyTheMainStream_TheMovementAndHitStreamsReadTheMiddle()
        {
            var streams = new RollStreams(new ConstantRollSource(0.2f));

            Assert.That(streams.Main.Next(), Is.EqualTo(0.2f));
            Assert.That(streams.Movement.Next(), Is.EqualTo(0.5f), "no bearing jitter, a stop pulled in by half the jitter");
            Assert.That(streams.Hit.Next(), Is.EqualTo(0.5f), "the middle of the spread: a hit is its base damage");
        }

        [Test]
        public void TheStreamsStaySeparate()
        {
            var main = new QueuedRollSource(0.1f);
            var movement = new QueuedRollSource(0.2f);
            var hit = new QueuedRollSource(0.3f);

            var streams = new RollStreams(main, movement, hit);

            Assert.That(streams.Hit.Next(), Is.EqualTo(0.3f));
            Assert.That(streams.Movement.Next(), Is.EqualTo(0.2f));
            Assert.That(streams.Main.Next(), Is.EqualTo(0.1f));
        }
    }
}
