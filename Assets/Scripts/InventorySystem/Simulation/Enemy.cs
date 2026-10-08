using System;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;

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
        internal Enemy(EnemyArchetype archetype, int sourceLevel)
        {
            var stats = EnemyArchetypes.Of(archetype);
            Archetype = archetype;
            HealthResource = new CharacterResource(StatName.Health, stats.Health.At(sourceLevel));
            ArmorPercent = stats.ArmorPercent.At(sourceLevel);
            StrikeDamage = stats.Damage.At(sourceLevel);
            AttackSpeed = stats.AttackSpeed;
            StrikeRange = stats.StrikeRange;
            MovementSpeed = stats.MovementSpeed;
            Xp = stats.Xp.At(sourceLevel);
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

        /// <summary>How far from the hero this enemy can Strike, in ground units.</summary>
        public float StrikeRange { get; }

        /// <summary>Ground units this enemy walks per second while chasing the hero.</summary>
        public float MovementSpeed { get; }

        /// <summary>
        /// Where this enemy stands on the ground. The Encounter owns it and moves it on sim time; the arena
        /// only reads it. Starts on the spawn ring.
        /// </summary>
        public Coordinate Position { get; internal set; }

        /// <summary>
        /// The bearing, in degrees in 0..360 (positive from +x toward +z), this enemy spawned on - seen from the
        /// ground's origin. Kept until it falls.
        /// </summary>
        public float Bearing { get; internal set; }

        /// <summary>
        /// How far from the hero this enemy stops walking: its Strike Range pulled in by a seeded jitter drawn
        /// once at spawn, so it always ends up within range.
        /// </summary>
        internal float StopDistance { get; set; }

        /// <summary>Seconds banked toward this enemy's next Strike. The Encounter advances it.</summary>
        internal float StrikeTimer { get; set; }

        /// <summary>Monotonic spawn order — the deterministic tie-break when two enemies share HP.</summary>
        internal int SpawnIndex { get; set; }

        /// <summary>
        /// This enemy's health — the same type the hero's resource globes render, and the only
        /// copy of the number. A bar subscribes to its <c>CurrentHasChanged</c>; that event
        /// raises <i>before</i> <c>CurrentValue</c> is written, so a handler reads the new value
        /// from its arguments, not from here.
        /// </summary>
        public CharacterResource HealthResource { get; }

        public float Health => HealthResource.CurrentValue;
        public float MaxHealth => HealthResource.TotalValue;
        public float HealthFraction => MaxHealth <= 0f ? 0f : Math.Max(0f, Math.Min(1f, Health / MaxHealth));
        public bool IsDown => Health <= 0f;

        public void ReceivePhysical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            HealthResource.RemoveFromCurrent(rawDamage * (1f - ArmorPercent * 0.01f));
        }

        public void ReceiveMagical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            HealthResource.RemoveFromCurrent(rawDamage); // enemies have no magic resist (ADR-0010)
        }

        public void Regenerate(float deltaSeconds) { /* enemies do not regenerate */ }
    }
}
