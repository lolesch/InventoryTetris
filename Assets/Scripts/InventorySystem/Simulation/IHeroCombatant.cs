namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The hero side of the fight. Adds the two attacks' live stats and the Resource pool to
    /// <see cref="ICombatant"/>; the Encounter turns these into cadences and damage each tick
    /// (Strike on <c>1 / AttackSpeed</c>, Cast paced by Resource against <see cref="CastCost"/>).
    ///
    /// Every value is read live, not snapshotted — re-gearing mid-Encounter is meant to change
    /// the fight on the spot (spec story 10). The runtime adapter reads these off
    /// <c>BaseCharacter</c> stats; a test supplies a fake.
    /// </summary>
    public interface IHeroCombatant : ICombatant
    {
        /// <summary>Current Resource — the Cast's fuel.</summary>
        float Resource { get; }

        /// <summary>Full Resource — the denominator for <see cref="ResourceFraction"/> and the globe.</summary>
        float MaxResource { get; }

        /// <summary><see cref="Resource"/> / <see cref="MaxResource"/>, clamped to <c>[0, 1]</c>.</summary>
        float ResourceFraction { get; }

        /// <summary>Flat physical damage the Strike deals to its single target.</summary>
        float PhysicalDamage { get; }

        /// <summary>Strikes per second. The Strike's cadence is <c>1 / AttackSpeed</c>.</summary>
        float AttackSpeed { get; }

        /// <summary>Flat magical damage the Cast deals to <em>each</em> of its targets.</summary>
        float MagicalDamage { get; }

        /// <summary>
        /// Resource spent per Cast. Flat for the MVP; a future <c>CastCostReduction</c> stat
        /// lowers it (ADR-0010). The steady-state Cast cadence emerges as
        /// <c>CastCost / (Resource regen per second)</c>.
        /// </summary>
        float CastCost { get; }

        /// <summary>Hero level — the <c>(SourceLevel - Level)</c> term that balances settled XP.</summary>
        int Level { get; }

        /// <summary>Spend <paramref name="amount"/> of Resource on a Cast. Never called for more than <see cref="Resource"/> holds.</summary>
        void SpendResource(float amount);
    }
}
