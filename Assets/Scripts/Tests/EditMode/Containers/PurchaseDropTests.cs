using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// Where a purchase in progress may land and where it falls back to (issues #31, #129): the
    /// rule the slot displays and the drag cursor both ask <see cref="PurchaseDrop"/>.
    /// </summary>
    [TestFixture]
    public sealed class PurchaseDropTests
    {
        private static CharacterInventory Container() => new(new Vector2Int(2, 2), new TestCatalog());

        private CharacterInventory bag;
        private CharacterInventory stash;
        private CharacterInventory shelf;

        [SetUp]
        public void SetUp()
        {
            bag = Container();
            stash = Container();
            shelf = Container();
        }

        [Test]
        public void APurchase_LandsInTheBag_AndOnTheEquipment()
        {
            var equipment = Container();

            Assert.That(PurchaseDrop.MayLandIn(true, bag, bag, equipment), Is.True);
            Assert.That(PurchaseDrop.MayLandIn(true, equipment, bag, equipment), Is.True);
        }

        [Test]
        public void APurchase_DoesNotLand_OnTheStash_AnotherShelf_OrTheWorld()
        {
            var equipment = Container();

            Assert.That(PurchaseDrop.MayLandIn(true, stash, bag, equipment), Is.False, "the Stash");
            Assert.That(PurchaseDrop.MayLandIn(true, shelf, bag, equipment), Is.False, "a shelf");
            Assert.That(PurchaseDrop.MayLandIn(true, null, bag, equipment), Is.False, "the world has no container");
        }

        [Test]
        public void APlayersOwnItem_MayLandAnywhere()
        {
            var equipment = Container();

            Assert.That(PurchaseDrop.MayLandIn(false, stash, bag, equipment), Is.True);
            Assert.That(PurchaseDrop.MayLandIn(false, shelf, bag, equipment), Is.True);
            Assert.That(PurchaseDrop.MayLandIn(false, null, bag, equipment), Is.True);
        }

        [Test]
        public void APurchaseFallsBack_ToItsOwnShelf_NeverTheBag()
        {
            Assert.That(PurchaseDrop.FallbackFor(true, shelf, bag), Is.SameAs(shelf));
        }

        [Test]
        public void APurchaseWithNoKnownOrigin_FallsBackToTheBag()
        {
            Assert.That(PurchaseDrop.FallbackFor(true, null, bag), Is.SameAs(bag));
        }

        [Test]
        public void APlayersOwnItem_FallsBackToTheBag()
        {
            Assert.That(PurchaseDrop.FallbackFor(false, shelf, bag), Is.SameAs(bag));
        }
    }
}
