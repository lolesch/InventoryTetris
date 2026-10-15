using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The ground owns its pick-up (issue #284): Town has a ground and no Run, so no loot flow exists
    /// here. Asserts container contents only; the ground's change events are not the contract (ADR-0007).
    /// </summary>
    [TestFixture]
    public sealed class GroundContainerPickUpTests
    {
        private const string SwordId = "test.sword";

        private static TestCatalog catalog;

        [SetUp]
        public void SetCatalog() =>
            catalog = new TestCatalog()
                .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u });

        [TearDown]
        public void ClearCatalog() => catalog = null;

        private static ItemInstance Sword(int seed = 1) => new(SwordId, ItemRarity.Common, seed, null);

        private static GroundContainer Ground() => new(new Vector2Int(4, 4), catalog);

        private static CharacterInventory Bag(int width = 4, int height = 4) => new(new Vector2Int(width, height), catalog);

        private static CharacterEquipment Equipment() => new(new Vector2Int(14, 1), catalog, null);

        private static void Lay(GroundContainer ground, ItemInstance item) =>
            Assert.That(ground.TryLand(new Package(null, item, 1u)), Is.True);

        /// <summary>The player's placement (<see cref="ItemAcquisition"/>) without the MonoBehaviour.</summary>
        private sealed class FakePlayer : IItemReceiver
        {
            private readonly CharacterEquipment equipment;
            private readonly CharacterInventory bag;

            public FakePlayer(CharacterEquipment equipment, CharacterInventory bag)
            {
                this.equipment = equipment;
                this.bag = bag;
            }

            public Exception Throws { get; set; }

            public IReadOnlyList<AbstractDimensionalContainer> ReceivingContainers => new AbstractDimensionalContainer[] { equipment, bag };

            public bool PickUpItem(ItemInstance item, uint amount)
            {
                var package = new Package(null, item, amount);
                var placed = ItemAcquisition.TryPlace(ref package, equipment, bag);
                return Throws == null ? placed : throw Throws;
            }
        }

        [Test]
        public void PickUp_WithAutoEquipOnAndAnEmptySlot_EquipsTheDrop_AndRemovesItFromTheGround()
        {
            var ground = Ground();
            var bag = Bag();
            var equipment = Equipment();
            equipment.autoEquip = true;
            var sword = Sword();
            Lay(ground, sword);

            var picked = ground.PickUp(sword, new FakePlayer(equipment, bag));

            Assert.That(picked, Is.True);
            Assert.That(ground.StoredPackages, Is.Empty);
            Assert.That(equipment.StoredPackages.Values, Has.Some.Matches<Package>(p => ReferenceEquals(p.Item, sword)));
            Assert.That(bag.StoredPackages, Is.Empty);
        }

        [Test]
        public void PickUp_WithAutoEquipOff_BagsTheDrop_AndRemovesItFromTheGround()
        {
            var ground = Ground();
            var bag = Bag();
            var equipment = Equipment();
            equipment.autoEquip = false;
            var sword = Sword();
            Lay(ground, sword);

            var picked = ground.PickUp(sword, new FakePlayer(equipment, bag));

            Assert.That(picked, Is.True);
            Assert.That(ground.StoredPackages, Is.Empty);
            Assert.That(bag.StoredPackages.Values, Has.Some.Matches<Package>(p => ReferenceEquals(p.Item, sword)));
            Assert.That(equipment.StoredPackages, Is.Empty);
        }

        [Test]
        public void PickUp_WithNoRoom_LeavesTheDropOnTheGround()
        {
            var ground = Ground();
            var full = Bag(1, 1);
            var filler = new Package(full, Sword(2), 1u);
            Assert.That(full.TryAddToContainer(ref filler), Is.True);
            var equipment = Equipment();
            equipment.autoEquip = false;
            var sword = Sword();
            Lay(ground, sword);

            var picked = ground.PickUp(sword, new FakePlayer(equipment, full));

            Assert.That(picked, Is.False);
            Assert.That(ground.PackagesOldestFirst().ConvertAll(p => p.Item), Is.EqualTo(new[] { sword }));
            Assert.That(full.StoredPackages, Has.Count.EqualTo(1));
        }

        [Test]
        public void PickUp_OfAnItemNotOnTheGround_ChangesNothing()
        {
            var ground = Ground();
            var bag = Bag();
            var equipment = Equipment();
            Lay(ground, Sword(2));

            var picked = ground.PickUp(Sword(), new FakePlayer(equipment, bag));

            Assert.That(picked, Is.False);
            Assert.That(ground.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(bag.StoredPackages, Is.Empty);
        }

        [Test]
        public void PickUp_WhenThePlayerThrows_LeavesTheDropOnTheGround_AndSurfacesTheFailure()
        {
            var ground = Ground();
            var bag = Bag();
            var equipment = Equipment();
            equipment.autoEquip = false;
            var boom = new InvalidOperationException("equip blew up");
            var sword = Sword();
            Lay(ground, sword);
            ItemInstance reportedItem = null;
            Exception reported = null;

            var picked = ground.PickUp(sword, new FakePlayer(equipment, bag) { Throws = boom },
                (item, exception) => (reportedItem, reported) = (item, exception));

            Assert.That(picked, Is.False);
            Assert.That(ground.PackagesOldestFirst().ConvertAll(p => p.Item), Is.EqualTo(new[] { sword }));
            Assert.That(bag.StoredPackages, Is.Empty, "the failed bag add rolled back");
            Assert.That(reported, Is.SameAs(boom));
            Assert.That(reportedItem, Is.SameAs(sword));
        }
    }
}
