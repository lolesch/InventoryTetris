using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Locks in the iron-based ladder (5 / 12 / 20) and Currency.TryGetPayment:
    /// spend smallest denominations first, make change on overpay, refuse when the
    /// wallet total is short, free when zero.
    /// </summary>
    [TestFixture]
    public sealed class CurrencyTests
    {
        // Currency(iron, copper, silver, gold). Denomination values: 1 / 5 / 60 / 1200.
        private static Currency Coins(uint iron = 0, uint copper = 0, uint silver = 0, uint gold = 0) =>
            new(iron, copper, silver, gold);

        private static void AssertCoins(Currency actual, uint iron, uint copper, uint silver, uint gold)
        {
            Assert.That(actual.Iron, Is.EqualTo(iron), "iron");
            Assert.That(actual.Copper, Is.EqualTo(copper), "copper");
            Assert.That(actual.Silver, Is.EqualTo(silver), "silver");
            Assert.That(actual.Gold, Is.EqualTo(gold), "gold");
        }

        [Test]
        public void Ladder_IsIronBased_AtFiveTwelveTwenty()
        {
            Assert.That(Currency.ironToCopper, Is.EqualTo(5u), "iron -> copper");
            Assert.That(Currency.copperToSilver, Is.EqualTo(12u), "copper -> silver");
            Assert.That(Currency.silverToGold, Is.EqualTo(20u), "silver -> gold");
            Assert.That(Currency.ironToSilver, Is.EqualTo(60u), "iron -> silver");
            Assert.That(Currency.ironToGold, Is.EqualTo(1200u), "gold keeps its 1200");
        }

        [Test]
        public void Decompose_MaxSubGoldTotal_FillsEveryLowerDenomination()
        {
            var wallet = new Currency(1199u);

            Assert.That(wallet.Gold, Is.EqualTo(0u), "gold");
            Assert.That(wallet.Silver, Is.EqualTo(19u), "silver");
            Assert.That(wallet.Copper, Is.EqualTo(11u), "copper");
            Assert.That(wallet.Iron, Is.EqualTo(4u), "iron");
        }

        [Test]
        public void Decompose_RoundTripsThroughTotal()
        {
            for (var total = 0u; total < 2500u; total++)
                Assert.That(new Currency(total).Total, Is.EqualTo(total), $"round trip at {total}");
        }

        [Test]
        public void ExactPayment_TakesThePrice_NoChange()
        {
            var ok = Coins(silver: 3).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 3, 0);
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void Overpay_BreaksASingleGold_AndReturnsChange()
        {
            var ok = Coins(gold: 1).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 0, 1);
            AssertCoins(change, 0, 0, 17, 0); // 1200 - 180 = 1020 = 17 silver
        }

        [Test]
        public void SmallestFirst_SpendsSilver_LeavesGoldUntouched()
        {
            var ok = Coins(silver: 4, gold: 1).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 3, 0);
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void PartialBreak_TakesAllSilverThenOneGold_AndReturnsChange()
        {
            var ok = Coins(silver: 2, gold: 1).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 0, 0, 2, 1); // 120 + 1200 = 1320 paid
            AssertCoins(change, 0, 0, 19, 0);  // 1320 - 180 = 1140 = 19 silver
        }

        [Test]
        public void CannotAfford_ReturnsFalse_WithDefaultOuts()
        {
            var ok = Coins(silver: 2).TryGetPayment(Coins(silver: 3), out var toRemove, out var change);

            Assert.That(ok, Is.False);
            Assert.That(toRemove.Total, Is.EqualTo(0u));
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void FreeItem_ReturnsTrue_ChargesNothing()
        {
            var ok = Coins(iron: 5).TryGetPayment(new Currency(0u), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            Assert.That(toRemove.Total, Is.EqualTo(0u));
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void NonCanonicalWallet_LooseIronCoversASilverPrice()
        {
            // 150 loose iron (in practice two packages: a full 120-stack + 30)
            var ok = Coins(iron: 150).TryGetPayment(Coins(silver: 1), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 60, 0, 0, 0); // price total is 60
            Assert.That(change.Total, Is.EqualTo(0u));
        }
    }
}
