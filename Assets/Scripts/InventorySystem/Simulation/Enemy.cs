using System;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// One enemy body in an Encounter - a parametric <see cref="EnemyArchetype"/> resolved at
    /// a source level. Strike-only: it hits the hero on its own <c>1 / AttackSpeed</c> cadence
    /// and never Casts. The Encounter owns its lifetime; it is exposed read-only so the UI can
    /// draw its globe.
    ///
    /// Stat-backed: it carries a modifiable <see cref="CharacterStat"/> for every stat its archetype
    /// defines (Health, Armor, MagicResist, the damage stat of its <see cref="StrikeDamageType"/>,
    /// AttackSpeed, MovementSpeed), based on the archetype curves at the source level. Everything
    /// the fight reads - mitigation, Strike damage, cadence, walking speed - is read live off those
    /// stats, so a modifier added later (an effect, a piece of gear) changes the fight on the spot.
    /// It has no regeneration and no resource pool, so it carries no such stat. Strike Range is a
    /// base property of the archetype, not a stat, as for the hero.
    /// </summary>
    public sealed class Enemy : ICombatant
    {
        private readonly CharacterStat _armor;
        private readonly CharacterStat _magicResist;
        private readonly CharacterStat _damage;
        private readonly CharacterStat _attackSpeed;
        private readonly CharacterStat _movementSpeed;
        private readonly CharacterStat[] _stats;

        internal Enemy(EnemyArchetype archetype, int sourceLevel)
        {
            var stats = EnemyArchetypes.Of(archetype);
            Archetype = archetype;
            StrikeDamageType = stats.DamageType;
            HealthResource = new CharacterResource(StatName.Health, stats.Health.At(sourceLevel));
            _armor = new CharacterStat(StatName.Armor, stats.ArmorPercent.At(sourceLevel));
            _magicResist = new CharacterStat(StatName.MagicResist, stats.MagicResistPercent.At(sourceLevel));
            _damage = new CharacterStat(
                stats.DamageType == DamageType.MagicalDamage ? StatName.MagicalDamage : StatName.PhysicalDamage,
                stats.Damage.At(sourceLevel));
            _attackSpeed = new CharacterStat(StatName.AttackSpeed, stats.AttackSpeed);
            _movementSpeed = new CharacterStat(StatName.MovementSpeed, stats.MovementSpeed);
            _stats = new CharacterStat[] { HealthResource, _armor, _magicResist, _damage, _attackSpeed, _movementSpeed };
            StrikeRange = stats.StrikeRange;
            Xp = stats.Xp.At(sourceLevel);
        }

        public EnemyArchetype Archetype { get; }

        /// <summary>
        /// The enemy's modifiable stat <paramref name="name"/>, or null when its archetype defines none (a
        /// Brute has no <c>MagicalDamage</c>; no enemy has regeneration, a Resource pool or a Shield).
        /// Health is the same <see cref="HealthResource"/> the health bar renders.
        /// </summary>
        public CharacterStat Stat(StatName name)
        {
            for (var i = 0; i < _stats.Length; i++)
                if (_stats[i].Stat == name)
                    return _stats[i];
            return null;
        }

        /// <summary>Pre-mitigation damage this enemy's Strike deals to the hero, off its damage stat.</summary>
        public float StrikeDamage => _damage.TotalValue;

        /// <summary>The type of damage this enemy's Strike deals - the hero mitigates it with the matching resist.</summary>
        public DamageType StrikeDamageType { get; }

        /// <summary>Strikes per second.</summary>
        public float AttackSpeed => _attackSpeed.TotalValue;

        /// <summary>Percent physical mitigation against the hero's Strike.</summary>
        public float ArmorPercent => _armor.TotalValue;

        /// <summary>Percent magical mitigation against the hero's Cast.</summary>
        public float MagicResistPercent => _magicResist.TotalValue;

        /// <summary>XP this body adds to the Encounter pot when it falls, before the balance term.</summary>
        public float Xp { get; }

        /// <summary>How far from the hero this enemy can Strike, in ground units.</summary>
        public float StrikeRange { get; }

        /// <summary>Ground units this enemy walks per second while chasing the hero.</summary>
        public float MovementSpeed => _movementSpeed.TotalValue;

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

        public float ReceivePhysical(float rawDamage) => TakeHit(rawDamage, ArmorPercent);

        public float ReceiveMagical(float rawDamage) => TakeHit(rawDamage, MagicResistPercent);

        // The resist clamps to 0..100 percent, as the hero's does: past a full resist a hit mitigates to nothing,
        // never to a heal.
        // Returns what the health actually lost: the mitigated hit, but no more than it had left.
        private float TakeHit(float rawDamage, float resistPercent)
        {
            if (rawDamage <= 0f) return 0f;
            var resist = Math.Max(0f, Math.Min(100f, resistPercent));
            var before = HealthResource.CurrentValue;
            HealthResource.RemoveFromCurrent(rawDamage * (1f - resist * 0.01f));
            return before - HealthResource.CurrentValue;
        }

        public void Regenerate(float deltaSeconds) { /* enemies do not regenerate */ }
    }
}
