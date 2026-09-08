using System;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Locations
{
    /// <summary>
    /// Local fakes for the Locations tests. Each test assembly owns its own copy (the same
    /// convention <c>InventorySystem.Simulation.Tests</c> follows), so a test here never depends
    /// on another assembly's test code.
    /// </summary>
    internal static class LocationFakes
    {
        /// <summary>
        /// An <see cref="IRollSource"/> that hands back a fixed script of rolls in order and
        /// throws once it runs dry — a test that miscounts how many rolls a path consumes fails
        /// loudly.
        /// </summary>
        internal sealed class QueuedRollSource : IRollSource
        {
            private readonly float[] _rolls;
            private int _next;

            public QueuedRollSource(params float[] rolls) => _rolls = rolls ?? Array.Empty<float>();

            public float Next()
            {
                if (_next >= _rolls.Length)
                    throw new InvalidOperationException(
                        $"the roll script ran dry after {_rolls.Length} rolls — the path under test consumes more");
                return _rolls[_next++];
            }
        }

        /// <summary><see cref="IRollSource"/> that always returns the same value.</summary>
        internal sealed class ConstantRollSource : IRollSource
        {
            private readonly float _value;

            public ConstantRollSource(float value) => _value = value;

            public float Next() => _value;
        }

        /// <summary>
        /// A hand-tunable <see cref="IHeroCombatant"/> — enough of the fake Hero the simulation
        /// tests use to drive an <see cref="EncounterSimulation"/> from an authored profile.
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
            public float MagicFind { get; set; }
            public float IncreasedItemQuantity { get; set; }

            public float HealthFraction => MaxHealth <= 0f ? 0f : Clamp01(Health / MaxHealth);
            public float ResourceFraction => MaxResource <= 0f ? 0f : Clamp01(Resource / MaxResource);
            public bool IsDown => Health <= 0f;

            public void ReceivePhysical(float rawDamage)
            {
                if (rawDamage <= 0f) return;
                Health = Math.Max(0f, Health - rawDamage * (1f - ArmorPercent * 0.01f));
            }

            public void ReceiveMagical(float rawDamage)
            {
                if (rawDamage <= 0f) return;
                Health = Math.Max(0f, Health - rawDamage);
            }

            public void SpendResource(float amount) => Resource = Math.Max(0f, Resource - amount);

            public void Regenerate(float deltaSeconds)
            {
                if (Health > 0f)
                    Health = Math.Min(MaxHealth, Health + HealthRegenPerSecond * deltaSeconds);
                Resource = Math.Min(MaxResource, Resource + ResourceRegenPerSecond * deltaSeconds);
            }

            private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>
        /// The neutral <see cref="HeroBehaviour"/> these tests steer an Encounter with: the named
        /// Engagement, no retreat trigger, no Cast hold. Locations tests are about the profile a
        /// <c>LocationConfig</c> maps onto, not about the player's steering.
        /// </summary>
        internal static HeroBehaviour Engaging(int engagement) => new() { Engagement = engagement };
    }
}