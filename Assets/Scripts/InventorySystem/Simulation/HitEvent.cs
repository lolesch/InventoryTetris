using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// One hit that landed (issue #211): who dealt it, who took it, the damage type, the raw amount (pre-mitigation,
    /// the damage spread already applied) and the amount the target actually lost. Raised by
    /// <see cref="EncounterSimulation.HitLanded"/> for the hero's Strike and Cast and every enemy Strike - the seam
    /// the damage numbers and, later, effects read from. A hit the target mitigates fully still lands, with a
    /// <see cref="LostAmount"/> of 0.
    /// </summary>
    public readonly struct HitEvent
    {
        public HitEvent(ICombatant dealer, ICombatant target, DamageType damageType, float rawAmount, float lostAmount)
        {
            Dealer = dealer;
            Target = target;
            DamageType = damageType;
            RawAmount = rawAmount;
            LostAmount = lostAmount;
        }

        /// <summary>The hero, or the enemy that struck.</summary>
        public ICombatant Dealer { get; }

        /// <summary>The combatant that took the hit: an enemy for the hero's hits, the hero for an enemy's.</summary>
        public ICombatant Target { get; }

        public DamageType DamageType { get; }

        /// <summary>The damage before the target's mitigation, with the damage spread applied.</summary>
        public float RawAmount { get; }

        /// <summary>What the target actually lost: after its mitigation, and no more than it had to lose.</summary>
        public float LostAmount { get; }
    }
}
