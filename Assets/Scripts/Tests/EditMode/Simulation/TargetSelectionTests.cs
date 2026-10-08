using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Targeting is minimal and deterministic (ADR-0010, issue #209): the Strike hits the hero's one sticky
    /// weighted-proximity target (on the collapsed ground every enemy scores alike, so the first spawn), the
    /// Cast each of the <c>CastTargets</c> highest-HP enemies, no RNG. Health no longer steers the Strike;
    /// where the enemies stand does (<c>HeroTargetingTests</c>).
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
            hero, MixedQuad(), new ConstantRollSource(0f), Behaviours.Engaging(10),
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
        public void Strike_HitsTheStickyTarget_NotTheLowestHealthEnemy()
        {
            var sim = NewSim(StrikerCaster());
            var firstSpawn = sim.Enemies[0];
            var skirmishers = sim.Enemies.Where(e => e.Archetype == EnemyArchetype.Skirmisher).ToList();
            Assert.That(firstSpawn.Archetype, Is.EqualTo(EnemyArchetype.Brute), "premise: a Brute is the first spawn");

            sim.Advance(0.1f); // one tick

            // The Brute takes the Strike (mitigated by its Armor) and a Cast; no Skirmisher takes a Strike - only
            // one of them is the Cast's 3rd target.
            var strike = 5f * (1f - firstSpawn.ArmorPercent * 0.01f);
            Assert.That(firstSpawn.MaxHealth - firstSpawn.Health, Is.EqualTo(strike + 3f).Within(0.001f));
            var losses = skirmishers.Select(e => e.MaxHealth - e.Health).OrderBy(x => x).ToList();
            Assert.That(losses[0], Is.EqualTo(0f).Within(0.001f));
            Assert.That(losses[1], Is.EqualTo(3f).Within(0.001f), "Cast's 3rd target - MagicalDamage, not a Strike");
        }

        [Test]
        public void Cast_HitsTheThreeHighestHealthEnemies_BothBrutesAndOneSkirmisher()
        {
            var sim = NewSim(StrikerCaster());
            var brutes = sim.Enemies.Where(e => e.Archetype == EnemyArchetype.Brute).ToList();
            var struck = sim.StrikeTarget;
            var strike = 5f * (1f - struck.ArmorPercent * 0.01f);

            sim.Advance(0.1f);

            foreach (var brute in brutes)
                Assert.That(brute.MaxHealth - brute.Health - (brute == struck ? strike : 0f), Is.EqualTo(3f).Within(0.001f),
                    "each Brute took exactly one Cast, and the sticky target also the Strike");
        }

        [Test]
        public void Strike_FinishesTheFirstSpawnBeforeTurningToAFragileSkirmisher()
        {
            var hero = StrikerCaster();
            hero.PhysicalDamage = 20f; // burst the Strike target
            hero.MagicalDamage = 1f;   // barely erode the rest
            var sim = new EncounterSimulation(hero, MixedQuad(), new ConstantRollSource(0f), Behaviours.Engaging(10),
                new EncounterTuning { CastCadence = 0.05f });

            var order = new List<EnemyArchetype>();
            sim.EnemyDefeated += e => order.Add(e.Archetype);

            for (var i = 0; i < 2000 && sim.Phase != SimulationPhase.Ended && sim.EncountersCleared == 0; i++)
                sim.Advance(0.1f);

            Assert.That(order, Has.Count.EqualTo(4), "premise: the whole quad fell");
            Assert.That(order[0], Is.EqualTo(EnemyArchetype.Brute), "the Brute he started on fell first, though a Skirmisher was frailer");
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
