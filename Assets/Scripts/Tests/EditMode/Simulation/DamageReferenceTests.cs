using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The reference maximum a damage number's size is read against (issue #213): the best raw hit of a damage type
    /// the dealer can land, with the damage spread applied. For the hero it is his own damage stat of that type; for
    /// hits on the hero it is the strongest hit of that type among the archetypes the Location can field. Source
    /// level 5 reads Brute damage 1 + 0.82 * 5 = 5.1 and Skirmisher damage 0.5 + 0.5 * 5 = 3.
    /// </summary>
    [TestFixture]
    public sealed class DamageReferenceTests
    {
        private const float Spread = 0.2f;
        private const float Eps = 1e-4f;

        private static EncounterProfile Location(int brutes, int skirmishers, int sourceLevel = 5) => new(
            sourceLevel: sourceLevel,
            packed: EnemyArchetype.Brute,
            rosterBrute: new IntRange(brutes),
            rosterSkirmisher: new IntRange(skirmishers),
            packBatch: new IntRange(1),
            packedSpawnWeight: 0.5f,
            spawnInterval: 10f,
            table: FakeLootTable.ForCategory(ItemCategory.Equipment));

        // --- the hero's hits -----------------------------------------------------

        [Test]
        public void TheHerosPhysicalHit_IsHisPhysicalDamageWithTheSpreadApplied()
        {
            var hero = new FakeHero { PhysicalDamage = 10f, MagicalDamage = 99f };

            Assert.That(DamageReference.OfHero(hero, DamageType.PhysicalDamage, Spread), Is.EqualTo(12f).Within(Eps));
        }

        [Test]
        public void TheHerosMagicalHit_IsHisMagicalDamageWithTheSpreadApplied()
        {
            var hero = new FakeHero { PhysicalDamage = 99f, MagicalDamage = 20f };

            Assert.That(DamageReference.OfHero(hero, DamageType.MagicalDamage, Spread), Is.EqualTo(24f).Within(Eps));
        }

        [Test]
        public void TheHerosHit_WithNoSpread_IsTheStatItself()
        {
            var hero = new FakeHero { PhysicalDamage = 10f };

            Assert.That(DamageReference.OfHero(hero, DamageType.PhysicalDamage, 0f), Is.EqualTo(10f).Within(Eps));
        }

        [Test]
        public void TheHerosHit_IsReadLive()
        {
            var hero = new FakeHero { PhysicalDamage = 10f };
            DamageReference.OfHero(hero, DamageType.PhysicalDamage, Spread);

            hero.PhysicalDamage = 20f;

            Assert.That(DamageReference.OfHero(hero, DamageType.PhysicalDamage, Spread), Is.EqualTo(24f).Within(Eps));
        }

        // --- hits on the hero ------------------------------------------------------

        [Test]
        public void ALocationOfBrutes_HitsPhysicallyAtTheBrutesCurve()
        {
            Assert.That(DamageReference.OfEnemies(Location(brutes: 3, skirmishers: 0), DamageType.PhysicalDamage, Spread),
                Is.EqualTo(6.12f).Within(Eps));
        }

        [Test]
        public void ALocationOfBrutes_CannotHitMagically_SoTheReferenceIsZero()
        {
            Assert.That(DamageReference.OfEnemies(Location(brutes: 3, skirmishers: 0), DamageType.MagicalDamage, Spread), Is.EqualTo(0f));
        }

        [Test]
        public void ALocationOfSkirmishers_HitsMagicallyAtTheSkirmishersCurve()
        {
            Assert.That(DamageReference.OfEnemies(Location(brutes: 0, skirmishers: 4), DamageType.MagicalDamage, Spread),
                Is.EqualTo(3.6f).Within(Eps));
        }

        [Test]
        public void ALocationOfSkirmishers_CannotHitPhysically_SoTheReferenceIsZero()
        {
            Assert.That(DamageReference.OfEnemies(Location(brutes: 0, skirmishers: 4), DamageType.PhysicalDamage, Spread), Is.EqualTo(0f));
        }

        [Test]
        public void AMixedLocation_HasAReferenceForEachType()
        {
            var location = Location(brutes: 2, skirmishers: 2);

            Assert.That(DamageReference.OfEnemies(location, DamageType.PhysicalDamage, Spread), Is.EqualTo(6.12f).Within(Eps));
            Assert.That(DamageReference.OfEnemies(location, DamageType.MagicalDamage, Spread), Is.EqualTo(3.6f).Within(Eps));
        }

        [Test]
        public void TheReference_FollowsTheLocationsSourceLevel()
        {
            // Brute damage at level 10: 1 + 0.82 * 10 = 9.2.
            Assert.That(DamageReference.OfEnemies(Location(brutes: 1, skirmishers: 0, sourceLevel: 10), DamageType.PhysicalDamage, 0f),
                Is.EqualTo(9.2f).Within(Eps));
        }

        [Test]
        public void ARosterThatMayRollSome_IsFielded()
        {
            // A range of 0..2 can roll 2, so the Location can field it; Location(0, ...) above cannot.
            var maybe = new EncounterProfile(5, EnemyArchetype.Brute, new IntRange(0, 2), new IntRange(2), new IntRange(1), 0.5f, 10f,
                FakeLootTable.ForCategory(ItemCategory.Equipment));

            Assert.That(DamageReference.OfEnemies(maybe, DamageType.PhysicalDamage, 0f), Is.EqualTo(5.1f).Within(Eps));
        }
    }
}
