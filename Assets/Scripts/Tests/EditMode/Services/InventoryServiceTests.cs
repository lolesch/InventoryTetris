using NUnit.Framework;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The inventory service over a built Hero and World (issue #112): the container a role names,
    /// where a Quick Move lands, acquisition with the debug Stash overflow, and each Supply's
    /// Restock - which still clears the Sold container (#128). No scene and no locator: the
    /// session is built by <see cref="SessionBuilder"/> from a test config.
    /// </summary>
    [TestFixture]
    public sealed class InventoryServiceTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private ItemService items;
        private Session session;
        private InventoryService service;

        [SetUp]
        public void SetUp()
        {
            var config = TestGameConfig.Create(created);
            items = new ItemService(config, new SessionBuilderTests.FixedRolls(0.5f));
            session = SessionBuilder.Build(config, GameBoot.Load().DefaultHero, items);
            service = new InventoryService(session, items);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private Package Equipment() => new(null, items.RollEquipment(EquipmentType.Chest), 1u);

        // ── containers by role ───────────────────────────────────────────────

        [Test]
        public void ContainerFor_NamesTheHerosAndTheWorldsContainers()
        {
            var hero = session.Hero;
            var world = session.World;

            Assert.That(service.ContainerFor(ContainerRole.Equipment), Is.SameAs(hero.Equipment));
            Assert.That(service.ContainerFor(ContainerRole.Inventory), Is.SameAs(hero.Inventory));
            Assert.That(service.ContainerFor(ContainerRole.Stash), Is.SameAs(hero.Stash));
            Assert.That(service.ContainerFor(ContainerRole.VendorSupply), Is.SameAs(world.VendorSupply));
            Assert.That(service.ContainerFor(ContainerRole.HealerSupply), Is.SameAs(world.HealerSupply));
            Assert.That(service.ContainerFor(ContainerRole.Sold), Is.SameAs(world.Sold));
        }

        // ── Quick Move ───────────────────────────────────────────────────────

        [Test]
        public void QuickMoveFor_ReadsTheWorldsInventoryContext()
        {
            var hero = session.Hero;

            Assert.That(service.QuickMoveFor(hero.Inventory).Kind, Is.EqualTo(QuickMoveIntentKind.None), "no context, no sink");

            session.World.Context.Set(InventoryContext.Vendor);

            Assert.That(service.QuickMoveFor(hero.Inventory).Kind, Is.EqualTo(QuickMoveIntentKind.Sell));
            Assert.That(service.QuickMoveFor(session.World.VendorSupply).Kind, Is.EqualTo(QuickMoveIntentKind.Buy));
        }

        // ── acquisition ──────────────────────────────────────────────────────

        [Test]
        public void PickUpOrStash_AutoEquips_ElseTheBag()
        {
            var hero = session.Hero;

            Assert.That(service.PickUpOrStash(Equipment()), Is.True);
            Assert.That(hero.Equipment.StoredPackages, Has.Count.EqualTo(1), "auto-equip first");

            hero.Equipment.autoEquip = false;

            Assert.That(service.PickUpOrStash(Equipment()), Is.True);
            Assert.That(hero.Inventory.StoredPackages, Has.Count.EqualTo(1), "else the bag");
        }

        [Test]
        public void PickUpOrStash_WithAFullBag_OverflowsToTheStash_InADebugBuild()
        {
            Assume.That(UnityEngine.Debug.isDebugBuild, "the Stash overflow is a debug-build feature");
            var hero = session.Hero;
            hero.Equipment.autoEquip = false;

            for (var i = 0; i < hero.Inventory.Capacity; i++)
            {
                var filler = Equipment();

                if (!hero.Inventory.TryAddToContainer(ref filler))
                    break;
            }

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Stash"));

            Assert.That(service.PickUpOrStash(Equipment()), Is.True);
            Assert.That(hero.Stash.StoredPackages, Has.Count.EqualTo(1));
        }

        // ── Restock ──────────────────────────────────────────────────────────

        [Test]
        public void RestockTownStops_StocksBothSupplies_AndClearsTheSoldContainer()
        {
            var sold = Equipment();
            Assert.That(session.World.Sold.TryAddToContainer(ref sold), Is.True);

            service.RestockTownStops();

            Assert.That(session.World.VendorSupply.StoredPackages, Is.Not.Empty);
            Assert.That(session.World.HealerSupply.StoredPackages, Is.Not.Empty);
            Assert.That(session.World.Sold.StoredPackages, Is.Empty);
        }

        [Test]
        public void RestockVendorSupply_RefillsOnlyTheVendor_AndStillClearsTheSoldContainer()
        {
            service.RestockTownStops();
            var healerStock = session.World.HealerSupply.StoredPackages.Count;
            var sold = Equipment();
            _ = session.World.Sold.TryAddToContainer(ref sold);

            service.RestockVendorSupply();

            Assert.That(session.World.Sold.StoredPackages, Is.Empty);
            Assert.That(session.World.VendorSupply.StoredPackages, Is.Not.Empty);
            Assert.That(session.World.HealerSupply.StoredPackages, Has.Count.EqualTo(healerStock), "the Healer's shelf is untouched");
        }

        [Test]
        public void RestockHealerSupply_RefillsOnlyTheHealer_AndStillClearsTheSoldContainer()
        {
            service.RestockTownStops();
            var vendorStock = session.World.VendorSupply.StoredPackages.Count;
            var sold = Equipment();
            _ = session.World.Sold.TryAddToContainer(ref sold);

            service.RestockHealerSupply();

            Assert.That(session.World.Sold.StoredPackages, Is.Empty);
            Assert.That(session.World.HealerSupply.StoredPackages, Is.Not.Empty);
            Assert.That(session.World.VendorSupply.StoredPackages, Has.Count.EqualTo(vendorStock), "the Vendor's shelf is untouched");
        }

        [Test]
        public void ARestock_ReplacesTheShelf_NeverAddsToIt()
        {
            service.RestockVendorSupply();
            var once = session.World.VendorSupply.StoredPackages.Count;

            service.RestockVendorSupply();

            Assert.That(session.World.VendorSupply.StoredPackages, Has.Count.EqualTo(once));
        }

        // ── the session is read on every call ────────────────────────────────

        [Test]
        public void TheService_OperatesOnWhateverSessionItIsOver()
        {
            var other = SessionBuilder.Build(TestGameConfig.Create(created), GameBoot.Load().DefaultHero, items);
            var otherService = new InventoryService(other, items);

            otherService.RestockTownStops();

            Assert.That(other.World.VendorSupply.StoredPackages, Is.Not.Empty);
            Assert.That(session.World.VendorSupply.StoredPackages, Is.Empty, "the first session was not touched");
        }
    }
}
