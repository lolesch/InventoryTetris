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
    /// The oldest-out piece the Sold container shares with the floor, at the container seam:
    /// a container that does not compact (the floor's case) evicts the oldest Packages until the
    /// new one fits and leaves every survivor in its cell. The Sold container's own behaviour,
    /// compaction included, is <see cref="SaleTests"/>.
    /// </summary>
    [TestFixture]
    public sealed class OldestOutInventoryTests
    {
        private const string SwordId = "test.sword";
        private const string GreatswordId = "test.greatsword";

        private static TestCatalog catalog;

        [SetUp]
        public void SetCatalog() => catalog = new TestCatalog()
            .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = GreatswordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.TwoByOne, BaseStackLimit = 1u });

        [TearDown]
        public void ClearCatalog() => catalog = null;

        private sealed class Ground : OldestOutInventory
        {
            public Ground(Vector2Int dimensions, IItemCatalog catalog) : base(dimensions, catalog) { }

            public bool Land(ItemInstance item)
            {
                if (!TryPlaceEvicting(new Package(this, item, 1u), out var landed, out var order, out _))
                    return false;

                NoteLanded(order, landed);
                return true;
            }
        }

        private static ItemInstance Sword(int seed) => new(SwordId, ItemRarity.Rare, seed, null);
        private static ItemInstance Greatsword() => new(GreatswordId, ItemRarity.Rare, 9, null);

        private static Vector2Int? CellOf(AbstractDimensionalContainer container, ItemInstance item) =>
            container.StoredPackages.Where(entry => ReferenceEquals(entry.Value.Item, item))
                .Select(entry => (Vector2Int?)entry.Key).FirstOrDefault();

        [Test]
        public void ANewPackage_EvictsTheOldestUntilItFits_AndLeavesTheSurvivorsInTheirCells()
        {
            var ground = new Ground(new Vector2Int(3, 1), catalog);
            var oldest = Sword(1);
            var middle = Sword(2);
            var newest = Sword(3);
            var wide = Greatsword();
            _ = ground.Land(oldest);
            _ = ground.Land(middle);
            _ = ground.Land(newest);

            Assert.That(ground.Land(wide), Is.True);

            Assert.That(CellOf(ground, oldest), Is.Null, "the oldest went first");
            Assert.That(CellOf(ground, middle), Is.Null, "one cell was not enough, so the next oldest went too");
            Assert.That(CellOf(ground, newest), Is.EqualTo(new Vector2Int(2, 0)), "nothing was moved");
            Assert.That(CellOf(ground, wide), Is.EqualTo(new Vector2Int(0, 0)));
        }
    }
}
