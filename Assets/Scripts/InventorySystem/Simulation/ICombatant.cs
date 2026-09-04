namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The Encounter simulation's view of a fighter — the hero or one enemy. The sim owns
    /// targeting and cadence timing; a combatant owns its own health and mitigates its own
    /// incoming damage, so the sim never needs a defender's Armor / resist numbers.
    ///
    /// Health is exposed in absolute terms because targeting is by absolute HP (the Strike
    /// picks the single lowest-HP enemy, the Cast the highest-HP — ADR-0010), not by
    /// fraction: a 60-HP Skirmisher at 90 % outranks a 172-HP Brute at 50 %.
    /// </summary>
    public interface ICombatant
    {
        /// <summary>Current absolute health. Reaches 0 when the combatant is down.</summary>
        float Health { get; }

        /// <summary>Full health — the denominator for <see cref="HealthFraction"/> and the globe.</summary>
        float MaxHealth { get; }

        /// <summary><see cref="Health"/> / <see cref="MaxHealth"/>, clamped to <c>[0, 1]</c>.</summary>
        float HealthFraction { get; }

        /// <summary>True once <see cref="Health"/> has reached 0 — out of the fight.</summary>
        bool IsDown { get; }

        /// <summary>
        /// Take a physical hit. <paramref name="rawDamage"/> is pre-mitigation; the combatant
        /// applies its own Armor. The hero adapter routes this through the live
        /// <c>BaseCharacter.ReceiveDamageFrom</c> path.
        /// </summary>
        void ReceivePhysical(float rawDamage);

        /// <summary>
        /// Take a magical hit. <paramref name="rawDamage"/> is pre-mitigation; the combatant
        /// applies its own magic resist (enemies have none — ADR-0010).
        /// </summary>
        void ReceiveMagical(float rawDamage);

        /// <summary>
        /// Advance this combatant's own regeneration by one tick. The sim calls it once per
        /// tick for every combatant; enemies do not regenerate.
        /// </summary>
        void Regenerate(float deltaSeconds);
    }
}
