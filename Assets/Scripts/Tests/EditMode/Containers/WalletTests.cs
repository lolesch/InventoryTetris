using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The wallet module (foundational-rework Phase 3, issue #14): currency logic lifted off
    /// <see cref="CharacterInventory"/> onto a <see cref="Wallet"/> that is backed by a
    /// container but owns the balance. Assertions are on <see cref="Wallet.Balance"/> and on
    /// what renders in the grid, never on package identity or event counts.
    ///
    /// <para>Acceptance criteria under test: <see cref="Wallet.TryPay"/> spends smallest
    /// denomination first and nets to exactly the price; <see cref="Wallet.Consolidate"/> is
    /// value-preserving; a deposit then a pay of the same amount returns to the start
    /// balance; coins still render in grid cells.</para>
    /// </summary>
    [TestFixture]
    public sealed class WalletTests
    {
        private const string IronId = "test.iron";
        private const string CopperId = "test.copper";
        private const string SilverId = "test.silver";
        private const string GoldId = "test.gold";

        private FakeCurrencyMinter minter;

        [SetUp]
        public void SetCatalog()
        {
            var catalog = new TestCatalog()
                .With(Coin(IronId, CurrencyType.Iron))
                .With(Coin(CopperId, CurrencyType.Copper))
                .With(Coin(SilverId, CurrencyType.Silver))
                .With(Coin(GoldId, CurrencyType.Gold));

            ItemView.Catalog = catalog;
            minter = new FakeCurrencyMinter(catalog);

            static TestDefinition Coin(string id, CurrencyType type) => new()
            {
                Id = id,
                Category = ItemCategory.Currency,
                CurrencyType = type,
                Footprint = ItemSize.OneByOne,
                BaseStackLimit = 999u,
            };
        }

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private Wallet NewWallet(int width = 6, int height = 6) =>
            new(new CharacterInventory(new Vector2Int(width, height)), minter);

        private void Seed(Wallet wallet, CurrencyType type, uint count)
        {
            var package = new Package(wallet.Container, minter.MintCurrency(type), count);
            _ = wallet.Container.TryAddToContainer(ref package);
        }

        private static bool HoldsCurrency(Wallet wallet) => wallet.Container.StoredPackages.Values
            .Any(package => ItemView.Of(package.Item).Definition.Category == ItemCategory.Currency);

        // ── Balance ─────────────────────────────────────────────────────────

        [Test]
        public void Balance_SumsTheCoinsInTheBackingContainer()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Gold, 2u);
            Seed(wallet, CurrencyType.Silver, 3u);

            Assert.That(wallet.Balance.Gold, Is.EqualTo(2u));
            Assert.That(wallet.Balance.Silver, Is.EqualTo(3u));
            Assert.That(wallet.Balance.Total, Is.EqualTo(2u * Currency.ironToGold + 3u * Currency.ironToSilver));
        }

        [Test]
        public void Balance_OfAnEmptyWallet_IsZero()
        {
            Assert.That(NewWallet().Balance.Total, Is.Zero);
        }

        // ── CanAfford ───────────────────────────────────────────────────────

        [Test]
        public void CanAfford_IsTrueAtExactBalance_FalseOneOver()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Silver, 3u); // 180

            Assert.That(wallet.CanAfford(new Currency(180u)), Is.True);
            Assert.That(wallet.CanAfford(new Currency(181u)), Is.False);
        }

        // ── TryPay ──────────────────────────────────────────────────────────

        [Test]
        public void TryPay_SpendsTheSmallestDenominationFirst_LeavingLargerCoins()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Silver, 4u); // 240
            Seed(wallet, CurrencyType.Gold, 1u);   // 1200

            var paid = wallet.TryPay(new Currency(3u * Currency.ironToSilver)); // 180 = 3 silver

            Assert.That(paid, Is.True);
            Assert.That(wallet.Balance.Gold, Is.EqualTo(1u), "the gold was not broken");
            Assert.That(wallet.Balance.Total, Is.EqualTo(1200u + 60u));
        }

        [Test]
        public void TryPay_BreakingALargeCoin_NetsExactlyThePrice_BankingTheChange()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Gold, 1u); // 1200, and no smaller coins to pay 180 with

            var paid = wallet.TryPay(new Currency(180u));

            Assert.That(paid, Is.True);
            Assert.That(wallet.Balance.Gold, Is.EqualTo(0u), "the gold was broken");
            Assert.That(wallet.Balance.Total, Is.EqualTo(1200u - 180u), "change banked - the wallet lost exactly the price");
        }

        [Test]
        public void TryPay_FromLooseIronAcrossPackages_CoversASilverPrice()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Iron, 40u);
            Seed(wallet, CurrencyType.Copper, 30u); // 40 + 150 = 190 total

            var paid = wallet.TryPay(new Currency(Currency.ironToSilver)); // 60

            Assert.That(paid, Is.True);
            Assert.That(wallet.Balance.Total, Is.EqualTo(190u - 60u));
        }

        [Test]
        public void TryPay_WhenTheBalanceIsShort_ReturnsFalse_AndSpendsNothing()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Silver, 2u); // 120

            var paid = wallet.TryPay(new Currency(180u));

            Assert.That(paid, Is.False);
            Assert.That(wallet.Balance.Total, Is.EqualTo(120u));
        }

        [Test]
        public void TryPay_AZeroPrice_ReturnsTrue_AndSpendsNothing()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Iron, 5u);

            var paid = wallet.TryPay(new Currency(0u));

            Assert.That(paid, Is.True);
            Assert.That(wallet.Balance.Total, Is.EqualTo(5u));
        }

        [Test]
        public void TryPay_TheWholeBalance_LeavesNoCoinsInTheGrid()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Silver, 2u); // 120

            _ = wallet.TryPay(new Currency(120u));

            Assert.That(HoldsCurrency(wallet), Is.False);
            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        // ── Deposit ─────────────────────────────────────────────────────────

        [Test]
        public void Deposit_MintsCoinsThatRenderInGridCells()
        {
            var wallet = NewWallet();

            wallet.Deposit(new Currency(65u)); // 13 copper

            Assert.That(HoldsCurrency(wallet), Is.True, "the coins are packages in the grid");
            Assert.That(wallet.Balance.Total, Is.EqualTo(65u));
        }

        [Test]
        public void DepositThenPayTheSameAmount_ReturnsToTheStartingBalance()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Gold, 1u); // start at 1200

            wallet.Deposit(new Currency(333u));
            var paid = wallet.TryPay(new Currency(333u));

            Assert.That(paid, Is.True);
            Assert.That(wallet.Balance.Total, Is.EqualTo(1200u));
        }

        // ── Consolidate ─────────────────────────────────────────────────────

        [Test]
        public void Consolidate_PreservesTotalValue()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Iron, 8u);
            Seed(wallet, CurrencyType.Copper, 13u);
            var before = wallet.Balance.Total;

            wallet.Consolidate();

            Assert.That(wallet.Balance.Total, Is.EqualTo(before));
        }

        [Test]
        public void Consolidate_FoldsLooseCoinsIntoTheLargestDenominationThatFits()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Iron, 15u); // 15 iron -> 3 copper

            wallet.Consolidate();

            Assert.That(wallet.Balance.Iron, Is.EqualTo(0u));
            Assert.That(wallet.Balance.Copper, Is.EqualTo(3u));
            Assert.That(wallet.Balance.Total, Is.EqualTo(15u));
        }

        [Test]
        public void Consolidate_LeavesTheRemainderLoose()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Iron, 14u); // 14 iron -> 2 copper + 4 iron

            wallet.Consolidate();

            Assert.That(wallet.Balance.Copper, Is.EqualTo(2u));
            Assert.That(wallet.Balance.Iron, Is.EqualTo(4u));
        }

        [Test]
        public void Consolidate_AnAlreadyCanonicalWallet_DoesNothing()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Gold, 2u);
            var positionsBefore = wallet.Container.StoredPackages.Keys.OrderBy(p => p.x).ThenBy(p => p.y).ToList();

            wallet.Consolidate();

            Assert.That(wallet.Container.StoredPackages.Keys.OrderBy(p => p.x).ThenBy(p => p.y), Is.EqualTo(positionsBefore));
            Assert.That(wallet.Balance.Gold, Is.EqualTo(2u));
        }

        // ── OnBalanceChanged ────────────────────────────────────────────────

        [Test]
        public void OnBalanceChanged_FiresWithTheNewBalance_AfterADeposit()
        {
            var wallet = NewWallet();
            Currency? seen = null;
            wallet.OnBalanceChanged += balance => seen = balance;

            wallet.Deposit(new Currency(60u));

            Assert.That(seen.HasValue, Is.True);
            Assert.That(seen.Value.Total, Is.EqualTo(60u));
        }

        [Test]
        public void OnBalanceChanged_FiresAfterAPayment()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Silver, 3u); // 180
            var fired = 0;
            wallet.OnBalanceChanged += _ => fired++;

            _ = wallet.TryPay(new Currency(60u));

            Assert.That(fired, Is.GreaterThan(0));
            Assert.That(wallet.Balance.Total, Is.EqualTo(120u));
        }

        [Test]
        public void OnBalanceChanged_DoesNotFire_ForAValuePreservingConsolidate()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Iron, 15u);
            var fired = 0;
            wallet.OnBalanceChanged += _ => fired++;

            wallet.Consolidate();

            Assert.That(fired, Is.Zero, "the net balance never changed");
        }

        [Test]
        public void OnBalanceChanged_FiresWhenThePlayerDragsACoinOutOfTheGrid()
        {
            var wallet = NewWallet();
            Seed(wallet, CurrencyType.Gold, 2u);
            Currency? seen = null;
            wallet.OnBalanceChanged += balance => seen = balance;

            // A drag pulls one coin straight off the backing container.
            var position = wallet.Container.StoredPackages.Keys.First();
            var stored = wallet.Container.StoredPackages[position];
            _ = wallet.Container.RemoveAtPosition(position, new Package(wallet.Container, stored.Item, 1u));

            Assert.That(seen.HasValue, Is.True);
            Assert.That(seen.Value.Gold, Is.EqualTo(1u));
        }
    }
}
