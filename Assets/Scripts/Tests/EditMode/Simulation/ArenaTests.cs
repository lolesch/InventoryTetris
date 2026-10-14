using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The Arena (ADR-0020) answers the spatial questions the Encounter asks - where the hero stands, who is in
    /// reach, who a Cast shape covers, where a spawn goes and how a walk steps - without an Encounter around it.
    /// Each test states positions and asks the Arena directly. The hero's Strike Range, a Brute's walking speed (2)
    /// and Strike Range (1.5) are read off the fixtures.
    /// </summary>
    [TestFixture]
    public sealed class ArenaTests
    {
        private const float Tick = 0.1f;

        private static ArenaTuning Standard(Coordinate origin = default)
        {
            var tuning = ArenaTuning.Standard();
            tuning.Origin = origin;
            return tuning;
        }

        private static Enemy BruteAt(float x, float z)
        {
            var enemy = new Enemy(EnemyArchetype.Brute, 1) { Position = new Coordinate(x, z) };
            enemy.StopDistance = enemy.StrikeRange;
            return enemy;
        }

        private static Enemy Down(Enemy enemy)
        {
            enemy.HealthResource.RemoveFromCurrent(enemy.MaxHealth);
            return enemy;
        }

        private static CastDefinition Disk(float radius = 2f, float range = 7f, AreaAnchor anchor = AreaAnchor.Target) =>
            new() { Range = range, Shape = AreaShape.Disk(radius), Anchor = anchor };

        // --- positions ----------------------------------------------------------

        [Test]
        public void TheHero_StartsAtTheOrigin()
        {
            var origin = new Coordinate(3f, -4f);

            Assert.That(new Arena(Standard(origin)).HeroPosition, Is.EqualTo(origin));
        }

        // --- reach --------------------------------------------------------------

        [Test]
        public void ReachIsTheDistanceFromTheHero_WithASliverOfSlackOnTheEdge()
        {
            var arena = new Arena(Standard());

            Assert.That(arena.InRangeOfHero(new Coordinate(3f, 0f), 3f), Is.True, "on the edge");
            Assert.That(arena.InRangeOfHero(new Coordinate(3.0005f, 0f), 3f), Is.True, "within float slack of it");
            Assert.That(arena.InRangeOfHero(new Coordinate(3.01f, 0f), 3f), Is.False, "past it");
        }

        [Test]
        public void ReachFollowsTheHero_NotTheOrigin()
        {
            var arena = new Arena(Standard());
            arena.WalkHero(1000f, 1f, new Coordinate(8f, 0f), 0f);

            Assert.That(arena.InRangeOfHero(new Coordinate(8f, 1f), 1.5f), Is.True);
            Assert.That(arena.InRangeOfHero(new Coordinate(0f, 0f), 1.5f), Is.False);
        }

        // --- the hero's walk ----------------------------------------------------

        [Test]
        public void TheHero_WalksTowardAPointAtHisSpeedTimesTheScale()
        {
            var arena = new Arena(Standard());

            arena.WalkHero(300f, 1f, new Coordinate(10f, 0f), 0f); // 300 * 0.01 = 3 units a second

            Assert.That(arena.HeroPosition.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(arena.HeroPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TheHero_StopsAtTheStopDistance_NeverPastIt()
        {
            var arena = new Arena(Standard());

            arena.WalkHero(10_000f, 1f, new Coordinate(10f, 0f), 1.5f);

            Assert.That(arena.HeroPosition.x, Is.EqualTo(8.5f).Within(0.0001f));
        }

        [Test]
        public void TheHero_WithinTheStopDistanceAlready_StandsStill()
        {
            var arena = new Arena(Standard());

            arena.WalkHero(300f, 1f, new Coordinate(1f, 0f), 1.5f);

            Assert.That(arena.HeroPosition, Is.EqualTo(new Coordinate(0f, 0f)));
        }

        [TestCase(0f)]
        [TestCase(-50f)]
        [TestCase(float.NaN)]
        public void AHeroWithoutAWalkingSpeed_StandsWhereHeIs(float movementSpeed)
        {
            var arena = new Arena(Standard());

            arena.WalkHero(movementSpeed, 1f, new Coordinate(10f, 0f), 0f);

            Assert.That(arena.HeroPosition, Is.EqualTo(new Coordinate(0f, 0f)));
        }

        [Test]
        public void TheHero_WalksHomeWithoutOvershootingTheOrigin()
        {
            var arena = new Arena(Standard(new Coordinate(1f, 1f)));
            arena.WalkHero(10_000f, 1f, new Coordinate(5f, 1f), 0f);

            arena.WalkHero(10_000f, 1f, arena.Origin, 0f);

            Assert.That(arena.HeroPosition, Is.EqualTo(new Coordinate(1f, 1f)));
        }

        // --- the enemies' walk --------------------------------------------------

        [Test]
        public void AnEnemy_WalksTowardTheHeroAtItsSpeed()
        {
            var arena = new Arena(Standard());
            var enemy = BruteAt(10f, 0f);

            arena.WalkEnemy(enemy, Tick); // a Brute walks 2 units a second

            Assert.That(enemy.Position.x, Is.EqualTo(9.8f).Within(0.0001f));
        }

        [Test]
        public void AnEnemy_StopsAtItsStopDistance_NeverPastIt()
        {
            var arena = new Arena(Standard());
            var enemy = BruteAt(1.55f, 0f);
            enemy.StopDistance = 1.5f;

            arena.WalkEnemy(enemy, 1f);

            Assert.That(enemy.Position.x, Is.EqualTo(1.5f).Within(0.0001f));
        }

        // --- spawn bearings -----------------------------------------------------

        [Test]
        public void WithNobodyAlive_TheBearingIsTheMiddleOfTheWholeCircle()
        {
            Assert.That(new Arena(Standard()).SpawnBearing(new Enemy[0], 0.5f), Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void TheBearing_BisectsTheWidestGapBetweenTheLiving()
        {
            var a = BruteAt(1f, 0f);
            var b = BruteAt(1f, 0f);
            a.Bearing = 0f;
            b.Bearing = 90f;

            // gaps: 0..90 and 90..360; the wide one is centred on 225
            Assert.That(new Arena(Standard()).SpawnBearing(new[] { a, b }, 0.5f), Is.EqualTo(225f).Within(0.001f));
        }

        [Test]
        public void TheBearing_IgnoresTheFallen()
        {
            var alive = BruteAt(1f, 0f);
            var fallen = Down(BruteAt(1f, 0f));
            alive.Bearing = 0f;
            fallen.Bearing = 180f;

            Assert.That(new Arena(Standard()).SpawnBearing(new[] { alive, fallen }, 0.5f), Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void TheRoll_StraysTheBearingByTheJitterOfTheGap_WithoutReachingANeighbour()
        {
            var arena = new Arena(Standard()); // bearing jitter 0.5
            var none = new Enemy[0];

            Assert.That(arena.SpawnBearing(none, 0f), Is.EqualTo(90f).Within(0.001f));
            Assert.That(arena.SpawnBearing(none, 1f), Is.EqualTo(270f).Within(0.001f));
        }

        [Test]
        public void ASpawn_StandsOnTheRingBeyondTheEdge_AtItsBearing_AndStopsWithinItsRange()
        {
            var origin = new Coordinate(3f, -4f);
            var arena = new Arena(Standard(origin)); // radius 10 + margin 2, stop jitter 0.2
            var enemy = new Enemy(EnemyArchetype.Brute, 1);

            arena.PlaceSpawn(enemy, new Enemy[0], bearingRoll: 0f, stopRoll: 0.5f); // nobody alive: a roll of 0 strays a quarter circle, to bearing 90

            Assert.That(enemy.Bearing, Is.EqualTo(90f).Within(0.001f));
            Assert.That(enemy.Position.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(enemy.Position.z, Is.EqualTo(-4f + 12f).Within(0.001f));
            Assert.That(enemy.StopDistance, Is.EqualTo(1.5f * (1f - 0.2f * 0.5f)).Within(0.0001f));
        }

        // --- shapes -------------------------------------------------------------

        [Test]
        public void TheDensestCluster_IsAimedAtOverALoneEnemy_EvenWhenTheLoneOneIsFirstAndNearer()
        {
            var lone = BruteAt(-2f, 0f);
            var clump = new[] { BruteAt(5f, 0f), BruteAt(5.5f, 0.5f), BruteAt(5f, 1f) };

            var aim = new Arena(Standard()).DensestCluster(new[] { lone }.Concat(clump).ToList(), Disk());

            Assert.That(clump, Does.Contain(aim));
        }

        [Test]
        public void TheDensestCluster_OnATie_GoesToTheNearestTheHero_ThenTheEarliest()
        {
            var far = BruteAt(5.5f, 0f);
            var near = BruteAt(3f, 0f);
            var arena = new Arena(Standard());

            Assert.That(arena.DensestCluster(new[] { far, near }, Disk()), Is.SameAs(near));

            var first = BruteAt(3f, 0f);
            var mirrored = BruteAt(-3f, 0f);
            Assert.That(arena.DensestCluster(new[] { first, mirrored }, Disk()), Is.SameAs(first));
        }

        [Test]
        public void TheDensestCluster_OnlyConsidersEnemiesWithinCastRange_AndTheLiving()
        {
            var outOfRange = BruteAt(8f, 0f);
            var fallen = Down(BruteAt(2f, 0f));
            var arena = new Arena(Standard());

            Assert.That(arena.DensestCluster(new[] { outOfRange, fallen }, Disk(range: 7f)), Is.Null);
            Assert.That(arena.DensestCluster(new[] { outOfRange, fallen }, Disk(range: 9f)), Is.SameAs(outOfRange));
        }

        [Test]
        public void TheDensestCluster_WithNobody_IsNull()
        {
            Assert.That(new Arena(Standard()).DensestCluster(new Enemy[0], Disk()), Is.Null);
        }

        [Test]
        public void TheShape_CoversTheLivingInsideIt_InTheOrderGiven()
        {
            var aim = BruteAt(4f, 0f);
            var inside = BruteAt(5f, 0f);
            var outside = BruteAt(-4f, 0f);
            var fallen = Down(BruteAt(4f, 1f));
            var arena = new Arena(Standard());

            var covered = arena.InShape(new[] { outside, inside, fallen, aim }, Disk(), aim);

            Assert.That(covered, Is.EqualTo(new[] { inside, aim }));
        }

        [Test]
        public void TheShape_IsPlacedOnTheTarget_OrOnTheHeroPointingAtIt_PerTheAnchor()
        {
            var aim = BruteAt(6f, 0f);
            var nearHero = BruteAt(1.5f, 0f);
            var all = new[] { nearHero, aim };
            var arena = new Arena(Standard());

            Assert.That(arena.InShape(all, Disk(radius: 2f, anchor: AreaAnchor.Target), aim), Is.EqualTo(new[] { aim }));
            Assert.That(arena.InShape(all, Disk(radius: 2f, anchor: AreaAnchor.Origin), aim), Is.EqualTo(new[] { nearHero }));
        }

        [Test]
        public void TheShape_IsScaledByTheCastSize_AsAnAreaMultiplier()
        {
            var aim = BruteAt(4f, 0f);
            var edge = BruteAt(6.5f, 0f);
            var arena = new Arena(Standard());
            var roomier = Disk(radius: 2f);
            roomier.Size = 2f; // twice the area: radius 2 * sqrt(2) = 2.83

            Assert.That(arena.InShape(new[] { aim, edge }, Disk(radius: 2f), aim), Is.EqualTo(new[] { aim }));
            Assert.That(arena.InShape(new[] { aim, edge }, roomier, aim), Is.EqualTo(new[] { aim, edge }));
        }
    }
}
