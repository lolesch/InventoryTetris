using System;
using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>How the hero picks the enemy his Cast is aimed at.</summary>
    public enum CastTargeting
    {
        /// <summary>
        /// Of the enemies within Cast Range, the one whose shape would catch the most enemies; a tie goes to the
        /// one nearest the hero, then the earliest spawned.
        /// </summary>
        DensestCluster,
    }

    /// <summary>
    /// What a Cast is (spatial-combat spec): how far it reaches, how it picks its target, and the area it hits
    /// there. A tuning value of <see cref="EncounterTuning"/> now, a skill's data once skills exist. The Cast's
    /// cost and cadence stay where they were - the hero's cast cost and <see cref="EncounterTuning.CastCadence"/>.
    /// </summary>
    public sealed class CastDefinition
    {
        /// <summary>
        /// Cast Range: only enemies within this arena distance of the hero are candidates to aim at. It is not
        /// the shape's reach - an enemy in range may be hit by a shape that spills past it.
        /// </summary>
        public float Range { get; set; } = 7f;

        /// <summary>The pattern that picks the enemy the shape is placed on.</summary>
        public CastTargeting Targeting { get; set; } = CastTargeting.DensestCluster;

        /// <summary>The area one Cast hits, before <see cref="Size"/> scales it.</summary>
        public AreaShape Shape { get; set; } = AreaShape.Disk(2f);

        /// <summary>
        /// The area multiplier applied to <see cref="Shape"/> (<see cref="AreaShape.Scaled"/>): 1 is the shape as
        /// given, 2 is twice the area.
        /// </summary>
        public float Size { get; set; } = 1f;

        /// <summary>Where the shape starts: on the aimed-at enemy, or on the hero pointing at it.</summary>
        public AreaAnchor Anchor { get; set; } = AreaAnchor.Target;

        /// <summary>
        /// The Cast the game plays on: range 7, a disk of radius 2 on the target. Untested starting points, to be
        /// tuned in play.
        /// </summary>
        public static CastDefinition Standard() => new();

        internal void Validate()
        {
            if (Range < 0f)
                throw new ArgumentOutOfRangeException(nameof(Range), Range, "Cast Range cannot be negative.");
            if (Size <= 0f)
                throw new ArgumentOutOfRangeException(nameof(Size), Size, "Cast size must be positive.");
            if (!Enum.IsDefined(typeof(CastTargeting), Targeting))
                throw new ArgumentOutOfRangeException(nameof(Targeting), Targeting, "Unknown Cast targeting.");
        }
    }
}
