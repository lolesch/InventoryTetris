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
    /// The Hero context's Quick Move to the ground (issue #63): a shift-click on a bag item lifts
    /// it out of its container and lays it on the Run's ground, where the Ground Items List shows
    /// it and a click can pick it back up. Real <see cref="CharacterInventory"/>; the ground is
    /// <see cref="RecordingLootGround"/>.
    /// </summary>
    [TestFixture]
    public sealed class DropTransactionTests
    {
        private const string SwordId = "fake.sword";
        private const string PotionId = "fake.potion";

        private InMemoryItemCatalog catalog;
        private CharacterInventory bag;
        private RecordingLootGround ground;

        [SetUp]
        public void SetUp()
        {
            catalog = new InMemoryItemCatalog(
                new FakeItemDefinition { Id = SwordId },
                new FakeItemDefinition { Id = PotionId, BaseStackLimit = 10u });
            bag = new CharacterInventory(new Vector2Int(4, 4), catalog);
            ground = new RecordingLootGround();
        }

        private ItemInstance Store(string definitionId, uint amount)
        {
            var item = new ItemInstance(definitionId, ItemRarity.Common, 1, null);
            var package = new Package(bag, item, amount);
            Assert.That(bag.TryAddToContainer(ref package), Is.True, "fixture: the item fits");
            return item;
        }

        [Test]
        public void Run_LiftsTheItemOutOfTheContainer_AndLaysItOnTheGround()
        {
            var sword = Store(SwordId, 1u);

            var dropped = DropTransaction.Run(bag, Vector2Int.zero, ground);

            Assert.That(dropped, Is.True);
            Assert.That(ground.Placed.Select(placed => placed.Item), Is.EqualTo(new[] { sword }));
            Assert.That(bag.TryGetPackageAt(Vector2Int.zero, out _), Is.False, "the cell is empty");
        }

        [Test]
        public void Run_OfAStack_LaysOneGroundPackage()
        {
            var potion = Store(PotionId, 3u);

            var dropped = DropTransaction.Run(bag, Vector2Int.zero, ground);

            Assert.That(dropped, Is.True);
            Assert.That(ground.Placed, Has.Count.EqualTo(1), "a stack of 3 is one package, not 3 entries");
            Assert.That(ground.Placed[0].Item, Is.SameAs(potion));
            Assert.That(ground.Placed[0].Amount, Is.EqualTo(3u));
        }

        [Test]
        public void Run_WhenTheGroundRefusesThePackage_LeavesTheItemWhereItWas()
        {
            _ = Store(PotionId, 3u);
            ground.Refuses = true;

            var dropped = DropTransaction.Run(bag, Vector2Int.zero, ground);

            Assert.That(dropped, Is.False);
            Assert.That(bag.TryGetPackageAt(Vector2Int.zero, out var stored), Is.True, "handed back");
            Assert.That(stored.Amount, Is.EqualTo(3u));
        }

        [Test]
        public void Run_OnAnEmptyCell_DropsNothing()
        {
            _ = Store(SwordId, 1u);

            var dropped = DropTransaction.Run(bag, new Vector2Int(3, 3), ground);

            Assert.That(dropped, Is.False);
            Assert.That(ground.Placed, Is.Empty);
        }

        // ── Place: a Package already in hand (the cursor dropped on the ground slot) ──

        [Test]
        public void Place_LaysOneGroundPackage_OfAPackageInHand()
        {
            var potion = new ItemInstance(PotionId, ItemRarity.Common, 1, null);

            var placed = DropTransaction.Place(new Package(null, potion, 3u), ground);

            Assert.That(placed, Is.True);
            Assert.That(ground.Placed, Has.Count.EqualTo(1));
            Assert.That(ground.Placed[0].Amount, Is.EqualTo(3u));
        }

        [Test]
        public void Place_WhenTheGroundRefuses_ReportsItSoTheCallerKeepsTheItem()
        {
            var sword = new ItemInstance(SwordId, ItemRarity.Common, 1, null);
            ground.Refuses = true;

            Assert.That(DropTransaction.Place(new Package(null, sword, 1u), ground), Is.False);
        }

        [Test]
        public void Place_OfAnEmptyHand_DropsNothing()
        {
            Assert.That(DropTransaction.Place(default, ground), Is.False);
            Assert.That(ground.Placed, Is.Empty);
        }

        [Test]
        public void Place_WithNoGround_ReportsItSoTheCallerKeepsTheItem()
        {
            var sword = new ItemInstance(SwordId, ItemRarity.Common, 1, null);

            Assert.That(DropTransaction.Place(new Package(null, sword, 1u), null), Is.False,
                "the ground slot sends the item back instead of deleting it");
        }

        [Test]
        public void Run_WithNoGround_LeavesTheItemWhereItWas()
        {
            _ = Store(SwordId, 1u);

            var dropped = DropTransaction.Run(bag, Vector2Int.zero, null);

            Assert.That(dropped, Is.False, "a Run in Town has no ground to drop on");
            Assert.That(bag.TryGetPackageAt(Vector2Int.zero, out var stored), Is.True);
            Assert.That(stored.IsValid, Is.True);
        }
    }
}
