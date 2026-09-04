using System;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="IRollSource"/> that hands back a fixed script of rolls in order and throws
    /// once it runs dry — a test that miscounts how many rolls the Encounter consumes fails
    /// loudly. (Same shape as <c>InventorySystem.Items.Tests</c>' internal copy; each test
    /// assembly owns its own so neither depends on the other's test code.)
    /// </summary>
    internal sealed class QueuedRollSource : IRollSource
    {
        private readonly float[] _rolls;
        private int _next;

        public QueuedRollSource(params float[] rolls) => _rolls = rolls ?? Array.Empty<float>();

        public int Consumed => _next;

        public float Next()
        {
            if (_next >= _rolls.Length)
                throw new InvalidOperationException(
                    $"the roll script ran dry after {_rolls.Length} rolls — the path under test consumes more");
            return _rolls[_next++];
        }
    }

    /// <summary><see cref="IRollSource"/> backed by a seeded <see cref="Random"/> — repeatable.</summary>
    internal sealed class SeededRollSource : IRollSource
    {
        private readonly Random _rng;

        public SeededRollSource(int seed) => _rng = new Random(seed);

        public float Next() => (float)_rng.NextDouble();
    }

    /// <summary><see cref="IRollSource"/> that always returns the same value.</summary>
    internal sealed class ConstantRollSource : IRollSource
    {
        private readonly float _value;

        public ConstantRollSource(float value) => _value = value;

        public float Next() => _value;
    }

    /// <summary>
    /// A hand-tunable <see cref="IHeroCombatant"/>. Health and Resource are plain settable
    /// pools; <see cref="Regenerate"/> tops them up at the configured per-second rates and
    /// counts its calls; <see cref="ReceivePhysical"/> applies <see cref="ArmorPercent"/>.
    /// </summary>
    internal sealed class FakeHero : IHeroCombatant
    {
        public float MaxHealth { get; set; } = 1_000_000f;
        public float Health { get; set; } = 1_000_000f;
        public float MaxResource { get; set; } = 100f;
        public float Resource { get; set; }
        public float HealthRegenPerSecond { get; set; }
        public float ResourceRegenPerSecond { get; set; }
        public float ArmorPercent { get; set; }

        public float PhysicalDamage { get; set; } = 10f;
        public float AttackSpeed { get; set; } = 1f;
        public float MagicalDamage { get; set; } = 5f;
        public float CastCost { get; set; } = 16f;
        public int Level { get; set; } = 1;

        public int RegenerateCalls { get; private set; }
        public float PhysicalDamageTaken { get; private set; }

        public float HealthFraction => MaxHealth <= 0f ? 0f : Clamp01(Health / MaxHealth);
        public float ResourceFraction => MaxResource <= 0f ? 0f : Clamp01(Resource / MaxResource);
        public bool IsDown => Health <= 0f;

        public void ReceivePhysical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            var dealt = rawDamage * (1f - ArmorPercent * 0.01f);
            PhysicalDamageTaken += dealt;
            Health = Math.Max(0f, Health - dealt);
        }

        public void ReceiveMagical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            Health = Math.Max(0f, Health - rawDamage);
        }

        public void SpendResource(float amount) => Resource = Math.Max(0f, Resource - amount);

        public void Regenerate(float deltaSeconds)
        {
            RegenerateCalls++;
            if (Health > 0f)
                Health = Math.Min(MaxHealth, Health + HealthRegenPerSecond * deltaSeconds);
            Resource = Math.Min(MaxResource, Resource + ResourceRegenPerSecond * deltaSeconds);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    internal static class Profiles
    {
        /// <summary>A single enemy of one archetype, no further spawns — isolates one target.</summary>
        public static EncounterProfile Solo(EnemyArchetype archetype, int sourceLevel = 5) =>
            Group(archetype, 1, sourceLevel);

        /// <summary>
        /// <paramref name="count"/> enemies of one archetype, all present at open, no further
        /// spawns — the Roster is spent the moment the Encounter begins.
        /// </summary>
        public static EncounterProfile Group(EnemyArchetype archetype, int count, int sourceLevel = 5) => new(
            sourceLevel: sourceLevel,
            packed: archetype,
            rosterBrute: new IntRange(archetype == EnemyArchetype.Brute ? count : 0),
            rosterSkirmisher: new IntRange(archetype == EnemyArchetype.Skirmisher ? count : 0),
            packBatch: new IntRange(1),
            packedSpawnWeight: 1f,
            spawnInterval: 100f,
            spawnJitter: 0f,
            initialSpawn: count);
    }
}
