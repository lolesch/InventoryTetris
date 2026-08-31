using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks ProbabilityTable&lt;T&gt;: weights normalize to a vector summing to 1 in
    /// enum-declaration order; a zero-weight member gets probability 0; an all-zero
    /// table produces no NaN. Fail-weight scaling and sampling are in their own fixtures.
    /// </summary>
    [TestFixture]
    public sealed class ProbabilityTableTests
    {
        private const float Tol = 1e-5f;

        // weights parallel to Coin: [None, Copper, Silver, Gold]; the None slot is ignored.
        private static ProbabilityTable<Coin> Table(float copper, float silver, float gold, float failWeight = 0f, float exponent = 1f) =>
            new(new[] { 0f, copper, silver, gold }, failWeight, exponent);

        [Test]
        public void Weights_NormalizeToAVectorSummingToOne()
        {
            var table = Table(copper: 1f, silver: 2f, gold: 1f);

            var p = table.Probabilities;

            Assert.That(p.Count, Is.EqualTo(4));
            Assert.That(p[0] + p[1] + p[2] + p[3], Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Weights_MapToTheirShareInEnumOrder()
        {
            var table = Table(copper: 1f, silver: 2f, gold: 1f); // total 4

            Assert.That(table.ProbabilityOf(Coin.Copper), Is.EqualTo(0.25f).Within(Tol));
            Assert.That(table.ProbabilityOf(Coin.Silver), Is.EqualTo(0.50f).Within(Tol));
            Assert.That(table.ProbabilityOf(Coin.Gold), Is.EqualTo(0.25f).Within(Tol));
        }

        [Test]
        public void ZeroWeightMember_GetsProbabilityZero()
        {
            var table = Table(copper: 3f, silver: 0f, gold: 1f);

            Assert.That(table.ProbabilityOf(Coin.Silver), Is.EqualTo(0f));
        }

        [Test]
        public void NegativeWeight_IsTreatedAsZero()
        {
            var table = Table(copper: 3f, silver: -5f, gold: 1f);

            Assert.That(table.ProbabilityOf(Coin.Silver), Is.EqualTo(0f));
            Assert.That(table.ProbabilityOf(Coin.Copper) + table.ProbabilityOf(Coin.Gold), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void AllZeroTable_DoesNotDivideByZeroOrProduceNaN()
        {
            var table = Table(copper: 0f, silver: 0f, gold: 0f);

            foreach (var v in table.Probabilities)
                Assert.That(float.IsNaN(v), Is.False, "no entry is NaN");

            // no success mass: the fail bucket owns the whole vector
            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void EnumWithNoDefaultMember_IsAllSuccess_NoNaN()
        {
            var table = new ProbabilityTable<NoFail>(new[] { 1f, 1f, 2f }, failWeight: 10f, failExponent: 1f);

            Assert.That(table.ProbabilityOf(NoFail.C), Is.EqualTo(0.5f).Within(Tol));
            foreach (var v in table.Probabilities)
                Assert.That(float.IsNaN(v), Is.False);
        }

        [Test]
        public void WrongWeightCount_Throws()
        {
            Assert.That(() => new ProbabilityTable<Coin>(new[] { 1f, 2f }, 0f, 1f),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ProbabilitiesView_CannotMutateTheTable()
        {
            var table = Table(copper: 1f, silver: 1f, gold: 1f);

            Assert.That(table.Probabilities, Is.Not.AssignableTo<float[]>(),
                "the view must not be the backing array");
        }
    }
}
