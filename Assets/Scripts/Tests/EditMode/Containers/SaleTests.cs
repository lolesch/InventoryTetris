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
    /// The Sale (issue #126), at the container seam: one transaction over the source, the Sold
    /// container and the Wallet. The Package leaves the Inventory or the Equipment, lands in the
    /// Sold container, and the Wallet is paid the sell value times the amount, once. A sale that
    /// cannot pay out, or pays 0, or whose Package carries a price, leaves every container and the
    /// Wallet exactly as they were. A full Sold container evicts its oldest sold Packages until
    /// the sale fits; the sale order is the only ledger.
    ///
    /// <para>Assertions are on container contents, the Wallet balance and the stat receiver,
    /// never on event counts (ADR-0007).</para>
    /// </summary>
    [TestFixture]
    public sealed class SaleTests
    {
        private const string SwordId = "test.sword";
        private const string HelmId = "test.helm";
        private const string GreatswordId = "test.greatsword";
        private const string CopperId = "test.copper";
        private const string SilverId = "test.silver";
        private const string GoldId = "test.gold";
        private const string IronId = "test.iron";

        private static readonly uint SwordValue = 6u * 35u; // 210
        private static readonly uint HelmValue = 4u * 20u;  // 80

        private static TestCatalog catalog;
        private FakeCurrencyMinter minter;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new TestCatalog()
                .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = GreatswordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.TwoByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = IronId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Iron, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
                .With(new TestDefinition { Id = CopperId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Copper, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
                .With(new TestDefinition { Id = SilverId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Silver, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
                .With(new TestDefinition { Id = GoldId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Gold, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u });

            minter = new FakeCurrencyMinter(catalog);
        }

        [TearDown]
        public void ClearCatalog() => catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        /// A Rare sword whose PhysicalDamage affix values it at 6 * 35 = 210 base units.
        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, 6f) });

        /// A Rare helm valued at armor * 20 base units; 4 -> 80, 3 -> 60 (exactly one silver coin).
        private static ItemInstance Helm(float armor = 4f) => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, armor) });

        /// A 2x1 Rare greatsword worth 6 * 35 = 210, wide enough to need two 1x1 cells.
        private static ItemInstance Greatsword() => new(GreatswordId, ItemRarity.Rare, 9, new[] { Affix(StatName.PhysicalDamage, 6f) });

        /// A helm with no affixes: it prices at 0 and is never a sale.
        private static ItemInstance Worthless() => new(HelmId, ItemRarity.Common, 0, null);

        /// A stackable 1x1 coin worth 5 base units each.
        private static ItemInstance Copper() => ItemInstance.Coin(CopperId, CurrencyType.Copper);

        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height), catalog);
        private static CharacterEquipment Equipment(IStatReceiver stats = null) => new(new Vector2Int(14, 1), catalog, stats);
        private static SoldContainer Sold(int width = 4, int height = 4) => new(new Vector2Int(width, height), catalog);

        private Wallet WalletOver(AbstractDimensionalContainer coins) => new(coins, minter);

        private void SeedCash(Wallet wallet, CurrencyType type, uint count)
        {
            var package = new Package(wallet.Container, minter.MintCurrency(type), count);
            _ = wallet.Container.TryAddToContainer(ref package);
        }

        private static Vector2Int Put(AbstractDimensionalContainer container, ItemInstance item, uint amount = 1u)
        {
            var package = new Package(container, item, amount);
            _ = container.TryAddToContainer(ref package);
            return container.StoredPackages.First(entry => ReferenceEquals(entry.Value.Item, item)).Key;
        }

        private static bool Holds(AbstractDimensionalContainer container, ItemInstance instance) =>
            container.StoredPackages.Values.Any(package => ReferenceEquals(package.Item, instance));

        private static uint CountOf(AbstractDimensionalContainer container, ItemInstance instance) =>
            (uint)container.StoredPackages.Values.Where(package => ReferenceEquals(package.Item, instance)).Sum(package => (int)package.Amount);

        // ── from the Inventory ──────────────────────────────────────────────

        [Test]
        public void SellFromTheInventory_MovesThePackageToSold_AndBanksTheSellValueOnce()
        {
            var inventory = Inventory();
            var wallet = WalletOver(inventory); // the player's coins sit in the Inventory the sale takes from
            var sold = Sold();
            var sword = Sword();
            var cell = Put(inventory, sword);

            Assert.That(Sale.TrySell(sold, wallet, inventory, cell), Is.True);

            Assert.That(Holds(inventory, sword), Is.False, "the sword left the Inventory");
            Assert.That(Holds(sold, sword), Is.True, "and sits in the Sold container");
            Assert.That(wallet.Balance.Total, Is.EqualTo(SwordValue), "the Wallet was paid the sell value, once");
        }

        [Test]
        public void SellFromTheInventory_IsExactlyOneSale_NoPackageIsInTwoPlaces()
        {
            var inventory = Inventory();
            var wallet = WalletOver(inventory);
            var sold = Sold();
            var sword = Sword();
            var helm = Helm();
            _ = Put(inventory, sword);
            var helmCell = Put(inventory, helm);

            Assert.That(Sale.TrySell(sold, wallet, inventory, helmCell), Is.True);

            Assert.That(Holds(inventory, sword), Is.True, "the other item was not touched");
            Assert.That(Holds(inventory, helm), Is.False);
            Assert.That(sold.StoredPackages.Count, Is.EqualTo(1));
            Assert.That(wallet.Balance.Total, Is.EqualTo(HelmValue));
        }

        // ── from the Equipment ──────────────────────────────────────────────

        [Test]
        public void SellFromTheEquipment_LiftsTheAffixesOnCommit_AndBanksThePayout()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var helm = Helm();

            var worn = new Package(null, helm, 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            stats.Added.Clear();

            Assert.That(Sale.TrySell(sold, wallet, equipment, slot), Is.True);

            Assert.That(equipment.StoredPackages, Is.Empty, "the slot was vacated");
            Assert.That(Holds(sold, helm), Is.True, "the helm is in the Sold container");
            Assert.That(stats.Removed.Select(r => r.Stat), Is.EquivalentTo(new[] { StatName.Armor }), "the worn affix was lifted");
            Assert.That(wallet.Balance.Total, Is.EqualTo(HelmValue));
        }

        [Test]
        public void ARefusedSaleFromTheEquipment_LiftsNothing()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var wallet = WalletOver(Inventory(1, 1));
            SeedCash(wallet, CurrencyType.Gold, 1u); // the wallet is full: an 80 payout cannot land
            var sold = Sold();
            var helm = Helm();

            var worn = new Package(null, helm, 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            stats.Added.Clear();

            Assert.That(Sale.TrySell(sold, wallet, equipment, slot), Is.False);

            Assert.That(Holds(equipment, helm), Is.True, "still worn");
            Assert.That(stats.Removed, Is.Empty, "no affix was lifted by a sale that never happened");
        }

        // ── the payout must fit, else nothing happens ──────────────────────

        [Test]
        public void APayoutThatDoesNotFit_LeavesEveryContainerAndTheWalletUntouched()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory(1, 1));
            SeedCash(wallet, CurrencyType.Gold, 1u); // no free cell, no copper/silver pile to merge into
            var sold = Sold();
            var helm = Helm();
            var cell = Put(source, helm);

            Assert.That(Sale.TrySell(sold, wallet, source, cell), Is.False);

            Assert.That(Holds(source, helm), Is.True, "the helm stayed in its source");
            Assert.That(sold.StoredPackages, Is.Empty, "nothing reached the Sold container");
            Assert.That(wallet.Balance.Total, Is.EqualTo(Currency.ironToGold), "no coin was dropped or paid");
        }

        [Test]
        public void ThePayoutMayUseTheSpaceTheSaleFrees()
        {
            // One cell, holding the very item being sold: the payout (one silver) fits only
            // because the sale empties that cell first.
            var inventory = Inventory(1, 1);
            var wallet = WalletOver(inventory);
            var sold = Sold();
            var helm = Helm(armor: 3f); // 60 base units = one silver
            var cell = Put(inventory, helm);

            Assert.That(Sale.TrySell(sold, wallet, inventory, cell), Is.True);

            Assert.That(Holds(sold, helm), Is.True);
            Assert.That(wallet.Balance.Silver, Is.EqualTo(1u));
            Assert.That(wallet.Balance.Total, Is.EqualTo(Currency.ironToSilver));
        }

        [Test]
        public void ASaleThatPaysZero_LeavesEveryContainerAndTheWalletUntouched()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var worthless = Worthless();
            var cell = Put(source, worthless);

            Assert.That(Sale.TrySell(sold, wallet, source, cell), Is.False);

            Assert.That(Holds(source, worthless), Is.True);
            Assert.That(sold.StoredPackages, Is.Empty);
            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        // ── eviction: oldest first, the sale order is the only ledger ──────

        [Test]
        public void AFullSoldContainer_EvictsTheOldestSoldPackage_ToTakeTheNewSale()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold(2, 1);
            var first = Sword();
            var second = Helm();
            var third = Helm(armor: 5f);
            _ = Sale.TrySell(sold, wallet, source, Put(source, first));
            _ = Sale.TrySell(sold, wallet, source, Put(source, second));

            Assert.That(Sale.TrySell(sold, wallet, source, Put(source, third)), Is.True,
                "a sale always succeeds when it can pay out, even into a full Sold container");

            Assert.That(Holds(sold, first), Is.False, "the oldest fell off");
            Assert.That(Holds(sold, second), Is.True);
            Assert.That(Holds(sold, third), Is.True);
            Assert.That(sold.StoredPackages.Count, Is.EqualTo(2));
        }

        [Test]
        public void ASaleNeedingMoreRoom_EvictsMoreThanOnePackage_OldestFirst()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold(3, 1);
            var oldest = Sword();
            var middle = Helm();
            var newest = Helm(armor: 5f);
            _ = Sale.TrySell(sold, wallet, source, Put(source, oldest));
            _ = Sale.TrySell(sold, wallet, source, Put(source, middle));
            _ = Sale.TrySell(sold, wallet, source, Put(source, newest));
            var wide = Greatsword();

            Assert.That(Sale.TrySell(sold, wallet, source, Put(source, wide)), Is.True);

            Assert.That(Holds(sold, wide), Is.True);
            Assert.That(Holds(sold, oldest), Is.False, "the oldest went first");
            Assert.That(Holds(sold, middle), Is.False, "and the next, until the 2x1 fit");
            Assert.That(Holds(sold, newest), Is.True, "the newest survived: three cells, a wide item and one single");
        }

        [Test]
        public void ASaleThatCanNeverFit_LeavesEveryContainerAndTheWalletUntouched()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold(1, 1);
            var resident = Helm();
            _ = Sale.TrySell(sold, wallet, source, Put(source, resident));
            var wide = Greatsword();
            var cell = Put(source, wide);
            var balance = wallet.Balance.Total;

            Assert.That(Sale.TrySell(sold, wallet, source, cell), Is.False, "2x1 cannot fit a 1x1 Sold container");

            Assert.That(Holds(source, wide), Is.True, "the greatsword stayed");
            Assert.That(Holds(sold, resident), Is.True, "and nothing was evicted for a sale that could not happen");
            Assert.That(wallet.Balance.Total, Is.EqualTo(balance));
        }

        // ── currency is not for sale ───────────────────────────────────────

        [Test]
        public void Currency_IsNeverSold_ByShiftClick()
        {
            var inventory = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var coins = Copper();
            var cell = Put(inventory, coins, 5u);

            Assert.That(Sale.TrySell(sold, wallet, inventory, cell), Is.False);

            Assert.That(CountOf(inventory, coins), Is.EqualTo(5u), "the coins stayed");
            Assert.That(sold.StoredPackages, Is.Empty);
            Assert.That(wallet.Balance.Total, Is.Zero, "nothing was paid for money");
        }

        [Test]
        public void Currency_IsNeverSold_ByDrop()
        {
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var held = new Package(null, Copper(), 5u);

            Assert.That(Sale.CanSellHeld(sold, wallet, held), Is.False, "so the drop target shows the forbidden tint");
            Assert.That(Sale.TrySellHeld(sold, wallet, held), Is.False);

            Assert.That(sold.StoredPackages, Is.Empty);
            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        // ── the Sold container stays old-to-young: compact, then add ───────

        private static Vector2Int CellOf(AbstractDimensionalContainer container, ItemInstance item) =>
            container.StoredPackages.First(entry => ReferenceEquals(entry.Value.Item, item)).Key;

        [Test]
        public void ASale_LandsAfterTheOlderOnes_NotInAGapAMissingItemLeft()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold(5, 1);
            var a = Sword();
            var b = Helm();
            var c = Helm(armor: 5f);
            var d = Helm(armor: 6f);
            _ = Sale.TrySell(sold, wallet, source, Put(source, a));
            _ = Sale.TrySell(sold, wallet, source, Put(source, b));
            _ = Sale.TrySell(sold, wallet, source, Put(source, c));
            var bCell = CellOf(sold, b);
            _ = sold.RemoveAtPosition(bCell, sold.StoredPackages[bCell]); // bought back: a gap in the middle

            Assert.That(Sale.TrySell(sold, wallet, source, Put(source, d)), Is.True);

            var cellA = CellOf(sold, a);
            var cellC = CellOf(sold, c);
            var cellD = CellOf(sold, d);
            Assert.That(cellA.x, Is.LessThan(cellC.x));
            Assert.That(cellC.x, Is.LessThan(cellD.x), "the newest is last, not in the hole b left");
            Assert.That(cellD.x - cellA.x, Is.EqualTo(2), "and the three sit together with no gap");
        }

        [Test]
        public void AfterCompacting_TheOldestIsStillTheOneThatFallsOff()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold(3, 1);
            var a = Sword();
            var b = Helm();
            var c = Helm(armor: 5f);
            var d = Helm(armor: 6f);
            _ = Sale.TrySell(sold, wallet, source, Put(source, a));
            _ = Sale.TrySell(sold, wallet, source, Put(source, b));
            _ = Sale.TrySell(sold, wallet, source, Put(source, c));
            var bCell = CellOf(sold, b);
            _ = sold.RemoveAtPosition(bCell, sold.StoredPackages[bCell]);
            _ = Sale.TrySell(sold, wallet, source, Put(source, d)); // fills the grid again: a, c, d

            var e = Helm(armor: 7f);
            Assert.That(Sale.TrySell(sold, wallet, source, Put(source, e)), Is.True);

            Assert.That(Holds(sold, a), Is.False, "a was the oldest");
            Assert.That(Holds(sold, c) && Holds(sold, d) && Holds(sold, e), Is.True);
        }

        [Test]
        public void ARefusedSale_DoesNotReshuffleTheSoldContainer()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold(5, 1);
            var a = Sword();
            var b = Helm();
            var c = Helm(armor: 5f);
            _ = Sale.TrySell(sold, wallet, source, Put(source, a));
            _ = Sale.TrySell(sold, wallet, source, Put(source, b));
            _ = Sale.TrySell(sold, wallet, source, Put(source, c));
            var bCell = CellOf(sold, b);
            _ = sold.RemoveAtPosition(bCell, sold.StoredPackages[bCell]);
            var cellC = CellOf(sold, c);

            Assert.That(Sale.TrySell(sold, wallet, source, Put(source, Worthless())), Is.False);

            Assert.That(CellOf(sold, c), Is.EqualTo(cellC), "a sale that pays nothing moved nothing, the gap included");
        }

        // ── a Package that carries a price is never a sale ─────────────────

        [Test]
        public void APackageCarryingAPrice_IsNeverASale()
        {
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var held = new Package(null, Sword(), 1u); // lifted off a shelf: a purchase in progress

            Assert.That(Sale.TrySellHeld(sold, wallet, held, carriedPrice: 315f), Is.False);

            Assert.That(sold.StoredPackages, Is.Empty);
            Assert.That(wallet.Balance.Total, Is.Zero, "nobody is paid for something not yet owned");
        }

        [Test]
        public void AHeldPackageWithNoPrice_SellsLikeAnyOther()
        {
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var sword = Sword();

            Assert.That(Sale.TrySellHeld(sold, wallet, new Package(null, sword, 1u)), Is.True);

            Assert.That(Holds(sold, sword), Is.True);
            Assert.That(wallet.Balance.Total, Is.EqualTo(SwordValue));
        }

        // ── the drop sale: the forbidden tint asks before release, the drop does it ──

        [Test]
        public void CanSellHeld_IsTrue_ForAPlayerPackageThatPaysAndFits()
        {
            var wallet = WalletOver(Inventory());

            Assert.That(Sale.CanSellHeld(Sold(), wallet, new Package(null, Sword(), 1u)), Is.True);
        }

        [Test]
        public void CanSellHeld_IsFalse_ForAPurchaseInProgress()
        {
            var wallet = WalletOver(Inventory());

            Assert.That(Sale.CanSellHeld(Sold(), wallet, new Package(null, Sword(), 1u), carriedPrice: 315f), Is.False);
        }

        [Test]
        public void CanSellHeld_IsFalse_WhenThePayoutIsZero()
        {
            var wallet = WalletOver(Inventory());

            Assert.That(Sale.CanSellHeld(Sold(), wallet, new Package(null, Worthless(), 1u)), Is.False);
        }

        [Test]
        public void CanSellHeld_IsFalse_WhenThePayoutWouldNotFit()
        {
            var wallet = WalletOver(Inventory(1, 1));
            SeedCash(wallet, CurrencyType.Gold, 1u); // no free cell, no copper/silver pile to merge into

            Assert.That(Sale.CanSellHeld(Sold(), wallet, new Package(null, Helm(), 1u)), Is.False);
        }

        [Test]
        public void CanSellHeld_IsFalse_ForNothingInHand_OrNoSoldContainer()
        {
            var wallet = WalletOver(Inventory());

            Assert.That(Sale.CanSellHeld(Sold(), wallet, default), Is.False);
            Assert.That(Sale.CanSellHeld(null, wallet, new Package(null, Sword(), 1u)), Is.False);
        }

        [Test]
        public void CanSellHeld_AgreesWithTrySellHeld()
        {
            var sword = new Package(null, Sword(), 1u);
            var worthless = new Package(null, Worthless(), 1u);

            foreach (var held in new[] { sword, worthless })
            {
                var wallet = WalletOver(Inventory());
                var sold = Sold();
                var asked = Sale.CanSellHeld(sold, wallet, held);

                Assert.That(Sale.TrySellHeld(sold, wallet, held), Is.EqualTo(asked));
            }
        }

        // ── guards ─────────────────────────────────────────────────────────

        [Test]
        public void SellingFromAnEmptyCell_DoesNothing()
        {
            var source = Inventory();
            var wallet = WalletOver(Inventory());
            var sold = Sold();

            Assert.That(Sale.TrySell(sold, wallet, source, new Vector2Int(2, 2)), Is.False);

            Assert.That(sold.StoredPackages, Is.Empty);
            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        [Test]
        public void SellingOutOfTheSoldContainerItself_DoesNothing()
        {
            var wallet = WalletOver(Inventory());
            var sold = Sold();
            var sword = Sword();
            var cell = Put(sold, sword);

            Assert.That(Sale.TrySell(sold, wallet, sold, cell), Is.False, "a bought-back item is a Buy, not a sale");

            Assert.That(Holds(sold, sword), Is.True);
            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        // ── the Sold container buys back like a Supply ─────────────────────

        [Test]
        public void ASoldPackage_IsBoughtBackAtTheSupplyPrice_SellValueTimesTheMarkup()
        {
            var inventory = Inventory();
            var wallet = WalletOver(inventory);
            var sold = Sold();
            var sword = Sword();
            _ = Sale.TrySell(sold, wallet, inventory, Put(inventory, sword));
            var cell = sold.StoredPackages.Keys.Single();
            var price = VendorTransaction.BuyPrice(sword, catalog);

            Assert.That(price, Is.EqualTo(SwordValue * VendorTransaction.Markup));
            Assert.That(VendorTransaction.CanAffordBuy(wallet, price), Is.False, "210 in the Wallet is short of 315");

            SeedCash(wallet, CurrencyType.Silver, 2u); // 120 more: 330

            Assert.That(VendorTransaction.Buy(sold, cell, sold.StoredPackages[cell], wallet, price), Is.True);

            Assert.That(Holds(inventory, sword), Is.True, "the sword is back with its affixes");
            Assert.That(sold.StoredPackages, Is.Empty);
            Assert.That(wallet.Balance.Total, Is.EqualTo(330u - (uint)price));
        }
    }
}
