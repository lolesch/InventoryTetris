using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="EncounterSimulation.HeroTarget"/> (issues #182, #209) is the target of the hero's latest Strike or
    /// Approach Intent: with the auto-hero Brain, the sticky weighted-proximity target, the earliest spawned on a tie,
    /// so the arena's target highlight cannot disagree with the Strike that follows. It is read after a tick, which is
    /// when the hero's Brain decides. These fights run on the collapsed arena, where every enemy stands on the hero
    /// and scores alike; where they stand apart is <c>HeroTargetingTests</c>.
    /// </summary>
    [TestFixture]
    public sealed class HeroTargetTests
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
        public void HeroTarget_IsTheEnemyTheStrikeHit()
        {
            var sim = NewSim(Striker());
            var before = sim.Enemies.ToDictionary(e => e, e => e.Health);

            sim.Advance(0.1f); // one tick, one Strike
            var target = sim.HeroTarget;

            Assert.That(target, Is.Not.Null);
            Assert.That(before[target] - target.Health, Is.EqualTo(StrikeDamage * (1f - target.ArmorPercent * 0.01f)).Within(0.001f),
                "the named enemy took the Strike");
            foreach (var other in sim.Enemies.Where(e => e != target))
                Assert.That(other.Health, Is.EqualTo(before[other]).Within(0.001f),
                    "negative control: nobody else took damage, so the target was the one hit");
        }

        [Test]
        public void HeroTarget_IsNull_BeforeTheFirstTickHasDecided()
        {
            Assert.That(NewSim(Striker()).HeroTarget, Is.Null);
        }

        [Test]
        public void HeroTarget_IsNotTheLowestHealthEnemy_AFragileSkirmisherDoesNotOutrankTheFirstSpawn()
        {
            var sim = NewSim(Striker());

            sim.Advance(0.1f);
            var target = sim.HeroTarget;

            Assert.That(sim.Enemies.Min(e => e.Health), Is.LessThan(sim.Enemies[0].Health), "premise: a weaker enemy is on the field");
            Assert.That(target, Is.SameAs(sim.Enemies[0]));
            Assert.That(target.Archetype, Is.EqualTo(EnemyArchetype.Brute));
        }

        [Test]
        public void HeroTarget_OnATie_IsTheEarliestSpawned()
        {
            var sim = NewSim(Striker());
            var skirmishers = sim.Enemies.Where(e => e.Archetype == EnemyArchetype.Skirmisher).ToList();
            Assert.That(skirmishers[0].Health, Is.EqualTo(skirmishers[1].Health), "premise: a tie on health");

            sim.Advance(0.1f);

            Assert.That(sim.HeroTarget, Is.SameAs(sim.Enemies[0]), "every score ties on the collapsed arena");
        }

        [Test]
        public void HeroTarget_IsStickyOnceChosen_AHurtBystanderDoesNotTakeIt()
        {
            var sim = NewSim(Striker());
            sim.Advance(0.1f);
            var target = sim.HeroTarget;

            sim.Enemies.Last().ReceivePhysical(1f);
            sim.Advance(0.1f);

            Assert.That(sim.HeroTarget, Is.SameAs(target), "health no longer decides it");
        }

        [Test]
        public void HeroTarget_SkipsAFallenEnemy_OnTheNextTick()
        {
            var sim = NewSim(Striker());
            sim.Advance(0.1f);
            var first = sim.HeroTarget;

            first.ReceivePhysical(first.MaxHealth * 100f);
            sim.Advance(0.1f);

            Assert.That(first.IsDown, "premise: the enemy is down");
            Assert.That(sim.HeroTarget, Is.Not.SameAs(first).And.Not.Null);
            Assert.That(sim.HeroTarget.IsDown, Is.False);
        }

        [Test]
        public void HeroTarget_IsNull_WithNoEnemyInTheEncounter()
        {
            var sim = NewSim(Striker(), delayFirstSpawn: true);
            Assert.That(sim.Enemies, Is.Empty, "premise: the first spawn is still a delay away");

            sim.Advance(0.1f);

            Assert.That(sim.HeroTarget, Is.Null);
        }

        [Test]
        public void HeroTarget_IsNull_WhenEveryEnemyIsDown()
        {
            var sim = NewSim(Striker());
            sim.Advance(0.1f);
            foreach (var enemy in sim.Enemies)
                enemy.ReceivePhysical(enemy.MaxHealth * 100f);

            sim.Advance(0.1f);

            Assert.That(sim.HeroTarget, Is.Null);
        }
    }
}
