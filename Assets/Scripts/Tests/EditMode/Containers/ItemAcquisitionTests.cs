using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The placement rule behind <see cref="IItemReceiver"/> (issue #103): auto-equip into an
    /// empty slot, else the Inventory, and <c>false</c> - with nothing moved anywhere else - when
    /// neither has room. This is the rule <c>Hero</c> delegates to, so it is the one place
    /// "no Stash fallback on the receiver path" can be pinned without a scene.
    /// </summary>
    [TestFixture]
    public sealed class ItemAcquisitionTests
    {
        private const string SwordId = "test.sword";
        private const string HelmId = "test.helm";
        private const string PotionId = "test.potion";

        private static IItemCatalog catalog;

        [SetUp]
        public void SetCatalog() => catalog = new TestCatalog()
            .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = PotionId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Potion, Footprint = ItemSize.OneByOne, BaseStackLimit = 5u });

        [TearDown]
        public void ClearCatalog() => catalog = null;

        private static Package Of(string id, uint amount = 1u) => new(null, new ItemInstance(id, ItemRarity.Common, 1, null), amount);
        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height), catalog);
        private static CharacterEquipment Equipment(bool autoEquip = true) => new(new Vector2Int(14, 1), catalog, null) { autoEquip = autoEquip };

        [Test]
        public void Equipment_AutoEquipOnAndSlotEmpty_Equips()
        {
            var inventory = Inventory();
            var equipment = Equipment();
            var package = Of(SwordId);

            Assert.That(ItemAcquisition.TryPlace(ref package, equipment, inventory), Is.True);
            Assert.That(equipment.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(inventory.StoredPackages, Is.Empty);
        }

        [Test]
        public void Equipment_AutoEquipOff_LandsInTheInventory()
        {
            var inventory = Inventory();
            var equipment = Equipment(autoEquip: false);
            var package = Of(SwordId);

            Assert.That(ItemAcquisition.TryPlace(ref package, equipment, inventory), Is.True);
            Assert.That(equipment.StoredPackages, Is.Empty);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
        }

        [Test]
        public void Equipment_SlotOccupied_LandsInTheInventory_WithoutSwapping()
        {
            var inventory = Inventory();
            var equipment = Equipment();
            var first = Of(HelmId);
            var second = Of(HelmId);
            Assert.That(ItemAcquisition.TryPlace(ref first, equipment, inventory), Is.True, "test setup: the only helm slot is now taken");

            Assert.That(ItemAcquisition.TryPlace(ref second, equipment, inventory), Is.True);
            Assert.That(equipment.StoredPackages, Has.Count.EqualTo(1), "the equipped helm stayed");
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1), "the second helm went to the bag");
        }

        [Test]
        public void NonEquipment_LandsInTheInventory()
        {
            var inventory = Inventory();
            var equipment = Equipment();
            var package = Of(PotionId);

            Assert.That(ItemAcquisition.TryPlace(ref package, equipment, inventory), Is.True);
            Assert.That(equipment.StoredPackages, Is.Empty);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
        }

        [Test]
        public void NoEquipmentContainer_FallsThroughToTheInventory()
        {
            var inventory = Inventory();
            var package = Of(SwordId);

            Assert.That(ItemAcquisition.TryPlace(ref package, null, inventory), Is.True);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
        }

        [Test]
        public void NoRoomAnywhere_ReturnsFalse_AndMovesNothing()
        {
            var inventory = Inventory(1, 1);
            var equipment = Equipment(autoEquip: false);
            var filler = Of(SwordId);
            Assert.That(ItemAcquisition.TryPlace(ref filler, equipment, inventory), Is.True, "test setup: the one cell is full");
            var package = Of(SwordId);

            Assert.That(ItemAcquisition.TryPlace(ref package, equipment, inventory), Is.False);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1), "still just the filler");
            Assert.That(equipment.StoredPackages, Is.Empty);
            Assert.That(package.Amount, Is.EqualTo(1u), "the whole package is still in the caller's hands");
        }

        [Test]
        public void PartialStack_ReturnsFalse_AndLeavesTheRemainderInThePackage()
        {
            var inventory = Inventory(1, 1);
            var equipment = Equipment();
            var held = Of(PotionId, 4u);
            Assert.That(ItemAcquisition.TryPlace(ref held, equipment, inventory), Is.True, "test setup: 4 of the 5-stack sit in the only cell");
            var incoming = Of(PotionId, 3u);

            Assert.That(ItemAcquisition.TryPlace(ref incoming, equipment, inventory), Is.False);
            Assert.That(incoming.Amount, Is.EqualTo(2u), "one fit; the remainder is the caller's to handle");
        }
    }
}
