using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The Spawn Profile (ADR-0010 second amendment): a per-archetype Roster arrives over the
    /// fight — the packed type in Packs, the other singly — refilling toward the soft
    /// <see cref="EncounterSimulation.EngagementTarget"/>. Engagement is a target the fight
    /// holds, not a ceiling; a Pack overshoots it; spawning stops once the Roster is spent.
    ///
    /// The hero deals no damage in these tests, so nothing dies and the spawn side is isolated.
    /// </summary>
    [TestFixture]
    public sealed class SpawnScheduleTests
    {
        private static FakeHero InertHero() => new()
        {
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            Resource = 0f,
            AttackSpeed = 0.01f,
        };

        [Test]
        public void Spawning_HoldsAliveCountAtTheEngagementTarget()
        {
            var profile = new EncounterProfile(
                sourceLevel: 3,
                packed: EnemyArchetype.Skirmisher,
                rosterBrute: new IntRange(0),
                rosterSkirmisher: new IntRange(12),
                packBatch: new IntRange(1),          // trickle — no real Pack
                packedSpawnWeight: 1f,
                spawnInterval: 0.5f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment),
                spawnJitter: 0f,
                initialSpawn: 2);

            var sim = new EncounterSimulation(InertHero(), profile, new ConstantRollSource(0f), engagementTarget: 3);

            for (var i = 0; i < 200; i++) sim.Advance(0.1f); // 20 s

            Assert.That(sim.AliveEnemyCount, Is.EqualTo(3));
            Assert.That(sim.EncountersCleared, Is.EqualTo(0), "nothing died, so nothing cleared");
        }

        [Test]
        public void APack_OvershootsTheEngagementTarget()
        {
            var profile = new EncounterProfile(
                sourceLevel: 3,
                packed: EnemyArchetype.Brute,
                rosterBrute: new IntRange(6),
                rosterSkirmisher: new IntRange(0),
                packBatch: new IntRange(3),          // a real Pack of 3
                packedSpawnWeight: 1f,
                spawnInterval: 0.5f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment),
                spawnJitter: 0f,
                initialSpawn: 1);

            var sim = new EncounterSimulation(InertHero(), profile, new ConstantRollSource(0f), engagementTarget: 2);

            // Long enough for the first post-initial spawn tick to fire.
            for (var i = 0; i < 10; i++) sim.Advance(0.1f);

            Assert.That(sim.AliveEnemyCount, Is.EqualTo(4), "1 initial + a Pack of 3, past the target of 2");
        }

        [Test]
        public void Spawning_StopsOnceTheRosterIsSpent()
        {
            var profile = new EncounterProfile(
                sourceLevel: 3,
                packed: EnemyArchetype.Brute,
                rosterBrute: new IntRange(2),
                rosterSkirmisher: new IntRange(0),
                packBatch: new IntRange(1),
                packedSpawnWeight: 1f,
                spawnInterval: 0.4f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment),
                spawnJitter: 0f,
                initialSpawn: 1);

            var sim = new EncounterSimulation(InertHero(), profile, new ConstantRollSource(0f), engagementTarget: 10);

            for (var i = 0; i < 200; i++) sim.Advance(0.1f);

            Assert.That(sim.AliveEnemyCount, Is.EqualTo(2), "1 initial + 1 more = the whole Roster; no more");
        }

        [Test]
        public void ThePackedArchetypeArrivesInPacks_TheOtherOneAtATime()
        {
            var profile = new EncounterProfile(
                sourceLevel: 3,
                packed: EnemyArchetype.Brute,
                rosterBrute: new IntRange(20),
                rosterSkirmisher: new IntRange(20),
                packBatch: new IntRange(4),          // fixed → no roll drawn for it
                packedSpawnWeight: 0.5f,
                spawnInterval: 0.05f,                 // < tick, so every tick is a spawn tick
                table: FakeLootTable.ForCategory(ItemCategory.Equipment),
                spawnJitter: 0f,
                initialSpawn: 0);

            // Each spawn draws: type roll, then one desync per body, then a jitter roll.
            //   type ≥ 0.5 → Other (Skirmisher), batch 1  → 3 rolls
            //   type < 0.5 → packed (Brute), batch 4       → 6 rolls
            var rolls = new QueuedRollSource(
                0.9f, 0f, 0f,                      // tick 1 → 1 Skirmisher
                0.1f, 0f, 0f, 0f, 0f, 0f);         // tick 2 → a Pack of 4 Brutes

            var sim = new EncounterSimulation(InertHero(), profile, rolls, engagementTarget: 100);

            sim.Advance(0.1f); // first spawn tick — a lone Skirmisher
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(1));
            Assert.That(sim.Enemies.Single().Archetype, Is.EqualTo(EnemyArchetype.Skirmisher));

            sim.Advance(0.1f); // second spawn tick — a Brute Pack of 4
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(5));
            Assert.That(sim.Enemies.Count(e => e.Archetype == EnemyArchetype.Brute), Is.EqualTo(4));
        }

        [Test]
        public void InitialPresence_DrawsThePackedTypeFirst()
        {
            var profile = new EncounterProfile(
                sourceLevel: 3,
                packed: EnemyArchetype.Skirmisher,
                rosterBrute: new IntRange(5),
                rosterSkirmisher: new IntRange(5),
                packBatch: new IntRange(2),
                packedSpawnWeight: 0.5f,
                spawnInterval: 5f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment),
                spawnJitter: 0f,
                initialSpawn: 3);

            var sim = new EncounterSimulation(InertHero(), profile, new ConstantRollSource(0f), engagementTarget: 10);

            // 3 bodies at open: packed (Skirmisher) fills first, so 3 Skirmishers before any Brute.
            Assert.That(sim.Enemies.Count(e => e.Archetype == EnemyArchetype.Skirmisher), Is.EqualTo(3));
            Assert.That(sim.Enemies.Count(e => e.Archetype == EnemyArchetype.Brute), Is.EqualTo(0));
        }
    }
}
