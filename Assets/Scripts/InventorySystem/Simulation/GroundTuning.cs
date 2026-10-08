using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The tuning values of the ground the fight takes place on (spatial-combat spec): a flat disk on the
    /// XZ plane around an <see cref="Origin"/> the hero calls home. Enemies spawn <see cref="SpawnMargin"/>
    /// beyond its <see cref="Radius"/> and walk in. Plain tuning, like <see cref="EncounterTuning"/>: a test
    /// overrides what it needs, the <see cref="Standard"/> placeholders are untested starting points.
    ///
    /// A default-constructed ground is <i>collapsed</i> (radius and margin zero): every enemy spawns on the
    /// hero, already within any Strike Range, so the fight behaves as it did before the sim owned position.
    /// <see cref="Standard"/> is the real ground.
    /// </summary>
    public sealed class GroundTuning
    {
        /// <summary>Where the hero stands, and the centre of the ground. Enemies spawn around it.</summary>
        public Coordinate Origin { get; set; }

        /// <summary>Radius of the ground, in ground units.</summary>
        public float Radius { get; set; }

        /// <summary>How far beyond the edge an enemy spawns, so it walks onto the ground.</summary>
        public float SpawnMargin { get; set; }

        /// <summary>
        /// The most an enemy's stop distance is pulled in from its Strike Range, as a fraction of it: the
        /// stop is <c>StrikeRange * (1 - StopJitter * roll)</c>, so it always stands within range.
        /// </summary>
        public float StopJitter { get; set; } = 0.2f;

        /// <summary>
        /// How far a spawn bearing may stray from the middle of the widest gap between living enemies, as a
        /// fraction of that gap (0 = dead centre, 1 = up to its neighbours). Below 1 it never lands on one.
        /// </summary>
        public float BearingJitter { get; set; } = 0.5f;

        /// <summary>
        /// The hero's Strike Range while unarmed. A weapon type will set it later; gear never rolls range.
        /// </summary>
        public float HeroStrikeRange { get; set; } = 1.5f;

        /// <summary>The ground the game plays on: radius 10, spawn margin 2.</summary>
        public static GroundTuning Standard() => new() { Radius = 10f, SpawnMargin = 2f };

        internal void Validate()
        {
            if (Radius < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(Radius), Radius, "Radius cannot be negative.");
            if (SpawnMargin < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(SpawnMargin), SpawnMargin, "Spawn margin cannot be negative.");
            if (StopJitter < 0f || StopJitter >= 1f)
                throw new System.ArgumentOutOfRangeException(nameof(StopJitter), StopJitter, "Stop jitter is a fraction below 1.");
            if (BearingJitter < 0f || BearingJitter > 1f)
                throw new System.ArgumentOutOfRangeException(nameof(BearingJitter), BearingJitter, "Bearing jitter is a fraction of 0..1.");
            if (HeroStrikeRange < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(HeroStrikeRange), HeroStrikeRange, "Strike Range cannot be negative.");
        }
    }
}
