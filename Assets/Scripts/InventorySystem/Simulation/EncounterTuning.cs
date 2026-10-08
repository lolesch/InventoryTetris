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
        /// Whether the first Encounter waits one spawn delay — the Location's own
        /// <c>SpawnInterval ± SpawnJitter</c> — before its opening bodies arrive, instead of
        /// having them at open. Off by default so a test sees the Roster the moment the sim is
        /// built; the <c>SimulationService</c> turns it on for a real Send or Relocate.
        /// </summary>
        public bool DelayFirstSpawn { get; set; }

        /// <summary>
        /// Minimum seconds between two Casts — the burst ceiling. Well below the steady-state
        /// cadence (<c>CastCost / regen</c>) so there is headroom for a threshold burst.
        /// </summary>
        public float CastCadence { get; set; } = 0.35f;

        /// <summary>How many of the highest-HP enemies one Cast hits.</summary>
        public int CastTargets { get; set; } = 3;

        /// <summary>
        /// The ground the fight takes place on. Collapsed by default - every enemy spawns on the hero, so a
        /// test sees the fight at once, as with <see cref="DelayFirstSpawn"/>; the <c>SimulationService</c>
        /// plays on <see cref="GroundTuning.Standard"/>.
        /// </summary>
        public GroundTuning Ground { get; set; } = new();

        /// <summary>
        /// How far one hit's damage strays from its base, as a symmetric fraction of it (issue #211): each hit
        /// rolls a factor of <c>1 + (2 * roll - 1) * DamageSpread</c> from the hit stream, so 0.2 is +-20 %.
        /// Zero by default - every figure is its base damage, as for a test; the <c>SimulationService</c> plays on
        /// <see cref="StandardDamageSpread"/>. Weapons will carry real minimum and maximum damage later.
        /// </summary>
        public float DamageSpread { get; set; }

        /// <summary>The spread the game plays on: +-20 %. A placeholder, an untested starting point.</summary>
        public const float StandardDamageSpread = 0.2f;

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
            if (DamageSpread < 0f || DamageSpread > 1f)
                throw new ArgumentOutOfRangeException(nameof(DamageSpread), DamageSpread, "Damage spread is a fraction of 0..1.");
            if (MaxTicksPerAdvance < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxTicksPerAdvance), MaxTicksPerAdvance, "Max ticks per advance must be at least 1.");
            if (Ground == null)
                throw new ArgumentNullException(nameof(Ground));
            Ground.Validate();
        }
    }
}
