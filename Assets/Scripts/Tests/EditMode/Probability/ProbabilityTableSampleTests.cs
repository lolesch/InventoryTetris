using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks ProbabilityTable&lt;T&gt;.Sample: a single-pass CDF walk over a [0,1] roll.
    /// Exact boundaries resolve to the outcome that owns them; roll 0 skips zero-weight
    /// entries; a roll at or past the final threshold returns the last non-zero outcome,
    /// never default(T); observed frequencies over a seeded sample match the weights.
    /// </summary>
    [TestFixture]
    public sealed class ProbabilityTableSampleTests
    {
        // Tier: [Nothing, White, Blue, Yellow, Orange]
        private static ProbabilityTable<Tier> Table(float white, float blue, float yellow, float orange,
            float failWeight = 0f, float exponent = 1f) =>
            new(new[] { 0f, white, blue, yellow, orange }, failWeight, exponent);

        [Test]
        public void RollOfZero_ReturnsFirstNonZeroOutcome_NotTheZeroWeightFailBucket()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f); // P(Nothing) == 0

            Assert.That(table.Sample(0f), Is.EqualTo(Tier.White));
        }

        [Test]
        public void RollOfOne_ReturnsTheLastNonZeroOutcome()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f);

            Assert.That(table.Sample(1f), Is.EqualTo(Tier.Orange));
        }

        [Test]
        public void RollPastTheFinalThreshold_ReturnsLastOutcome_NeverDefault()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f);

            Assert.That(table.Sample(1.0001f), Is.EqualTo(Tier.Orange));
            Assert.That(table.Sample(42f), Is.EqualTo(Tier.Orange));
        }

        [Test]
        public void RollPastFinalThreshold_SkipsATrailingZeroWeightMember()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 0f); // Orange has weight 0

            Assert.That(table.Sample(1f), Is.EqualTo(Tier.Yellow));
            Assert.That(table.Sample(2f), Is.EqualTo(Tier.Yellow));
        }

        [Test]
        public void ExactCdfBoundary_ResolvesToTheOutcomeThatOwnsIt()
        {
            // white .25, blue .25, yellow .25, orange .25  →  CDF 0.25 / 0.50 / 0.75 / 1.0
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f);

            Assert.That(table.Sample(0.25f), Is.EqualTo(Tier.White),  "0.25 is still inside White's band");
            Assert.That(table.Sample(0.50f), Is.EqualTo(Tier.Blue));
            Assert.That(table.Sample(0.75f), Is.EqualTo(Tier.Yellow));
        }

        [Test]
        public void StaticSample_OverAnArbitraryVector_WalksTheSameWay()
        {
            var vector = new[] { 0f, 0.5f, 0f, 0.5f, 0f };

            Assert.That(ProbabilityTable<Tier>.Sample(vector, 0f),   Is.EqualTo(Tier.White));
            Assert.That(ProbabilityTable<Tier>.Sample(vector, 0.5f), Is.EqualTo(Tier.White));
            Assert.That(ProbabilityTable<Tier>.Sample(vector, 0.6f), Is.EqualTo(Tier.Yellow));
            Assert.That(ProbabilityTable<Tier>.Sample(vector, 1f),   Is.EqualTo(Tier.Yellow));
        }

        [Test]
        public void ObservedFrequencies_MatchTheWeights_OverASeededSample()
        {
            var table = Table(white: 50f, blue: 30f, yellow: 15f, orange: 5f);
            var rng = new Random(12345);
            var counts = new int[ProbabilityTable<Tier>.Outcomes.Count];

            const int n = 200_000;
            for (var i = 0; i < n; i++)
            {
                var outcome = table.Sample((float)rng.NextDouble());
                counts[(int)IndexOf(outcome)]++;
            }

            AssertShare(counts, Tier.White, 0.50f, n);
            AssertShare(counts, Tier.Blue, 0.30f, n);
            AssertShare(counts, Tier.Yellow, 0.15f, n);
            AssertShare(counts, Tier.Orange, 0.05f, n);

            static void AssertShare(int[] counts, Tier tier, float expected, int n) =>
                Assert.That(counts[(int)IndexOf(tier)] / (float)n, Is.EqualTo(expected).Within(0.01f), tier.ToString());
        }

        private static int IndexOf(Tier tier)
        {
            var values = ProbabilityTable<Tier>.Outcomes;
            for (var i = 0; i < values.Count; i++)
                if (values[i] == tier) return i;
            return -1;
        }
    }
}
