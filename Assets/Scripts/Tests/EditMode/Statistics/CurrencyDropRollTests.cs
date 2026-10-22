using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Locks CurrencyDropRoll.Amount: flat map from a [0,1] roll to a whole amount
    /// in [min, max] inclusive, with the half-open top (roll == 1) folded back to max.
    /// </summary>
    [TestFixture]
    public sealed class CurrencyDropRollTests
    {
        [Test]
        public void RollOfZero_ReturnsMin() =>
            Assert.That(CurrencyDropRoll.Amount(10, 30, 0f), Is.EqualTo(10u));

        [Test]
        public void RollOfExactlyOne_ReturnsMax_NotMaxPlusOne() =>
            Assert.That(CurrencyDropRoll.Amount(10, 30, 1f), Is.EqualTo(30u));

        [Test]
        public void RollJustBelowOne_ReturnsMax() =>
            Assert.That(CurrencyDropRoll.Amount(10, 30, 0.9999f), Is.EqualTo(30u));

        [Test]
        public void Midpoint_LandsMidRange() =>
            // span 21, offset = floor(0.5 * 21) = 10
            Assert.That(CurrencyDropRoll.Amount(10, 30, 0.5f), Is.EqualTo(20u));

        [Test]
        public void DegenerateRange_AlwaysReturnsThatValue()
        {
            Assert.That(CurrencyDropRoll.Amount(1, 1, 0f), Is.EqualTo(1u));
            Assert.That(CurrencyDropRoll.Amount(1, 1, 0.5f), Is.EqualTo(1u));
            Assert.That(CurrencyDropRoll.Amount(1, 1, 1f), Is.EqualTo(1u));
        }

        [Test]
        public void EveryRoll_StaysWithinRange()
        {
            for (var i = 0; i <= 1000; i++)
                Assert.That(CurrencyDropRoll.Amount(4, 12, i / 1000f), Is.InRange(4u, 12u), $"roll {i / 1000f}");
        }
    }
}
