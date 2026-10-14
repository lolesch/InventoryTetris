using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The combat-wide constants that are not authored per Location (ADR-0010). <c>new</c> is the quiet
    /// default a test wants; <c>Standard()</c> is what the game plays on.
    /// </summary>
    [Serializable]
    public sealed class EncounterTuning
    {
        /// <summary>The smallest tick: combat time always moves forward.</summary>
        public const float MinTick = 0.001f;

        /// <summary>The smallest Cast cadence: two Casts are always apart.</summary>
        public const float MinCastCadence = 0.001f;

        /// <summary>Seconds per simulation tick — the finest granularity combat can observe. At least <see cref="MinTick"/>.</summary>
        public float Tick = 0.1f;

        /// <summary>Quiet seconds between an Encounter clearing and the next one building. Not negative.</summary>
        public float Beat = 1.0f;

        /// <summary>
        /// Whether the first Encounter waits one spawn delay before its opening bodies arrive. Off by
        /// default so a test sees the Roster the moment the sim is built.
        /// </summary>
        public bool DelayFirstSpawn;

        /// <summary>
        /// Minimum seconds between two Casts, the burst ceiling. Well below the steady-state cadence so a
        /// threshold burst has headroom.
        /// </summary>
        public float CastCadence = 0.35f;

        /// <summary>
        /// The Cast's range, targeting, shape, size and anchor (issue #212).
        /// </summary>
        public CastDefinition Cast = CastDefinition.Standard();

        /// <summary>
        /// The arena the fight takes place on. Collapsed by default so a test sees the fight at once.
        /// </summary>
        public ArenaTuning Arena = new();

        /// <summary>How the hero picks the enemy he fights. A tuning value until skills can supply it.</summary>
        public HeroTargeting HeroTargeting = HeroTargeting.WeightedProximity;

        /// <summary>
        /// How far one hit's damage strays from its base, as a symmetric fraction of it (issue #211): 0.2 is
        /// +-20 %. Zero by default so a test sees base damage. 0..1.
        /// </summary>
        public float DamageSpread;

        /// <summary>The spread the game plays on: +-20 %. A placeholder, an untested starting point.</summary>
        public const float StandardDamageSpread = 0.2f;

        /// <summary>Spiral-of-death clamp handed to the <see cref="CombatClock"/>. At least 1.</summary>
        public int MaxTicksPerAdvance = 8;

        /// <summary>
        /// The tuning the game plays on; <c>GameConfig</c> starts from it, so a value changed here moves the game.
        /// </summary>
        public static EncounterTuning Standard() => new()
        {
            DelayFirstSpawn = true,
            Arena = ArenaTuning.Standard(),
            Cast = CastDefinition.Standard(),
            DamageSpread = StandardDamageSpread,
        };

        /// <summary>
        /// A copy with every value, and the nested Arena and Cast, pulled into its range. A missing Arena or
        /// Cast becomes its default.
        /// </summary>
        public EncounterTuning Sanitised() => new()
        {
            Tick = Math.Max(MinTick, Tick),
            Beat = Math.Max(0f, Beat),
            DelayFirstSpawn = DelayFirstSpawn,
            CastCadence = Math.Max(MinCastCadence, CastCadence),
            Cast = (Cast ?? new CastDefinition()).Sanitised(),
            Arena = (Arena ?? new ArenaTuning()).Sanitised(),
            HeroTargeting = HeroTargeting,
            DamageSpread = Math.Clamp(DamageSpread, 0f, 1f),
            MaxTicksPerAdvance = Math.Max(1, MaxTicksPerAdvance),
        };

        /// <summary>Throws when a value is outside its range.</summary>
        internal void Validate()
        {
            if (Cast == null)
                throw new ArgumentNullException(nameof(Cast));
            if (Arena == null)
                throw new ArgumentNullException(nameof(Arena));

            var sane = Sanitised();

            TuningRange.Require(nameof(Tick), Tick, sane.Tick);
            TuningRange.Require(nameof(Beat), Beat, sane.Beat);
            TuningRange.Require(nameof(CastCadence), CastCadence, sane.CastCadence);
            TuningRange.Require(nameof(DamageSpread), DamageSpread, sane.DamageSpread);
            if (MaxTicksPerAdvance != sane.MaxTicksPerAdvance)
                throw new ArgumentOutOfRangeException(nameof(MaxTicksPerAdvance), MaxTicksPerAdvance, "Max ticks per advance must be at least 1.");
            Cast.Validate();
            Arena.Validate();
        }
    }
}
