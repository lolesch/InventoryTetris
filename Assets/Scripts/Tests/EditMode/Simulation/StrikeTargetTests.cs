using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="EncounterSimulation.StrikeTarget"/> (issues #182, #209) is a read-only peek at the hero's
    /// own choice - the sticky weighted-proximity target, the earliest spawned on a tie - so the arena's target
    /// highlight cannot disagree with the Strike that follows. It holds no state and raises nothing. These
    /// fights run on the collapsed ground, where every enemy stands on the hero and scores alike; where they
    /// stand apart is <c>HeroTargetingTests</c>.
    /// </summary>
    [TestFixture]
    public sealed class StrikeTargetTests
    {
        private const float StrikeDamage = 5f;

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

        // Strikes every tick; no Resource, so the Cast never fires and only the Strike lands damage.
        private static FakeHero Striker() => new()
        {
            AttackSpeed = 10f,
            PhysicalDamage = StrikeDamage,
        };

        private static EncounterSimulation NewSim(FakeHero hero, bool delayFirstSpawn = false) => new(
            hero, MixedQuad(), new ConstantRollSource(0f), Behaviours.Engaging(10),
            new EncounterTuning { DelayFirstSpawn = delayFirstSpawn });

        [Test]
        public void StrikeTarget_IsTheEnemyTheNextStrikeHits()
        {
            var sim = NewSim(Striker());
            var target = sim.StrikeTarget;
            var before = sim.Enemies.ToDictionary(e => e, e => e.Health);

            sim.Advance(0.1f); // one tick, one Strike

            Assert.That(target, Is.Not.Null);
            Assert.That(before[target] - target.Health, Is.EqualTo(StrikeDamage * (1f - target.ArmorPercent * 0.01f)).Within(0.001f),
                "the peeked enemy took the Strike");
            foreach (var other in sim.Enemies.Where(e => e != target))
                Assert.That(other.Health, Is.EqualTo(before[other]).Within(0.001f),
                    "negative control: nobody else took damage, so the peek was the one hit");
        }

        [Test]
        public void StrikeTarget_IsNotTheLowestHealthEnemy_AFragileSkirmisherDoesNotOutrankTheFirstSpawn()
        {
            var sim = NewSim(Striker());

            var target = sim.StrikeTarget;

            Assert.That(sim.Enemies.Min(e => e.Health), Is.LessThan(sim.Enemies[0].Health), "premise: a weaker enemy is on the field");
            Assert.That(target, Is.SameAs(sim.Enemies[0]));
            Assert.That(target.Archetype, Is.EqualTo(EnemyArchetype.Brute));
        }

        [Test]
        public void StrikeTarget_OnATie_IsTheEarliestSpawned()
        {
            var sim = NewSim(Striker());
            var skirmishers = sim.Enemies.Where(e => e.Archetype == EnemyArchetype.Skirmisher).ToList();
            Assert.That(skirmishers[0].Health, Is.EqualTo(skirmishers[1].Health), "premise: a tie on health");

            Assert.That(sim.StrikeTarget, Is.SameAs(sim.Enemies[0]), "every score ties on the collapsed ground");
        }

        [Test]
        public void StrikeTarget_IsStickyOnceChosen_AHurtBystanderDoesNotTakeIt()
        {
            var sim = NewSim(Striker());
            var target = sim.StrikeTarget;
            sim.Advance(0.1f);

            sim.Enemies.Last().ReceivePhysical(1f);

            Assert.That(sim.StrikeTarget, Is.SameAs(target), "health no longer decides it");
        }

        [Test]
        public void StrikeTarget_SkipsAFallenEnemy()
        {
            var sim = NewSim(Striker());
            var first = sim.StrikeTarget;

            first.ReceivePhysical(first.MaxHealth * 100f);

            Assert.That(first.IsDown, "premise: the enemy is down");
            Assert.That(sim.StrikeTarget, Is.Not.SameAs(first).And.Not.Null);
            Assert.That(sim.StrikeTarget.IsDown, Is.False);
        }

        [Test]
        public void StrikeTarget_IsNull_WithNoEnemyInTheEncounter()
        {
            var sim = NewSim(Striker(), delayFirstSpawn: true);
            Assert.That(sim.Enemies, Is.Empty, "premise: the first spawn is still a delay away");

            Assert.That(sim.StrikeTarget, Is.Null);
        }

        [Test]
        public void StrikeTarget_IsNull_WhenEveryEnemyIsDown()
        {
            var sim = NewSim(Striker());
            foreach (var enemy in sim.Enemies)
                enemy.ReceivePhysical(enemy.MaxHealth * 100f);

            Assert.That(sim.StrikeTarget, Is.Null);
        }

        [Test]
        public void ReadingStrikeTarget_DoesNotMutateTheSim()
        {
            List<float> Run(bool peek)
            {
                var sim = NewSim(Striker());
                var trace = new List<float>();
                for (var i = 0; i < 40; i++)
                {
                    if (peek)
                        for (var p = 0; p < 3; p++)
                            _ = sim.StrikeTarget;

                    sim.Advance(0.1f);
                    foreach (var e in sim.Enemies) trace.Add(e.Health);
                    trace.Add(sim.EnemiesDefeated);
                    trace.Add(-1f); // tick separator
                }
                return trace;
            }

            Assert.That(Run(peek: true), Is.EqualTo(Run(peek: false)));
        }
    }
}
