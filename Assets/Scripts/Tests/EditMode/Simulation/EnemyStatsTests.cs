using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Stat-backed enemies and their magic resist (issue #210). An enemy carries a modifiable stat for every stat
    /// its archetype defines, with base values off the archetype curves at the Location's source level; a
    /// modifier on one of them changes the fight, read through the simulation. Enemies still have no regeneration
    /// and no resource pool.
    /// </summary>
    [TestFixture]
    public sealed class EnemyStatsTests
    {
        private const float Tick = 0.1f;

        private static StatModifier Set(float value) => new(new Vector2Int(0, 1000), value, StatModifierType.Overwrite);
        private static StatModifier Flat(float value) => new(new Vector2Int(-1000, 1000), value, StatModifierType.FlatAdd);

        private static FakeHero Caster(float magicalDamage) => new()
        {
            PhysicalDamage = 0f, MagicalDamage = magicalDamage, Resource = 100f, CastCost = 16f, AttackSpeed = 0.01f,
        };

        private static EncounterSimulation Sim(FakeHero hero, EnemyArchetype archetype) => new(hero, Profiles.Solo(archetype),
            new ConstantRollSource(0f), Behaviours.Engaging(1), new EncounterTuning());

        private static void Fight(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++) sim.Advance(Tick);
        }

        // --- the stats an enemy carries ---------------------------------------

        [Test]
        public void ABrute_CarriesAStatForEachOfItsDefinedStats_WithBasesOffTheCurvesAtItsSourceLevel()
        {
            var curves = EnemyArchetypes.Brute;
            var brute = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);

            Assert.That(brute.Stat(StatName.Health).BaseValue, Is.EqualTo(curves.Health.At(5)).Within(0.001f));
            Assert.That(brute.Stat(StatName.Armor).BaseValue, Is.EqualTo(curves.ArmorPercent.At(5)).Within(0.001f));
            Assert.That(brute.Stat(StatName.MagicResist).BaseValue, Is.EqualTo(curves.MagicResistPercent.At(5)).Within(0.001f));
            Assert.That(brute.Stat(StatName.PhysicalDamage).BaseValue, Is.EqualTo(curves.Damage.At(5)).Within(0.001f));
            Assert.That(brute.Stat(StatName.AttackSpeed).BaseValue, Is.EqualTo(curves.AttackSpeed));
            Assert.That(brute.Stat(StatName.MovementSpeed).BaseValue, Is.EqualTo(curves.MovementSpeed));
        }

        [Test]
        public void TheDamageStat_IsTheOneOfTheArchetypesDamageType()
        {
            var brute = new Enemy(EnemyArchetype.Brute, 5);
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, 5);

            Assert.That(brute.Stat(StatName.PhysicalDamage), Is.Not.Null);
            Assert.That(brute.Stat(StatName.MagicalDamage), Is.Null, "a physical striker has no magical damage stat");
            Assert.That(skirmisher.Stat(StatName.MagicalDamage), Is.Not.Null);
            Assert.That(skirmisher.Stat(StatName.PhysicalDamage), Is.Null, "a magical striker has no physical damage stat");
            Assert.That(skirmisher.StrikeDamage, Is.EqualTo(EnemyArchetypes.Skirmisher.Damage.At(5)).Within(0.001f));
        }

        [Test]
        public void TheHealthStat_IsTheResourceTheHealthBarRenders()
        {
            var brute = new Enemy(EnemyArchetype.Brute, 5);

            Assert.That(brute.Stat(StatName.Health), Is.SameAs(brute.HealthResource));
        }

        [Test]
        public void AnEnemy_HasNoRegenerationAndNoResourcePool()
        {
            var brute = new Enemy(EnemyArchetype.Brute, 5);

            foreach (var absent in new[] { StatName.HealthRegeneration, StatName.Resource, StatName.ResourceRegeneration, StatName.Shield })
                Assert.That(brute.Stat(absent), Is.Null, absent.ToString());

            brute.ReceivePhysical(brute.MaxHealth / 2f);
            var hurt = brute.Health;
            brute.Regenerate(1000f);
            Assert.That(brute.Health, Is.EqualTo(hurt), "it does not heal");
        }

        // --- a modifier changes the fight -------------------------------------

        [Test]
        public void AnArmorModifier_ChangesWhatAPhysicalHitTakes()
        {
            var plain = new Enemy(EnemyArchetype.Brute, 5);
            var bare = new Enemy(EnemyArchetype.Brute, 5);
            bare.Stat(StatName.Armor).AddModifier(Set(0f));

            plain.ReceivePhysical(10f);
            bare.ReceivePhysical(10f);

            Assert.That(bare.MaxHealth - bare.Health, Is.EqualTo(10f).Within(0.0001f), "no armor: the whole hit");
            Assert.That(plain.MaxHealth - plain.Health, Is.LessThan(10f), "negative control: the unmodified Brute is armored");
        }

        [Test]
        public void AMovementSpeedModifier_ChangesHowFarItWalksInATick()
        {
            float WalkedIn(float speedMultiplier)
            {
                var sim = new EncounterSimulation(Caster(0f), Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f),
                    Behaviours.Engaging(1), new EncounterTuning { Ground = GroundTuning.Standard() });
                var brute = sim.Enemies.Single();
                brute.Stat(StatName.MovementSpeed).AddModifier(Set(brute.MovementSpeed * speedMultiplier));
                var before = Coordinate.Distance(brute.Position, sim.HeroPosition);
                sim.Advance(Tick);
                return before - Coordinate.Distance(brute.Position, sim.HeroPosition);
            }

            Assert.That(WalkedIn(2f), Is.EqualTo(2f * WalkedIn(1f)).Within(0.001f));
            Assert.That(WalkedIn(1f), Is.GreaterThan(0f), "negative control: it does walk at its base speed");
        }

        [Test]
        public void ADamageModifier_ChangesWhatItsStrikeDeals()
        {
            float TakenFromABruteWith(float extraDamage)
            {
                var hero = Caster(0f);
                var sim = Sim(hero, EnemyArchetype.Brute);
                sim.Enemies.Single().Stat(StatName.PhysicalDamage).AddModifier(Flat(extraDamage));
                Fight(sim, 19); // cadence 1 / 0.55 s with the desync roll at 0: exactly one Strike by tick 19
                return hero.PhysicalDamageTaken;
            }

            var baseline = TakenFromABruteWith(0f);

            Assert.That(baseline, Is.GreaterThan(0f), "premise: the Strike landed");
            Assert.That(TakenFromABruteWith(10f), Is.EqualTo(baseline + 10f).Within(0.001f));
        }

        // --- magic resist -----------------------------------------------------

        [Test]
        public void ACast_IsMitigatedByTheEnemysMagicResist()
        {
            var hero = Caster(20f);
            var sim = Sim(hero, EnemyArchetype.Brute);
            var brute = sim.Enemies.Single();
            brute.Stat(StatName.MagicResist).AddModifier(Set(50f));

            Fight(sim, 4); // the first Cast lands on tick 4

            Assert.That(brute.MaxHealth - brute.Health, Is.EqualTo(10f).Within(0.0001f), "20 * (1 - 50%)");
        }

        [Test]
        public void ACast_AtTheArchetypesOwnMagicResist_IsMitigatedByTheCurveValue()
        {
            var hero = Caster(20f);
            var sim = Sim(hero, EnemyArchetype.Brute);
            var brute = sim.Enemies.Single();

            Fight(sim, 4);

            Assert.That(brute.MagicResistPercent, Is.EqualTo(2f).Within(0.0001f), "Brute: 0.4 per source level, so 2 at S5");
            Assert.That(brute.MaxHealth - brute.Health, Is.EqualTo(19.6f).Within(0.0001f));
        }

        [Test]
        public void MagicResist_DoesNotMitigateAPhysicalHit_AndArmorDoesNotMitigateAMagicalOne()
        {
            var resistant = new Enemy(EnemyArchetype.Skirmisher, 5);
            resistant.Stat(StatName.MagicResist).AddModifier(Set(100f));
            var armored = new Enemy(EnemyArchetype.Skirmisher, 5);
            armored.Stat(StatName.Armor).AddModifier(Set(100f));

            resistant.ReceivePhysical(7f);
            armored.ReceiveMagical(7f);

            Assert.That(resistant.MaxHealth - resistant.Health, Is.EqualTo(7f).Within(0.0001f), "full magic resist, still takes the physical hit");
            Assert.That(armored.MaxHealth - armored.Health, Is.GreaterThan(6f), "full armor, still takes (almost all of) the magical hit");
        }

        [Test]
        public void AResistPast100_NeverHealsTheEnemy()
        {
            var brute = new Enemy(EnemyArchetype.Brute, 5);
            brute.Stat(StatName.MagicResist).AddModifier(Set(250f));
            brute.ReceivePhysical(10f);
            var hurt = brute.Health;

            brute.ReceiveMagical(10f);

            Assert.That(brute.Health, Is.EqualTo(hurt), "clamped to full mitigation, not a heal");
        }
    }
}
