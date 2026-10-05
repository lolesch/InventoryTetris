using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The hero side of the Encounter sim (issue #43) — a thin adapter that reads every value
    /// <em>live</em> off a <see cref="Hero"/>'s stats and resources, so a mid-fight
    /// re-gear changes the fight on the spot (spec story 10). It holds no snapshot of its own.
    ///
    /// Outgoing damage is exposed as the raw <c>PhysicalDamage</c> / <c>MagicalDamage</c> stat
    /// values — the sim owns the Strike / Cast cadence, so the adapter deliberately does
    /// <em>not</em> fold in <see cref="Hero.CalculateDamageOutput"/>'s <c>AttackSpeed</c> term, which would
    /// double-count against a real cadence (ADR-0010). Incoming damage and Resource spend route
    /// through the existing <see cref="Hero"/> paths so the globes reflect sim state;
    /// <see cref="Regenerate"/> forwards to <see cref="Hero.Regenerate"/>, though the simulation
    /// service's tick drives regen directly at sim speed (issue #45).
    /// </summary>
    public sealed class HeroCombatant : IHeroCombatant
    {
        /// <summary>
        /// MVP flat Cast cost (spec <i>HeroBehaviour</i> / ADR-0010 — "flat for the MVP"; the
        /// <c>/prototype</c> starting point is 16). There is no gear stat for it yet, so it is a
        /// constructor parameter (read from <c>GameConfig</c> by the simulation service) rather than a hidden constant.
        /// </summary>
        public const float DefaultCastCost = 16f;

        private readonly Hero _character;
        private readonly float _castCost;

        public HeroCombatant(Hero character, float castCost = DefaultCastCost)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            if (castCost < 0f) throw new ArgumentOutOfRangeException(nameof(castCost), castCost, "Cast cost cannot be negative.");
            _castCost = castCost;
        }

        private CharacterResource HealthPool => _character.GetResource(StatName.Health);
        private CharacterResource ResourcePool => _character.GetResource(StatName.Resource);

        public float Health => HealthPool.CurrentValue;
        public float MaxHealth => HealthPool.TotalValue;
        public float HealthFraction => Fraction(HealthPool);
        public bool IsDown => _character.IsDead;

        public float Resource => ResourcePool.CurrentValue;
        public float MaxResource => ResourcePool.TotalValue;
        public float ResourceFraction => Fraction(ResourcePool);

        public float PhysicalDamage => _character.GetStatValue(StatName.PhysicalDamage);
        public float AttackSpeed => _character.GetStatValue(StatName.AttackSpeed);
        public float MagicalDamage => _character.GetStatValue(StatName.MagicalDamage);
        public float CastCost => _castCost;

        public int Level => (int)_character.Level;

        public float MagicFind => _character.GetStatValue(StatName.IncreasedItemRarity);
        public float IncreasedItemQuantity => _character.GetStatValue(StatName.IncreasedItemQuantity);

        public void ReceivePhysical(float rawDamage)
        {
            if (rawDamage > 0f)
                _character.ReceiveDamage(DamageType.PhysicalDamage, rawDamage);
        }

        public void ReceiveMagical(float rawDamage)
        {
            if (rawDamage > 0f)
                _character.ReceiveDamage(DamageType.MagicalDamage, rawDamage);
        }

        public void SpendResource(float amount)
        {
            if (amount > 0f)
                _ = ResourcePool.RemoveFromCurrent(amount);
        }

        public void Regenerate(float deltaSeconds) => _character.Regenerate(deltaSeconds);

        private static float Fraction(CharacterResource pool)
        {
            var total = pool.TotalValue;
            if (total <= 0f) return 0f;
            var f = pool.CurrentValue / total;
            return f < 0f ? 0f : f > 1f ? 1f : f;
        }
    }
}
