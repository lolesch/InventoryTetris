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

        // ── the Unlock graph: only an Active Location can be Sent or Relocated to ──

        private void Unlock(Unlockable source, Unlockable target, bool whenComplete)
        {
            var so = new SerializedObject(source);
            var links = so.FindProperty(whenComplete ? "unlocksWhenComplete" : "unlocksWhenActive");
            links.arraySize++;
            links.GetArrayElementAtIndex(links.arraySize - 1).objectReferenceValue = target;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The service reads the authored graph when it is built, so links go in first.
        private SimulationService GameOver(params LocationConfig[] authored)
        {
            TestLocations.Author(config, authored);
            return new SimulationService(session, items, inventory, config, new SessionBuilderTests.FixedRolls(0.5f));
        }

        [Test]
        public void OnAFreshHero_TheRootIsActive_AndWhatItUnlocksWhenCompleteIsLocked()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);

            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Active));
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Locked));
        }

        [Test]
        public void WithTheSourceCompleted_ItIsComplete_AndWhatItUnlocksIsActive()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);

            session.Hero.MarkCompleted("thornwood");

            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active));
        }

        [Test]
        public void AnUnlockableNamedOnlyByALockedSource_IsHidden()
        {
            var gravel = Location("gravel");
            Unlock(thornwood, ashfen, whenComplete: true);
            Unlock(ashfen, gravel, whenComplete: true);
            var game = GameOver(thornwood, ashfen, gravel);

            Assert.That(game.StateOf(gravel), Is.EqualTo(UnlockState.Hidden));
        }

        [Test]
        public void AWhenActiveLink_IsSatisfiedByACompleteSource()
        {
            Unlock(thornwood, ashfen, whenComplete: false);
            var game = GameOver(thornwood, ashfen);
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active), "Active source");

            session.Hero.MarkCompleted("thornwood");

            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active), "Complete source still satisfies it");
        }

        [Test]
        public void AWhenCompleteLink_IsNotSatisfiedByAnActiveSource()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);

            Assert.That(game.StateOf(ashfen), Is.Not.EqualTo(UnlockState.Active));
        }

        [Test]
        public void TwoSourcesAreAnOr()
        {
            var gravel = Location("gravel");
            Unlock(thornwood, gravel, whenComplete: true);
            Unlock(ashfen, gravel, whenComplete: true);
            var game = GameOver(thornwood, ashfen, gravel);

            session.Hero.MarkCompleted("ashfen");

            Assert.That(game.StateOf(gravel), Is.EqualTo(UnlockState.Active));
        }

        [Test]
        public void ASendAndARelocate_ToALockedOrHiddenLocation_AreRefused_AndChangeNothing()
        {
            var gravel = Location("gravel");
            Unlock(thornwood, ashfen, whenComplete: true);
            Unlock(ashfen, gravel, whenComplete: true);
            var game = GameOver(thornwood, ashfen, gravel);

            Assert.Throws<InvalidOperationException>(() => game.Send(ashfen), "Locked");
            Assert.Throws<InvalidOperationException>(() => game.Send(gravel), "Hidden");
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(session.Hero.SelectedLocation, Is.Null);

            game.Send(thornwood);
            var encounter = game.Run.Encounter;

            Assert.Throws<InvalidOperationException>(() => game.Relocate(ashfen), "Locked");
            Assert.Throws<InvalidOperationException>(() => game.Relocate(gravel), "Hidden");
            Assert.That(game.Run.Encounter, Is.SameAs(encounter));
            Assert.That(session.Hero.SelectedLocation, Is.SameAs(thornwood));
        }

        [Test]
        public void ASendAndARelocate_ToAnActiveLocation_Work()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);
            session.Hero.MarkCompleted("thornwood");

            game.Send(thornwood);
            game.Relocate(ashfen);

            Assert.That(session.Hero.SelectedLocation, Is.SameAs(ashfen));
        }

        [Test]
        public void CanSend_IsFalseForAClosedLocation_AndForADownHero_AndTrueOtherwise()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);

            Assert.That(game.CanSend(thornwood), Is.True, "an Active Location");
            Assert.That(game.CanSend(ashfen), Is.False, "a Locked Location");

            session.Hero.MarkCompleted("thornwood");
            Assert.That(game.CanSend(ashfen), Is.True, "opened by the Complete");

            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            Assert.That(session.Hero.IsDead, Is.True, "premise");
            Assert.That(game.CanSend(thornwood), Is.False, "the hero is down");
        }

        // ── a Location's Complete opens what it Unlocks, in the same Run ──────

        // An invincible hero clears the one-Encounter Population the test Locations field.
        private static void TickUntilComplete(SimulationService game)
        {
            for (var i = 0; i < 20000 && game.Run.Encounter.Phase != SimulationPhase.Complete; i++)
                game.Tick(0.1f);

            Assert.That(game.Run.Encounter.Phase, Is.EqualTo(SimulationPhase.Complete), "premise: the Population cleared");
        }

        [Test]
        public void ClearingThePopulation_CompletesTheLocation_AndOpensWhatItUnlocks_WithoutAReturn()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);
            session.Hero.IsInvincible = true;
            var changed = 0;
            game.ProgressionChanged += () => changed++;

            game.Send(thornwood);
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Locked), "premise");
            TickUntilComplete(game);

            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Live), "no Return needed");

            game.Relocate(ashfen);
            Assert.That(session.Hero.SelectedLocation, Is.SameAs(ashfen));
        }

        [Test]
        public void ClearingACompleteLocationAgain_ChangesNothing_AndRaisesNoSignal()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);
            session.Hero.IsInvincible = true;
            session.Hero.MarkCompleted("thornwood");
            var changed = 0;
            game.ProgressionChanged += () => changed++;

            game.Send(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active));
            Assert.That(changed, Is.EqualTo(0));
        }

        // Stories 11 and 12 (the Return after a Complete is free) and 15 (a Death never takes a Location back).
        [Test]
        public void AReturnAfterAComplete_IsFree_AndKeepsWhatTheCompleteOpened()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);
            session.Hero.IsInvincible = true;
            game.Send(thornwood);
            TickUntilComplete(game);
            var changed = 0;
            game.ProgressionChanged += () => changed++;

            _ = game.Return();

            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active));
            Assert.That(changed, Is.EqualTo(0), "the Return changes no Unlock State");
        }

        [Test]
        public void ADeathAfterAComplete_KeepsWhatItOpenedOpen_SoASendThereRecoversTheCorpse()
        {
            Unlock(thornwood, ashfen, whenComplete: true);
            var game = GameOver(thornwood, ashfen);
            var item = PutInBag();
            session.Hero.IsInvincible = true;
            game.Send(thornwood);
            TickUntilComplete(game);
            game.Relocate(ashfen);
            session.Hero.IsInvincible = false;

            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            for (var i = 0; i < 100 && game.Run.Phase == RunPhase.Live; i++)
                game.Tick(0.5f);

            Assert.That(session.Hero.Corpse.Exists, Is.True, "premise: the hero died in Ashfen");
            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(ashfen), Is.EqualTo(UnlockState.Active), "still open to go back for the Corpse");
            Assert.That(session.Hero.HasCompleted("thornwood"), Is.True);

            game.Respawn();
            game.Send(ashfen);

            Assert.That(session.Hero.Corpse.Exists, Is.False);
            Assert.That(Holds(session.Hero, item), Is.True);
        }

        // ── a Quest Completes on its objective ──────────────────────────────

        private Quest NewQuest(string id, string objectiveId) => TestQuests.Create(created, id, objectiveId);

        private SimulationService GameOver(LocationConfig[] locations, params Quest[] quests)
        {
            TestQuests.Author(config, quests);
            return GameOver(locations);
        }

        [Test]
        public void AQuestActiveBeforeItsObjectiveCompletes_CompletesWithIt_AndTheStepRaisesOneSignal()
        {
            var merchant = NewQuest("merchant", "thornwood");
            var game = GameOver(new[] { thornwood }, merchant);
            session.Hero.IsInvincible = true;
            var changed = 0;
            game.ProgressionChanged += () => changed++;
            Assert.That(game.StateOf(merchant), Is.EqualTo(UnlockState.Active), "premise: a root Quest is Active from the start");

            game.Send(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(merchant), Is.EqualTo(UnlockState.Complete));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void ALockedTownStopNamedByAQuest_IsLockedUntilTheQuestCompletes_InTheSameRun()
        {
            var vendor = TestTownStops.Create(created, "town.vendor");
            var merchant = NewQuest("merchant", "thornwood");
            Unlock(merchant, vendor, whenComplete: true);
            TestTownStops.Author(config, vendor);
            var game = GameOver(new[] { thornwood }, merchant);
            session.Hero.IsInvincible = true;
            var changed = 0;
            game.ProgressionChanged += () => changed++;
            Assert.That(game.StateOf(vendor), Is.EqualTo(UnlockState.Locked), "a fresh hero sees the Vendor greyed out");

            game.Send(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(vendor), Is.EqualTo(UnlockState.Active));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void AQuestMadeActiveByTheObjectivesOwnComplete_StaysActive_AndCompletesOnALaterComplete()
        {
            var late = NewQuest("late", "thornwood");
            Unlock(thornwood, late, whenComplete: true);
            var game = GameOver(new[] { thornwood }, late);
            session.Hero.IsInvincible = true;
            Assert.That(game.StateOf(late), Is.Not.EqualTo(UnlockState.Active), "premise: the Quest waits on Thornwood");

            game.Send(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(thornwood), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(late), Is.EqualTo(UnlockState.Active), "the Complete that opened it does not count");

            game.Relocate(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(late), Is.EqualTo(UnlockState.Complete));
        }

        [Test]
        public void AQuestWhoseObjectiveIsAQuest_CompletesInTheSameStep()
        {
            var inner = NewQuest("inner", "thornwood");
            var outer = NewQuest("outer", "inner");
            var game = GameOver(new[] { thornwood }, outer, inner);
            session.Hero.IsInvincible = true;
            var changed = 0;
            game.ProgressionChanged += () => changed++;

            game.Send(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(inner), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(outer), Is.EqualTo(UnlockState.Complete));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void AQuestMadeActiveByACascade_DoesNotCountTheCompleteThatOpenedIt()
        {
            var inner = NewQuest("inner", "thornwood");
            var after = NewQuest("after", "inner");
            Unlock(inner, after, whenComplete: true);
            var game = GameOver(new[] { thornwood }, inner, after);
            session.Hero.IsInvincible = true;

            game.Send(thornwood);
            TickUntilComplete(game);

            Assert.That(game.StateOf(inner), Is.EqualTo(UnlockState.Complete));
            Assert.That(game.StateOf(after), Is.EqualTo(UnlockState.Active));
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
        public void TheRun_IsBuiltOnFirstAsk_ThenTheWorldsOwn_AndStartsWithNoRun()
        {
            var run = service.Run;

            Assert.That(run.Phase, Is.EqualTo(RunPhase.None));
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

        // ── the pace a Hero figure walks at (ADR-0021) ───────────────────────

        [Test]
        public void TheHeroWalkingSpeed_IsHisMovementStatTimesTheConfigsScale_WithNoRunNeeded()
        {
            // The default hero's 325 at the standard 0.01 is the 3.25 arena units per second the fight walks at.
            Assert.That(service.HeroWalkingSpeed, Is.EqualTo(3.25f).Within(1e-4f));

            SetConfig("Arena.MovementSpeedScale", 0.02f);

            Assert.That(service.HeroWalkingSpeed, Is.EqualTo(6.5f).Within(1e-4f));
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
            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.Live));
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
        public void HeroContextQuickMove_DropsToTheGround_InTownAndInARun()
        {
            session.World.Context.Set(InventoryContext.Hero);
            var bag = session.Hero.Inventory;

            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.Drop), "Town has the World's ground");

            service.Send(thornwood);
            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.Drop));

            _ = service.Return();
            Assert.That(inventory.QuickMoveFor(bag).Kind, Is.EqualTo(QuickMoveIntentKind.Drop), "back in Town it is still there");
        }

        [Test]
        public void GroundContextQuickMove_PicksUpFromTheGround_InTownAndInARun()
        {
            session.World.Context.Set(InventoryContext.Ground);
            var ground = session.World.Ground;

            Assert.That(inventory.QuickMoveFor(ground).Kind, Is.EqualTo(QuickMoveIntentKind.PickUp), "Town has the World's ground");
            Assert.That(inventory.QuickMoveFor(session.Hero.Inventory).Kind, Is.EqualTo(QuickMoveIntentKind.Drop));

            service.Send(thornwood);
            Assert.That(inventory.QuickMoveFor(ground).Kind, Is.EqualTo(QuickMoveIntentKind.PickUp));
            Assert.That(inventory.QuickMoveFor(session.Hero.Inventory).Kind, Is.EqualTo(QuickMoveIntentKind.Drop));
        }

        [Test]
        public void TheTownGround_TakesADropFromTheBag_AndGivesItBackOnAClick()
        {
            var hero = session.Hero;
            hero.Equipment.autoEquip = false;
            var sword = items.RollEquipment();
            var package = new Package(null, sword, 1u);
            Assert.That(hero.Inventory.TryAddToContainer(ref package), Is.True);

            _ = DropTransaction.Run(hero.Inventory, hero.Inventory.StoredPackages.Keys.Single(), session.World.LootGround);

            Assert.That(hero.Inventory.StoredPackages, Is.Empty);
            Assert.That(session.World.Ground.PackagesOldestFirst().Select(p => p.Item), Is.EqualTo(new[] { sword }));

            Assert.That(session.World.LootGround.PickUpFromGround(sword), Is.True);

            Assert.That(session.World.Ground.StoredPackages, Is.Empty);
            Assert.That(hero.Inventory.StoredPackages.Values.Select(p => p.Item), Is.EqualTo(new[] { sword }));
        }

        [Test]
        public void TheGroundAClickReaches_IsTheLootFlowInARun_AndTheTownDeskOtherwise()
        {
            Assert.That(session.World.LootGround, Is.SameAs(session.World.GroundDesk));

            service.Send(thornwood);
            Assert.That(session.World.LootGround, Is.SameAs(service.LootFlow), "the Run counts what banks toward its take");

            _ = service.Return();
            Assert.That(session.World.LootGround, Is.SameAs(session.World.GroundDesk));
        }

        [Test]
        public void Send_WipesTheTownGround()
        {
            _ = session.World.LootGround.PlaceOnGround(new Package(null, items.RollEquipment(), 1u));
            Assert.That(session.World.Ground.StoredPackages, Is.Not.Empty, "fixture: something lies in Town");

            service.Send(thornwood);

            Assert.That(session.World.Ground.StoredPackages, Is.Empty);
        }

        [Test]
        public void Return_EndsTheRun_RetiresTheLootFlow_AndRestocksTheShops()
        {
            session.World.VendorSupply.RemoveAll();
            service.Send(thornwood);

            _ = service.Return();

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.None));
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

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.Live));
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

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.None));
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
        public void Tick_DoesNotRecall_OnItsOwn()
        {
            session.Hero.IsInvincible = true;
            service.Send(thornwood);

            for (var i = 0; i < 100; i++)
                service.Tick(0.5f);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.Live), "a Run ends only by a Return or a Death");
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
        public void WalkDelta_ScalesRealTime_ByTheSimSpeed_AndIgnoresPauseAndHold()
        {
            session.Hero.Behaviour.SimSpeed = 4f;

            Assert.That(service.WalkDelta(0.1f), Is.EqualTo(0.4f).Within(1e-5f), "in Town too, where no Run is live");

            service.Send(thornwood);
            service.SetPaused(true);
            service.SetMenuFreeze(true);

            Assert.That(service.SimDelta(0.1f), Is.EqualTo(0f), "premise: the Run is frozen");
            Assert.That(service.WalkDelta(0.1f), Is.EqualTo(0.4f).Within(1e-5f), "a shown walk goes on");
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

            service.SetMenuFreeze(true);
            service.Tick(0.1f);

            Assert.That(service.IsMenuFrozen, Is.True);
            Assert.That(service.Run.Encounter.Duration, Is.EqualTo(duration), "the Encounter's clock froze");
            Assert.That(service.SimDelta(0.1f), Is.EqualTo(0f), "views on sim time freeze with it");

            service.SetMenuFreeze(false);
            service.Tick(0.1f);

            Assert.That(service.IsMenuFrozen, Is.False);
            Assert.That(service.Run.Encounter.Duration, Is.GreaterThan(duration));
        }

        [Test]
        public void ARunPausedBeforeTheHold_IsStillPausedAfterTheRelease_AndOneRunningResumes()
        {
            service.Send(thornwood);
            service.SetPaused(true);

            service.SetMenuFreeze(true);
            service.SetMenuFreeze(false);

            Assert.That(service.IsPaused, Is.True, "the player's own Pause is given back as found");

            service.SetPaused(false);
            service.SetMenuFreeze(true);
            service.SetMenuFreeze(false);

            Assert.That(service.IsPaused, Is.False);
            Assert.That(service.SimDelta(0.1f), Is.GreaterThan(0f));
        }

        [Test]
        public void TheHold_NeverChangesIsPaused_OrRaisesPausedChanged()
        {
            service.Send(thornwood);
            var raised = new List<bool>();
            service.PausedChanged += raised.Add;

            service.SetMenuFreeze(true);
            Assert.That(service.IsPaused, Is.False);
            service.SetMenuFreeze(false);

            service.SetPaused(true);
            raised.Clear();
            service.SetMenuFreeze(true);
            Assert.That(service.IsPaused, Is.True);
            service.SetMenuFreeze(false);

            Assert.That(raised, Is.Empty);
        }

        [Test]
        public void HoldingAndReleasingInTown_DoesNothingAndDoesNotThrow()
        {
            Assert.DoesNotThrow(() => service.SetMenuFreeze(true));
            Assert.That(service.IsMenuFrozen, Is.False, "there is nothing to hold in Town");
            Assert.DoesNotThrow(() => service.SetMenuFreeze(false));

            service.SetMenuFreeze(true);
            service.Send(thornwood);

            Assert.That(service.IsMenuFrozen, Is.False, "a refused hold does not wait for the next Run");
            Assert.That(service.SimDelta(0.1f), Is.GreaterThan(0f));
        }

        [Test]
        public void HoldAndRelease_AreIdempotent()
        {
            service.Send(thornwood);

            service.SetMenuFreeze(true);
            service.SetMenuFreeze(true);
            service.SetMenuFreeze(false);

            Assert.That(service.IsMenuFrozen, Is.False, "a second hold is the same hold");

            service.SetMenuFreeze(false);

            Assert.That(service.IsMenuFrozen, Is.False);
        }

        [Test]
        public void TheRunsEnd_ReleasesTheHold()
        {
            service.Send(thornwood);
            service.SetMenuFreeze(true);

            _ = service.Return();
            service.Send(thornwood);

            Assert.That(service.IsMenuFrozen, Is.False, "a hold left over from the last Run does not freeze the next");
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
            for (var i = 0; i < 100 && service.Run.Phase == RunPhase.Live; i++)
                service.Tick(0.5f);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.None), "the Run never ended");
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

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.None));
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
        public void Respawn_OnALivingHero_RaisesNothing()
        {
            var raised = 0;
            service.Respawned += () => raised++;

            service.Respawn();

            Assert.That(raised, Is.Zero);
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
        public void ASend_BeforeRespawn_IsRefused_AndPaysNoSecondPenaltyNorOverwritesTheCorpse()
        {
            var hero = session.Hero;
            _ = PutInBag();
            service.Send(thornwood);
            hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
            var corpseItems = hero.Corpse.Items.Count;
            var experience = hero.GetResource(StatName.Experience).CurrentValue;
            var balance = hero.Wallet.Balance.Total;
            var settled = 0;
            service.RunSettled += _ => settled++;

            Assert.Throws<InvalidOperationException>(() => service.Send(ashfen), "the hero is still down");
            service.Tick(0.5f);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.None), "no new Run");
            Assert.That(hero.SelectedLocation, Is.SameAs(thornwood), "the Location the Death was judged against");
            Assert.That(hero.Corpse.Items.Count, Is.EqualTo(corpseItems));
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.EqualTo(experience));
            Assert.That(hero.Wallet.Balance.Total, Is.EqualTo(balance));
            Assert.That(settled, Is.Zero, "no second Death settled");
        }

        [Test]
        public void ASend_AfterRespawn_StartsANewRun()
        {
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            service.Respawn();
            service.Send(ashfen);

            Assert.That(service.Run.Phase, Is.EqualTo(RunPhase.Live));
            Assert.That(session.Hero.SelectedLocation, Is.SameAs(ashfen));
        }

        [Test]
        public void ARelocate_BeforeRespawn_IsRefused()
        {
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();

            Assert.Throws<InvalidOperationException>(() => service.Relocate(ashfen));

            Assert.That(session.Hero.SelectedLocation, Is.SameAs(thornwood));
        }

        [Test]
        public void ASecondDeath_CannotArrive_DuringTheFirstDeathsBeat()
        {
            var hero = session.Hero;
            _ = PutInBag();
            service.Send(thornwood);
            hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
            var settled = 0;
            service.RunSettled += _ => settled++;
            Assert.That(service.DeathBeatPlaying, Is.True, "premise: inside the beat");

            Assert.Throws<InvalidOperationException>(() => service.Send(ashfen));
            for (var i = 0; i < 10; i++)
                service.Tick(0.1f);

            Assert.That(settled, Is.Zero);
            Assert.That(hero.Corpse.Items.Count, Is.EqualTo(1), "the first Corpse still holds the bag");
        }

        // ── the Death slow-down ──────────────────────────────────────────────

        // Tuned at 0.25x for 1.5 s of real time (the GameConfig defaults).
        private void DieAndSettle()
        {
            service.Send(thornwood);
            session.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome();
        }

        [Test]
        public void ADeath_SlowsTheViewsTime_ForTheTunedRealTime_ThenBackToNormal()
        {
            Assert.That(service.ViewTimeFactor, Is.EqualTo(1f), "no Death yet");

            DieAndSettle();

            Assert.That(service.ViewTimeFactor, Is.EqualTo(0.25f));
            Assert.That(service.SimDelta(1f), Is.EqualTo(0.25f).Within(1e-5f));

            service.Tick(1f);
            Assert.That(service.SimDelta(1f), Is.EqualTo(0.25f).Within(1e-5f), "1 s of the 1.5 s has passed");

            service.Tick(0.6f);
            Assert.That(service.ViewTimeFactor, Is.EqualTo(1f));
            Assert.That(service.SimDelta(1f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TheDeathSlowDown_MultipliesTheSimSpeed_AndNeverWritesIt()
        {
            session.Hero.Behaviour.SimSpeed = 4f;

            DieAndSettle();

            Assert.That(service.SimDelta(1f), Is.EqualTo(1f).Within(1e-5f), "4x speed, slowed to a quarter");
            service.Tick(5f);
            Assert.That(session.Hero.Behaviour.SimSpeed, Is.EqualTo(4f));
            Assert.That(service.SimDelta(1f), Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void Respawn_ClearsTheDeathSlowDown_Early()
        {
            DieAndSettle();

            service.Respawn();

            Assert.That(service.ViewTimeFactor, Is.EqualTo(1f));
            Assert.That(service.SimDelta(1f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void AHeroLoad_ClearsTheDeathSlowDown_Early()
        {
            DieAndSettle();

            Assert.That(session.TryLoad(GameBoot.Load().DefaultHero), Is.True);

            Assert.That(service.ViewTimeFactor, Is.EqualTo(1f));
        }

        // ── the Death beat ───────────────────────────────────────────────────

        [Test]
        public void TheDeathBeat_PlaysFromTheDeath_UntilTheSlowDownRunsOut_ThenSaysSoOnce()
        {
            var ended = 0;
            service.DeathBeatEnded += () => ended++;
            Assert.That(service.DeathBeatPlaying, Is.False, "no Death yet");

            DieAndSettle();

            Assert.That(service.DeathBeatPlaying, Is.True);
            service.Tick(1f);
            Assert.That(ended, Is.Zero, "1 s of the 1.5 s has passed");

            service.Tick(0.6f);
            service.Tick(1f);

            Assert.That(service.DeathBeatPlaying, Is.False);
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void TheDeathBeat_AlreadyPlays_WhenTheCorpseIsLaid_SoItsMarkerCanWait()
        {
            var playingWhenLaid = false;
            _ = PutInBag();
            service.CorpseChanged += () => playingWhenLaid = service.DeathBeatPlaying;

            DieAndSettle();

            Assert.That(playingWhenLaid, Is.True);
        }

        [Test]
        public void Respawn_EndsTheDeathBeat_Early_AndSaysSo()
        {
            var ended = 0;
            DieAndSettle();
            service.DeathBeatEnded += () => ended++;

            service.Respawn();
            service.Respawn();

            Assert.That(service.DeathBeatPlaying, Is.False);
            Assert.That(ended, Is.EqualTo(1), "once, for the beat that was playing");
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

        // Held means in the bag or on the hero.
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
        public void ADeath_StripsTheHeroBare_ToTheCorpse_CoinsIncluded()
        {
            var hero = session.Hero;
            var worn = items.RollEquipment(EquipmentType.Chest);
            var package = new Package(null, worn, 1u);
            Assert.That(hero.Equipment.AutoEquip(ref package), Is.True);
            _ = PutInBag();
            _ = hero.Wallet.Deposit(new Currency(500u));
            Assert.That(hero.Wallet.Balance.Total, Is.GreaterThan(0u), "the test hero has coins to lose");

            DieAndSettle();

            Assert.That(hero.Equipment.StoredPackages, Is.Empty);
            Assert.That(hero.Inventory.StoredPackages, Is.Empty);
            Assert.That(hero.Wallet.Balance.Total, Is.Zero);
            Assert.That(hero.Corpse.Items, Has.Member(worn));
        }

        [Test]
        public void Recovery_PutsEverythingBackWhereItLay_ABagItemStaysInTheBagEvenWithAFreeSlot()
        {
            var hero = session.Hero;
            var worn = items.RollEquipment(EquipmentType.Chest);
            var wornPackage = new Package(null, worn, 1u);
            Assert.That(hero.Equipment.AutoEquip(ref wornPackage), Is.True);
            var wornCell = hero.Equipment.StoredPackages.Single().Key;

            var carried = items.RollEquipment(EquipmentType.Helm);
            var carriedPackage = new Package(null, carried, 1u);
            var carriedCell = new Vector2Int(2, 1);
            Assert.That(hero.Inventory.TryAddAtPosition(carriedCell, ref carriedPackage), Is.True);

            _ = hero.Wallet.Deposit(new Currency(500u));
            var balance = hero.Wallet.Balance.Total;

            DieAndSettle();
            service.Respawn();
            service.Send(thornwood);

            Assert.That(hero.Corpse.Exists, Is.False);
            Assert.That(hero.Equipment.StoredPackages[wornCell].Item, Is.SameAs(worn));
            Assert.That(hero.Inventory.StoredPackages[carriedCell].Item, Is.SameAs(carried));
            Assert.That(hero.Equipment.StoredPackages.Values.Select(stored => stored.Item), Has.No.Member(carried),
                "the bag item did not equip itself into the free slot");
            Assert.That(hero.Wallet.Balance.Total, Is.EqualTo(balance));
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

        // ── the walk in Town (ADR-0021) ──────────────────────────────────────

        [Test]
        public void HeroWalkingSpeed_IsPositiveInTown_AndRisesInStepWithTheMovementStat()
        {
            var stat = session.Hero.GetStat(StatName.MovementSpeed);
            var before = service.HeroWalkingSpeed;
            var statBefore = stat.TotalValue;
            Assert.That(before, Is.GreaterThan(0f), "premise: the default hero walks");

            stat.AddModifier(new StatModifier(new Vector2Int(0, 1000), 100f));

            Assert.That(service.HeroWalkingSpeed / before, Is.EqualTo((statBefore + 100f) / statBefore).Within(1e-4f));
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
