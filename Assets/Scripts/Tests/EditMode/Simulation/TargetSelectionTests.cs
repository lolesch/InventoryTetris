using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Targeting is minimal and deterministic (ADR-0010): the Strike hits the single lowest-HP
    /// enemy, the Cast each of the <c>CastTargets</c> highest-HP enemies, no RNG. With Brutes
    /// (bulky) and Skirmishers (fragile) in the same fight this means the Strike picks off
    /// Skirmishers and the Cast grinds the Brute pack.
    /// </summary>
    [TestFixture]
    public sealed class TargetSelectionTests
    {
        // 2 Brutes + 2 Skirmishers, all present from t=0, no further spawns.
        private static EncounterProfile MixedQuad() => new(
            sourceLevel: 5,
            packed: EnemyArchetype.Brute,
            rosterBrute: new IntRange(2),
            rosterSkirmisher: new IntRange(2),
            packBatch: new IntRange(2),
            packedSpawnWeight: 1f,
            spawnInterval: 1f,
            table: FakeLootTable.ForCategory(ItemCategory.Equipment),
            spawnJitter: 0f,
            initialSpawn: 4);

        private static EncounterSimulation NewSim(FakeHero hero) => new(
            hero, MixedQuad(), new ConstantRollSource(0f), engagementTarget: 10,
            new EncounterTuning { CastCadence = 0.05f });

        private static FakeHero StrikerCaster() => new()
        {
            AttackSpeed = 10f,     // Strike every tick
            PhysicalDamage = 5f,
            MagicalDamage = 3f,
            CastCost = 10f,
            MaxResource = 1000f,
            Resource = 1000f,
        };

        [Test]
        public void Strike_HitsTheLowestHealthEnemy_ASkirmisher()
        {
            var sim = NewSim(StrikerCaster());
            var skirmishers = sim.Enemies.Where(e => e.Archetype == EnemyArchetype.Skirmisher).ToList();

            sim.Advance(0.1f); // one tick

            // One Skirmisher is the Strike target; the other is the Cast's 3rd target
            // (the Strike ran first, dropping its target below the other Skirmisher).
            var losses = skirmishers.Select(e => e.MaxHealth - e.Health).OrderBy(x => x).ToList();
            Assert.That(losses[0], Is.EqualTo(3f).Within(0.001f), "Cast's 3rd target — MagicalDamage");
            Assert.That(losses[1], Is.EqualTo(5f).Within(0.001f), "Strike target — PhysicalDamage");
        }

        [Test]
        public void Cast_HitsTheThreeHighestHealthEnemies_BothBrutesAndOneSkirmisher()
        {
            var sim = NewSim(StrikerCaster());
            var brutes = sim.Enemies.Where(e => e.Archetype == EnemyArchetype.Brute).ToList();

            sim.Advance(0.1f);

            foreach (var brute in brutes)
                Assert.That(brute.MaxHealth - brute.Health, Is.EqualTo(3f).Within(0.001f),
                    "each Brute took exactly one Cast, no Strike");
        }

        [Test]
        public void Strike_ClearsEverySkirmisherBeforeAnyBrute()
        {
            var hero = StrikerCaster();
            hero.PhysicalDamage = 20f; // burst the Strike target
            hero.MagicalDamage = 1f;   // barely erode the Brutes
            var sim = new EncounterSimulation(hero, MixedQuad(), new ConstantRollSource(0f), engagementTarget: 10,
                new EncounterTuning { CastCadence = 0.05f });

            var order = new List<EnemyArchetype>();
            sim.EnemyDefeated += e => order.Add(e.Archetype);

            for (var i = 0; i < 2000 && sim.Phase != SimulationPhase.Ended && sim.EncountersCleared == 0; i++)
                sim.Advance(0.1f);

            var firstBrute = order.IndexOf(EnemyArchetype.Brute);
            var lastSkirmisher = order.LastIndexOf(EnemyArchetype.Skirmisher);
            Assert.That(lastSkirmisher, Is.LessThan(firstBrute), "every Skirmisher fell before the first Brute");
        }

        [Test]
        public void TargetSelection_IsDeterministic_GivenTheSameInputs()
        {
            List<float> Run()
            {
                var sim = NewSim(StrikerCaster());
                var trace = new List<float>();
                for (var i = 0; i < 40; i++)
                {
                    sim.Advance(0.1f);
                    foreach (var e in sim.Enemies) trace.Add(e.Health);
                    trace.Add(-1f); // tick separator
                }
                return trace;
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
