using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The hero's two concurrent attacks run on independent cadences (ADR-0010): the Strike on
    /// <c>1 / AttackSpeed</c>, the Cast paced by how fast Resource refills against
    /// <see cref="IHeroCombatant.CastCost"/>. Both are fed hand-metered clock deltas and read
    /// out through the damage a lone enemy takes.
    /// </summary>
    [TestFixture]
    public sealed class HeroAttackCadenceTests
    {
        private static EncounterTuning FastCast() => new() { CastCadence = 0.05f };

        [Test]
        public void Strike_LandsOnOneOverAttackSpeed()
        {
            var hero = new FakeHero
            {
                AttackSpeed = 2f,       // interval 0.5 s → every 5th tick
                PhysicalDamage = 1f,
                MagicalDamage = 0f,
                Resource = 0f,          // never casts
            };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Skirmisher), new ConstantRollSource(0f), engagementTarget: 5, FastCast());
            var enemy = sim.Enemies[0];

            // 2.0 s of combat = 4 whole Strike intervals.
            for (var i = 0; i < 20; i++) sim.Advance(0.1f);

            Assert.That(enemy.MaxHealth - enemy.Health, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void Strike_DoesNotLandBeforeItsFirstWholeInterval()
        {
            var hero = new FakeHero { AttackSpeed = 2f, PhysicalDamage = 1f, MagicalDamage = 0f, Resource = 0f };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Skirmisher), new ConstantRollSource(0f), engagementTarget: 5, FastCast());
            var enemy = sim.Enemies[0];

            for (var i = 0; i < 4; i++) sim.Advance(0.1f); // 0.4 s < 0.5 s

            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
        }

        [Test]
        public void Cast_LandsOnItsResourceFedCadence()
        {
            // Resource +20/s, CastCost 20 → a Cast is affordable once per second.
            var hero = new FakeHero
            {
                PhysicalDamage = 0f,          // Strike does nothing observable
                MagicalDamage = 1f,
                CastCost = 20f,
                MaxResource = 1000f,
                Resource = 0f,
                ResourceRegenPerSecond = 20f,
            };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f), engagementTarget: 5, FastCast());
            var enemy = sim.Enemies[0];

            // 3.1 s → Casts affordable at t = 1, 2, 3.
            // Hero regen runs before the sim tick (issue #45 moved it to the driver).
            for (var i = 0; i < 31; i++)
            {
                hero.Regenerate(0.1f);
                sim.Advance(0.1f);
            }

            Assert.That(enemy.MaxHealth - enemy.Health, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void Cast_HoldsWhileResourceIsBelowItsCost()
        {
            var hero = new FakeHero
            {
                PhysicalDamage = 0f,
                MagicalDamage = 1f,
                CastCost = 50f,
                MaxResource = 1000f,
                Resource = 0f,
                ResourceRegenPerSecond = 5f, // needs 10 s to afford one Cast
            };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f), engagementTarget: 5, FastCast());
            var enemy = sim.Enemies[0];

            // Hero regen runs before the sim tick (issue #45 moved it to the driver).
            for (var i = 0; i < 50; i++)
            {
                hero.Regenerate(0.1f);
                sim.Advance(0.1f); // 5 s — never affordable
            }

            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
        }

    }
}
