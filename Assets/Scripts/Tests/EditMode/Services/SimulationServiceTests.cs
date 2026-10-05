using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The simulation service over a built Hero and World (issue #113): no scene, no locator. The
    /// Run is the World's, the Corpse and the selected Location the Hero's, the tick is driven by
    /// calling <see cref="ISimulationService.Tick"/> the way <see cref="GameLoop"/> does, and the
    /// existing Run / loot-flow / settlement tests stay where they are, below this seam.
    /// </summary>
    [TestFixture]
    public sealed class SimulationServiceTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private ItemService items;
        private Session session;
        private InventoryService inventory;
        private SimulationService service;
        private LocationConfig thornwood;
        private LocationConfig ashfen;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            items = new ItemService(config, new SessionBuilderTests.FixedRolls(0.5f));
            session = SessionBuilder.Build(config, GameBoot.Load().DefaultHero, items);
            inventory = new InventoryService(session, items);
            service = new SimulationService(session, items, inventory, config, new SessionBuilderTests.FixedRolls(0.5f));
            thornwood = Location("thornwood");
            ashfen = Location("ashfen");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private LocationConfig Location(string id)
        {
            var location = ScriptableObject.CreateInstance<LocationConfig>();
            created.Add(location);

            var so = new SerializedObject(location);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("categoryDistribution").objectReferenceValue = config.ItemCategoryDistribution;
            so.FindProperty("rarityDistribution").objectReferenceValue = config.ItemRarityDistribution;
            _ = so.ApplyModifiedPropertiesWithoutUndo();

            return location;
        }

        // ── a fresh World and Hero ───────────────────────────────────────────

        [Test]
        public void AFreshWorld_HasNoRun_AndAFreshHero_HasNoCorpseAndNoLocation()
        {
            Assert.That(session.World.Run, Is.Null);
            Assert.That(session.World.LootFlow, Is.Null);
            Assert.That(session.Hero.Corpse.Exists, Is.False);
            Assert.That(session.Hero.SelectedLocation, Is.Null);
        }

        [Test]
        public void TheRun_IsBuiltOnFirstAsk_ThenTheWorldsOwn_AndStartsInTown()
        {
            var run = service.Run;

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(run.Encounter, Is.Null);
            Assert.That(session.World.Run, Is.SameAs(run));
            Assert.That(service.Run, Is.SameAs(run));
        }

        [Test]
        public void TwoWorlds_EachHoldTheirOwnRun()
        {
            var other = SessionBuilder.Build(config, GameBoot.Load().DefaultHero, items);
            var otherService = new SimulationService(other, items, new InventoryService(other, items), config,
                new SessionBuilderTests.FixedRolls(0.5f));

            Assert.That(otherService.Run, Is.Not.SameAs(service.Run));
            Assert.That(other.Hero.Corpse, Is.Not.SameAs(session.Hero.Corpse));
        }

        // ── Send / Recall ────────────────────────────────────────────────────

        [Test]
        public void Send_SelectsTheLocationOnTheHero_AndOpensTheRunWithALootFlow()
        {
            service.Send(thornwood);

            Assert.That(session.Hero.SelectedLocation, Is.SameAs(thornwood));
            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InField));
            Assert.That(service.Run.Encounter, Is.Not.Null);
            Assert.That(service.LootFlow, Is.Not.Null);
            Assert.That(session.World.LootFlow, Is.SameAs(service.LootFlow));
        }

        [Test]
        public void HeroContextQuickMove_DropsToTheGround_OnlyWhileARunHasOne()
        {
            session.World.Context.Set(InventoryContext.Hero);
            var bag = session.Hero.Inventory;

            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.None), "Town has no ground (issue #63)");

            service.Send(thornwood);
            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.Drop));

            _ = service.Recall();
            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.None), "the Run's end takes the ground with it");
        }

        [Test]
        public void Recall_EndsTheRun_RetiresTheLootFlow_AndRestocksTheShops()
        {
            session.World.VendorSupply.RemoveAll();
            service.Send(thornwood);

            _ = service.Recall();

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(service.LootFlow, Is.Null);
            Assert.That(session.World.VendorSupply.StoredPackages, Is.Not.Empty, "a Recall brings the shops new stock");
        }

        [Test]
        public void ARefusedSend_LeavesTheSelectedLocationAlone()
        {
            service.Send(thornwood);

            Assert.Throws<InvalidOperationException>(() => service.Send(ashfen));

            Assert.That(session.Hero.SelectedLocation, Is.SameAs(thornwood));
        }

        [Test]
        public void Relocate_MovesTheLiveRun_AndSelectsTheNewLocation()
        {
            service.Send(thornwood);
            var before = service.Run.Encounter;

            service.Relocate(ashfen);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InField));
            Assert.That(service.Run.Encounter, Is.Not.SameAs(before));
            Assert.That(session.Hero.SelectedLocation, Is.SameAs(ashfen));
        }

        // ── the tick ─────────────────────────────────────────────────────────

        [Test]
        public void Tick_RegeneratesTheHero_InTown()
        {
            var hero = session.Hero;
            var resource = hero.GetResource(StatName.Resource);
            _ = resource.RemoveFromCurrent(resource.TotalValue);

            service.Tick(1f);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(resource.CurrentValue, Is.GreaterThan(0f));
        }

        [Test]
        public void Tick_RegeneratesTheHero_InTheField_Too()
        {
            service.Send(thornwood);
            var resource = session.Hero.GetResource(StatName.Resource);
            _ = resource.RemoveFromCurrent(resource.TotalValue);

            service.Tick(1f);

            Assert.That(resource.CurrentValue, Is.GreaterThan(0f));
        }

        [Test]
        public void Tick_ScalesTheEncounterClock_ByTheHerosBehaviourProfile()
        {
            session.Hero.Behaviour.SimSpeed = 4f;
            service.Send(thornwood);

            // The combat clock banks fixed ticks and caps them per advance, so keep the step small.
            service.Tick(0.1f);

            Assert.That(service.Run.Encounter.Duration, Is.EqualTo(0.4f).Within(0.11f), "4x the 0.1s of real time");
        }

        [Test]
        public void Tick_AutoRecalls_WhenTheBehaviourProfileSaysRetreat_AndRestocks()
        {
            session.World.VendorSupply.RemoveAll();
            session.Hero.Behaviour.RetreatHealthFraction = 1f;
            service.Send(thornwood);

            TickUntilHome();

            Assert.That(service.Run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Recalled));
            Assert.That(session.Hero.Corpse.Exists, Is.False);
            Assert.That(session.World.VendorSupply.StoredPackages, Is.Not.Empty);
        }

        [Test]
        public void Tick_InTown_AdvancesNoEncounter()
        {
            service.Tick(1f);

            Assert.That(service.Run.Encounter, Is.Null);
        }

        [Test]
        public void TheBehaviourProfile_IsTheHeros_AndLive()
        {
            Assert.That(session.Hero.Behaviour.SimSpeed, Is.EqualTo(Mathf.Max(1f, config.SimSpeed)));
            Assert.That(session.Hero.Behaviour.Engagement, Is.EqualTo(Mathf.Max(1, config.Engagement)));
        }

        // ── Death and recovery ───────────────────────────────────────────────

        // The hero first arrives (one spawn delay), then the fight finds it down.
        private void TickUntilHome()
        {
            for (var i = 0; i < 100 && service.Run.Phase == RunPhase.InField; i++)
                service.Tick(0.5f);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InTown), "the Run never ended");
        }

        private ItemInstance PutInBag()
        {
            var item = items.RollEquipment(EquipmentType.Chest);
            var package = new Package(null, item, 1u);
            Assert.That(session.Hero.Inventory.TryAddToContainer(ref package), Is.True);
            return item;
        }

        private static IEnumerable<ItemInstance> NonCurrencyInBag(Hero hero) =>
            hero.Inventory.StoredPackages
                .Where(entry => hero.Inventory.ViewOf(entry.Value.Item).Definition.Category != ItemCategory.Currency)
                .Select(entry => entry.Value.Item);

        [Test]
        public void ADeath_BuriesTheBagOnTheHerosCorpse_RevivesAndReturnsToTown()
        {
            var hero = session.Hero;
            _ = PutInBag();
            service.Send(thornwood);

            hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(service.Run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(hero.Corpse.Exists, Is.True);
            Assert.That(hero.Corpse.Items.Count, Is.EqualTo(1));
            Assert.That(NonCurrencyInBag(hero), Is.Empty);
            Assert.That(hero.IsDead, Is.False, "the settlement revives the hero");
            Assert.That(service.LootFlow, Is.Null);
        }

        [Test]
        public void ADeath_DoesNotRestockTheShops()
        {
            session.World.VendorSupply.RemoveAll();
            service.Send(thornwood);

            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            Assert.That(session.World.VendorSupply.StoredPackages, Is.Empty);
        }

        // A recovered piece of gear goes through the acquisition entry point, so it auto-equips into
        // an empty slot exactly as a fresh Drop does - held means in the bag or on the hero.
        private static bool Holds(Hero hero, ItemInstance item) =>
            hero.Inventory.StoredPackages.Any(entry => entry.Value.Item == item)
            || hero.Equipment.StoredPackages.Any(entry => entry.Value.Item == item);

        [Test]
        public void SendingBackToTheCorpsesLocation_GivesItBackToTheHero_AndClearsIt()
        {
            var hero = session.Hero;
            var item = PutInBag();
            service.Send(thornwood);
            hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            service.Send(thornwood);

            Assert.That(hero.Corpse.Exists, Is.False);
            Assert.That(Holds(hero, item), Is.True);
        }

        [Test]
        public void SendingElsewhere_LeavesTheCorpseStanding()
        {
            var hero = session.Hero;
            _ = PutInBag();
            service.Send(thornwood);
            hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            service.Send(ashfen);

            Assert.That(hero.Corpse.Exists, Is.True);
        }

        // ── reaching it ──────────────────────────────────────────────────────

        [Test]
        public void TheService_IsRegisteredUnderItsInterface_AtBoot()
        {
            var registry = GameBoot.Build(config);

            Assert.That(registry.Get<ISimulationService>(), Is.InstanceOf<SimulationService>());
        }
    }
}
