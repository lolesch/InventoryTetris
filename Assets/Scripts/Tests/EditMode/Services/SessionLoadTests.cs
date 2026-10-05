using NUnit.Framework;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// A hero load (issue #114, ADR-0015 decision 4): a new Hero and a new World, swapped as a pair, and
    /// <see cref="ISession.HeroLoaded"/> raised after. Driven through the booted locator, because the
    /// point of the swap is that nothing reachable from it is the old Hero's or the old World's.
    /// </summary>
    [TestFixture]
    public sealed class SessionLoadTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private HeroData data;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameLoop.Reset();

            config = TestGameConfig.Create(created);
            data = config.DefaultHero;
            GameBoot.Arm(config);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();

            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private LocationConfig Location()
        {
            var location = ScriptableObject.CreateInstance<LocationConfig>();
            created.Add(location);

            var so = new SerializedObject(location);
            so.FindProperty("id").stringValue = "thornwood";
            so.FindProperty("categoryDistribution").objectReferenceValue = config.ItemCategoryDistribution;
            so.FindProperty("rarityDistribution").objectReferenceValue = config.ItemRarityDistribution;
            _ = so.ApplyModifiedPropertiesWithoutUndo();

            return location;
        }

        // ── the swap ─────────────────────────────────────────────────────────

        [Test]
        public void TryLoad_SwapsTheHeroAndTheWorld_AsAPair()
        {
            var hero = Session.Instance.Hero;
            var world = Session.Instance.World;

            Assert.That(Session.Instance.TryLoad(data), Is.True);

            Assert.That(Session.Instance.Hero, Is.Not.SameAs(hero));
            Assert.That(Session.Instance.World, Is.Not.SameAs(world));
            Assert.That(Session.Instance.Hero.IsOutfitted, Is.True);
        }

        [Test]
        public void TryLoad_RaisesHeroLoaded_Once_AfterBothAreSwapped()
        {
            var raised = 0;
            Hero seen = null;
            World seenWorld = null;
            Session.Instance.HeroLoaded += () =>
            {
                raised++;
                seen = Session.Instance.Hero;
                seenWorld = Session.Instance.World;
            };

            _ = Session.Instance.TryLoad(data);

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(seen, Is.SameAs(Session.Instance.Hero), "a listener reads the new Hero");
            Assert.That(seenWorld, Is.SameAs(Session.Instance.World), "and the new World");
        }

        [Test]
        public void TryLoad_GivesTheNewHeroNothingOfTheOld()
        {
            var old = Session.Instance.Hero;
            old.Wallet.Deposit(new Currency(9u));
            var package = new Package(null, ItemService.Instance.RollEquipment(EquipmentType.Chest), 1u);
            _ = old.Stash.TryAddToContainer(ref package);

            _ = Session.Instance.TryLoad(data);

            Assert.That(Session.Instance.Hero.Wallet.Balance.Total, Is.Zero);
            Assert.That(Session.Instance.Hero.Stash.StoredPackages, Is.Empty);
            Assert.That(Session.Instance.World.VendorSupply.StoredPackages, Is.Not.Empty, "the new World's shelves are stocked as a booted one's are");
            Assert.That(Session.Instance.World.HealerSupply.StoredPackages, Is.Not.Empty);
            Assert.That(Session.Instance.World.Sold.StoredPackages, Is.Empty);
        }

        [Test]
        public void TryLoad_WithoutATemplate_Throws_AndChangesNothing()
        {
            var hero = Session.Instance.Hero;

            Assert.That(() => Session.Instance.TryLoad(null), Throws.ArgumentNullException);
            Assert.That(Session.Instance.Hero, Is.SameAs(hero));
        }

        // ── unreachable ──────────────────────────────────────────────────────

        [Test]
        public void AfterASwap_TheOldHerosContainersAndCorpse_AreUnreachableThroughTheLocator()
        {
            var oldHero = Session.Instance.Hero;
            var oldCorpse = oldHero.Corpse;

            _ = Session.Instance.TryLoad(data);

            var inventory = InventoryService.Instance;
            Assert.That(inventory.ContainerFor(ContainerRole.Equipment), Is.Not.SameAs(oldHero.Equipment));
            Assert.That(inventory.ContainerFor(ContainerRole.Inventory), Is.Not.SameAs(oldHero.Inventory));
            Assert.That(inventory.ContainerFor(ContainerRole.Stash), Is.Not.SameAs(oldHero.Stash));
            Assert.That(Session.Instance.Hero.Corpse, Is.Not.SameAs(oldCorpse));
        }

        [Test]
        public void AfterASwap_TheOldWorldsSuppliesSoldContainerAndRun_AreUnreachableThroughTheLocator()
        {
            var oldWorld = Session.Instance.World;
            var oldRun = SimulationService.Instance.Run;

            _ = Session.Instance.TryLoad(data);

            var inventory = InventoryService.Instance;
            Assert.That(inventory.ContainerFor(ContainerRole.VendorSupply), Is.Not.SameAs(oldWorld.VendorSupply));
            Assert.That(inventory.ContainerFor(ContainerRole.HealerSupply), Is.Not.SameAs(oldWorld.HealerSupply));
            Assert.That(inventory.ContainerFor(ContainerRole.Sold), Is.Not.SameAs(oldWorld.Sold));
            Assert.That(SimulationService.Instance.Run, Is.Not.SameAs(oldRun));
            Assert.That(SimulationService.Instance.Run.Phase, Is.EqualTo(RunPhase.InTown));
        }

        [Test]
        public void AfterASwap_AQuickMoveAndARestock_OperateOnTheNewPair()
        {
            _ = Session.Instance.TryLoad(data);

            Session.Instance.World.VendorSupply.RemoveAll();
            InventoryService.Instance.RestockTownStops();
            Session.Instance.World.Context.Set(InventoryContext.Vendor);

            Assert.That(Session.Instance.World.VendorSupply.StoredPackages, Is.Not.Empty);
            Assert.That(InventoryService.Instance.QuickMoveFor(Session.Instance.World.VendorSupply).Kind,
                Is.EqualTo(QuickMoveIntentKind.Buy));
        }

        // ── the rule for a live Run ──────────────────────────────────────────

        [Test]
        public void TryLoad_DuringALiveRun_Refuses_AndChangesNothing()
        {
            SimulationService.Instance.Send(Location());
            var hero = Session.Instance.Hero;
            var world = Session.Instance.World;
            var raised = 0;
            Session.Instance.HeroLoaded += () => raised++;

            Assert.That(Session.Instance.TryLoad(data), Is.False);

            Assert.That(Session.Instance.Hero, Is.SameAs(hero));
            Assert.That(Session.Instance.World, Is.SameAs(world));
            Assert.That(SimulationService.Instance.Run.Phase, Is.EqualTo(RunPhase.InField), "the Run was not ended for it");
            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void TryLoad_AfterTheRunEnded_Succeeds()
        {
            SimulationService.Instance.Send(Location());
            _ = SimulationService.Instance.Recall();

            Assert.That(Session.Instance.TryLoad(data), Is.True);
        }

        [Test]
        public void TryLoad_InTownWithNoRunBuiltYet_Succeeds()
        {
            Assert.That(Session.Instance.World.Run, Is.Null);
            Assert.That(Session.Instance.TryLoad(data), Is.True);
        }

        // ── the Inventory Context subscription ───────────────────────────────

        [Test]
        public void ContextChanged_FollowsTheNewWorld_AndNoLongerTheOld()
        {
            var oldContext = Session.Instance.World.Context;
            _ = Session.Instance.TryLoad(data);

            var seen = new List<InventoryContext>();
            InventoryService.Instance.ContextChanged += seen.Add;

            oldContext.Set(InventoryContext.Stash);
            Assert.That(seen, Is.Empty, "the discarded World's context reaches nobody");

            Session.Instance.World.Context.Set(InventoryContext.Vendor);
            Assert.That(seen, Is.EqualTo(new[] { InventoryContext.Vendor }));
        }

        [Test]
        public void TryLoad_TellsContextSubscribers_TheNewWorldStartsClosed()
        {
            Session.Instance.World.Context.Set(InventoryContext.Stash);

            var seen = new List<InventoryContext>();
            InventoryService.Instance.ContextChanged += seen.Add;

            _ = Session.Instance.TryLoad(data);

            Assert.That(seen, Is.EqualTo(new[] { InventoryContext.None }),
                "a panel that was up for the old World is told it is closed");
            Assert.That(InventoryService.Instance.ActiveContext, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void TheHealer_RefillsTheNewHero_NotTheOldOne()
        {
            var old = Session.Instance.Hero;
            _ = Session.Instance.TryLoad(data);

            var hero = Session.Instance.Hero;
            hero.ReceiveDamage(DamageType.PhysicalDamage, 100000f);
            old.ReceiveDamage(DamageType.PhysicalDamage, 100000f);
            Assume.That(hero.IsDead, Is.True);

            Session.Instance.World.Context.Set(InventoryContext.Healer);

            Assert.That(hero.IsDead, Is.False);
            Assert.That(old.IsDead, Is.True, "the old Hero is not healed by the new World's Healer");
        }

        [Test]
        public void TwoSwaps_LeaveOneRelayPerSubscriber_NotOnePerLoad()
        {
            _ = Session.Instance.TryLoad(data);
            _ = Session.Instance.TryLoad(data);

            var calls = 0;
            InventoryService.Instance.ContextChanged += _ => calls++;

            Session.Instance.World.Context.Set(InventoryContext.Healer);

            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
