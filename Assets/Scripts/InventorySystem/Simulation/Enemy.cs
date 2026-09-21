using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// One enemy body in an Encounter — a parametric <see cref="EnemyArchetype"/> resolved at
    /// a source level. Strike-only: it hits the hero on its own <c>1 / AttackSpeed</c> cadence
    /// and never Casts. The Encounter owns its lifetime; it is exposed read-only so the UI can
    /// draw its globe.
    /// </summary>
    public sealed class Enemy : ICombatant
    {
        private float _health;

        internal Enemy(EnemyArchetype archetype, int sourceLevel)
        {
            var stats = EnemyArchetypes.Of(archetype);
            Archetype = archetype;
            MaxHealth = stats.Health.At(sourceLevel);
            ArmorPercent = stats.ArmorPercent.At(sourceLevel);
            StrikeDamage = stats.Damage.At(sourceLevel);
            AttackSpeed = stats.AttackSpeed;
            Xp = stats.Xp.At(sourceLevel);
            _health = MaxHealth;
        }

        public EnemyArchetype Archetype { get; }

        /// <summary>Pre-mitigation damage this enemy's Strike deals to the hero.</summary>
        public float StrikeDamage { get; }

        /// <summary>Strikes per second.</summary>
        public float AttackSpeed { get; }

        /// <summary>Percent physical mitigation against the hero's Strike.</summary>
        public float ArmorPercent { get; }

        /// <summary>XP this body adds to the Encounter pot when it falls, before the balance term.</summary>
        public float Xp { get; }

        /// <summary>Seconds banked toward this enemy's next Strike. The Encounter advances it.</summary>
        internal float StrikeTimer { get; set; }

        /// <summary>Monotonic spawn order — the deterministic tie-break when two enemies share HP.</summary>
        internal int SpawnIndex { get; set; }

        public float Health => _health;
        public float MaxHealth { get; }
        public float HealthFraction => MaxHealth <= 0f ? 0f : Math.Max(0f, Math.Min(1f, _health / MaxHealth));
        public bool IsDown => _health <= 0f;

        public void ReceivePhysical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            _health = Math.Max(0f, _health - rawDamage * (1f - ArmorPercent * 0.01f));
        }

        public void ReceiveMagical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            _health = Math.Max(0f, _health - rawDamage); // enemies have no magic resist (ADR-0010)
        }

        public void Regenerate(float deltaSeconds) { /* enemies do not regenerate */ }
    }
}
