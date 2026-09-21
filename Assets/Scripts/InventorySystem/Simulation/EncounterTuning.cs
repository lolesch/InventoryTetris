using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The combat-wide constants that are <em>not</em> authored per Location — the tick rate,
    /// the inter-Encounter beat, and the Cast's burst ceiling and target count (ADR-0010;
    /// starting points from the issue-#18 <c>/prototype</c>). Defaults match the prototype's
    /// tuning surface; a test overrides them to make a cadence land on round numbers.
    /// </summary>
    public sealed class EncounterTuning
    {
        /// <summary>Seconds per simulation tick — the finest granularity combat can observe.</summary>
        public float Tick { get; set; } = 0.1f;

        /// <summary>Quiet seconds between an Encounter clearing and the next one building.</summary>
        public float Beat { get; set; } = 1.0f;

        /// <summary>
        /// Minimum seconds between two Casts — the burst ceiling. Well below the steady-state
        /// cadence (<c>CastCost / regen</c>) so there is headroom for a threshold burst.
        /// </summary>
        public float CastCadence { get; set; } = 0.35f;

        /// <summary>How many of the highest-HP enemies one Cast hits.</summary>
        public int CastTargets { get; set; } = 3;

        /// <summary>Spiral-of-death clamp handed to the <see cref="CombatClock"/>.</summary>
        public int MaxTicksPerAdvance { get; set; } = 8;

        internal void Validate()
        {
            if (Tick <= 0f)
                throw new ArgumentOutOfRangeException(nameof(Tick), Tick, "Tick must be positive.");
            if (Beat < 0f)
                throw new ArgumentOutOfRangeException(nameof(Beat), Beat, "Beat cannot be negative.");
            if (CastCadence <= 0f)
                throw new ArgumentOutOfRangeException(nameof(CastCadence), CastCadence, "Cast cadence must be positive.");
            if (CastTargets < 1)
                throw new ArgumentOutOfRangeException(nameof(CastTargets), CastTargets, "Cast must hit at least one target.");
            if (MaxTicksPerAdvance < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxTicksPerAdvance), MaxTicksPerAdvance, "Max ticks per advance must be at least 1.");
        }
    }
}
