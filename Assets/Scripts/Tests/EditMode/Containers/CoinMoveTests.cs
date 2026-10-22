using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// A coin the player moves, at the container seam (coin-overflow spec, stories 7 and 9): one
    /// shift-clicked or dropped into the bag - the container behind the Wallet - leaves the bag's coins
    /// gold first, iron last; into any other container (the Stash, the Ground) it takes the first free
    /// cell. Because the bag keeps its coins in order, shift-clicking them across to the Stash one by
    /// one fills the Stash in order. Assertions are on container contents and balances, never on
    /// event counts (ADR-0007).
    /// </summary>
    [TestFixture]
    public sealed class CoinMoveTests
    {
        private const string IronId = "test.iron";
        private const string CopperId = "test.copper";
        private const string SilverId = "test.silver";
        private const string GoldId = "test.gold";
        private const string PotionId = "test.potion";

        private static readonly CurrencyType[] GoldFirst =
            { CurrencyType.Gold, CurrencyType.Silver, CurrencyType.Copper, CurrencyType.Iron };

        private static IItemCatalog catalog;
        private FakeCurrencyMinter minter;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new TestCatalog()
                .With(Coin(IronId, CurrencyType.Iron))
                .With(Coin(CopperId, CurrencyType.Copper))
                .With(Coin(SilverId, CurrencyType.Silver))
                .With(Coin(GoldId, CurrencyType.Gold))
                .With(new TestDefinition { Id = PotionId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Potion, Footprint = ItemSize.OneByOne, BaseStackLimit = 5u });

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
        public void ClearCatalog() => catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static CharacterInventory Container(int width = 4, int height = 4) => new(new Vector2Int(width, height), catalog);

        private Wallet BagWallet(int width = 4, int height = 4) => new(Container(width, height), minter);

        /// Lays <paramref name="count"/> coins in the first free cell, the way a hand-placed or stacked coin lands.
        private ItemInstance Put(AbstractDimensionalContainer container, CurrencyType type, uint count = 1u)
        {
            var coin = minter.MintCurrency(type);
            var package = new Package(container, coin, count);
            _ = container.TryAddToContainer(ref package);
            return coin;
        }

        private static CurrencyType TypeOf(Package package) => ItemView.Resolve(package.Item, catalog).Definition.CurrencyType;

        /// The denominations in the grid, one entry per cell, in the order the container fills its cells.
        private static CurrencyType[] InCellOrder(AbstractDimensionalContainer container) => container.StoredPackages
            .OrderBy(entry => entry.Key.x).ThenBy(entry => entry.Key.y)
            .Select(entry => TypeOf(entry.Value))
            .ToArray();

        private static uint Total(AbstractDimensionalContainer container, CurrencyType type) => (uint)container.StoredPackages.Values
            .Where(package => TypeOf(package) == type)
            .Sum(package => (int)package.Amount);

        /// <summary>
        /// The shift-click into another container, exactly as <c>AbstractSlotDisplay.TryQuickMove</c> runs its
        /// <c>MoveToContainer</c> arm: the Package leaves <paramref name="source"/> and lands in the first free cell
        /// of <paramref name="target"/>.
        /// </summary>
        private static void ShiftClickTo(AbstractDimensionalContainer source, AbstractDimensionalContainer target, Vector2Int position)
        {
            Assert.That(source.TryGetPackageAt(position, out var package), Is.True, $"nothing stored at {position}");

            using var transaction = new ItemTransaction(source, target).ReHomeThrough(target);

            _ = source.RemoveAtPosition(position, package);
            _ = transaction.TryReHomeToContainerOrHand(ref package, new PackageOrigin(source, position));

            transaction.Commit();
        }

        /// <summary>The player's placement (<see cref="ItemAcquisition"/>) without the MonoBehaviour.</summary>
        private sealed class FakePlayer : IItemReceiver
        {
            private readonly CharacterEquipment equipment;
            private readonly CharacterInventory inventory;

            public FakePlayer(CharacterInventory inventory)
            {
                equipment = new CharacterEquipment(new Vector2Int(14, 1), catalog, null);
                this.inventory = inventory;
            }

            public IReadOnlyList<AbstractDimensionalContainer> ReceivingContainers => new AbstractDimensionalContainer[] { equipment, inventory };

            public bool PickUpItem(ItemInstance item, uint amount)
            {
                var package = new Package(null, item, amount);
                return ItemAcquisition.TryPlace(ref package, equipment, inventory);
            }
        }

        // ── into the Stash or the Ground: the first free cell ───────────────

        [Test]
        public void ACoinShiftClickedIntoTheStash_TakesTheFirstFreeCell()
        {
            var bag = BagWallet();
            var stash = Container();
            _ = Put(stash, CurrencyType.Iron);
            _ = Put(bag.Container, CurrencyType.Gold);

            ShiftClickTo(bag.Container, stash, new Vector2Int(0, 0));

            Assert.That(InCellOrder(stash), Is.EqualTo(new[] { CurrencyType.Iron, CurrencyType.Gold }), "gold did not jump ahead of the iron");
            Assert.That(bag.Balance.Total, Is.Zero);
        }

        [Test]
        public void ShiftClickingTheBagsCoinsToTheStashInGridOrder_LeavesTheStashGoldFirst()
        {
            var bag = BagWallet();
            var stash = Container();
            _ = bag.Deposit(new Currency(iron: 2u, copper: 3u, silver: 1u, gold: 1u));

            foreach (var position in bag.Container.StoredPackages.Keys.OrderBy(p => p.x).ThenBy(p => p.y).ToList())
                ShiftClickTo(bag.Container, stash, position);

            Assert.That(InCellOrder(stash), Is.EqualTo(GoldFirst));
            Assert.That(bag.Container.StoredPackages, Is.Empty);
            Assert.That(Total(stash, CurrencyType.Gold), Is.EqualTo(1u));
            Assert.That(Total(stash, CurrencyType.Silver), Is.EqualTo(1u));
            Assert.That(Total(stash, CurrencyType.Copper), Is.EqualTo(3u));
            Assert.That(Total(stash, CurrencyType.Iron), Is.EqualTo(2u));
        }

        // ── into the bag: the bag's coins are re-laid ───────────────────────

        [Test]
        public void ACoinTakenFromTheStash_LeavesTheBagsCoinsGoldFirst()
        {
            var bag = BagWallet();
            var stash = Container();
            _ = Put(bag.Container, CurrencyType.Iron, 4u);
            _ = Put(bag.Container, CurrencyType.Silver);
            _ = Put(stash, CurrencyType.Gold);

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0), new FakePlayer((CharacterInventory)bag.Container), wallet: bag);

            Assert.That(moved, Is.True);
            Assert.That(stash.StoredPackages, Is.Empty);
            Assert.That(InCellOrder(bag.Container), Is.EqualTo(new[] { CurrencyType.Gold, CurrencyType.Silver, CurrencyType.Iron }));
            Assert.That(bag.Balance.Total, Is.EqualTo(1200u + 60u + 4u));
        }

        [Test]
        public void ACoinTakenFromTheStashIntoAFullBag_LosesNoCoin()
        {
            var bag = BagWallet(2, 1);
            var stash = Container();
            _ = Put(bag.Container, CurrencyType.Iron, 3u);
            _ = Put(bag.Container, CurrencyType.Copper, 1u);
            _ = Put(stash, CurrencyType.Copper, 2u); // stacks onto the copper pile: the bag has no free cell

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0), new FakePlayer((CharacterInventory)bag.Container), wallet: bag);

            Assert.That(moved, Is.True);
            Assert.That(stash.StoredPackages, Is.Empty);
            Assert.That(InCellOrder(bag.Container), Is.EqualTo(new[] { CurrencyType.Copper, CurrencyType.Iron }));
            Assert.That(bag.Balance.Copper, Is.EqualTo(3u));
            Assert.That(bag.Balance.Iron, Is.EqualTo(3u));
        }

        [Test]
        public void APotionTakenFromTheStash_LeavesTheBagsHandPlacedCoinsWhereTheyLie()
        {
            var bag = BagWallet();
            var stash = Container();
            _ = Put(bag.Container, CurrencyType.Iron);
            _ = Put(bag.Container, CurrencyType.Gold);
            var potion = new Package(stash, new ItemInstance(PotionId, ItemRarity.Common, 1, null), 1u);
            _ = stash.TryAddToContainer(ref potion);

            _ = PickUpTransaction.Run(stash, new Vector2Int(0, 0), new FakePlayer((CharacterInventory)bag.Container), wallet: bag);

            Assert.That(InCellOrder(bag.Container).Take(2), Is.EqualTo(new[] { CurrencyType.Iron, CurrencyType.Gold }));
        }

        [Test]
        public void ACoinPickedUpFromTheGround_LeavesTheBagsCoinsGoldFirst()
        {
            var bag = BagWallet();
            var lying = new GroundContainer(new Vector2Int(4, 4), catalog);
            var desk = new GroundDesk(lying, new FakePlayer((CharacterInventory)bag.Container), bag);
            _ = Put(bag.Container, CurrencyType.Iron, 4u);
            var gold = minter.MintCurrency(CurrencyType.Gold);
            Assert.That(desk.PlaceOnGround(new Package(lying, gold, 1u)), Is.True);

            var taken = desk.PickUpFromGround(gold);

            Assert.That(taken, Is.True);
            Assert.That(lying.StoredPackages, Is.Empty);
            Assert.That(InCellOrder(bag.Container), Is.EqualTo(new[] { CurrencyType.Gold, CurrencyType.Iron }));
            Assert.That(bag.Balance.Total, Is.EqualTo(1200u + 4u));
        }
    }
}
