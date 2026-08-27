using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Locks in Currency.TryGetPayment: spend smallest denominations first, make
    /// change on overpay, refuse when the wallet total is short, free when zero.
    /// </summary>
    [TestFixture]
    public sealed class CurrencyTests
    {
        // Currency(copper, iron, silver, gold). Denomination values: 1 / 20 / 240 / 1200.
        private static Currency Coins(uint copper = 0, uint iron = 0, uint silver = 0, uint gold = 0) =>
            new(copper, iron, silver, gold);

        private static void AssertCoins(Currency actual, uint copper, uint iron, uint silver, uint gold)
        {
            Assert.That(actual.Copper, Is.EqualTo(copper), "copper");
            Assert.That(actual.Iron, Is.EqualTo(iron), "iron");
            Assert.That(actual.Silver, Is.EqualTo(silver), "silver");
            Assert.That(actual.Gold, Is.EqualTo(gold), "gold");
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
            AssertCoins(change, 0, 0, 2, 0); // 1200 - 720 = 480 = 2 silver
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
            AssertCoins(toRemove, 0, 0, 2, 1); // 480 + 1200 = 1680 paid
            AssertCoins(change, 0, 0, 4, 0);   // 1680 - 720 = 960 = 4 silver
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
            var ok = Coins(copper: 5).TryGetPayment(new Currency(0u), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            Assert.That(toRemove.Total, Is.EqualTo(0u));
            Assert.That(change.Total, Is.EqualTo(0u));
        }

        [Test]
        public void NonCanonicalWallet_LooseCopperCoversAnIronPrice()
        {
            // 25 loose copper (in practice two packages: a full 20-stack + 5)
            var ok = Coins(copper: 25).TryGetPayment(Coins(iron: 1), out var toRemove, out var change);

            Assert.That(ok, Is.True);
            AssertCoins(toRemove, 20, 0, 0, 0); // price total is 20
            Assert.That(change.Total, Is.EqualTo(0u));
        }
    }
}
