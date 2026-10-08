using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Archetype damage types (issue #210): each archetype declares the type its Strike deals - Brute physical,
    /// Skirmisher magical - and the hero mitigates it with the matching resist. Driven through the simulation's
    /// advance entry point on the collapsed default ground, where every enemy is in reach from the first tick.
    /// Each mitigation test is paired with the opposite resist as its negative control.
    /// </summary>
    [TestFixture]
    public sealed class EnemyDamageTypeTests
    {
        private const float Tick = 0.1f;

        private static FakeHero Passive() => new() { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f };

        private static EncounterSimulation Sim(FakeHero hero, EnemyArchetype archetype) => new(hero, Profiles.Solo(archetype),
            new ConstantRollSource(0f), Behaviours.Engaging(1), new EncounterTuning());

        private static void Fight(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++) sim.Advance(Tick);
        }

        [Test]
        public void EachArchetype_DeclaresTheDamageTypeOfItsStrike()
        {
            Assert.That(EnemyArchetypes.Of(EnemyArchetype.Brute).DamageType, Is.EqualTo(DamageType.PhysicalDamage));
            Assert.That(EnemyArchetypes.Of(EnemyArchetype.Skirmisher).DamageType, Is.EqualTo(DamageType.MagicalDamage));
            Assert.That(new Enemy(EnemyArchetype.Brute, 5).StrikeDamageType, Is.EqualTo(DamageType.PhysicalDamage));
            Assert.That(new Enemy(EnemyArchetype.Skirmisher, 5).StrikeDamageType, Is.EqualTo(DamageType.MagicalDamage));
        }

        [Test]
        public void ABruteStrike_IsPhysical_SoArmorStopsItAndMagicResistDoesNot()
        {
            var armored = Passive();
            armored.ArmorPercent = 100f;
            var resistant = Passive();
            resistant.MagicResistPercent = 100f;

            Fight(Sim(armored, EnemyArchetype.Brute), 60);
            Fight(Sim(resistant, EnemyArchetype.Brute), 60);

            Assert.That(armored.Health, Is.EqualTo(armored.MaxHealth), "full armor stops a physical hit");
            Assert.That(resistant.PhysicalDamageTaken, Is.GreaterThan(0f), "negative control: magic resist does not");
            Assert.That(resistant.MagicalDamageTaken, Is.EqualTo(0f), "and nothing arrived as magical damage");
        }

        [Test]
        public void ASkirmisherStrike_IsMagical_SoMagicResistStopsItAndArmorDoesNot()
        {
            var resistant = Passive();
            resistant.MagicResistPercent = 100f;
            var armored = Passive();
            armored.ArmorPercent = 100f;

            Fight(Sim(resistant, EnemyArchetype.Skirmisher), 60);
            Fight(Sim(armored, EnemyArchetype.Skirmisher), 60);

            Assert.That(resistant.Health, Is.EqualTo(resistant.MaxHealth), "full magic resist stops a magical hit");
            Assert.That(armored.MagicalDamageTaken, Is.GreaterThan(0f), "negative control: armor does not");
            Assert.That(armored.PhysicalDamageTaken, Is.EqualTo(0f), "and nothing arrived as physical damage");
        }

        [Test]
        public void AStrike_DealsTheEnemysDamageStatOfItsType_Mitigated()
        {
            var hero = Passive();
            hero.MagicResistPercent = 25f;
            var sim = Sim(hero, EnemyArchetype.Skirmisher);
            var skirmisher = sim.Enemies.Single();

            Fight(sim, 7); // cadence 1 / 1.6 s with the desync roll at 0: exactly one hit has landed by tick 7

            Assert.That(hero.MagicalDamageTaken, Is.EqualTo(skirmisher.StrikeDamage * 0.75f).Within(0.0001f));
        }
    }
}
