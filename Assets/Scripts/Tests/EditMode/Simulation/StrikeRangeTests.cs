using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Strikes need reach (issue #207): an enemy hits the hero only while he is within its Strike Range, and
    /// walking in banks no burst; the hero's Strike only lands on a target within his own range. Each test pairs
    /// the standard arena with the collapsed default, where the same fight starts at once - the negative
    /// control that shows the range is what holds the Strike back.
    /// </summary>
    [TestFixture]
    public sealed class StrikeRangeTests
    {
        private const float Tick = 0.1f;

        private static FakeHero Passive() => new() { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f };

        // Strikes on every tick; no Resource, so the Cast never fires.
        private static FakeHero Striker() => new() { PhysicalDamage = 5f, MagicalDamage = 0f, Resource = 0f, AttackSpeed = 10f };

        private static EncounterTuning OnTheArena() => new() { Arena = ArenaTuning.Standard() };

        private static EncounterSimulation Sim(FakeHero hero, EncounterProfile profile, EncounterTuning tuning,
            IRollSource main = null, IRollSource movement = null) => new(hero, profile, main ?? new ConstantRollSource(0f),
            Behaviours.Engaging(10), tuning, bag: null, movementRolls: movement ?? new ConstantRollSource(0.5f));

        private static float Distance(EncounterSimulation sim, Enemy enemy) => Coordinate.Distance(enemy.Position, sim.HeroPosition);

        // The times (in ticks) at which the hero lost health.
        private static List<int> HitsOnTheHero(EncounterSimulation sim, FakeHero hero, int ticks)
        {
            var hits = new List<int>();
            var taken = hero.PhysicalDamageTaken;
            for (var tick = 1; tick <= ticks; tick++)
            {
                sim.Advance(Tick);
                if (hero.PhysicalDamageTaken > taken)
                    hits.Add(tick);
                taken = hero.PhysicalDamageTaken;
            }
            return hits;
        }

        [Test]
        public void AnEnemy_StrikesOnlyOnceTheHeroIsWithinItsStrikeRange()
        {
            var hero = Passive();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), OnTheArena());
            var brute = sim.Enemies.Single();

            var hits = new List<float>(); // the distance at each hit
            var taken = 0f;
            for (var i = 0; i < 150; i++)
            {
                sim.Advance(Tick);
                if (hero.PhysicalDamageTaken > taken)
                    hits.Add(Distance(sim, brute));
                taken = hero.PhysicalDamageTaken;
            }

            Assert.That(hits, Is.Not.Empty, "it does hit him once it has arrived");
            Assert.That(hits.Max(), Is.LessThanOrEqualTo(brute.StrikeRange + 0.01f), "never from outside its range");
        }

        [Test]
        public void AnEnemy_StrikesFromTheStartOnACollapsedArena_ThatIsWhatTheRangeHoldsBack()
        {
            var hero = Passive();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new EncounterTuning());

            var hits = HitsOnTheHero(sim, hero, 30);

            Assert.That(hits, Is.Not.Empty, "already in range, it strikes within its first cadence");
        }

        [Test]
        public void WalkingIn_DoesNotBankABurstOfStrikes()
        {
            var hero = Passive();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), OnTheArena());
            var brute = sim.Enemies.Single();

            var hits = HitsOnTheHero(sim, hero, 150);

            Assert.That(hits, Has.Count.GreaterThanOrEqualTo(2));
            var cadence = 1f / brute.AttackSpeed;
            Assert.That((hits[1] - hits[0]) * Tick, Is.GreaterThanOrEqualTo(cadence - 2 * Tick),
                "after the arrival Strike the next one waits a whole cadence");
        }

        [Test]
        public void ARangedEnemy_StrikesFromAStandOff_WhereTheHeroCannotReachIt()
        {
            var hero = Striker();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Skirmisher), OnTheArena());
            var skirmisher = sim.Enemies.Single();

            for (var i = 0; i < 150; i++) sim.Advance(Tick);

            Assert.That(Distance(sim, skirmisher), Is.GreaterThan(sim.HeroStrikeRange), "premise: it stands off");
            Assert.That(hero.MagicalDamageTaken, Is.GreaterThan(0f), "it hits him from there");
            Assert.That(skirmisher.Health, Is.EqualTo(skirmisher.MaxHealth), "he cannot reach it");
        }

        [Test]
        public void TheHero_CannotStrikeAnEnemyStillWalkingIn()
        {
            var sim = Sim(Striker(), Profiles.Solo(EnemyArchetype.Brute), OnTheArena());
            var brute = sim.Enemies.Single();
            var struckAt = new List<float>(); // the distance of each Strike
            var health = brute.Health;

            for (var i = 0; i < 100; i++)
            {
                sim.Advance(Tick);
                if (brute.Health < health)
                    struckAt.Add(Distance(sim, brute));
                health = brute.Health;
            }

            Assert.That(struckAt, Is.Not.Empty, "once it has arrived he strikes it");
            Assert.That(struckAt.Max(), Is.LessThanOrEqualTo(sim.HeroStrikeRange + 0.01f));
        }

        [Test]
        public void TheHero_WaitingForAnEnemyToArrive_BanksNoBurstOfStrikes()
        {
            var hero = new FakeHero { PhysicalDamage = 1f, MagicalDamage = 0f, Resource = 0f, AttackSpeed = 0.5f };
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), OnTheArena());
            var strikes = new List<int>();
            var tick = 0;
            sim.HeroStriked += () => strikes.Add(tick);

            for (tick = 1; tick <= 120; tick++) sim.Advance(Tick);

            Assert.That(strikes, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That((strikes[1] - strikes[0]) * Tick, Is.GreaterThanOrEqualTo(1f / hero.AttackSpeed - 2 * Tick),
                "the arrival Strike is followed by a whole cadence of waiting");
        }

        [Test]
        public void TheHero_StrikesAtOnceOnACollapsedArena()
        {
            var sim = Sim(Striker(), Profiles.Solo(EnemyArchetype.Brute), new EncounterTuning());
            var brute = sim.Enemies.Single();

            sim.Advance(Tick);

            Assert.That(brute.Health, Is.LessThan(brute.MaxHealth));
        }

        [Test]
        public void StrikeTarget_GivesWayToAnEnemyInReach_WhenTheTargetStandsOff()
        {
            var profile = new EncounterProfile(
                sourceLevel: 5, packed: EnemyArchetype.Skirmisher, rosterBrute: new IntRange(1), rosterSkirmisher: new IntRange(1),
                packBatch: new IntRange(1), packedSpawnWeight: 1f, spawnInterval: 100f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment), spawnJitter: 0f, initialSpawn: 2);
            var sim = Sim(Passive(), profile, OnTheArena());
            var brute = sim.Enemies.Single(e => e.Archetype == EnemyArchetype.Brute);
            var skirmisher = sim.Enemies.Single(e => e.Archetype == EnemyArchetype.Skirmisher);
            Assert.That(sim.StrikeTarget, Is.SameAs(skirmisher), "premise: both walk in, and the earlier spawn is his target");

            for (var i = 0; i < 150; i++) sim.Advance(Tick);

            Assert.That(Distance(sim, skirmisher), Is.GreaterThan(sim.HeroStrikeRange), "premise: the skirmisher stands outside his reach");
            Assert.That(sim.StrikeTarget, Is.SameAs(brute), "the Brute is the one inside it");
        }

        [Test]
        public void TheSameSeed_GivesTheSamePositions_AndTheMovementSeedMovesThem()
        {
            List<Coordinate> Trace(int movementSeed)
            {
                var profile = Profiles.Group(EnemyArchetype.Brute, 3);
                var sim = Sim(Passive(), profile, OnTheArena(), new SeededRollSource(3), new SeededRollSource(movementSeed));
                var trace = new List<Coordinate>();
                for (var i = 0; i < 60; i++)
                {
                    sim.Advance(Tick);
                    trace.AddRange(sim.Enemies.Select(e => e.Position));
                }
                return trace;
            }

            Assert.That(Trace(11), Is.EqualTo(Trace(11)), "the same seeds run twice");
            Assert.That(Trace(11), Is.Not.EqualTo(Trace(12)), "negative control: the movement stream is what places them");
        }

        [Test]
        public void Positions_AdvanceOnlyWithSimTime()
        {
            float Walked(float delta)
            {
                var sim = Sim(Passive(), Profiles.Solo(EnemyArchetype.Brute), OnTheArena());
                var brute = sim.Enemies.Single();
                var before = Distance(sim, brute);
                sim.Advance(delta);
                return before - Distance(sim, brute);
            }

            Assert.That(Walked(0f), Is.EqualTo(0f), "frozen at zero");
            Assert.That(Walked(0.25f), Is.EqualTo(2f * Walked(0.1f)).Within(0.001f), "two ticks walk twice what one does");
            Assert.That(Walked(0.1f), Is.GreaterThan(0f));
        }
    }
}
