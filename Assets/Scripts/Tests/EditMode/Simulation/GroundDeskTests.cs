using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The Town ground (issue #285): the player drops on it and picks up from it with no Run and so no
    /// loot flow. The topology is the game's - the bag is the container behind the Wallet - and the asserts
    /// read container contents and balances only, never event counts (ADR-0007).
    /// </summary>
    [TestFixture]
    public sealed class GroundDeskTests
    {
        private const string SwordId = "fake.sword";

        private static InMemoryItemCatalog catalog;

        private CharacterInventory bag;
        private Wallet wallet;
        private BagItemReceiver player;
        private GroundContainer ground;
        private GroundDesk desk;

        [SetUp]
        public void SetUp()
        {
            catalog = new InMemoryItemCatalog(
                new FakeItemDefinition { Id = SwordId },
                new FakeItemDefinition { Id = "fake.copper", Category = ItemCategory.Currency, CurrencyType = CurrencyType.Copper, BaseStackLimit = 999u });

            Build(bagSize: new Vector2Int(6, 6), groundSize: new Vector2Int(6, 6));
        }

        [TearDown]
        public void ClearCatalog() => catalog = null;

        private void Build(Vector2Int bagSize, Vector2Int groundSize)
        {
            bag = new CharacterInventory(bagSize, catalog);
            wallet = new Wallet(bag, new FakeCurrencyMinter(catalog));
            player = new BagItemReceiver(bag);
            ground = new GroundContainer(groundSize, catalog);
            desk = new GroundDesk(ground, player, wallet);
        }

        private static ItemInstance Sword(int seed = 1) => new(SwordId, ItemRarity.Common, seed, null);

        private static Package Pack(ItemInstance item) => new(null, item, 1u);

        private ItemInstance Carry(ItemInstance item)
        {
            var package = Pack(item);
            Assert.That(bag.TryAddToContainer(ref package), Is.True, "fixture: the bag has room");
            return item;
        }

        private static ItemInstance[] Lying(GroundContainer ground) =>
            ground.PackagesOldestFirst().Select(package => package.Item).ToArray();

        // ── shift-click on the bag: DropTransaction.Run ─────────────────────

        [Test]
        public void QuickMovingAnItemFromTheBag_LaysItOnTheGround()
        {
            var sword = Carry(Sword());

            var dropped = DropTransaction.Run(bag, bag.StoredPackages.Keys.Single(), desk);

            Assert.That(dropped, Is.True);
            Assert.That(bag.StoredPackages, Is.Empty);
            Assert.That(Lying(ground), Is.EqualTo(new[] { sword }));
        }

        // ── a drag released on the ground slot: DropTransaction.Place ───────

        [Test]
        public void AnItemReleasedOnTheGroundSlot_LiesThere()
        {
            var sword = Sword();

            var placed = DropTransaction.Place(Pack(sword), desk);

            Assert.That(placed, Is.True);
            Assert.That(Lying(ground), Is.EqualTo(new[] { sword }));
        }

        [Test]
        public void ALandingOnAFullGround_EvictsTheOldest()
        {
            Build(bagSize: new Vector2Int(6, 6), groundSize: new Vector2Int(1, 1));
            var older = Sword(1);
            var newer = Sword(2);
            _ = DropTransaction.Place(Pack(older), desk);

            _ = DropTransaction.Place(Pack(newer), desk);

            Assert.That(Lying(ground), Is.EqualTo(new[] { newer }));
        }

        // ── a click on the Ground Items List: PickUpFromGround ──────────────

        [Test]
        public void ClickingAnItem_HandsItToThePlayer_AndClearsItFromTheGround()
        {
            var sword = Sword();
            _ = desk.PlaceOnGround(Pack(sword));
            player.Equips = _ => true; // auto-equip on, slot empty

            var picked = desk.PickUpFromGround(sword);

            Assert.That(picked, Is.True);
            Assert.That(player.Equipped, Is.EqualTo(new[] { sword }));
            Assert.That(ground.StoredPackages, Is.Empty);
        }

        [Test]
        public void ClickingAnItem_WithAFullBag_LeavesItLying()
        {
            Build(bagSize: new Vector2Int(1, 1), groundSize: new Vector2Int(6, 6));
            var carried = Carry(Sword(1));
            var lying = Sword(2);
            _ = desk.PlaceOnGround(Pack(lying));

            var picked = desk.PickUpFromGround(lying);

            Assert.That(picked, Is.False);
            Assert.That(Lying(ground), Is.EqualTo(new[] { lying }));
            Assert.That(bag.StoredPackages.Values.Select(package => package.Item), Is.EqualTo(new[] { carried }));
        }

        // ── coins: the Wallet, not the entry point ──────────────────────────

        [Test]
        public void ClickingACoinStack_BanksItToTheWallet()
        {
            _ = desk.PlaceOnGround(new Package(null, wallet.MintCoin(CurrencyType.Copper), 3u));

            var picked = desk.PickUpFromGround(ground.PackagesOldestFirst().Single().Item);

            Assert.That(picked, Is.True);
            Assert.That(wallet.Balance.Copper, Is.EqualTo(3u));
            Assert.That(ground.StoredPackages, Is.Empty);
        }

        [Test]
        public void ClickingACoinStack_WithAFullBag_LeavesItLying()
        {
            Build(bagSize: new Vector2Int(1, 1), groundSize: new Vector2Int(6, 6));
            _ = Carry(Sword());
            var coins = wallet.MintCoin(CurrencyType.Copper);
            _ = desk.PlaceOnGround(new Package(null, coins, 3u));

            var picked = desk.PickUpFromGround(coins);

            Assert.That(picked, Is.False);
            Assert.That(wallet.Balance.Total, Is.Zero);
            Assert.That(ground.PackagesOldestFirst().Single().Amount, Is.EqualTo(3u));
        }

        [Test]
        public void ACoinStackPushedOffTheGround_IsDeleted()
        {
            Build(bagSize: new Vector2Int(6, 6), groundSize: new Vector2Int(1, 1));
            _ = desk.PlaceOnGround(new Package(null, wallet.MintCoin(CurrencyType.Copper), 3u));
            var sword = Sword();

            _ = desk.PlaceOnGround(Pack(sword));

            Assert.That(wallet.Balance.Total, Is.Zero);
            Assert.That(Lying(ground), Is.EqualTo(new[] { sword }));
        }
    }
}
