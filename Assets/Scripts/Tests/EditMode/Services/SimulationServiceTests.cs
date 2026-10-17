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

        private void SetConfig(string path, float value)
        {
            var so = new SerializedObject(config);
            so.FindProperty($"<Tuning>k__BackingField.{path}").floatValue = value;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The damage spread the Encounter plays on, read off the first hit it lands: the hit's reference scale
        // against the same reference taken with no spread.
        private float SpreadOfTheFirstHit()
        {
            var sim = service.Run.Encounter;
            HitEvent? first = null;
            sim.HitLanded += hit =>
            {
                // A Cast of a hero with no magic lands at a scale of 0, which shows no spread.
                if (hit.Reference > 0f)
                    first ??= hit;
            };

            for (var i = 0; i < 600 && first == null; i++)
                service.Tick(0.1f);

            Assert.That(first, Is.Not.Null, "a hit landed");
            var hit = first.Value;
            var bare = ReferenceEquals(hit.Target, sim.Hero)
                ? DamageReference.OfEnemies(sim.Profile, hit.DamageType, 0f)
                : DamageReference.OfHero(sim.Hero, hit.DamageType, 0f);
            Assert.That(bare, Is.GreaterThan(0f));
            return hit.Reference / bare - 1f;
        }

        [Test]
        public void Send_PlaysOnTheStandardTuning_WhenTheConfigIsLeftAtItsDefaults()
        {
            service.Send(thornwood);

            var sim = service.Run.Encounter;
            var arena = ArenaTuning.Standard();
            var cast = CastDefinition.Standard();

            Assert.That(sim.Arena.Radius, Is.EqualTo(arena.Radius));
            Assert.That(sim.Arena.SpawnMargin, Is.EqualTo(arena.SpawnMargin));
            Assert.That(sim.Arena.StopJitter, Is.EqualTo(arena.StopJitter));
            Assert.That(sim.Arena.BearingJitter, Is.EqualTo(arena.BearingJitter));
            Assert.That(sim.Arena.HeroStrikeRange, Is.EqualTo(arena.HeroStrikeRange));
            Assert.That(sim.Arena.MovementSpeedScale, Is.EqualTo(arena.MovementSpeedScale));
            Assert.That(sim.CastRange, Is.EqualTo(cast.Range));
            Assert.That(SpreadOfTheFirstHit(), Is.EqualTo(EncounterTuning.StandardDamageSpread).Within(1e-4f));
        }

        [Test]
        public void Send_BuildsTheArenaCastAndDamageSpreadFromTheConfig()
        {
            SetConfig("Arena.Radius", 25f);
            SetConfig("Arena.SpawnMargin", 4f);
            SetConfig("Arena.StopJitter", 0.3f);
            SetConfig("Arena.BearingJitter", 0.75f);
            SetConfig("Arena.HeroStrikeRange", 2.5f);
            SetConfig("Arena.MovementSpeedScale", 0.02f);
            SetConfig("Cast.Range", 9f);
            SetConfig("DamageSpread", 0.5f);

            service.Send(thornwood);

            var sim = service.Run.Encounter;
            Assert.That(sim.Arena.Radius, Is.EqualTo(25f));
            Assert.That(sim.Arena.SpawnMargin, Is.EqualTo(4f));
            Assert.That(sim.Arena.StopJitter, Is.EqualTo(0.3f));
            Assert.That(sim.Arena.BearingJitter, Is.EqualTo(0.75f));
            Assert.That(sim.Arena.HeroStrikeRange, Is.EqualTo(2.5f));
            Assert.That(sim.Arena.MovementSpeedScale, Is.EqualTo(0.02f));
            Assert.That(sim.CastRange, Is.EqualTo(9f));
            Assert.That(SpreadOfTheFirstHit(), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Send_WithAnOutOfRangeTuningValue_PullsItBackInsteadOfThrowing()
        {
            SetConfig("Arena.Radius", -5f);
            SetConfig("Arena.StopJitter", 1f);
            SetConfig("Cast.Size", 0f);
            SetConfig("DamageSpread", 3f);

            Assert.DoesNotThrow(() => service.Send(thornwood));

            var sim = service.Run.Encounter;
            Assert.That(sim.Arena.Radius, Is.Zero);
            Assert.That(sim.Arena.StopJitter, Is.LessThan(1f));
            Assert.That(SpreadOfTheFirstHit(), Is.EqualTo(1f).Within(1e-4f));
        }

        // ── Send / Return ────────────────────────────────────────────────────

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
        public void TheGround_IsTheWorldsOneContainer_ThroughSendRelocateAndReturn()
        {
            var ground = session.World.Ground;
            Assert.That(inventory.ContainerFor(ContainerRole.Ground), Is.SameAs(ground), "a display binds it by role");

            service.Send(thornwood);
            Assert.That(service.LootFlow.Ground, Is.SameAs(ground));
            Assert.That(service.LootFlow.PlaceOnGround(new Package(null, items.RollEquipment(), 1u)), Is.True);

            service.Relocate(ashfen);
            Assert.That(service.LootFlow.Ground, Is.SameAs(ground), "the new loot flow lays on the same container");

            _ = service.Return();
            Assert.That(ground.StoredPackages, Is.Empty, "a Drop on the ground when the Run ends is gone");
            Assert.That(inventory.ContainerFor(ContainerRole.Ground), Is.SameAs(ground));
        }

        [Test]
        public void HeroContextQuickMove_DropsToTheGround_OnlyWhileARunHasOne()
        {
            session.World.Context.Set(InventoryContext.Hero);
            var bag = session.Hero.Inventory;

            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.None), "Town has no ground (issue #63)");

            service.Send(thornwood);
            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.Drop));

            _ = service.Return();
            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.None), "the Run's end takes the ground with it");
        }

        [Test]
        public void GroundContextQuickMove_PicksUpFromTheGround_OnlyWhileARunHasOne()
        {
            session.World.Context.Set(InventoryContext.Ground);
            var ground = session.World.Ground;

            Assert.That(inventory.QuickMoveFor(ground).Kind, Is.EqualTo(QuickMoveIntentKind.None), "Town has no ground (issue #63)");

            service.Send(thornwood);
            Assert.That(inventory.QuickMoveFor(ground).Kind, Is.EqualTo(QuickMoveIntentKind.PickUp));
            Assert.That(inventory.QuickMoveFor(session.Hero.Inventory).Kind, Is.EqualTo(QuickMoveIntentKind.Drop));

            _ = service.Return();
            Assert.That(inventory.QuickMoveFor(ground).Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        [Test]
        public void Return_EndsTheRun_RetiresTheLootFlow_AndRestocksTheShops()
        {
            session.World.VendorSupply.RemoveAll();
            service.Send(thornwood);

            _ = service.Return();

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(service.LootFlow, Is.Null);
            Assert.That(session.World.VendorSupply.StoredPackages, Is.Not.Empty, "a Return brings the shops new stock");
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
        public void ASentRun_FightsOnTheStandardArena_WithItsEnemiesSpawnedAtTheEdge()
        {
            service = new SimulationService(session, items, inventory, config, new RollStreams(new SessionBuilderTests.FixedRolls(0.5f),
                new SessionBuilderTests.FixedRolls(0.5f)));
            service.Send(thornwood);
            var encounter = service.Run.Encounter;

            // The first bodies wait one spawn delay; the combat clock caps its ticks per advance, so step small.
            for (var i = 0; i < 400 && encounter.Enemies.Count == 0; i++)
                service.Tick(0.1f);

            Assert.That(encounter.Enemies, Is.Not.Empty, "premise: something spawned");
            var arena = ArenaTuning.Standard();
            foreach (var enemy in encounter.Enemies)
                Assert.That(Submodules.Utility.Extensions.Coordinate.Distance(enemy.Position, encounter.HeroPosition),
                    Is.GreaterThan(arena.Radius - 1f), "none starts on top of the hero");
        }

        [Test]
        public void Tick_AutoRecalls_WhenTheBehaviourProfileSaysRetreat_AndRestocks()
        {
            session.World.VendorSupply.RemoveAll();
            session.Hero.Behaviour.RetreatHealthFraction = 1f;
            service.Send(thornwood);

            TickUntilHome();

            Assert.That(service.Run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Returned));
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

        // ── the menu hold ────────────────────────────────────────────────────

        [Test]
        public void AHeldRun_StandsStill_ThenResumesWhereItStopped()
        {
            service.Send(thornwood);
            service.Tick(0.1f);
            var duration = service.Run.Encounter.Duration;

            service.SetHeld(true);
            service.Tick(0.1f);

            Assert.That(service.IsHeld, Is.True);
            Assert.That(service.Run.Encounter.Duration, Is.EqualTo(duration), "the Encounter's clock froze");
            Assert.That(service.SimDelta(0.1f), Is.EqualTo(0f), "views on sim time freeze with it");

            service.SetHeld(false);
            service.Tick(0.1f);

            Assert.That(service.IsHeld, Is.False);
            Assert.That(service.Run.Encounter.Duration, Is.GreaterThan(duration));
        }

        [Test]
        public void ARunPausedBeforeTheHold_IsStillPausedAfterTheRelease_AndOneRunningResumes()
        {
            service.Send(thornwood);
            service.SetPaused(true);

            service.SetHeld(true);
            service.SetHeld(false);

            Assert.That(service.IsPaused, Is.True, "the player's own Pause is given back as found");

            service.SetPaused(false);
            service.SetHeld(true);
            service.SetHeld(false);

            Assert.That(service.IsPaused, Is.False);
            Assert.That(service.SimDelta(0.1f), Is.GreaterThan(0f));
        }

        [Test]
        public void TheHold_NeverChangesIsPaused_OrRaisesPausedChanged()
        {
            service.Send(thornwood);
            var raised = new List<bool>();
            service.PausedChanged += raised.Add;

            service.SetHeld(true);
            Assert.That(service.IsPaused, Is.False);
            service.SetHeld(false);

            service.SetPaused(true);
            raised.Clear();
            service.SetHeld(true);
            Assert.That(service.IsPaused, Is.True);
            service.SetHeld(false);

            Assert.That(raised, Is.Empty);
        }

        [Test]
        public void HoldingAndReleasingInTown_DoesNothingAndDoesNotThrow()
        {
            Assert.DoesNotThrow(() => service.SetHeld(true));
            Assert.That(service.IsHeld, Is.False, "there is nothing to hold in Town");
            Assert.DoesNotThrow(() => service.SetHeld(false));

            service.SetHeld(true);
            service.Send(thornwood);

            Assert.That(service.IsHeld, Is.False, "a refused hold does not wait for the next Run");
            Assert.That(service.SimDelta(0.1f), Is.GreaterThan(0f));
        }

        [Test]
        public void HoldAndRelease_AreIdempotent()
        {
            service.Send(thornwood);

            service.SetHeld(true);
            service.SetHeld(true);
            service.SetHeld(false);

            Assert.That(service.IsHeld, Is.False, "a second hold is the same hold");

            service.SetHeld(false);

            Assert.That(service.IsHeld, Is.False);
        }

        [Test]
        public void TheRunsEnd_ReleasesTheHold()
        {
            service.Send(thornwood);
            service.SetHeld(true);

            _ = service.Return();
            service.Send(thornwood);

            Assert.That(service.IsHeld, Is.False, "a hold left over from the last Run does not freeze the next");
        }

        [Test]
        public void TheRunsEnd_ClearsThePause()
        {
            service.Send(thornwood);
            service.SetPaused(true);
            var raised = new List<bool>();
            service.PausedChanged += raised.Add;

            _ = service.Return();

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
                var input = new FakeInput();
                _ = new PauseHotkey(service, input);

                events.SetSelectedGameObject(field);
                service.Send(thornwood);

                input.Press(GameAction.TogglePause);

                Assert.That(service.IsPaused, Is.False, "the space went to the field");
                Assert.That(events.currentSelectedGameObject, Is.SameAs(field), "and the field kept its focus");

                events.SetSelectedGameObject(null);
                input.Press(GameAction.TogglePause);

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
            var input = new FakeInput();
            _ = new PauseHotkey(service, input);

            input.Press(GameAction.TogglePause);
            Assert.That(service.IsPaused, Is.False, "Town ignores the key");

            service.Send(thornwood);
            input.Press(GameAction.TogglePause);
            Assert.That(service.IsPaused, Is.True);

            input.Press(GameAction.TogglePause);
            Assert.That(service.IsPaused, Is.False, "the same key resumes");

            input.Press(GameAction.ShowDetails);
            Assert.That(service.IsPaused, Is.False, "another action, no change");
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

        // ── the lifetime Kill count ──────────────────────────────────────────

        // An invincible hero, so the Run is still there once the first body has fallen.
        private int SendAndTickUntilAKill()
        {
            session.Hero.IsInvincible = true;
            service.Send(thornwood);

            for (var i = 0; i < 2000 && service.Run.Encounter.EnemiesDefeated == 0; i++)
                service.Tick(0.1f);

            Assert.That(service.Run.Encounter.EnemiesDefeated, Is.GreaterThan(0), "premise: the hero killed something");
            return service.Run.Encounter.EnemiesDefeated;
        }

        [Test]
        public void AReturn_FoldsTheRunsKillsIntoTheHerosLifetimeCount_Once()
        {
            var kills = SendAndTickUntilAKill();
            Assert.That(session.Hero.TotalKills, Is.EqualTo(0), "a Run in the Field has not been folded yet");

            var result = service.Return();
            service.Tick(0.5f);
            service.Tick(0.5f);

            Assert.That(result.EnemiesDefeated, Is.EqualTo(kills));
            Assert.That(session.Hero.TotalKills, Is.EqualTo(kills));
        }

        [Test]
        public void ADeath_CountsTheKillsOfTheRunThatEndedInIt()
        {
            var kills = SendAndTickUntilAKill();

            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            Assert.That(service.Run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(session.Hero.TotalKills, Is.GreaterThanOrEqualTo(kills));
            Assert.That(session.Hero.TotalKills, Is.EqualTo(service.Run.LastResult?.EnemiesDefeated));
        }

        [Test]
        public void TheLifetimeCount_AddsUpAcrossRuns_AndRelocateKeepsTheRunsKillsForItsEnd()
        {
            var first = SendAndTickUntilAKill();
            service.Relocate(ashfen);
            _ = service.Return();
            Assert.That(session.Hero.TotalKills, Is.GreaterThanOrEqualTo(first), "the Relocate carried the first Encounter's kills into the Run");
            var afterFirst = session.Hero.TotalKills;

            _ = SendAndTickUntilAKill();
            _ = service.Return();

            Assert.That(session.Hero.TotalKills, Is.EqualTo(afterFirst + service.Run.LastResult?.EnemiesDefeated));
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
        public void ADeath_BuriesTheBagOnTheHerosCorpse_AndLeavesTheHeroDownInTown()
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
            Assert.That(hero.IsDead, Is.True, "the settlement does not revive; Respawn does");
            Assert.That(service.LootFlow, Is.Null);
        }

        [Test]
        public void TheHero_StaysDownThroughTownTicks_UntilRespawn()
        {
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            service.Tick(5f);

            Assert.That(session.Hero.GetResource(StatName.Health).CurrentValue, Is.Zero);
        }

        [Test]
        public void Respawn_RevivesTheHeroAtFullHealthAndResource()
        {
            var hero = session.Hero;
            service.Send(thornwood);
            hero.GetResource(StatName.Health).DepleteCurrent();
            hero.GetResource(StatName.Resource).DepleteCurrent();
            TickUntilHome();

            service.Respawn();

            var health = hero.GetResource(StatName.Health);
            var resource = hero.GetResource(StatName.Resource);
            Assert.That(hero.IsDead, Is.False);
            Assert.That(health.CurrentValue, Is.EqualTo(health.TotalValue));
            Assert.That(resource.CurrentValue, Is.EqualTo(resource.TotalValue));
        }

        [Test]
        public void Respawn_RaisesRespawned_AfterTheReviveAndOnlyOnce()
        {
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
            var seen = new List<bool>();
            service.Respawned += () => seen.Add(session.Hero.IsDead);

            service.Respawn();

            Assert.That(seen, Is.EqualTo(new[] { false }), "raised once, with the hero already up");
        }

        [Test]
        public void Respawn_KeepsTheCorpseAndWhatTheDeathCost()
        {
            var hero = session.Hero;
            _ = PutInBag();
            service.Send(thornwood);
            hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
            var experience = hero.GetResource(StatName.Experience).CurrentValue;
            var balance = hero.Wallet.Balance.Total;

            service.Respawn();

            Assert.That(hero.Corpse.Exists, Is.True);
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.EqualTo(experience));
            Assert.That(hero.Wallet.Balance.Total, Is.EqualTo(balance));
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
            service.Respawn();

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
            service.Respawn();

            service.Send(ashfen);

            Assert.That(hero.Corpse.Exists, Is.True);
        }

        // ── the Corpse marker ────────────────────────────────────────────────

        [Test]
        public void ACorpseLiesAtItsLocationOnly_UntilItIsRecovered()
        {
            Assert.That(service.CorpseLiesAt(thornwood), Is.False, "no Corpse yet");

            _ = PutInBag();
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
            service.Respawn();

            Assert.That(service.CorpseLiesAt(thornwood), Is.True);
            Assert.That(service.CorpseLiesAt(ashfen), Is.False);

            service.Send(thornwood);

            Assert.That(service.CorpseLiesAt(thornwood), Is.False, "recovered");
        }

        [Test]
        public void ASecondDeathElsewhere_MovesTheMarker()
        {
            _ = PutInBag();
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
            service.Respawn();

            service.Send(ashfen);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            Assert.That(service.CorpseLiesAt(thornwood), Is.False, "replaced");
            Assert.That(service.CorpseLiesAt(ashfen), Is.True);
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
