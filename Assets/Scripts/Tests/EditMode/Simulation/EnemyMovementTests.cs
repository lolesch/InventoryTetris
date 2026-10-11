using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Enemies stand on the simulation's arena (issue #207): they spawn at its edge on a bearing the sim
    /// picks, walk in on sim time toward the hero and stop within their Strike Range. Everything is observed
    /// through <see cref="EncounterSimulation.Advance"/> with a passive fake hero and scripted movement rolls;
    /// the standard arena is radius 10 + margin 2, so a spawn is 12 units from the origin.
    /// </summary>
    [TestFixture]
    public sealed class EnemyMovementTests
    {
        private const float SpawnDistance = 12f;
        private const float Tick = 0.1f;

        // Neither attack can land or fire: the hero only stands there.
        private static FakeHero Passive() => new() { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f };

        private static EncounterTuning OnTheArena(Coordinate origin = default)
        {
            var arena = ArenaTuning.Standard();
            arena.Origin = origin;
            return new EncounterTuning { Arena = arena };
        }

        private static EncounterSimulation Sim(EncounterProfile profile, IRollSource movement, EncounterTuning tuning = null,
            FakeHero hero = null) => new(hero ?? Passive(), profile, new ConstantRollSource(0f), Behaviours.Engaging(10),
            tuning ?? OnTheArena(), bag: null, movementRolls: movement);

        private static float DistanceToHero(EncounterSimulation sim, Enemy enemy) =>
            Coordinate.Distance(enemy.Position, sim.HeroPosition);

        // Degrees in 0..360, positive from +x toward +z, as the sim reports a bearing.
        private static float AngleOf(Coordinate fromOrigin) =>
            (Coordinate.SignedAngle(new Coordinate(1f, 0f), fromOrigin) + 360f) % 360f;

        [Test]
        public void AnEnemy_SpawnsBeyondTheArenasEdge_AroundTheOrigin()
        {
            var origin = new Coordinate(3f, -4f);
            var sim = Sim(Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0.5f), OnTheArena(origin));

            var enemy = sim.Enemies.Single();

            Assert.That(Coordinate.Distance(enemy.Position, origin), Is.EqualTo(SpawnDistance).Within(0.001f));
            Assert.That(sim.HeroPosition, Is.EqualTo(origin), "the hero stands at the origin");
        }

        [Test]
        public void APack_SpreadsAroundTheArena_InsteadOfStackingOnOneBearing()
        {
            var sim = Sim(Profiles.Group(EnemyArchetype.Skirmisher, 4), new ConstantRollSource(0.5f));

            var bearings = sim.Enemies.Select(e => e.Bearing).OrderBy(b => b).ToList();

            Assert.That(bearings, Has.Count.EqualTo(4));
            for (var i = 0; i < bearings.Count; i++)
            {
                var next = bearings[(i + 1) % bearings.Count] + (i == bearings.Count - 1 ? 360f : 0f);
                Assert.That(next - bearings[i], Is.EqualTo(90f).Within(0.01f), "the largest gap is always bisected");
            }
        }

        [Test]
        public void AnEnemysBearing_IsWhereItStands_SeenFromTheOrigin()
        {
            var sim = Sim(Profiles.Group(EnemyArchetype.Skirmisher, 4), new ConstantRollSource(0.5f));

            foreach (var enemy in sim.Enemies)
                Assert.That(AngleOf(enemy.Position - sim.HeroPosition), Is.EqualTo(enemy.Bearing).Within(0.01f));
        }

        [Test]
        public void TheSpawnBearing_Jitters_ButOnlyFromTheMovementStream()
        {
            float FirstBearing(float roll) =>
                Sim(Profiles.Solo(EnemyArchetype.Brute), new QueuedRollSource(roll, 0f)).Enemies.Single().Bearing;

            var low = FirstBearing(0f);
            var mid = FirstBearing(0.5f);
            var high = FirstBearing(0.99f);

            Assert.That(low, Is.LessThan(mid));
            Assert.That(mid, Is.LessThan(high));
            Assert.That(low, Is.GreaterThan(0f));
            Assert.That(high, Is.LessThan(360f));
        }

        [Test]
        public void AnEnemy_KeepsItsBearing_WhileItWalksIn()
        {
            var sim = Sim(Profiles.Group(EnemyArchetype.Brute, 3), new ConstantRollSource(0.5f));
            var before = sim.Enemies.ToDictionary(e => e, e => e.Bearing);

            for (var i = 0; i < 30; i++) sim.Advance(Tick);

            foreach (var enemy in sim.Enemies)
            {
                Assert.That(enemy.Bearing, Is.EqualTo(before[enemy]));
                Assert.That(AngleOf(enemy.Position - sim.HeroPosition), Is.EqualTo(enemy.Bearing).Within(0.05f),
                    "it walked straight in along its bearing");
            }
        }

        [Test]
        public void ABearing_IsFreedWhenItsEnemyFalls_AndTheNextSpawnTakesTheLargestGap()
        {
            // Three at open (180, 0, 90 under neutral jitter), a fourth a spawn interval later.
            var profile = new EncounterProfile(
                sourceLevel: 5, packed: EnemyArchetype.Brute, rosterBrute: new IntRange(4), rosterSkirmisher: new IntRange(0),
                packBatch: new IntRange(1), packedSpawnWeight: 1f, spawnInterval: 1f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment), spawnJitter: 0f, initialSpawn: 3);
            var sim = Sim(profile, new ConstantRollSource(0.5f));

            var gone = sim.Enemies.Single(e => e.Bearing.Equals(sim.Enemies.Min(x => x.Bearing)));
            gone.ReceivePhysical(gone.MaxHealth * 100f); // down, so its bearing no longer counts

            for (var i = 0; i < 12 && sim.Enemies.Count < 4; i++) sim.Advance(Tick);

            Assert.That(sim.Enemies, Has.Count.EqualTo(4));
            // Living bearings 90 and 180: the wide gap runs 180 -> 450, so its middle is 315 (the fallen 0 is inside it).
            Assert.That(sim.Enemies.Last().Bearing, Is.EqualTo(315f).Within(0.01f), "the arrival bisects the gap the fall opened");
            Assert.That(gone.Bearing, Is.EqualTo(0f).Within(0.01f), "premise: the fallen one stood at 0");
        }

        [Test]
        public void AnEnemy_ChasesTheHero_AtItsMovementSpeed()
        {
            var sim = Sim(Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0.5f));
            var enemy = sim.Enemies.Single();

            for (var i = 0; i < 10; i++) sim.Advance(Tick); // one second

            Assert.That(DistanceToHero(sim, enemy), Is.EqualTo(SpawnDistance - enemy.MovementSpeed).Within(0.01f));
        }

        [TestCase(EnemyArchetype.Brute, 0.0f)]
        [TestCase(EnemyArchetype.Brute, 0.9999f)]
        [TestCase(EnemyArchetype.Skirmisher, 0.5f)]
        public void AnEnemy_StopsWithinItsStrikeRangeReducedByTheJitter_NeverOvershooting(EnemyArchetype archetype, float stopRoll)
        {
            var sim = Sim(Profiles.Solo(archetype), new QueuedRollSource(0.5f, stopRoll));
            var enemy = sim.Enemies.Single();
            var tuning = ArenaTuning.Standard();
            var expectedStop = enemy.StrikeRange * (1f - tuning.StopJitter * stopRoll);

            var closest = float.MaxValue;
            for (var i = 0; i < 200; i++)
            {
                sim.Advance(Tick);
                closest = System.Math.Min(closest, DistanceToHero(sim, enemy));
            }

            Assert.That(DistanceToHero(sim, enemy), Is.EqualTo(expectedStop).Within(0.002f));
            Assert.That(closest, Is.GreaterThanOrEqualTo(expectedStop - 0.002f), "it never walked past its stop");
            Assert.That(DistanceToHero(sim, enemy), Is.LessThanOrEqualTo(enemy.StrikeRange));
        }

        private static StatModifier SpeedSetTo(float value) =>
            new(new Vector2Int(-1000, 1000), value, StatModifierType.Overwrite);

        private static bool IsFinite(Coordinate position) => float.IsFinite(position.x) && float.IsFinite(position.z);

        [Test]
        public void AnEnemyWithANegativeMovementSpeed_DoesNotWalkBackwards()
        {
            var sim = Sim(Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0.5f));
            var enemy = sim.Enemies.Single();
            enemy.Stat(StatName.MovementSpeed).AddModifier(SpeedSetTo(-3f));
            var spawned = enemy.Position;

            for (var i = 0; i < 20; i++) sim.Advance(Tick);

            Assert.That(enemy.MovementSpeed, Is.LessThan(0f), "premise: the modifier took the speed below zero");
            Assert.That(enemy.Position, Is.EqualTo(spawned), "it stands where it spawned, neither in nor out");
        }

        [Test]
        public void AnEnemyWithANaNMovementSpeed_KeepsAFinitePosition()
        {
            var sim = Sim(Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0.5f));
            var enemy = sim.Enemies.Single();
            enemy.Stat(StatName.MovementSpeed).AddModifier(SpeedSetTo(float.NaN));
            var spawned = enemy.Position;

            for (var i = 0; i < 20; i++) sim.Advance(Tick);

            Assert.That(float.IsNaN(enemy.MovementSpeed), Is.True, "premise: the modifier made the speed NaN");
            Assert.That(IsFinite(enemy.Position), Is.True, "NaN never reaches the position");
            Assert.That(enemy.Position, Is.EqualTo(spawned));
        }

        [Test]
        public void AnEnemyWithAPositiveSpeed_StillWalksIn_NegativeControlForTheSpeedGuard()
        {
            var sim = Sim(Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0.5f));
            var enemy = sim.Enemies.Single();

            for (var i = 0; i < 20; i++) sim.Advance(Tick);

            Assert.That(DistanceToHero(sim, enemy), Is.LessThan(SpawnDistance));
        }

        [Test]
        public void AnEnemy_ThatSpawnsOnTheHero_DoesNotMove()
        {
            var sim = Sim(Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0.5f), new EncounterTuning());
            var enemy = sim.Enemies.Single();
            var spawned = enemy.Position;

            for (var i = 0; i < 20; i++) sim.Advance(Tick);

            Assert.That(enemy.Position, Is.EqualTo(spawned), "the default arena is collapsed onto the origin: already in range");
        }

        [Test]
        public void EachArchetype_DefinesAStrikeRangeAndAMovementSpeed_AndSkirmishersStandOff()
        {
            var brute = EnemyArchetypes.Of(EnemyArchetype.Brute);
            var skirmisher = EnemyArchetypes.Of(EnemyArchetype.Skirmisher);

            Assert.That(brute.StrikeRange, Is.GreaterThan(0f));
            Assert.That(brute.MovementSpeed, Is.GreaterThan(0f));
            Assert.That(skirmisher.StrikeRange, Is.GreaterThan(brute.StrikeRange), "ranged is a longer Strike Range");
            Assert.That(skirmisher.MovementSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void SpawnBearingAndStopJitter_DrawFromTheMovementStream_NeverTheMainOne()
        {
            (int Main, int Movement) Drawn(IRollSource movement)
            {
                var main = new CountingRolls(new SeededRollSource(7));
                var counted = new CountingRolls(movement);
                var sim = new EncounterSimulation(Passive(), Profiles.Group(EnemyArchetype.Brute, 3), main, Behaviours.Engaging(10),
                    OnTheArena(), bag: null, movementRolls: counted);
                for (var i = 0; i < 50; i++) sim.Advance(Tick);
                return (main.Count, counted.Count);
            }

            var first = Drawn(new SeededRollSource(1));
            var second = Drawn(new SeededRollSource(2));

            Assert.That(first.Movement, Is.EqualTo(6), "a bearing and a stop roll for each of the three spawns");
            Assert.That(first, Is.EqualTo(second), "the main stream is drawn the same however the movement rolls fall");
        }

        private sealed class CountingRolls : IRollSource
        {
            private readonly IRollSource _inner;
            public CountingRolls(IRollSource inner) => _inner = inner;
            public int Count { get; private set; }
            public float Next() { Count++; return _inner.Next(); }
        }
    }
}
