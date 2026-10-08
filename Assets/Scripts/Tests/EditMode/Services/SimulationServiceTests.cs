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

        // ── the Encounter's tuning comes from the config ─────────────────────

        private void SetConfig(string property, float value)
        {
            var so = new SerializedObject(config);
            so.FindProperty($"<{property}>k__BackingField").floatValue = value;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Send_PlaysOnTheStandardTuning_WhenTheConfigIsLeftAtItsDefaults()
        {
            service.Send(thornwood);

            var sim = service.Run.Encounter;
            var ground = GroundTuning.Standard();
            var cast = CastDefinition.Standard();

            Assert.That(sim.Ground.Radius, Is.EqualTo(ground.Radius));
            Assert.That(sim.Ground.SpawnMargin, Is.EqualTo(ground.SpawnMargin));
            Assert.That(sim.Ground.StopJitter, Is.EqualTo(ground.StopJitter));
            Assert.That(sim.Ground.BearingJitter, Is.EqualTo(ground.BearingJitter));
            Assert.That(sim.Ground.HeroStrikeRange, Is.EqualTo(ground.HeroStrikeRange));
            Assert.That(sim.Ground.MovementSpeedScale, Is.EqualTo(ground.MovementSpeedScale));
            Assert.That(sim.CastRange, Is.EqualTo(cast.Range));
            Assert.That(sim.DamageSpread, Is.EqualTo(EncounterTuning.StandardDamageSpread));
        }

        [Test]
        public void Send_BuildsTheGroundCastAndDamageSpreadFromTheConfig()
        {
            SetConfig("GroundRadius", 25f);
            SetConfig("SpawnMargin", 4f);
            SetConfig("StopJitter", 0.3f);
            SetConfig("BearingJitter", 0.75f);
            SetConfig("UnarmedStrikeRange", 2.5f);
            SetConfig("MovementSpeedScale", 0.02f);
            SetConfig("CastRange", 9f);
            SetConfig("DamageSpread", 0.5f);

            service.Send(thornwood);

            var sim = service.Run.Encounter;
            Assert.That(sim.Ground.Radius, Is.EqualTo(25f));
            Assert.That(sim.Ground.SpawnMargin, Is.EqualTo(4f));
            Assert.That(sim.Ground.StopJitter, Is.EqualTo(0.3f));
            Assert.That(sim.Ground.BearingJitter, Is.EqualTo(0.75f));
            Assert.That(sim.Ground.HeroStrikeRange, Is.EqualTo(2.5f));
            Assert.That(sim.Ground.MovementSpeedScale, Is.EqualTo(0.02f));
            Assert.That(sim.CastRange, Is.EqualTo(9f));
            Assert.That(sim.DamageSpread, Is.EqualTo(0.5f));
        }

        [Test]
        public void Send_WithAnOutOfRangeTuningValue_PullsItBackInsteadOfThrowing()
        {
            SetConfig("GroundRadius", -5f);
            SetConfig("StopJitter", 1f);
            SetConfig("CastSize", 0f);
            SetConfig("DamageSpread", 3f);

            Assert.DoesNotThrow(() => service.Send(thornwood));

            var sim = service.Run.Encounter;
            Assert.That(sim.Ground.Radius, Is.Zero);
            Assert.That(sim.Ground.StopJitter, Is.LessThan(1f));
            Assert.That(sim.DamageSpread, Is.EqualTo(1f));
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
        public void ASentRun_FightsOnTheStandardGround_WithItsEnemiesSpawnedAtTheEdge()
        {
            service = new SimulationService(session, items, inventory, config, new SessionBuilderTests.FixedRolls(0.5f),
                new SessionBuilderTests.FixedRolls(0.5f));
            service.Send(thornwood);
            var encounter = service.Run.Encounter;

            // The first bodies wait one spawn delay; the combat clock caps its ticks per advance, so step small.
            for (var i = 0; i < 400 && encounter.Enemies.Count == 0; i++)
                service.Tick(0.1f);

            Assert.That(encounter.Enemies, Is.Not.Empty, "premise: something spawned");
            var ground = GroundTuning.Standard();
            foreach (var enemy in encounter.Enemies)
                Assert.That(Submodules.Utility.Extensions.Coordinate.Distance(enemy.Position, encounter.HeroPosition),
                    Is.GreaterThan(ground.Radius - 1f), "none starts on top of the hero");
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

        // ── pause ────────────────────────────────────────────────────────────

        [Test]
        public void Pause_InTown_IsRefused()
        {
            var raised = new List<bool>();
            service.PausedChanged += raised.Add;

            service.SetPaused(true);

            Assert.That(service.IsPaused, Is.False);
            Assert.That(raised, Is.Empty);
        }

        [Test]
        public void ApausedRun_StandsStill_ThenResumesWhereItStopped()
        {
            service.Send(thornwood);
            service.Tick(0.1f);
            var resource = session.Hero.GetResource(StatName.Resource);
            _ = resource.RemoveFromCurrent(resource.TotalValue);
            var duration = service.Run.Encounter.Duration;

            service.SetPaused(true);
            service.Tick(0.1f);

            Assert.That(service.IsPaused, Is.True);
            Assert.That(service.Run.Encounter.Duration, Is.EqualTo(duration), "the Encounter's clock froze");
            Assert.That(resource.CurrentValue, Is.EqualTo(0f), "so did the Hero's regeneration");

            service.SetPaused(false);
            service.Tick(0.1f);

            Assert.That(service.Run.Encounter.Duration, Is.GreaterThan(duration));
        }

        [Test]
        public void SimDelta_ScalesRealTime_ByTheSimSpeed_AndIsZeroWhilePaused()
        {
            session.Hero.Behaviour.SimSpeed = 4f;
            service.Send(thornwood);

            Assert.That(service.SimDelta(0.1f), Is.EqualTo(0.4f).Within(1e-5f));

            service.SetPaused(true);

            Assert.That(service.SimDelta(0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void Pausing_RaisesPausedChanged_OncePerChange_AndLeavesTheSimSpeedAlone()
        {
            session.Hero.Behaviour.SimSpeed = 4f;
            service.Send(thornwood);
            var raised = new List<bool>();
            service.PausedChanged += raised.Add;

            service.SetPaused(true);
            service.SetPaused(true);
            service.SetPaused(false);

            Assert.That(raised, Is.EqualTo(new[] { true, false }));
            Assert.That(session.Hero.Behaviour.SimSpeed, Is.EqualTo(4f));
        }

        [Test]
        public void TheRunsEnd_ClearsThePause()
        {
            service.Send(thornwood);
            service.SetPaused(true);
            var raised = new List<bool>();
            service.PausedChanged += raised.Add;

            _ = service.Recall();

            Assert.That(service.IsPaused, Is.False);
            Assert.That(raised, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void ANewRun_StartsMoving_EvenAfterAPauseThatNeverGotCleared()
        {
            service.Send(thornwood);
            service.SetPaused(true);

            // Mid-Run hero load: a fresh World has no Run in the Field, so the flag outlives its Run.
            session.World.Run = null;
            Assert.That(service.IsPaused, Is.False, "a flag left from another World freezes nothing");

            service.Send(ashfen);

            Assert.That(service.IsPaused, Is.False);
        }

        private sealed class TextFieldStub : MonoBehaviour, UnityEngine.EventSystems.IUpdateSelectedHandler
        {
            public void OnUpdateSelected(UnityEngine.EventSystems.BaseEventData eventData) { }
        }

        private static void SwitchEventSystem(UnityEngine.EventSystems.EventSystem events, string message) =>
            typeof(UnityEngine.EventSystems.EventSystem)
                .GetMethod(message, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(events, null);

        [Test]
        public void ThePauseKey_IsLeftToATextFieldThatHasFocus()
        {
            var events = new GameObject("events").AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Edit Mode never runs OnEnable, which is what makes an EventSystem the current one.
            SwitchEventSystem(events, "OnEnable");
            var field = new GameObject("field", typeof(TextFieldStub));

            try
            {
                events.SetSelectedGameObject(field);
                service.Send(thornwood);

                new PauseHotkey(service, () => true).Tick(0.1f);

                Assert.That(service.IsPaused, Is.False, "the space went to the field");
                Assert.That(events.currentSelectedGameObject, Is.SameAs(field), "and the field kept its focus");

                events.SetSelectedGameObject(null);
                new PauseHotkey(service, () => true).Tick(0.1f);

                Assert.That(service.IsPaused, Is.True, "with no field focused the key pauses");
            }
            finally
            {
                SwitchEventSystem(events, "OnDisable");
                UnityEngine.Object.DestroyImmediate(events.gameObject);
                UnityEngine.Object.DestroyImmediate(field);
            }
        }

        [Test]
        public void ThePauseKey_Toggles_OnlyWhileARunIsInTheField()
        {
            var down = false;
            var hotkey = new PauseHotkey(service, () => down);

            down = true;
            hotkey.Tick(0.1f);
            Assert.That(service.IsPaused, Is.False, "Town ignores the key");

            service.Send(thornwood);
            hotkey.Tick(0.1f);
            Assert.That(service.IsPaused, Is.True);

            hotkey.Tick(0.1f);
            Assert.That(service.IsPaused, Is.False, "the same key resumes");

            down = false;
            hotkey.Tick(0.1f);
            Assert.That(service.IsPaused, Is.False, "no press, no change");
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
