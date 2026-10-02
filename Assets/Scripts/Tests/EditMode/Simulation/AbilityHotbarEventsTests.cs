using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="EncounterSimulation.HeroStriked"/> and <see cref="EncounterSimulation.HeroCast"/>
    /// are the seam the Ability hotbar (issue #62) flashes its two icons on — raised exactly when
    /// the matching attack actually lands, not merely on the tick its cadence elapses.
    /// </summary>
    [TestFixture]
    public sealed class AbilityHotbarEventsTests
    {
        private static EncounterTuning FastCast() => new() { CastCadence = 0.05f };

        [Test]
        public void HeroStriked_FiresOnceForEachLandedStrike()
        {
            var hero = new FakeHero { AttackSpeed = 10f, PhysicalDamage = 1f, MagicalDamage = 0f, Resource = 0f };
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), FastCast());

            var strikes = 0;
            sim.HeroStriked += () => strikes++;

            for (var i = 0; i < 4; i++) sim.Advance(0.1f); // Strike every tick

            Assert.That(strikes, Is.EqualTo(4));
        }

        [Test]
        public void HeroStriked_DoesNotFireBeforeTheFirstWholeInterval()
        {
            var hero = new FakeHero { AttackSpeed = 2f, PhysicalDamage = 1f, MagicalDamage = 0f, Resource = 0f };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Skirmisher), new ConstantRollSource(0f), Behaviours.Engaging(5), FastCast());

            var strikes = 0;
            sim.HeroStriked += () => strikes++;

            for (var i = 0; i < 4; i++) sim.Advance(0.1f); // 0.4 s < 0.5 s interval

            Assert.That(strikes, Is.EqualTo(0));
        }

        [Test]
        public void HeroCast_FiresOnceForEachAffordableCast()
        {
            var hero = new FakeHero
            {
                PhysicalDamage = 0f,
                MagicalDamage = 1f,
                CastCost = 20f,
                MaxResource = 1000f,
                Resource = 0f,
                ResourceRegenPerSecond = 20f,
            };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f), Behaviours.Engaging(5), FastCast());

            var casts = 0;
            sim.HeroCast += () => casts++;

            // 3.1 s → Casts affordable at t = 1, 2, 3.
            for (var i = 0; i < 31; i++)
            {
                hero.Regenerate(0.1f);
                sim.Advance(0.1f);
            }

            Assert.That(casts, Is.EqualTo(3));
        }

        [Test]
        public void HeroCast_DoesNotFireWhileResourceIsBelowItsCost()
        {
            var hero = new FakeHero
            {
                PhysicalDamage = 0f,
                MagicalDamage = 1f,
                CastCost = 50f,
                MaxResource = 1000f,
                Resource = 0f,
                ResourceRegenPerSecond = 5f, // never reaches CastCost inside this window
            };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f), Behaviours.Engaging(5), FastCast());

            var casts = 0;
            sim.HeroCast += () => casts++;

            for (var i = 0; i < 50; i++)
            {
                hero.Regenerate(0.1f);
                sim.Advance(0.1f);
            }

            Assert.That(casts, Is.EqualTo(0));
        }
    }
}
