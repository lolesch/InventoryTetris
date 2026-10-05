using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The Hero and its World built from a test config and a fixed roll source (issue #112): no
    /// scene, no locator, no <c>UnityEngine.Random</c>. Asserts what the build hands the Hero and
    /// the World, and the one ordering fact that matters - the Equipment was built with the Hero
    /// as its stat receiver.
    /// </summary>
    [TestFixture]
    public sealed class SessionBuilderTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private HeroData heroData;
        private ItemService items;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            heroData = GameBoot.Load().DefaultHero;
            items = new ItemService(config, new FixedRolls(0.5f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private Session Build() => SessionBuilder.Build(config, heroData, items);

        // ── the Hero ─────────────────────────────────────────────────────────

        [Test]
        public void Build_GivesTheHero_ItsContainers_AtTheConfiguredSizes()
        {
            var hero = Build().Hero;

            Assert.That(hero.IsOutfitted, Is.True);
            Assert.That(hero.Equipment.Dimensions, Is.EqualTo(config.EquipmentSize));
            Assert.That(hero.Inventory.Dimensions, Is.EqualTo(config.InventorySize));
            Assert.That(hero.Stash.Dimensions, Is.EqualTo(config.StashSize));
        }

        [Test]
        public void Build_BacksTheWallet_WithTheHerosInventory()
        {
            var hero = Build().Hero;

            hero.Wallet.Deposit(new Currency(5u));

            Assert.That(hero.Wallet.Container, Is.SameAs(hero.Inventory));
            Assert.That(hero.Wallet.Balance.Total, Is.EqualTo(5u));
            Assert.That(hero.Inventory.StoredPackages, Is.Not.Empty, "the coins are cells of the Inventory");
        }

        [Test]
        public void Build_BuildsTheEquipment_WithTheHeroAsItsStatReceiver()
        {
            var hero = Build().Hero;
            var before = StatTotals(hero);

            var equipped = hero.PickUpItem(items.RollEquipment(EquipmentType.Chest), 1u);

            Assert.That(equipped, Is.True);
            Assert.That(hero.Equipment.StoredPackages, Has.Count.EqualTo(1), "auto-equip took it");
            Assert.That(StatTotals(hero), Is.Not.EqualTo(before), "its affixes landed on this hero, not on some other receiver");
        }

        [Test]
        public void Build_SeedsTheBehaviourProfile_FromTheConfigDefaults()
        {
            var behaviour = Build().Hero.Behaviour;

            Assert.That(behaviour.SimSpeed, Is.EqualTo(config.SimSpeed));
            Assert.That(behaviour.Engagement, Is.EqualTo(config.Engagement));
            Assert.That(behaviour.RetreatHealthFraction, Is.EqualTo(config.RetreatHealthFraction));
            Assert.That(behaviour.RecallBagFillFraction, Is.EqualTo(config.RecallBagFillFraction));
            Assert.That(behaviour.CastThreshold, Is.EqualTo(config.CastThreshold));
            Assert.That(behaviour.LootFilterMinimum, Is.EqualTo(config.LootFilterMinimum));
        }

        [Test]
        public void ABuiltHero_IsAnItemReceiver_ThatStopsAtNoRoom()
        {
            var hero = Build().Hero;
            hero.Equipment.autoEquip = false;

            FillWithEquipment(hero.Inventory);

            Assert.That(hero.PickUpItem(items.RollEquipment(EquipmentType.Chest), 1u), Is.False);
            Assert.That(hero.Stash.StoredPackages, Is.Empty, "no debug Stash behind the receiver");
        }

        [Test]
        public void AHeroThatWasNeverOutfitted_SaysSo_WhenAskedForAContainer()
        {
            var bare = new Hero(heroData);

            Assert.That(bare.IsOutfitted, Is.False);
            Assert.That(() => bare.Inventory, Throws.InvalidOperationException);
            Assert.That(() => bare.PickUpItem(items.RollConsumable(ConsumableType.Book), 1u), Throws.InvalidOperationException);
        }

        [Test]
        public void AHero_CannotBeOutfittedTwice()
        {
            var hero = Build().Hero;

            Assert.That(() => hero.Outfit(hero.Equipment, hero.Inventory, hero.Stash, hero.Wallet, hero.Behaviour),
                Throws.InvalidOperationException);
        }

        // ── the World ────────────────────────────────────────────────────────

        [Test]
        public void Build_BuildsTheWorld_WithTheSuppliesTheSoldContainerAndAnIdleContext()
        {
            var world = Build().World;

            Assert.That(world.VendorSupply.Dimensions, Is.EqualTo(config.SupplySize));
            Assert.That(world.HealerSupply.Dimensions, Is.EqualTo(config.SupplySize), "one size for every Supply");
            Assert.That(world.Sold.Dimensions, Is.EqualTo(config.SoldSize));
            Assert.That(world.Context.Active, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void TheWorld_HoldsNoneOfTheHerosContainers()
        {
            var session = Build();
            var hero = session.Hero;
            var world = session.World;

            var all = new AbstractDimensionalContainer[]
            {
                hero.Equipment, hero.Inventory, hero.Stash, world.VendorSupply, world.HealerSupply, world.Sold,
            };

            Assert.That(all.Distinct().Count(), Is.EqualTo(all.Length), "six containers, no two the same");
        }

        [Test]
        public void TwoBuilds_ShareNothing()
        {
            var first = Build();
            var second = Build();

            first.Hero.Wallet.Deposit(new Currency(9u));
            first.World.Context.Set(InventoryContext.Vendor);

            Assert.That(second.Hero, Is.Not.SameAs(first.Hero));
            Assert.That(second.Hero.Wallet.Balance.Total, Is.Zero);
            Assert.That(second.World.Context.Active, Is.EqualTo(InventoryContext.None));
        }

        // ── arguments ────────────────────────────────────────────────────────

        [Test]
        public void Build_WithoutAHeroTemplate_Throws_NamingIt() =>
            Assert.That(() => SessionBuilder.Build(config, null, items),
                Throws.ArgumentNullException.With.Message.Contain(nameof(HeroData)));

        [Test]
        public void Build_WithoutAConfigOrAnItemService_Throws()
        {
            Assert.That(() => SessionBuilder.Build(null, heroData, items), Throws.ArgumentNullException);
            Assert.That(() => SessionBuilder.Build(config, heroData, null), Throws.ArgumentNullException);
        }

        // Equipment does not stack, so the bag is full after a handful of pieces.
        private void FillWithEquipment(AbstractDimensionalContainer container)
        {
            for (var i = 0; i < container.Capacity; i++)
            {
                var package = new Package(null, items.RollEquipment(EquipmentType.Chest), 1u);

                if (!container.TryAddToContainer(ref package))
                    return;
            }

            Assert.Fail("the container never filled");
        }

        private static float[] StatTotals(Hero hero) =>
            hero.Stats.Select(stat => stat.TotalValue).Concat(hero.Resources.Select(resource => resource.TotalValue)).ToArray();

        internal sealed class FixedRolls : IRollSource
        {
            private readonly float value;
            public FixedRolls(float value) => this.value = value;
            public float Next() => value;
        }
    }
}
