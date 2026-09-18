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
    /// The shift-click-into-the-Sell-Basket move (issue #33): a pure container-seam helper
    /// that stages the Package sitting in a source cell into the basket and remembers the
    /// origin, in one <see cref="ItemTransaction"/>. The backpack and the paper-doll are the
    /// two sources; an equipment source's affix lift rides the commit exactly as a normal
    /// unequip does, and a basket with no room rolls back so the item stays where it was.
    /// The slot displays call this when the quick-move resolver returns a
    /// <see cref="QuickMoveIntentKind.SellBasket"/> intent.
    ///
    /// <para>Assertions are on container contents, item count and the stat receiver, never
    /// on how many events fired (ADR-0007 - the one seam is
    /// <c>InventorySystem.Containers</c>). Prior art: <see cref="SellBasketTests"/>.</para>
    /// </summary>
    [TestFixture]
    public sealed class SellBasketQuickMoveTests
    {
        private const string SwordId = "test.sword";
        private const string HelmId = "test.helm";

        private TestCatalog catalog;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new TestCatalog()
                .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u });

            ItemView.Catalog = catalog;
        }

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, 6f) });
        private static ItemInstance Helm() => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, 4f) });

        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height));
        private static CharacterEquipment Equipment(IStatReceiver stats = null) => new(new Vector2Int(14, 1), stats);

        // ── backpack source ─────────────────────────────────────────────────

        [Test]
        public void BackpackSource_StagesIntoTheBasket_AndRecordsTheOrigin()
        {
            var basket = new SellBasket.Basket(Inventory());
            var backpack = Inventory();
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Sword(), 1u));
            var sword = backpack.StoredPackages[new Vector2Int(0, 0)].Item;

            Assert.That(SellBasketQuickMove.SendToBasket(basket, backpack, new Vector2Int(0, 0)), Is.True,
                "the shift-click staged the item");

            Assert.That(backpack.StoredPackages, Is.Empty, "the backpack cell was vacated");
            var basketCell = basket.Container.StoredPackages.Keys.Single();
            Assert.That(basket.Container.StoredPackages[basketCell].Item, Is.SameAs(sword), "the item is in the basket");

            Assert.That(basket.Origins[basketCell].Container, Is.SameAs(backpack), "the origin is the backpack");
            Assert.That(basket.Origins[basketCell].Cell, Is.EqualTo(new Vector2Int(0, 0)), "the origin is the vacated cell");
        }

        // ── equipment source: unequip straight into the basket ─────────────

        [Test]
        public void EquipmentSource_UnequipsIntoTheBasket_AndLiftsTheAffixOnCommit()
        {
            var basket = new SellBasket.Basket(Inventory());
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var backpack = Inventory();

            var worn = new Package(backpack, Helm(), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            var instance = equipment.StoredPackages[slot].Item;
            stats.Added.Clear();

            Assert.That(SellBasketQuickMove.SendToBasket(basket, equipment, slot), Is.True,
                "the worn item was staged");

            Assert.That(equipment.StoredPackages, Is.Empty, "the slot was vacated");
            Assert.That(basket.Container.StoredPackages.Values.Single().Item, Is.SameAs(instance), "the item is in the basket");
            Assert.That(stats.Removed.Select(r => r.Stat), Is.EquivalentTo(new[] { StatName.Armor }), "the worn affix was lifted");
            Assert.That(stats.Added, Is.Empty, "nothing was re-applied");
        }

        // ── a full basket leaves the item in the source ────────────────────

        [Test]
        public void FullBasket_ReturnsFalse_AndLeavesTheItemInTheSource()
        {
            var basket = new SellBasket.Basket(Inventory(1, 1));
            var backpack = Inventory(1, 1);

            // Fill the single basket cell so the sword has nowhere to land.
            _ = basket.Container.AddAtPosition(new Vector2Int(0, 0), new Package(basket.Container, Sword(), 1u));
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Helm(), 1u));
            var helm = backpack.StoredPackages[new Vector2Int(0, 0)].Item;

            Assert.That(SellBasketQuickMove.SendToBasket(basket, backpack, new Vector2Int(0, 0)), Is.False,
                "a full basket cannot take the item");

            Assert.That(backpack.StoredPackages[new Vector2Int(0, 0)].Item, Is.SameAs(helm), "the item stayed in the backpack");
            Assert.That(basket.Container.StoredPackages.Count, Is.EqualTo(1), "the basket is unchanged");
            Assert.That(basket.Origins, Is.Empty, "no origin was recorded for a failed move");
        }

        // ── nothing stored at the cell ─────────────────────────────────────

        [Test]
        public void MissingSourcePackage_ReturnsFalse()
        {
            var basket = new SellBasket.Basket(Inventory());
            var backpack = Inventory();

            Assert.That(SellBasketQuickMove.SendToBasket(basket, backpack, new Vector2Int(0, 0)), Is.False);
            Assert.That(basket.Container.StoredPackages, Is.Empty);
        }
    }
}
