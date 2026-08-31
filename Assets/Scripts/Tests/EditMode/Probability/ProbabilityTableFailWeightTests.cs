using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks the fail-weight scaling: a designer fail probability p with exponent e
    /// yields an effective fail probability of exactly p^e; small fail probabilities
    /// such as 5% are representable on a small table (the regression for the old uint
    /// truncation); exponent 1 and exponent 0 behave.
    /// </summary>
    [TestFixture]
    public sealed class ProbabilityTableFailWeightTests
    {
        private const float Tol = 1e-4f;

        // Coin: [None, Copper, Silver, Gold] — success weights sum to `successSum`.
        private static ProbabilityTable<Coin> Table(float successSum, float failWeight, float exponent)
        {
            var each = successSum / 3f;
            return new ProbabilityTable<Coin>(new[] { 0f, each, each, each }, failWeight, exponent);
        }

        [Test]
        public void ExponentOne_GivesTheDesignerFailProbabilityDirectly()
        {
            // p = 10 / (10 + 90) = 0.10
            var table = Table(successSum: 90f, failWeight: 10f, exponent: 1f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.10f).Within(Tol));
        }

        [Test]
        public void ExponentTwo_SquaresTheDesignerFailProbability()
        {
            // p = 0.25 → p^2 = 0.0625
            var table = Table(successSum: 30f, failWeight: 10f, exponent: 2f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.0625f).Within(Tol));
        }

        [Test]
        public void FractionalExponent_IsHonoured()
        {
            // p = 0.5, e = 1.5 → 0.5^1.5 = 0.353553...
            var table = Table(successSum: 10f, failWeight: 10f, exponent: 1.5f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.35355f).Within(Tol));
        }

        [Test]
        public void SmallFailProbability_IsRepresentable_OnASmallTable()
        {
            // The old (uint) cast on a table whose success weights total 6 forced
            // a 10% ask down to 0. Here 5% on a small table must land near 5%.
            // p = f / (f + 6) = 0.05  →  f = 6 * 0.05 / 0.95 = 0.31578...
            var table = new ProbabilityTable<Coin>(new[] { 0f, 3f, 2f, 1f }, failWeight: 0.31578f, failExponent: 1f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.05f).Within(1e-3f));
            Assert.That(table.ProbabilityOf(Coin.None), Is.GreaterThan(0f), "not truncated to zero");
        }

        [Test]
        public void ZeroFailWeight_MeansNoFailBucket()
        {
            var table = Table(successSum: 100f, failWeight: 0f, exponent: 3f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0f));
            Assert.That(Sum(table), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void SuccessMembers_ShareTheRemainderAfterFail()
        {
            var table = Table(successSum: 90f, failWeight: 10f, exponent: 1f); // P(None) = 0.10

            // each success member is 30/90 of the remaining 0.90
            Assert.That(table.ProbabilityOf(Coin.Copper), Is.EqualTo(0.30f).Within(Tol));
            Assert.That(Sum(table), Is.EqualTo(1f).Within(Tol));
        }

        private static float Sum(ProbabilityTable<Coin> table)
        {
            var s = 0f;
            foreach (var v in table.Probabilities) s += v;
            return s;
        }
    }
}
