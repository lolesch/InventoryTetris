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
            Assert.That(ground.Placed, Is.EqualTo(new[] { sword }));
            Assert.That(bag.TryGetPackageAt(Vector2Int.zero, out _), Is.False, "the cell is empty");
        }

        [Test]
        public void Run_OfAStack_LaysOneGroundEntryPerUnit()
        {
            // The ground is one slot per item (GLOSSARY "Ground Items List"), so a stack of 3 is 3 slots -
            // dropping it must not quietly lose two thirds of it.
            var potion = Store(PotionId, 3u);

            var dropped = DropTransaction.Run(bag, Vector2Int.zero, ground);

            Assert.That(dropped, Is.True);
            Assert.That(ground.Placed, Is.EqualTo(new[] { potion, potion, potion }));
        }

        [Test]
        public void Run_OnAnEmptyCell_DropsNothing()
        {
            _ = Store(SwordId, 1u);

            var dropped = DropTransaction.Run(bag, new Vector2Int(3, 3), ground);

            Assert.That(dropped, Is.False);
            Assert.That(ground.Placed, Is.Empty);
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
