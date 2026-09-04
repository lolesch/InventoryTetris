using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="RunPenalty"/> — the two Death fractions (share of progress-to-next-level lost,
    /// share of currency banked this Run withdrawn). The exact numbers are unfrozen balance
    /// (spec <i>Out of Scope</i>), so they are injected; both must be a real fraction.
    /// </summary>
    [TestFixture]
    public sealed class RunPenaltyTests
    {
        [Test]
        public void None_CostsNothing()
        {
            Assert.That(RunPenalty.None.XpLossFraction, Is.EqualTo(0f));
            Assert.That(RunPenalty.None.CurrencyFeeFraction, Is.EqualTo(0f));
        }

        [Test]
        public void Default_EqualsNone()
        {
            RunPenalty fresh = default;

            Assert.That(fresh.XpLossFraction, Is.EqualTo(0f));
            Assert.That(fresh.CurrencyFeeFraction, Is.EqualTo(0f));
        }

        [Test]
        public void AFractionInRange_IsKept()
        {
            var penalty = new RunPenalty(xpLossFraction: 0.3f, currencyFeeFraction: 0.15f);

            Assert.That(penalty.XpLossFraction, Is.EqualTo(0.3f));
            Assert.That(penalty.CurrencyFeeFraction, Is.EqualTo(0.15f));
        }

        [TestCase(-0.01f, 0.5f)]
        [TestCase(0.5f, -0.01f)]
        [TestCase(1.01f, 0.5f)]
        [TestCase(0.5f, 1.01f)]
        public void AFractionOutside0To1_Throws(float xp, float fee)
        {
            Assert.That(() => new RunPenalty(xp, fee), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ANaNFraction_Throws()
        {
            // Every comparison against NaN is false, so a naive `< 0 || > 1` range check lets it
            // through — this pins that RunPenalty rejects it instead.
            Assert.That(() => new RunPenalty(float.NaN, 0.5f), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunPenalty(0.5f, float.NaN), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TheEndpoints_AreValid()
        {
            Assert.That(() => new RunPenalty(0f, 0f), Throws.Nothing);
            Assert.That(() => new RunPenalty(1f, 1f), Throws.Nothing);
        }
    }
}
