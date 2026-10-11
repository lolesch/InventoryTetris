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
    /// The Cast is an area (issue #212): of the enemies within Cast Range the hero aims at the one whose shape would
    /// catch the most enemies, and everything inside takes his magical damage. Driven through the simulation's
    /// advance entry point. The enemies are placed by hand on the arena (the hero stands at its origin) and pinned
    /// there, so a test reads as a picture: who stands where, and who is hit by the one Cast the hero can afford.
    /// </summary>
    [TestFixture]
    public sealed class CastAreaTests
    {
        private const float Tick = 0.1f;
        private const float CastDamage = 5f;
        private const float CastCost = 16f;

        // One enemy to place: x and z on the arena, the hero at (0, 0).
        private readonly struct At
        {
            public readonly float X, Z;
            public At(float x, float z) { X = x; Z = z; }
            public static implicit operator At((float x, float z) p) => new(p.x, p.z);
        }

        private sealed class Scene
        {
            public EncounterSimulation Sim;
            public FakeHero Hero;
            public List<Enemy> Enemies;
            public List<HitEvent> Hits = new();
            public int Casts;

            /// <summary>The indices (in spawn order) of the enemies that lost health.</summary>
            public int[] Struck => Enemies.Select((e, i) => (e, i)).Where(p => p.e.Health < p.e.MaxHealth).Select(p => p.i).ToArray();

            /// <summary>One tick: the Cast the hero can afford (once) lands, if it fires at all.</summary>
            public Scene Cast()
            {
                Sim.Advance(Tick);
                return this;
            }
        }

        // The hero can pay for exactly one Cast and his Strike is idle, so only the Cast can hurt anyone. The enemies are
        // Brutes without magic resist, pinned where they stand (a stop distance nothing is farther than).
        private static Scene Arrange(CastDefinition cast, params At[] at) => Arrange(cast, null, at);

        private static Scene Arrange(CastDefinition cast, IRollSource hitRolls, params At[] at)
        {
            var hero = new FakeHero
            {
                AttackSpeed = 0.01f, PhysicalDamage = 0f, MagicalDamage = CastDamage,
                CastCost = CastCost, Resource = CastCost, MaxResource = CastCost,
            };
            var tuning = new EncounterTuning { CastCadence = 0.05f, Cast = cast, DamageSpread = hitRolls == null ? 0f : 0.2f };
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Brute, at.Length), new ConstantRollSource(0f),
                Behaviours.Engaging(at.Length + 1), tuning, hitRolls: hitRolls);

            var enemies = sim.Enemies.ToList();
            for (var i = 0; i < at.Length; i++)
            {
                enemies[i].WithoutMagicResist();
                enemies[i].Position = new Coordinate(at[i].X, at[i].Z);
                enemies[i].StopDistance = float.MaxValue;
            }

            var scene = new Scene { Sim = sim, Hero = hero, Enemies = enemies };
            sim.HitLanded += scene.Hits.Add;
            sim.HeroCast += () => scene.Casts++;
            return scene;
        }

        private static CastDefinition Disk(float radius = 2f, float range = 7f, AreaAnchor anchor = AreaAnchor.Target) =>
            new() { Range = range, Shape = AreaShape.Disk(radius), Anchor = anchor };

        // --- the densest cluster -----------------------------------------------

        [Test]
        public void TheCast_CatchesAClump_AndSkipsALoneEnemyInRange()
        {
            // The lone enemy spawned first, so "the first few" would take it; the clump is what the shape catches most of.
            var scene = Arrange(Disk(), (-6f, 0f), (3f, 0f), (3.5f, 0.5f), (3f, 1f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void TheCast_AimsAtTheDenserOfTwoClumps_EvenWhenTheOtherIsNearer()
        {
            var scene = Arrange(Disk(), (2f, 0f), (2f, 1f), (-5f, 0f), (-5f, 1f), (-5.5f, 0.5f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void ATie_GoesToTheEnemyNearestTheHero()
        {
            // Two lone enemies, the nearer one spawned second.
            var scene = Arrange(Disk(), (5.5f, 0f), (3f, 0f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ATieOnDistanceToo_GoesToTheEarliestSpawned()
        {
            // Equally near, one on each side: spawn order decides, whichever side it is on.
            Assert.That(Arrange(Disk(), (3f, 0f), (-3f, 0f)).Cast().Struck, Is.EqualTo(new[] { 0 }));
            Assert.That(Arrange(Disk(), (-3f, 0f), (3f, 0f)).Cast().Struck, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void ARoomierShape_CatchesWhatASmallerOneMisses_SizeIsAnAreaMultiplier()
        {
            var apart = new At[] { (3f, 0f), (6.5f, 0f) };

            Assert.That(Arrange(Disk(), apart).Cast().Struck, Is.EqualTo(new[] { 0 }), "a disk of radius 2 holds one of them");

            var roomy = Disk();
            roomy.Size = 4f; // four times the area, twice the radius
            Assert.That(Arrange(roomy, apart).Cast().Struck, Is.EqualTo(new[] { 0, 1 }));
        }

        // --- the range gate ------------------------------------------------------

        [Test]
        public void AnEnemyBeyondCastRange_IsNotAimedAt_AndCostsNothing()
        {
            var scene = Arrange(Disk(), (8f, 0f)).Cast();

            Assert.That(scene.Struck, Is.Empty);
            Assert.That(scene.Casts, Is.Zero, "no Cast fired");
            Assert.That(scene.Hero.Resource, Is.EqualTo(CastCost), "and no resource was spent");
        }

        [Test]
        public void AnEnemyInsideCastRange_IsAimedAt_AndTheCastSpendsItsCost()
        {
            var scene = Arrange(Disk(), (6.9f, 0f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0 }), "negative control: a hair nearer and it fires");
            Assert.That(scene.Casts, Is.EqualTo(1));
            Assert.That(scene.Hero.Resource, Is.Zero);
        }

        [Test]
        public void CastRange_IsInclusive_AndReadFromTheDefinition()
        {
            Assert.That(Arrange(Disk(range: 7f), (7f, 0f)).Cast().Casts, Is.EqualTo(1), "exactly at range");
            Assert.That(Arrange(Disk(range: 7f), (7.01f, 0f)).Cast().Casts, Is.Zero, "just beyond");
            Assert.That(Arrange(Disk(range: 9f), (8f, 0f)).Cast().Casts, Is.EqualTo(1), "a longer range reaches it");
        }

        [Test]
        public void OnlyEnemiesInRangeAreAimedAt_ButTheShapeStillHitsWhatSpillsPastIt()
        {
            // A clump of three stands past the range with one enemy of the clump just inside it; the lone enemy at the
            // other side is in range too. The one inside the clump is the only anchor that sees three.
            var scene = Arrange(Disk(), (-4f, 0f), (6.5f, 0f), (7.5f, 0f), (8f, 0.5f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 1, 2, 3 }), "the two beyond range are caught by a shape aimed inside it");
        }

        [Test]
        public void AClumpOutOfRange_IsNotChosen_OverALoneEnemyInRange()
        {
            var scene = Arrange(Disk(), (-4f, 0f), (9f, 0f), (9.5f, 0f), (9f, 1f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void AShapeThatWouldCatchNobody_DoesNotFire()
        {
            // A short sector on the hero never reaches an enemy that is merely in range: nothing to hit, so nothing to pay.
            var cast = new CastDefinition { Range = 7f, Shape = AreaShape.Sector(2f, 90f), Anchor = AreaAnchor.Origin };

            var scene = Arrange(cast, (5f, 0f)).Cast();

            Assert.That(scene.Casts, Is.Zero);
            Assert.That(scene.Hero.Resource, Is.EqualTo(CastCost));
        }

        // --- the shapes, with each anchor ---------------------------------------

        [Test]
        public void ADisk_OnTheTarget_CatchesTheClumpItIsAimedAt()
        {
            var scene = Arrange(Disk(anchor: AreaAnchor.Target), (1f, 0f), (5f, 0f), (5.5f, 0f), (5f, 1f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ADisk_OnTheOrigin_CatchesWhatStandsAroundTheHeroInstead()
        {
            var scene = Arrange(Disk(anchor: AreaAnchor.Origin), (1f, 0f), (5f, 0f), (5.5f, 0f), (5f, 1f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0 }), "the same field, but the disk is around the hero");
        }

        [Test]
        public void ASector_OnTheOrigin_FansOutFromTheHeroTowardTheTarget()
        {
            var cast = new CastDefinition { Range = 7f, Shape = AreaShape.Sector(6f, 60f), Anchor = AreaAnchor.Origin };

            // P0 and P1 lie in one 60-degree fan from the hero, P2 is 56 degrees off, P3 is behind him.
            var scene = Arrange(cast, (3f, 0f), (4f, 1f), (2f, 3f), (-3f, 0f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void ASector_OnTheTarget_CatchesTheEnemiesAheadOfIt_NotThoseBehind()
        {
            var cast = new CastDefinition { Range = 7f, Shape = AreaShape.Sector(3f, 90f), Anchor = AreaAnchor.Target };

            // The sector starts on P0 and points away from the hero: P1 and P2 are ahead of it, P3 behind.
            var scene = Arrange(cast, (4f, 0f), (6f, 0.5f), (6f, -0.5f), (2.5f, 0f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void ASector_OnTheOrigin_IsNotTheSameFieldAsOnTheTarget_NegativeControl()
        {
            var cast = new CastDefinition { Range = 7f, Shape = AreaShape.Sector(3f, 90f), Anchor = AreaAnchor.Origin };

            var scene = Arrange(cast, (4f, 0f), (6f, 0.5f), (6f, -0.5f), (2.5f, 0f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 3 }), "from the hero it reaches the near one only");
        }

        [Test]
        public void ARectangle_OnTheOrigin_IsABeamFromTheHeroAlongTheLineToTheTarget()
        {
            var cast = new CastDefinition { Range = 7f, Shape = AreaShape.Rectangle(3f, 2f), Anchor = AreaAnchor.Origin };

            // The far clump of three is out of the beam's reach; the two near enemies are in it.
            var scene = Arrange(cast, (1f, 0f), (2f, 0f), (5f, 0f), (6f, 0f), (6.5f, 0.3f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void ARectangle_OnTheTarget_StartsAtTheTargetAndRunsPastIt()
        {
            var cast = new CastDefinition { Range = 7f, Shape = AreaShape.Rectangle(3f, 2f), Anchor = AreaAnchor.Target };

            var scene = Arrange(cast, (1f, 0f), (2f, 0f), (5f, 0f), (6f, 0f), (6.5f, 0.3f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        // --- what a hit is ---------------------------------------------------------

        [Test]
        public void EveryEnemyInTheShape_TakesTheHerosMagicalDamage_AndEachHitRaisesAnEvent()
        {
            var scene = Arrange(Disk(), (3f, 0f), (3.5f, 0.5f), (-6f, 0f)).Cast();

            Assert.That(scene.Hits, Has.Count.EqualTo(2));
            Assert.That(scene.Hits.Select(h => h.Target), Is.EqualTo(new[] { scene.Enemies[0], scene.Enemies[1] }));
            Assert.That(scene.Hits.All(h => ReferenceEquals(h.Dealer, scene.Hero) && h.DamageType == DamageType.MagicalDamage));
            foreach (var enemy in new[] { scene.Enemies[0], scene.Enemies[1] })
                Assert.That(enemy.MaxHealth - enemy.Health, Is.EqualTo(CastDamage).Within(0.0001f));
        }

        [Test]
        public void TheHero_IsNeverHitByHisOwnCast_EvenStandingInsideTheShape()
        {
            // One enemy on top of the hero: he is inside the disk, and only the enemy takes the hit.
            var scene = Arrange(Disk(), (0f, 0f)).Cast();

            Assert.That(scene.Struck, Is.EqualTo(new[] { 0 }));
            Assert.That(scene.Hero.MagicalDamageTaken, Is.Zero);
            Assert.That(scene.Hits.Select(h => h.Target), Is.EqualTo(new[] { scene.Enemies[0] }));
        }

        [Test]
        public void EachTargetOfOneCast_RollsItsOwnSpread_InSpawnOrder()
        {
            // Three hits, three rolls: 0 and 1 are the ends of the +-20 % spread, 0.5 its middle.
            var rolls = new QueuedRollSource(0f, 1f, 0.5f);

            var scene = Arrange(Disk(), rolls, (3f, 0f), (3.5f, 0.5f), (3f, 1f)).Cast();

            Assert.That(scene.Hits.Select(h => h.RawAmount).ToArray(), Is.EqualTo(new[] { 4f, 6f, 5f }).Within(0.0001f));
            Assert.That(rolls.Consumed, Is.EqualTo(3), "no more, no fewer");
        }

        [Test]
        public void AnEnemyTheCastKills_StillFallsAndIsReported()
        {
            var scene = Arrange(Disk(), (3f, 0f), (3.5f, 0.5f));
            scene.Hero.MagicalDamage = 1_000_000f;
            var fallen = new List<Enemy>();
            scene.Sim.EnemyDefeated += fallen.Add;

            scene.Cast();

            Assert.That(fallen, Is.EquivalentTo(scene.Enemies));
        }
    }
}
