using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// With <see cref="EncounterTuning.DelayFirstSpawn"/> the first Encounter does not have its
    /// bodies at open: the hero waits one spawn delay — the Location's own
    /// <c>SpawnInterval ± SpawnJitter</c> — and the opening batch arrives then. Nothing fights
    /// while it waits. Without the flag nothing changes (every other test relies on that).
    /// </summary>
    [TestFixture]
    public sealed class ArrivalDelayTests
    {
        private static EncounterProfile Profile(float spawnInterval = 1f, float spawnJitter = 0.5f) => new(
            sourceLevel: 3,
            packed: EnemyArchetype.Skirmisher,
            rosterBrute: new IntRange(0),
            rosterSkirmisher: new IntRange(4),
            packBatch: new IntRange(1),
            packedSpawnWeight: 1f,
            spawnInterval: spawnInterval,
            table: FakeLootTable.ForCategory(ItemCategory.Equipment),
            spawnJitter: spawnJitter,
            initialSpawn: 2);

        private static FakeHero StrikingHero() => new()
        {
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
        };

        private static EncounterTuning Delayed() => new() { DelayFirstSpawn = true, CastCadence = 0.05f };

        private static EncounterSimulation Sim(float roll, EncounterProfile profile = null, IHeroCombatant hero = null) =>
            new(hero ?? StrikingHero(), profile ?? Profile(), new ConstantRollSource(roll), Behaviours.Engaging(5), Delayed());

        private static void Run(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++) sim.Advance(0.1f);
        }

        [Test]
        public void ByDefault_TheBodiesAreThereAtOpen()
        {
            var sim = new EncounterSimulation(StrikingHero(), Profile(), new ConstantRollSource(0f), Behaviours.Engaging(5));

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Fighting));
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(2));
            Assert.That(sim.CurrentEncounter, Is.EqualTo(1));
        }

        [Test]
        public void WithTheFlag_TheSimOpensArriving_WithNoBodies()
        {
            var sim = Sim(roll: 0.5f);

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Arriving));
            Assert.That(sim.IsArriving, Is.True);
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(0));
            Assert.That(sim.CurrentEncounter, Is.EqualTo(0), "no Encounter has begun yet");
        }

        [Test]
        public void TheOpeningBatch_ArrivesAfterTheSpawnDelay_NotBefore()
        {
            var sim = Sim(roll: 0.5f); // jitter term 0 → exactly SpawnInterval (1 s)

            Run(sim, 5);
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(0), "half the delay in — still quiet");

            Run(sim, 11); // well past 1 s
            Assert.That(sim.Phase, Is.Not.EqualTo(SimulationPhase.Arriving));
            Assert.That(sim.CurrentEncounter, Is.EqualTo(1));
            Assert.That(sim.EnemiesDefeated + sim.AliveEnemyCount, Is.GreaterThan(0));
        }

        [Test]
        public void TheDelayIsTheLocationsIntervalJittered_LowRollShorter_HighRollLonger()
        {
            // interval 1 s, jitter 0.5 s: roll 0 → 0.5 s, roll 1 → 1.5 s.
            var early = Sim(roll: 0f);
            var late = Sim(roll: 1f);

            Run(early, 3);
            Run(late, 3);
            Assert.That(early.IsArriving, Is.True, "0.3 s is under even the shortest delay");
            Assert.That(late.IsArriving, Is.True);

            Run(early, 4); // 0.7 s — past 0.5
            Run(late, 4);
            Assert.That(early.IsArriving, Is.False, "the low roll arrived by 0.7 s");
            Assert.That(late.IsArriving, Is.True, "the high roll is still waiting at 0.7 s");

            Run(late, 10); // 1.7 s — past 1.5
            Assert.That(late.IsArriving, Is.False);
        }

        [Test]
        public void ArrivingBodies_AreAnnouncedThroughEnemySpawned()
        {
            var sim = new EncounterSimulation(new FakeHero { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f, AttackSpeed = 0.01f },
                Profile(), new ConstantRollSource(0.5f), Behaviours.Engaging(5), Delayed());
            var spawned = 0;
            sim.EnemySpawned += _ => spawned++;

            Run(sim, 5);
            Assert.That(spawned, Is.EqualTo(0));

            Run(sim, 11);
            Assert.That(spawned, Is.GreaterThanOrEqualTo(2), "a listener attached up front sees the opening batch arrive");
        }

        [Test]
        public void NothingFights_WhileTheHeroWaits()
        {
            var sim = Sim(roll: 1f); // the longest wait
            var strikes = 0;
            var casts = 0;
            sim.HeroStriked += () => strikes++;
            sim.HeroCast += () => casts++;

            Run(sim, 10); // 1 s of the 1.5 s wait

            Assert.That(sim.IsArriving, Is.True);
            Assert.That(strikes, Is.EqualTo(0));
            Assert.That(casts, Is.EqualTo(0));
            Assert.That(sim.EnemiesDefeated, Is.EqualTo(0));
        }

        [Test]
        public void TheDelayDoesNotApplyAgain_AfterTheBeat()
        {
            var sim = new EncounterSimulation(StrikingHero(),
                Profiles.Group(EnemyArchetype.Skirmisher, 2),   // 100 s spawn interval — a delay would show
                new ConstantRollSource(0.5f), Behaviours.Engaging(5),
                new EncounterTuning { DelayFirstSpawn = true, Beat = 0.5f, CastCadence = 0.05f });

            // The first Encounter only opens after the (100 s) delay; fast-forward through it.
            for (var i = 0; i < 2000 && sim.IsArriving; i++) sim.Advance(0.1f);
            for (var i = 0; i < 100 && sim.EncountersCleared < 1; i++) sim.Advance(0.1f);
            Assert.That(sim.EncountersCleared, Is.EqualTo(1), "setup: first Encounter cleared");

            for (var i = 0; i < 20 && sim.CurrentEncounter < 2; i++) sim.Advance(0.1f); // beat is 0.5 s

            Assert.That(sim.CurrentEncounter, Is.EqualTo(2), "the next Encounter opens after the beat alone");
            Assert.That(sim.AliveEnemyCount, Is.GreaterThan(0));
        }

        [Test]
        public void Abandon_WhileArriving_EndsTheSim()
        {
            var sim = Sim(roll: 0.5f);

            sim.Abandon();

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(sim.Advance(5f), Is.EqualTo(0));
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(0), "no bodies arrive after an exit");
        }

        [Test]
        public void ARunSentWithTheDelay_HasNoBodiesYet_ButIsInField()
        {
            var hero = StrikingHero();
            var run = new RunState(p => new EncounterSimulation(hero, p, new ConstantRollSource(0.5f), Behaviours.Engaging(5), Delayed()));

            run.Send(Profile());

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField));
            Assert.That(run.Encounter.AliveEnemyCount, Is.EqualTo(0));
        }
    }
}
