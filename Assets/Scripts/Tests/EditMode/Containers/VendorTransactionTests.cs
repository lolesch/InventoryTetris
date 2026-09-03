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
    /// The vendor rows of the movement matrix (issue #11): <see cref="VendorTransaction"/>
    /// routes selling and buying through <see cref="ItemTransaction"/> so the coin mint /
    /// payment only lands on commit. A sale banks exactly the sell value; a purchase pays
    /// the price exactly and places the item, or - with no inventory room - rolls back with
    /// the item still on the shelf and nothing charged. Assertions are on wallet value and
    /// container contents, never on how many refreshes fired.
    /// </summary>
    [TestFixture]
    public sealed class VendorTransactionTests
    {
        private const string SwordId = "test.sword";
        private const string CopperId = "test.copper";
        private const string IronId = "test.iron";
        private const string SilverId = "test.silver";
        private const string GoldId = "test.gold";

        private TestCatalog catalog;
        private FakeCurrencyMinter minter;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new TestCatalog()
                .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = CopperId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Copper, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
                .With(new TestDefinition { Id = IronId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Iron, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
                .With(new TestDefinition { Id = SilverId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Silver, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
                .With(new TestDefinition { Id = GoldId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Gold, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u });

            ItemView.Catalog = catalog;
            minter = new FakeCurrencyMinter(catalog);
        }

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        /// A Rare sword whose single PhysicalDamage affix values it at 6 * 35 = 210 base units.
        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, 6f) });

        private const float SwordSellValue = 6f * 35f;                       // 210
        private static readonly float SwordBuyPrice = SwordSellValue * VendorTransaction.Markup; // 315

        private CharacterInventory Wallet(int width = 4, int height = 4) => new(new Vector2Int(width, height), minter);

        private void SeedCash(CharacterInventory wallet, CurrencyType type, uint count)
        {
            var package = new Package(wallet, minter.MintCurrency(type), count);
            _ = wallet.TryAddToContainer(ref package);
        }

        /// The wallet's total spendable value in base units - the sum over its coin packages.
        private static uint WalletValue(CharacterInventory wallet)
        {
            var total = 0u;

            foreach (var package in wallet.StoredPackages.Values)
            {
                var view = ItemView.Of(package.Item);
                if (view.Definition.Category == ItemCategory.Currency)
                    total += (uint)view.SellValue * package.Amount;
            }

            return total;
        }

        private static bool Holds(AbstractDimensionalContainer container, ItemInstance instance) =>
            container.StoredPackages.Values.Any(package => ReferenceEquals(package.Item, instance));

        // ── Sell ────────────────────────────────────────────────────────────

        [Test]
        public void Sell_MintsCoinsWorthTheSellValue_IntoTheWallet()
        {
            var wallet = Wallet();

            VendorTransaction.Sell(new Package(null, Sword(), 1u), wallet);

            Assert.That(WalletValue(wallet), Is.EqualTo((uint)SwordSellValue));
        }

        [Test]
        public void Sell_AddsToTheCashAlreadyInTheWallet()
        {
            var wallet = Wallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200

            VendorTransaction.Sell(new Package(null, Sword(), 1u), wallet);

            Assert.That(WalletValue(wallet), Is.EqualTo(1200u + (uint)SwordSellValue));
        }

        [Test]
        public void Sell_PricesTheWholeStack()
        {
            var wallet = Wallet();
            // A five-copper pile: sell value is the denomination value (5) times the amount.
            var pile = new Package(null, minter.MintCurrency(CurrencyType.Copper), 5u);

            VendorTransaction.Sell(pile, wallet);

            Assert.That(WalletValue(wallet), Is.EqualTo(5u * (uint)Currency.ironToCopper));
        }

        [Test]
        public void Sell_AnInvalidPackage_BanksNothing()
        {
            var wallet = Wallet();

            VendorTransaction.Sell(default, wallet);

            Assert.That(WalletValue(wallet), Is.Zero);
        }

        // ── Buy ─────────────────────────────────────────────────────────────

        [Test]
        public void Buy_PaysThePriceExactly_AndPlacesTheItem()
        {
            var wallet = Wallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200
            var store = new CharacterInventory(new Vector2Int(4, 4));
            var onShelf = new Package(store, Sword(), 1u);
            _ = store.AddAtPosition(new Vector2Int(0, 0), onShelf);
            var instance = store.StoredPackages[new Vector2Int(0, 0)].Item;

            var bought = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(bought, Is.True);
            Assert.That(store.StoredPackages, Is.Empty, "the shelf no longer holds it");
            Assert.That(Holds(wallet, instance), Is.True, "the bought item is in the bag");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u - (uint)SwordBuyPrice), "the price was paid exactly");
        }

        [Test]
        public void Buy_WithNoInventoryRoom_RollsBack_AndChargesNothing()
        {
            var wallet = Wallet(1, 1);
            SeedCash(wallet, CurrencyType.Gold, 1u); // fills the wallet's only cell
            var store = new CharacterInventory(new Vector2Int(4, 4));
            var onShelf = new Package(store, Sword(), 1u);
            _ = store.AddAtPosition(new Vector2Int(0, 0), onShelf);
            var instance = store.StoredPackages[new Vector2Int(0, 0)].Item;

            var bought = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(bought, Is.False);
            Assert.That(Holds(store, instance), Is.True, "the item stayed on the shelf");
            Assert.That(Holds(wallet, instance), Is.False, "nothing landed in the full bag");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u), "no charge on a rolled-back buy");
        }

        [Test]
        public void Buy_WhenTheWalletCannotAfford_DoesNothing()
        {
            var wallet = Wallet();
            SeedCash(wallet, CurrencyType.Iron, 100u); // 100 < 315
            var store = new CharacterInventory(new Vector2Int(4, 4));
            var onShelf = new Package(store, Sword(), 1u);
            _ = store.AddAtPosition(new Vector2Int(0, 0), onShelf);
            var instance = store.StoredPackages[new Vector2Int(0, 0)].Item;

            var bought = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(bought, Is.False);
            Assert.That(Holds(store, instance), Is.True);
            Assert.That(WalletValue(wallet), Is.EqualTo(100u));
        }

        [Test]
        public void Buy_LeavesTheStoreAndWalletEnrollableAgain()
        {
            var wallet = Wallet();
            SeedCash(wallet, CurrencyType.Gold, 1u);
            var store = new CharacterInventory(new Vector2Int(4, 4));
            _ = store.AddAtPosition(new Vector2Int(0, 0), new Package(store, Sword(), 1u));

            _ = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(() => new ItemTransaction(store, wallet).Dispose(), Throws.Nothing);
        }

        // ── the markup rule ────────────────────────────────────────────────

        [Test]
        public void BuyPrice_IsTheSellValueTimesTheMarkup()
        {
            var sword = Sword();

            Assert.That(VendorTransaction.BuyPrice(sword), Is.EqualTo(SwordSellValue * VendorTransaction.Markup));
        }
    }
}
