using System;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// <see cref="IRollSource"/> backed by a seeded <see cref="Random"/> - the same trick
    /// <c>ProbabilityTableSampleTests</c> uses to make a statistical assertion repeatable.
    /// </summary>
    internal sealed class SeededRollSource : IRollSource
    {
        private readonly Random rng;

        public SeededRollSource(int seed) => rng = new Random(seed);

        public float Next() => (float)rng.NextDouble();
    }

    /// <summary>
    /// <see cref="IRollSource"/> that hands back a fixed script of rolls in order and throws
    /// once it runs dry - so a test that miscounts how many rolls a path consumes fails
    /// loudly instead of reading zeros.
    /// </summary>
    internal sealed class QueuedRollSource : IRollSource
    {
        private readonly float[] rolls;
        private int next;

        public QueuedRollSource(params float[] rolls) => this.rolls = rolls ?? Array.Empty<float>();

        public int Consumed => next;

        public float Next()
        {
            if (next >= rolls.Length)
                throw new InvalidOperationException(
                    $"the roll script ran dry after {rolls.Length} rolls - the path under test consumes more");
            return rolls[next++];
        }
    }

    /// <summary>
    /// <see cref="IRollSource"/> that always returns the same value. Handy when a test cares
    /// about one decision and wants every other roll pinned to a corner.
    /// </summary>
    internal sealed class ConstantRollSource : IRollSource
    {
        private readonly float value;

        public ConstantRollSource(float value) => this.value = value;

        public float Next() => value;
    }
}
