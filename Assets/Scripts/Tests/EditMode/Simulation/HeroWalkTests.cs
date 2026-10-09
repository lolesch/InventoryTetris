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
    /// The hero walks to fight (issue #209): he closes to his Strike Range on his target, keeps it until it
    /// falls, and walks home when nothing lives. Everything is observed through
    /// <see cref="EncounterSimulation.Advance"/> with a fake hero. A Skirmisher stands off at about 5.4 units,
    /// outside the unarmed Strike Range of 1.5, so only a hero who walks can reach it; the standard arena is
    /// radius 10 + margin 2, and 300 points of movement speed are 3 units per second.
    /// </summary>
    [TestFixture]
    public sealed class HeroWalkTests
    {
        private const float Tick = 0.1f;
        private const float WalkSpeed = 300f; // stat points: 3 arena units per second at the default scale
        private const float UnarmedRange = 1.5f;

        // Strikes on every tick for a sliver of damage, so a Skirmisher (about 60 health) outlives every test that is not about
        // its death. No Resource: the Cast never fires.
        private static FakeHero Walker(float speed = WalkSpeed, float damage = 0.1f) => new()
        {
            PhysicalDamage = damage,
            MagicalDamage = 0f,
            Resource = 0f,
            AttackSpeed = 10f,
            MovementSpeed = speed,
        };

        private static EncounterTuning OnTheArena(float beat = 1000f)
        {
            var tuning = new EncounterTuning { Arena = ArenaTuning.Standard(), Beat = beat };
            tuning.Arena.HeroStrikeRange = UnarmedRange;
            return tuning;
        }

        private static EncounterSimulation Sim(FakeHero hero, EncounterProfile profile, EncounterTuning tuning = null,
            HeroBehaviour behaviour = null, IRollSource movement = null) => new(hero, profile, new ConstantRollSource(0f),
            behaviour ?? Behaviours.Engaging(10), tuning ?? OnTheArena(), bag: null,
            movementRolls: movement ?? new ConstantRollSource(0.5f));

        private static EncounterSimulation SoloSkirmisher(FakeHero hero, EncounterTuning tuning = null) =>
            Sim(hero, Profiles.Solo(EnemyArchetype.Skirmisher), tuning);

        private static void Run(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++) sim.Advance(Tick);
        }

        private static float Gap(EncounterSimulation sim, Enemy enemy) => Coordinate.Distance(sim.HeroPosition, enemy.Position);

        [Test]
        public void TheHero_WalksToAnEnemyStandingOff_AndStopsAtHisStrikeRange_WithoutOvershooting()
        {
            var sim = SoloSkirmisher(Walker());
            var skirmisher = sim.Enemies.Single();
            Assert.That(Gap(sim, skirmisher), Is.GreaterThan(UnarmedRange), "premise: out of his reach");

            Run(sim, 100);

            Assert.That(sim.HeroPosition, Is.Not.EqualTo(sim.Arena.Origin), "he walked");
            Assert.That(Gap(sim, skirmisher), Is.EqualTo(UnarmedRange).Within(0.01f), "and stopped at his Strike Range");
        }

        [Test]
        public void TheHero_StrikesTheEnemyOnceHeHasWalkedWithinReach()
        {
            var sim = SoloSkirmisher(Walker());
            var skirmisher = sim.Enemies.Single();

            Run(sim, 100);

            Assert.That(skirmisher.Health, Is.LessThan(skirmisher.MaxHealth));
        }

        [Test]
        public void AHeroWithZeroMovementSpeed_NeverMoves_AndNeverReachesAnEnemyStandingOff()
        {
            var sim = SoloSkirmisher(Walker(speed: 0f));
            var skirmisher = sim.Enemies.Single();

            Run(sim, 200);

            Assert.That(sim.HeroPosition, Is.EqualTo(sim.Arena.Origin));
            Assert.That(Gap(sim, skirmisher), Is.GreaterThan(UnarmedRange));
            Assert.That(skirmisher.Health, Is.EqualTo(skirmisher.MaxHealth), "he cannot reach it");
        }

        [TestCase(-300f)]
        [TestCase(float.NaN)]
        public void AHeroWithANegativeOrNaNMovementSpeed_StandsWhereHeIs_WithAFinitePosition(float speed)
        {
            var sim = SoloSkirmisher(Walker(speed: speed));

            Run(sim, 50);

            Assert.That(sim.HeroPosition, Is.EqualTo(sim.Arena.Origin));
        }

        [Test]
        public void TheHeroWalks_MovementSpeedTimesTheArenasScale_UnitsPerSecond()
        {
            float Walked(float scale)
            {
                var tuning = OnTheArena();
                tuning.Arena.MovementSpeedScale = scale;
                var sim = SoloSkirmisher(Walker(), tuning);
                Run(sim, 10); // one second
                return Coordinate.Distance(sim.HeroPosition, sim.Arena.Origin);
            }

            Assert.That(Walked(0.01f), Is.EqualTo(3f).Within(0.01f));
            Assert.That(Walked(0.02f), Is.EqualTo(6f).Within(0.01f), "negative control: the scale is what sets the pace");
        }

        [Test]
        public void TheHeroStopsAtHisWeaponsStrikeRange_AndAnUnarmedHeroAtTheArenas()
        {
            float StoppedAt(float? weaponRange, float arenaRange)
            {
                var tuning = OnTheArena();
                tuning.Arena.HeroStrikeRange = arenaRange;
                var hero = Walker();
                hero.WeaponStrikeRange = weaponRange;
                var sim = SoloSkirmisher(hero, tuning);
                Run(sim, 100);
                return Gap(sim, sim.Enemies.Single());
            }

            Assert.That(StoppedAt(weaponRange: 4f, arenaRange: 1.5f), Is.EqualTo(4f).Within(0.01f), "the weapon sets it");
            Assert.That(StoppedAt(weaponRange: null, arenaRange: 2.5f), Is.EqualTo(2.5f).Within(0.01f), "unarmed falls back to the arena's");
        }

        [Test]
        public void WithNoEnemyAlive_TheHeroWalksBackToTheOrigin_AndStopsThere()
        {
            var sim = SoloSkirmisher(Walker(damage: 30f));
            for (var i = 0; i < 200 && sim.EnemiesDefeated == 0; i++) sim.Advance(Tick);
            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "premise: he killed it");
            var away = Coordinate.Distance(sim.HeroPosition, sim.Arena.Origin);
            Assert.That(away, Is.GreaterThan(1f), "premise: it drew him off the origin");

            var trail = new List<float>();
            for (var i = 0; i < 100; i++)
            {
                sim.Advance(Tick);
                trail.Add(Coordinate.Distance(sim.HeroPosition, sim.Arena.Origin));
            }

            Assert.That(sim.HeroPosition, Is.EqualTo(sim.Arena.Origin), "home, exactly - no overshoot");
            for (var i = 1; i < trail.Count; i++)
                Assert.That(trail[i], Is.LessThanOrEqualTo(trail[i - 1]), "he only ever closes on home");
        }
    }
}
