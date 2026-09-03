using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// <see cref="HoverPreview.Under"/> is the single data source for the hover-preview
    /// panel (issue #13, QA-2). The slot displays used to read it straight off the slot's
    /// own <c>Position</c> and only ever <em>added</em> a preview, leaning on a pointer-exit
    /// to remove it. Since a drop follows the drag visual and routinely lands a cell away,
    /// a swap could leave the hovered cell empty with the carried-away item still on screen.
    /// These tests pin the two things the fix depends on: the cell the cursor rests over
    /// after a drop names the item now there, and an emptied cell reports "nothing" so the
    /// caller hides the panel instead of leaving it stale.
    /// </summary>
    [TestFixture]
    public sealed class HoverPreviewTests
    {
        private const string RingId = "test.ring";     // 1x1
        private const string HelmId = "test.helm";     // 1x1
        private const string PlateId = "test.plate";   // 2x2

        [SetUp]
        public void SetCatalog() => ItemView.Catalog = new TestCatalog()
            .With(new TestDefinition { Id = RingId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Ring, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = PlateId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Chest, Footprint = ItemSize.TwoByTwo, BaseStackLimit = 1u });

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        private static ItemInstance Ring() => new(RingId, ItemRarity.Magic, 3, null);
        private static ItemInstance Helm() => new(HelmId, ItemRarity.Rare, 5, null);
        private static ItemInstance Plate() => new(PlateId, ItemRarity.Rare, 5, null);

        private static CharacterInventory Inventory() => new(new Vector2Int(4, 4));

        [Test]
        public void Under_ANullContainer_ReturnsAnInvalidPackage()
        {
            Assert.That(HoverPreview.Under(null, Vector2Int.zero).IsValid, Is.False);
        }

        [Test]
        public void Under_AnEmptyCell_ReturnsAnInvalidPackage()
        {
            var inventory = Inventory();

            Assert.That(HoverPreview.Under(inventory, new Vector2Int(2, 2)).IsValid, Is.False);
        }

        [Test]
        public void Under_ACellHoldingAnItem_ReturnsThatItemsPackage()
        {
            var inventory = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(1, 1), new Package(inventory, Helm(), 1u));
            var stored = inventory.StoredPackages[new Vector2Int(1, 1)].Item;

            var preview = HoverPreview.Under(inventory, new Vector2Int(1, 1));

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Item, Is.SameAs(stored));
        }

        [Test]
        public void Under_ACellCoveredByAMultiCellItem_ResolvesToTheItemsOrigin()
        {
            var inventory = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Plate(), 1u));
            var stored = inventory.StoredPackages[new Vector2Int(0, 0)].Item;

            // The cursor rests over the plate's far corner, not its origin cell.
            var preview = HoverPreview.Under(inventory, new Vector2Int(1, 1));

            Assert.That(preview.Item, Is.SameAs(stored));
        }

        [Test]
        public void Under_AtTheLandingCellAfterASwap_DescribesTheItemThatLanded_NotTheOneSentToHand()
        {
            // QA-2: hover a slot holding B, drop A onto it. A is in the slot, B is in hand -
            // the preview must describe A now, not the B the drag carried away.
            var inventory = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Helm(), 1u));
            var bInstance = inventory.StoredPackages[new Vector2Int(0, 0)].Item;

            var displaced = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Ring(), 1u));
            Assert.That(displaced.Item, Is.SameAs(bInstance), "premise: the swap displaced B onto the cursor");

            var preview = HoverPreview.Under(inventory, new Vector2Int(0, 0));

            Assert.That(preview.Item.DefinitionId, Is.EqualTo(RingId));
            Assert.That(preview.Item, Is.Not.SameAs(bInstance));
        }

        [Test]
        public void Under_AtACellAVacatingSwapEmptied_ReturnsInvalid_SoThePreviewClears()
        {
            // The cursor is over the far corner of a 2x2 B. A 1x1 A dropped onto B's origin
            // displaces B entirely; the corner the pointer still rests on is now empty. The
            // old code found nothing there and left B's preview frozen - Under must report
            // "nothing" so the caller hides it.
            var inventory = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Plate(), 1u));

            var displaced = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Ring(), 1u));
            Assert.That(displaced.IsValid, Is.True, "premise: the 2x2 was displaced to the cursor");

            Assert.That(HoverPreview.Under(inventory, new Vector2Int(1, 1)).IsValid, Is.False);
        }
    }
}
