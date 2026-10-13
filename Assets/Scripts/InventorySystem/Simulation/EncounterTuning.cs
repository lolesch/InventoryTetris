using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The combat-wide constants that are <em>not</em> authored per Location — the tick rate,
    /// the inter-Encounter beat, and the Cast's burst ceiling and definition (ADR-0010;
    /// starting points from the issue-#18 <c>/prototype</c>). Defaults match the prototype's
    /// tuning surface; a test overrides them to make a cadence land on round numbers.
    ///
    /// A plain <c>[Serializable]</c> class of public fields - the Simulation assembly stays engine-free - that
    /// owns its defaults (<c>new</c> is the quiet one a test wants, <see cref="Standard"/> the one the game plays
    /// on) and its ranges (<see cref="Sanitised"/>), so a tunable is declared here once and <c>GameConfig</c>
    /// merely holds the object.
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
        /// Whether the first Encounter waits one spawn delay — the Location's own
        /// <c>SpawnInterval ± SpawnJitter</c> — before its opening bodies arrive, instead of
        /// having them at open. Off by default so a test sees the Roster the moment the sim is
        /// built; <see cref="Standard"/> turns it on for a real Send or Relocate.
        /// </summary>
        public bool DelayFirstSpawn;

        /// <summary>
        /// Minimum seconds between two Casts — the burst ceiling. Well below the steady-state
        /// cadence (<c>CastCost / regen</c>) so there is headroom for a threshold burst. At least <see cref="MinCastCadence"/>.
        /// </summary>
        public float CastCadence = 0.35f;

        /// <summary>
        /// The Cast's range, targeting, shape, size and anchor (issue #212). On the collapsed default
        /// <see cref="Arena"/> every enemy stands on the hero, so it catches them all.
        /// </summary>
        public CastDefinition Cast = CastDefinition.Standard();

        /// <summary>
        /// The arena the fight takes place on. Collapsed by default - every enemy spawns on the hero, so a
        /// test sees the fight at once, as with <see cref="DelayFirstSpawn"/>; <see cref="Standard"/> plays on
        /// <see cref="ArenaTuning.Standard"/>.
        /// </summary>
        public ArenaTuning Arena = new();

        /// <summary>How the hero picks the enemy he fights. A tuning value until skills can supply it.</summary>
        public HeroTargeting HeroTargeting = HeroTargeting.WeightedProximity;

        /// <summary>
        /// How far one hit's damage strays from its base, as a symmetric fraction of it (issue #211): each hit
        /// rolls a factor of <c>1 + (2 * roll - 1) * DamageSpread</c> from the hit stream, so 0.2 is +-20 %.
        /// Zero by default - every figure is its base damage, as for a test; <see cref="Standard"/> plays on
        /// <see cref="StandardDamageSpread"/>. Weapons will carry real minimum and maximum damage later. 0..1.
        /// </summary>
        public float DamageSpread;

        /// <summary>The spread the game plays on: +-20 %. A placeholder, an untested starting point.</summary>
        public const float StandardDamageSpread = 0.2f;

        /// <summary>Spiral-of-death clamp handed to the <see cref="CombatClock"/>. At least 1.</summary>
        public int MaxTicksPerAdvance = 8;

        /// <summary>
        /// The tuning the game plays on: the first Encounter waits one spawn delay, on the real arena and Cast,
        /// with the damage spread. <c>GameConfig</c> starts from it, so a value changed here moves the game.
        /// </summary>
        public static EncounterTuning Standard() => new()
        {
            DelayFirstSpawn = true,
            Arena = ArenaTuning.Standard(),
            Cast = CastDefinition.Standard(),
            DamageSpread = StandardDamageSpread,
        };

        /// <summary>
        /// A copy with every value, and the <see cref="Arena"/> and <see cref="Cast"/> it nests, pulled into its
        /// range - the one place the ranges are stated. A missing Arena or Cast becomes its default.
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
            DamageSpread = Math.Min(Math.Max(0f, DamageSpread), 1f),
            MaxTicksPerAdvance = Math.Max(1, MaxTicksPerAdvance),
        };

        /// <summary>Throws when a value is outside the range <see cref="Sanitised"/> pulls it back into.</summary>
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
