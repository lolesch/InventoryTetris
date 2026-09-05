using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="EncounterProfile"/> is the engine-free stand-in for the <c>LocationConfig</c>
    /// ScriptableObject (issue #25); its constructor enforces the same rules #25 will, so a
    /// hand-built profile fails the same way an ill-authored asset would.
    /// </summary>
    [TestFixture]
    public sealed class EncounterProfileTests
    {
        private static EncounterProfile Build(
            int sourceLevel = 3,
            int bruteMin = 1, int bruteMax = 1,
            int skirmisherMin = 1, int skirmisherMax = 1,
            int packBatchMin = 1, int packBatchMax = 2,
            float packedSpawnWeight = 0.5f,
            float spawnInterval = 2f,
            float spawnJitter = 0f,
            int initialSpawn = 1) => new(
            sourceLevel, EnemyArchetype.Brute,
            new IntRange(bruteMin, bruteMax), new IntRange(skirmisherMin, skirmisherMax),
            new IntRange(packBatchMin, packBatchMax),
            packedSpawnWeight, spawnInterval,
            FakeLootTable.ForCategory(ItemCategory.Equipment),
            spawnJitter, initialSpawn);

        [Test]
        public void AValidProfile_Constructs() => Assert.That(() => Build(), Throws.Nothing);

        [Test]
        public void SourceLevelBelowOne_Throws() =>
            Assert.That(() => Build(sourceLevel: 0), Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void ANegativeRosterMinimum_Throws() =>
            Assert.That(() => Build(bruteMin: -1, bruteMax: 2), Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void ARosterMaxBelowItsMin_Throws() =>
            Assert.That(() => Build(skirmisherMin: 5, skirmisherMax: 2), Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void AnEmptyRoster_Throws() =>
            Assert.That(() => Build(bruteMin: 0, bruteMax: 0, skirmisherMin: 0, skirmisherMax: 0),
                Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void APackBatchBelowOne_Throws() =>
            Assert.That(() => Build(packBatchMin: 0, packBatchMax: 3), Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void APackedSpawnWeightOutsideZeroToOne_Throws()
        {
            Assert.That(() => Build(packedSpawnWeight: -0.1f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => Build(packedSpawnWeight: 1.1f), Throws.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void ANonPositiveSpawnInterval_Throws() =>
            Assert.That(() => Build(spawnInterval: 0f), Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void ANegativeSpawnJitter_Throws() =>
            Assert.That(() => Build(spawnJitter: -1f), Throws.InstanceOf<System.ArgumentException>());

        [Test]
        public void ANullLootTable_Throws() =>
            Assert.That(() => new EncounterProfile(
                3, EnemyArchetype.Brute,
                new IntRange(1), new IntRange(1), new IntRange(1),
                0.5f, 2f, table: null),
                Throws.ArgumentNullException);

        [Test]
        public void RosterFor_ReturnsThePerArchetypeCount()
        {
            var profile = Build(bruteMin: 4, bruteMax: 4, skirmisherMin: 9, skirmisherMax: 9);

            Assert.That(profile.RosterFor(EnemyArchetype.Brute).Min, Is.EqualTo(4));
            Assert.That(profile.RosterFor(EnemyArchetype.Skirmisher).Min, Is.EqualTo(9));
        }

        [Test]
        public void TheConstructor_RejectsANullHeroProfileAndTuning()
        {
            var profile = Build();
            var rolls = new ConstantRollSource(0f);

            Assert.That(() => new EncounterSimulation(null, profile, rolls, 3), Throws.ArgumentNullException);
            Assert.That(() => new EncounterSimulation(new FakeHero(), null, rolls, 3), Throws.ArgumentNullException);
            Assert.That(() => new EncounterSimulation(new FakeHero(), profile, null, 3), Throws.ArgumentNullException);
            Assert.That(() => new EncounterSimulation(new FakeHero(), profile, rolls, 0), Throws.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void EncounterTuning_RejectsANonPositiveTick() =>
            Assert.That(() => new EncounterSimulation(new FakeHero(), Build(), new ConstantRollSource(0f), 3,
                new EncounterTuning { Tick = 0f }), Throws.InstanceOf<System.ArgumentException>());
    }
}
