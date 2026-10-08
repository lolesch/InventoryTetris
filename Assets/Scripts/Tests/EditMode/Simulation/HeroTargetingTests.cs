using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// How the hero picks and keeps his target (issue #209, weighted proximity): one target until it falls, the
    /// lowest <c>w * distance from the origin + (1 - w) * distance from himself</c> when he has none, and a swap
    /// to an enemy inside his reach only when his target is outside it. Enemies are stood where a scenario needs
    /// them (the sim's position setter is internal to its test assembly) and the choice is read through
    /// <see cref="EncounterSimulation.StrikeTarget"/>, which peeks at what the next tick will choose, and through
    /// who takes the Strike.
    /// </summary>
    [TestFixture]
    public sealed class HeroTargetingTests
    {
        private const float Tick = 0.1f;
        private const float UnarmedRange = 1.5f;

        private static FakeHero Hero(float speed, float damage = 0.1f) => new()
        {
            PhysicalDamage = damage,
            MagicalDamage = 0f,
            Resource = 0f,
            AttackSpeed = 10f,
            MovementSpeed = speed,
        };

        private static EncounterSimulation Sim(FakeHero hero, EncounterProfile profile, HeroBehaviour behaviour)
        {
            var tuning = new EncounterTuning { Ground = GroundTuning.Standard(), Beat = 1000f };
            tuning.Ground.HeroStrikeRange = UnarmedRange;
            return new EncounterSimulation(hero, profile, new ConstantRollSource(0f), behaviour, tuning, bag: null,
                movementRolls: new ConstantRollSource(0.5f));
        }

        private static HeroBehaviour Weighted(float originWeight) => new() { Engagement = 10, OriginWeight = originWeight };

        private static void Stand(Enemy enemy, float x, float z) => enemy.Position = new Coordinate(x, z);

        private static void Stand(Enemy enemy, Coordinate at) => enemy.Position = at;

        private static void Run(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++) sim.Advance(Tick);
        }

        // Three Skirmishers stood so the first goes down at once; returns the sim with the other two alive and the
        // hero standing off the origin, where "near me" and "near home" are different places.
        private static (EncounterSimulation Sim, Enemy First, Enemy Second) HeroStandingOffTheOrigin(HeroBehaviour behaviour)
        {
            var sim = Sim(Hero(speed: 300f, damage: 100f), Profiles.Group(EnemyArchetype.Skirmisher, 3), behaviour);
            Stand(sim.Enemies[0], 5f, 0f);
            Stand(sim.Enemies[1], -9f, 0f);
            Stand(sim.Enemies[2], 0f, -9f);

            for (var i = 0; i < 100 && sim.EnemiesDefeated == 0; i++) sim.Advance(Tick);
            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "premise: the near one fell");
            Assert.That(Coordinate.Distance(sim.HeroPosition, sim.Ground.Origin), Is.GreaterThan(3f), "premise: he stands off the origin");

            return (sim, sim.Enemies[0], sim.Enemies[1]);
        }

        [Test]
        public void WithNoTarget_AFullOriginWeight_PicksTheEnemyNearestTheOrigin()
        {
            var (sim, _, _) = HeroStandingOffTheOrigin(Weighted(1f));
            var h = sim.HeroPosition;
            var nearMe = sim.Enemies[0];
            var nearHome = sim.Enemies[1];
            Stand(nearMe, h + new Coordinate(0f, 4f));
            Stand(nearHome, -3f, 0f);
            Assert.That(Coordinate.Distance(nearHome.Position, sim.Ground.Origin), Is.LessThan(Coordinate.Distance(nearMe.Position, sim.Ground.Origin)), "premise");
            Assert.That(Coordinate.Distance(nearMe.Position, h), Is.LessThan(Coordinate.Distance(nearHome.Position, h)), "premise");

            Assert.That(sim.StrikeTarget, Is.SameAs(nearHome));
        }

        [Test]
        public void WithNoTarget_AZeroOriginWeight_PicksTheEnemyNearestHim_AndTheWeightIsReadLive()
        {
            var behaviour = Weighted(1f);
            var (sim, _, _) = HeroStandingOffTheOrigin(behaviour);
            var h = sim.HeroPosition;
            var nearMe = sim.Enemies[0];
            var nearHome = sim.Enemies[1];
            Stand(nearMe, h + new Coordinate(0f, 4f));
            Stand(nearHome, -3f, 0f);
            Assert.That(sim.StrikeTarget, Is.SameAs(nearHome), "premise: home pulls first");

            behaviour.OriginWeight = 0f;

            Assert.That(sim.StrikeTarget, Is.SameAs(nearMe));
        }

        [Test]
        public void WithNoTarget_ABalancedWeight_TradesDistanceFromHomeAgainstDistanceFromHim()
        {
            var behaviour = Weighted(0.5f);
            var (sim, _, _) = HeroStandingOffTheOrigin(behaviour);
            var h = sim.HeroPosition;
            var a = sim.Enemies[0];
            var b = sim.Enemies[1];
            // At the midpoint weight only the sum of the two distances counts.
            Stand(a, h + new Coordinate(0f, 4f));
            Stand(b, -3f, 0f);
            float Sum(Enemy e) => Coordinate.Distance(e.Position, sim.Ground.Origin) + Coordinate.Distance(e.Position, h);
            var cheaper = Sum(a) < Sum(b) ? a : b;

            Assert.That(sim.StrikeTarget, Is.SameAs(cheaper));
        }

        [Test]
        public void OnATie_TheEarliestSpawnedIsChosen_WhicheverSideItStandsOn()
        {
            var sim = Sim(Hero(speed: 0f), Profiles.Group(EnemyArchetype.Skirmisher, 2), Weighted(0.5f));
            var first = sim.Enemies[0];
            var second = sim.Enemies[1];

            Stand(first, 4f, 0f);
            Stand(second, -4f, 0f);
            Assert.That(sim.StrikeTarget, Is.SameAs(first));

            Stand(first, -4f, 0f);
            Stand(second, 4f, 0f);
            Assert.That(sim.StrikeTarget, Is.SameAs(first), "negative control: not geometry, spawn order");
        }

        [Test]
        public void HeKeepsHisTarget_WhenABetterScoringEnemyAppears_UntilItFalls()
        {
            var sim = Sim(Hero(speed: 300f), Profiles.Group(EnemyArchetype.Skirmisher, 2), Weighted(1f));
            var target = sim.Enemies[0];
            var newcomer = sim.Enemies[1];
            Stand(target, 6f, 0f);
            Stand(newcomer, -9f, 0f);
            sim.Advance(Tick);
            Assert.That(sim.StrikeTarget, Is.SameAs(target), "premise: it is his target");

            Stand(newcomer, 0f, 3f); // nearer home than the target ever was, still outside his reach
            Assert.That(Coordinate.Distance(newcomer.Position, sim.Ground.Origin), Is.LessThan(Coordinate.Distance(target.Position, sim.Ground.Origin)), "premise");
            Assert.That(Coordinate.Distance(newcomer.Position, sim.HeroPosition), Is.GreaterThan(UnarmedRange), "premise");
            Run(sim, 3);
            Assert.That(sim.StrikeTarget, Is.SameAs(target), "he keeps it");

            target.ReceivePhysical(target.MaxHealth * 100f);

            Assert.That(sim.StrikeTarget, Is.SameAs(newcomer), "negative control: once it has fallen the newcomer is next");
        }

        [Test]
        public void HeSwitchesToAnEnemyInsideHisReach_WhenHisTargetIsOutsideIt_PickingTheBestScoreInside()
        {
            var sim = Sim(Hero(speed: 300f), Profiles.Group(EnemyArchetype.Skirmisher, 3), Weighted(1f));
            var far = sim.Enemies[0];
            Stand(far, 6f, 0f);
            Stand(sim.Enemies[1], -9f, 0f);
            Stand(sim.Enemies[2], 0f, -9f);
            sim.Advance(Tick);
            Assert.That(sim.StrikeTarget, Is.SameAs(far), "premise: the far one is his target");

            var h = sim.HeroPosition;
            var nearerHim = sim.Enemies[1];
            var nearerHome = sim.Enemies[2];
            Stand(nearerHim, h + new Coordinate(0.9f, 0f));
            Stand(nearerHome, h + new Coordinate(-1.3f, 0f));
            Assert.That(Coordinate.Distance(nearerHome.Position, sim.Ground.Origin), Is.LessThan(Coordinate.Distance(nearerHim.Position, sim.Ground.Origin)), "premise");
            Assert.That(Coordinate.Distance(nearerHome.Position, h), Is.LessThanOrEqualTo(UnarmedRange), "premise: both inside his reach");
            Assert.That(Coordinate.Distance(far.Position, h), Is.GreaterThan(UnarmedRange), "premise: his target is outside it");

            Assert.That(sim.StrikeTarget, Is.SameAs(nearerHome), "the best score of those inside, not the nearest");
        }

        [Test]
        public void AnEnemyClosingInMidWalk_MakesHimStopAndFightIt_ThenHeReturnsToTheFarOne()
        {
            var profile = new EncounterProfile(
                sourceLevel: 5, packed: EnemyArchetype.Brute, rosterBrute: new IntRange(1), rosterSkirmisher: new IntRange(1),
                packBatch: new IntRange(1), packedSpawnWeight: 1f, spawnInterval: 100f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment), spawnJitter: 0f, initialSpawn: 2);
            // A slow walker: the Brute arrives long before he reaches the Skirmisher.
            var sim = Sim(Hero(speed: 50f), profile, Weighted(1f));
            var skirmisher = sim.Enemies.Single(e => e.Archetype == EnemyArchetype.Skirmisher);
            var brute = sim.Enemies.Single(e => e.Archetype == EnemyArchetype.Brute);
            Stand(skirmisher, -8f, 0f);
            Stand(brute, 0f, 9f);

            sim.Advance(Tick);
            Assert.That(sim.StrikeTarget, Is.SameAs(skirmisher), "premise: he sets out for the far one");

            var ticks = 0;
            while (!ReferenceEquals(sim.StrikeTarget, brute) && ticks++ < 400) sim.Advance(Tick);
            Assert.That(sim.StrikeTarget, Is.SameAs(brute), "the closing Brute became his target");
            Assert.That(Coordinate.Distance(sim.HeroPosition, brute.Position), Is.LessThanOrEqualTo(UnarmedRange + 0.01f), "it came within his reach");
            Assert.That(Coordinate.Distance(sim.HeroPosition, skirmisher.Position), Is.GreaterThan(UnarmedRange), "while the Skirmisher was still out of it");

            var stoodAt = sim.HeroPosition;
            var bruteHealth = brute.Health;
            Run(sim, 20);
            Assert.That(sim.HeroPosition, Is.EqualTo(stoodAt), "he stopped walking");
            Assert.That(brute.Health, Is.LessThan(bruteHealth), "and fought it");
            Assert.That(skirmisher.Health, Is.EqualTo(skirmisher.MaxHealth), "the Skirmisher was never touched");

            brute.ReceivePhysical(brute.MaxHealth * 100f);
            Assert.That(sim.StrikeTarget, Is.SameAs(skirmisher), "with the Brute down he goes back to the one he was after");
        }

        [Test]
        public void WithNoEnemyAliveBetweenSpawns_TheHeroWalksHome_WhileTheEncounterIsStillOn()
        {
            var profile = new EncounterProfile(
                sourceLevel: 5, packed: EnemyArchetype.Skirmisher, rosterBrute: new IntRange(0), rosterSkirmisher: new IntRange(2),
                packBatch: new IntRange(1), packedSpawnWeight: 1f, spawnInterval: 100f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment), spawnJitter: 0f, initialSpawn: 1);
            var sim = Sim(Hero(speed: 300f, damage: 30f), profile, Weighted(0.5f));
            for (var i = 0; i < 200 && sim.EnemiesDefeated == 0; i++) sim.Advance(Tick);
            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "premise: the first fell");
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Fighting), "premise: the second is still to spawn");
            Assert.That(Coordinate.Distance(sim.HeroPosition, sim.Ground.Origin), Is.GreaterThan(1f), "premise: he is off the origin");

            Run(sim, 100);

            Assert.That(sim.AliveEnemyCount, Is.Zero);
            Assert.That(sim.HeroPosition, Is.EqualTo(sim.Ground.Origin));
        }

        [Test]
        public void TheSameSeeds_GiveTheSameHeroPath_AndAnotherMovementSeedGivesAnother()
        {
            System.Collections.Generic.List<Coordinate> Path(int movementSeed)
            {
                var tuning = new EncounterTuning { Ground = GroundTuning.Standard(), Beat = 1000f };
                var sim = new EncounterSimulation(Hero(speed: 300f, damage: 3f), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                    new SeededRollSource(3), Weighted(0.5f), tuning, bag: null, movementRolls: new SeededRollSource(movementSeed));
                var path = new System.Collections.Generic.List<Coordinate>();
                for (var i = 0; i < 150; i++)
                {
                    sim.Advance(Tick);
                    path.Add(sim.HeroPosition);
                }
                return path;
            }

            Assert.That(Path(11), Is.EqualTo(Path(11)), "the same seeds run twice");
            Assert.That(Path(11), Is.Not.EqualTo(Path(12)), "negative control: the movement stream decides where he walks");
        }
    }
}
