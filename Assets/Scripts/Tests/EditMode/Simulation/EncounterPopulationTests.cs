using Submodules.Utility.Extensions;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// A Send fields a Population: a rolled count of Encounters. The sim begins a next Encounter only while the
    /// Population lasts; after the last clear it stands in the Complete phase, once, with nothing moving.
    /// </summary>
    [TestFixture]
    public sealed class EncounterPopulationTests
    {
        private static FakeHero OneShotHero() => new()
        {
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
            MovementSpeed = 300f, // 3 arena units a second
        };

        private static EncounterTuning ShortBeat()
        {
            var tuning = new EncounterTuning { Beat = 0.5f, CastCadence = 0.05f, Arena = ArenaTuning.Standard() };
            tuning.Arena.HeroStrikeRange = 1.5f;
            return tuning;
        }

        private static EncounterProfile Ranged(int min, int max) => new(
            sourceLevel: 5,
            packed: EnemyArchetype.Skirmisher,
            rosterBrute: new IntRange(0),
            rosterSkirmisher: new IntRange(1),
            packBatch: new IntRange(1),
            packedSpawnWeight: 1f,
            spawnInterval: 100f,
            table: FakeLootTable.ForCategory(ItemCategory.Equipment),
            initialSpawn: 1,
            population: new IntRange(min, max));

        private static EncounterSimulation Sim(int population) =>
            new(OneShotHero(), Profiles.Group(EnemyArchetype.Skirmisher, 1, population: population),
                new ConstantRollSource(0f), Behaviours.Engaging(5), ShortBeat());

        private static void RunUntilComplete(EncounterSimulation sim)
        {
            for (var i = 0; i < 500 && sim.Phase != SimulationPhase.Complete; i++) sim.Advance(0.1f);
        }

        // ─── the progress readout ────────────────────────────────────────────

        [Test]
        public void TheProgressText_ReadsTheCurrentIndexOfThePopulation()
        {
            var sim = Sim(population: 5);

            Assert.That(sim.ProgressText, Is.EqualTo("Encounter 1 of 5"));

            for (var i = 0; i < 500 && sim.EncountersCleared < 1; i++) sim.Advance(0.1f);
            for (var i = 0; i < 20 && sim.CurrentEncounter < 2; i++) sim.Advance(0.1f); // rides the beat into Encounter 2

            Assert.That(sim.ProgressText, Is.EqualTo("Encounter 2 of 5"));
        }

        [Test]
        public void WhileArriving_TheProgressText_NamesThePopulationAndNoIndex()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Group(EnemyArchetype.Skirmisher, 1, population: 4),
                new ConstantRollSource(0.5f), Behaviours.Engaging(5), new EncounterTuning { DelayFirstSpawn = true });

            Assert.That(sim.IsArriving, Is.True, "setup");
            Assert.That(sim.ProgressText, Is.EqualTo("Arriving…   4 Encounters"));
        }

        [Test]
        public void WhileArriving_APopulationOfOne_ReadsInTheSingular()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Group(EnemyArchetype.Skirmisher, 1, population: 1),
                new ConstantRollSource(0.5f), Behaviours.Engaging(5), new EncounterTuning { DelayFirstSpawn = true });

            Assert.That(sim.IsArriving, Is.True, "setup");
            Assert.That(sim.ProgressText, Is.EqualTo("Arriving…   1 Encounter"));
        }

        // ─── the roll ────────────────────────────────────────────────────────

        [Test]
        public void ThePopulation_IsRolledWithinTheLocationsRange()
        {
            var low = new EncounterSimulation(OneShotHero(), Ranged(3, 6), new ConstantRollSource(0f), Behaviours.Engaging(5));
            var high = new EncounterSimulation(OneShotHero(), Ranged(3, 6), new ConstantRollSource(1f), Behaviours.Engaging(5));

            Assert.That(low.Population, Is.EqualTo(3));
            Assert.That(high.Population, Is.EqualTo(6));
        }

        [Test]
        public void EverySend_RollsTheRangeFresh()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            var run = new RunState(p => new EncounterSimulation(OneShotHero(), p, new SeededRollSource(seen.Count + 1), Behaviours.Engaging(5)));
            for (var i = 0; i < 12; i++)
            {
                run.Send(Ranged(3, 6));
                Assert.That(run.Encounter.Population, Is.InRange(3, 6));
                seen.Add(run.Encounter.Population);
                run.Return();
            }

            Assert.That(seen.Count, Is.GreaterThan(1), "twelve Sends do not all field the same count");
        }

        [Test]
        public void ARelocate_RollsTheNewLocationsPopulation()
        {
            var run = new RunState(p => new EncounterSimulation(OneShotHero(), p, new ConstantRollSource(1f), Behaviours.Engaging(5)));
            run.Send(Ranged(2, 4));
            Assert.That(run.Encounter.Population, Is.EqualTo(4));

            run.Relocate(Ranged(7, 9));

            Assert.That(run.Encounter.Population, Is.EqualTo(9));
        }

        [Test]
        public void APopulationBelowOne_IsRejected()
        {
            Assert.That(() => Ranged(0, 3), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => Ranged(4, 3), Throws.ArgumentException);
        }

        // ─── the loop ────────────────────────────────────────────────────────

        [Test]
        public void WhileThePopulationLasts_AClearBeatsAndBeginsTheNextEncounter()
        {
            var sim = Sim(population: 3);
            var completed = 0;
            sim.LocationCompleted += () => completed++;

            for (var i = 0; i < 500 && sim.EncountersCleared < 1; i++) sim.Advance(0.1f);

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Beat));
            Assert.That(completed, Is.EqualTo(0));

            for (var i = 0; i < 20 && sim.CurrentEncounter < 2; i++) sim.Advance(0.1f);
            Assert.That(sim.CurrentEncounter, Is.EqualTo(2));
            Assert.That(sim.Population, Is.EqualTo(3));
        }

        [Test]
        public void AfterTheLastClear_TheSimIsCompleteAtOnce_WithNoBeatAndNoNextEncounter()
        {
            var sim = Sim(population: 2);

            RunUntilComplete(sim);

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Complete));
            Assert.That(sim.EncountersCleared, Is.EqualTo(2));
            Assert.That(sim.CurrentEncounter, Is.EqualTo(2), "the index stays on the last Encounter of the Population");

            for (var i = 0; i < 50; i++) sim.Advance(0.1f);
            Assert.That(sim.CurrentEncounter, Is.EqualTo(2), "no Encounter begins after the last one");
        }

        [Test]
        public void TheLocationCompletedSignal_IsRaisedExactlyOnce()
        {
            var sim = Sim(population: 2);
            var completed = 0;
            sim.LocationCompleted += () => completed++;

            RunUntilComplete(sim);
            for (var i = 0; i < 50; i++) sim.Advance(0.1f);

            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void InTheCompletePhase_NothingSpawnsAndTheHeroStandsWhereHeIs()
        {
            var sim = Sim(population: 1);
            var spawned = 0;
            sim.EnemySpawned += _ => spawned++;

            RunUntilComplete(sim);
            var where = sim.HeroPosition;
            Assert.That(where, Is.Not.EqualTo(sim.Arena.Origin), "premise: the fight drew him off the origin");
            var kills = sim.EnemiesDefeated;
            var at = sim.Duration;

            for (var i = 0; i < 100; i++) sim.Advance(0.1f);

            Assert.That(spawned, Is.EqualTo(0));
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(0));
            Assert.That(sim.EnemiesDefeated, Is.EqualTo(kills));
            Assert.That(sim.HeroPosition, Is.EqualTo(where), "he stands where he is");
            Assert.That(sim.Duration, Is.EqualTo(at), "the cadences, and the clock with them, stand still");
        }

        // ─── leaving the Complete phase ──────────────────────────────────────

        [Test]
        public void AReturn_WorksFromTheCompletePhase()
        {
            var run = NewRun();
            run.Send(Profiles.Group(EnemyArchetype.Skirmisher, 1));
            RunUntilComplete(run.Encounter);

            var result = run.Return();

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Returned));
            Assert.That(result.EncountersCleared, Is.EqualTo(1));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
        }

        [Test]
        public void ARelocate_WorksFromTheCompletePhase_AndFieldsAFreshPopulation()
        {
            var run = NewRun();
            run.Send(Profiles.Group(EnemyArchetype.Skirmisher, 1));
            RunUntilComplete(run.Encounter);
            var completed = run.Encounter;

            run.Relocate(Profiles.Group(EnemyArchetype.Skirmisher, 1, population: 2));

            Assert.That(completed.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Fighting));
            Assert.That(run.Encounter.Population, Is.EqualTo(2));
            Assert.That(run.Encounter.CurrentEncounter, Is.EqualTo(1));
        }

        [Test]
        public void ARunRelocatedFromAComplete_KeepsItsTotals()
        {
            var run = NewRun();
            run.Send(Profiles.Group(EnemyArchetype.Skirmisher, 1));
            RunUntilComplete(run.Encounter);

            run.Relocate(Profiles.Group(EnemyArchetype.Skirmisher, 1));
            RunUntilComplete(run.Encounter);

            Assert.That(run.Return().EncountersCleared, Is.EqualTo(2));
        }

        private static RunState NewRun() =>
            new(p => new EncounterSimulation(OneShotHero(), p, new ConstantRollSource(0f), Behaviours.Engaging(5), ShortBeat()));
    }
}
