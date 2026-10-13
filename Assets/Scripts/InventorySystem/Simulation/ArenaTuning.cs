using System;
using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The tuning values of the arena the fight takes place on (spatial-combat spec): a flat disk on the
    /// XZ plane around an <see cref="Origin"/> the hero calls home. Enemies spawn <see cref="SpawnMargin"/>
    /// beyond its <see cref="Radius"/> and walk in. Plain tuning, like <see cref="EncounterTuning"/>: a test
    /// overrides what it needs, the <see cref="Standard"/> placeholders are untested starting points.
    ///
    /// A default-constructed arena is <i>collapsed</i> (radius and margin zero): every enemy spawns on the
    /// hero, already within any Strike Range, so the fight behaves as it did before the sim owned position.
    /// <see cref="Standard"/> is the real arena.
    /// </summary>
    [Serializable]
    public sealed class ArenaTuning
    {
        /// <summary>The largest stop jitter: an enemy always stands within range.</summary>
        public const float MaxStopJitter = 0.99f;

        /// <summary>Where the hero stands, and the centre of the arena. Enemies spawn around it.</summary>
        public Coordinate Origin;

        /// <summary>Radius of the arena, in arena units. Not negative.</summary>
        public float Radius;

        /// <summary>How far beyond the edge an enemy spawns, so it walks onto the arena. Not negative.</summary>
        public float SpawnMargin;

        /// <summary>
        /// The most an enemy's stop distance is pulled in from its Strike Range, as a fraction of it: the
        /// stop is <c>StrikeRange * (1 - StopJitter * roll)</c>, so it always stands within range. 0..<see cref="MaxStopJitter"/>.
        /// </summary>
        public float StopJitter = 0.2f;

        /// <summary>
        /// How far a spawn bearing may stray from the middle of the widest gap between living enemies, as a
        /// fraction of that gap (0 = dead centre, 1 = up to its neighbours). Below 1 it never lands on one. 0..1.
        /// </summary>
        public float BearingJitter = 0.5f;

        /// <summary>
        /// The hero's Strike Range while unarmed, in arena units. A weapon type will set it later; gear never
        /// rolls range. Not negative.
        /// </summary>
        public float HeroStrikeRange = 1.5f;

        /// <summary>
        /// Arena units per second the hero walks for each point of his <c>MovementSpeed</c> stat. The stat is
        /// authored on a scale of hundreds (the default hero has 325), the arena on a scale of tens, so this
        /// converts: 325 at 0.01 is 3.25 units per second, between a Brute (2) and a Skirmisher (3.5). Not negative.
        /// </summary>
        public float MovementSpeedScale = 0.01f;

        /// <summary>The arena the game plays on: radius 10, spawn margin 2.</summary>
        public static ArenaTuning Standard() => new() { Radius = 10f, SpawnMargin = 2f };

        /// <summary>A copy with every value pulled into its range - the one place the ranges are stated.</summary>
        public ArenaTuning Sanitised() => new()
        {
            Origin = Origin,
            Radius = Math.Max(0f, Radius),
            SpawnMargin = Math.Max(0f, SpawnMargin),
            StopJitter = Math.Min(Math.Max(0f, StopJitter), MaxStopJitter),
            BearingJitter = Math.Min(Math.Max(0f, BearingJitter), 1f),
            HeroStrikeRange = Math.Max(0f, HeroStrikeRange),
            MovementSpeedScale = Math.Max(0f, MovementSpeedScale),
        };

        /// <summary>Throws when a value is outside the range <see cref="Sanitised"/> pulls it back into.</summary>
        internal void Validate()
        {
            var sane = Sanitised();

            TuningRange.Require(nameof(Radius), Radius, sane.Radius);
            TuningRange.Require(nameof(SpawnMargin), SpawnMargin, sane.SpawnMargin);
            TuningRange.Require(nameof(StopJitter), StopJitter, sane.StopJitter);
            TuningRange.Require(nameof(BearingJitter), BearingJitter, sane.BearingJitter);
            TuningRange.Require(nameof(HeroStrikeRange), HeroStrikeRange, sane.HeroStrikeRange);
            TuningRange.Require(nameof(MovementSpeedScale), MovementSpeedScale, sane.MovementSpeedScale);
        }
    }
}
