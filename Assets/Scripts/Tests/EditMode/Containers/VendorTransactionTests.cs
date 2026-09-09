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
        private const string TwoHandedSwordId = "test.twohanded.sword"; // a 2×2 purchase for the no-room tests
        private const string ArrowId = "test.arrow"; // a cheap restock filler for the fixed-price test
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
                .With(new TestDefinition { Id = TwoHandedSwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.TWOHANDEDWEAPONS, Footprint = ItemSize.TwoByTwo, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 20u })
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

        /// A 2×2 greatsword that cannot fit into a wallet whose every cell is already coin-filled.
        private static ItemInstance TwoHandedSword() => new(TwoHandedSwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, 6f) });

        private Wallet NewWallet(int width = 4, int height = 4) =>
            new(new CharacterInventory(new Vector2Int(width, height)), minter);

        private void SeedCash(Wallet wallet, CurrencyType type, uint count)
        {
            var package = new Package(wallet.Container, minter.MintCurrency(type), count);
            _ = wallet.Container.TryAddToContainer(ref package);
        }

        /// The wallet's total spendable value in base units.
        private static uint WalletValue(Wallet wallet) => wallet.Balance.Total;

        private static bool Holds(AbstractDimensionalContainer container, ItemInstance instance) =>
            container.StoredPackages.Values.Any(package => ReferenceEquals(package.Item, instance));

        // ── Sell ────────────────────────────────────────────────────────────

        [Test]
        public void Sell_MintsCoinsWorthTheSellValue_IntoTheWallet()
        {
            var wallet = NewWallet();

            VendorTransaction.Sell(new Package(null, Sword(), 1u), wallet);

            Assert.That(WalletValue(wallet), Is.EqualTo((uint)SwordSellValue));
        }

        [Test]
        public void Sell_AddsToTheCashAlreadyInTheWallet()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200

            VendorTransaction.Sell(new Package(null, Sword(), 1u), wallet);

            Assert.That(WalletValue(wallet), Is.EqualTo(1200u + (uint)SwordSellValue));
        }

        [Test]
        public void Sell_PricesTheWholeStack()
        {
            var wallet = NewWallet();
            // A five-copper pile: sell value is the denomination value (5) times the amount.
            var pile = new Package(null, minter.MintCurrency(CurrencyType.Copper), 5u);

            VendorTransaction.Sell(pile, wallet);

            Assert.That(WalletValue(wallet), Is.EqualTo(5u * (uint)Currency.ironToCopper));
        }

        [Test]
        public void Sell_AnInvalidPackage_BanksNothing()
        {
            var wallet = NewWallet();

            VendorTransaction.Sell(default, wallet);

            Assert.That(WalletValue(wallet), Is.Zero);
        }

        // ── Buy ─────────────────────────────────────────────────────────────

        [Test]
        public void Buy_PaysThePriceExactly_AndPlacesTheItem()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200
            var store = new CharacterInventory(new Vector2Int(4, 4));
            var onShelf = new Package(store, Sword(), 1u);
            _ = store.AddAtPosition(new Vector2Int(0, 0), onShelf);
            var instance = store.StoredPackages[new Vector2Int(0, 0)].Item;

            var bought = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(bought, Is.True);
            Assert.That(store.StoredPackages, Is.Empty, "the shelf no longer holds it");
            Assert.That(Holds(wallet.Container, instance), Is.True, "the bought item is in the bag");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u - (uint)SwordBuyPrice), "the price was paid exactly");
        }

        [Test]
        public void Buy_WithNoInventoryRoom_RollsBack_AndChargesNothing()
        {
            var wallet = NewWallet(1, 1);
            SeedCash(wallet, CurrencyType.Gold, 1u); // fills the wallet's only cell
            var store = new CharacterInventory(new Vector2Int(4, 4));
            var onShelf = new Package(store, Sword(), 1u);
            _ = store.AddAtPosition(new Vector2Int(0, 0), onShelf);
            var instance = store.StoredPackages[new Vector2Int(0, 0)].Item;

            var bought = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(bought, Is.False);
            Assert.That(Holds(store, instance), Is.True, "the item stayed on the shelf");
            Assert.That(Holds(wallet.Container, instance), Is.False, "nothing landed in the full bag");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u), "no charge on a rolled-back buy");
        }

        [Test]
        public void Buy_WhenTheWalletCannotAfford_DoesNothing()
        {
            var wallet = NewWallet();
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
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u);
            var store = new CharacterInventory(new Vector2Int(4, 4));
            _ = store.AddAtPosition(new Vector2Int(0, 0), new Package(store, Sword(), 1u));

            _ = VendorTransaction.Buy(store, new Vector2Int(0, 0),
                store.StoredPackages[new Vector2Int(0, 0)], wallet, SwordBuyPrice);

            Assert.That(() => new ItemTransaction(store, wallet.Container).Dispose(), Throws.Nothing);
        }

        // ── buy on drop (issue #31) ────────────────────────────────────────
        // A drag buy is a pick-up (uncharged) followed by a drop into a player container
        // whose placement transaction queues the payment. These fixture helpers drive the
        // exact primitives the slot displays run (issue #9 #10 #11): the pick-up removes the
        // package off the shelf; the drop places it in an ItemTransaction and queues the
        // payment as a commit-time effect; the return sends it back through ReturnToOrigin.

        /// Drag pick-up: the slot display removes the item off the shelf onto the cursor. Uncharged.
        private static Package shelfPickUp(CharacterInventory shelf, Vector2Int at)
        {
            Assert.That(shelf.TryGetPackageAt(at, out var stored), Is.True, $"nothing stored at {at}");
            _ = shelf.RemoveAtPosition(at, stored);
            return stored;
        }

        /// The drop, exactly as <c>InventorySlotDisplay</c> / <c>EquipmentSlotDisplay</c> run it
        /// (issue #31): an unaffordable or unplaceable purchase is rejected before the drop
        /// (item stays in hand, nothing charged); otherwise the placement runs in a
        /// transaction with the payment queued as a commit-time effect.
        private static bool DragDrop(AbstractDimensionalContainer bag, Vector2Int at, ref Package inHand,
            Wallet wallet, float price)
        {
            if (!bag.CanPlaceAt(at, ItemView.Of(inHand.Item).Dimensions))
                return false; // no room - nothing lands, nothing charged

            var cursor = new CursorHolder(null);
            using var transaction = new ItemTransaction(cursor, bag);

            if (!VendorTransaction.TryQueuePurchase(transaction, wallet, price))
                return false; // can't afford - dispose rolls back, nothing lands, nothing charged

            var displaced = bag.AddAtPosition(at, inHand);
            if (displaced.IsValid)
                _ = transaction.TryReHomeToHandOrContainer(ref displaced, bag, at);
            transaction.Commit();
            return true;
        }

        [Test]
        public void DragBuy_PickingUpOffTheShelf_ChargesNothing()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200
            var shelf = new CharacterInventory(new Vector2Int(4, 4));
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, Sword(), 1u));

            var inHand = shelfPickUp(shelf, new Vector2Int(0, 0));

            Assert.That(inHand.IsValid, Is.True, "the item is on the cursor");
            Assert.That(shelf.StoredPackages, Is.Empty, "it left the shelf");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u), "nothing was charged at pick-up");
        }

        [Test]
        public void DragBuy_DropIntoTheBag_ChargesExactlyThePickUpPrice_AndPlacesTheItem()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200
            var shelf = new CharacterInventory(new Vector2Int(4, 4));
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, Sword(), 1u));
            var instance = shelf.StoredPackages[new Vector2Int(0, 0)].Item;
            var inHand = shelfPickUp(shelf, new Vector2Int(0, 0));

            var price = VendorTransaction.BuyPrice(instance);
            var placed = DragDrop(wallet.Container, new Vector2Int(1, 1), ref inHand, wallet, price);

            Assert.That(placed, Is.True);
            Assert.That(Holds(wallet.Container, instance), Is.True, "the bought item landed in the bag");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u - (uint)SwordBuyPrice), "the price was paid exactly on landing");
        }

        [Test]
        public void DragBuy_WhereTheItemCannotFit_RejectsTheDrop_ChargesNothing_TheItemStaysInHand()
        {
            // A 2×2 wallet, every cell filled with a gold pile: the 2×2 greatsword has no
            // place to land, so the drop is refused exactly as the slot display refuses a
            // blocked footprint.
            var wallet = NewWallet(2, 2);
            for (var y = 0; y < 2; y++)
                for (var x = 0; x < 2; x++)
                    _ = wallet.Container.AddAtPosition(new Vector2Int(x, y),
                        new Package(wallet.Container, minter.MintCurrency(CurrencyType.Gold), 1u));
            var before = WalletValue(wallet); // 4 × 1200 = 4800

            var shelf = new CharacterInventory(new Vector2Int(4, 4));
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, TwoHandedSword(), 1u));
            var instance = shelf.StoredPackages[new Vector2Int(0, 0)].Item;
            var inHand = shelfPickUp(shelf, new Vector2Int(0, 0));

            var placed = DragDrop(wallet.Container, new Vector2Int(0, 0), ref inHand, wallet, VendorTransaction.BuyPrice(instance));

            Assert.That(placed, Is.False, "the drop could not land - the item stays in hand");
            Assert.That(Holds(wallet.Container, instance), Is.False, "nothing landed in the full bag");
            Assert.That(WalletValue(wallet), Is.EqualTo(before), "no charge - the buy never landed");
            Assert.That(inHand.IsValid, Is.True, "the item is still on the cursor, ready to return");
        }

        [Test]
        public void DragBuy_WhenTheWalletCannotAfford_RejectsTheDrop_AndChargesNothing()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Iron, 100u); // 100 < 315
            var shelf = new CharacterInventory(new Vector2Int(4, 4));
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, Sword(), 1u));
            var instance = shelf.StoredPackages[new Vector2Int(0, 0)].Item;
            var inHand = shelfPickUp(shelf, new Vector2Int(0, 0));

            var placed = DragDrop(wallet.Container, new Vector2Int(0, 0), ref inHand, wallet, VendorTransaction.BuyPrice(instance));

            Assert.That(placed, Is.False, "the drop was rejected before placing anything");
            Assert.That(Holds(wallet.Container, instance), Is.False, "nothing landed");
            Assert.That(WalletValue(wallet), Is.EqualTo(100u), "nothing was charged");
        }

        [Test]
        public void DragBuy_DroppingBackOnTheShelf_ReturnsItNoCharge()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u);
            var shelf = new CharacterInventory(new Vector2Int(4, 4));
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, Sword(), 1u));
            var instance = shelf.StoredPackages[new Vector2Int(0, 0)].Item;
            var inHand = shelfPickUp(shelf, new Vector2Int(0, 0));
            var backpack = wallet.Container;

            // Dropping back onto the shelf is a return to origin - the same ReturnToOrigin
            // the cancel and panel-close paths use.
            var leftOnCursor = ReturnToOrigin.Return(inHand, shelf, new Vector2Int(0, 0), backpack);

            Assert.That(leftOnCursor.IsValid, Is.False, "the package found its home on the shelf");
            Assert.That(Holds(shelf, instance), Is.True, "the item is back on the shelf");
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u), "dropping it back charged nothing");
        }

        [Test]
        public void DragBuy_PriceIsFixedAtPickUp_EvenAfterARestock()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u); // 1200
            var shelf = new CharacterInventory(new Vector2Int(4, 4));
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, Sword(), 1u));
            var instance = shelf.StoredPackages[new Vector2Int(0, 0)].Item;
            var priceAtPickUp = VendorTransaction.BuyPrice(instance); // 315
            var inHand = shelfPickUp(shelf, new Vector2Int(0, 0));

            // The store restocks while the drag is live - the shelf now sells a cheap arrow.
            _ = shelf.AddAtPosition(new Vector2Int(0, 0), new Package(shelf, new ItemInstance(ArrowId, ItemRarity.Common, 0, null), 1u));

            var placed = DragDrop(wallet.Container, new Vector2Int(1, 1), ref inHand, wallet, priceAtPickUp);

            Assert.That(placed, Is.True);
            Assert.That(WalletValue(wallet), Is.EqualTo(1200u - (uint)SwordBuyPrice),
                "the price paid is the pick-up price (315), never the restocked arrow's");
        }

        // ── the markup rule ────────────────────────────────────────────────

        [Test]
        public void BuyPrice_IsTheSellValueTimesTheMarkup()
        {
            var sword = Sword();

            Assert.That(VendorTransaction.BuyPrice(sword), Is.EqualTo(SwordSellValue * VendorTransaction.Markup));
        }

        // ── the check-then-queue protocol (TF#4) ───────────────────────────
        // TryQueuePurchase is the whole ordering rule: affordability is checked before the
        // charge is queued, so a queued payment can never fail at commit. These pin the
        // three answers a drop target needs - not a purchase, affordable, unaffordable.

        [Test]
        public void TryQueuePurchase_NoPrice_LetsAnOrdinaryDropThrough_AndChargesNothing()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u);
            var before = WalletValue(wallet);

            using var transaction = new ItemTransaction(wallet.Container);
            var mayProceed = VendorTransaction.TryQueuePurchase(transaction, wallet, null);
            transaction.Commit();

            Assert.That(mayProceed, Is.True, "an ordinary drop is not a purchase - nothing to check");
            Assert.That(WalletValue(wallet), Is.EqualTo(before), "nothing was queued, so nothing is charged");
        }

        [Test]
        public void TryQueuePurchase_Affordable_QueuesTheChargeForCommit()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Gold, 1u);
            var before = WalletValue(wallet);

            using var transaction = new ItemTransaction(wallet.Container);
            var mayProceed = VendorTransaction.TryQueuePurchase(transaction, wallet, 100f);

            Assert.That(mayProceed, Is.True);
            Assert.That(WalletValue(wallet), Is.EqualTo(before), "queued, not charged - the effect waits for commit");

            transaction.Commit();

            Assert.That(WalletValue(wallet), Is.EqualTo(before - 100u), "commit charges exactly the queued price");
        }

        [Test]
        public void TryQueuePurchase_Unaffordable_RefusesTheDrop_AndQueuesNothing()
        {
            var wallet = NewWallet();
            SeedCash(wallet, CurrencyType.Copper, 1u);
            var before = WalletValue(wallet);

            using var transaction = new ItemTransaction(wallet.Container);
            var mayProceed = VendorTransaction.TryQueuePurchase(transaction, wallet, before + 1_000f);
            transaction.Commit();

            Assert.That(mayProceed, Is.False, "the caller must abandon the drop");
            Assert.That(WalletValue(wallet), Is.EqualTo(before),
                "committing anyway charges nothing - the unaffordable price was never queued");
        }
    }
}
