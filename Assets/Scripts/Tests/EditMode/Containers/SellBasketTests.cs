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
    /// The Sell Basket (issue #32) - a fourth role played by the existing container type
    /// (the player stages a sale in it like a backpack), with a value preview and an
    /// explicit Confirm / Cancel. Preview is a pure sum of vendor sell value, full amount
    /// per stack; Confirm banks the summed value as one consolidated payout and clears the
    /// basket; Cancel returns every Package through the return-to-origin primitive - a
    /// stack price is its full amount, re-equip what was unequipped, back to the backpack
    /// what came from the backpack - with the wallet untouched.
    ///
    /// <para>Assertions are on container contents, item count and wallet value, never on
    /// how many refresh or content-changed events fired (ADR-0007 - the one seam is
    /// <c>InventorySystem.Containers</c>). Prior art: <see cref="VendorTransactionTests"/>
    /// and <see cref="ReturnToOriginTests"/>.</para>
    /// </summary>
    [TestFixture]
    public sealed class SellBasketTests
    {
        private const string SwordId = "test.sword";
        private const string HelmId = "test.helm";
        private const string CopperId = "test.copper";
        private const string SilverId = "test.silver";
        private const string GoldId = "test.gold";

        private static readonly float SwordValue = 6f * 35f; // 210
        private static readonly float HelmValue = 4f * 20f;  // 80

        private TestCatalog catalog;
        private FakeCurrencyMinter minter;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new TestCatalog()
                .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = CopperId, Category = ItemCategory.Currency, CurrencyType = CurrencyType.Copper, Footprint = ItemSize.OneByOne, BaseStackLimit = 999u })
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

        /// A Rare helm whose Armor affix values it at 4 * 20 = 80 base units.
        private static ItemInstance Helm() => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, 4f) });

        /// A 1x1 coin worth 5 base units each.
        private static ItemInstance Copper() => new(CopperId, ItemRarity.Common, 0, null);

        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height));
        private static CharacterEquipment Equipment(IStatReceiver stats = null) => new(new Vector2Int(14, 1), stats);
        private static Vector2Int SlotFor(EquipmentType type) => CharacterEquipment.GetTypeSpecificPositions(type).First();

        private Wallet NewWallet(int width = 4, int height = 4) =>
            new(new CharacterInventory(new Vector2Int(width, height)), minter);

        private void SeedCash(Wallet wallet, CurrencyType type, uint count)
        {
            var package = new Package(wallet.Container, minter.MintCurrency(type), count);
            _ = wallet.Container.TryAddToContainer(ref package);
        }

        private static uint WalletValue(Wallet wallet) => wallet.Balance.Total;

        private static bool Holds(AbstractDimensionalContainer container, ItemInstance instance) =>
            container.StoredPackages.Values.Any(package => ReferenceEquals(package.Item, instance));

        /// <summary>
        /// Stages the Package at <paramref name="originCell"/> of <paramref name="origin"/>
        /// into the basket at <paramref name="at"/>, recording the origin so a Cancel can
        /// return it - the exact primitive the basket slot display's drop runs (the item
        /// leaves the origin onto the drag, drops into the basket, the origin is remembered).
        /// Returns whether it fully staged.
        /// </summary>
        private static bool StageIntoBasket(SellBasket.Basket basket, AbstractDimensionalContainer origin,
            Vector2Int originCell, Vector2Int at)
        {
            Assert.That(origin.TryGetPackageAt(originCell, out var stored), Is.True, $"nothing stored at {originCell}");
            _ = origin.RemoveAtPosition(originCell, stored); // the drag pick-up lifts the item off the origin
            var inHand = stored;
            return SellBasket.Stage(basket, ref inHand, new SellBasket.Origin(origin, originCell), at);
        }

        // ── preview ─────────────────────────────────────────────────────────

        [Test]
        public void Preview_AnEmptyBasket_IsZero()
        {
            var basket = new SellBasket.Basket(Inventory());

            Assert.That(SellBasket.PreviewValue(basket), Is.Zero, "empty basket prices at zero");
        }

        [Test]
        public void Preview_SumsTheVendorSellValue_OverMixedPackages()
        {
            var basket = new SellBasket.Basket(Inventory(6, 1));
            var backpack = Inventory();

            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            _ = backpack.AddAtPosition(new Vector2Int(1, 0), new Package(backpack, Copper(), 3u));
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(1, 0), new Vector2Int(1, 0)), Is.True);

            Assert.That(SellBasket.PreviewValue(basket), Is.EqualTo(SwordValue + 3u * Currency.ironToCopper));
        }

        [Test]
        public void Preview_AStackOfCoins_IsPricedByItsFullAmount()
        {
            var basket = new SellBasket.Basket(Inventory(6, 1));
            var backpack = Inventory();
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Copper(), 3u));

            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);

            Assert.That(SellBasket.PreviewValue(basket), Is.EqualTo(3u * Currency.ironToCopper), "the whole 3-pile, not 1");
        }

        // ── confirm ─────────────────────────────────────────────────────────

        [Test]
        public void Confirm_BanksThePreviewedValue_AndClearsTheBasket()
        {
            var basket = new SellBasket.Basket(Inventory(4, 2));
            var wallet = NewWallet();
            var backpack = Inventory();

            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            _ = backpack.AddAtPosition(new Vector2Int(1, 0), new Package(backpack, Copper(), 3u));
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(1, 0), new Vector2Int(1, 0)), Is.True);

            var previewed = SellBasket.PreviewValue(basket);

            Assert.That(SellBasket.Confirm(basket, wallet), Is.True);
            Assert.That(WalletValue(wallet), Is.EqualTo(previewed), "coins banked equal the preview");
            Assert.That(basket.Container.StoredPackages, Is.Empty, "the basket is cleared");
        }

        [Test]
        public void Confirm_ConservesValueExactly_TheCoinsEqualThePreview()
        {
            var basket = new SellBasket.Basket(Inventory(4, 2));
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200 before
            var backpack = Inventory();
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);

            var previewed = SellBasket.PreviewValue(basket);

            Assert.That(SellBasket.Confirm(basket, wallet), Is.True);
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u + (uint)previewed), "the payout added exactly the preview");
        }

        // ── cancel ──────────────────────────────────────────────────────────

        [Test]
        public void Cancel_ReturnsEveryPackageToItsOrigin_WithTheWalletUntouched()
        {
            var basket = new SellBasket.Basket(Inventory(4, 2));
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200, must not move
            var backpack = Inventory();

            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            _ = backpack.AddAtPosition(new Vector2Int(1, 0), new Package(backpack, Copper(), 3u));
            var sword = backpack.StoredPackages[new Vector2Int(0, 0)].Item;
            var copper = backpack.StoredPackages[new Vector2Int(1, 0)].Item;
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(1, 0), new Vector2Int(1, 0)), Is.True);

            Assert.That(SellBasket.Cancel(basket, backpack).IsValid, Is.False, "everything returned home");
            Assert.That(basket.Container.StoredPackages, Is.Empty, "the basket is empty");
            Assert.That(backpack.StoredPackages[new Vector2Int(0, 0)].Item, Is.SameAs(sword), "the sword is back at its cell");
            Assert.That(backpack.StoredPackages[new Vector2Int(1, 0)].Item, Is.SameAs(copper), "the pile is back at its cell");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u), "the wallet is untouched");
        }

        [Test]
        public void Cancel_AnEquipmentOrigin_ReEquipsIt_AndReappliesTheAffix()
        {
            var basket = new SellBasket.Basket(Inventory(4, 2));
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var backpack = Inventory();

            var worn = new Package(backpack, Helm(), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            var instance = equipment.StoredPackages[slot].Item;
            stats.Added.Clear();
            Assert.That(StageIntoBasket(basket, equipment, slot, default), Is.True, "the worn helm was staged");

            Assert.That(SellBasket.Cancel(basket, backpack).IsValid, Is.False);
            Assert.That(basket.Container.StoredPackages, Is.Empty);
            Assert.That(equipment.StoredPackages[slot].Item, Is.SameAs(instance), "re-equipped at its slot");
            Assert.That(stats.Added.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }), "the affix is re-applied on cancel");
        }

        [Test]
        public void Cancel_WhenAnOriginCellIsNowOccupied_FallsBackToTheBackpack()
        {
            var basket = new SellBasket.Basket(Inventory(4, 2));
            var wallet = NewWallet();
            var backpack = Inventory();

            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            var sword = backpack.StoredPackages[new Vector2Int(0, 0)].Item;
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);

            // Something else takes the vacated cell before the cancel resolves.
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Helm(), 1u));

            Assert.That(SellBasket.Cancel(basket, backpack).IsValid, Is.False);
            Assert.That(basket.Container.StoredPackages, Is.Empty);
            Assert.That(backpack.StoredPackages[new Vector2Int(0, 0)].Item.DefinitionId, Is.EqualTo(HelmId), "the occupying helm is untouched");
            Assert.That(Holds(backpack, sword), Is.True, "the sword landed in the backpack as the fallback");
            Assert.That(WalletValue(wallet), Is.Zero);
        }

        [Test]
        public void Cancel_WhenAnItemFitsNeitherItsOriginNorTheBackpack_LeavesItOnTheCursor_AndDestroysNothing()
        {
            var basket = new SellBasket.Basket(Inventory(1, 1));
            var wallet = NewWallet();
            var origin = Inventory(1, 1);

            _ = origin.AddAtPosition(new Vector2Int(0, 0), new Package(origin, Sword(), 1u));
            Assert.That(StageIntoBasket(basket, origin, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);
            var staged = basket.Container.StoredPackages.Values.Single().Item;

            // Both the origin and the backpack now have their only cells taken before the cancel.
            _ = origin.AddAtPosition(new Vector2Int(0, 0), new Package(origin, Helm(), 1u));
            var backpack = Inventory(1, 1);
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Copper(), 1u));

            var leftover = SellBasket.Cancel(basket, backpack);

            Assert.That(leftover.IsValid, Is.True, "the unplaceable item comes back on the cursor, never destroyed");
            Assert.That(leftover.Item, Is.SameAs(staged));
            Assert.That(basket.Container.StoredPackages, Is.Empty, "it left the basket too (ReturnToOrigin hands it back to the cursor)");
            Assert.That(origin.StoredPackages[new Vector2Int(0, 0)].Item.DefinitionId, Is.EqualTo(HelmId));
            Assert.That(WalletValue(wallet), Is.Zero);
        }

        [Test]
        public void Cancel_AnEmptyBasket_DoesNothing()
        {
            var basket = new SellBasket.Basket(Inventory());
            var backpack = Inventory();
            var wallet = NewWallet();

            Assert.That(SellBasket.Cancel(basket, backpack).IsValid, Is.False);
            Assert.That(basket.Container.StoredPackages, Is.Empty);
            Assert.That(backpack.StoredPackages, Is.Empty);
            Assert.That(WalletValue(wallet), Is.Zero);
        }

        // ── the ledger ──────────────────────────────────────────────────────

        [Test]
        public void Staging_Records_EachPackagesOrigin_SoCancelCanReturnThem()
        {
            var basket = new SellBasket.Basket(Inventory(2, 1));
            var backpack = Inventory();
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            _ = backpack.AddAtPosition(new Vector2Int(1, 0), new Package(backpack, Copper(), 1u));

            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);
            Assert.That(StageIntoBasket(basket, backpack, new Vector2Int(1, 0), new Vector2Int(1, 0)), Is.True);

            Assert.That(basket.Origins.Count, Is.EqualTo(2), "one origin record per staged package");
            Assert.That(basket.Origins.Values.Select(o => o.Cell), Is.EquivalentTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }));
        }
    }
}